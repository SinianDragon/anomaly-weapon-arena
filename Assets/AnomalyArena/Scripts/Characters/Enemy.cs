using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// 敌人：一直追玩家，几乎贴到玩家身上才开始蓄力挥拳（身体变黄），蓄力时方向锁定、原地不动。
    /// 蓄完朝锁定方向挥拳：玩家这时还在拳头的扇形范围内才扣血，所以看到变黄就走开可以躲掉。碰到玩家本身不扣血。
    /// 如果玩家钩住的敌人挡在拳头范围内，先打它（掩体）。这一版敌人不捡武器。
    /// </summary>
    public class Enemy : Combatant
    {
        [Header("Attack")]
        public float attackDamage = 5f;
        [Tooltip("离玩家身体边缘多近才开始蓄力（几乎贴上）")] public float triggerRange = 0.3f;
        [Tooltip("拳头落下时的判定距离，从身体边缘算；蓄力期间玩家走出这个距离就躲开了")] public float attackRange = 0.8f;
        [Tooltip("拳头的扇形角度，朝向在开始蓄力时锁定")] public float attackArc = 120f;
        public float windupTime = 0.5f;
        public float attackInterval = 1f;
        [Tooltip("打中玩家时把玩家撞飞的距离")] public float attackKnockback = 5f;

        [Header("Look")]
        public Material normalMaterial;
        public Material windupMaterial;

        [Header("Type")]
        [Tooltip("大型敌人：掉武器、血条更宽、用大型贴图（不再按半径判断）")] public bool large;

        public bool IsLarge => large;
        public bool WindingUp => windup;
        public override Team Team => Team.Enemy;
        public override Vector3 AimDirection => facing;
        public override float CorpseDamage => attackDamage;
        protected override Texture2D ArtTexture => Art.Set ? (IsLarge ? Art.Set.enemyLarge : Art.Set.enemySmall) : null;
        protected override float ArtHeight => IsLarge ? Art.Set.enemyLargeHeight : Art.Set.enemySmallHeight;
        protected override Texture2D ArtWindupTexture => Art.Set ? (IsLarge ? Art.Set.enemyLargeWindup : Art.Set.enemySmallWindup) : null;

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

            if (windup)
            {
                // 方向已锁定，不再跟着玩家转
                Stop();
                windupTimer -= dt;
                // 蓄力中一闪一闪：有发光版就让发光版忽明忽暗，没有就把立牌染黄
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 30f);
                if (artWindupSprite) Art.Tint(artWindupSprite, Color.Lerp(Color.white, new Color(1.35f, 1.3f, 1.1f), pulse));
                else if (artSprite) Art.Tint(artSprite, Color.Lerp(Color.white, new Color(1f, 0.85f, 0.2f), pulse));
                if (windupTimer <= 0f)
                {
                    PerformAttack();
                    CancelWindup();
                    cooldown = attackInterval;
                }
                return;
            }

            Vector3 to = Query.Flat(p.Position - Position);
            float dist = to.magnitude;
            if (dist > 0.01f) facing = to / dist;
            float edge = dist - radius - p.Radius;

            if (edge <= triggerRange && cooldown <= 0f)
            {
                windup = true;
                windupTimer = windupTime;
                SetLook(true);
                Stop();
                return;
            }

            Vector3 v = edge > triggerRange ? facing * moveSpeed : Vector3.zero;
            v = KeepOffEdges(v, dt);
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
        }

        void PerformAttack()
        {
            var arc = Art.On ? Art.Set.enemyPunchArc : null;
            if (arc)
            {
                // 美术版：拳头前方闪一道弧光（贴图右边 = 拳头方向），长度约等于拳头的判定距离
                float reach = radius + attackRange;
                Fx.SpriteFlat(arc, Query.AtCastHeight(Position) + facing * (reach * 0.7f), facing, reach * 1.4f, 0.2f);
            }
            else
            {
                Fx.Sector(Position, facing, radius + attackRange, attackArc, new Color(1f, 0.3f, 0.2f, 0.55f));
            }
            var p = GM.player;
            var shield = p.HeldShield;
            if (shield != null && shield != this && InFist(shield))
            {
                // 打在掩体上不撞飞（距离 0），但给出方向让碎片朝外溅
                shield.ReceiveDamage(DamageInfo.Attack(attackDamage, this, shield.Position - Position));
                return;
            }
            if (p.IsAlive && InFist(p))
                p.ReceiveDamage(DamageInfo.Attack(attackDamage, this, p.Position - Position, attackKnockback));
        }

        /// <summary>目标在拳头的扇形里：边缘距离不超过 attackRange，且在锁定方向左右 attackArc/2 以内（贴身时不看角度）。</summary>
        bool InFist(Combatant c)
        {
            Vector3 to = Query.Flat(c.Position - Position);
            float d = to.magnitude;
            if (d - radius - c.Radius > attackRange) return false;
            return d <= radius + c.Radius * 0.5f || Vector3.Angle(facing, to) <= attackArc * 0.5f;
        }

        /// <summary>
        /// 自己走路时不会主动走下缺口；只有被撞飞、被扔才会掉下去。
        /// 下一步会离开平台（离边不到一个身位）时，先试着只沿 x 或只沿 z 走（贴着边滑），都不行就停下。
        /// </summary>
        Vector3 KeepOffEdges(Vector3 v, float dt)
        {
            if (ArenaShape.ContainsWithMargin(Position + v * dt, radius)) return v;
            var alongX = new Vector3(v.x, 0f, 0f);
            if (ArenaShape.ContainsWithMargin(Position + alongX * dt, radius)) return alongX;
            var alongZ = new Vector3(0f, 0f, v.z);
            if (ArenaShape.ContainsWithMargin(Position + alongZ * dt, radius)) return alongZ;
            return Vector3.zero;
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
            if (ShowArtWindup(yellow)) return;
            if (!yellow && artSprite) Art.Tint(artSprite, Color.white);
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
