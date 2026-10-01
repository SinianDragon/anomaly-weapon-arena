using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AnomalyArena
{
    /// <summary>Weapons on the ground: how many are added at the start of each wave (wave 1 at game start), the cap, and pickup lookup.</summary>
    public class WeaponSpawner : MonoBehaviour
    {
        [Serializable]
        public class WeaponDef
        {
            public WeaponType type;
            public Weapon prefab;
            public int uses = 6;
            public WeaponEffect effectA;
            public WeaponEffect effectB;
            [Range(0f, 1f)] [Tooltip("Chance of rolling effectA")] public float chanceA = 0.5f;
            [Tooltip("Debug: every newly spawned weapon of this type uses this effect")] public EffectId forceOnSpawn = EffectId.None;
        }

        public WeaponDef[] defs;
        [Tooltip("Weapons added at the start of each wave: wave 1 (game start), wave 2, wave 3...; if there are more waves than entries, the last entry is used")]
        public int[] perWaveCounts = { 3, 4, 5  };
        [Tooltip("Maximum weapons on the ground at once; no more are added when full")] public int groundCap = 6;
        public float pickupRadius = 1f;
        [Tooltip("Chance that defeating a large enemy drops a random weapon")] [Range(0f, 1f)] public float largeDropChance = 0.5f;
        [Tooltip("Large-enemy drops ignore the ground weapon cap")] public bool dropIgnoresCap = true;

        public readonly List<Weapon> ground = new List<Weapon>();

        public void SpawnInitial() => SpawnForWave(0);

        public int CountForWave(int waveIndex) =>
            perWaveCounts == null || perWaveCounts.Length == 0 ? 0 : perWaveCounts[Mathf.Clamp(waveIndex, 0, perWaveCounts.Length - 1)];

        public void SpawnForWave(int waveIndex) => SpawnRandom(CountForWave(waveIndex));

        public void SpawnRandom(int count)
        {
            for (int i = 0; i < count && ground.Count < groundCap; i++)
            {
                var def = defs[Random.Range(0, defs.Length)];
                Spawn(def, RandomPosition());
            }
        }

        /// <summary>When a large enemy is defeated, drops a weapon of any type by chance (its effect is rolled as usual).</summary>
        public void TryLargeDrop(Vector3 pos)
        {
            if (Random.value < largeDropChance) return;
            if (!dropIgnoresCap && ground.Count >= groundCap) return;
            pos = ArenaShape.ClampInside(pos, 1.5f);
            var w = Spawn(defs[Random.Range(0, defs.Length)], pos);
            Fx.Pop(pos + Vector3.up * 0.6f, new Color(0.85f, 0.65f, 0.15f), 2f);
            GameManager.Instance.hud.Toast("The big one dropped: " + w.Label, new Color(1f, 0.85f, 0.4f));
        }

        public Weapon Spawn(WeaponDef def, Vector3 pos)
        {
            var w = Instantiate(def.prefab);
            w.name = def.type.ToString();
            WeaponEffect e = Random.value < def.chanceA ? def.effectA : def.effectB;
            if (def.forceOnSpawn != EffectId.None)
            {
                var forced = GetEffect(def.forceOnSpawn);
                if (forced != null && forced.type == def.type) e = forced;
            }
            w.Init(e, def.uses);
            w.PlaceOnGround(pos);
            ground.Add(w);
            return w;
        }

        /// <summary>The swapped-out weapon stays on the ground and keeps its effect, reveal state and uses.</summary>
        public void Drop(Weapon w, Vector3 pos)
        {
            w.PlaceOnGround(ArenaShape.ClampInside(pos, 1f));
            ground.Add(w);
        }

        public void Take(Weapon w) => ground.Remove(w);

        public Weapon FindNear(Vector3 pos)
        {
            Weapon best = null;
            float bestSq = pickupRadius * pickupRadius;
            foreach (var w in ground)
            {
                if (!w) continue;
                float sq = Query.Flat(w.transform.position - pos).sqrMagnitude;
                if (sq <= bestSq)
                {
                    bestSq = sq;
                    best = w;
                }
            }
            return best;
        }

        public WeaponEffect GetEffect(EffectId id)
        {
            foreach (var d in defs)
            {
                if (d.effectA && d.effectA.id == id) return d.effectA;
                if (d.effectB && d.effectB.id == id) return d.effectB;
            }
            return null;
        }

        public WeaponDef GetDef(WeaponType t) => Array.Find(defs, d => d.type == t);

        Vector3 RandomPosition()
        {
            var player = GameManager.Instance.player;
            Vector3 best = Vector3.zero;
            float bestScore = -1f;
            for (int i = 0; i < 40; i++)
            {
                var p = ArenaShape.RandomInside(2.5f);
                float d = player ? Query.Flat(p - player.Position).magnitude : 99f;
                foreach (var w in ground) if (w) d = Mathf.Min(d, Query.Flat(w.transform.position - p).magnitude);
                if (d >= 4f) return p;
                if (d > bestScore)
                {
                    bestScore = d;
                    best = p;
                }
            }
            return best;
        }
    }
}
