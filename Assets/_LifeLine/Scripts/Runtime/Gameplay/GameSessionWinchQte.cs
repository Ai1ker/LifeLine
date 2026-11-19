using UnityEngine;

namespace Lifeline
{
    public partial class GameSession
    {
        [Header("设备效率 / repaired sea-water filter")]
        [Min(1)] public float filterInteractionMultiplier = 1.2f;
        [Header("绞盘动态目标 / runtime QTE state")]
        [Range(0, 1)] public float winchTargetCenter = .69f;
        public bool winchCenterInitialized;
        [Min(0)] public float winchCueCooldown;
        [Min(.05f)] public float winchMinimumInputInterval = .18f;

        public float DeckInteractionEfficiency => filterRepaired ? filterInteractionMultiplier : 1;
        public float GetDeviceInteractionEfficiency(CrewActor actor) => actor != null && actor.onboard && !IsIncapacitated(actor) ? DeckInteractionEfficiency : 1;
        public float GetGeneratorStrokeInterval(CrewActor actor = null) => Mathf.Max(.1f, tuning.generatorStrokeInterval / GetDeviceInteractionEfficiency(actor));
        public float WinchQteCenter
        {
            get { if (!winchCenterInitialized) ResetWinchTarget(); return winchTargetCenter; }
        }
        public static float CircularQteDistance(float a, float b) => Mathf.Abs(Mathf.DeltaAngle(a * 360, b * 360)) / 360;

        public void ResetWinchTarget()
        {
            winchTargetCenter = tuning != null ? Mathf.Repeat(tuning.winchCenter, 1) : .69f;
            winchCenterInitialized = true; winchCueCooldown = 0;
            RefreshWinchTarget();
        }
        void RefreshWinchTarget()
        {
            float next = Random.value;
            // Only an accepted player result moves the window; idle revolutions leave it in place.
            if (CircularQteDistance(next, winchTargetCenter) < .15f) next = Mathf.Repeat(winchTargetCenter + Random.Range(.2f, .8f), 1);
            winchTargetCenter = next; winchCenterInitialized = true;
        }
        internal float WinchInteractionSpeed
        {
            get
            {
                CrewActor owner = winchOperatorRole == CrewRole.Captain ? captain : diver;
                return winchActive && owner != null && owner.controlsCaptured ? GetDeviceInteractionEfficiency(owner) : 1;
            }
        }
    }
}
