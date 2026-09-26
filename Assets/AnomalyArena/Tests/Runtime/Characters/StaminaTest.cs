using NUnit.Framework;

namespace AnomalyArena
{
    [TestFixture]
    public class StaminaTest
    {
        private static Stamina CreateSystemUnderTest() => new Stamina
        {
            sprintDuration = 1f,
            recoverDuration = 3f,
            sprintMultiplier = 2f,
            minToRestart = 0.25f,
        };

        [Test]
        public void Tick_SprintHeld_ReturnsSprintMultiplier()
        {
            var sut = CreateSystemUnderTest();

            var actual = sut.Tick(0.1f, true);

            Assert.That(actual, Is.EqualTo(2f));
        }

        [Test]
        public void Tick_SprintHeldForSprintDuration_EmptiesAndBecomesExhausted()
        {
            var sut = CreateSystemUnderTest();

            sut.Tick(1f, true);

            Assert.That(sut.Value, Is.EqualTo(0f), "value");
            Assert.That(sut.Exhausted, Is.True, "exhausted");
        }

        [Test]
        public void Tick_RestingForRecoverDurationFromEmpty_RefillsCompletely()
        {
            var sut = CreateSystemUnderTest();
            sut.Tick(1f, true);

            sut.Tick(3f, false);

            Assert.That(sut.Value, Is.EqualTo(1f));
        }

        [Test]
        public void Tick_ExhaustedAndRecoveredBelowRestartThreshold_DoesNotSprint()
        {
            var sut = CreateSystemUnderTest();
            sut.Tick(1f, true);
            sut.Tick(0.5f, false);

            var actual = sut.Tick(0.01f, true);

            Assert.That(actual, Is.EqualTo(1f));
        }

        [Test]
        public void Tick_ExhaustedAndRecoveredAboveRestartThreshold_SprintsAgain()
        {
            var sut = CreateSystemUnderTest();
            sut.Tick(1f, true);
            sut.Tick(0.9f, false);

            var actual = sut.Tick(0.01f, true);

            Assert.That(actual, Is.EqualTo(2f));
        }
    }
}
