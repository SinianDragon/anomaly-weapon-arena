using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    // Shared setup for in-scene integration tests: turn off waves and clear ground weapons, leaving only what the test places itself
    internal static class ArenaTestUtils
    {
        public const string ArenaScene = "Assets/AnomalyArena/Scenes/Arena.unity";

        // HP of test enemies: high enough that no single hit kills, so 'did HP drop' tells whether a hit landed
        public const float SturdyHp = 999f;

        public static async Awaitable<GameManager> StartSandboxAsync()
        {
            // Wait one frame so GameManager.Start (initial weapon spawn) runs first, then clear the field
            await Awaitable.NextFrameAsync();
            var gm = GameManager.Instance;
            gm.waves.enabled = false;
            gm.BeginPlaying();
            foreach (var w in gm.weapons.ground.ToArray())
            {
                gm.weapons.Take(w);
                Object.Destroy(w.gameObject);
            }
            gm.player.DebugSetAim(Vector3.forward);
            return gm;
        }

        // An enemy that does not move or attack and has a lot of HP
        public static Enemy CreateEnemy(Vector3 position, string name, bool large = false)
        {
            var gm = GameManager.Instance;
            var enemy = Object.Instantiate(large ? gm.waves.largePrefab : gm.waves.smallPrefab, position, Quaternion.identity);
            enemy.name = name;
            enemy.moveSpeed = 0f;
            enemy.attackInterval = 9999f;
            enemy.maxHp = SturdyHp;
            enemy.Heal(SturdyHp);
            return enemy;
        }

        // Gives the player a weapon with a chosen effect (decided before reveal)
        public static Weapon GiveWeapon(PlayerController player, WeaponType type, EffectId effect)
        {
            var spawner = GameManager.Instance.weapons;
            var def = spawner.GetDef(type);
            var weapon = spawner.Spawn(def, new Vector3(20f, 0f, 20f));
            weapon.name = $"{type}({effect})";
            weapon.Init(spawner.GetEffect(effect), def.uses);
            spawner.Take(weapon);
            player.Equip(weapon);
            return weapon;
        }

        // Waits until the condition holds; used with [Timeout] on the test method, which fails the test if the condition never holds
        public static async Awaitable WaitUntilAsync(Func<bool> condition)
        {
            while (!condition()) await Awaitable.NextFrameAsync();
        }
    }
}
