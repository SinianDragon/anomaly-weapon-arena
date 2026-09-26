using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// 平台的形状：一个不规则多边形（逆时针，俯视 x 向右、z 向上），墙沿着边建在外侧，缺口是边上不建墙的一段。
    /// 编辑器生成场景（ArenaSetup）和游戏逻辑（出生点、武器位置、敌人不走下边缘、钩子限位）共用这一份数据。
    /// </summary>
    public static class ArenaShape
    {
        /// <summary>多边形顶点。有斜切的角，也有向内凹的边（V1、V11），外接范围和原来的 30 × 30 差不多，镜头不用动。</summary>
        public static readonly Vector2[] Vertices =
        {
            new Vector2(-11f, -15f),   // V0
            new Vector2(-3f, -13.5f),  // V1 底边向内凹
            new Vector2(9f, -15f),     // V2
            new Vector2(14f, -11f),    // V3
            new Vector2(16f, -4f),     // V4
            new Vector2(15f, 7f),      // V5
            new Vector2(12f, 13.5f),   // V6
            new Vector2(3f, 15f),      // V7
            new Vector2(-8f, 14f),     // V8
            new Vector2(-13.5f, 10.5f), // V9
            new Vector2(-15.5f, 2f),   // V10
            new Vector2(-14f, -6f),    // V11 左边向内凹
            new Vector2(-16f, -11f),   // V12
        };

        public struct Gap
        {
            public string name;
            /// <summary>在第几条边上（边 i 从顶点 i 到顶点 i+1）。</summary>
            public int edge;
            /// <summary>缺口中点离边起点的距离，负数表示边的正中间。</summary>
            public float center;
            public float width;
        }

        // 小缺口：比玩家和小型敌人（直径 1）宽、比大型敌人（直径 2）窄，大型会卡住
        public const float SmallGapWidth = 1.6f;
        // 大缺口：所有人都会掉下去
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

        /// <summary>边的外法线（逆时针多边形：沿边前进方向的右手边朝外）。</summary>
        public static Vector2 EdgeOutward(int i)
        {
            Vector2 d = EdgeDirection(i);
            return new Vector2(d.y, -d.x);
        }

        /// <summary>缺口在它那条边上的起止距离。</summary>
        public static (float from, float to) GapRange(Gap g)
        {
            float len = EdgeLength(g.edge);
            float c = g.center < 0f ? len * 0.5f : g.center;
            return (c - g.width * 0.5f, c + g.width * 0.5f);
        }

        public static Vector3 ToWorld(Vector2 p, float y = 0f) => new Vector3(p.x, y, p.y);
        public static Vector2 ToPlane(Vector3 p) => new Vector2(p.x, p.z);

        /// <summary>点在多边形内（射线法，凹多边形也成立）。</summary>
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

        /// <summary>点到多边形边界的最短距离。</summary>
        public static float DistanceToEdge(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < Vertices.Length; i++) best = Mathf.Min(best, (p - ClosestOnEdge(i, p)).magnitude);
            return best;
        }

        /// <summary>在多边形内，并且离每条边都至少 margin（例如角色半径）。</summary>
        public static bool ContainsWithMargin(Vector3 world, float margin)
        {
            Vector2 p = ToPlane(world);
            return Contains(p) && DistanceToEdge(p) >= margin;
        }

        /// <summary>把点拉回多边形内、离边至少 margin：已经满足就原样返回；否则移到最近的边上再往里推 margin。</summary>
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

        /// <summary>多边形内随机一点，离边至少 margin（用外接矩形拒绝采样）。</summary>
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
            return Vector3.zero; // 几乎不会发生：多边形中心附近是空地
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
