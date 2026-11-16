using UnityEngine;

namespace Lifeline
{
    public class StationAnchor : MonoBehaviour
    {
        public StationKind kind;
        public string displayName;
        public float interactionRadius = 1.8f;
        public Transform interactionPoint;
        public Transform visualRoot;
        public bool available = true;
        public Vector3 InteractionPosition => interactionPoint != null ? interactionPoint.position : transform.position;
        public bool CanInteract(CrewActor actor) => available && actor != null && actor.onboard && Vector2.Distance(actor.Position, InteractionPosition) <= interactionRadius;
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, .8f, .2f, .6f);
            Gizmos.DrawWireSphere(InteractionPosition, interactionRadius);
        }
    }
}
