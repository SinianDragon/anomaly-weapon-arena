using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnomalyArena.EditorTools
{
    /// <summary>
    /// Builds the irregular platform from ArenaShape: floor (polygon mesh with a skirt), walls along the outside of each edge (mitered at the joints), gaps (black outline + FallZone).
    /// The meshes are saved to Scenes/ArenaGeometry.asset. Menu 5 rebuilds only this part of an existing scene; other objects and values are untouched.
    /// </summary>
    public static class ArenaGeometryBuilder
    {
        const string MeshPath = "Assets/AnomalyArena/Scenes/ArenaGeometry.asset";
        const float WallThick = 1f;
        const float WallHeight = 1.5f;

        const float FloorDepth = 1f;

        // The FallZone starts 1 unit outside the gap line and is 4 units deep, so a large enemy stuck in a small gap is not counted as fallen.
        // Art.DecorateArena relies on 'FallZone center = gap midpoint + 1 + 4 / 2 = 3 units outward'
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

        static Material Mat(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>($"Assets/AnomalyArena/Materials/{name}.mat");

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

        // ───── Floor ─────

        /// <summary>Top face (ear-clipping triangulation, works for concave polygons) + a skirt going down by FloorDepth. UV = world xz, so textures tile per unit.</summary>
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

        /// <summary>Ear-clipping triangulation of a counter-clockwise simple polygon.</summary>
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
                    if (Cross(v[b] - v[a], v[c] - v[b]) <= 0f) continue; // a reflex corner cannot be an ear
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

        // ───── Walls ─────

        /// <summary>Solid segments left on edge e after removing gaps (distance ranges along the edge).</summary>
        static List<(float, float)> SolidRanges(int e)
        {
            var cuts = new List<(float, float)>();
            foreach (var g in ArenaShape.Gaps)
                if (g.edge == e)
                    cuts.Add(ArenaShape.GapRange(g));
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
        /// One wall segment: inner side on the edge, outer side WallThick outward. An end that sits on a vertex is mitered (intersection of the two adjacent edges after offsetting),
        /// so adjacent segments meet cleanly at corners; an end at a gap is cut square.
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

        /// <summary>Miter point on the outer side of the wall at vertex i: intersection of the previous and next edges, each offset outward by WallThick.</summary>
        static Vector2 MiterPoint(int i)
        {
            int prev = (i + ArenaShape.EdgeCount - 1) % ArenaShape.EdgeCount;
            Vector2 n1 = ArenaShape.EdgeOutward(prev), n2 = ArenaShape.EdgeOutward(i);
            Vector2 m = (n1 + n2).normalized;
            float cos = Mathf.Max(0.3f, Vector2.Dot(m, n2)); // keeps very sharp corners from pushing the miter point too far out
            return ArenaShape.Vertices[i] + m * (WallThick / cos);
        }

        /// <summary>Extrudes a convex quad on the plane into a prism of height h (separate vertices per face, flat shading).</summary>
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

        // ───── Gaps ─────

        static void BuildGap(ArenaShape.Gap g, Transform parent, Material edgeMat)
        {
            var (from, to) = ArenaShape.GapRange(g);
            Vector2 a = ArenaShape.EdgeStart(g.edge), d = ArenaShape.EdgeDirection(g.edge);
            Vector3 n = ArenaShape.ToWorld(ArenaShape.EdgeOutward(g.edge));
            Vector3 mid = ArenaShape.ToWorld(a + d * ((from + to) * 0.5f));
            float width = to - from;
            var gt = new GameObject(g.name).transform;
            gt.SetParent(parent, false);

            // Black outline along the gap: a black strip on the edge line that says 'you fall here'
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = "EdgeOutline";
            Object.DestroyImmediate(strip.GetComponent<Collider>());
            strip.transform.SetParent(gt, false);
            strip.transform.position = mid - n * 0.08f + Vector3.up * 0.01f;
            strip.transform.rotation = Quaternion.LookRotation(n);
            strip.transform.localScale = new Vector3(width, 0.02f, 0.16f);
            strip.GetComponent<Renderer>().sharedMaterial = edgeMat;
            strip.isStatic = true;

            // FallZone outside the gap: local z points outward, local x runs along the edge
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

        /// <summary>
        /// Builds a mesh face by face: given the desired facing, the triangle winding is adjusted so the front faces that way.
        /// UVs are in world units, 1 per unit: upward faces use xz (floor, wall tops); vertical faces use 'horizontal distance along the face, height',
        /// otherwise xz barely changes on a vertical face and the texture stretches into vertical stripes.
        /// </summary>
        sealed class MeshBuilder
        {
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<int> tris = new List<int>();

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 facing)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f) (b, c) = (c, b);
                bool horizontal = Mathf.Abs(facing.normalized.y) > 0.5f;
                Vector3 tangent = horizontal ? Vector3.right : Vector3.Cross(Vector3.up, facing).normalized;
                int i = verts.Count;
                foreach (var v in new[] { a, b, c })
                {
                    verts.Add(v);
                    uvs.Add(horizontal ? new Vector2(v.x, v.z) : new Vector2(Vector3.Dot(v, tangent), v.y));
                }

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
                var mesh = new Mesh { vertices = verts.ToArray(), triangles = tris.ToArray(), uv = uvs.ToArray() };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}