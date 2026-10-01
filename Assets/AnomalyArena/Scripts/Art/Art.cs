using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Whitebox / illustrated toggle. It can only be switched on the title screen (switching reloads the scene), so each object only has to check the flag when it is created:
    /// if on, hide the whitebox primitive and show a textured quad instead. Static fields survive scene reloads, so restarting with R keeps the choice.
    /// </summary>
    public static class Art
    {
        public static bool Enabled;

        public static ArtSet Set => GameManager.Instance ? GameManager.Instance.art : null;
        public static bool On => Enabled && Set != null;

        // Sorting: ground decoration < characters < held weapon < projectiles
        public const int OrderGround = 0, OrderCharacter = 1, OrderHeld = 2, OrderProjectile = 3;

        static readonly Dictionary<Texture, Material> mats = new Dictionary<Texture, Material>();
        static MaterialPropertyBlock mpb;

        public static float Aspect(Texture t) => t ? (float)t.width / t.height : 1f;

        static Material Mat(Texture tex)
        {
            if (mats.TryGetValue(tex, out var m) && m) return m;
            m = new Material(GameManager.Instance.fxMaterial);
            m.SetTexture("_BaseMap", tex);
            m.color = Color.white;
            mats[tex] = m;
            return m;
        }

        static Renderer Quad(Transform parent, Texture tex, int order)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            // Must be destroyed immediately: under a character (dynamic rigidbody), a MeshCollider destroyed at end of frame throws errors and takes part in this frame's collisions
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = tex.name;
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Mat(tex);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingOrder = order;
            return r;
        }

        /// <summary>A billboard that always faces the camera, with its bottom edge at the parent's origin (the character's feet). Returns a pivot that can be scaled as a whole.</summary>
        public static Transform Billboard(Transform parent, Texture tex, float height, int order, out Renderer sprite)
        {
            var pivot = new GameObject("ArtBillboard").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = new Vector3(0f, 0.05f, 0f);
            pivot.gameObject.AddComponent<FaceCamera>();
            sprite = Quad(pivot, tex, order);
            sprite.transform.localScale = new Vector3(height * Aspect(tex), height, 1f);
            sprite.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            return pivot;
        }

        /// <summary>
        /// Adds another layer on top of an existing billboard (wind-up glow, protection bubble), concentric with the base image. When layerTex has the same pixel density as the base (same margin on every side),
        /// scaling by 'layer texture height / base texture height' makes the body line up exactly with the base.
        /// </summary>
        public static Renderer BillboardLayer(Transform pivot, Texture baseTex, float baseHeight, Texture layerTex,
            int order)
        {
            var r = Quad(pivot, layerTex, order);
            float h = baseHeight * layerTex.height / baseTex.height;
            r.transform.localScale = new Vector3(h * Aspect(layerTex), h, 1f);
            r.transform.localPosition = new Vector3(0f, baseHeight * 0.5f, -0.01f);
            return r;
        }

        /// <summary>A centered square texture lying flat on the ground (spawn X, range ring). size is the side length.</summary>
        public static Renderer Ground(Transform parent, Texture tex, float size, int order)
        {
            var r = Quad(parent, tex, order);
            r.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            r.transform.localScale = new Vector3(size * Aspect(tex), size, 1f);
            return r;
        }

        /// <summary>Repeats the texture tiles times along U (the texture must be imported as Repeat).</summary>
        public static void TileU(Renderer r, float tiles)
        {
            if (!r) return;
            mpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetVector("_BaseMap_ST", new Vector4(Mathf.Max(0.01f, tiles), 1f, 0f, 0f));
            r.SetPropertyBlock(mpb);
        }

        /// <summary>Lies flat on the ground with the right side of the texture pointing along the parent's forward (weapons, projectiles). length is the length along forward.</summary>
        public static Transform Flat(Transform parent, Texture tex, float length, int order, out Renderer sprite,
            Vector3 localPos = default)
        {
            var holder = new GameObject("ArtFlat").transform;
            holder.SetParent(parent, false);
            holder.localPosition = localPos;
            sprite = Quad(holder, tex, order);
            // Rotate 90° around X to lie flat (face up), then -90° around Y so the right side of the texture points forward
            sprite.transform.localRotation = Quaternion.Euler(0f, -90f, 0f) * Quaternion.Euler(90f, 0f, 0f);
            sprite.transform.localScale = new Vector3(length, length / Aspect(tex), 1f);
            return holder;
        }

        public static void Tint(Renderer r, Color c)
        {
            if (!r) return;
            mpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            r.SetPropertyBlock(mpb);
        }

        public static void HideWhitebox(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
        }

        /// <summary>Replaces the floor with the sand texture and lays a row of 'sand edge + black pit' outside each gap.</summary>
        public static void DecorateArena()
        {
            var s = Set;
            var floor = GameObject.Find("Arena/Floor");
            if (floor && s.floor)
            {
                var r = floor.GetComponent<Renderer>();
                var m = new Material(GameManager.Instance.litMaterial);
                m.SetTexture("_BaseMap", s.floor);
                m.color = Color.white;
                // The floor mesh UV is world xz (built by ArenaSetup); one texture tile per floorTileSize units
                float tile = 1f / Mathf.Max(0.5f, s.floorTileSize);
                m.SetTextureScale("_BaseMap", new Vector2(tile, tile));
                r.sharedMaterial = m;
            }

            var walls = GameObject.Find("Arena/Walls");
            if (walls && s.wallBrick)
            {
                // The wall mesh UV is also world xz (ArenaGeometryBuilder): the top-down camera mostly sees wall tops, bricks tile per unit
                var m = new Material(GameManager.Instance.litMaterial);
                m.SetTexture("_BaseMap", s.wallBrick);
                m.color = Color.white;
                float tile = 1f / Mathf.Max(0.25f, s.wallTileSize);
                m.SetTextureScale("_BaseMap", new Vector2(tile, tile));
                foreach (var r in walls.GetComponentsInChildren<Renderer>()) r.sharedMaterial = m;
            }

            if (!s.gapEdge) return;

            const float depth = 1.4f;
            foreach (var z in Object.FindObjectsByType<FallZone>())
            {
                var box = z.GetComponent<BoxCollider>();
                Vector3 outward = z.outward;
                // ArenaSetup: FallZone local z points outward, local x runs along the edge; its center is 3 units outside the gap midpoint and it is 1 unit wider than the gap along the edge
                Vector3 along = z.transform.right;
                float width = box.size.x - 1f;
                Vector3 edge = z.transform.position - outward * 3f;
                edge.y = 0.01f;
                int n = Mathf.Max(1, Mathf.CeilToInt(width / depth));
                float w = width / n;
                for (int i = 0; i < n; i++)
                {
                    var tile = Quad(z.transform.parent, s.gapEdge, OrderGround).transform;
                    tile.position = edge + along * (-width * 0.5f + w * (i + 0.5f)) + outward * (depth * 0.5f);
                    // Top of the texture (sand) faces the arena, bottom (black pit) faces outward
                    tile.rotation = Quaternion.LookRotation(Vector3.down, -outward);
                    tile.localScale = new Vector3(w, depth, 1f);
                }
            }
        }
    }

    /// <summary>Turns a billboard toward the camera every frame.</summary>
    public class FaceCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            var gm = GameManager.Instance;
            if (gm && gm.cam) transform.rotation = gm.cam.transform.rotation;
        }
    }

    /// <summary>
    /// Chain blade shown while the hook extends: hilt + tiled chain + tip (a knife). The chain repeats per link, so links never stretch however far it extends.
    /// The tip is tipScale times thicker than the chain; when the total length is shorter than hilt plus tip, everything is scaled down.
    /// </summary>
    public class StretchBlade
    {
        readonly Transform root, hilt, mid, tip;
        readonly Renderer midSprite;
        readonly float thickness, tipScale, hiltLen, tipLen, linkLen;

        public StretchBlade(ArtSet s, float thickness, float tipScale = 1.4f)
        {
            this.thickness = thickness;
            this.tipScale = tipScale;
            root = new GameObject("HookArt").transform;
            hilt = Art.Flat(root, s.hookHilt, 1f, Art.OrderProjectile, out _);
            mid = Art.Flat(root, s.hookMid, 1f, Art.OrderProjectile, out midSprite);
            tip = Art.Flat(root, s.hookTip, 1f, Art.OrderProjectile, out _);
            hiltLen = thickness * Art.Aspect(s.hookHilt);
            tipLen = thickness * tipScale * Art.Aspect(s.hookTip);
            linkLen = thickness * Art.Aspect(s.hookMid);
        }

        public void Set(Vector3 origin, Vector3 dir, float length)
        {
            root.position = origin;
            root.rotation = Quaternion.LookRotation(dir);
            float k = Mathf.Min(1f, length / (hiltLen + tipLen));
            float h = hiltLen * k, t = tipLen * k, m = Mathf.Max(0f, length - h - t), th = thickness * k;
            Place(hilt, 0f, h, th);
            Place(mid, h, m, th);
            Place(tip, h + m, t, th * tipScale);
            Art.TileU(midSprite, m / Mathf.Max(0.001f, linkLen * k));
        }

        static void Place(Transform part, float start, float len, float th)
        {
            part.localPosition = new Vector3(0f, 0f, start + len * 0.5f);
            part.GetChild(0).localScale = new Vector3(Mathf.Max(0.001f, len), th, 1f);
        }

        public void Destroy()
        {
            if (root) Object.Destroy(root.gameObject);
        }
    }
}