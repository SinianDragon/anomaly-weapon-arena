using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture]
    public class WeaponSpawnerTest
    {
        [TestCase(0, 3)]
        [TestCase(1, 4)]
        [TestCase(2, 5)]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Timeout(10000)]
        public async Task CountForWave_ArenaSceneSupply_ReturnsDesignedCount(int waveIndex, int expected)
        {
            await Awaitable.NextFrameAsync();
            var sut = GameManager.Instance.weapons;

            var actual = sut.CountForWave(waveIndex);

            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}
