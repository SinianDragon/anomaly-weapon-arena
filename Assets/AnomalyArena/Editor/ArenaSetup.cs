using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalyArena.EditorTools
{
    /// <summary>
    /// 一键生成白盒：图层、材质、效果数值资源、预制体、场景（地面 / 墙 / 缺口 / FallZone）和构建设置。
    /// 重新运行会覆盖场景和预制体，但保留已经调过的效果数值资源。
    /// </summary>
    public static class ArenaSetup
    {
        const string Root = "Assets/AnomalyArena";
        const string MatDir = Root + "/Materials";
        const string PrefabDir = Root + "/Prefabs";
        const string EffectDir = Root + "/Effects";
        public const string ScenePath = Root + "/Scenes/Arena.unity";

        const float Half = 15f;       // 30 × 30
        const float WallThick = 1f;
        const float WallHeight = 1.5f;

        struct Gap
        {
            public string name;
            public char edge; // N S E W
            public float from, to;
        }

        // 规格第 3 节：以左下角为原点的坐标换算到以中心为原点（减 15）
        static readonly Gap[] Gaps =
        {
            new Gap { name = "LargeGap1_North", edge = 'N', from = 4f - Half, to = 12f - Half },
            new Gap { name = "LargeGap2_South", edge = 'S', from = 19f - Half, to = 26f - Half },
            new Gap { name = "SmallGap1_East", edge = 'E', from = 5f - Half, to = 7.5f - Half },
            new Gap { name = "SmallGap2_West", edge = 'W', from = 20f - Half, to = 22.5f - Half },
        };

        [MenuItem("Anomaly Arena/1. Build Whitebox Scene")]
        public static void Setup()
        {
            foreach (var d in new[] { MatDir, PrefabDir, EffectDir, Root + "/Scenes" })
                Directory.CreateDirectory(d);
            EnsureLayer("Wall");
            EnsureLayer("Character");
            int wallLayer = LayerMask.NameToLayer("Wall");
            int charLayer = LayerMask.NameToLayer("Character");

            // ───── 材质 ─────
            var mLit = Lit("Base", Color.white);
            var mFx = Fx("FxTransparent");
            var mFloor = Lit("Floor", new Color(0.78f, 0.8f, 0.82f));
            var mWall = Lit("Wall", new Color(0.3f, 0.32f, 0.35f));
            var mGapEdge = Lit("GapEdge", new Color(0.03f, 0.03f, 0.03f));
            var mPlayer = Lit("Player", new Color(0.2f, 0.45f, 0.85f));
            var mFacing = Lit("Facing", new Color(0.95f, 0.95f, 0.95f));
            var mSmall = Lit("EnemySmall", new Color(0.85f, 0.2f, 0.18f));
            var mLarge = Lit("EnemyLarge", new Color(0.45f, 0.08f, 0.08f));
            var mWindup = Lit("EnemyWindup", new Color(1f, 0.85f, 0.1f));
            var mSpawnX = Lit("SpawnX", new Color(0.95f, 0.1f, 0.1f));
            var mGun = Lit("Gun", new Color(0.25f, 0.27f, 0.3f));
            var mKnife = Lit("Knife", new Color(0.82f, 0.86f, 0.9f));
            var mMissile = Lit("Missile", new Color(0.9f, 0.35f, 0.2f));
            var noFriction = NoFriction();

            // ───── 效果数值资源（已存在则保留数值） ─────
            var swing = Effect<GunSwingEffect>("GunSwing", EffectId.GunSwing, WeaponType.Gun, "Swing", 0.35f,
                "Swings the gun like a club: 90° arc in front, knocks enemies back 5.");
            var reverse = Effect<GunReverseShotEffect>("GunReverseShot", EffectId.GunReverseShot, WeaponType.Gun, "Reverse Shot", 0.3f,
                "The bullet flies out behind you. The recoil pushes you forward 3.");
            var knifeThrow = Effect<KnifeThrowEffect>("KnifeThrow", EffectId.KnifeThrow, WeaponType.Knife, "Throw", 0.3f,
                "Flies straight 12. On a kill it drags the corpse back to where you stood. Move!");
            var hook = Effect<KnifeHookEffect>("KnifeHook", EffectId.KnifeHook, WeaponType.Knife, "Hook", 0.2f,
                "Hooks the first enemy in line and holds it as a shield. Left-click to throw it.");
            var homing = Effect<MissileHomingEffect>("MissileHoming", EffectId.MissileHoming, WeaponType.Missile, "Homing", 0.5f,
                "Launches in a random direction. After 1 s it comes for you. The blast hits everyone.");
            var self = Effect<MissileSelfLaunchEffect>("MissileSelfLaunch", EffectId.MissileSelfLaunch, WeaponType.Missile, "Self-Launch", 0.3f,
                "Launches YOU 15: kills whatever you hit, bounces off walls. Mind the gaps.");

            // ───── 预制体 ─────
            var playerGo = BuildCharacter("Player", 0.5f, 2f, 1f, mPlayer, mFacing, noFriction, charLayer, out var pBody, out var pHand);
            var pc = playerGo.AddComponent<PlayerController>();
            pc.maxHp = 50f;
            pc.moveSpeed = 6f;
            pc.radius = 0.5f;
            pc.knockbackScale = 1f;
            pc.protectionTime = 0.5f;
            pc.bodyRenderer = pBody;
            pc.hand = pHand;
            var playerPrefab = SavePrefab(playerGo, "Player");

            var smallGo = BuildCharacter("EnemySmall", 0.5f, 2f, 1f, mSmall, mFacing, noFriction, charLayer, out var sBody, out var sHand);
            var small = smallGo.AddComponent<Enemy>();
            SetEnemy(small, 10f, 6f * 0.9f, 0.5f, 1f, 5f, 1.5f, 0.4f, 1f, sBody, sHand, mSmall, mWindup);
            var smallPrefab = SavePrefab(smallGo, "EnemySmall").GetComponent<Enemy>();

            var largeGo = BuildCharacter("EnemyLarge", 1.5f, 3f, 5f, mLarge, mFacing, noFriction, charLayer, out var lBody, out var lHand);
            var large = largeGo.AddComponent<Enemy>();
            SetEnemy(large, 30f, 6f * 0.6f, 1.5f, 0.5f, 15f, 2f, 0.8f, 2f, lBody, lHand, mLarge, mWindup);
            var largePrefab = SavePrefab(largeGo, "EnemyLarge").GetComponent<Enemy>();

            // 地上的枪 / 刀 / 导弹：细长方块 / 扁平长条 / 竖着的圆柱
            var gunPrefab = BuildWeapon("Gun", WeaponType.Gun, PrimitiveType.Cube, new Vector3(0.25f, 0.25f, 1.2f), mGun);
            var knifePrefab = BuildWeapon("Knife", WeaponType.Knife, PrimitiveType.Cube, new Vector3(0.45f, 0.08f, 1.0f), mKnife);
            var missilePrefab = BuildWeapon("Missile", WeaponType.Missile, PrimitiveType.Cylinder, new Vector3(0.4f, 0.5f, 0.4f), mMissile);

            // ───── 场景 ─────
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.1f);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 150f;
            camGo.transform.position = new Vector3(0f, 46f, -15f);
            camGo.transform.rotation = Quaternion.Euler(74f, 0f, 0f);
            camGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.52f);

            BuildArena(mFloor, mWall, mGapEdge, wallLayer);

            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            var waves = new GameObject("WaveManager").AddComponent<WaveManager>();
            var spawner = new GameObject("WeaponSpawner").AddComponent<WeaponSpawner>();
            var hud = new GameObject("HUD").AddComponent<Hud>();

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = Vector3.zero;

            gm.player = player.GetComponent<PlayerController>();
            gm.waves = waves;
            gm.weapons = spawner;
            gm.hud = hud;
            gm.cam = cam;
            gm.litMaterial = mLit;
            gm.fxMaterial = mFx;
            gm.spawnMarkerMaterial = mSpawnX;

            waves.smallPrefab = smallPrefab;
            waves.largePrefab = largePrefab;

            spawner.defs = new[]
            {
                new WeaponSpawner.WeaponDef { type = WeaponType.Gun, prefab = gunPrefab, uses = 6, effectA = swing, effectB = reverse },
                new WeaponSpawner.WeaponDef { type = WeaponType.Knife, prefab = knifePrefab, uses = 4, effectA = knifeThrow, effectB = hook },
                new WeaponSpawner.WeaponDef { type = WeaponType.Missile, prefab = missilePrefab, uses = 2, effectA = homing, effectB = self },
            };

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "Anomaly Weapon Arena";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true; // GitHub Pages 不返回 Content-Encoding
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            AssetDatabase.SaveAssets();
            Debug.Log("[AnomalyArena] Whitebox scene built: " + ScenePath);
        }

        [MenuItem("Anomaly Arena/2. Build WebGL")]
        public static void BuildWebGL()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/WebGL",
                target = BuildTarget.WebGL,
            });
            Debug.Log("[AnomalyArena] WebGL build: " + report.summary.result);
        }

        // ───── 场地 ─────

        static void BuildArena(Material floor, Material wall, Material gapEdge, int wallLayer)
        {
            var root = new GameObject("Arena").transform;

            var f = Box("Floor", root, new Vector3(0f, -0.5f, 0f), new Vector3(Half * 2f, 1f, Half * 2f), floor, true);
            f.layer = 0;

            var walls = new GameObject("Walls").transform;
            walls.SetParent(root, false);
            foreach (char edge in "NSEW")
            {
                bool horizontal = edge == 'N' || edge == 'S';
                float from = horizontal ? -Half - WallThick : -Half;
                float to = horizontal ? Half + WallThick : Half;
                float cur = from;
                foreach (var g in Gaps)
                {
                    if (g.edge != edge) continue;
                    if (g.from > cur) WallSegment(walls, edge, cur, g.from, wall, gapEdge, wallLayer);
                    cur = g.to;
                }
                if (to > cur) WallSegment(walls, edge, cur, to, wall, gapEdge, wallLayer);
            }

            var gaps = new GameObject("Gaps").transform;
            gaps.SetParent(root, false);
            foreach (var g in Gaps)
            {
                var gt = new GameObject(g.name).transform;
                gt.SetParent(gaps, false);
                float w = g.to - g.from, mid = (g.from + g.to) * 0.5f;

                // 缺口边缘描黑
                Vector3 stripPos = EdgePoint(g.edge, mid, -0.08f, 0.01f);
                Vector3 stripScale = g.edge == 'N' || g.edge == 'S' ? new Vector3(w, 0.02f, 0.16f) : new Vector3(0.16f, 0.02f, w);
                Box("EdgeOutline", gt, stripPos, stripScale, gapEdge, false);

                // 缺口外的 FallZone：从边线外 1 格开始，避免卡在小缺口上的大型敌人被误判
                const float offset = 1f, depth = 4f;
                Vector3 zonePos = EdgePoint(g.edge, mid, offset + depth * 0.5f, -4f);
                Vector3 zoneSize = g.edge == 'N' || g.edge == 'S' ? new Vector3(w + 1f, 12f, depth) : new Vector3(depth, 12f, w + 1f);
                var zone = new GameObject("FallZone");
                zone.transform.SetParent(gt, false);
                zone.transform.position = zonePos;
                var bc = zone.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.size = zoneSize;
                zone.AddComponent<FallZone>().outward = Outward(g.edge);
            }
        }

        static void WallSegment(Transform parent, char edge, float from, float to, Material wall, Material gapEdge, int layer)
        {
            if (to - from < 0.01f) return;
            float len = to - from, mid = (from + to) * 0.5f;
            Vector3 pos = EdgePoint(edge, mid, WallThick * 0.5f, WallHeight * 0.5f);
            Vector3 scale = edge == 'N' || edge == 'S' ? new Vector3(len, WallHeight, WallThick) : new Vector3(WallThick, WallHeight, len);
            var go = Box($"Wall_{edge}_{from:0.#}_{to:0.#}", parent, pos, scale, wall, true);
            go.layer = layer;

            // 靠近缺口的墙端面也描黑
            foreach (var g in Gaps)
            {
                if (g.edge != edge) continue;
                float end = Mathf.Abs(to - g.from) < 0.01f ? to : Mathf.Abs(from - g.to) < 0.01f ? from : float.NaN;
                if (float.IsNaN(end)) continue;
                Vector3 capPos = EdgePoint(edge, end, WallThick * 0.5f, WallHeight * 0.5f);
                Vector3 capScale = edge == 'N' || edge == 'S' ? new Vector3(0.06f, WallHeight + 0.02f, WallThick + 0.02f) : new Vector3(WallThick + 0.02f, WallHeight + 0.02f, 0.06f);
                Box("GapEdgeCap", go.transform.parent, capPos, capScale, gapEdge, false);
            }
        }

        /// <summary>edge 边上沿边坐标 along、向外 outward、高度 y 的点。</summary>
        static Vector3 EdgePoint(char edge, float along, float outward, float y)
        {
            switch (edge)
            {
                case 'N': return new Vector3(along, y, Half + outward);
                case 'S': return new Vector3(along, y, -Half - outward);
                case 'E': return new Vector3(Half + outward, y, along);
                default: return new Vector3(-Half - outward, y, along);
            }
        }

        static Vector3 Outward(char edge) =>
            edge == 'N' ? Vector3.forward : edge == 'S' ? Vector3.back : edge == 'E' ? Vector3.right : Vector3.left;

        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material m, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            return go;
        }

        // ───── 预制体 ─────

        static GameObject BuildCharacter(string name, float r, float height, float mass, Material body, Material facing,
            PhysicsMaterial physMat, int layer, out Renderer bodyRenderer, out Transform hand)
        {
            var root = new GameObject(name) { layer = layer };
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearDamping = 0f;
            var col = root.AddComponent<CapsuleCollider>();
            col.radius = r;
            col.height = height;
            col.center = new Vector3(0f, height * 0.5f, 0f);
            col.sharedMaterial = physMat;

            // Unity 胶囊体默认直径 1、高 2
            var vis = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            vis.name = "Body";
            Object.DestroyImmediate(vis.GetComponent<Collider>());
            vis.transform.SetParent(root.transform, false);
            vis.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            vis.transform.localScale = new Vector3(r * 2f, height * 0.5f, r * 2f);
            bodyRenderer = vis.GetComponent<Renderer>();
            bodyRenderer.sharedMaterial = body;

            // 前面加一个小方块表示朝向
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Facing";
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.transform.SetParent(root.transform, false);
            float s = Mathf.Max(0.25f, r * 0.4f);
            nose.transform.localPosition = new Vector3(0f, height * 0.72f, r);
            nose.transform.localScale = new Vector3(s, s, s);
            nose.GetComponent<Renderer>().sharedMaterial = facing;

            var h = new GameObject("Hand").transform;
            h.SetParent(root.transform, false);
            h.localPosition = new Vector3(r * 0.9f, height * 0.45f, r * 0.6f);
            hand = h;
            return root;
        }

        static void SetEnemy(Enemy e, float hp, float speed, float r, float knockScale, float dmg, float range, float windup,
            float interval, Renderer body, Transform hand, Material normal, Material windupMat)
        {
            e.maxHp = hp;
            e.moveSpeed = speed;
            e.radius = r;
            e.knockbackScale = knockScale;
            e.protectionTime = 0f;
            e.attackDamage = dmg;
            e.attackRange = range;
            e.windupTime = windup;
            e.attackInterval = interval;
            e.attackKnockback = 5f;
            e.bodyRenderer = body;
            e.hand = hand;
            e.normalMaterial = normal;
            e.windupMaterial = windupMat;
        }

        static Weapon BuildWeapon(string name, WeaponType type, PrimitiveType shape, Vector3 scale, Material m)
        {
            var root = new GameObject(name);
            var vis = new GameObject("Visual").transform;
            vis.SetParent(root.transform, false);
            var part = GameObject.CreatePrimitive(shape);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(vis, false);
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = m;
            var w = root.AddComponent<Weapon>();
            w.type = type;
            w.visual = vis;
            return SavePrefab(root, name).GetComponent<Weapon>();
        }

        static GameObject SavePrefab(GameObject go, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ───── 资源 ─────

        static T Effect<T>(string file, EffectId id, WeaponType type, string displayName, float cooldown, string desc) where T : WeaponEffect
        {
            string path = $"{EffectDir}/{file}.asset";
            var e = AssetDatabase.LoadAssetAtPath<T>(path);
            if (e == null)
            {
                e = ScriptableObject.CreateInstance<T>();
                e.cooldown = cooldown;
                e.description = desc;
                AssetDatabase.CreateAsset(e, path);
            }
            e.id = id;
            e.type = type;
            e.displayName = displayName;
            if (string.IsNullOrEmpty(e.description)) e.description = desc;
            EditorUtility.SetDirty(e);
            return e;
        }

        static Material Lit(string name, Color c)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = c;
            m.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Fx(string name)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.color = Color.white;
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static PhysicsMaterial NoFriction()
        {
            string path = $"{MatDir}/NoFriction.physicMaterial";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (pm != null) return pm;
            pm = new PhysicsMaterial("NoFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };
            AssetDatabase.CreateAsset(pm, path);
            return pm;
        }

        static void EnsureLayer(string name)
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == name) return;
            for (int i = 8; i < layers.arraySize; i++)
            {
                var el = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(el.stringValue)) continue;
                el.stringValue = name;
                tm.ApplyModifiedProperties();
                return;
            }
            Debug.LogError("[AnomalyArena] No free layer slot for " + name);
        }
    }
}
