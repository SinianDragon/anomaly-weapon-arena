using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Throwing knife routine:
    ///   Outbound  enter: thrown. Flies straight along the aim direction. exit: hit a character → Seeking (or max reached → Returning); hit a wall / flew the full range → Returning.
    ///   Seeking   enter: found the next target after a hit. Flies toward the target's current position each frame.
    ///             exit: hit a character → look for the next (maxTargets reached or none found → Returning); target gone / wall in between → look again or Returning.
    ///   Returning enter: starts returning and records the user's position at that moment (lock point), then flies to that point; disappears on arrival, and only then can the knife be thrown again.
    /// Each character is hit by the same thrown knife at most once. The first enemy killed is carried along; while returning with a corpse, characters on the way (including the user)
    /// take that corpse's attack damage when alive and are knocked back.
    /// </summary>
    public class ThrownKnife : Projectile
    {
        enum Phase
        {
            Outbound,
            Seeking,
            Returning
        }

        // Collision radius: how close the blade must be to a character's body edge to count as a hit
        const float HitRadius = 0.35f;

        KnifeThrowEffect cfg;
        Weapon weapon;
        Phase phase;
        Vector3 pos, dir;
        float traveled;
        int hits;
        Combatant seekTarget;
        Vector3 lockPoint;
        float corpseDamage;
        float corpseRadius;
        Transform corpse;
        Transform spinner;
        GameObject lockMarker;
        readonly HashSet<Combatant> struck = new HashSet<Combatant>();
        readonly HashSet<Combatant> hitOnReturn = new HashSet<Combatant>();

        /// <summary>How many characters have been hit so far (for tests and UI).</summary>
        public int Hits => hits;

        public bool Returning => phase == Phase.Returning;

        public void Launch(KnifeThrowEffect e, Weapon w, IWeaponHolder owner, Vector3 start, Vector3 direction)
        {
            cfg = e;
            weapon = w;
            if (weapon) weapon.InFlight = this;
            Owner = owner;
            pos = Query.AtCastHeight(start);
            dir = Query.Flat(direction).normalized;
            phase = Phase.Outbound;
            // The blade spins in the air with a very short trail: it reads at a glance as a thrown knife, not the stretched blade of the hook
            spinner = new GameObject("Spin").transform;
            spinner.SetParent(transform, false);
            if (Art.On && Art.Set.knife) Art.Flat(spinner, Art.Set.knife, 1.6f, Art.OrderProjectile, out _);
            else MakeVisual(spinner, PrimitiveType.Cube, new Vector3(0.18f, 0.08f, 1f), new Color(0.82f, 0.88f, 0.94f));
            AddTrail(new Color(0.85f, 0.9f, 1f, 0.5f), 0.1f).time = 0.08f;
            UpdateTransform();
        }

        void Update()
        {
            if (spinner)
                spinner.Rotate(0f, (phase == Phase.Returning ? -1f : 1f) * 1080f * Time.deltaTime, 0f, Space.Self);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            switch (phase)
            {
                case Phase.Outbound:
                    if (Advance(cfg.speed * dt) && traveled >= cfg.range) StartReturn();
                    break;
                case Phase.Seeking:
                    if (!ValidTarget(seekTarget) && !SeekNext(pos))
                    {
                        StartReturn();
                        break;
                    }

                    dir = Query.Flat(seekTarget.Position - pos).normalized;
                    Advance(cfg.speed * dt);
                    break;
                case Phase.Returning:
                    if (TickReturn(cfg.returnSpeed * dt)) return;
                    break;
            }

            UpdateTransform();
        }

        /// <summary>Flies step along dir: hits a wall → return; touches a character not yet hit → hit. Returns false when this step has switched to returning or another phase.</summary>
        bool Advance(float step)
        {
            Vector3 next = pos + dir * step;
            if (Query.WallBetween(pos, next, out var wall))
            {
                pos += dir * Mathf.Max(0f, wall.distance - 0.1f);
                StartReturn();
                return false;
            }

            pos = next;
            traveled += step;
            foreach (var c in Query.Characters(pos, HitRadius))
            {
                if (!ValidTarget(c)) continue;
                Strike(c);
                return false;
            }

            return true;
        }

        /// <summary>A valid target: alive, not the thrower, and not yet hit by this knife.</summary>
        bool ValidTarget(Combatant c) => c != null && c.IsAlive && (IWeaponHolder)c != Owner && !struck.Contains(c);

        void Strike(Combatant c)
        {
            struck.Add(c);
            hits++;
            float dmg = c.CorpseDamage;
            float r = c.Radius;
            Vector3 at = c.Position;
            c.ReceiveDamage(DamageInfo.Attack(cfg.damage, this, dir, 0f, HitKind.Pierce));
            if (!c.IsAlive && corpse == null) AttachCorpse(c, dmg, r);
            if (hits >= cfg.maxTargets || !SeekNext(at)) StartReturn();
        }

        /// <summary>Finds the nearest next target within bounceRange of from with no wall in between.</summary>
        bool SeekNext(Vector3 from)
        {
            seekTarget = null;
            float best = float.MaxValue;
            foreach (var c in Query.Characters(Query.AtCastHeight(from), cfg.bounceRange))
            {
                if (!ValidTarget(c)) continue;
                float d = Query.Flat(c.Position - from).sqrMagnitude;
                if (d >= best ||
                    Query.WallBetween(Query.AtCastHeight(from), Query.AtCastHeight(c.Position), out _)) continue;
                best = d;
                seekTarget = c;
            }

            if (seekTarget == null) return false;
            phase = Phase.Seeking;
            return true;
        }

        /// <summary>Flies toward the lock point; hits characters when carrying a corpse. Destroys itself on arrival and returns true.</summary>
        bool TickReturn(float step)
        {
            Vector3 to = Query.Flat(lockPoint - pos);
            if (to.magnitude <= step)
            {
                pos = lockPoint;
                Destroy(gameObject); // the knife (and corpse) disappears at the return point; the knife can be thrown again
                return true;
            }

            dir = to.normalized;
            pos += dir * step;
            if (corpse == null) return false;
            foreach (var c in Query.Characters(pos, corpseRadius + 0.1f))
            {
                if (!c.IsAlive || !hitOnReturn.Add(c)) continue;
                c.ReceiveDamage(DamageInfo.Attack(corpseDamage, this, dir, cfg.corpseKnockback, HitKind.Slam));
            }

            return false;
        }

        void AttachCorpse(Combatant c, float damage, float r)
        {
            corpseDamage = damage;
            corpseRadius = r;
            // Illustrated mode prefers the 'face down, knife in its back' corpse image; otherwise the enemy billboard laid flat and greyed
            bool large = c is Enemy e && e.IsLarge;
            var corpseTex = Art.On ? (large ? Art.Set.corpseLarge : Art.Set.corpseSmall) : null;
            var enemyTex = corpseTex ? corpseTex : Art.On ? (large ? Art.Set.enemyLarge : Art.Set.enemySmall) : null;
            if (enemyTex)
            {
                // Illustrated mode: enemy texture laid flat and greyed, dragged behind the knife
                corpse = Art.Flat(transform, enemyTex, r * 2.6f, Art.OrderProjectile, out var cr,
                    new Vector3(0f, 0.1f - Query.CastHeight, -r));
                if (!corpseTex) Art.Tint(cr, new Color(0.55f, 0.5f, 0.5f));
            }
            else
            {
                corpse = MakeVisual(transform, PrimitiveType.Capsule, new Vector3(r * 2f, r * 2f, r * 2f),
                    new Color(0.42f, 0.36f, 0.34f), new Vector3(0f, r - Query.CastHeight + 0.1f, -r));
                corpse.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            // The carried corpse does not hit itself on the way back
            hitOnReturn.Add(c);
        }

        void StartReturn()
        {
            if (phase == Phase.Returning) return;
            phase = Phase.Returning;
            seekTarget = null;
            var owner = Owner as Combatant;
            lockPoint = Query.AtCastHeight(owner != null ? owner.Position : pos);
            if (corpse == null) return;
            lockMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(lockMarker.GetComponent<Collider>());
            lockMarker.name = "KnifeReturnPoint";
            lockMarker.transform.position = new Vector3(lockPoint.x, 0.02f, lockPoint.z);
            lockMarker.transform.localScale = new Vector3(1.4f, 0.01f, 1.4f);
            lockMarker.GetComponent<Renderer>().sharedMaterial =
                GameManager.Instance.FxMat(new Color(1f, 0.3f, 0.25f, 0.45f));
        }

        void UpdateTransform()
        {
            transform.position = pos;
            if (dir.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(dir);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (lockMarker) Destroy(lockMarker);
            // The knife can only be thrown again after the thrown one disappears (reached the return point, or cleaned up on death / victory); a knife with no uses left disappears only then
            if (weapon && weapon.InFlight == this)
            {
                weapon.InFlight = null;
                if (weapon.Holder is Combatant holder && holder.Weapon == weapon) holder.CheckWeaponDepleted();
            }
        }
    }
}