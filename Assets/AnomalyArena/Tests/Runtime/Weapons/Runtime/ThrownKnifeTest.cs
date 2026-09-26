using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture]
    public class ThrownKnifeTest
    {
        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(15000)]
        public async Task FiveEnemiesInBounceRange_HitsFourThenReturns()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();
            var enemies = new[]
            {
                ArenaTestUtils.CreateEnemy(new Vector3(0f, 0f, 4f), "EnemySmall(0)"),
                ArenaTestUtils.CreateEnemy(new Vector3(2f, 0f, 6f), "EnemySmall(1)"),
                ArenaTestUtils.CreateEnemy(new Vector3(4f, 0f, 8f), "EnemySmall(2)"),
                ArenaTestUtils.CreateEnemy(new Vector3(6f, 0f, 10f), "EnemySmall(3)"),
                ArenaTestUtils.CreateEnemy(new Vector3(8f, 0f, 12f), "EnemySmall(4)"),
            };
            var knife = ArenaTestUtils.GiveWeapon(gm.player, WeaponType.Knife, EffectId.KnifeThrow);
            await Awaitable.FixedUpdateAsync();

            gm.player.TryUseWeapon();
            await ArenaTestUtils.WaitUntilAsync(() => !knife.InFlight);

            Assert.That(enemies, Has.Exactly(4).Matches<Enemy>(e => e.Hp < e.maxHp));
        }

        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(15000)]
        public async Task TryUseWeapon_KnifeStillFlying_ReturnsFalse()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();
            var knife = ArenaTestUtils.GiveWeapon(gm.player, WeaponType.Knife, EffectId.KnifeThrow);
            gm.player.TryUseWeapon();
            var thrown = (ThrownKnife)knife.InFlight;
            // 没打到人时飞满 12 格才返回（约 0.67 秒），此时早已过了 0.3 秒的冷却，挡住再扔的只剩“飞刀还没回来”
            await ArenaTestUtils.WaitUntilAsync(() => thrown.Returning);

            var actual = gm.player.TryUseWeapon();

            Assert.That(actual, Is.False);
        }

        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(15000)]
        public async Task TryUseWeapon_KnifeCameBack_ReturnsTrue()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();
            var knife = ArenaTestUtils.GiveWeapon(gm.player, WeaponType.Knife, EffectId.KnifeThrow);
            gm.player.TryUseWeapon();
            await ArenaTestUtils.WaitUntilAsync(() => !knife.InFlight);

            var actual = gm.player.TryUseWeapon();

            Assert.That(actual, Is.True);
        }
    }
}
