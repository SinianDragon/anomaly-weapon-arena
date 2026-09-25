using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// 人形导弹冲刺。进：使用者进入 Dashing（刚体改为运动学）。
    /// 出：剩余距离用完 → Normal；中途掉进缺口或死亡 → 直接结束，不改使用者状态。
    /// </summary>
    public class SelfLaunch : WeaponRuntime
    {
        MissileSelfLaunchEffect cfg;
        Vector3 dir;
        float remaining;
        float trailTimer;

        public override bool LocksMovement => true;

        public void Begin(MissileSelfLaunchEffect e, Weapon w, IWeaponHolder u)
        {
            cfg = e;
            Bind(w, u);
            dir = Query.Flat(u.AimDirection).normalized;
            remaining = cfg.distance;
            u.SetState(CharacterState.Dashing);
        }

        void FixedUpdate()
        {
            if (user == null || user.State != CharacterState.Dashing)
            {
                Finish();
                return;
            }
            float step = Mathf.Min(cfg.speed * Time.fixedDeltaTime, remaining);
            remaining -= step;

            Vector3 p = user.Position;
            float r = user.Radius;
            for (int i = 0; i < 4 && step > 1e-4f; i++)
            {
                // 从身后半个身位开始扫，避免贴墙时扫描起点已在墙内
                Vector3 origin = Query.AtCastHeight(p) - dir * r;
                if (Physics.SphereCast(origin, r * 0.95f, dir, out var hit, step + r, GameManager.WallMask, QueryTriggerInteraction.Ignore))
                {
                    float move = Mathf.Max(0f, hit.distance - r - 0.02f);
                    p += dir * move;
                    step -= move;
                    Vector3 n = Query.Flat(hit.normal).normalized;
                    dir = Vector3.Reflect(dir, n);
                    dir = Query.Flat(dir).normalized; // 撞墙反弹，不扣墙伤，用剩下的距离继续飞
                    if (move < 1e-3f) step -= 0.01f;
                }
                else
                {
                    p += dir * step;
                    step = 0f;
                }
            }
            user.SetMovePosition(p);

            foreach (var c in Query.Characters(Query.AtCastHeight(p), r + 0.1f))
                if ((IWeaponHolder)c != user && c.IsAlive) c.Kill(); // 大型也直接死

            trailTimer -= Time.fixedDeltaTime;
            if (trailTimer <= 0f)
            {
                trailTimer = 0.04f;
                Fx.Pop(Query.AtCastHeight(p), new Color(0.9f, 0.35f, 0.2f), 0.8f);
            }

            if (remaining <= 0f)
            {
                user.SetState(CharacterState.Normal);
                Finish();
            }
        }

        public override void Cancel()
        {
            if (user != null && user.State == CharacterState.Dashing) user.SetState(CharacterState.Normal);
            Finish();
        }
    }
}
