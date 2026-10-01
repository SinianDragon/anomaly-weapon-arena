using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalyArena
{
    /// <summary>Minimal whitebox feedback: a translucent flash that disappears right away (not a proper effect).</summary>
    public static class Fx
    {
        class Fade : MonoBehaviour
        {
            public float life;
            public Action<Transform, float> update;
            public Material mat;
            public Mesh mesh;
            public Color color;
            float t;

            void Update()
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / life);
                update?.Invoke(transform, k);
                var c = color;
                c.a *= 1f - k;
                mat.color = c;
                if (t >= life) Destroy(gameObject);
            }

            void OnDestroy()
            {
                if (mat) Destroy(mat);
                if (mesh) Destroy(mesh);
            }
        }

        static Fade Make(GameObject go, Color c, float life, Texture tex = null)
        {
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            var m = GameManager.Instance.FxMat(c);
            if (tex) m.SetTexture("_BaseMap", tex);
            r.sharedMaterial = m;
            r.sortingOrder = Art.OrderProjectile;
            var f = go.AddComponent<Fade>();
            f.life = life;
            f.mat = m;
            f.color = c;
            return f;
        }

        // ───── Illustrated mode: one-shot textured effects ─────

        /// <summary>Camera-facing texture (hit spark, dust) that grows from startSize to endSize and fades out. size is the height.</summary>
        public static void SpriteBillboard(Texture tex, Vector3 pos, float startSize, float endSize, float life)
        {
            var go = Prim(PrimitiveType.Quad, pos, Vector3.one * startSize);
            var cam = GameManager.Instance.cam;
            if (cam) go.transform.rotation = cam.transform.rotation;
            float aspect = Art.Aspect(tex);
            var f = Make(go, Color.white, life, tex);
            f.update = (t, k) =>
            {
                float s = Mathf.Lerp(startSize, endSize, Mathf.Sqrt(k));
                t.localScale = new Vector3(s * aspect, s, 1f);
            };
        }

        /// <summary>Texture lying flat on the ground with its right side pointing along forward (punch arc); its length grows from length to length × grow and fades out.</summary>
        public static void SpriteFlat(Texture tex, Vector3 pos, Vector3 forward, float length, float life,
            float grow = 1.2f)
        {
            var go = Prim(PrimitiveType.Quad, pos, Vector3.one);
            // Same as Art.Flat: lie flat first, then point the right side of the texture forward
            go.transform.rotation = Quaternion.LookRotation(Query.Flat(forward)) *
                                    Quaternion.Euler(0f, -90f, 0f) * Quaternion.Euler(90f, 0f, 0f);
            float aspect = Art.Aspect(tex);
            var f = Make(go, Color.white, life, tex);
            f.update = (t, k) =>
            {
                float l = length * Mathf.Lerp(1f, grow, Mathf.Sqrt(k));
                t.localScale = new Vector3(l, l / aspect, 1f);
            };
        }

        /// <summary>Round texture on the ground (explosion range ring); its diameter expands from 0 to 2 × radius and fades out.</summary>
        public static void SpriteRing(Texture tex, Vector3 pos, float radius, float life)
        {
            var go = Prim(PrimitiveType.Quad, new Vector3(pos.x, 0.06f, pos.z), Vector3.one * 0.1f);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var f = Make(go, Color.white, life, tex);
            f.update = (t, k) =>
            {
                float d = Mathf.Lerp(0.2f, radius * 2f, Mathf.Sqrt(Mathf.Min(1f, k * 1.6f)));
                t.localScale = new Vector3(d, d, 1f);
            };
        }

        /// <summary>Camera-facing frame animation played in order (explosion), life seconds in total, fading on the last frame.</summary>
        public static void Flipbook(System.Collections.Generic.IReadOnlyList<Texture> frames, Vector3 pos, float size,
            float life)
        {
            if (frames == null || frames.Count == 0) return;
            var go = Prim(PrimitiveType.Quad, pos, Vector3.one * size);
            var cam = GameManager.Instance.cam;
            if (cam) go.transform.rotation = cam.transform.rotation;
            var f = Make(go, Color.white, life, frames[0]);
            var mat = f.mat;
            f.update = (t, k) =>
            {
                var tex = frames[Mathf.Min(frames.Count - 1, (int)(k * frames.Count))];
                mat.SetTexture("_BaseMap", tex);
                float s = size * Mathf.Lerp(0.7f, 1.15f, k);
                t.localScale = new Vector3(s * Art.Aspect(tex), s, 1f);
            };
        }

        static GameObject Prim(PrimitiveType t, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(t);
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = pos;
            go.transform.localScale = scale;
            return go;
        }

        public static void Pop(Vector3 pos, Color c, float size)
        {
            c.a = 0.6f;
            var f = Make(Prim(PrimitiveType.Sphere, pos, Vector3.one * 0.2f), c, 0.25f);
            f.update = (t, k) => t.localScale = Vector3.one * Mathf.Lerp(0.2f, size, Mathf.Sqrt(k));
        }

        /// <summary>
        /// Hit shards: count small cubes fly from pos within spreadDeg degrees either side of dir, fast then slow, shrinking and fading as they go.
        /// When dir is zero they fly in all directions.
        /// </summary>
        public static void Burst(Vector3 pos, Vector3 dir, Color c, int count, float speed, float spreadDeg, float size)
        {
            dir = Query.Flat(dir);
            bool omni = dir.sqrMagnitude < 1e-4f;
            if (!omni) dir.Normalize();
            for (int i = 0; i < count; i++)
            {
                Vector3 d = omni
                    ? Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), Vector3.up) * Vector3.forward
                    : Quaternion.AngleAxis(UnityEngine.Random.Range(-spreadDeg, spreadDeg) * 0.5f, Vector3.up) * dir;
                d.y = UnityEngine.Random.Range(0.1f, 0.6f); // slightly upward so the splash reads from the top-down camera
                Vector3 v = d.normalized * speed * UnityEngine.Random.Range(0.6f, 1.2f);
                float s = size * UnityEngine.Random.Range(0.6f, 1.3f);
                float life = UnityEngine.Random.Range(0.22f, 0.38f);
                var go = Prim(PrimitiveType.Cube, pos, Vector3.one * s);
                go.transform.rotation = UnityEngine.Random.rotation;
                var f = Make(go, c, life);
                f.update = (t, k) =>
                {
                    float travel = 1f - (1f - k) * (1f - k); // fast then slow
                    t.position = pos + v * (life * travel);
                    t.localScale = Vector3.one * (s * (1f - k * 0.7f));
                };
            }
        }

        /// <summary>Ground shock ring: expands quickly from the center to radius, then disappears.</summary>
        public static void Ring(Vector3 pos, float radius, Color c, float life = 0.25f)
        {
            var go = Prim(PrimitiveType.Cylinder, new Vector3(pos.x, 0.08f, pos.z), new Vector3(0.3f, 0.01f, 0.3f));
            var f = Make(go, c, life);
            f.update = (t, k) =>
            {
                float d = Mathf.Lerp(0.3f, radius * 2f, Mathf.Sqrt(k));
                t.localScale = new Vector3(d, 0.01f, d);
            };
        }

        public static void Explosion(Vector3 pos, float radius)
        {
            var f = Make(Prim(PrimitiveType.Sphere, pos, Vector3.one), new Color(1f, 0.55f, 0.15f, 0.6f), 0.4f);
            f.update = (t, k) =>
            {
                float s = Mathf.Lerp(0.5f, radius * 2f, Mathf.Sqrt(k));
                t.localScale = new Vector3(s, s * 0.5f, s);
            };
        }

        /// <summary>Ground sector showing the swing range.</summary>
        public static void Sector(Vector3 origin, Vector3 dir, float radius, float arcDeg, Color c)
        {
            var go = new GameObject("SwingArc");
            go.transform.position = new Vector3(origin.x, 0.05f, origin.z);
            go.transform.rotation = Quaternion.LookRotation(Query.Flat(dir));
            var mesh = new Mesh();
            SectorMesh(mesh, radius, arcDeg);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
            Make(go, c, 0.15f).mesh = mesh;
        }

        /// <summary>Rebuilds mesh as a sector facing +Z (origin at the center). Called every frame by the charge preview.</summary>
        public static void SectorMesh(Mesh mesh, float radius, float arcDeg)
        {
            int seg = Mathf.Max(4, Mathf.CeilToInt(arcDeg / 8f));
            var verts = new Vector3[seg + 2];
            var tris = new int[seg * 3];
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Deg2Rad * Mathf.Lerp(-arcDeg * 0.5f, arcDeg * 0.5f, i / (float)seg);
                verts[i + 1] = new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
            }

            for (int i = 0; i < seg; i++)
            {
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }

            mesh.Clear();
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
        }
    }
}