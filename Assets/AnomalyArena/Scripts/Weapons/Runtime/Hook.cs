using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// 钩子流程：伸出 → 拉回（随鼠标甩）→ 黏住（掩体）→ 投掷，或者没钩到 → 收回。
    /// 状态进入 / 退出：
    ///   Extend  进：按左键。出：钩到角色 → Pull；碰墙或伸满 → Retract。
    ///   Pull    进：钩到角色，目标进入 Hooked。出：拉到身前 → Hold；目标失效 → Retract。
    ///   Hold    进：拉到身前。出：左键或右键换武器 → 投掷结束；目标死亡 → 结束。
    ///   Retract 进：没钩到。出：收回到 0 → 结束。
    /// </summary>
    public class Hook : WeaponRuntime
    {
        enum Phase { Extend, Pull, Hold, Retract }

        KnifeHookEffect cfg;
        Phase phase;
        Vector3 dir;
        float length;
        float pullDistance;
        Combatant target;

        public Combatant HeldTarget => phase == Phase.Hold && Valid() ? target : null;
        public override bool LocksMovement => phase != Phase.Hold;
        public override float SpeedMultiplier => phase == Phase.Hold ? cfg.dragSpeedMultiplier : 1f;
        public override bool CanSwap => phase == Phase.Hold;

        public void Begin(KnifeHookEffect e, Weapon w, IWeaponHolder u)
        {
            cfg = e;
            Bind(w, u);
            dir = u.AimDirection;
            phase = Phase.Extend;
            UpdateBlade();
        }

        bool Valid() => target != null && target.IsAlive && target.State == CharacterState.Hooked;

        float HoldDistance => user.Radius + target.Radius + 0.1f;

        void FixedUpdate()
        {
            if (user == null || !user.IsAlive)
            {
                Cancel();
                return;
            }
            float dt = Time.fixedDeltaTime;
            switch (phase)
            {
                case Phase.Extend:
                {
                    length = Mathf.Min(length + cfg.extendSpeed * dt, cfg.range);
                    Vector3 origin = Query.AtCastHeight(user.Position);
                    int mask = GameManager.WallMask | GameManager.CharacterMask;
                    // 从使用者体内出发，SphereCast 会自动忽略使用者自己
                    if (Physics.SphereCast(origin, cfg.hookRadius, dir, out var hit, length, mask, QueryTriggerInteraction.Ignore))
                    {
                        var c = Combatant.From(hit.collider);
                        if (c != null && (IWeaponHolder)c != user && c.IsAlive && c.State != CharacterState.Hooked)
                        {
                            target = c;
                            c.SetState(CharacterState.Hooked);
                            pullDistance = Query.Flat(c.Position - user.Position).magnitude;
                            phase = Phase.Pull;
                        }
                        else if (c == null || (IWeaponHolder)c != user)
                        {
                            length = hit.distance;
                            phase = Phase.Retract;
                        }
                    }
                    else if (length >= cfg.range)
                    {
                        phase = Phase.Retract;
                    }
                    break;
                }
                case Phase.Pull:
                {
                    if (!Valid())
                    {
                        Release();
                        phase = Phase.Retract;
                        break;
                    }
                    // 拉的过程中移动鼠标，敌人跟着甩向那一边；拉回途中不会掉进缺口（Hooked 忽略 FallZone）
                    pullDistance = Mathf.MoveTowards(pullDistance, HoldDistance, cfg.pullSpeed * dt);
                    dir = user.AimDirection;
                    PlaceTarget(pullDistance);
                    if (pullDistance <= HoldDistance + 1e-3f) phase = Phase.Hold;
                    break;
                }
                case Phase.Hold:
                {
                    if (!Valid())
                    {
                        target = null;
                        Finish();
                        return;
                    }
                    dir = user.AimDirection;
                    PlaceTarget(HoldDistance);
                    break;
                }
                case Phase.Retract:
                    length -= cfg.retractSpeed * dt;
                    if (length <= 0f)
                    {
                        Finish();
                        return;
                    }
                    break;
            }
            UpdateBlade();
        }

        void PlaceTarget(float distance)
        {
            float limit = GameManager.Instance.rules.arenaHalfSize - target.Radius;
            Vector3 p = user.Position + dir * distance;
            p.x = Mathf.Clamp(p.x, -limit, limit);
            p.z = Mathf.Clamp(p.z, -limit, limit);
            p.y = target.Position.y;
            target.SetMovePosition(p);
            length = Query.Flat(p - user.Position).magnitude;
        }

        public override void OnUsePressed()
        {
            if (phase == Phase.Hold) Throw();
        }

        public override void OnSwapAway()
        {
            if (phase == Phase.Hold) Throw();
        }

        /// <summary>朝当前瞄准方向扔出去，本身不造成伤害；撞墙按规则扣墙伤，飞进缺口掉下去。</summary>
        void Throw()
        {
            var t = target;
            target = null;
            if (t != null && t.IsAlive && t.State == CharacterState.Hooked)
            {
                t.SetState(CharacterState.Normal);
                t.Knockback(user.AimDirection, cfg.throwDistance, KnockKind.Throw);
            }
            Finish();
        }

        void Release()
        {
            if (target != null && target.State == CharacterState.Hooked) target.SetState(CharacterState.Normal);
            target = null;
        }

        public override void Cancel()
        {
            Release();
            Finish();
        }

        void UpdateBlade()
        {
            Vector3 origin = Query.AtCastHeight(user.Position);
            float len = Mathf.Max(0.05f, length);
            transform.position = origin + dir * (len * 0.5f);
            transform.rotation = Quaternion.LookRotation(dir);
            transform.localScale = new Vector3(0.12f, 0.06f, len);
        }
    }
}
