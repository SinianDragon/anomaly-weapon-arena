using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AnomalyArena
{
    public enum GameState { Title, Playing, Won, Lost }

    /// <summary>Controls a run: start, win / lose, restart with R, cleanup of projectiles and special states.</summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        [Serializable]
        public class Rules
        {
            [Tooltip("Damage for hitting a wall after being knocked back / thrown (ignores protection)")] public float wallDamage = 5f;
            [Tooltip("Speed toward the wall at impact must exceed this to count as a hit (to be tuned)")] public float wallHitSpeed = 3f;
            [Tooltip("Knockback damping: initial speed = distance × damping")] public float knockDamping = 6f;
        }

        /// <summary>Feel parameters for hit feedback (flash, squash, hit stop, screen shake).</summary>
        [Serializable]
        public class Feedback
        {
            [Tooltip("Seconds the white flash lasts")] public float flashTime = 0.1f;
            [Tooltip("How much the body is squashed on hit (0 = no deformation)")] public float squash = 0.35f;
            [Tooltip("Hit stop for a normal hit, in seconds (real time)")] public float hitStop = 0.035f;
            [Tooltip("Hit stop for a heavy hit (damage reaches heavyDamage, wall impact, explosion, player hit), in seconds")] public float heavyHitStop = 0.08f;
            [Tooltip("Damage at which a hit counts as heavy")] public float heavyDamage = 10f;
            [Tooltip("Time scale during hit stop")] public float hitStopTimeScale = 0.05f;
            [Tooltip("Screen shake amplitude when the player is hit (units)")] public float playerHitShake = 0.5f;
            [Tooltip("Screen shake amplitude when an enemy takes a heavy hit (units)")] public float heavyShake = 0.25f;
            [Tooltip("How many units of shake decay per second")] public float shakeDecay = 4f;
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
        [Tooltip("Base material for whitebox objects created at runtime (URP Lit)")] public Material litMaterial;
        [Tooltip("Material for translucent hints (URP Unlit transparent)")] public Material fxMaterial;
        public Material spawnMarkerMaterial;

        [Header("Art")]
        [Tooltip("Illustrated-mode textures; whitebox / illustrated is switched on the title screen")] public ArtSet art;

        public GameState State { get; private set; } = GameState.Title;
        public DeathCause LoseCause { get; private set; }
        public float Elapsed { get; private set; }

        readonly List<MonoBehaviour> transients = new List<MonoBehaviour>();
        readonly Dictionary<Color, Material> matCache = new Dictionary<Color, Material>();
        bool finalWaveCleared;
        /// <summary>After restarting with R, skip the title screen and start from wave 1.</summary>
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
            // timeScale is global and is not restored when the scene reloads on restart / art switch; switching scenes mid hit stop would leave everything in slow motion
            Time.timeScale = 1f;
        }

        // ───── Hit feedback: hit stop and screen shake ─────

        /// <summary>Hit stop: nearly pauses for the next seconds seconds (real time). Overlapping requests take the longest, they do not add up.</summary>
        public void HitStop(float seconds)
        {
            if (seconds <= 0f) return;
            hitStopLeft = Mathf.Max(hitStopLeft, seconds);
            // Applied immediately instead of waiting for the next Update: by the next frame a whole frame's time would already be deducted,
            // and with a long frame (hitch, editor throttled in the background) the hit stop would end before it took effect
            Time.timeScale = feedback.hitStopTimeScale;
            hitStopStartedThisFrame = true;
        }

        /// <summary>Screen shake: amplitude amount units, decaying by shakeDecay. Overlapping requests take the largest, they do not add up.</summary>
        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        void TickHitStop()
        {
            if (hitStopLeft <= 0f) return;
            // A hit stop that just started is not counted down yet: guarantees at least one fully frozen frame, visible even when a frame is longer than the hit stop (hitch)
            if (hitStopStartedThisFrame) hitStopStartedThisFrame = false;
            else hitStopLeft -= Time.unscaledDeltaTime;
            Time.timeScale = hitStopLeft > 0f ? feedback.hitStopTimeScale : 1f;
        }

        void TickShake()
        {
            if (!cam) return;
            // Decays in real time: the camera keeps shaking during hit stop
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

        /// <summary>START button / Space on the title screen.</summary>
        public void BeginPlaying()
        {
            if (State != GameState.Title) return;
            State = GameState.Playing;
            waves.StartWaves();
        }

        /// <summary>Whitebox / illustrated switch on the title screen: store the flag and reload the scene so every object is created in the new mode.</summary>
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
                    // Mouse clicks are left to the on-screen buttons, so clicking 'switch art' does not also start the game
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

        /// <summary>Called by WaveManager when the last enemy of wave 3 is eliminated.</summary>
        public void NotifyFinalWaveCleared() => finalWaveCleared = true;

        /// <summary>Win / lose is decided once at the end of the frame: if the player and the last enemy die in the same frame, it is a win (to be decided).</summary>
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

        // ───── Unified cleanup of projectiles and special states ─────

        public void Register(MonoBehaviour t)
        {
            if (t && !transients.Contains(t)) transients.Add(t);
        }

        public void Unregister(MonoBehaviour t) => transients.Remove(t);

        /// <summary>On wave change or player death: remove all flying knives, corpses and missiles, and end hooks and dashes.</summary>
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

        // ───── Materials ─────

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
