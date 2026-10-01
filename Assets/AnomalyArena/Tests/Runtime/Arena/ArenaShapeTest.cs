using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture]
    public class ArenaShapeTest
    {
        [Test]
        public void Contains_Center_ReturnsTrue()
        {
            var actual = ArenaShape.Contains(Vector2.zero);

            Assert.That(actual, Is.True);
        }

        [Test]
        public void Contains_PointBeyondTheIrregularEdge_ReturnsFalse()
        {
            // Between the bottom-left V12 (-16, -11) and V0 (-11, -15) the corner is chamfered: (-15, -14), inside the square, is outside the new shape
            var actual = ArenaShape.Contains(new Vector2(-15f, -14f));

            Assert.That(actual, Is.False);
        }

        [Test]
        public void ClampInside_PointOutside_ReturnsPointInsideWithMargin()
        {
            var actual = ArenaShape.ClampInside(new Vector3(40f, 0f, 3f), 1f);

            Assert.That(ArenaShape.ContainsWithMargin(actual, 0.99f), Is.True);
        }

        [Test]
        public void RandomInside_WithMargin_ReturnsPointInsideWithMargin()
        {
            var actual = ArenaShape.RandomInside(2.5f);

            Assert.That(ArenaShape.ContainsWithMargin(actual, 2.5f), Is.True);
        }

        [Test]
        public void GapRange_SmallGap_IsCenteredOnItsEdge()
        {
            var gap = ArenaShape.Gaps[4];
            float half = ArenaShape.EdgeLength(gap.edge) * 0.5f;

            var (from, to) = ArenaShape.GapRange(gap);

            Assert.That((from + to) * 0.5f, Is.EqualTo(half).Within(1e-4f));
        }

        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Timeout(10000)]
        public async Task SmallGapWidth_BetweenSmallAndLargeEnemyDiameters_LetsOnlySmallOnesFall()
        {
            await Awaitable.NextFrameAsync();
            var waves = GameManager.Instance.waves;

            var actual = ArenaShape.SmallGapWidth;

            Assert.That(actual, Is.GreaterThan(waves.smallPrefab.radius * 2f).And.LessThan(waves.largePrefab.radius * 2f));
        }
    }
}
