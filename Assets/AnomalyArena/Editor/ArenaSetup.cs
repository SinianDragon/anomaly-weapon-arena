using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalyArena.EditorTools
{
    /// <summary>
    /// One-click whitebox build: layers, materials, effect value assets, prefabs, the scene (floor / walls / gaps / FallZones) and build settings.
    /// Running it again overwrites the scene and prefabs but keeps effect value assets that have already been tuned.
    /// </summary>
    public static class ArenaSetup
    {
        const string Root = "Assets/AnomalyArena";
        const string MatDir = Root + "/Materials";
        const string PrefabDir = Root + "/Prefabs";
        const string EffectDir = Root + "/Effects";
        public const string ScenePath = Root + "/Scenes/Arena.unity";

        /// <summary>Release version: written to PlayerSettings.bundleVersion and shown in the bottom-right corner of the title screen.</summary>
        public const string Version = "1.0.0";

        const float LargeRadius = 1f, LargeHeight = 4f; // large enemy = twice the small one (radius 0.5, height 2)

        [MenuItem("Anomaly Arena/1. Build Whitebox Scene")]
        public static void Setup()
        {
            foreach (var d in new[] { MatDir, PrefabDir, EffectDir, Root + "/Scenes" })
                Directory.CreateDirectory(d);
            EnsureLayer("Wall");
            EnsureLayer("Character");
            int wallLayer = LayerMask.NameToLayer("Wall");
            int charLayer = LayerMask.NameToLayer("Character");

            // ───── Materials ─────
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

            // ───── Effect value assets (existing values are kept; names and descriptions are refreshed every time) ─────
            var (swing, reverse, knifeThrow, hook, homing, self) = Effects();

            // ───── Prefabs ─────
            var playerGo = BuildCharacter("Player", 0.5f, 2f, 1f, mPlayer, mFacing, noFriction, charLayer,
                out var pBody, out var pHand);
            var pc = playerGo.AddComponent<PlayerController>();
            pc.maxHp = 50f;
            pc.moveSpeed = 6f;
            pc.radius = 0.5f;
            pc.knockbackScale = 1f;
            pc.protectionTime = 0.5f;
            pc.bodyRenderer = pBody;
            pc.hand = pHand;
            var playerPrefab = SavePrefab(playerGo, "Player");

            var smallGo = BuildCharacter("EnemySmall", 0.5f, 2f, 1f, mSmall, mFacing, noFriction, charLayer,
                out var sBody, out var sHand);
            var small = smallGo.AddComponent<Enemy>();
            SetEnemy(small, 10f, 6f * 0.9f, 0.5f, 1f, 5f, 0.3f, 0.8f, 0.5f, 1f, sBody, sHand, mSmall, mWindup);
            var smallPrefab = SavePrefab(smallGo, "EnemySmall").GetComponent<Enemy>();

            // Large = twice the size of small: radius 1, height 4
            var largeGo = BuildCharacter("EnemyLarge", LargeRadius, LargeHeight, 5f, mLarge, mFacing, noFriction,
                charLayer, out var lBody, out var lHand);
            var large = largeGo.AddComponent<Enemy>();
            SetEnemy(large, 30f, 6f * 0.6f, LargeRadius, 0.5f, 15f, 0.4f, 1f, 0.8f, 2f, lBody, lHand, mLarge, mWindup);
            large.large = true;
            var largePrefab = SavePrefab(largeGo, "EnemyLarge").GetComponent<Enemy>();

            // Gun / knife / missile on the ground: thin box / flat bar / upright cylinder
            var gunPrefab = BuildWeapon("Gun", WeaponType.Gun, PrimitiveType.Cube, new Vector3(0.25f, 0.25f, 1.2f),
                mGun);
            var knifePrefab = BuildWeapon("Knife", WeaponType.Knife, PrimitiveType.Cube,
                new Vector3(0.45f, 0.08f, 1.0f), mKnife);
            var missilePrefab = BuildWeapon("Missile", WeaponType.Missile, PrimitiveType.Cylinder,
                new Vector3(0.4f, 0.5f, 0.4f), mMissile);

            // ───── Scene ─────
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

            ArenaGeometryBuilder.Build(mFloor, mWall, mGapEdge, wallLayer); // irregular polygon platform, shape defined in ArenaShape

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
            gm.art = BuildArtSet();

            waves.smallPrefab = smallPrefab;
            waves.largePrefab = largePrefab;

            spawner.defs = new[]
            {
                new WeaponSpawner.WeaponDef
                    { type = WeaponType.Gun, prefab = gunPrefab, uses = 6, effectA = swing, effectB = reverse },
                new WeaponSpawner.WeaponDef
                    { type = WeaponType.Knife, prefab = knifePrefab, uses = 4, effectA = knifeThrow, effectB = hook },
                new WeaponSpawner.WeaponDef
                    { type = WeaponType.Missile, prefab = missilePrefab, uses = 2, effectA = homing, effectB = self },
            };

            EditorSceneManager.SaveScene(scene, ScenePath);
            // Reopen the scene that was just saved: otherwise the scene in memory still points at the old prefabs and pressing Play reports missing references
            EditorSceneManager.OpenScene(ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "Anomaly Weapon Arena";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true; // GitHub Pages does not send Content-Encoding
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

        /// <summary>
        /// Update without rebuilding the scene: refresh effect names / descriptions, generate the art set and assign it to the scene's GameManager,
        /// switch the enemy prefabs to the close-range punch values, shrink the large enemy to twice the small one, and set weapon supply to 3 / 5 / 8 per wave. All other tuned values are kept.
        /// </summary>
        [MenuItem("Anomaly Arena/3. Update Existing Scene (texts, art, enemies, weapon supply)")]
        public static void UpdateExisting()
        {
            var (_, reverse, knifeThrow, _, _, _) = Effects();
            // v0.8 feel: Reverse Shot becomes hold-to-fire (one use = a 5-round clip, small recoil per shot); the knife returns a bit faster.
            // Old values stored in the assets override the code defaults, so they are written explicitly here
            reverse.cooldown = 0.12f;
            reverse.holdToRepeat = true;
            reverse.roundsPerUse = 5;
            reverse.recoilDistance = 0.8f;
            knifeThrow.returnSpeed = 14f;
            EditorUtility.SetDirty(reverse);
            EditorUtility.SetDirty(knifeThrow);
            var art = BuildArtSet();
            PatchEnemy(PrefabDir + "/EnemySmall.prefab", false, 0.5f, 2f, 0.3f, 0.8f, 0.5f);
            PatchEnemy(PrefabDir + "/EnemyLarge.prefab", true, LargeRadius, LargeHeight, 0.4f, 1f, 0.8f);

            var scene = EditorSceneManager.OpenScene(ScenePath);
            var gm = Object.FindAnyObjectByType<GameManager>();
            gm.art = art;
            EditorUtility.SetDirty(gm);
            // Ground cap raised to 12 so the 8 weapons of wave 3 fit
            var spawner = Object.FindAnyObjectByType<WeaponSpawner>();
            spawner.perWaveCounts = new[] { 3, 5, 8 };
            spawner.groundCap = 12;
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.SaveScene(scene);
            PlayerSettings.productName = "Anomaly Weapon Arena";
            PlayerSettings.bundleVersion = Version;
            AssetDatabase.SaveAssets();
            Debug.Log("[AnomalyArena] Existing scene updated: texts, art set, enemies, weapon supply.");
        }

        static void PatchEnemy(string path, bool large, float r, float height, float trigger, float range, float windup)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            var e = root.GetComponent<Enemy>();
            e.large = large;
            e.radius = r;
            e.triggerRange = trigger;
            e.attackRange = range;
            e.attackArc = 120f;
            e.windupTime = windup;
            ApplyBodySize(root, r, height);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        /// <summary>Sets up the collider, whitebox body, facing marker and hand position from radius and height (shared by BuildCharacter and PatchEnemy).</summary>
        static void ApplyBodySize(GameObject root, float r, float height)
        {
            var col = root.GetComponent<CapsuleCollider>();
            col.radius = r;
            col.height = height;
            col.center = new Vector3(0f, height * 0.5f, 0f);
            // Unity's capsule has diameter 1 and height 2 by default
            var body = root.transform.Find("Body");
            body.localPosition = new Vector3(0f, height * 0.5f, 0f);
            body.localScale = new Vector3(r * 2f, height * 0.5f, r * 2f);
            var nose = root.transform.Find("Facing");
            float s = Mathf.Max(0.25f, r * 0.4f);
            nose.localPosition = new Vector3(0f, height * 0.72f, r);
            nose.localScale = new Vector3(s, s, s);
            root.transform.Find("Hand").localPosition = new Vector3(r * 0.9f, height * 0.45f, r * 0.6f);
        }

        static (GunSwingEffect, GunReverseShotEffect, KnifeThrowEffect, KnifeHookEffect, MissileHomingEffect,
            MissileSelfLaunchEffect) Effects()
        {
            var swing = Effect<GunSwingEffect>("GunSwing", EffectId.GunSwing, WeaponType.Gun, "Charge Swing", 0.35f,
                "Hold to charge (you slow down), release to hit EVERYONE in the arc. Longer charge = wider reach, more damage, bigger knockback");
            var reverse = Effect<GunReverseShotEffect>("GunReverseShot", EffectId.GunReverseShot, WeaponType.Gun,
                "Reverse Shot", 0.12f,
                "Hold to fire. Bullets fly out of your BACK and every shot's recoil nudges you forward - don't aim at a gap. 5 rounds per use");
            var knifeThrow = Effect<KnifeThrowEffect>("KnifeThrow", EffectId.KnifeThrow, WeaponType.Knife,
                "Throwing Knife", 0.3f,
                "Ricochets between up to 4 enemies, then flies back - you can't throw again until it returns. A kill drags the corpse back at you");
            var hook = Effect<KnifeHookEffect>("KnifeHook", EffectId.KnifeHook, WeaponType.Knife, "Hook", 0.2f,
                "The blade extends and hooks the first enemy, pulling it in as a shield; left click again to throw it");
            var homing = Effect<MissileHomingEffect>("MissileHoming", EffectId.MissileHoming, WeaponType.Missile,
                "Homing", 0.5f,
                "Flies off in a random direction, then locks onto a random target after 1s (it likes you best); the blast hits everyone");
            var self = Effect<MissileSelfLaunchEffect>("MissileSelfLaunch", EffectId.MissileSelfLaunch,
                WeaponType.Missile, "Launch Yourself", 0.3f,
                "Fire YOURSELF 15 tiles like a missile: kills enemies on the way; a wall stops you, hurts you and bounces you back; gaps swallow you");
            return (swing, reverse, knifeThrow, hook, homing, self);
        }

        // ───── Illustrated-mode assets ─────

        const string ArtDir = Root + "/Art";

        /// <summary>Sets texture import options (transparency, tiling for the floor) and creates / updates ArtSet.asset.</summary>
        static ArtSet BuildArtSet()
        {
            string path = ArtDir + "/ArtSet.asset";
            var set = AssetDatabase.LoadAssetAtPath<ArtSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<ArtSet>();
                AssetDatabase.CreateAsset(set, path);
            }

            set.player = Tex("player");
            set.enemySmall = Tex("enemy_small");
            set.enemyLarge = Tex("enemy_large");
            set.gun = Tex("gun");
            set.knife = Tex("knife");
            set.missileLauncher = Tex("missile_launcher");
            set.hookHilt = Tex("hook_hilt");
            set.hookMid = Tex("hook_mid", true); // one chain link, tiled along the length
            set.hookTip = Tex("hook_tip");
            set.missile = Tex("missile");
            set.flame = Tex("flame");
            set.floor = Tex("floor_sand_muted", true); // desaturated, low-contrast sand so text on top stays readable
            set.gapEdge = Tex("gap_edge");
            set.wallBrick = Tex("wall_brick", true);
            // 1.0 assets: wind-up glow (made from the base image plus a halo, same margin on every side), corpses, effects
            set.enemySmallWindup = Tex("enemy_small_windup");
            set.enemyLargeWindup = Tex("enemy_large_windup");
            set.corpseSmall = Tex("corpse_small");
            set.corpseLarge = Tex("corpse_large");
            set.rocketFlame = Tex("rocket_flame");
            set.bullet = Tex("bullet");
            set.enemyPunchArc = Tex("enemy_punch_arc");
            set.hitSpark = Tex("hit_spark");
            set.dust = Tex("dust");
            set.explosionFrames = new[] { Tex("explosion_0"), Tex("explosion_1"), Tex("explosion_2") };
            set.explosionRing = Tex("explosion_ring");
            set.shieldBubble = Tex("shield_bubble");
            set.spawnXSmall = Tex("spawn_x_small");
            set.spawnXLarge = Tex("spawn_x_large");
            EditorUtility.SetDirty(set);
            return set;
        }

        static Texture2D Tex(string name, bool repeat = false)
        {
            string path = $"{ArtDir}/Textures/{name}.png";
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                bool dirty = ti.textureType != TextureImporterType.Default || !ti.alphaIsTransparency ||
                             ti.npotScale != TextureImporterNPOTScale.None
                             || ti.wrapMode != (repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp);
                if (dirty)
                {
                    ti.textureType = TextureImporterType.Default;
                    ti.alphaIsTransparency = true;
                    ti.npotScale = TextureImporterNPOTScale.None; // keep the original aspect ratio; quads are scaled to match
                    ti.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                    ti.mipmapEnabled = true;
                    ti.SaveAndReimport();
                }
            }

            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null) Debug.LogError("[AnomalyArena] Missing art texture: " + path);
            return t;
        }

        // ───── Prefabs ─────

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
            col.sharedMaterial = physMat;

            var vis = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            vis.name = "Body";
            Object.DestroyImmediate(vis.GetComponent<Collider>());
            vis.transform.SetParent(root.transform, false);
            bodyRenderer = vis.GetComponent<Renderer>();
            bodyRenderer.sharedMaterial = body;

            // Small box in front to show facing
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Facing";
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.transform.SetParent(root.transform, false);
            nose.GetComponent<Renderer>().sharedMaterial = facing;

            hand = new GameObject("Hand").transform;
            hand.SetParent(root.transform, false);
            ApplyBodySize(root, r, height);
            return root;
        }

        static void SetEnemy(Enemy e, float hp, float speed, float r, float knockScale, float dmg, float trigger,
            float range, float windup,
            float interval, Renderer body, Transform hand, Material normal, Material windupMat)
        {
            e.maxHp = hp;
            e.moveSpeed = speed;
            e.radius = r;
            e.knockbackScale = knockScale;
            e.protectionTime = 0f;
            e.attackDamage = dmg;
            e.triggerRange = trigger;
            e.attackRange = range;
            e.attackArc = 120f;
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

        // ───── Assets ─────

        static T Effect<T>(string file, EffectId id, WeaponType type, string displayName, float cooldown, string desc)
            where T : WeaponEffect
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
            e.description = desc;
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
                if (layers.GetArrayElementAtIndex(i).stringValue == name)
                    return;
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