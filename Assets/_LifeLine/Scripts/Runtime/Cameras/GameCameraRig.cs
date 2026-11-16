using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

namespace Lifeline
{
    /// <summary>
    /// Aboard ship the crew share a fitted view by default; the preference can split it.
    /// Diving always splits local play. Independent channels retain online local-role full-screen views;
    /// the official Brain/Follow/Confiner/Deoccluder/Decollider pipeline moves the real cameras.
    /// Initialize also works in the Editor so all components and references can be saved in scenes.
    /// </summary>
    [DefaultExecutionOrder(-120)]
    public class GameCameraRig : MonoBehaviour
    {
        public GameSession session;
        public CrewActor captain, diver;
        public ShipMotor ship;
        public Camera captainCamera, diverCamera;
        public bool onlineView;
        public CrewRole localRole;
        public const string ShipSplitPreferenceKey = "LifeLine.View.SplitScreenOnShip";
        public bool splitScreenOnShip;

        [Header("Per-view rendering (physics layers remain unchanged)")]
        public LayerMask captainViewMask = ~0;
        public LayerMask diverViewMask = ~0;
        [Range(0, 31)] public int shipSolidLayer = 10;
        [Tooltip("Reserve this layer for ship-only visuals. Never assign it to underwater weapons or physics roots.")]
        [Range(0, 31)] public int shipPresentationLayer = 12;
        public bool isolateUnderwaterShip = true;
        [Tooltip("Only the small water-map ship proxy uses this layer; the cabin stays in the remote ship map.")]
        [Range(0, 31)] public int underwaterProxyLayer = 14;

        [Header("Serialized Cinemachine rig (safe to tune / replace)")]
        public CinemachineCamera captainVirtualCamera, diverVirtualCamera;
        public CinemachineBrain captainBrain, diverBrain;
        public CinemachineFollow captainFollow, diverFollow;
        public CinemachineConfiner2D captainConfiner, diverConfiner;
        public CinemachineDeoccluder captainDeoccluder, diverDeoccluder;
        public CinemachineDecollider captainDecollider, diverDecollider;
        public Transform captainFocus, diverFocus;
        public BoxCollider2D shipBoundary, waterBoundary;

        [Header("Framing (metres)")]
        [Min(1)] public float cameraDistance = 24;
        [Min(1)] public float captainSize = 6.3f;
        [Min(1)] public float diverSize = 7.2f;
        [Min(1)] public float sharedMinimumSize = 7.5f;
        [Min(1)] public float sharedMaximumSize = 24;
        public Vector2 sharedPadding = new Vector2(1.8f, 1.3f);
        public float shipHalfWidth = 12;
        public Vector2 shipVerticalRange = new Vector2(-5, 3);
        public Vector2 captainOffset = new Vector2(0, .8f);
        public Vector2 diverOffset = new Vector2(0, -.35f);
        public bool keepUnderwaterViewBelowSurface = true;
        [Min(0)] public float waterSurfaceViewPadding = .1f;
        [Tooltip("Room above the waterline for the small ship proxy, never the physical cabin map.")]
        [Min(0)] public float underwaterSurfaceHeadroom = 4.5f;
        public float horizontalLookAhead = .7f;
        public Vector3 followDamping = new Vector3(.24f, .2f, 0);
        [Min(0)] public float zoomDamping = .16f;
        [Tooltip("Ship-local camera confinement, follows the moving ship without rebaking the shape.")]
        public Rect shipViewBounds = new Rect(-24, -20, 48, 40);
        public Rect underwaterViewBounds = new Rect(-22, -55, 122, 65);
        public Color oceanClearColor = new Color(.48f, .76f, .82f);

        [Header("Official Cinemachine 3D obstruction handling")]
        [Tooltip("Solid world geometry only. Ship cutaway solids are excluded from camera obstruction; Crew/pickups/CameraBounds must also be excluded.")]
        public LayerMask obstacleMask = 1;
        [Min(.01f)] public float cameraCollisionRadius = .22f;
        [Min(0)] public float minimumObstacleDistance = 1.05f;
        [Min(0)] public float returnFromObstructionDamping = .15f;
        [Range(0, 31)] public int cameraBoundsLayer = 2;
        public bool obstructionAvoidance = true;
        public bool preventCameraPenetration = true;
        public bool IsSplitScreen { get; private set; }
        public bool IsSharedView => !onlineView && !IsSplitScreen;
        bool configured;
        bool previousSplit;
        float lastCaptainLens = -1, lastDiverLens = -1;
        sealed class VisualLayer
        {
            public GameObject gameObject;
            public int original;
            public bool touched;
        }
        readonly Dictionary<GameObject, VisualLayer> isolatedVisuals = new Dictionary<GameObject, VisualLayer>();
        readonly List<GameObject> layersToRestore = new List<GameObject>();

        public void Initialize(GameSession owner)
        {
            Initialize(owner, owner != null ? owner.captain : captain,
                owner != null ? owner.diver : diver, owner != null ? owner.ship : ship);
        }

        public void Initialize(GameSession owner, CrewActor captainActor, CrewActor diverActor, ShipMotor shipMotor)
        {
            session = owner; captain = captainActor; diver = diverActor; ship = shipMotor;
            BuildRuntimeRig();
        }

        [ContextMenu("Build / refresh serialized Cinemachine rig")]
        public void BuildRuntimeRig()
        {
            // Rebuilding resets the CM lens. Drop our cache even when Initialize is called twice.
            lastCaptainLens = lastDiverLens = -1;
            captainFocus = EnsureChild(captainFocus, "Captain tracking focus");
            diverFocus = EnsureChild(diverFocus, "Diver tracking focus");
            captainCamera = EnsureCamera(captainCamera, "Captain output camera", 0);
            diverCamera = EnsureCamera(diverCamera, "Diver output camera", 1);
            captainBrain = ConfigureBrain(captainCamera, OutputChannels.Channel01);
            diverBrain = ConfigureBrain(diverCamera, OutputChannels.Channel02);
            captainVirtualCamera = EnsureVirtualCamera(captainVirtualCamera, "Captain Cinemachine", captainFocus, OutputChannels.Channel01);
            diverVirtualCamera = EnsureVirtualCamera(diverVirtualCamera, "Diver Cinemachine", diverFocus, OutputChannels.Channel02);
            captainFollow = ConfigureFollow(captainVirtualCamera);
            diverFollow = ConfigureFollow(diverVirtualCamera);
            shipBoundary = ConfigureBoundary(shipBoundary, "Ship camera boundary", shipViewBounds);
            waterBoundary = ConfigureBoundary(waterBoundary, "Water camera boundary", underwaterViewBounds);
            captainConfiner = ConfigureConfiner(captainVirtualCamera);
            diverConfiner = ConfigureConfiner(diverVirtualCamera);
            captainDeoccluder = ConfigureDeoccluder(captainVirtualCamera);
            diverDeoccluder = ConfigureDeoccluder(diverVirtualCamera);
            captainDecollider = ConfigureDecollider(captainVirtualCamera);
            diverDecollider = ConfigureDecollider(diverVirtualCamera);
            configured = true;
            RefreshView(true);
            if (!Application.isPlaying)
            {
                // Explicit builder calls produce final output-camera transforms before scene save.
                if (captainCamera.enabled) PrimeEditorBrain(captainBrain);
                if (diverCamera.enabled) PrimeEditorBrain(diverBrain);
            }
        }

        static void PrimeEditorBrain(CinemachineBrain brain)
        {
            var previousMethod = brain.UpdateMethod;
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            try { brain.ManualUpdate(); }
            finally { brain.UpdateMethod = previousMethod; }
        }

        public void SetNetworkView(bool enabled, CrewRole role)
        {
            // Presentation seam only. Authority/transport is supplied by the session's network layer.
            onlineView = enabled; localRole = role;
            if (configured || captainVirtualCamera != null) RefreshView(true);
        }

        public static bool LoadShipSplitPreference() => PlayerPrefs.GetInt(ShipSplitPreferenceKey, 0) != 0;
        public static void SaveShipSplitPreference(bool enabled)
        {
            PlayerPrefs.SetInt(ShipSplitPreferenceKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
        public void SetShipSplitPreference(bool enabled, bool persist = true)
        {
            splitScreenOnShip = enabled;
            if (persist) SaveShipSplitPreference(enabled);
            if (configured) RefreshView(true);
        }
        public static bool ShouldUseSplitScreen(bool online, bool diverOnboard, bool splitOnShip)
            => !online && (splitOnShip || !diverOnboard);
        public void RefreshViewNow(bool snap = true)
        {
            if (!configured) BuildRuntimeRig();
            else RefreshView(snap);
        }

        void Awake()
        {
            splitScreenOnShip = LoadShipSplitPreference();
            if (session != null) Initialize(session);
            else BuildRuntimeRig();
        }

        void LateUpdate()
        {
            if (!configured && captainVirtualCamera != null && diverVirtualCamera != null) configured = true;
            if (configured) RefreshView(false);
        }

        void RefreshView(bool snap)
        {
            if (captainCamera == null || diverCamera == null || captainFocus == null || diverFocus == null) return;
            IsSplitScreen = ShouldUseSplitScreen(onlineView, diver == null || diver.onboard, splitScreenOnShip);
            bool shared = !onlineView && !IsSplitScreen;
            bool transition = snap || previousSplit != IsSplitScreen;
            previousSplit = IsSplitScreen;
            captainCamera.enabled = !onlineView || localRole == CrewRole.Captain;
            diverCamera.enabled = IsSplitScreen || onlineView && localRole == CrewRole.Diver;
            captainBrain.enabled = captainCamera.enabled;
            diverBrain.enabled = diverCamera.enabled;
            captainCamera.rect = IsSplitScreen ? new Rect(0, 0, .5f, 1) : new Rect(0, 0, 1, 1);
            diverCamera.rect = IsSplitScreen ? new Rect(.5f, 0, .5f, 1) : new Rect(0, 0, 1, 1);
            RefreshViewLayers();

            Vector3 boatPosition = ship != null ? ship.transform.position : Vector3.zero;
            if (shipBoundary != null) shipBoundary.transform.position = new Vector3(boatPosition.x, boatPosition.y, 0);
            // Transform positions contain Unity Rigidbody interpolation for the rendered frame.
            Vector3 capPosition = captain != null ? captain.transform.position : boatPosition + Vector3.up;
            Vector3 divPosition = diver != null ? diver.transform.position : capPosition;
            Vector3 capTarget = capPosition + (Vector3)captainOffset;
            float desiredCaptainSize = captainSize;
            if (shared) desiredCaptainSize = GetSharedFraming(capPosition, divPosition, boatPosition, captainCamera.aspect, out capTarget);
            else if (captain != null) capTarget.x += captain.Facing.x * horizontalLookAhead;
            capTarget.z = 0;
            captainFocus.position = capTarget;
            Vector3 divTarget = divPosition + (Vector3)diverOffset;
            if (diver != null) divTarget.x += diver.Facing.x * horizontalLookAhead;
            if (diver != null && !diver.onboard && keepUnderwaterViewBelowSurface)
            {
                float surface = session != null && session.world != null ? session.world.WaterSurface : -1.5f;
                divTarget.y = Mathf.Min(divTarget.y, surface - diverSize - waterSurfaceViewPadding + underwaterSurfaceHeadroom);
            }
            divTarget.z = 0; diverFocus.position = divTarget;

            SetBoundary(captainConfiner, shipBoundary);
            SetBoundary(diverConfiner, diver != null && diver.onboard ? shipBoundary : waterBoundary);
            UpdateLens(captainVirtualCamera, captainConfiner, desiredCaptainSize, ref lastCaptainLens, transition);
            UpdateLens(diverVirtualCamera, diverConfiner, diver != null && diver.onboard ? captainSize : diverSize, ref lastDiverLens, transition);
            if (transition)
            {
                captainVirtualCamera.PreviousStateIsValid = false;
                diverVirtualCamera.PreviousStateIsValid = false;
            }
        }

        void RefreshViewLayers()
        {
            bool underwater = diver != null && !diver.onboard;
            foreach (var entry in isolatedVisuals.Values) entry.touched = false;
            if (isolateUnderwaterShip)
            {
                IsolateVisuals(ship != null ? ship.transform : null, true);
                if (captain != null && captain.onboard) IsolateVisuals(captain.transform);
                if (diver != null && diver.onboard) IsolateVisuals(diver.transform);
            }
            layersToRestore.Clear();
            foreach (var pair in isolatedVisuals)
                if (!pair.Value.touched || pair.Key == null) layersToRestore.Add(pair.Key);
            foreach (var key in layersToRestore)
            {
                var record = isolatedVisuals[key];
                if (record.gameObject != null) record.gameObject.layer = record.original;
                isolatedVisuals.Remove(key);
            }
            int presentation = 1 << shipPresentationLayer;
            int waterProxy = 1 << underwaterProxyLayer;
            captainCamera.cullingMask = (captainViewMask | presentation) & ~waterProxy;
            diverCamera.cullingMask = isolateUnderwaterShip && underwater
                ? (diverViewMask | waterProxy) & ~((1 << shipSolidLayer) | presentation)
                : (diverViewMask | presentation) & ~waterProxy;
            // An invisible hull must not pull the underwater camera toward a cabin wall.
            // The playable cabin is a 2D cutaway. Its ceiling must never pull the camera
            // through the sprite plane when the crew jumps or the boat drifts in a storm.
            int diverObstacles = obstacleMask & ~(1 << shipSolidLayer);
            if (captainDeoccluder != null) captainDeoccluder.CollideAgainst = diverObstacles;
            if (captainDecollider != null)
            {
                var collision = captainDecollider.Decollision;
                collision.ObstacleLayers = diverObstacles; captainDecollider.Decollision = collision;
            }
            if (diverDeoccluder != null) diverDeoccluder.CollideAgainst = diverObstacles;
            if (diverDecollider != null)
            {
                var collision = diverDecollider.Decollision;
                collision.ObstacleLayers = diverObstacles;
                diverDecollider.Decollision = collision;
            }
        }

        void IsolateVisuals(Transform root, bool isShip = false)
        {
            if (root == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                GameObject go = renderer.gameObject;
                if (isShip && (go.layer == 8 || go.layer == 13 || renderer is LineRenderer || renderer is TrailRenderer)) continue;
                // Ship solid meshes already use ShipSolid. Leave collider/root layers intact.
                if (go.GetComponent<Collider>() != null || go.GetComponent<Rigidbody>() != null) continue;
                if (!isolatedVisuals.TryGetValue(go, out var record))
                {
                    // Generated scenes may have been saved while the editor preview isolated
                    // an aboard renderer. Recover its physics root's layer rather than treating
                    // the temporary ShipPresentation layer as the actor's authored water layer.
                    int original = go.layer == shipPresentationLayer ? (isShip ? shipSolidLayer : root.gameObject.layer) : go.layer;
                    record = new VisualLayer { gameObject = go, original = original };
                    isolatedVisuals.Add(go, record);
                }
                record.touched = true;
                go.layer = shipPresentationLayer;
            }
        }

        void RestoreVisualLayers()
        {
            foreach (var record in isolatedVisuals.Values)
                if (record.gameObject != null) record.gameObject.layer = record.original;
            isolatedVisuals.Clear();
            if (captainCamera != null) captainCamera.cullingMask = captainViewMask;
            if (diverCamera != null) diverCamera.cullingMask = diverViewMask;
        }

        void OnDisable() => RestoreVisualLayers();
        void OnDestroy() => RestoreVisualLayers();

        public float GetSharedFraming(Vector3 captainPosition, Vector3 diverPosition, Vector3 boatPosition, float aspect, out Vector3 focus)
        {
            focus = (captainPosition + diverPosition) * .5f + Vector3.up * .45f;
            focus.z = 0;
            float left = Mathf.Min(boatPosition.x - shipHalfWidth, Mathf.Min(captainPosition.x, diverPosition.x));
            float right = Mathf.Max(boatPosition.x + shipHalfWidth, Mathf.Max(captainPosition.x, diverPosition.x));
            float bottom = Mathf.Min(boatPosition.y + shipVerticalRange.x, Mathf.Min(captainPosition.y, diverPosition.y));
            float top = Mathf.Max(boatPosition.y + shipVerticalRange.y, Mathf.Max(captainPosition.y, diverPosition.y));
            float halfWidth = Mathf.Max(focus.x - left, right - focus.x) + sharedPadding.x;
            float halfHeight = Mathf.Max(focus.y - bottom, top - focus.y) + sharedPadding.y;
            return Mathf.Clamp(Mathf.Max(halfHeight, halfWidth / Mathf.Max(.1f, aspect)), sharedMinimumSize, sharedMaximumSize);
        }

        void UpdateLens(CinemachineCamera vcam, CinemachineConfiner2D confiner, float target, ref float last, bool snap)
        {
            if (vcam == null) return;
            float current = vcam.Lens.OrthographicSize;
            float weight = snap || !Application.isPlaying || zoomDamping <= 0 ? 1 : 1 - Mathf.Exp(-Time.unscaledDeltaTime / zoomDamping);
            float size = Mathf.Lerp(current, target, weight);
            // Quantize lens updates so Confiner's lens cache is not invalidated for subpixel jitter.
            size = Mathf.Round(size * 40) / 40;
            if (Mathf.Abs(size - last) < .024f) return;
            var lens = vcam.Lens; lens.OrthographicSize = size; vcam.Lens = lens;
            last = size;
            if (confiner != null) confiner.InvalidateLensCache();
        }

        static void SetBoundary(CinemachineConfiner2D confiner, Collider2D shape)
        {
            if (confiner == null || confiner.BoundingShape2D == shape) return;
            confiner.BoundingShape2D = shape; confiner.InvalidateBoundingShapeCache();
        }

        Camera EnsureCamera(Camera camera, string title, int depth)
        {
            bool created = camera == null;
            if (created) camera = Ensure<Camera>(EnsureChild(null, title).gameObject);
            camera.orthographic = true; camera.orthographicSize = captainSize;
            camera.nearClipPlane = .05f; camera.farClipPlane = 120;
            camera.backgroundColor = oceanClearColor; camera.clearFlags = CameraClearFlags.SolidColor; camera.depth = depth;
            if (created) { camera.transform.position = Vector3.back * cameraDistance; camera.transform.rotation = Quaternion.identity; }
            return camera;
        }
        CinemachineBrain ConfigureBrain(Camera camera, OutputChannels channel)
        {
            var brain = Ensure<CinemachineBrain>(camera.gameObject);
            brain.ChannelMask = channel;
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0);
            brain.LensModeOverride = new CinemachineBrain.LensModeOverrideSettings { Enabled = true, DefaultMode = LensSettings.OverrideModes.Orthographic };
            return brain;
        }
        CinemachineCamera EnsureVirtualCamera(CinemachineCamera vcam, string title, Transform focus, OutputChannels channel)
        {
            if (vcam == null) vcam = Ensure<CinemachineCamera>(EnsureChild(null, title).gameObject);
            vcam.transform.rotation = Quaternion.identity;
            vcam.Follow = focus; vcam.LookAt = focus; vcam.OutputChannel = channel;
            var lens = LensSettings.Default;
            lens.ModeOverride = LensSettings.OverrideModes.Orthographic; lens.OrthographicSize = captainSize;
            lens.NearClipPlane = .05f; lens.FarClipPlane = 120; vcam.Lens = lens;
            return vcam;
        }
        CinemachineFollow ConfigureFollow(CinemachineCamera vcam)
        {
            var follow = Ensure<CinemachineFollow>(vcam.gameObject);
            follow.FollowOffset = Vector3.back * cameraDistance;
            var tracking = TrackerSettings.Default; tracking.BindingMode = BindingMode.WorldSpace;
            tracking.PositionDamping = followDamping; follow.TrackerSettings = tracking;
            return follow;
        }
        BoxCollider2D ConfigureBoundary(BoxCollider2D shape, string title, Rect bounds)
        {
            if (shape == null) shape = Ensure<BoxCollider2D>(EnsureChild(null, title).gameObject);
            shape.gameObject.layer = cameraBoundsLayer;
            shape.offset = bounds.center; shape.size = bounds.size;
            shape.isTrigger = true;
            return shape;
        }
        CinemachineConfiner2D ConfigureConfiner(CinemachineCamera vcam)
        {
            var confiner = Ensure<CinemachineConfiner2D>(vcam.gameObject);
            confiner.Damping = .1f; confiner.SlowingDistance = .75f;
            confiner.OversizeWindow = new CinemachineConfiner2D.OversizeWindowSettings { Enabled = true, MaxWindowSize = Mathf.Max(sharedMaximumSize, Mathf.Max(captainSize, diverSize)), Padding = .5f };
            confiner.InvalidateBoundingShapeCache();
            return confiner;
        }
        CinemachineDeoccluder ConfigureDeoccluder(CinemachineCamera vcam)
        {
            var extension = Ensure<CinemachineDeoccluder>(vcam.gameObject);
            extension.CollideAgainst = obstacleMask; extension.MinimumDistanceFromTarget = minimumObstacleDistance;
            extension.AvoidObstacles = new CinemachineDeoccluder.ObstacleAvoidance
            {
                Enabled = obstructionAvoidance, CameraRadius = cameraCollisionRadius,
                DistanceLimit = cameraDistance + 2, MinimumOcclusionTime = 0,
                Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward,
                MaximumEffort = 5, Damping = returnFromObstructionDamping, DampingWhenOccluded = 0,
                UseFollowTarget = new CinemachineDeoccluder.ObstacleAvoidance.FollowTargetSettings { Enabled = true }
            };
            return extension;
        }
        CinemachineDecollider ConfigureDecollider(CinemachineCamera vcam)
        {
            var extension = Ensure<CinemachineDecollider>(vcam.gameObject);
            extension.CameraRadius = cameraCollisionRadius;
            extension.TerrainResolution = new CinemachineDecollider.TerrainSettings { Enabled = false };
            extension.Decollision = new CinemachineDecollider.DecollisionSettings
            {
                Enabled = preventCameraPenetration, ObstacleLayers = obstacleMask,
                Damping = returnFromObstructionDamping,
                UseFollowTarget = new CinemachineDecollider.DecollisionSettings.FollowTargetSettings { Enabled = true }
            };
            return extension;
        }
        Transform EnsureChild(Transform child, string title)
        {
            if (child != null) return child;
            child = transform.Find(title);
            if (child != null) return child;
            child = new GameObject(title).transform; child.SetParent(transform, false);
            return child;
        }
        static T Ensure<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component == null) component = go.AddComponent<T>();
            return component;
        }
    }
}
