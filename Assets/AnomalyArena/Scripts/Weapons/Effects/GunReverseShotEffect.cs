using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Gun · Reverse Shot: bullets fly opposite to the aim direction; the shooter is pushed toward the aim direction. Hold the left button to keep firing; one use = one clip.
    /// Backfire: firing while facing a gap pushes you in. Exploit: fire with your back to the enemies and use the recoil as a dash.
    /// Recoil uses Push (added on top of movement) and does not enter the knocked state: otherwise each shot would have to wait for the knockback to end and firing would stutter.
    /// </summary>
    [CreateAssetMenu(menuName = "Anomaly Arena/Effects/Gun Reverse Shot")]
    public class GunReverseShotEffect : WeaponEffect
    {
        public float damage = 10f;
        public float bulletSpeed = 25f;
        public float bulletRange = 40f;
        [Tooltip("How far each shot's recoil pushes the shooter (stacks per shot; no wall damage, but can push you into a gap)")] public float recoilDistance = 0.8f;

        public override void Use(Weapon weapon, IWeaponHolder user)
        {
            Vector3 aim = user.AimDirection;
            Vector3 start = user.Position - aim * (user.Radius + 0.3f);
            var go = new GameObject("Bullet");
            go.AddComponent<Bullet>().Launch(this, user, start, -aim);
            user.Push(aim, recoilDistance);
        }
    }
}
