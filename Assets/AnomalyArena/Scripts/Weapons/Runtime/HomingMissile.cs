using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// 追踪导弹：先朝随机方向直飞；lockDelay 秒后锁定发射者，以有限转向速度追过去。
    /// 路上碰到任何人或墙就爆炸；到寿命原地爆炸。爆炸伤到范围内所有人。
    /// </summary>
    public class HomingMissile : Projectile
    {
        MissileHomingEffect cfg;
        Vector3 pos, dir;
        float t;
        bool locked;
        Renderer body;

        public void Launch(MissileHomingEffect e, IWeaponHolder owner, Vector3 start, Vector3 direction)
        {
            cfg = e;
            Owner = owner;
            pos = Query.AtCastHeight(start);
            dir = Query.Flat(direction).normalized;
            var v = MakeVisual(transform, PrimitiveType.Cylinder, new Vector3(0.3f, 0.35f, 0.3f), new Color(0.9f, 0.35f, 0.2f));
            v.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body = v.GetComponent<Renderer>();
            AddTrail(new Color(1f, 1f, 1f, 0.6f), 0.2f);
            UpdateTransform();
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            t += dt;
            var owner = Owner as Combatant;
            if (t >= cfg.lockDelay && owner != null && owner.IsAlive)
            {
                if (!locked)
                {
                    locked = true;
                    body.sharedMaterial = GameManager.Instance.Mat(new Color(1f, 0.15f, 0.15f));
                }
                dir = Query.Steer(dir, owner.Position - pos, cfg.turnRate * dt);
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
                if ((IWeaponHolder)c == Owner && !locked) continue; // 刚发射时不炸自己
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

        void Explode()
        {
            Fx.Explosion(pos, cfg.explosionRadius);
            foreach (var c in Query.Characters(pos, cfg.explosionRadius))
            {
                if (!c.IsAlive) continue;
                Vector3 away = Query.Flat(c.Position - pos);
                if (away.sqrMagnitude < 1e-4f) away = Random.insideUnitSphere;
                c.ReceiveDamage(DamageInfo.Attack(cfg.damage, this, away, cfg.knockback));
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
