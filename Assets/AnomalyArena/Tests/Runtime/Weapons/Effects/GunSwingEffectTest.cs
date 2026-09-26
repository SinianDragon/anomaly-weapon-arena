using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture]
    public class GunSwingEffectTest
    {
        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(10000)]
        public async Task Swing_FullChargeWithTwoEnemiesInArc_DamagesBoth()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();
            var near = ArenaTestUtils.CreateEnemy(new Vector3(0f, 0f, 3f), "EnemySmall(Near)");
            var far = ArenaTestUtils.CreateEnemy(new Vector3(1.5f, 0f, 4f), "EnemySmall(Far)");
            var sut = (GunSwingEffect)gm.weapons.GetEffect(EffectId.GunSwing);
            await Awaitable.FixedUpdateAsync();

            sut.Swing(gm.player, 1f);

            Assert.That(new[] { near, far }, Has.All.Matches<Enemy>(e => e.Hp < e.maxHp));
        }

        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(10000)]
        public async Task Swing_TapWithEnemyBeyondTapReach_MissesIt()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();
            var enemy = ArenaTestUtils.CreateEnemy(new Vector3(0f, 0f, 4.5f), "EnemySmall(Far)");
            var sut = (GunSwingEffect)gm.weapons.GetEffect(EffectId.GunSwing);
            await Awaitable.FixedUpdateAsync();

            sut.Swing(gm.player, 0f);

            Assert.That(enemy.Hp, Is.EqualTo(enemy.maxHp));
        }
    }
}
