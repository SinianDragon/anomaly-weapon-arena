using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// 刀 · 飞刀：沿瞄准方向直线飞，不追踪，碰到的第一个敌人扣血。打死了就带着尸体飞回来，没打死就空着飞回来。
    /// 反噬：带回的尸体撞到你，扣这具尸体生前的攻击伤害并把你撞飞。
    /// </summary>
    [CreateAssetMenu(menuName = "Anomaly Arena/Effects/Knife Throw")]
    public class KnifeThrowEffect : WeaponEffect
    {
        public float range = 12f;
        public float speed = 18f;
        public float damage = 10f;
        public float returnSpeed = 12f;
        [Tooltip("尸体撞到人时的撞飞距离")] public float corpseKnockback = 5f;

        public override void Use(Weapon weapon, IWeaponHolder user)
        {
            var go = new GameObject("ThrownKnife");
            go.AddComponent<ThrownKnife>().Launch(this, user, user.Position + user.AimDirection * (user.Radius + 0.2f), user.AimDirection);
        }
    }
}
