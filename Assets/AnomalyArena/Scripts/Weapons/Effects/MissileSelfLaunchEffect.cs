using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Missile · Launch Yourself: the user dashes a fixed distance along the aim direction, instantly killing enemies on the way (large ones too);
    /// a wall stops the dash, deals wall damage once and bounces the user back; attack damage is ignored while dashing.
    /// Backfire: if there is a gap on the path, you dash into it.
    /// </summary>
    [CreateAssetMenu(menuName = "Anomaly Arena/Effects/Missile Self Launch")]
    public class MissileSelfLaunchEffect : WeaponEffect
    {
        public float distance = 15f;
        public float speed = 25f;
        [Tooltip("Distance bounced back after hitting a wall (the bounce itself deals no further wall damage)")] public float wallBounce = 3f;

        public override void Use(Weapon weapon, IWeaponHolder user)
        {
            var go = new GameObject("SelfLaunch");
            go.AddComponent<SelfLaunch>().Begin(this, weapon, user);
        }
    }
}
