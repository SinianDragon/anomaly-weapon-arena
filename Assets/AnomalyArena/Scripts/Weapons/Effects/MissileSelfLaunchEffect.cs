using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// 导弹 · 发射自己：使用者朝瞄准方向冲出固定距离，路上碰到的敌人直接死（大型也算）；
    /// 撞墙反弹，用剩下的距离继续飞，这一下不扣墙伤；冲刺时不吃攻击伤害。
    /// 反噬：路线上有缺口就会冲下去。
    /// </summary>
    [CreateAssetMenu(menuName = "Anomaly Arena/Effects/Missile Self Launch")]
    public class MissileSelfLaunchEffect : WeaponEffect
    {
        public float distance = 15f;
        public float speed = 25f;

        public override void Use(Weapon weapon, IWeaponHolder user)
        {
            var go = new GameObject("SelfLaunch");
            go.AddComponent<SelfLaunch>().Begin(this, weapon, user);
        }
    }
}
