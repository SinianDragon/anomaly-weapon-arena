using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture]
    public class QueryTest
    {
        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(10000)]
        public async Task MeleeTarget_TwoEnemiesInPunchReach_ReturnsNearest()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();
            var p = gm.player;
            var near = ArenaTestUtils.CreateEnemy(p.Position + new Vector3(-0.2f, 0f, 1.3f), "EnemySmall(Near)");
            ArenaTestUtils.CreateEnemy(p.Position + new Vector3(0.6f, 0f, 1.6f), "EnemySmall(Far)");
            await Awaitable.FixedUpdateAsync();

            var actual = Query.MeleeTarget(p, p.Position, Vector3.forward, p.radius + p.punchRange, p.punchArc);

            Assert.That(actual, Is.SameAs(near));
        }

        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(10000)]
        public async Task InSector_EnemyBehindPlayer_ExcludesIt()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();
            var p = gm.player;
            ArenaTestUtils.CreateEnemy(p.Position + new Vector3(0f, 0f, -2f), "EnemySmall(Behind)");
            await Awaitable.FixedUpdateAsync();

            var actual = Query.InSector(p, p.Position, Vector3.forward, 5f, 150f);

            Assert.That(actual, Is.Empty);
        }
    }
}
