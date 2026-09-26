using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>物理查询的小工具。所有判定都在固定高度 CastHeight 上做。</summary>
    public static class Query
    {
        public const float CastHeight = 0.8f;
        static readonly Collider[] buffer = new Collider[64];

        public static Vector3 AtCastHeight(Vector3 p) => new Vector3(p.x, CastHeight, p.z);

        public static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        /// <summary>球形范围内的所有角色（去重）。</summary>
        public static List<Combatant> Characters(Vector3 center, float radius)
        {
            var list = new List<Combatant>();
            int n = Physics.OverlapSphereNonAlloc(center, radius, buffer, GameManager.CharacterMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = Combatant.From(buffer[i]);
                if (c != null && !list.Contains(c)) list.Add(c);
            }
            return list;
        }

        /// <summary>
        /// 前方扇形里的所有活着的角色（不含 self）：身体边缘在 reach 以内、方向在 arc/2 以内；贴身时不看角度。
        /// 范围攻击（蓄力挥砍）用。
        /// </summary>
        public static List<Combatant> InSector(IWeaponHolder self, Vector3 origin, Vector3 aim, float reach, float arcDeg)
        {
            var list = new List<Combatant>();
            // 多查 2 格：大型敌人中心在 reach 外、身体边缘在 reach 内也要算
            foreach (var c in Characters(AtCastHeight(origin), reach + 2f))
            {
                if ((IWeaponHolder)c == self || !c.IsAlive) continue;
                Vector3 to = Flat(c.Position - origin);
                float d = to.magnitude;
                if (d - c.Radius > reach) continue;
                if (d > c.Radius && Vector3.Angle(aim, to) > arcDeg * 0.5f) continue;
                list.Add(c);
            }
            return list;
        }

        /// <summary>近战单体攻击（空手出拳）：同一个扇形里身体边缘离 origin 最近的那一个。没有返回 null。</summary>
        public static Combatant MeleeTarget(IWeaponHolder self, Vector3 origin, Vector3 aim, float reach, float arcDeg)
        {
            Combatant best = null;
            float bestEdge = float.MaxValue;
            foreach (var c in InSector(self, origin, aim, reach, arcDeg))
            {
                float edge = Flat(c.Position - origin).magnitude - c.Radius;
                if (edge >= bestEdge) continue;
                bestEdge = edge;
                best = c;
            }
            return best;
        }

        /// <summary>从 from 到 to 这一段是否撞到墙。</summary>
        public static bool WallBetween(Vector3 from, Vector3 to, out RaycastHit hit)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 1e-5f)
            {
                hit = default;
                return false;
            }
            return Physics.Raycast(from, d / len, out hit, len, GameManager.WallMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>在 XZ 平面上把 dir 按有限角速度转向 want。</summary>
        public static Vector3 Steer(Vector3 dir, Vector3 want, float maxDegrees)
        {
            want = Flat(want);
            if (want.sqrMagnitude < 1e-6f) return dir;
            float angle = Vector3.SignedAngle(dir, want, Vector3.up);
            float step = Mathf.Clamp(angle, -maxDegrees, maxDegrees);
            return (Quaternion.AngleAxis(step, Vector3.up) * dir).normalized;
        }
    }
}
