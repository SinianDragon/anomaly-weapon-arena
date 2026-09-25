using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// 导弹 · 追踪：发射后朝一个随机方向飞（和瞄准方向无关）；碰到敌人或墙就爆炸。
    /// 反噬：飞了 1 秒还没炸，就锁定使用者追过来。爆炸伤到范围内所有人，包括发射的人。
    /// </summary>
    [CreateAssetMenu(menuName = "Anomaly Arena/Effects/Missile Homing")]
    public class MissileHomingEffect : WeaponEffect
    {
        public float speed = 8f;
        [Tooltip("随机方向的范围：360 = 完全随机；小于 360 则在瞄准方向左右这个角度内随机（待定）")]
        [Range(0f, 360f)] public float randomSpreadDegrees = 360f;
        public float lockDelay = 1f;
        [Tooltip("锁定后每秒最多转多少度")] public float turnRate = 90f;
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
