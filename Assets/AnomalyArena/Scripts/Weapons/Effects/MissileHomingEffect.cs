using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Missile · Homing: flies off in a random direction after launch (unrelated to the aim direction); explodes on touching a character or a wall.
    /// If it has not exploded after 1 second, it locks onto a weighted-random target on the field (the player has a higher weight) and chases it.
    /// The blast hits everyone in range, including whoever fired it.
    /// </summary>
    [CreateAssetMenu(menuName = "Anomaly Arena/Effects/Missile Homing")]
    public class MissileHomingEffect : WeaponEffect
    {
        public float speed = 8f;
        [Tooltip("Range of the random direction: 360 = fully random; below 360 = random within this angle either side of the aim direction (to be decided)")]
        [Range(0f, 360f)] public float randomSpreadDegrees = 360f;
        public float lockDelay = 1f;
        [Tooltip("Weight of the player when picking a target")] public float playerWeight = 3f;
        [Tooltip("Weight of each enemy when picking a target")] public float enemyWeight = 1f;
        [Tooltip("Maximum degrees per second it can turn after locking on")] public float turnRate = 90f;
        public float lifetime = 5f;
        public float explosionRadius = 3f;
        public float damage = 10f;
        public float knockback = 5f;

        public override void Use(Weapon weapon, IWeaponHolder user)
        {
            float half = randomSpreadDegrees * 0.5f;
            Vector3 dir = Quaternion.AngleAxis(Random.Range(-half, half), Vector3.up) * user.AimDirection;
            var go = new GameObject("HomingMissile");
            go.AddComponent<HomingMissile>().Launch(this, user, user.Position + dir * (user.Radius + 0.4f), dir);
        }
    }
}
