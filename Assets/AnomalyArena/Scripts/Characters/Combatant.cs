using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Shared implementation for the player and enemies: HP, damage and protection, rigidbody-impulse knockback, wall damage, falling into gaps, state machine, holding a weapon.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public abstract class Combatant : MonoBehaviour, IWeaponHolder, IDamageDealer
    {
        [Header("Stats")]
        public float maxHp = 10f;
        public float moveSpeed = 6f;
        public float radius = 0.5f;
        [Tooltip("Multiplier on knockback distance (0.5 for large enemies)")] public float knockbackScale = 1f;
        [Tooltip("Protection time after being hit; attack damage is ignored during it (wall damage is not)")] public float protectionTime = 0f;

        [Header("Refs")]
        public Renderer bodyRenderer;
        public Transform hand;

        public float Hp { get; protected set; }
        public CharacterState State { get; private set; } = CharacterState.Normal;
        public DeathCause DeathCause { get; private set; }
        public Weapon Weapon { get; private set; }
        public bool Protected => protectTimer > 0f;

        public event Action<Combatant> Eliminated;

        protected Rigidbody rb;
        protected Vector3 knockVel;
        /// <summary>Push added on top of normal movement (burst-fire recoil), decaying with the same factor as knockback.</summary>
        Vector3 pushVel;
        protected float weaponCooldown;
        KnockKind knockKind;
        float protectTimer;
        float fallTimer;
        bool eliminated;

        protected static GameManager GM => GameManager.Instance;

        // ───── IWeaponHolder ─────
        public Transform Root => transform;
        public Vector3 Position => rb ? rb.position : transform.position;
        public float Radius => radius;
        public bool IsAlive => State != CharacterState.Dead && State != CharacterState.Falling;
        public IWeaponHolder Owner => this;
        public abstract Team Team { get; }
        public abstract Vector3 AimDirection { get; }
        /// <summary>Damage this corpse deals when carried back by the throwing knife (= its attack damage when alive).</summary>
        public virtual float CorpseDamage => 0f;

        /// <summary>The enemy this character is holding with the hook and can use as cover.</summary>
        public Combatant HeldShield => Weapon != null && Weapon.Active is Hook h ? h.HeldTarget : null;
        public bool MovementLocked => Weapon != null && Weapon.Active != null && Weapon.Active.LocksMovement;
        public float SpeedMultiplier => Weapon != null && Weapon.Active != null ? Weapon.Active.SpeedMultiplier : 1f;

        public static Combatant From(Collider col)
        {
            if (col == null) return null;
            if (col.attachedRigidbody) return col.attachedRigidbody.GetComponent<Combatant>();
            return col.GetComponentInParent<Combatant>();
        }

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody>();
            Hp = maxHp;
        }

        // ───── Illustrated mode ─────

        /// <summary>Billboard in illustrated mode (null in whitebox mode).</summary>
        protected Transform artPivot;
        protected Renderer artSprite;
        protected virtual Texture2D ArtTexture => null;
        /// <summary>Glowing billboard shown during wind-up (falls back to tinting if missing).</summary>
        protected virtual Texture2D ArtWindupTexture => null;
        protected virtual float ArtHeight => 2f;

        /// <summary>Wind-up glow layer in illustrated mode; when null, wind-up is shown by tinting.</summary>
        protected Renderer artWindupSprite;
        // Bubble around the body during post-hit protection (translucent sphere in whitebox, bubble texture in illustrated mode)
        GameObject shieldVisual;

        /// <summary>Position above the head (where the HP bar goes): in illustrated mode this is the top of the billboard along the camera's up.</summary>
        public Vector3 HeadPoint(Camera cam)
        {
            if (artPivot && cam) return artPivot.position + cam.transform.up * (ArtHeight + 0.15f);
            return Position + Vector3.up * (radius * 2f + 0.6f);
        }

        protected virtual void Start()
        {
            if (Art.On && ArtTexture != null)
            {
                Art.HideWhitebox(gameObject);
                artPivot = Art.Billboard(transform, ArtTexture, ArtHeight, Art.OrderCharacter, out artSprite);
                if (ArtWindupTexture)
                {
                    artWindupSprite = Art.BillboardLayer(artPivot, ArtTexture, ArtHeight, ArtWindupTexture,
                        Art.OrderCharacter);
                    artWindupSprite.enabled = false;
                }
            }
            squashTarget = artPivot ? artPivot : bodyRenderer ? bodyRenderer.transform : null;
            if (squashTarget) squashBase = squashTarget.localScale;
            if (protectionTime > 0f) BuildShieldVisual();
        }

        /// <summary>Wind-up glow: switch to the glowing billboard if there is one; otherwise return false so the caller tints instead.</summary>
        protected bool ShowArtWindup(bool on)
        {
            if (!artWindupSprite) return false;
            artWindupSprite.enabled = on;
            artSprite.enabled = !on;
            return true;
        }

        void BuildShieldVisual()
        {
            if (artPivot && Art.Set.shieldBubble)
            {
                // The bubble is slightly larger than the billboard and concentric with it
                var bubble = Art.Set.shieldBubble;
                var r = Art.BillboardLayer(artPivot, bubble, ArtHeight * 1.15f, bubble, Art.OrderHeld);
                r.transform.localPosition = new Vector3(0f, ArtHeight * 0.5f, -0.02f);
                shieldVisual = r.gameObject;
            }
            else
            {
                shieldVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                UnityEngine.Object.DestroyImmediate(shieldVisual.GetComponent<Collider>()); // under a dynamic rigidbody, must be destroyed immediately
                shieldVisual.name = "Shield";
                shieldVisual.transform.SetParent(transform, false);
                shieldVisual.transform.localPosition = new Vector3(0f, radius * 1.6f, 0f);
                shieldVisual.transform.localScale = Vector3.one * (radius * 2.8f);
                var mr = shieldVisual.GetComponent<Renderer>();
                mr.sharedMaterial = GM.FxMat(new Color(0.6f, 0.85f, 1f, 0.28f));
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            shieldVisual.SetActive(false);
        }

        // ───── Hit feedback ─────

        // Duration of the squash-and-recover (seconds, real time)
        const float SquashTime = 0.15f;
        // White flash in illustrated mode: Unlit is 'texture × color', so the color has to go far above 1 to saturate to near white (black outlines stay black)
        static readonly Color ArtFlashColor = new Color(6f, 6f, 6f, 1f);
        static MaterialPropertyBlock flashBlock;

        float flashTimer;
        float squashTimer;
        bool flashing;
        Transform squashTarget;
        Vector3 squashBase;

        static Color HitColor(HitKind kind) => kind switch
        {
            HitKind.Bullet => new Color(1f, 0.95f, 0.45f),
            HitKind.Pierce => new Color(0.7f, 0.95f, 1f),
            HitKind.Slam => new Color(0.95f, 0.9f, 0.8f),
            HitKind.Blast => new Color(1f, 0.5f, 0.15f),
            _ => new Color(1f, 0.8f, 0.35f),
        };

        /// <summary>
        /// Hit feedback: white flash, squash and recover, shards flying in the hit direction, shock ring, floating damage number;
        /// the player being hit, heavy hits, wall impacts and explosions also add hit stop and screen shake. Every damage path (attack, wall, killed by the human missile) goes through here.
        /// </summary>
        void PlayHitFeedback(float amount, Vector3 dir, HitKind kind)
        {
            var fb = GM.feedback;
            bool heavy = amount >= fb.heavyDamage || kind == HitKind.Slam || kind == HitKind.Blast;
            bool isPlayer = Team == Team.Player;
            flashTimer = fb.flashTime;
            squashTimer = SquashTime;

            Vector3 at = Query.AtCastHeight(Position);
            Color c = HitColor(kind);
            float size = Mathf.Max(0.12f, radius * 0.3f);
            // Bullet, pierce: a narrow fast jet; blunt: a wide fan; slam, blast: all directions
            var (count, speed, spread) = kind switch
            {
                HitKind.Bullet => (9, 12f, 50f),
                HitKind.Pierce => (10, 15f, 30f),
                HitKind.Slam => (14, 8f, 360f),
                HitKind.Blast => (16, 10f, 360f),
                _ => (heavy ? 14 : 9, heavy ? 11f : 8f, 120f),
            };
            Fx.Burst(at, spread >= 360f ? Vector3.zero : dir, c, count, speed, spread, size);
            var art = Art.On ? Art.Set : null;
            if (art && art.hitSpark)
                Fx.SpriteBillboard(art.hitSpark, at + Vector3.up * 0.3f, radius * 1.2f, radius * (heavy ? 3.2f : 2.4f), 0.2f);
            else
                Fx.Pop(at, c, radius * (heavy ? 3.5f : 2.5f));
            if (heavy) Fx.Ring(Position, radius + 2.5f, c);
            // Wall impact or hit by a corpse: a puff of dust at the feet
            if (kind == HitKind.Slam && art && art.dust)
                Fx.SpriteBillboard(art.dust, new Vector3(Position.x, 0.1f, Position.z), radius * 1.5f, radius * 3.5f, 0.45f);

            GM.hud.DamageNumber(Position, amount, isPlayer ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.95f, 0.6f), heavy || isPlayer);
            if (isPlayer)
            {
                GM.HitStop(fb.heavyHitStop);
                GM.Shake(fb.playerHitShake);
            }
            else
            {
                // Bullets come in bursts: hit stop on every shot would make firing stutter, so bullet hits on enemies skip hit stop and shake
                if (kind != HitKind.Bullet)
                {
                    GM.HitStop(heavy ? fb.heavyHitStop : fb.hitStop);
                    if (heavy) GM.Shake(fb.heavyShake);
                }
            }
        }

        /// <summary>Flash and squash only change appearance, so they run in LateUpdate: scale and color are overridden after all movement and state are done.</summary>
        void LateUpdate()
        {
            // Uses real time: flash and squash keep playing during hit stop, which gives the 'freeze frame' look
            float dt = Time.unscaledDeltaTime;
            if (shieldVisual)
            {
                // Blinks during the last 0.15 s of protection to signal 'you can be hit again soon'
                bool show = Protected && IsAlive && (protectTimer > 0.15f || Mathf.Repeat(Time.time * 20f, 1f) < 0.5f);
                if (shieldVisual.activeSelf != show) shieldVisual.SetActive(show);
            }
            if (flashTimer > 0f || flashing)
            {
                flashTimer -= dt;
                SetFlash(flashTimer > 0f && IsAlive);
            }
            if (squashTimer > 0f && squashTarget && IsAlive)
            {
                squashTimer = Mathf.Max(0f, squashTimer - dt);
                float s = GM.feedback.squash * (squashTimer / SquashTime);
                // Billboard: wider horizontally, shorter vertically; whitebox capsule: wider on both horizontal axes, shorter in height
                squashTarget.localScale = artPivot
                    ? Vector3.Scale(squashBase, new Vector3(1f + s, 1f - s, 1f))
                    : Vector3.Scale(squashBase, new Vector3(1f + s, 1f - s, 1f + s));
            }
        }

        void SetFlash(bool on)
        {
            if (on == flashing) return;
            flashing = on;
            if (artSprite)
            {
                Art.Tint(artSprite, on ? ArtFlashColor : Color.white);
                Art.Tint(artWindupSprite, on ? ArtFlashColor : Color.white);
            }
            else if (bodyRenderer)
            {
                // Whitebox overrides color with a PropertyBlock: the shared material is untouched, and the yellow wind-up material of enemies is unaffected
                if (on)
                {
                    flashBlock ??= new MaterialPropertyBlock();
                    flashBlock.SetColor("_BaseColor", Color.white);
                    bodyRenderer.SetPropertyBlock(flashBlock);
                }
                else bodyRenderer.SetPropertyBlock(null);
            }
        }

        // ───── State machine ─────

        public void SetState(CharacterState s)
        {
            if (State == s || State == CharacterState.Dead) return;
            var old = State;

            // Exit the old state
            if (old == CharacterState.Hooked || old == CharacterState.Dashing)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
            }

            State = s;

            // Enter the new state
            switch (s)
            {
                case CharacterState.Normal:
                    knockVel = Vector3.zero;
                    break;
                case CharacterState.Hooked:
                case CharacterState.Dashing:
                    knockVel = Vector3.zero;
                    if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
                    rb.isKinematic = true;
                    break;
                case CharacterState.Falling:
                    fallTimer = 0f;
                    rb.isKinematic = false;
                    break;
                case CharacterState.Dead:
                    if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
                    rb.isKinematic = true;
                    break;
            }
            OnStateChanged(old, s);
        }

        protected virtual void OnStateChanged(CharacterState from, CharacterState to) { }

        protected virtual void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (protectTimer > 0f) protectTimer -= dt;
            if (weaponCooldown > 0f) weaponCooldown -= dt;

            // Fallback: far off the platform without having touched a FallZone
            if (IsAlive && State != CharacterState.Hooked && State != CharacterState.Dashing && rb.position.y < -2f)
                OnEnterFallZone(Vector3.zero);

            switch (State)
            {
                case CharacterState.Knocked:
                case CharacterState.Thrown:
                    knockVel *= Mathf.Exp(-GM.rules.knockDamping * dt);
                    rb.linearVelocity = new Vector3(knockVel.x, rb.linearVelocity.y, knockVel.z);
                    if (knockVel.magnitude < 0.3f) SetState(CharacterState.Normal);
                    break;
                case CharacterState.Falling:
                    fallTimer += dt;
                    if (fallTimer > 2f) OnFallFinished();
                    break;
                case CharacterState.Normal:
                    TickNormal(dt);
                    if (pushVel.sqrMagnitude > 1e-4f)
                    {
                        rb.linearVelocity += pushVel;
                        pushVel *= Mathf.Exp(-GM.rules.knockDamping * dt);
                    }
                    break;
            }
            if (IsAlive && State != CharacterState.Dashing && State != CharacterState.Hooked)
            {
                Vector3 face = Query.Flat(AimDirection);
                if (face.sqrMagnitude > 1e-4f) rb.MoveRotation(Quaternion.LookRotation(face));
            }
        }

        protected abstract void TickNormal(float dt);

        // ───── Knockback and wall hits ─────

        /// <inheritdoc/>
        public void Push(Vector3 dir, float distance)
        {
            if (!IsAlive) return;
            dir = Query.Flat(dir);
            if (dir.sqrMagnitude < 1e-6f || distance <= 0f) return;
            // Same as knockback: initial speed = distance × damping, exponential decay pushes exactly distance; stacks shot by shot in a burst
            pushVel += dir.normalized * (distance * GM.rules.knockDamping);
        }

        /// <inheritdoc/>
        public void SlamIntoWall(Vector3 at, Vector3 away, float bounceDistance)
        {
            if (!IsAlive) return;
            // The rigidbody is kinematic while dashing; MovePosition only applies on the next physics step and is lost after switching back to dynamic, so set the position directly
            rb.position = at;
            transform.position = at;
            if (State == CharacterState.Dashing || State == CharacterState.Hooked) SetState(CharacterState.Normal);
            TakeWallDamage(away);
            if (IsAlive) Knockback(away, bounceDistance, KnockKind.Push);
        }

        /// <summary>Knockback by rigidbody impulse. Distance d maps to initial speed d × damping; exponential decay makes it slide exactly d.</summary>
        public void Knockback(Vector3 dir, float distance, KnockKind kind)
        {
            if (!IsAlive || State == CharacterState.Hooked || State == CharacterState.Dashing) return;
            dir = Query.Flat(dir);
            if (dir.sqrMagnitude < 1e-6f || distance <= 0f) return;
            dir.Normalize();
            if (kind == KnockKind.Hit) distance *= knockbackScale;
            knockVel = dir * (distance * GM.rules.knockDamping);
            Vector3 cur = Query.Flat(rb.linearVelocity);
            rb.AddForce(knockVel - cur, ForceMode.VelocityChange);
            knockKind = kind;
            SetState(kind == KnockKind.Throw ? CharacterState.Thrown : CharacterState.Knocked);
        }

        void OnCollisionEnter(Collision c) => CheckWallHit(c);
        void OnCollisionStay(Collision c) => CheckWallHit(c);

        /// <summary>Hitting a wall during knockback: stop; wall damage only applies above the speed threshold, once per impact.</summary>
        void CheckWallHit(Collision c)
        {
            if (State != CharacterState.Knocked && State != CharacterState.Thrown) return;
            if (c.gameObject.layer != GameManager.WallLayer) return;
            for (int i = 0; i < c.contactCount; i++)
            {
                Vector3 to = Query.Flat(c.GetContact(i).point - rb.position);
                if (to.sqrMagnitude < 1e-6f) continue;
                if (Vector3.Dot(knockVel, to.normalized) < GM.rules.wallHitSpeed) continue;
                bool damage = knockKind != KnockKind.Push;
                SetState(CharacterState.Normal);
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                if (damage) TakeWallDamage(-to.normalized);
                return;
            }
        }

        /// <param name="away">Direction away from the wall: shards fly this way</param>
        void TakeWallDamage(Vector3 away)
        {
            if (!IsAlive) return;
            Hp -= GM.rules.wallDamage;
            PlayHitFeedback(GM.rules.wallDamage, away, HitKind.Slam);
            OnDamaged(GM.rules.wallDamage);
            if (Hp <= 0f) Die(DeathCause.HpDepleted);
        }

        // ───── Damage and death ─────

        public virtual bool ReceiveDamage(DamageInfo info)
        {
            if (!IsAlive) return false;
            if (info.isAttack && (Protected || State == CharacterState.Dashing)) return false;
            Hp -= info.amount;
            if (info.isAttack && protectionTime > 0f) protectTimer = protectionTime;
            PlayHitFeedback(info.amount, info.knockDir, info.kind);
            OnDamaged(info.amount);
            if (Hp <= 0f)
            {
                Hp = 0f;
                Die(DeathCause.HpDepleted);
                return true;
            }
            if (info.knockDistance > 0f) Knockback(info.knockDir, info.knockDistance, KnockKind.Hit);
            return true;
        }

        public void Heal(float amount) => Hp = Mathf.Min(maxHp, Hp + amount);

        /// <summary>Instant death (hit by the human missile; large enemies too). Plays one pierce hit effect scaled by remaining HP.</summary>
        public void Kill()
        {
            if (!IsAlive) return;
            PlayHitFeedback(Mathf.Max(Hp, GM.feedback.heavyDamage), Vector3.zero, HitKind.Pierce);
            Hp = 0f;
            Die(DeathCause.HpDepleted);
        }

        protected void Die(DeathCause cause)
        {
            if (!IsAlive) return;
            Hp = Mathf.Max(0f, Hp);
            DeathCause = cause;
            SetState(CharacterState.Dead);
            OnDied();
            ReportEliminated();
        }

        /// <summary>FallZone trigger: no fall while hooked; otherwise counts as falling into the gap immediately.</summary>
        public void OnEnterFallZone(Vector3 outward)
        {
            if (!IsAlive || State == CharacterState.Hooked) return;
            DeathCause = DeathCause.FellIntoGap;
            Vector3 v = State == CharacterState.Dashing || rb.isKinematic ? Vector3.zero : Query.Flat(rb.linearVelocity);
            SetState(CharacterState.Falling);
            rb.linearVelocity = v + outward * 3f;
            OnFell();
            ReportEliminated();
        }

        void ReportEliminated()
        {
            if (eliminated) return;
            eliminated = true;
            Eliminated?.Invoke(this);
        }

        protected virtual void OnDamaged(float amount) { }
        protected virtual void OnDied() { }
        protected virtual void OnFell() { }
        protected virtual void OnFallFinished() { gameObject.SetActive(false); }

        public void SetMovePosition(Vector3 p) => rb.MovePosition(p);

        // ───── Weapon ─────

        public void Equip(Weapon w)
        {
            Weapon = w;
            w.AttachTo(this, hand ? hand : transform);
        }

        /// <summary>Left button released: forwarded to the running routine (Charge Swing swings now).</summary>
        public void ReleaseWeapon()
        {
            if (Weapon != null && Weapon.Active != null) Weapon.Active.OnUseReleased();
        }

        public Weapon Unequip()
        {
            var w = Weapon;
            Weapon = null;
            return w;
        }

        /// <summary>
        /// Left button pressed: if a routine is running (hook holding) it handles the press; cannot be used while the previous thrown knife is still out;
        /// otherwise fires the effect once. If the clip still has rounds they are used first; when empty, one use is spent and the clip is refilled (roundsPerUse).
        /// </summary>
        public bool TryUseWeapon()
        {
            var w = Weapon;
            if (w == null || !IsAlive) return false;
            if (w.Active != null)
            {
                w.Active.OnUsePressed();
                return true;
            }
            if (w.InFlight) return false;
            if (State != CharacterState.Normal || weaponCooldown > 0f) return false;
            w.ApplyDebugForce();
            bool first = !w.Revealed;
            bool fromClip = w.RoundsLeft > 0;
            w.Effect.Use(w, this);
            if (fromClip) w.RoundsLeft--;
            else
            {
                w.UsesLeft--;
                w.RoundsLeft = Mathf.Max(1, w.Effect.roundsPerUse) - 1;
            }
            w.Revealed = true;
            weaponCooldown = w.Effect.cooldown;
            if (first) OnWeaponRevealed(w);
            CheckWeaponDepleted();
            return true;
        }

        protected virtual void OnWeaponRevealed(Weapon w) { }

        public void OnWeaponRuntimeFinished() => CheckWeaponDepleted();

        /// <summary>The weapon disappears once uses and clip are both empty, no routine is running and the thrown knife has returned.</summary>
        public void CheckWeaponDepleted()
        {
            if (Weapon == null || Weapon.UsesLeft > 0 || Weapon.RoundsLeft > 0 || Weapon.Active != null || Weapon.InFlight) return;
            var w = Unequip();
            w.Consume();
        }

    }
}
