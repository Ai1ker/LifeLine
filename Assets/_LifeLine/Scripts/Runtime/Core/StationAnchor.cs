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
        SpriteRenderer generatorSprite;
        Sprite regularGeneratorSprite;

        public void ShowEelGenerator(Sprite eel, bool installed)
        {
            if (eel == null || visualRoot == null) return;
            if (generatorSprite == null)
            {
                generatorSprite = visualRoot.GetComponentInChildren<SpriteRenderer>(true);
                if (generatorSprite == null) return;
                regularGeneratorSprite = generatorSprite.sprite;
            }
            Sprite sprite = installed ? eel : regularGeneratorSprite;
            if (generatorSprite.sprite == sprite || sprite == null) return;
            float width = generatorSprite.sprite.bounds.size.x * Mathf.Abs(generatorSprite.transform.localScale.x);
            generatorSprite.sprite = sprite;
            generatorSprite.transform.localScale = Vector3.one * (width / sprite.bounds.size.x);
        }
        public Vector3 InteractionPosition => interactionPoint != null ? interactionPoint.position : transform.position;
        public bool CanInteract(CrewActor actor) => available && isActiveAndEnabled && actor != null && (actor.session == null || actor.session.IsStationUnlocked(kind)) && actor.onboard && Vector2.Distance(actor.Position, InteractionPosition) <= interactionRadius;
        /// <summary>Context stays visible beside the binding even when the shared status message changes.</summary>
        public string GetInteractionWarning(CrewActor actor) => kind == StationKind.Helm && actor != null
            && actor.session != null && !actor.session.engineRepaired ? GameSession.EngineUnavailableMessage : string.Empty;
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, .8f, .2f, .6f);
            Gizmos.DrawWireSphere(InteractionPosition, interactionRadius);
        }
    }
}
