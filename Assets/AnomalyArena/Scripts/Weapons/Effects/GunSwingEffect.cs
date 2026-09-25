using UnityEngine;

namespace AnomalyArena
{
    /// <summary>枪 · 挥砍：前方扇形，扣血并把敌人撞飞。没有反噬；利用方式是把敌人撞进缺口或撞墙。</summary>
    [CreateAssetMenu(menuName = "Anomaly Arena/Effects/Gun Swing")]
    public class GunSwingEffect : WeaponEffect
    {
        public float arcDegrees = 90f;
        [Tooltip("从使用者中心算")] public float radius = 2.5f;
        public float damage = 5f;
        public float knockback = 5f;

        public override void Use(Weapon weapon, IWeaponHolder user)
        {
            Vector3 origin = user.Position;
            Vector3 aim = user.AimDirection;
            Fx.Sector(origin, aim, radius, arcDegrees, new Color(1f, 0.85f, 0.35f, 0.55f));
            foreach (var c in Query.Characters(Query.AtCastHeight(origin), radius + 0.1f))
            {
                if ((IWeaponHolder)c == user || !c.IsAlive) continue;
                Vector3 to = Query.Flat(c.Position - origin);
                if (to.magnitude - c.Radius > radius) continue;
                if (to.magnitude > c.Radius && Vector3.Angle(aim, to) > arcDegrees * 0.5f) continue;
                Vector3 dir = to.sqrMagnitude > 1e-4f ? to.normalized : aim;
                c.ReceiveDamage(DamageInfo.Attack(damage, user as IDamageDealer, dir, knockback));
            }
        }
    }
}
