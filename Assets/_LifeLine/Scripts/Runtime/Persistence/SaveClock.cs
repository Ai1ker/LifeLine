using UnityEngine;

namespace Lifeline
{
    public static class SaveClock
    {
        // Old saves used a 180s cycle and did not carry their clock configuration.
        public static bool TryMap(GameSaveData data, GameTuning tuning, out float elapsed)
        {
            float cycle = data.cycleSeconds > 0 ? data.cycleSeconds : 180f;
            elapsed = data.state.elapsed;
            if (data.state.day != 1 + Mathf.FloorToInt(elapsed / cycle)) return false;
            if (Mathf.Approximately(cycle, tuning.daySeconds)) return true;
            float daylight = data.cycleSeconds > 0 ? data.daylightSeconds : 120.6f;
            float dusk = data.cycleSeconds > 0 ? data.duskSeconds : 23.4f;
            float night = cycle - daylight - dusk;
            if (daylight <= 0 || dusk <= 0 || night <= 0) return false;
            float local = Mathf.Repeat(elapsed, cycle);
            float newDay = tuning.daySeconds * tuning.dayFraction, newDusk = tuning.daySeconds * tuning.duskFraction;
            float mapped = local < daylight ? local / daylight * newDay
                : local < daylight + dusk ? newDay + (local - daylight) / dusk * newDusk
                : newDay + newDusk + (local - daylight - dusk) / night * (tuning.daySeconds - newDay - newDusk);
            elapsed = (data.state.day - 1) * tuning.daySeconds + Mathf.Min(mapped, tuning.daySeconds - .0001f);
            return true;
        }
    }
}
