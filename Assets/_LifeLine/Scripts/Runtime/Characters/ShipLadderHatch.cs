using UnityEngine;

namespace Lifeline
{
    /// <summary>Solid while walking; opens only for a crew member deliberately using the ladder.</summary>
    public class ShipLadderHatch : MonoBehaviour
    {
        public Collider hatchCollider;
        public CrewActor[] crew;
        private void FixedUpdate()
        {
            if (hatchCollider == null) return;
            bool climbing = false;
            if (crew != null) foreach (CrewActor actor in crew) if (actor != null && actor.onboard && actor.IsClimbing) climbing = true;
            hatchCollider.enabled = !climbing;
        }
        private void OnDisable() { if (hatchCollider != null) hatchCollider.enabled = true; }
    }
}
