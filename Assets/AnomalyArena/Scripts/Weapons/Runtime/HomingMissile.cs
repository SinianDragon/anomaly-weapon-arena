using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Homing missile: first flies straight in a random direction; after lockDelay seconds it locks onto a weighted-random target on the field (player weight 3, each enemy 1)
    /// and chases it at a limited turn rate; if the target is gone it picks again. It explodes on touching any character or wall, and in place at the end of its lifetime. The blast hits everyone in range.
    /// </summary>
    public class HomingMissile : Projectile
    {
        MissileHomingEffect cfg;
        Vector3 pos, dir;
        float t;
        bool locked;
        Combatant target;
        Renderer body;

        public void Launch(MissileHomingEffect e, IWeaponHolder owner, Vector3 start, Vector3 direction)
        {
            cfg = e;
            Owner = owner;
            pos = Query.AtCastHeight(start);
            dir = Query.Flat(direction).normalized;
            if (Art.On && Art.Set.missile)
            {
                // Illustrated mode: missile texture + exhaust flame (the head of the flame texture touches the missile's tail, flames pointing back)
                Art.Flat(transform, Art.Set.missile, 1.6f, Art.OrderProjectile, out body);
                if (Art.Set.flame)
                    flame = Art.Flat(transform, Art.Set.flame, 1.3f, Art.OrderProjectile, out _,
                        new Vector3(0f, 0f, -1.4f));
            }
            else
            {
                var v = MakeVisual(transform, PrimitiveType.Cylinder, new Vector3(0.3f, 0.35f, 0.3f),
                    new Color(0.9f, 0.35f, 0.2f));
                v.localRotation = Quaternion.Euler(90f, 0f, 0f);
                body = v.GetComponent<Renderer>();
                AddTrail(new Color(1f, 1f, 1f, 0.6f), 0.2f);
            }

            UpdateTransform();
        }

        Transform flame;

        void Update()
        {
            if (flame) flame.localScale = new Vector3(1f, 1f, 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 12f, 0f));
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            t += dt;
            if (t >= cfg.lockDelay)
            {
                if (target == null || !target.IsAlive) PickTarget();
                if (target != null) dir = Query.Steer(dir, target.Position - pos, cfg.turnRate * dt);
            }

            Vector3 next = pos + dir * (cfg.speed * dt);
            if (Query.WallBetween(pos, next, out var wall))
            {
                pos += dir * Mathf.Max(0f, wall.distance - 0.1f);
                Explode();
                return;
            }

            pos = next;

            foreach (var c in Query.Characters(pos, 0.4f))
            {
                if (!c.IsAlive) continue;
                if ((IWeaponHolder)c == Owner && !locked) continue; // does not blow up on its owner right after launch
                Explode();
                return;
            }

            if (t >= cfg.lifetime || Mathf.Abs(pos.x) > 40f || Mathf.Abs(pos.z) > 40f)
            {
                Explode();
                return;
            }

            UpdateTransform();
        }

        /// <summary>Picks a weighted-random target among all living characters.</summary>
        void PickTarget()
        {
            var gm = GameManager.Instance;
            var candidates = new System.Collections.Generic.List<Combatant>();
            if (gm.player != null && gm.player.IsAlive) candidates.Add(gm.player);
            foreach (var e in gm.waves.Alive)
                if (e != null && e.IsAlive)
                    candidates.Add(e);
            float total = 0f;
            foreach (var c in candidates) total += Weight(c);
            target = null;
            if (total <= 0f) return;
            float r = Random.value * total;
            foreach (var c in candidates)
            {
                r -= Weight(c);
                if (r > 0f) continue;
                target = c;
                break;
            }

            if (target == null) target = candidates[candidates.Count - 1];
            if (!locked)
            {
                locked = true;
                if (Art.On) Art.Tint(body, new Color(1f, 0.45f, 0.45f));
                else body.sharedMaterial = gm.Mat(new Color(1f, 0.15f, 0.15f));
            }

            if (target == gm.player) gm.hud.Toast("The missile locked onto YOU!", new Color(1f, 0.5f, 0.45f));
        }

        float Weight(Combatant c) => c.Team == Team.Player ? cfg.playerWeight : cfg.enemyWeight;

        void Explode()
        {
            var art = Art.On ? Art.Set : null;
            if (art && art.explosionFrames != null && art.explosionFrames.Length > 0)
            {
                // Illustrated mode: fireball animation + a ground ring expanding to the real blast radius, so you can see whether the blast reaches you
                Fx.Flipbook(art.explosionFrames, pos + Vector3.up * 0.5f, cfg.explosionRadius * 1.5f, 0.35f);
                if (art.explosionRing) Fx.SpriteRing(art.explosionRing, pos, cfg.explosionRadius, 0.45f);
            }
            else
            {
                Fx.Explosion(pos, cfg.explosionRadius);
            }

            foreach (var c in Query.Characters(pos, cfg.explosionRadius))
            {
                if (!c.IsAlive) continue;
                Vector3 away = Query.Flat(c.Position - pos);
                if (away.sqrMagnitude < 1e-4f) away = Random.insideUnitSphere;
                c.ReceiveDamage(DamageInfo.Attack(cfg.damage, this, away, cfg.knockback, HitKind.Blast));
            }

            Destroy(gameObject);
        }

        void UpdateTransform()
        {
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}