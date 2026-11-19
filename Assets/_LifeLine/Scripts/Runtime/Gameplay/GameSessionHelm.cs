namespace Lifeline
{
    public partial class GameSession
    {
        public const string EngineUnavailableMessage = "引擎损坏，修复后才能航行。";

        /// <summary>Both crew share the same helm gate and feedback; zero power still allows taking the helm.</summary>
        public bool TryInteractHelm(CrewActor actor)
        {
            if (phase != SessionPhase.Playing || actor == null || actor.session != this || !actor.onboard
                || IsIncapacitated(actor) || actor.controlsCaptured || ship == null) return false;
            // Engine damage is the actionable blocker even if a previous operator still owns
            // the wheel. Release that stale operation so no player remains stuck at the helm.
            if (!engineRepaired)
            {
                ship.SetHelm(null);
                Tell(EngineUnavailableMessage);
                return false;
            }
            if (ship.helmOperator != null && ship.helmOperator != actor)
            {
                Tell("同伴正在掌舵，先请对方离开船舵。");
                return false;
            }
            ship.SetHelm(actor);
            return true;
        }
    }
}
