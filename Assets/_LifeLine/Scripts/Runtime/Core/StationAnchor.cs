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
        [Header("教学废墟外观")]
        [Tooltip("Rubble object shown while the station is a ruin. Assigned by the tutorial system / prefab.")]
        public GameObject ruinVisual;
        [Tooltip("Normal station visuals. Hidden while ruined; falls back to visualRoot when empty.")]
        public GameObject restoredVisual;

        bool ruinAutoCreated;
        Vector3 iconLocalPos; // icon position relative to visualRoot (restored state)
        Vector3 ruinRootPos;  // same spot in station-root space (ruin state)

        void Awake()
        {
            // Read the real placeholder position instead of hardcoding a height. In the prefab,
            // upper-deck stations have Housing at y~0.42, lower-deck stations at y~-3.58.
            // iconLocalPos is relative to visualRoot (used by the restored icon).
            iconLocalPos = new Vector3(0, 0.42f, 0.4f);
            if (visualRoot != null)
            {
                var housing = visualRoot.Find("Housing");
                if (housing != null) iconLocalPos = housing.localPosition;
                iconLocalPos.z -= 0.85f; // toward camera, in front of the placeholder box
            }
            // ruinRootPos is the same spot expressed in the STATION ROOT space (ruinVisual lives there).
            if (visualRoot != null) ruinRootPos = visualRoot.localPosition + iconLocalPos;
            else ruinRootPos = iconLocalPos;
            if (ruinVisual == null) CreateRuntimeRuin();
            if (restoredVisual == null && visualRoot != null) restoredVisual = visualRoot.gameObject;
            // Per-station pixel icon on the restored visual (hidden while ruined).
            if (visualRoot != null)
                PixelArtDecor.Apply(visualRoot, PixelArtDecor.StationSprite(kind), 1.4f,
                    hidePlaceholders: true, localOffset: iconLocalPos);
        }

        void CreateRuntimeRuin()
        {
            // Ruin = same station icon tinted dark grey; cleaning restores full colour.
            var root = new GameObject("RuinVisual (runtime)");
            root.transform.SetParent(transform, false);
            var sr = root.AddComponent<SpriteRenderer>();
            var sprite = Resources.Load<Sprite>(PixelArtDecor.ArtFolder + PixelArtDecor.StationSprite(kind));
            var shader = Shader.Find("Sprites/Default");
            if (sprite != null)
            {
                sr.sprite = sprite;
                if (shader != null) sr.sharedMaterial = new Material(shader);
                sr.color = new Color(.32f, .32f, .36f, 1f);
                sr.sortingOrder = 1;
                float ppu = sprite.pixelsPerUnit > 0 ? sprite.pixelsPerUnit : 100f;
                float scale = 1.4f * ppu / Mathf.Max(1f, sprite.rect.width);
                root.transform.localScale = Vector3.one * scale;
            }
            else
            {
                // No pixel-art PNGs in this build (art is retired): show a grey box placeholder
                // so the ruin is still visible. Each renderer owns its material (no texture bleed).
                sr.sprite = PixelArtDecor.GreyBox();
                if (shader != null) sr.sharedMaterial = new Material(shader);
                sr.color = new Color(.32f, .32f, .36f, 1f);
                sr.sortingOrder = 1;
                root.transform.localScale = Vector3.one * 1.4f; // 1x1 sprite -> 1.4 world units
            }
            root.transform.localPosition = ruinRootPos + new Vector3(0, 0, 0.05f);
            ruinVisual = root;
            ruinAutoCreated = true;
        }

        /// <summary>Toggle ruin vs restored look. Safe to call before/after the tutorial is active.</summary>
        public void SetRestored(bool restored)
        {
            if (ruinVisual == null) return;
            ruinVisual.SetActive(!restored);
            if (restoredVisual != null) restoredVisual.SetActive(restored);
        }

        void OnDestroy()
        {
            if (ruinAutoCreated && ruinVisual != null)
            {
                var sr = ruinVisual.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sharedMaterial != null) Destroy(sr.sharedMaterial);
            }
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
