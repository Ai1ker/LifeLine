using System;
using UnityEngine;

namespace Lifeline
{
    public partial class GameSession
    {
        public event Action<CrewActor> OperationInterrupted;
        public Vector2 LightningInterval => new Vector2(
            Mathf.Max(tuning.lightningMinFloor, tuning.lightningIntervalMin - Mathf.Max(0, day - 2) * tuning.lightningIntervalDayReduction),
            Mathf.Max(tuning.lightningMaxFloor, tuning.lightningIntervalMax - Mathf.Max(0, day - 2) * tuning.lightningIntervalDayReduction));
        public float LightningWarningSeconds => Mathf.Max(tuning.lightningWarningFloor,
            tuning.lightningWarning - Mathf.Max(0, day - 2) * tuning.lightningWarningDayReduction);
        public float BirdInterval => Mathf.Max(tuning.birdMinimumInterval,
            tuning.birdInterval - Mathf.Max(0, day - 3) * tuning.birdIntervalDayReduction);
        // Mutually exclusive extra-one / extra-two outcomes. Day 1 uses the base probabilities.
        public Vector2 BirdExtraChances => new Vector2(
            Mathf.Min(tuning.birdExtraOneCap, tuning.birdExtraOneChance + Mathf.Max(0, day - 1) * tuning.birdExtraDailyChance),
            Mathf.Min(tuning.birdExtraTwoCap, tuning.birdExtraTwoChance + Mathf.Max(0, day - 1) * tuning.birdExtraDailyChance));
        public int BirdWaveCount(float roll) => roll < BirdExtraChances.y ? 3 : roll < BirdExtraChances.x + BirdExtraChances.y ? 2 : 1;
        public void SpawnBirdWave(float roll)
        {
            int count = BirdWaveCount(roll);
            float side = UnityEngine.Random.value < .5f ? -1 : 1;
            for (int i = 0; i < count; i++)
                SpawnEnemy(day >= tuning.mutationStartDay && UnityEngine.Random.value < tuning.armoredBirdChance ? EnemyKind.ArmoredBird : EnemyKind.Bird,
                    ShipPoint(new Vector3((i % 2 == 0 ? side : -side) * (16 + i * 2), 6 + i * 1.5f, 0)));
        }
        public void InterruptOperation(CrewActor actor)
        {
            if (actor == null || IsIncapacitated(actor) || !actor.onboard) return;
            OperationInterrupted?.Invoke(actor);
            actor.controlsCaptured = false;
            if (ship != null && ship.helmOperator == actor) ship.SetHelm(null);
            if (winchOperatorRole == actor.role && winchActive) SetWinchActive(false);
            reviveHeld[(int)actor.role] = false;
            reviveProgress[(int)actor.role] = 0; reviveTargets[(int)actor.role] = null;
            // Automatic jobs retain their materials and progress; only the player's operation stops.
        }
    }
}
