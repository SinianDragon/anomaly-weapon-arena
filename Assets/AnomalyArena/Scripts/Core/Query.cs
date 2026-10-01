using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Small physics query helpers. All checks are done at the fixed height CastHeight.</summary>
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

        /// <summary>All characters within a sphere (deduplicated).</summary>
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
        /// All living characters in an arc in front (excluding self): body edge within reach, direction within arc/2; angle ignored when touching.
        /// Used by area attacks (Charge Swing).
        /// </summary>
        public static List<Combatant> InSector(IWeaponHolder self, Vector3 origin, Vector3 aim, float reach, float arcDeg)
        {
            var list = new List<Combatant>();
            // Query 2 extra units: a large enemy whose center is outside reach but whose body edge is inside still counts
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

        /// <summary>Single-target melee (unarmed punch): the one character in the same arc whose body edge is closest to origin. Null if none.</summary>
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

        /// <summary>Whether the segment from from to to hits a wall.</summary>
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

        /// <summary>Turns dir toward want on the XZ plane at a limited angular speed.</summary>
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
