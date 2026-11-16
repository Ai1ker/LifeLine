using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Lifeline
{
    /// <summary>
    /// A deliberately small, non-interactive representation of the ship in the underwater map.
    /// All position/state comes from GameSession; no cabin renderers or ship colliders are copied.
    /// Replace visualRoot and edgeMarker with art/UI prefabs without changing the simulation.
    /// </summary>
    [DefaultExecutionOrder(160)]
    public sealed class UnderwaterShipProxy : MonoBehaviour
    {
        public GameSession session;
        public GameCameraRig cameraRig;
        public Transform visualRoot;
        public RectTransform edgeMarker;
        public Material hullMaterial, deckMaterial, mastMaterial;
        public Color hullColor = new Color(.96f, .68f, .29f);
        public Color deckColor = new Color(.95f, .96f, .82f);
        public Color mastColor = new Color(.16f, .32f, .36f);
        public Vector3 proxyScale = Vector3.one;
        public float surfaceHeight = .28f;
        public float foregroundZ = -1.2f;
        [Range(0, 31)] public int presentationLayer = 14;
        public bool showTopEdgeMarker = true;
        [Range(.5f, .9f)] public float markerViewportHeight = .78f;
        public Vector2 markerSize = new Vector2(58, 30);
        public float markerHorizontalPadding = 24;

        public bool IsSurfaceProxyVisible => visualRoot != null && visualRoot.gameObject.activeSelf;
        public bool IsEdgeMarkerVisible => edgeMarker != null && edgeMarker.gameObject.activeSelf;
        readonly List<Material> generatedMaterials = new List<Material>();
        Canvas markerCanvas;

        void Awake()
        {
            if (session != null) Initialize(session, cameraRig);
        }

        public void Initialize(GameSession owner, GameCameraRig rig)
        {
            session = owner; cameraRig = rig;
            if (visualRoot == null) BuildGeometry();
            if (edgeMarker == null) BuildEdgeMarker();
            if (session != null)
            {
                var maps = session.GetComponent<GameMapPartition>();
                if (maps == null) maps = FindFirstObjectByType<GameMapPartition>();
                if (maps != null && maps.session == session) maps.MoveGameplayObject(gameObject, false);
            }
            RefreshNow();
        }

        void LateUpdate() => RefreshNow();

        public void RefreshNow()
        {
            bool visible = session != null && session.ship != null && session.diver != null && !session.diver.onboard
                && (session.phase == SessionPhase.Playing || session.phase == SessionPhase.Paused);
            if (visualRoot != null) visualRoot.gameObject.SetActive(visible);
            if (!visible)
            {
                if (edgeMarker != null) edgeMarker.gameObject.SetActive(false);
                return;
            }
            float surface = session.world != null ? session.world.WaterSurface : -1.5f;
            transform.position = new Vector3(session.BoatAnchor.x, surface + surfaceHeight, foregroundZ);
            visualRoot.localScale = proxyScale;
            foreach (var renderer in visualRoot.GetComponentsInChildren<Renderer>(true)) renderer.gameObject.layer = presentationLayer;
            if (edgeMarker == null) return;
            Camera output = cameraRig != null ? cameraRig.diverCamera : null;
            bool enabledView = output != null && output.enabled;
            Vector3 viewport = enabledView ? output.WorldToViewportPoint(transform.position + Vector3.up * .5f) : Vector3.zero;
            bool outside = viewport.z <= 0 || viewport.y > markerViewportHeight || viewport.x < .05f || viewport.x > .95f;
            edgeMarker.gameObject.SetActive(showTopEdgeMarker && enabledView && outside);
            if (!edgeMarker.gameObject.activeSelf) return;
            Rect pixels = output.pixelRect;
            float screenX = output.WorldToScreenPoint(transform.position).x;
            float padding = markerHorizontalPadding + markerSize.x * .5f;
            screenX = Mathf.Clamp(screenX, pixels.xMin + padding, pixels.xMax - padding);
            edgeMarker.position = new Vector3(screenX, pixels.yMin + pixels.height * markerViewportHeight, 0);
            edgeMarker.sizeDelta = markerSize;
        }

        void BuildGeometry()
        {
            visualRoot = new GameObject("Small surface ship - visual only").transform;
            visualRoot.SetParent(transform, false);
            Material hull = hullMaterial != null ? hullMaterial : MakeMaterial("Water map proxy hull", hullColor);
            Material deck = deckMaterial != null ? deckMaterial : MakeMaterial("Water map proxy deck", deckColor);
            Material mast = mastMaterial != null ? mastMaterial : MakeMaterial("Water map proxy mast", mastColor);
            Part("Hull", new Vector3(0, 0, 0), new Vector3(3.4f, .42f, .28f), hull);
            Part("Bow", new Vector3(1.65f, .13f, 0), new Vector3(.3f, .24f, .28f), hull);
            Part("Deck silhouette", new Vector3(-.25f, .46f, 0), new Vector3(1.25f, .52f, .24f), deck);
            Part("Mast", new Vector3(.42f, .88f, 0), new Vector3(.07f, 1.1f, .2f), mast);
            Part("Flag", new Vector3(.69f, 1.27f, 0), new Vector3(.48f, .28f, .15f), hull);
            Part("Window", new Vector3(-.25f, .49f, -.17f), new Vector3(.62f, .22f, .06f), mast);
        }

        void Part(string title, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = title; part.layer = presentationLayer;
            part.transform.SetParent(visualRoot, false); part.transform.localPosition = position; part.transform.localScale = scale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            }
            var renderer = part.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        }

        Material MakeMaterial(string title, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = title };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            generatedMaterials.Add(material); return material;
        }

        void BuildEdgeMarker()
        {
            var canvasObject = new GameObject("Surface ship bearing overlay", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            markerCanvas = canvasObject.GetComponent<Canvas>(); markerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            markerCanvas.sortingOrder = 3;
            edgeMarker = Rect("Small ship bearing", canvasObject.transform, Vector2.zero, Vector2.zero);
            edgeMarker.anchorMin = edgeMarker.anchorMax = Vector2.zero; edgeMarker.pivot = new Vector2(.5f, .5f);
            edgeMarker.sizeDelta = markerSize;
            Image("Hull", edgeMarker, new Vector2(.08f, .12f), new Vector2(.92f, .38f), hullColor);
            Image("Deck", edgeMarker, new Vector2(.28f, .38f), new Vector2(.62f, .69f), deckColor);
            Image("Mast", edgeMarker, new Vector2(.65f, .38f), new Vector2(.69f, .98f), mastColor);
            Image("Flag", edgeMarker, new Vector2(.69f, .80f), new Vector2(.93f, .98f), hullColor);
            Image("Bearing", edgeMarker, new Vector2(.49f, -.15f), new Vector2(.51f, .07f), mastColor);
        }

        static RectTransform Rect(string title, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(title, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        static void Image(string title, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var image = Rect(title, parent, min, max).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
        }

        void OnDestroy()
        {
            foreach (Material material in generatedMaterials)
                if (material != null)
                {
                    if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
                }
            generatedMaterials.Clear();
        }
    }
}
