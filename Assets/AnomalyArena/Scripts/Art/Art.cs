using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// 白盒 / 美术版切换。开关只能在标题画面按（切换时重新加载场景），所以每个物体只需要在生成时看一眼开关，
    /// 开着就把白盒方块藏起来、换成贴图面片。静态字段在重新加载场景后仍然保留，按 R 重开也不会丢。
    /// </summary>
    public static class Art
    {
        public static bool Enabled;

        public static ArtSet Set => GameManager.Instance ? GameManager.Instance.art : null;
        public static bool On => Enabled && Set != null;

        // 排序：地面装饰 < 角色 < 手里的武器 < 飞行物
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
            // 必须立即删：挂到角色（动态刚体）下面时，延迟到帧末才删的 MeshCollider 会报错并参与这一帧的碰撞
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

        /// <summary>始终正对镜头的立牌，底边在 parent 的原点（角色脚下）。返回可以整体缩放的支点。</summary>
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
        /// 在已有立牌上再叠一层（蓄力发光、保护泡泡），和底图同心。layerTex 的像素密度和底图相同时（四周留同样的边距），
        /// 按“层贴图高度 / 底图高度”放大后身体正好和底图重合。
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

        /// <summary>平躺在地上、居中的方形贴图（出生点 X、范围圈）。size 是边长。</summary>
        public static Renderer Ground(Transform parent, Texture tex, float size, int order)
        {
            var r = Quad(parent, tex, order);
            r.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            r.transform.localScale = new Vector3(size * Aspect(tex), size, 1f);
            return r;
        }

        /// <summary>沿贴图的 U 方向重复 tiles 次（贴图导入方式要是 Repeat）。</summary>
        public static void TileU(Renderer r, float tiles)
        {
            if (!r) return;
            mpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetVector("_BaseMap_ST", new Vector4(Mathf.Max(0.01f, tiles), 1f, 0f, 0f));
            r.SetPropertyBlock(mpb);
        }

        /// <summary>平躺在地面上、贴图的右边指向 parent 的前方（武器、飞行物）。length 是沿前方的长度。</summary>
        public static Transform Flat(Transform parent, Texture tex, float length, int order, out Renderer sprite,
            Vector3 localPos = default)
        {
            var holder = new GameObject("ArtFlat").transform;
            holder.SetParent(parent, false);
            holder.localPosition = localPos;
            sprite = Quad(holder, tex, order);
            // 先绕 X 转 90° 躺平（正面朝上），再绕 Y 转 -90° 让贴图的右边指向前方
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

        /// <summary>地板换成沙地贴图，缺口外面铺一排“沙地边缘 + 黑坑”。</summary>
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
                // 地板网格的 UV 就是世界坐标 xz（ArenaSetup 生成），每 floorTileSize 格铺一块贴图
                float tile = 1f / Mathf.Max(0.5f, s.floorTileSize);
                m.SetTextureScale("_BaseMap", new Vector2(tile, tile));
                r.sharedMaterial = m;
            }

            var walls = GameObject.Find("Arena/Walls");
            if (walls && s.wallBrick)
            {
                // 墙网格的 UV 也是世界坐标 xz（ArenaGeometryBuilder）：俯视镜头主要看到墙顶，砖按格平铺
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
                // ArenaSetup：FallZone 的本地 z 朝外、本地 x 沿着边；中心在缺口中点向外 3 格处，沿边方向比缺口宽 1 格
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
                    // 贴图上方（沙地）朝向场内，下方（黑坑）朝外
                    tile.rotation = Quaternion.LookRotation(Vector3.down, -outward);
                    tile.localScale = new Vector3(w, depth, 1f);
                }
            }
        }
    }

    /// <summary>立牌每帧转向镜头。</summary>
    public class FaceCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            var gm = GameManager.Instance;
            if (gm && gm.cam) transform.rotation = gm.cam.transform.rotation;
        }
    }

    /// <summary>
    /// 钩子伸出时的链刀：刀柄 + 重复平铺的链条 + 刀尖（一把刀）。链条按链节重复，伸多长链节都不变形。
    /// 刀尖比链条粗 tipScale 倍；总长短于刀柄加刀尖时整体缩小。
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