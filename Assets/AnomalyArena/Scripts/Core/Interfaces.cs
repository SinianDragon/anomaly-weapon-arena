using UnityEngine;

namespace AnomalyArena
{
    public enum Team
    {
        Player,
        Enemy
    }

    /// <summary>角色状态机的状态。每种状态的进入 / 退出写在 Combatant.SetState 里。</summary>
    public enum CharacterState
    {
        Normal, // 自由行动
        Knocked, // 被撞飞：不能操作，撞墙扣血
        Thrown, // 被钩子扔出去：同上，距离不受体型影响
        Hooked, // 被钩住：不能移动、不能攻击，位置由钩子控制，不会掉进缺口
        Dashing, // 人形导弹冲刺中：不吃攻击伤害，不被撞飞
        Falling, // 掉进缺口：已判定消灭 / 失败，只剩下落表现
        Dead,
    }

    public enum KnockKind
    {
        Hit, // 普通撞飞：距离乘体型系数，撞墙扣血
        Throw, // 钩子投掷：距离固定，撞墙扣血
        Push, // 后坐力推开：撞墙不扣血
    }

    public enum DeathCause
    {
        None,
        HpDepleted,
        FellIntoGap
    }

    /// <summary>受击特效的种类：决定碎片颜色、多少和有没有冲击环。</summary>
    public enum HitKind
    {
        Blunt, // 拳头、挥砍
        Bullet, // 子弹
        Pierce, // 飞刀、人形导弹穿过
        Slam, // 撞墙、被尸体砸到
        Blast, // 爆炸
    }

    public struct DamageInfo
    {
        public float amount;

        /// <summary>攻击伤害受 0.5 秒保护与冲刺免疫影响；撞墙伤害不走这里。</summary>
        public bool isAttack;

        /// <summary>撞飞方向；为零时受击特效的碎片随机飞。</summary>
        public Vector3 knockDir;

        public float knockDistance;
        public IDamageDealer dealer;
        public HitKind kind;

        public static DamageInfo Attack(float amount, IDamageDealer dealer, Vector3 knockDir = default,
            float knockDistance = 0f,
            HitKind kind = HitKind.Blunt) =>
            new DamageInfo
            {
                amount = amount, isAttack = true, dealer = dealer, knockDir = knockDir, knockDistance = knockDistance,
                kind = kind
            };
    }

    public interface IDamageDealer
    {
        IWeaponHolder Owner { get; }
    }

    public interface IDamageReceiver
    {
        bool IsAlive { get; }

        /// <returns>这次伤害是否真正生效（保护中 / 冲刺中返回 false）</returns>
        bool ReceiveDamage(DamageInfo info);
    }

    /// <summary>
    /// “能拿武器的人”：能拿武器、能被撞飞、能受伤、有朝向。
    /// 武器效果只通过这个接口操作使用者，以后敌人捡武器时不用改武器代码。
    /// </summary>
    public interface IWeaponHolder : IDamageReceiver
    {
        Transform Root { get; }
        Vector3 Position { get; }
        Vector3 AimDirection { get; }
        float Radius { get; }
        Team Team { get; }
        Weapon Weapon { get; }
        CharacterState State { get; }

        void Knockback(Vector3 dir, float distance, KnockKind kind);

        /// <summary>叠加在自己移动上的推力（连发后坐力）：不进入被撞飞状态、不打断操作，撞墙不扣血，推下缺口照样掉。</summary>
        void Push(Vector3 dir, float distance);

        /// <summary>撞墙（人形导弹）：瞬移到 at、扣一次墙伤，再朝 away 弹回 bounceDistance。</summary>
        void SlamIntoWall(Vector3 at, Vector3 away, float bounceDistance);

        void SetState(CharacterState state);
        void SetMovePosition(Vector3 position);
        void Kill();

        /// <summary>武器上的持续流程（钩子、冲刺）结束时调用，用来处理次数耗尽的武器。</summary>
        void OnWeaponRuntimeFinished();
    }
}