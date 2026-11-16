using UnityEngine;

namespace Lifeline
{
    /// <summary>Operable horizontal/vertical physical doorway. Keep collider and script on its stable root.</summary>
    public class TrapdoorGate : MonoBehaviour
    {
        public string saveId;
        public WorldGenerator world;
        public BoxCollider barrier;
        public Transform visualRoot;
        public TextMesh label;
        public bool horizontal, open;
        public float interactionDistance = 1.9f;
        public Vector3 sideA, sideB;
        public bool animatePlaceholder = true;
        public bool fitPlaceholderSize = true;
        public float openAngle = 90;
        [SerializeField] Vector3 closedScale = Vector3.one;

        public void Configure(bool isHorizontal, float clearWidth, float depth, Material material)
        {
            horizontal = isHorizontal;
            if (barrier == null) barrier = GetComponent<BoxCollider>();
            if (barrier == null) barrier = gameObject.AddComponent<BoxCollider>();
            barrier.isTrigger = false;
            barrier.size = horizontal ? new Vector3(clearWidth, .18f, depth) : new Vector3(.18f, clearWidth, depth);
            visualRoot = visualRoot != null ? visualRoot : transform.Find("VisualRoot");
            if (visualRoot == null)
            {
                var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mesh.name = "VisualRoot"; mesh.transform.SetParent(transform, false);
                var collider = mesh.GetComponent<Collider>(); collider.enabled = false;
                if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
                visualRoot = mesh.transform; visualRoot.localScale = barrier.size;
                mesh.GetComponent<Renderer>().sharedMaterial = material;
            }
            if (fitPlaceholderSize) visualRoot.localScale = barrier.size;
            if (animatePlaceholder) visualRoot.localRotation = Quaternion.identity;
            closedScale = visualRoot.localScale;
            if (label == null)
            {
                var text = new GameObject("Interaction label"); text.transform.SetParent(transform, false);
                text.transform.localPosition = new Vector3(0, horizontal ? .4f : clearWidth * .5f + .3f, -1);
                label = text.AddComponent<TextMesh>(); label.characterSize = .1f; label.fontSize = 48;
                label.anchor = TextAnchor.MiddleCenter; label.color = new Color(.95f, .73f, .32f);
            }
            if (fitPlaceholderSize) label.transform.localPosition = new Vector3(0, horizontal ? .4f : clearWidth * .5f + .3f, -1);
            SetOpen(false);
        }

        public bool InRange(CrewActor actor) => actor != null && !actor.onboard && !actor.downed
            && Vector2.Distance(actor.Position, transform.position) <= interactionDistance;
        public bool TryInteract(CrewActor actor)
        {
            if (!InRange(actor)) return false;
            SetOpen(!open); return true;
        }
        public void SetOpen(bool value)
        {
            // A door cannot sever a line which is already threaded through its opening.
            if (!value && open && barrier != null && world != null && world.session != null)
            {
                Bounds closedBounds = new Bounds(transform.TransformPoint(barrier.center), Vector3.Scale(barrier.size, transform.lossyScale));
                if (world.session.RopeCrossesBounds(closedBounds)) return;
            }
            open = value;
            if (barrier != null) barrier.enabled = !open;
            if (visualRoot != null && animatePlaceholder)
            {
                visualRoot.localRotation = Quaternion.Euler(0, 0, open ? openAngle : 0);
                visualRoot.localScale = closedScale;
            }
            if (label != null) label.text = open ? "OPEN / ENTER" : "HATCH / ENTER";
        }
    }
}
