using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// 人形导弹冲刺。进：使用者进入 Dashing（刚体改为运动学）。
    /// 出：剩余距离用完 → Normal；撞墙 → 停在墙前、扣一次墙伤、被弹回 wallBounce（SlamIntoWall）；
    ///     中途掉进缺口或死亡 → 直接结束，不改使用者状态。
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
            // 美术版：玩家身后拖一团导弹尾焰（挂在这个流程物体上，流程结束一起销毁）
            var flameTex = Art.On ? (Art.Set.rocketFlame ? Art.Set.rocketFlame : Art.Set.flame) : null;
            if (flameTex)
            {
                flame = Art.Flat(transform, flameTex, 2.2f, Art.OrderProjectile, out _);
                PlaceFlame(u.Position);
            }
        }

        Transform flame;

        void PlaceFlame(Vector3 p)
        {
            if (!flame) return;
            transform.position = Query.AtCastHeight(p) - dir * (user.Radius + 0.7f);
            transform.rotation = Quaternion.LookRotation(dir);
            flame.localScale = new Vector3(1f, 1f, 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 12f, 0f));
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
            // 从身后半个身位开始扫，避免贴墙时扫描起点已在墙内
            Vector3 origin = Query.AtCastHeight(p) - dir * r;
            if (Physics.SphereCast(origin, r * 0.95f, dir, out var hit, step + r, GameManager.WallMask,
                    QueryTriggerInteraction.Ignore))
            {
                // 撞墙：停在墙前，扣墙伤并被弹回；冲刺到此结束
                p += dir * Mathf.Max(0f, hit.distance - r - 0.02f);
                KillAlong(p, r);
                Vector3 away = Query.Flat(hit.normal);
                if (away.sqrMagnitude < 1e-4f) away = -dir;
                user.SlamIntoWall(new Vector3(p.x, user.Position.y, p.z), away.normalized, cfg.wallBounce);
                Finish();
                return;
            }

            p += dir * step;
            user.SetMovePosition(p);
            PlaceFlame(p);
            KillAlong(p, r);

            trailTimer -= Time.fixedDeltaTime;
            if (trailTimer <= 0f && !flame)
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

        /// <summary>冲刺路上碰到的人直接死（大型也算）。</summary>
        void KillAlong(Vector3 p, float r)
        {
            foreach (var c in Query.Characters(Query.AtCastHeight(p), r + 0.1f))
                if ((IWeaponHolder)c != user && c.IsAlive)
                    c.Kill();
        }

        public override void Cancel()
        {
            if (user != null && user.State == CharacterState.Dashing) user.SetState(CharacterState.Normal);
            Finish();
        }
    }
}