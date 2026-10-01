using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Reverse Shot bullet: damages the first enemy it hits and disappears; also disappears on walls.</summary>
    public class Bullet : Projectile
    {
        GunReverseShotEffect cfg;
        Vector3 pos, dir;
        float traveled;

        public void Launch(GunReverseShotEffect e, IWeaponHolder owner, Vector3 start, Vector3 direction)
        {
            cfg = e;
            Owner = owner;
            pos = Query.AtCastHeight(start);
            dir = Query.Flat(direction).normalized;
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(dir);
            if (Art.On && Art.Set.bullet)
            {
                // Illustrated mode: a bullet texture with its tip pointing right and a built-in trail, lying flat and pointing along the flight direction (trail behind)
                Art.Flat(transform, Art.Set.bullet, 1.3f, Art.OrderProjectile, out _, new Vector3(0f, 0f, -0.4f));
            }
            else
            {
                MakeVisual(transform, PrimitiveType.Sphere, Vector3.one * 0.3f, new Color(1f, 0.85f, 0.3f));
                AddTrail(new Color(1f, 0.85f, 0.3f, 0.8f), 0.15f);
            }
        }

        void FixedUpdate()
        {
            float step = cfg.bulletSpeed * Time.fixedDeltaTime;
            Vector3 next = pos + dir * step;
            if (Query.WallBetween(pos, next, out _))
            {
                Destroy(gameObject);
                return;
            }

            pos = next;
            traveled += step;
            transform.position = pos;
            foreach (var c in Query.Characters(pos, 0.25f))
            {
                if ((IWeaponHolder)c == Owner || !c.IsAlive) continue;
                c.ReceiveDamage(DamageInfo.Attack(cfg.damage, this, dir, 0f, HitKind.Bullet));
                Destroy(gameObject);
                return;
            }

            if (traveled >= cfg.bulletRange) Destroy(gameObject);
        }
    }
}