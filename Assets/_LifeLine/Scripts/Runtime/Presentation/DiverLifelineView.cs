using UnityEngine;

namespace Lifeline
{
    /// <summary>Draw the collision route used by the winch in the independent underwater map.</summary>
    [DefaultExecutionOrder(150)]
    public sealed class DiverLifelineView : MonoBehaviour
    {
        public GameSession session;
        public LineRenderer rope;
        public LineRenderer shipLead;
        public Material ropeMaterial;
        public Transform winchAttachment;
        public LootPickup cargo;
        public UnderwaterShipProxy shipProxy;
        public Vector3 cargoAttachmentLocal = new Vector3(0, .25f, 0);
        [Min(.01f)] public float width = .055f;
        public Color ropeColor = new Color(1, .79f, .32f);
        public float foregroundZ = -1.7f;
        public int presentationLayer = 8;
        Material generatedMaterial;

        void Start()
        {
            Initialize(session);
            if (cargo == null && session != null && session.maps != null) session.maps.MoveGameplayObject(gameObject, false);
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
                    if (anchor.kind == StationKind.Winch) { winchAttachment = anchor.interactionPoint != null ? anchor.interactionPoint : anchor.transform; break; }
            if (ropeMaterial != null) rope.sharedMaterial = ropeMaterial;
            RefreshNow();
        }

        public void Initialize(GameSession owner, LootPickup container) { cargo = container; Initialize(owner); }

        void LateUpdate() => RefreshNow();
        public void RefreshNow()
        {
            if (rope == null) return;
            if (cargo == null) RefreshShipLead();
            bool visible = session != null && session.diver != null
                && (cargo != null ? cargo.attached && !cargo.consumed : !session.diver.onboard)
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
            DiverRope route;
            if (cargo == null) route = session.GetOrCreateLifeline();
            else route = session.GetCargoLifeline(cargo);
            route.UpdateRoute(session.BoatAnchor, cargo != null ? cargo.transform.position : session.diver.Position);
            int count = route.Points.Count + 1;
            rope.positionCount = count;
            float surface = session.world != null ? session.world.WaterSurface : session.diver.waterSurfaceY;
            if (shipProxy == null) shipProxy = FindFirstObjectByType<UnderwaterShipProxy>();
            Vector3 winch = shipProxy != null ? shipProxy.WinchPoint : new Vector3(session.BoatAnchor.x, surface, foregroundZ);
            winch.z = foregroundZ; rope.SetPosition(0, winch);
            for (int i = 1; i < count; i++)
            {
                Vector3 point = route.Points[i - 1];
                point.z = foregroundZ; rope.SetPosition(i, point);
            }
            if (cargo != null)
            {
                Vector3 attachment = cargo.transform.TransformPoint(cargoAttachmentLocal);
                attachment.z = foregroundZ; rope.SetPosition(count - 1, attachment);
            }
        }

        void RefreshShipLead()
        {
            bool visible = session != null && session.ship != null && session.diver != null
                && (session.phase == SessionPhase.Playing || session.phase == SessionPhase.Paused);
            bool hasTarget = session != null && session.diver != null && !session.diver.onboard;
            if (!hasTarget && session != null && session.world != null)
                foreach (var pickup in session.world.pickups)
                    if (pickup != null && pickup.attached && !pickup.consumed) { hasTarget = true; break; }
            visible &= hasTarget && winchAttachment != null;
            if (shipLead == null && visible)
            {
                var lead = new GameObject("Winch outgoing cable - ship map");
                lead.transform.SetParent(session.ship.transform, false); lead.layer = 12;
                shipLead = lead.AddComponent<LineRenderer>(); shipLead.useWorldSpace = true;
                shipLead.positionCount = 2; shipLead.numCapVertices = 2;
                shipLead.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; shipLead.receiveShadows = false;
            }
            if (shipLead == null) return;
            shipLead.enabled = visible;
            if (!visible) return;
            Material material = ropeMaterial != null ? ropeMaterial : rope.sharedMaterial;
            if (material != null) shipLead.sharedMaterial = material;
            shipLead.startColor = shipLead.endColor = ropeColor; shipLead.startWidth = shipLead.endWidth = width;
            Vector3 from = winchAttachment.position, to = session.WaterToShipPoint(session.BoatAnchor);
            from.z = to.z = foregroundZ;
            shipLead.SetPosition(0, from); shipLead.SetPosition(1, to);
        }

        void OnDisable() { if (shipLead != null) shipLead.enabled = false; }
        void OnDestroy()
        {
            if (shipLead != null) Destroy(shipLead.gameObject);
            if (generatedMaterial != null) Destroy(generatedMaterial);
        }
    }
}
