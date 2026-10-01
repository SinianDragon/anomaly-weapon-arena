using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Knife · Throwing Knife: flies straight along the aim direction; after a hit it bounces to the next nearby character it has not hit, up to maxTargets, then flies back.
    /// The knife cannot be thrown again until it returns. The first enemy it kills is carried back with it.
    /// Backfire: the carried corpse hits you, dealing that corpse's attack damage when alive and knocking you back.
    /// </summary>
    [CreateAssetMenu(menuName = "Anomaly Arena/Effects/Knife Throw")]
    public class KnifeThrowEffect : WeaponEffect
    {
        [Tooltip("Length of the first straight flight; returns if nothing is hit")] public float range = 12f;
        public float speed = 18f;
        [Tooltip("Damage per character hit")] public float damage = 10f;
        [Tooltip("Maximum characters hit in a row (including the first)")] [Min(1)] public int maxTargets = 4;
        [Tooltip("After a hit, how far to look for the next target (targets behind a wall do not count)")] public float bounceRange = 7f;
        public float returnSpeed = 14f;
        [Tooltip("Knockback distance when the corpse hits someone")] public float corpseKnockback = 5f;

        public override void Use(Weapon weapon, IWeaponHolder user)
        {
            var go = new GameObject("ThrownKnife");
            go.AddComponent<ThrownKnife>().Launch(this, weapon, user, user.Position + user.AimDirection * (user.Radius + 0.2f), user.AimDirection);
        }
    }
}
