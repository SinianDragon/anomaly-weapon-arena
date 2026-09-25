using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// 玩家与敌人的共同实现：生命、受伤与保护、刚体冲量击飞、撞墙扣血、掉进缺口、状态机、持有武器。
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public abstract class Combatant : MonoBehaviour, IWeaponHolder, IDamageDealer
    {
        [Header("Stats")]
        public float maxHp = 10f;
        public float moveSpeed = 6f;
        public float radius = 0.5f;
        [Tooltip("被撞飞距离的倍率（大型敌人 0.5）")] public float knockbackScale = 1f;
        [Tooltip("挨打后的保护时间，期间攻击伤害无效（撞墙伤害不受影响）")] public float protectionTime = 0f;

        [Header("Refs")]
        public Renderer bodyRenderer;
        public Transform hand;

        public float Hp { get; protected set; }
        public CharacterState State { get; private set; } = CharacterState.Normal;
        public DeathCause DeathCause { get; private set; }
        public Weapon Weapon { get; private set; }
        public bool Protected => protectTimer > 0f;

        public event Action<Combatant> Eliminated;

        protected Rigidbody rb;
        protected Vector3 knockVel;
        protected float weaponCooldown;
        KnockKind knockKind;
        float protectTimer;
        float fallTimer;
        bool eliminated;

        protected static GameManager GM => GameManager.Instance;

        // ───── IWeaponHolder ─────
        public Transform Root => transform;
        public Vector3 Position => rb ? rb.position : transform.position;
        public float Radius => radius;
        public bool IsAlive => State != CharacterState.Dead && State != CharacterState.Falling;
        public IWeaponHolder Owner => this;
        public abstract Team Team { get; }
        public abstract Vector3 AimDirection { get; }
        /// <summary>这具尸体被飞刀带回时撞人的伤害（= 生前的攻击伤害）。</summary>
        public virtual float CorpseDamage => 0f;

        /// <summary>正被这个角色用钩子黏住、可以当掩体的敌人。</summary>
        public Combatant HeldShield => Weapon != null && Weapon.Active is Hook h ? h.HeldTarget : null;
        public bool MovementLocked => Weapon != null && Weapon.Active != null && Weapon.Active.LocksMovement;
        public float SpeedMultiplier => Weapon != null && Weapon.Active != null ? Weapon.Active.SpeedMultiplier : 1f;

        public static Combatant From(Collider col)
        {
            if (col == null) return null;
            if (col.attachedRigidbody) return col.attachedRigidbody.GetComponent<Combatant>();
            return col.GetComponentInParent<Combatant>();
        }

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody>();
            Hp = maxHp;
        }

        // ───── 状态机 ─────

        public void SetState(CharacterState s)
        {
            if (State == s || State == CharacterState.Dead) return;
            var old = State;

            // 退出旧状态
            if (old == CharacterState.Hooked || old == CharacterState.Dashing)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
            }

            State = s;

            // 进入新状态
            switch (s)
            {
                case CharacterState.Normal:
                    knockVel = Vector3.zero;
                    break;
                case CharacterState.Hooked:
                case CharacterState.Dashing:
                    knockVel = Vector3.zero;
                    if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
                    rb.isKinematic = true;
                    break;
                case CharacterState.Falling:
                    fallTimer = 0f;
                    rb.isKinematic = false;
                    break;
                case CharacterState.Dead:
                    if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
                    rb.isKinematic = true;
                    break;
            }
            OnStateChanged(old, s);
        }

        protected virtual void OnStateChanged(CharacterState from, CharacterState to) { }

        protected virtual void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (protectTimer > 0f) protectTimer -= dt;
            if (weaponCooldown > 0f) weaponCooldown -= dt;

            // 兜底：掉出平台很远却没碰到 FallZone
            if (IsAlive && State != CharacterState.Hooked && State != CharacterState.Dashing && rb.position.y < -2f)
                OnEnterFallZone(Vector3.zero);

            switch (State)
            {
                case CharacterState.Knocked:
                case CharacterState.Thrown:
                    knockVel *= Mathf.Exp(-GM.rules.knockDamping * dt);
                    rb.linearVelocity = new Vector3(knockVel.x, rb.linearVelocity.y, knockVel.z);
                    if (knockVel.magnitude < 0.3f) SetState(CharacterState.Normal);
                    break;
                case CharacterState.Falling:
                    fallTimer += dt;
                    if (fallTimer > 2f) OnFallFinished();
                    break;
                case CharacterState.Normal:
                    TickNormal(dt);
                    break;
            }
            if (IsAlive && State != CharacterState.Dashing && State != CharacterState.Hooked)
            {
                Vector3 face = Query.Flat(AimDirection);
                if (face.sqrMagnitude > 1e-4f) rb.MoveRotation(Quaternion.LookRotation(face));
            }
        }

        protected abstract void TickNormal(float dt);

        // ───── 撞飞与撞墙 ─────

        /// <summary>刚体冲量撞飞。距离 d 对应初速度 d × 阻尼系数，指数衰减后正好滑行 d。</summary>
        public void Knockback(Vector3 dir, float distance, KnockKind kind)
        {
            if (!IsAlive || State == CharacterState.Hooked || State == CharacterState.Dashing) return;
            dir = Query.Flat(dir);
            if (dir.sqrMagnitude < 1e-6f || distance <= 0f) return;
            dir.Normalize();
            if (kind == KnockKind.Hit) distance *= knockbackScale;
            knockVel = dir * (distance * GM.rules.knockDamping);
            Vector3 cur = Query.Flat(rb.linearVelocity);
            rb.AddForce(knockVel - cur, ForceMode.VelocityChange);
            knockKind = kind;
            SetState(kind == KnockKind.Throw ? CharacterState.Thrown : CharacterState.Knocked);
        }

        void OnCollisionEnter(Collision c) => CheckWallHit(c);
        void OnCollisionStay(Collision c) => CheckWallHit(c);

        /// <summary>撞飞途中碰到墙：停下；撞击速度超过阈值才扣墙伤，每次撞击只扣一次。</summary>
        void CheckWallHit(Collision c)
        {
            if (State != CharacterState.Knocked && State != CharacterState.Thrown) return;
            if (c.gameObject.layer != GameManager.WallLayer) return;
            for (int i = 0; i < c.contactCount; i++)
            {
                Vector3 to = Query.Flat(c.GetContact(i).point - rb.position);
                if (to.sqrMagnitude < 1e-6f) continue;
                if (Vector3.Dot(knockVel, to.normalized) < GM.rules.wallHitSpeed) continue;
                bool damage = knockKind != KnockKind.Push;
                SetState(CharacterState.Normal);
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                if (damage) TakeWallDamage();
                return;
            }
        }

        void TakeWallDamage()
        {
            if (!IsAlive) return;
            Hp -= GM.rules.wallDamage;
            OnDamaged(GM.rules.wallDamage);
            if (Hp <= 0f) Die(DeathCause.HpDepleted);
        }

        // ───── 受伤与死亡 ─────

        public virtual bool ReceiveDamage(DamageInfo info)
        {
            if (!IsAlive) return false;
            if (info.isAttack && (Protected || State == CharacterState.Dashing)) return false;
            Hp -= info.amount;
            if (info.isAttack && protectionTime > 0f) protectTimer = protectionTime;
            OnDamaged(info.amount);
            if (Hp <= 0f)
            {
                Hp = 0f;
                Die(DeathCause.HpDepleted);
                return true;
            }
            if (info.knockDistance > 0f) Knockback(info.knockDir, info.knockDistance, KnockKind.Hit);
            return true;
        }

        public void Heal(float amount) => Hp = Mathf.Min(maxHp, Hp + amount);

        public void Kill()
        {
            if (!IsAlive) return;
            Hp = 0f;
            Die(DeathCause.HpDepleted);
        }

        protected void Die(DeathCause cause)
        {
            if (!IsAlive) return;
            Hp = Mathf.Max(0f, Hp);
            DeathCause = cause;
            SetState(CharacterState.Dead);
            OnDied();
            ReportEliminated();
        }

        /// <summary>FallZone 触发：被钩住时不会掉；其余情况立即判定掉进缺口。</summary>
        public void OnEnterFallZone(Vector3 outward)
        {
            if (!IsAlive || State == CharacterState.Hooked) return;
            DeathCause = DeathCause.FellIntoGap;
            Vector3 v = State == CharacterState.Dashing || rb.isKinematic ? Vector3.zero : Query.Flat(rb.linearVelocity);
            SetState(CharacterState.Falling);
            rb.linearVelocity = v + outward * 3f;
            OnFell();
            ReportEliminated();
        }

        void ReportEliminated()
        {
            if (eliminated) return;
            eliminated = true;
            Eliminated?.Invoke(this);
        }

        protected virtual void OnDamaged(float amount) { }
        protected virtual void OnDied() { }
        protected virtual void OnFell() { }
        protected virtual void OnFallFinished() { gameObject.SetActive(false); }

        public void SetMovePosition(Vector3 p) => rb.MovePosition(p);

        // ───── 武器 ─────

        public void Equip(Weapon w)
        {
            Weapon = w;
            w.AttachTo(this, hand ? hand : transform);
        }

        public Weapon Unequip()
        {
            var w = Weapon;
            Weapon = null;
            return w;
        }

        /// <summary>按左键：流程进行中（钩子黏住）交给流程处理；否则发动一次效果并扣次。</summary>
        public bool TryUseWeapon()
        {
            var w = Weapon;
            if (w == null || !IsAlive) return false;
            if (w.Active != null)
            {
                w.Active.OnUsePressed();
                return true;
            }
            if (State != CharacterState.Normal || weaponCooldown > 0f) return false;
            w.ApplyDebugForce();
            bool first = !w.Revealed;
            w.Effect.Use(w, this);
            w.UsesLeft--;
            w.Revealed = true;
            weaponCooldown = w.Effect.cooldown;
            if (first) OnWeaponRevealed(w);
            CheckWeaponDepleted();
            return true;
        }

        protected virtual void OnWeaponRevealed(Weapon w) { }

        public void OnWeaponRuntimeFinished() => CheckWeaponDepleted();

        /// <summary>次数用完且没有进行中的流程时，武器消失。</summary>
        public void CheckWeaponDepleted()
        {
            if (Weapon == null || Weapon.UsesLeft > 0 || Weapon.Active != null) return;
            var w = Unequip();
            w.Consume();
        }

    }
}
