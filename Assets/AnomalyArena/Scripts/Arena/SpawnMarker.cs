using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Enemy spawn warning: a red X on the ground; the enemy spawns here 1 second later.</summary>
    public class SpawnMarker : MonoBehaviour
    {
        public bool large;
        public float timer;
        public float size;

        public static SpawnMarker Create(Vector3 pos, bool large, float delay, Material mat)
        {
            var go = new GameObject(large ? "SpawnX_Large" : "SpawnX_Small");
            var m = go.AddComponent<SpawnMarker>();
            m.large = large;
            m.timer = delay;
            m.size = large ? 2.4f : 1.2f; // the large enemy's marker is twice the small one
            var xTex = Art.On ? (large ? Art.Set.spawnXLarge : Art.Set.spawnXSmall) : null;
            if (xTex)
            {
                // Illustrated mode: a red X texture on the ground (same size as the whitebox X made of two crossed boxes)
                Art.Ground(go.transform, xTex, m.size * 1.2f, Art.OrderGround);
                m.MoveTo(pos);
                return m;
            }
            for (int i = 0; i < 2; i++)
            {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(bar.GetComponent<Collider>());
                bar.transform.SetParent(go.transform, false);
                bar.transform.localRotation = Quaternion.Euler(0f, i == 0 ? 45f : -45f, 0f);
                bar.transform.localScale = new Vector3(0.18f, 0.02f, m.size * 1.2f);
                var r = bar.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            m.MoveTo(pos);
            return m;
        }

        public Vector3 Position => transform.position;

        public void MoveTo(Vector3 p) => transform.position = new Vector3(p.x, 0.02f, p.z);

        void Update()
        {
            float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 18f);
            transform.localScale = new Vector3(pulse, 1f, pulse);
        }
    }
}
