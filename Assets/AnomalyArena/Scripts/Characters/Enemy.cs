using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// 敌人：一直追玩家，进入攻击距离后蓄力（身体变黄），蓄完才出伤害。碰到玩家本身不扣血。
    /// 如果玩家钩住的敌人也在攻击范围内，先打它（掩体）。这一版敌人不捡武器。
    /// </summary>
    public class Enemy : Combatant
    {
        [Header("Attack")]
        public float attackDamage = 5f;
        [Tooltip("从身体边缘算")] public float attackRange = 1.5f;
        public float windupTime = 0.4f;
        public float attackInterval = 1f;
        [Tooltip("打中玩家时把玩家撞飞的距离")] public float attackKnockback = 5f;

        [Header("Look")]
        public Material normalMaterial;
        public Material windupMaterial;

        public bool IsLarge => radius > 1f;
        public bool WindingUp => windup;
        public override Team Team => Team.Enemy;
        public override Vector3 AimDirection => facing;
        public override float CorpseDamage => attackDamage;

        Vector3 facing = Vector3.back;
        bool windup;
        float windupTimer;
        float cooldown;

        protected override void TickNormal(float dt)
        {
            if (cooldown > 0f) cooldown -= dt;
            var p = GM.player;
            if (GM.State != GameState.Playing || p == null || !p.IsAlive)
            {
                Stop();
                CancelWindup();
                return;
            }

            Vector3 to = Query.Flat(p.Position - Position);
            float dist = to.magnitude;
            if (dist > 0.01f) facing = to / dist;
            float edge = dist - radius - p.Radius;

            if (windup)
            {
                Stop();
                windupTimer -= dt;
                if (windupTimer <= 0f)
                {
                    PerformAttack();
                    CancelWindup();
                    cooldown = attackInterval;
                }
                return;
            }

            if (edge <= attackRange && cooldown <= 0f)
            {
                windup = true;
                windupTimer = windupTime;
                SetLook(true);
                Stop();
                return;
            }

            Vector3 v = edge > attackRange * 0.5f ? facing * moveSpeed : Vector3.zero;
            v = KeepOffEdges(v, dt);
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
        }

        void PerformAttack()
        {
            var p = GM.player;
            var shield = p.HeldShield;
            if (shield != null && shield != this && EdgeDistance(shield) <= attackRange)
            {
                shield.ReceiveDamage(DamageInfo.Attack(attackDamage, this));
                return;
            }
            if (p.IsAlive && EdgeDistance(p) <= attackRange)
                p.ReceiveDamage(DamageInfo.Attack(attackDamage, this, p.Position - Position, attackKnockback));
        }

        float EdgeDistance(Combatant c) => Query.Flat(c.Position - Position).magnitude - radius - c.Radius;

        /// <summary>自己走路时不会主动走下缺口；只有被撞飞、被扔才会掉下去。</summary>
        Vector3 KeepOffEdges(Vector3 v, float dt)
        {
            float limit = GM.rules.arenaHalfSize - radius;
            Vector3 next = Position + v * dt;
            if (Mathf.Abs(next.x) > limit && Mathf.Sign(v.x) == Mathf.Sign(next.x)) v.x = 0f;
            if (Mathf.Abs(next.z) > limit && Mathf.Sign(v.z) == Mathf.Sign(next.z)) v.z = 0f;
            return v;
        }

        void Stop() => rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        void CancelWindup()
        {
            if (!windup) return;
            windup = false;
            SetLook(false);
        }

        void SetLook(bool yellow)
        {
            if (bodyRenderer) bodyRenderer.sharedMaterial = yellow && windupMaterial ? windupMaterial : normalMaterial;
        }

        protected override void OnStateChanged(CharacterState from, CharacterState to)
        {
            if (to != CharacterState.Normal) CancelWindup();
        }

        protected override void OnDied()
        {
            Fx.Pop(Position + Vector3.up * radius, bodyRenderer ? bodyRenderer.sharedMaterial.color : Color.red, radius * 3f);
            Destroy(gameObject);
        }

        protected override void OnFallFinished() => Destroy(gameObject);
    }
}
