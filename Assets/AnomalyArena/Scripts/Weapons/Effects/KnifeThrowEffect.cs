using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// 刀 · 飞刀：沿瞄准方向直线飞出；打中人后弹向附近下一个没打过的人，最多打 maxTargets 个，然后飞回。
    /// 飞刀回来之前这把刀不能再扔。打死的第一个敌人会被带着一起飞回。
    /// 反噬：带回的尸体撞到你，扣这具尸体生前的攻击伤害并把你撞飞。
    /// </summary>
    [CreateAssetMenu(menuName = "Anomaly Arena/Effects/Knife Throw")]
    public class KnifeThrowEffect : WeaponEffect
    {
        [Tooltip("第一段直线飞多远，没打中人就返回")] public float range = 12f;
        public float speed = 18f;
        [Tooltip("每打中一个人的伤害")] public float damage = 10f;
        [Tooltip("最多连续打中几个人（含第一个）")] [Min(1)] public int maxTargets = 4;
        [Tooltip("打中一个人后，在多远内找下一个目标（中间隔墙的不算）")] public float bounceRange = 7f;
        public float returnSpeed = 14f;
        [Tooltip("尸体撞到人时的撞飞距离")] public float corpseKnockback = 5f;

        public override void Use(Weapon weapon, IWeaponHolder user)
        {
            var go = new GameObject("ThrownKnife");
            go.AddComponent<ThrownKnife>().Launch(this, weapon, user, user.Position + user.AimDirection * (user.Radius + 0.2f), user.AimDirection);
        }
    }
}
