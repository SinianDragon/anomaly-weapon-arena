using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Gun · Charge Swing: hold the left button to charge (movement slows, a growing sector is shown on the ground), release to swing.
    /// The longer the charge, the larger the range and the stronger the damage and knockback; a tap also swings, with the base range only.
    /// No backfire; the way to exploit it is knocking enemies into gaps or walls. Being knocked back while charging interrupts it, and the use is still spent.
    /// </summary>
    [CreateAssetMenu(menuName = "Anomaly Arena/Effects/Gun Swing")]
    public class GunSwingEffect : WeaponEffect
    {
        [Header("Tap (no charge)")] [Tooltip("Measured from the user's center")]
        public float minRadius = 2.5f;

        public float minArc = 90f;
        public float minDamage = 6f;
        public float minKnockback = 5f;

        [Header("Full charge")] public float maxRadius = 5f;
        public float maxArc = 150f;
        public float maxDamage = 15f;
        public float maxKnockback = 10f;

        [Header("Charging")] [Tooltip("Seconds to reach full charge")]
        public float maxChargeTime = 1f;

        [Tooltip("Movement speed multiplier while charging")] public float chargeMoveMultiplier = 0.4f;

        public override void Use(Weapon weapon, IWeaponHolder user)
        {
            new GameObject("SwingCharge").AddComponent<SwingCharge>().Begin(this, weapon, user);
        }

        /// <summary>Swings at charge ratio k (0–1).</summary>
        public void Swing(IWeaponHolder user, float k)
        {
            float radius = Mathf.Lerp(minRadius, maxRadius, k);
            float arc = Mathf.Lerp(minArc, maxArc, k);
            float damage = Mathf.Round(Mathf.Lerp(minDamage, maxDamage, k));
            float knockback = Mathf.Lerp(minKnockback, maxKnockback, k);
            Vector3 origin = user.Position;
            Vector3 aim = user.AimDirection;
            Fx.Sector(origin, aim, radius, arc, new Color(1f, 0.4f, 0.1f, Mathf.Lerp(0.6f, 0.9f, k)));
            // Area damage: everyone in the sector is hit (unlike the unarmed punch, which hits only the nearest)
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