using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AnomalyArena
{
    /// <summary>地上的武器：每一波开始时补几把（第 1 波在开局放）、上限、拾取查找。</summary>
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
            [Range(0f, 1f)] [Tooltip("抽到 effectA 的概率")] public float chanceA = 0.5f;
            [Tooltip("调试：新生成的这类武器全部用这个效果")] public EffectId forceOnSpawn = EffectId.None;
        }

        public WeaponDef[] defs;
        [Tooltip("每一波开始时补几把：第 1 波（开局）、第 2 波、第 3 波……波数多于数组时用最后一个")]
        public int[] perWaveCounts = { 3, 5, 8 };
        [Tooltip("地上最多同时有几把；满了就不再补")] public int groundCap = 12;
        public float pickupRadius = 1f;
        [Tooltip("击败大型敌人时掉落一把随机武器的概率")] [Range(0f, 1f)] public float largeDropChance = 0.5f;
        [Tooltip("大型敌人的掉落不受地上武器上限限制")] public bool dropIgnoresCap = true;

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

        /// <summary>大型敌人被击败时按概率掉落任意类型的武器（效果照常随机）。</summary>
        public void TryLargeDrop(Vector3 pos)
        {
            if (Random.value >= largeDropChance) return;
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

        /// <summary>换下的武器留在地上，保留效果、揭晓状态和次数。</summary>
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
