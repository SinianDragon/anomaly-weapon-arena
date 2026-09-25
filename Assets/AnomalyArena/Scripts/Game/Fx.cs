using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalyArena
{
    /// <summary>白盒阶段最低限度的反馈：半透明闪一下就消失（不算正式特效）。</summary>
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

        static Fade Make(GameObject go, Color c, float life)
        {
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            var m = GameManager.Instance.FxMat(c);
            r.sharedMaterial = m;
            var f = go.AddComponent<Fade>();
            f.life = life;
            f.mat = m;
            f.color = c;
            return f;
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

        public static void Explosion(Vector3 pos, float radius)
        {
            var f = Make(Prim(PrimitiveType.Sphere, pos, Vector3.one), new Color(1f, 0.55f, 0.15f, 0.6f), 0.4f);
            f.update = (t, k) =>
            {
                float s = Mathf.Lerp(0.5f, radius * 2f, Mathf.Sqrt(k));
                t.localScale = new Vector3(s, s * 0.5f, s);
            };
        }

        /// <summary>地面扇形，显示挥砍范围。</summary>
        public static void Sector(Vector3 origin, Vector3 dir, float radius, float arcDeg, Color c)
        {
            var go = new GameObject("SwingArc");
            go.transform.position = new Vector3(origin.x, 0.05f, origin.z);
            go.transform.rotation = Quaternion.LookRotation(Query.Flat(dir));
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
            var mesh = new Mesh { vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
            Make(go, c, 0.15f).mesh = mesh;
        }
    }
}
