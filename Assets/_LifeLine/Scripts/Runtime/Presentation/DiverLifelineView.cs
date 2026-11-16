using UnityEngine;

namespace Lifeline
{
    /// <summary>Draw the collision route used by the winch in the independent underwater map.</summary>
    [DefaultExecutionOrder(150)]
    public sealed class DiverLifelineView : MonoBehaviour
    {
        public GameSession session;
        public LineRenderer rope;
        public Material ropeMaterial;
        public Transform winchAttachment;
        [Min(.01f)] public float width = .055f;
        [Min(3)] public int segmentCount = 18;
        public Color ropeColor = new Color(1, .79f, .32f);
        [Min(0)] public float slackSag = .45f;
        public float foregroundZ = -.65f;
        public int presentationLayer = 8;
        Material generatedMaterial;

        void Start()
        {
            Initialize(session);
            if (session != null && session.maps != null) session.maps.MoveGameplayObject(gameObject, false);
        }

        public void Initialize(GameSession owner)
        {
            session = owner;
            if (rope == null)
            {
                var line = new GameObject("Diver lifeline"); line.transform.SetParent(transform, false);
                line.layer = presentationLayer; rope = line.AddComponent<LineRenderer>();
            }
            rope.useWorldSpace = true; rope.loop = false; rope.numCornerVertices = 2; rope.numCapVertices = 2;
            rope.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rope.receiveShadows = false;
            if (winchAttachment == null && session != null && session.ship != null)
                foreach (var anchor in session.ship.GetComponentsInChildren<StationAnchor>())
                    if (anchor.kind == StationKind.Winch) { winchAttachment = anchor.interactionPoint; break; }
            if (ropeMaterial != null) rope.sharedMaterial = ropeMaterial;
            RefreshNow();
        }

        void LateUpdate() => RefreshNow();
        public void RefreshNow()
        {
            if (rope == null) return;
            bool visible = session != null && session.diver != null && !session.diver.onboard
                && (session.phase == SessionPhase.Playing || session.phase == SessionPhase.Paused);
            rope.enabled = visible;
            if (!visible) return;
            if (rope.sharedMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    generatedMaterial = new Material(shader); generatedMaterial.name = "Lifeline placeholder rope";
                    if (generatedMaterial.HasProperty("_BaseColor")) generatedMaterial.SetColor("_BaseColor", ropeColor);
                    if (generatedMaterial.HasProperty("_Color")) generatedMaterial.SetColor("_Color", ropeColor);
                    rope.sharedMaterial = generatedMaterial;
                }
            }
            rope.startColor = rope.endColor = ropeColor; rope.startWidth = rope.endWidth = width;
            DiverRope route = session.GetOrCreateLifeline();
            route.UpdateRoute(session.BoatAnchor, session.diver.Position);
            int count = route.Points.Count + 1;
            rope.positionCount = count;
            float surface = session.world != null ? session.world.WaterSurface : session.diver.waterSurfaceY;
            rope.SetPosition(0, new Vector3(session.BoatAnchor.x, surface, foregroundZ));
            for (int i = 1; i < count; i++)
            {
                Vector3 point = route.Points[i - 1];
                point.z = foregroundZ; rope.SetPosition(i, point);
            }
        }
        void OnDestroy() { if (generatedMaterial != null) Destroy(generatedMaterial); }
    }
}
