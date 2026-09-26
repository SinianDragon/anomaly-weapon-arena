using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnomalyArena.EditorTools
{
    /// <summary>
    /// 按 ArenaShape 生成不规则平台：地板（多边形网格，带一圈侧面）、沿边外侧的墙（接缝处斜切对齐）、缺口（黑色描边 + FallZone）。
    /// 网格存成 Scenes/ArenaGeometry.asset。菜单 5 在现有场景里只重建这一部分，其他对象和数值不动。
    /// </summary>
    public static class ArenaGeometryBuilder
    {
        const string MeshPath = "Assets/AnomalyArena/Scenes/ArenaGeometry.asset";
        const float WallThick = 1f;
        const float WallHeight = 1.5f;
        const float FloorDepth = 1f;
        // FallZone 从缺口边线外 1 格开始、深 4 格：避免卡在小缺口上的大型敌人被误判。
        // Art.DecorateArena 依赖“FallZone 中心 = 缺口中点向外 1 + 4 / 2 = 3 格”
        const float ZoneOffset = 1f, ZoneDepth = 4f;

        [MenuItem("Anomaly Arena/5. Rebuild Arena Geometry (irregular shape)")]
        public static void RebuildInScene()
        {
            var scene = EditorSceneManager.OpenScene(ArenaSetup.ScenePath);
            var old = GameObject.Find("Arena");
            if (old) Object.DestroyImmediate(old);
            Build(Mat("Floor"), Mat("Wall"), Mat("GapEdge"), LayerMask.NameToLayer("Wall"));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[AnomalyArena] Arena rebuilt: {ArenaShape.EdgeCount} edges, {ArenaShape.Gaps.Length} gaps.");
        }

        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"Assets/AnomalyArena/Materials/{name}.mat");

        public static GameObject Build(Material floorMat, Material wallMat, Material gapEdgeMat, int wallLayer)
        {
            AssetDatabase.DeleteAsset(MeshPath);
            var root = new GameObject("Arena");

            var floorMesh = FloorMesh();
            AssetDatabase.CreateAsset(floorMesh, MeshPath);
            var floor = MeshObject("Floor", root.transform, floorMesh, floorMat);
            floor.AddComponent<MeshCollider>().sharedMesh = floorMesh;

            var walls = new GameObject("Walls").transform;
            walls.SetParent(root.transform, false);
            for (int e = 0; e < ArenaShape.EdgeCount; e++)
            {
                foreach (var (from, to) in SolidRanges(e))
                {
                    var mesh = WallMesh(e, from, to);
                    mesh.name = $"Wall_{e}_{from:0.#}_{to:0.#}";
                    AssetDatabase.AddObjectToAsset(mesh, MeshPath);
                    var go = MeshObject(mesh.name, walls, mesh, wallMat);
                    go.layer = wallLayer;
                    var col = go.AddComponent<MeshCollider>();
                    col.sharedMesh = mesh;
                    col.convex = true;
                }
            }

            var gaps = new GameObject("Gaps").transform;
            gaps.SetParent(root.transform, false);
            foreach (var g in ArenaShape.Gaps) BuildGap(g, gaps, gapEdgeMat);

            AssetDatabase.SaveAssets();
            return root;
        }

        // ───── 地板 ─────

        /// <summary>顶面（耳切法三角化，凹多边形也可以）+ 一圈向下 FloorDepth 的侧面。UV = 世界 xz，贴图按格平铺。</summary>
        static Mesh FloorMesh()
        {
            var v = ArenaShape.Vertices;
            var b = new MeshBuilder();
            foreach (var (i, j, k) in Triangulate(v))
                b.Tri(ArenaShape.ToWorld(v[i]), ArenaShape.ToWorld(v[j]), ArenaShape.ToWorld(v[k]), Vector3.up);
            for (int e = 0; e < v.Length; e++)
            {
                Vector3 a = ArenaShape.ToWorld(ArenaShape.EdgeStart(e)), c = ArenaShape.ToWorld(ArenaShape.EdgeEnd(e));
                Vector3 down = Vector3.down * FloorDepth;
                b.Quad(a, c, c + down, a + down, ArenaShape.ToWorld(ArenaShape.EdgeOutward(e)));
            }
            var mesh = b.ToMesh();
            mesh.name = "ArenaFloor";
            return mesh;
        }

        /// <summary>逆时针简单多边形的耳切法三角化。</summary>
        static List<(int, int, int)> Triangulate(Vector2[] v)
        {
            var idx = new List<int>();
            for (int i = 0; i < v.Length; i++) idx.Add(i);
            var tris = new List<(int, int, int)>();
            int guard = 0;
            while (idx.Count > 3 && guard++ < 1000)
            {
                for (int n = 0; n < idx.Count; n++)
                {
                    int a = idx[(n + idx.Count - 1) % idx.Count], b = idx[n], c = idx[(n + 1) % idx.Count];
                    if (Cross(v[b] - v[a], v[c] - v[b]) <= 0f) continue; // 凹角不能当耳朵
                    bool contains = false;
                    foreach (int o in idx)
                    {
                        if (o == a || o == b || o == c) continue;
                        if (!InTriangle(v[o], v[a], v[b], v[c])) continue;
                        contains = true;
                        break;
                    }
                    if (contains) continue;
                    tris.Add((a, b, c));
                    idx.RemoveAt(n);
                    break;
                }
            }
            tris.Add((idx[0], idx[1], idx[2]));
            return tris;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c) =>
            Cross(b - a, p - a) >= 0f && Cross(c - b, p - b) >= 0f && Cross(a - c, p - c) >= 0f;

        // ───── 墙 ─────

        /// <summary>第 e 条边上去掉缺口后剩下的实心段（边上的距离区间）。</summary>
        static List<(float, float)> SolidRanges(int e)
        {
            var cuts = new List<(float, float)>();
            foreach (var g in ArenaShape.Gaps)
                if (g.edge == e) cuts.Add(ArenaShape.GapRange(g));
            cuts.Sort((x, y) => x.Item1.CompareTo(y.Item1));
            var solid = new List<(float, float)>();
            float cur = 0f, len = ArenaShape.EdgeLength(e);
            foreach (var (from, to) in cuts)
            {
                if (from > cur + 0.01f) solid.Add((cur, from));
                cur = to;
            }
            if (len > cur + 0.01f) solid.Add((cur, len));
            return solid;
        }

        /// <summary>
        /// 一段墙：内侧贴着边，外侧向外 WallThick。落在顶点上的一端用斜切（两条相邻边外移后的交点），
        /// 相邻两段墙在拐角处严丝合缝；缺口那一端直接垂直切断。
        /// </summary>
        static Mesh WallMesh(int e, float from, float to)
        {
            Vector2 a = ArenaShape.EdgeStart(e), d = ArenaShape.EdgeDirection(e), n = ArenaShape.EdgeOutward(e);
            float len = ArenaShape.EdgeLength(e);
            Vector2 innerA = a + d * from, innerB = a + d * to;
            Vector2 outerA = from < 0.01f ? MiterPoint(e) : innerA + n * WallThick;
            Vector2 outerB = to > len - 0.01f ? MiterPoint((e + 1) % ArenaShape.EdgeCount) : innerB + n * WallThick;
            return Prism(new[] { innerA, innerB, outerB, outerA }, WallHeight);
        }

        /// <summary>顶点 i 处墙外侧的斜切点：前一条边和后一条边各自外移 WallThick 后的交点。</summary>
        static Vector2 MiterPoint(int i)
        {
            int prev = (i + ArenaShape.EdgeCount - 1) % ArenaShape.EdgeCount;
            Vector2 n1 = ArenaShape.EdgeOutward(prev), n2 = ArenaShape.EdgeOutward(i);
            Vector2 m = (n1 + n2).normalized;
            float cos = Mathf.Max(0.3f, Vector2.Dot(m, n2)); // 防止极尖的角把斜切点推得太远
            return ArenaShape.Vertices[i] + m * (WallThick / cos);
        }

        /// <summary>把平面上的凸四边形拉成高 h 的柱体（每个面单独顶点，平直着色）。</summary>
        static Mesh Prism(Vector2[] quad, float h)
        {
            var b = new MeshBuilder();
            Vector3 center = Vector3.zero;
            foreach (var p in quad) center += ArenaShape.ToWorld(p, h * 0.5f);
            center /= quad.Length;
            var lo = new Vector3[quad.Length];
            var hi = new Vector3[quad.Length];
            for (int i = 0; i < quad.Length; i++)
            {
                lo[i] = ArenaShape.ToWorld(quad[i]);
                hi[i] = ArenaShape.ToWorld(quad[i], h);
            }
            b.Quad(hi[0], hi[1], hi[2], hi[3], Vector3.up);
            for (int i = 0; i < quad.Length; i++)
            {
                int j = (i + 1) % quad.Length;
                Vector3 faceCenter = (lo[i] + lo[j] + hi[i] + hi[j]) * 0.25f;
                b.Quad(lo[i], lo[j], hi[j], hi[i], faceCenter - center);
            }
            return b.ToMesh();
        }

        // ───── 缺口 ─────

        static void BuildGap(ArenaShape.Gap g, Transform parent, Material edgeMat)
        {
            var (from, to) = ArenaShape.GapRange(g);
            Vector2 a = ArenaShape.EdgeStart(g.edge), d = ArenaShape.EdgeDirection(g.edge);
            Vector3 n = ArenaShape.ToWorld(ArenaShape.EdgeOutward(g.edge));
            Vector3 mid = ArenaShape.ToWorld(a + d * ((from + to) * 0.5f));
            float width = to - from;
            var gt = new GameObject(g.name).transform;
            gt.SetParent(parent, false);

            // 缺口边缘描黑：一条贴着边线的黑条，提示“这里会掉下去”
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = "EdgeOutline";
            Object.DestroyImmediate(strip.GetComponent<Collider>());
            strip.transform.SetParent(gt, false);
            strip.transform.position = mid - n * 0.08f + Vector3.up * 0.01f;
            strip.transform.rotation = Quaternion.LookRotation(n);
            strip.transform.localScale = new Vector3(width, 0.02f, 0.16f);
            strip.GetComponent<Renderer>().sharedMaterial = edgeMat;
            strip.isStatic = true;

            // 缺口外的 FallZone：本地 z 朝外、本地 x 沿着边
            var zone = new GameObject("FallZone");
            zone.transform.SetParent(gt, false);
            zone.transform.position = mid + n * (ZoneOffset + ZoneDepth * 0.5f) + Vector3.down * 4f;
            zone.transform.rotation = Quaternion.LookRotation(n);
            var bc = zone.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(width + 1f, 12f, ZoneDepth);
            zone.AddComponent<FallZone>().outward = n;
        }

        static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        /// <summary>逐面拼网格：给出期望朝向，自动调整三角形顺序让正面朝那边。UV 用世界 xz（地板贴图按格平铺）。</summary>
        sealed class MeshBuilder
        {
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<int> tris = new List<int>();

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 facing)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f) (b, c) = (c, b);
                int i = verts.Count;
                verts.Add(a);
                verts.Add(b);
                verts.Add(c);
                tris.Add(i);
                tris.Add(i + 1);
                tris.Add(i + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing)
            {
                Tri(a, b, c, facing);
                Tri(a, c, d, facing);
            }

            public Mesh ToMesh()
            {
                var uv = new Vector2[verts.Count];
                for (int i = 0; i < verts.Count; i++) uv[i] = new Vector2(verts[i].x, verts[i].z);
                var mesh = new Mesh { vertices = verts.ToArray(), triangles = tris.ToArray(), uv = uv };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
