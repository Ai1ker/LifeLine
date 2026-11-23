using NUnit.Framework;
using UnityEngine;

namespace Lifeline.Tests
{
    public sealed class DaylightClockTests
    {
        [Test]
        public void NewCycleChangesStageAtTheConfiguredSecond()
        {
            var tuning = ScriptableObject.CreateInstance<GameTuning>();
            var root = new GameObject("Clock fixture");
            root.SetActive(false);
            try
            {
                var game = root.AddComponent<GameSession>();
                game.tuning = tuning;
                game.elapsed = 179.99f;
                Assert.That(game.IsDusk || game.IsNight, Is.False);
                game.elapsed = 180f;
                Assert.That(game.IsDusk, Is.True);
                game.elapsed = 224.99f;
                Assert.That(game.IsDusk, Is.True);
                game.elapsed = 225f;
                Assert.That(game.IsNight, Is.True);
                game.elapsed = 279.99f;
                Assert.That(game.IsNight, Is.True);
                game.elapsed = 280f;
                Assert.That(game.IsDusk || game.IsNight, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tuning);
            }
        }

        [TestCase(0, 0, 0, 60.3f, 90f)]
        [TestCase(0, 0, 0, 132.3f, 202.5f)]
        [TestCase(0, 0, 0, 162f, 252.5f)]
        [TestCase(200, 140.6f, 23.4f, 70.3f, 90f)]
        [TestCase(200, 140.6f, 23.4f, 152.3f, 202.5f)]
        [TestCase(200, 140.6f, 23.4f, 182f, 252.5f)]
        public void LegacySaveKeepsDayAndItsPositionWithinTheSameTimePeriod(float cycle, float daylight, float dusk, float local, float expected)
        {
            var tuning = ScriptableObject.CreateInstance<GameTuning>();
            try
            {
                var data = new GameSaveData
                {
                    cycleSeconds = cycle, daylightSeconds = daylight, duskSeconds = dusk,
                    state = new SessionSaveState { day = 2, elapsed = (cycle > 0 ? cycle : 180f) + local }
                };
                Assert.That(SaveClock.TryMap(data, tuning, out float elapsed));
                Assert.That(elapsed, Is.EqualTo(280 + expected).Within(.002f));
                Assert.That(tuning.daySeconds * tuning.dayFraction, Is.EqualTo(180f).Within(.001f));
                Assert.That(tuning.daySeconds * tuning.duskFraction, Is.EqualTo(45f).Within(.001f));
                Assert.That(tuning.daySeconds * (1 - tuning.dayFraction - tuning.duskFraction), Is.EqualTo(55f).Within(.001f));
            }
            finally { Object.DestroyImmediate(tuning); }
        }

        [Test] public void NewSaveClockRoundTripsAndInvalidDayIsRejected()
        {
            var tuning = ScriptableObject.CreateInstance<GameTuning>();
            try
            {
                var data = new GameSaveData { cycleSeconds = 280, daylightSeconds = 180f, duskSeconds = 45f,
                    state = new SessionSaveState { day = 3, elapsed = 799 } };
                Assert.That(SaveClock.TryMap(data, tuning, out float elapsed));
                Assert.That(elapsed, Is.EqualTo(799));
                data.state.day = 4; Assert.That(SaveClock.TryMap(data, tuning, out elapsed), Is.False);
            }
            finally { Object.DestroyImmediate(tuning); }
        }
    }
}
