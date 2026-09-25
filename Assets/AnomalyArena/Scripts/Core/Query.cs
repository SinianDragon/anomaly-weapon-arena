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
