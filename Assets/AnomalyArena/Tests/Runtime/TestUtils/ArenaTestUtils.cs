using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    // 场景内集成测试的共用准备：关掉波次、清空地上武器，只留下测试自己放的东西
    internal static class ArenaTestUtils
    {
        public const string ArenaScene = "Assets/AnomalyArena/Scenes/Arena.unity";

        // 测试敌人的血量：足够多，任何一击都打不死，方便用“扣没扣血”判断打没打到
        public const float SturdyHp = 999f;

        public static async Awaitable<GameManager> StartSandboxAsync()
        {
            // 等一帧让 GameManager.Start（开局放武器）先跑完，再清场
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

        // 不会移动、不会主动攻击、血量很厚的敌人
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

        // 给玩家一把指定效果的武器（揭晓前就定好效果）
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

        // 等到条件成立；配合测试方法上的 [Timeout] 使用，条件永远不成立时由超时让测试失败
        public static async Awaitable WaitUntilAsync(Func<bool> condition)
        {
            while (!condition()) await Awaitable.NextFrameAsync();
        }
    }
}
