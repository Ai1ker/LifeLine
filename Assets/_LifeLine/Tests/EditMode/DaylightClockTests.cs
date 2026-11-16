using NUnit.Framework;
using UnityEngine;

namespace Lifeline.Tests
{
    public sealed class DaylightClockTests
    {
        [TestCase(60.3f, 70.3f)]
        [TestCase(132.3f, 152.3f)]
        [TestCase(162f, 182f)]
        public void LegacySaveKeepsDayAndItsPositionWithinTheSameTimePeriod(float local, float expected)
        {
            var tuning = ScriptableObject.CreateInstance<GameTuning>();
            try
            {
                var data = new GameSaveData { state = new SessionSaveState { day = 2, elapsed = 180 + local } };
                Assert.That(SaveClock.TryMap(data, tuning, out float elapsed));
                Assert.That(elapsed, Is.EqualTo(200 + expected).Within(.002f));
                Assert.That(tuning.daySeconds * tuning.dayFraction, Is.EqualTo(140.6f).Within(.001f));
                Assert.That(tuning.daySeconds * tuning.duskFraction, Is.EqualTo(23.4f).Within(.001f));
                Assert.That(tuning.daySeconds * (1 - tuning.dayFraction - tuning.duskFraction), Is.EqualTo(36).Within(.001f));
            }
            finally { Object.DestroyImmediate(tuning); }
        }

        [Test] public void NewSaveClockRoundTripsAndInvalidDayIsRejected()
        {
            var tuning = ScriptableObject.CreateInstance<GameTuning>();
            try
            {
                var data = new GameSaveData { cycleSeconds = 200, daylightSeconds = 140.6f, duskSeconds = 23.4f,
                    state = new SessionSaveState { day = 3, elapsed = 561 } };
                Assert.That(SaveClock.TryMap(data, tuning, out float elapsed));
                Assert.That(elapsed, Is.EqualTo(561));
                data.state.day = 4; Assert.That(SaveClock.TryMap(data, tuning, out elapsed), Is.False);
            }
            finally { Object.DestroyImmediate(tuning); }
        }
    }
}
