using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// 枪 · 蓄力挥砍：按住左键蓄力（移动变慢，地上显示逐渐变大的扇形），松开挥出。
    /// 蓄得越久范围越大、伤害和撞飞越强；轻点也能挥，但只有基础范围。
    /// 没有反噬；利用方式是把敌人撞进缺口或撞墙。蓄力中被撞飞会打断，这一次照样扣次。
    /// </summary>
    [CreateAssetMenu(menuName = "Anomaly Arena/Effects/Gun Swing")]
    public class GunSwingEffect : WeaponEffect
    {
        [Header("Tap (no charge)")] [Tooltip("从使用者中心算")]
        public float minRadius = 2.5f;

        public float minArc = 90f;
        public float minDamage = 6f;
        public float minKnockback = 5f;

        [Header("Full charge")] public float maxRadius = 5f;
        public float maxArc = 150f;
        public float maxDamage = 15f;
        public float maxKnockback = 10f;

        [Header("Charging")] [Tooltip("蓄满需要的秒数")]
        public float maxChargeTime = 1f;

        [Tooltip("蓄力时的移动速度倍率")] public float chargeMoveMultiplier = 0.4f;

        public override void Use(Weapon weapon, IWeaponHolder user)
        {
            new GameObject("SwingCharge").AddComponent<SwingCharge>().Begin(this, weapon, user);
        }

        /// <summary>按蓄力比例 k（0–1）挥出。</summary>
        public void Swing(IWeaponHolder user, float k)
        {
            float radius = Mathf.Lerp(minRadius, maxRadius, k);
            float arc = Mathf.Lerp(minArc, maxArc, k);
            float damage = Mathf.Round(Mathf.Lerp(minDamage, maxDamage, k));
            float knockback = Mathf.Lerp(minKnockback, maxKnockback, k);
            Vector3 origin = user.Position;
            Vector3 aim = user.AimDirection;
            Fx.Sector(origin, aim, radius, arc, new Color(1f, 0.4f, 0.1f, Mathf.Lerp(0.6f, 0.9f, k)));
            // 范围伤害：扇形里的人全部打到（和空手出拳只打最近一个不同）
            foreach (var c in Query.InSector(user, origin, aim, radius, arc))
            {
                Vector3 to = Query.Flat(c.Position - origin);
                Vector3 dir = to.sqrMagnitude > 1e-4f ? to.normalized : aim;
                if (c.ReceiveDamage(DamageInfo.Attack(damage, user as IDamageDealer, dir, knockback)))
                    Fx.Pop(Query.AtCastHeight(c.Position), new Color(1f, 0.85f, 0.3f), 1f + k * 1.5f);
            }
        }
    }
}