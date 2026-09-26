using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture]
    public class CombatantTest
    {
        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(10000)]
        public async Task TryUseWeapon_ReverseShotFirstShot_ConsumesOneUseAndLoadsClip()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();
            var gun = ArenaTestUtils.GiveWeapon(gm.player, WeaponType.Gun, EffectId.GunReverseShot);
            int usesBefore = gun.UsesLeft;
            int clip = gun.Effect.roundsPerUse;
            await Awaitable.FixedUpdateAsync();

            gm.player.TryUseWeapon();

            Assert.That(gun.UsesLeft, Is.EqualTo(usesBefore - 1), "uses");
            Assert.That(gun.RoundsLeft, Is.EqualTo(clip - 1), "rounds left in clip");
        }

        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(10000)]
        public async Task TryUseWeapon_ReverseShotRecoil_KeepsShooterInNormalState()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();
            ArenaTestUtils.GiveWeapon(gm.player, WeaponType.Gun, EffectId.GunReverseShot);
            await Awaitable.FixedUpdateAsync();

            gm.player.TryUseWeapon();
            await Awaitable.FixedUpdateAsync();

            Assert.That(gm.player.State, Is.EqualTo(CharacterState.Normal));
        }
    }
}
