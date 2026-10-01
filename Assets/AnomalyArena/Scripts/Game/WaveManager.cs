using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AnomalyArena
{
    /// <summary>
    /// Waves: a fixed number of small / large enemies per wave, at most maxAlive alive at once, one spawns as one dies.
    /// A red X appears on the ground 1 second before a spawn, at least minSpawnDistance from the player; if the player stands on the X, the enemy spawns elsewhere.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class WaveManager : MonoBehaviour
    {
        [Serializable]
        public class Wave
        {
            public int small;
            public int large;
        }

        public Wave[] waves =
        {
            new Wave { small = 4, large = 1 },
            new Wave { small = 8, large = 2 },
            new Wave { small = 15, large = 5 },
        };
        [Tooltip("Maximum number of enemies alive at once (to be decided)")] public int maxAlive = 12;
        public float spawnTelegraph = 1f;
        public float minSpawnDistance = 6f;
        [Tooltip("Interval between consecutive X markers")] public float spawnInterval = 0.25f;
        public float betweenWaveDelay = 2.5f;
        [Tooltip("Refill the player's HP before each wave")] public bool refillHpEachWave = true;
        [Tooltip("When not refilling, HP restored between waves (capped at max)")] public float waveHeal = 20f;

        public Enemy smallPrefab;
        public Enemy largePrefab;

        public int WaveIndex { get; private set; } = -1;
        public int WaveCount => waves.Length;
        public bool BetweenWaves { get; private set; }
        public float BetweenTimer { get; private set; }
        public int Remaining => queue.Count + markers.Count + alive.Count;
        public IReadOnlyList<Enemy> Alive => alive;
        public int Kills { get; private set; }

        readonly List<bool> queue = new List<bool>(); // true = large
        readonly List<SpawnMarker> markers = new List<SpawnMarker>();
        readonly List<Enemy> alive = new List<Enemy>();
        float spawnTimer;
        bool running;

        static GameManager GM => GameManager.Instance;

        public void StartWaves()
        {
            running = true;
            BeginWave(0);
        }

        void BeginWave(int i)
        {
            WaveIndex = i;
            BetweenWaves = false;
            queue.Clear();
            for (int k = 0; k < waves[i].small; k++) queue.Add(false);
            for (int k = 0; k < waves[i].large; k++) queue.Add(true);
            for (int k = queue.Count - 1; k > 0; k--)
            {
                int j = Random.Range(0, k + 1);
                (queue[k], queue[j]) = (queue[j], queue[k]);
            }
            spawnTimer = 0.5f;
            GM.hud.Banner($"Wave {i + 1}", i == waves.Length - 1 ? "Final wave!" : $"{queue.Count} enemies incoming");
        }

        void Update()
        {
            if (!running || GM.State != GameState.Playing) return;
            float dt = Time.deltaTime;

            if (BetweenWaves)
            {
                BetweenTimer -= dt;
                if (BetweenTimer <= 0f) BeginWave(WaveIndex + 1);
                return;
            }

            // Place an X: alive + pending spawns must not exceed the cap
            spawnTimer -= dt;
            if (queue.Count > 0 && alive.Count + markers.Count < maxAlive && spawnTimer <= 0f)
            {
                bool large = queue[queue.Count - 1];
                queue.RemoveAt(queue.Count - 1);
                markers.Add(SpawnMarker.Create(FindSpawnPoint(large), large, spawnTelegraph, GM.spawnMarkerMaterial));
                spawnTimer = spawnInterval;
            }

            TickMarkers(dt);

            if (queue.Count == 0 && markers.Count == 0 && alive.Count == 0) OnWaveCleared();
        }

        void TickMarkers(float dt)
        {
            var p = GM.player;
            for (int i = markers.Count - 1; i >= 0; i--)
            {
                var m = markers[i];
                float r = m.large ? largePrefab.radius : smallPrefab.radius;
                // The player stood on the X: the enemy spawns elsewhere
                if (p.IsAlive && Query.Flat(p.Position - m.Position).magnitude < r + p.Radius)
                {
                    m.MoveTo(FindSpawnPoint(m.large));
                    m.timer = spawnTelegraph;
                    continue;
                }
                m.timer -= dt;
                if (m.timer > 0f) continue;
                markers.RemoveAt(i);
                var e = Instantiate(m.large ? largePrefab : smallPrefab, new Vector3(m.Position.x, 0f, m.Position.z), Quaternion.identity);
                Destroy(m.gameObject);
                e.Eliminated += OnEnemyEliminated;
                alive.Add(e);
            }
        }

        void OnEnemyEliminated(Combatant c)
        {
            var e = c as Enemy;
            alive.Remove(e);
            Kills++;
            if (e != null && e.IsLarge) GM.weapons.TryLargeDrop(e.Position);
        }

        void OnWaveCleared()
        {
            if (WaveIndex >= waves.Length - 1)
            {
                running = false;
                GM.NotifyFinalWaveCleared();
                return;
            }
            // Changing waves does not reset the field: bullets / knives / missiles in flight, hooks and dashes continue, and the player keeps moving
            float before = GM.player.Hp;
            GM.player.Heal(refillHpEachWave ? GM.player.maxHp : waveHeal);
            GM.weapons.SpawnForWave(WaveIndex + 1); // supply for the next wave (see WeaponSpawner.perWaveCounts)
            BetweenWaves = true;
            BetweenTimer = betweenWaveDelay;
            GM.hud.Banner($"Wave {WaveIndex + 1} cleared!", refillHpEachWave ? "HP fully restored" : $"HP +{Mathf.RoundToInt(GM.player.Hp - before)}");
        }

        Vector3 FindSpawnPoint(bool large)
        {
            float r = large ? largePrefab.radius : smallPrefab.radius;
            var p = GM.player;
            Vector3 best = Vector3.zero;
            float bestScore = -1f;
            for (int i = 0; i < 50; i++)
            {
                var pt = ArenaShape.RandomInside(r + 0.5f);
                float dp = Query.Flat(pt - p.Position).magnitude;
                float dOther = 99f;
                foreach (var m in markers) dOther = Mathf.Min(dOther, Query.Flat(m.Position - pt).magnitude - r);
                foreach (var e in alive) if (e) dOther = Mathf.Min(dOther, Query.Flat(e.Position - pt).magnitude - r - e.radius);
                if (dp >= minSpawnDistance && dOther > 0.5f) return pt;
                float score = Mathf.Min(dp, minSpawnDistance) + Mathf.Min(dOther, 0.5f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = pt;
                }
            }
            return best;
        }
    }
}
