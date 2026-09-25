using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// 枪 · 反向射击：子弹朝瞄准方向的反方向飞；开枪的人被往瞄准方向推开。
    /// 反噬：面朝缺口开枪会把自己推下去。利用：背对敌人开枪，后坐力当冲刺。
    /// </summary>
    [CreateAssetMenu(menuName = "Anomaly Arena/Effects/Gun Reverse Shot")]
    public class GunReverseShotEffect : WeaponEffect
    {
        public float damage = 10f;
        public float bulletSpeed = 25f;
        public float bulletRange = 40f;
        [Tooltip("开枪的人被推开的距离（推开撞墙不扣血）")] public float recoilDistance = 3f;

        public override void Use(Weapon weapon, IWeaponHolder user)
        {
            Vector3 aim = user.AimDirection;
            Vector3 start = user.Position - aim * (user.Radius + 0.3f);
            var go = new GameObject("Bullet");
            go.AddComponent<Bullet>().Launch(this, user, start, -aim);
            user.Knockback(aim, recoilDistance, KnockKind.Push);
        }
    }
}
