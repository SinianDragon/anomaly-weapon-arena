using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AnomalyArena
{
    /// <summary>地上的武器：开局放几把、每波后补几把、上限、拾取查找。</summary>
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
        public int startCount = 3;
        public int perWave = 2;
        [Tooltip("地上最多同时有几把；满了就不再补")] public int groundCap = 5;
        public float pickupRadius = 1f;

        public readonly List<Weapon> ground = new List<Weapon>();

        public void SpawnInitial() => SpawnRandom(startCount);

        public void SpawnRandom(int count)
        {
            for (int i = 0; i < count && ground.Count < groundCap; i++)
            {
                var def = defs[Random.Range(0, defs.Length)];
                Spawn(def, RandomPosition());
            }
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
            float limit = GameManager.Instance.rules.arenaHalfSize - 1f;
            pos.x = Mathf.Clamp(pos.x, -limit, limit);
            pos.z = Mathf.Clamp(pos.z, -limit, limit);
            w.PlaceOnGround(pos);
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
            float limit = GameManager.Instance.rules.arenaHalfSize - 2.5f;
            var player = GameManager.Instance.player;
            Vector3 best = Vector3.zero;
            float bestScore = -1f;
            for (int i = 0; i < 40; i++)
            {
                var p = new Vector3(Random.Range(-limit, limit), 0f, Random.Range(-limit, limit));
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
