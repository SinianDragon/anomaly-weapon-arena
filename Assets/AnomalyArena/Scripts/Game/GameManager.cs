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

        /// <summary>受击反馈的手感参数（闪白、压扁、顿帧、震屏）。</summary>
        [Serializable]
        public class Feedback
        {
            [Tooltip("受击闪白持续秒数")] public float flashTime = 0.1f;
            [Tooltip("受击时身体被压扁的程度（0 = 不变形）")] public float squash = 0.35f;
            [Tooltip("普通受击的顿帧秒数（真实时间）")] public float hitStop = 0.035f;
            [Tooltip("重击（伤害达到 heavyDamage、撞墙、爆炸、玩家挨打）的顿帧秒数")] public float heavyHitStop = 0.08f;
            [Tooltip("伤害达到多少算重击")] public float heavyDamage = 10f;
            [Tooltip("顿帧期间的时间缩放")] public float hitStopTimeScale = 0.05f;
            [Tooltip("玩家挨打时的震屏幅度（格）")] public float playerHitShake = 0.5f;
            [Tooltip("重击敌人时的震屏幅度（格）")] public float heavyShake = 0.25f;
            [Tooltip("震屏每秒衰减多少格")] public float shakeDecay = 4f;
        }

        public static GameManager Instance { get; private set; }
        public static int WallLayer { get; private set; }
        public static int CharacterLayer { get; private set; }
        public static int WallMask { get; private set; }
        public static int CharacterMask { get; private set; }

        public Rules rules = new Rules();
        public Feedback feedback = new Feedback();

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

        [Header("Art")]
        [Tooltip("美术版贴图；在标题画面切换白盒 / 美术版")] public ArtSet art;

        public GameState State { get; private set; } = GameState.Title;
        public DeathCause LoseCause { get; private set; }
        public float Elapsed { get; private set; }

        readonly List<MonoBehaviour> transients = new List<MonoBehaviour>();
        readonly Dictionary<Color, Material> matCache = new Dictionary<Color, Material>();
        bool finalWaveCleared;
        /// <summary>按 R 重开后跳过标题，直接从第 1 波开始。</summary>
        static bool skipTitle;
        float hitStopLeft;
        bool hitStopStartedThisFrame;
        float shake;
        Vector3 camBase;

        void Awake()
        {
            Instance = this;
            WallLayer = LayerMask.NameToLayer("Wall");
            CharacterLayer = LayerMask.NameToLayer("Character");
            WallMask = 1 << WallLayer;
            CharacterMask = 1 << CharacterLayer;
            if (!cam) cam = Camera.main;
            if (cam) camBase = cam.transform.position;
            // timeScale 是全局的，重开 / 切美术重新加载场景时不会自动复原；顿帧中途切场景会一直慢动作
            Time.timeScale = 1f;
        }

        // ───── 受击反馈：顿帧与震屏 ─────

        /// <summary>顿帧：接下来 seconds 秒（真实时间）几乎暂停。同时来的多次取最长的，不累加。</summary>
        public void HitStop(float seconds)
        {
            if (seconds <= 0f) return;
            hitStopLeft = Mathf.Max(hitStopLeft, seconds);
            // 立即生效，而不是等下一帧的 Update：等到下一帧时会先扣掉这一整帧的耗时，
            // 帧时间一长（卡顿、编辑器在后台降帧）顿帧还没生效就已经结束了
            Time.timeScale = feedback.hitStopTimeScale;
            hitStopStartedThisFrame = true;
        }

        /// <summary>震屏：幅度 amount 格，按 shakeDecay 衰减。同时来的多次取最大的，不累加。</summary>
        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        void TickHitStop()
        {
            if (hitStopLeft <= 0f) return;
            // 刚开始的顿帧先不扣时间：保证至少完整停住一帧，一帧比顿帧还长（卡顿）时也看得到定格
            if (hitStopStartedThisFrame) hitStopStartedThisFrame = false;
            else hitStopLeft -= Time.unscaledDeltaTime;
            Time.timeScale = hitStopLeft > 0f ? feedback.hitStopTimeScale : 1f;
        }

        void TickShake()
        {
            if (!cam) return;
            // 用真实时间衰减：顿帧期间镜头照样在抖
            shake = Mathf.MoveTowards(shake, 0f, feedback.shakeDecay * Time.unscaledDeltaTime);
            Vector2 o = shake > 0f ? UnityEngine.Random.insideUnitCircle * shake : Vector2.zero;
            cam.transform.position = camBase + new Vector3(o.x, 0f, o.y);
        }

        void Start()
        {
            if (Art.On) Art.DecorateArena();
            weapons.SpawnInitial();
            if (!skipTitle) return;
            skipTitle = false;
            BeginPlaying();
        }

        /// <summary>标题画面的 START 按钮 / 空格。</summary>
        public void BeginPlaying()
        {
            if (State != GameState.Title) return;
            State = GameState.Playing;
            waves.StartWaves();
        }

        /// <summary>标题画面切换白盒 / 美术版：记下开关，重新加载场景让所有物体按新模式生成。</summary>
        public void ToggleArt()
        {
            if (State != GameState.Title) return;
            Art.Enabled = !Art.Enabled;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void Update()
        {
            TickHitStop();
            var kb = Keyboard.current;
            Cursor.visible = State != GameState.Playing;

            switch (State)
            {
                case GameState.Title:
                    // 鼠标点击交给界面上的按钮，避免点“切换美术”时顺便开局
                    if (kb != null && kb.spaceKey.wasPressedThisFrame) BeginPlaying();
                    else if (kb != null && kb.tKey.wasPressedThisFrame) ToggleArt();
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
            TickShake();
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
