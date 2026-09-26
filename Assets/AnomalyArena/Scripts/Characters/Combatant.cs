using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// 玩家与敌人的共同实现：生命、受伤与保护、刚体冲量击飞、撞墙扣血、掉进缺口、状态机、持有武器。
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public abstract class Combatant : MonoBehaviour, IWeaponHolder, IDamageDealer
    {
        [Header("Stats")]
        public float maxHp = 10f;
        public float moveSpeed = 6f;
        public float radius = 0.5f;
        [Tooltip("被撞飞距离的倍率（大型敌人 0.5）")] public float knockbackScale = 1f;
        [Tooltip("挨打后的保护时间，期间攻击伤害无效（撞墙伤害不受影响）")] public float protectionTime = 0f;

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
        /// <summary>叠加在正常移动上的推力（连发后坐力），按撞飞同样的系数衰减。</summary>
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
        /// <summary>这具尸体被飞刀带回时撞人的伤害（= 生前的攻击伤害）。</summary>
        public virtual float CorpseDamage => 0f;

        /// <summary>正被这个角色用钩子黏住、可以当掩体的敌人。</summary>
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

        // ───── 美术版 ─────

        /// <summary>美术版的立牌（白盒版为空）。</summary>
        protected Transform artPivot;
        protected Renderer artSprite;
        protected virtual Texture2D ArtTexture => null;
        protected virtual float ArtHeight => 2f;

        /// <summary>头顶的位置（血条放这里）：美术版是立牌顶，沿镜头上方算。</summary>
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
            }
            squashTarget = artPivot ? artPivot : bodyRenderer ? bodyRenderer.transform : null;
            if (squashTarget) squashBase = squashTarget.localScale;
        }

        // ───── 受击反馈 ─────

        // 压扁回弹的时长（秒，真实时间）
        const float SquashTime = 0.15f;
        // 美术版闪白：Unlit 是“贴图 × 颜色”，颜色调到远大于 1 才会饱和成接近纯白（黑描边保持黑）
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
        /// 受击反馈：闪白、压扁回弹、朝受击方向飞溅的碎片、冲击环、伤害飘字；
        /// 玩家挨打、重击、撞墙、爆炸再加顿帧和震屏。所有扣血路径（攻击、撞墙、被人形导弹撞死）都走这里。
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
            // 子弹、穿透：窄而快的一束；钝击：宽扇形；撞击、爆炸：全向
            var (count, speed, spread) = kind switch
            {
                HitKind.Bullet => (9, 12f, 50f),
                HitKind.Pierce => (10, 15f, 30f),
                HitKind.Slam => (14, 8f, 360f),
                HitKind.Blast => (16, 10f, 360f),
                _ => (heavy ? 14 : 9, heavy ? 11f : 8f, 120f),
            };
            Fx.Burst(at, spread >= 360f ? Vector3.zero : dir, c, count, speed, spread, size);
            Fx.Pop(at, c, radius * (heavy ? 3.5f : 2.5f));
            if (heavy) Fx.Ring(Position, radius + 2.5f, c);

            GM.hud.DamageNumber(Position, amount, isPlayer ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.95f, 0.6f), heavy || isPlayer);
            if (isPlayer)
            {
                GM.HitStop(fb.heavyHitStop);
                GM.Shake(fb.playerHitShake);
            }
            else
            {
                // 子弹是连发的：每发都顿帧会让射击一卡一卡的，所以子弹命中敌人不顿帧、不震屏
                if (kind != HitKind.Bullet)
                {
                    GM.HitStop(heavy ? fb.heavyHitStop : fb.hitStop);
                    if (heavy) GM.Shake(fb.heavyShake);
                }
            }
        }

        /// <summary>闪白和压扁只改外观，放在 LateUpdate：所有移动、状态都算完之后再覆盖缩放和颜色。</summary>
        void LateUpdate()
        {
            // 用真实时间：顿帧期间闪白和压扁照样播放，正好是“定格一下”的效果
            float dt = Time.unscaledDeltaTime;
            if (flashTimer > 0f || flashing)
            {
                flashTimer -= dt;
                SetFlash(flashTimer > 0f && IsAlive);
            }
            if (squashTimer > 0f && squashTarget && IsAlive)
            {
                squashTimer = Mathf.Max(0f, squashTimer - dt);
                float s = GM.feedback.squash * (squashTimer / SquashTime);
                // 立牌：横向变宽、纵向变矮；白盒胶囊：水平两轴变宽、高度变矮
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
            }
            else if (bodyRenderer)
            {
                // 白盒用 PropertyBlock 盖掉颜色：不改共享材质，敌人蓄力时换的黄色材质也不受影响
                if (on)
                {
                    flashBlock ??= new MaterialPropertyBlock();
                    flashBlock.SetColor("_BaseColor", Color.white);
                    bodyRenderer.SetPropertyBlock(flashBlock);
                }
                else bodyRenderer.SetPropertyBlock(null);
            }
        }

        // ───── 状态机 ─────

        public void SetState(CharacterState s)
        {
            if (State == s || State == CharacterState.Dead) return;
            var old = State;

            // 退出旧状态
            if (old == CharacterState.Hooked || old == CharacterState.Dashing)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
            }

            State = s;

            // 进入新状态
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

            // 兜底：掉出平台很远却没碰到 FallZone
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

        // ───── 撞飞与撞墙 ─────

        /// <inheritdoc/>
        public void Push(Vector3 dir, float distance)
        {
            if (!IsAlive) return;
            dir = Query.Flat(dir);
            if (dir.sqrMagnitude < 1e-6f || distance <= 0f) return;
            // 同撞飞：初速度 = 距离 × 阻尼系数，指数衰减后正好推 distance；连发时逐发叠加
            pushVel += dir.normalized * (distance * GM.rules.knockDamping);
        }

        /// <inheritdoc/>
        public void SlamIntoWall(Vector3 at, Vector3 away, float bounceDistance)
        {
            if (!IsAlive) return;
            // 冲刺中刚体是运动学的，MovePosition 要到下一步物理才生效，切回动态后会丢；这里直接设位置
            rb.position = at;
            transform.position = at;
            if (State == CharacterState.Dashing || State == CharacterState.Hooked) SetState(CharacterState.Normal);
            TakeWallDamage(away);
            if (IsAlive) Knockback(away, bounceDistance, KnockKind.Push);
        }

        /// <summary>刚体冲量撞飞。距离 d 对应初速度 d × 阻尼系数，指数衰减后正好滑行 d。</summary>
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

        /// <summary>撞飞途中碰到墙：停下；撞击速度超过阈值才扣墙伤，每次撞击只扣一次。</summary>
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

        /// <param name="away">背离墙的方向：碎片朝这边溅</param>
        void TakeWallDamage(Vector3 away)
        {
            if (!IsAlive) return;
            Hp -= GM.rules.wallDamage;
            PlayHitFeedback(GM.rules.wallDamage, away, HitKind.Slam);
            OnDamaged(GM.rules.wallDamage);
            if (Hp <= 0f) Die(DeathCause.HpDepleted);
        }

        // ───── 受伤与死亡 ─────

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

        /// <summary>直接死亡（被人形导弹撞到，大型也算）。按剩余血量播一次穿透受击特效。</summary>
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

        /// <summary>FallZone 触发：被钩住时不会掉；其余情况立即判定掉进缺口。</summary>
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

        // ───── 武器 ─────

        public void Equip(Weapon w)
        {
            Weapon = w;
            w.AttachTo(this, hand ? hand : transform);
        }

        /// <summary>松开左键：交给进行中的流程（蓄力挥砍在这时挥出）。</summary>
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
        /// 按左键：流程进行中（钩子黏住）交给流程处理；上一把飞刀还没回来时不能用；
        /// 否则发动一次效果。弹夹里还有子弹就先打弹夹，打空了才扣一次次数并装满弹夹（roundsPerUse）。
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

        /// <summary>次数和弹夹都用完、没有进行中的流程、飞刀也回来了时，武器消失。</summary>
        public void CheckWeaponDepleted()
        {
            if (Weapon == null || Weapon.UsesLeft > 0 || Weapon.RoundsLeft > 0 || Weapon.Active != null || Weapon.InFlight) return;
            var w = Unequip();
            w.Consume();
        }

    }
}
