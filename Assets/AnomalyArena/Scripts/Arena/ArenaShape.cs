using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Shape of the platform: an irregular polygon (counter-clockwise; top-down, x right, z up). Walls are built along the outside of the edges; a gap is a stretch of an edge with no wall.
    /// The editor scene builder (ArenaSetup) and the game logic (spawn points, weapon positions, enemies not walking off the edge, hook limits) share this one data set.
    /// </summary>
    public static class ArenaShape
    {
        /// <summary>Polygon vertices. Some corners are chamfered and two edges bend inward (V1, V11). The bounding area is about the same as the original 30 × 30, so the camera did not need to move.</summary>
        public static readonly Vector2[] Vertices =
        {
            new Vector2(-11f, -15f),   // V0
            new Vector2(-3f, -13.5f),  // V1 bottom edge bends inward
            new Vector2(9f, -15f),     // V2
            new Vector2(14f, -11f),    // V3
            new Vector2(16f, -4f),     // V4
            new Vector2(15f, 7f),      // V5
            new Vector2(12f, 13.5f),   // V6
            new Vector2(3f, 15f),      // V7
            new Vector2(-8f, 14f),     // V8
            new Vector2(-13.5f, 10.5f), // V9
            new Vector2(-15.5f, 2f),   // V10
            new Vector2(-14f, -6f),    // V11 left edge bends inward
            new Vector2(-16f, -11f),   // V12
        };

        public struct Gap
        {
            public string name;
            /// <summary>Which edge it is on (edge i goes from vertex i to vertex i+1).</summary>
            public int edge;
            /// <summary>Distance from the edge start to the gap midpoint; negative means the middle of the edge.</summary>
            public float center;
            public float width;
        }

        // Small gap: wider than the player and small enemies (diameter 1), narrower than large enemies (diameter 2), so large ones get stuck
        public const float SmallGapWidth = 1.6f;
        // Large gap: everyone falls through
        public const float LargeGapWidth = 7f;

        public static readonly Gap[] Gaps =
        {
            new Gap { name = "LargeGap_North", edge = 7, center = -1f, width = LargeGapWidth },
            new Gap { name = "LargeGap_South", edge = 1, center = -1f, width = LargeGapWidth },
            new Gap { name = "LargeGap_East", edge = 4, center = -1f, width = LargeGapWidth },
            new Gap { name = "SmallGap_East", edge = 3, center = -1f, width = SmallGapWidth },
            new Gap { name = "SmallGap_West", edge = 10, center = -1f, width = SmallGapWidth },
        };

        public static int EdgeCount => Vertices.Length;
        public static Vector2 EdgeStart(int i) => Vertices[i];
        public static Vector2 EdgeEnd(int i) => Vertices[(i + 1) % Vertices.Length];
        public static float EdgeLength(int i) => (EdgeEnd(i) - EdgeStart(i)).magnitude;
        public static Vector2 EdgeDirection(int i) => (EdgeEnd(i) - EdgeStart(i)).normalized;

        /// <summary>Outward normal of an edge (counter-clockwise polygon: the right-hand side of the travel direction points outward).</summary>
        public static Vector2 EdgeOutward(int i)
        {
            Vector2 d = EdgeDirection(i);
            return new Vector2(d.y, -d.x);
        }

        /// <summary>Start and end distance of a gap along its edge.</summary>
        public static (float from, float to) GapRange(Gap g)
        {
            float len = EdgeLength(g.edge);
            float c = g.center < 0f ? len * 0.5f : g.center;
            return (c - g.width * 0.5f, c + g.width * 0.5f);
        }

        public static Vector3 ToWorld(Vector2 p, float y = 0f) => new Vector3(p.x, y, p.y);
        public static Vector2 ToPlane(Vector3 p) => new Vector2(p.x, p.z);

        /// <summary>Whether a point is inside the polygon (ray casting; also valid for concave polygons).</summary>
        public static bool Contains(Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = Vertices.Length - 1; i < Vertices.Length; j = i++)
            {
                Vector2 a = Vertices[i], b = Vertices[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        /// <summary>Shortest distance from a point to the polygon boundary.</summary>
        public static float DistanceToEdge(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < Vertices.Length; i++) best = Mathf.Min(best, (p - ClosestOnEdge(i, p)).magnitude);
            return best;
        }

        /// <summary>Inside the polygon and at least margin away from every edge (e.g. a character radius).</summary>
        public static bool ContainsWithMargin(Vector3 world, float margin)
        {
            Vector2 p = ToPlane(world);
            return Contains(p) && DistanceToEdge(p) >= margin;
        }

        /// <summary>Pulls a point back inside the polygon, at least margin from the edges: returned unchanged if already valid; otherwise moved to the nearest edge and pushed inward by margin.</summary>
        public static Vector3 ClampInside(Vector3 world, float margin)
        {
            if (ContainsWithMargin(world, margin)) return world;
            Vector2 p = ToPlane(world);
            int bestEdge = 0;
            float best = float.MaxValue;
            for (int i = 0; i < Vertices.Length; i++)
            {
                float d = (p - ClosestOnEdge(i, p)).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                bestEdge = i;
            }
            Vector2 q = ClosestOnEdge(bestEdge, p) - EdgeOutward(bestEdge) * margin;
            return new Vector3(q.x, world.y, q.y);
        }

        /// <summary>A random point inside the polygon, at least margin from the edges (rejection sampling over the bounding rectangle).</summary>
        public static Vector3 RandomInside(float margin)
        {
            Vector2 min = Vertices[0], max = Vertices[0];
            foreach (var v in Vertices)
            {
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }
            for (int i = 0; i < 200; i++)
            {
                var w = new Vector3(Random.Range(min.x, max.x), 0f, Random.Range(min.y, max.y));
                if (ContainsWithMargin(w, margin)) return w;
            }
            return Vector3.zero; // practically never happens: the area around the polygon center is open floor
        }

        static Vector2 ClosestOnEdge(int i, Vector2 p)
        {
            Vector2 a = EdgeStart(i), b = EdgeEnd(i);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return a + ab * t;
        }
    }
}
