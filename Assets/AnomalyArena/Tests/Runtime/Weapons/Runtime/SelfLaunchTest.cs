using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture]
    public class SelfLaunchTest
    {
        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(15000)]
        public async Task DashIntoWall_StopsTakesWallDamageAndBouncesBack()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();
            var p = gm.player;
            // 从原点朝 -x 冲：左墙在 x ≈ -15.1 处，15 格一定撞得到；这一段没有缺口（西侧小缺口在 z -1.2 到 -2.8）
            p.DebugSetAim(Vector3.left);
            ArenaTestUtils.GiveWeapon(p, WeaponType.Missile, EffectId.MissileSelfLaunch);
            float hpBefore = p.Hp;
            await Awaitable.FixedUpdateAsync();

            p.TryUseWeapon();
            await ArenaTestUtils.WaitUntilAsync(() => p.State != CharacterState.Dashing);

            Assert.That(p.Hp, Is.EqualTo(hpBefore - gm.rules.wallDamage), "wall damage");
            Assert.That(p.State, Is.EqualTo(CharacterState.Knocked), "bounced back");
            Assert.That(ArenaShape.ContainsWithMargin(p.Position, p.radius - 0.05f), Is.True,
                "stopped inside the arena");
        }
    }
}