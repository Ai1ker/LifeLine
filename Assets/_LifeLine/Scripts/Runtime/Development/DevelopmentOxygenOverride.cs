#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Lifeline
{
    public partial class GameSession
    {
        // Not part of SessionSaveState. Removing this folder erases the optional partial hook.
        [System.NonSerialized] public bool developerInfiniteOxygen;

        partial void OverrideOxygenDrain(CrewActor actor, ref float drain)
        {
            if (!developerInfiniteOxygen) return;
            actor.oxygen = actor.oxygenMax;
            drain = 0;
        }
    }
}
#endif
