using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// 枪 · 反向射击：子弹朝瞄准方向的反方向飞；开枪的人被往瞄准方向推。按住左键连发，一次次数 = 一个弹夹。
    /// 反噬：面朝缺口开枪会把自己推下去。利用：背对敌人开枪，后坐力当冲刺。
    /// 后坐力走 Push（叠加在移动上），不进入被撞飞状态：否则每发之后都要等撞飞结束才能再开枪，射击一顿一顿的。
    /// </summary>
    [CreateAssetMenu(menuName = "Anomaly Arena/Effects/Gun Reverse Shot")]
    public class GunReverseShotEffect : WeaponEffect
    {
        public float damage = 10f;
        public float bulletSpeed = 25f;
        public float bulletRange = 40f;
        [Tooltip("每发后坐力把开枪的人推多远（逐发叠加；撞墙不扣血，推下缺口照样掉）")] public float recoilDistance = 0.8f;

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
