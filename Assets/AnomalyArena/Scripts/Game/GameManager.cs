using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AnomalyArena
{
    public enum GameState { Title, Playing, Won, Lost }

    /// <summary>一局的总控：开始、胜负判定、R 重开、清理飞行物和特殊状态。</summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        [Serializable]
        public class Rules
        {
            [Tooltip("被撞飞 / 被扔出去后撞墙扣的血（不受保护影响）")] public float wallDamage = 5f;
            [Tooltip("撞墙那一刻朝墙的速度超过这个值才算撞到（待调）")] public float wallHitSpeed = 3f;
            [Tooltip("撞飞的衰减系数：初速度 = 距离 × 系数")] public float knockDamping = 6f;
            [Tooltip("平台半边长：30 × 30 → 15")] public float arenaHalfSize = 15f;
        }

        public static GameManager Instance { get; private set; }
        public static int WallLayer { get; private set; }
        public static int CharacterLayer { get; private set; }
        public static int WallMask { get; private set; }
        public static int CharacterMask { get; private set; }

        public Rules rules = new Rules();

        [Header("Refs")]
        public PlayerController player;
        public WaveManager waves;
        public WeaponSpawner weapons;
        public Hud hud;
        public Camera cam;

        [Header("Materials")]
        [Tooltip("运行时生成的白盒物体用的基础材质（URP Lit）")] public Material litMaterial;
        [Tooltip("半透明提示用的材质（URP Unlit 透明）")] public Material fxMaterial;
        public Material spawnMarkerMaterial;

        [Header("UI")]
        [Tooltip("界面字体：思源黑体（SIL OFL）按游戏用到的字裁剪的子集")] public Font uiFont;

        public GameState State { get; private set; } = GameState.Title;
        public DeathCause LoseCause { get; private set; }
        public float Elapsed { get; private set; }

        readonly List<MonoBehaviour> transients = new List<MonoBehaviour>();
        readonly Dictionary<Color, Material> matCache = new Dictionary<Color, Material>();
        bool finalWaveCleared;
        /// <summary>按 R 重开后跳过标题，直接从第 1 波开始。</summary>
        static bool skipTitle;

        void Awake()
        {
            Instance = this;
            WallLayer = LayerMask.NameToLayer("Wall");
            CharacterLayer = LayerMask.NameToLayer("Character");
            WallMask = 1 << WallLayer;
            CharacterMask = 1 << CharacterLayer;
            if (!cam) cam = Camera.main;
        }

        void Start()
        {
            weapons.SpawnInitial();
            if (!skipTitle) return;
            skipTitle = false;
            BeginPlaying();
        }

        void BeginPlaying()
        {
            State = GameState.Playing;
            waves.StartWaves();
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            Cursor.visible = State != GameState.Playing;

            switch (State)
            {
                case GameState.Title:
                    if ((mouse != null && mouse.leftButton.wasPressedThisFrame) || (kb != null && kb.spaceKey.wasPressedThisFrame))
                        BeginPlaying();
                    break;
                case GameState.Playing:
                    Elapsed += Time.deltaTime;
                    break;
                default:
                    if (kb != null && kb.rKey.wasPressedThisFrame)
                    {
                        skipTitle = true;
                        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                    }
                    break;
            }
        }

        /// <summary>第 3 波最后一个敌人被消灭时由 WaveManager 调用。</summary>
        public void NotifyFinalWaveCleared() => finalWaveCleared = true;

        /// <summary>一帧结束时统一判定胜负：玩家和最后一个敌人同一帧死亡，判胜利（待定）。</summary>
        void LateUpdate()
        {
            if (State != GameState.Playing) return;
            if (finalWaveCleared)
            {
                State = GameState.Won;
                ClearTransients();
            }
            else if (!player.IsAlive)
            {
                State = GameState.Lost;
                LoseCause = player.DeathCause;
                ClearTransients();
            }
        }

        // ───── 飞行物和特殊状态的统一清理 ─────

        public void Register(MonoBehaviour t)
        {
            if (t && !transients.Contains(t)) transients.Add(t);
        }

        public void Unregister(MonoBehaviour t) => transients.Remove(t);

        /// <summary>切换波次、玩家死亡时：清掉所有飞行中的刀、尸体、导弹，结束钩子和冲刺。</summary>
        public void ClearTransients()
        {
            var copy = transients.ToArray();
            transients.Clear();
            foreach (var t in copy)
            {
                if (!t) continue;
                if (t is WeaponRuntime r) r.Cancel();
                else Destroy(t.gameObject);
            }
        }

        // ───── 材质 ─────

        public Material Mat(Color c)
        {
            c.a = 1f;
            if (matCache.TryGetValue(c, out var m)) return m;
            m = new Material(litMaterial);
            m.color = c;
            matCache[c] = m;
            return m;
        }

        public Material FxMat(Color c)
        {
            var m = new Material(fxMaterial);
            m.color = c;
            return m;
        }
    }
}
