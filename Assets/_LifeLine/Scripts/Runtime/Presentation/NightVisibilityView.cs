using UnityEngine;
using UnityEngine.UI;

namespace Lifeline
{
    /// <summary>A viewport-local UGUI overlay. Put the Image below HUD siblings in its Canvas.</summary>
    [DefaultExecutionOrder(200)]
    public class NightVisibilityView : MonoBehaviour
    {
        public GameSession session;
        public GameCameraRig cameraRig;
        public CrewActor diver;
        public Image maskImage;
        [Tooltip("Material using LifeLine/UI/Night Visibility. A private runtime copy receives shader parameters.")]
        public Material maskMaterial;

        [Header("Visibility (physical screen pixels)")]
        [Min(1)] public float personalRadiusPixels = 60;
        [Min(1)] public float searchlightRadiusPixels = 220;
        [Min(0)] public float featherPixels = 20;
        [Range(0, 1)] public float nightOpacity = 1;
        [Tooltip("T2 or higher with an effective powered searchlight. Zero disables the overlay.")]
        [Range(0, 1)] public float upgradedLightOpacity;
        public Color darknessColor = new Color(.006f, .014f, .025f, .98f);
        [Tooltip("Move the image to the first sibling, behind HUD and menus. Leave off if its Canvas has a dedicated background container.")]
        public bool placeBehindHud = true;
        public Vector3 focusWorldOffset;

        public bool MaskVisible => maskImage != null && maskImage.enabled;
        public float CurrentRadiusPixels { get; private set; }
        public Material RuntimeMaterial => materialInstance;
        Material materialInstance, materialSource;
        static readonly int FocusId = Shader.PropertyToID("_FocusUV");
        static readonly int ViewportId = Shader.PropertyToID("_ViewportPixels");
        static readonly int RectId = Shader.PropertyToID("_MaskRect");
        static readonly int RadiusId = Shader.PropertyToID("_RadiusPixels");
        static readonly int FeatherId = Shader.PropertyToID("_FeatherPixels");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        static readonly int DarknessId = Shader.PropertyToID("_DarknessColor");

        public void Initialize(GameSession owner, GameCameraRig rig, CrewActor actor, Image image, Material material)
        {
            session = owner; cameraRig = rig; diver = actor; maskImage = image; maskMaterial = material;
            RefreshNow();
        }

        void OnEnable() => RefreshNow();
        void LateUpdate() => RefreshNow();
        void OnDisable() { if (maskImage != null) maskImage.enabled = false; }

        public void RefreshNow()
        {
            if (maskImage == null) return;
            maskImage.raycastTarget = false;
            // This is a generated shader quad, not an atlas sprite: its UVs must span 0..1.
            maskImage.sprite = null; maskImage.overrideSprite = null; maskImage.type = Image.Type.Simple;
            CrewActor actor = diver != null ? diver : session != null ? session.diver : null;
            Camera camera = cameraRig != null ? cameraRig.diverCamera : null;
            bool gameplay = session != null && (session.phase == SessionPhase.Playing || session.phase == SessionPhase.Paused);
            bool localDiverVisible = cameraRig != null && (!cameraRig.onlineView || cameraRig.localRole == CrewRole.Diver);
            bool night = gameplay && session.IsNight && actor != null && !actor.onboard
                && localDiverVisible && camera != null && camera.enabled;
            float opacity = session != null && session.LightEffective && session.GetUpgrade(UpgradeKind.Searchlight) >= 1
                ? upgradedLightOpacity : nightOpacity;
            if (!night || opacity <= .0001f)
            {
                maskImage.enabled = false;
                return;
            }
            if (!EnsureMaterial()) { maskImage.enabled = false; return; }
            if (placeBehindHud && maskImage.transform.GetSiblingIndex() != 0) maskImage.transform.SetAsFirstSibling();
            // The Image must be a child of a full-screen RectTransform (the Canvas or a stretch container).
            Rect viewport = camera.rect;
            RectTransform rect = maskImage.rectTransform;
            rect.anchorMin = new Vector2(viewport.xMin, viewport.yMin);
            rect.anchorMax = new Vector2(viewport.xMax, viewport.yMax);
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            Vector3 focus = camera.WorldToViewportPoint(actor.transform.position + focusWorldOffset);
            CurrentRadiusPixels = session.LightEffective ? searchlightRadiusPixels : personalRadiusPixels;
            materialInstance.SetVector(FocusId, new Vector4(focus.x, focus.y, 0, 0));
            materialInstance.SetVector(ViewportId, new Vector4(Mathf.Max(1, camera.pixelWidth), Mathf.Max(1, camera.pixelHeight), 0, 0));
            Rect localRect = rect.rect;
            materialInstance.SetVector(RectId, new Vector4(localRect.xMin, localRect.yMin, localRect.width, localRect.height));
            materialInstance.SetFloat(RadiusId, CurrentRadiusPixels);
            materialInstance.SetFloat(FeatherId, Mathf.Min(featherPixels, CurrentRadiusPixels));
            materialInstance.SetFloat(OpacityId, opacity);
            materialInstance.SetColor(DarknessId, darknessColor);
            maskImage.enabled = true;
        }

        bool EnsureMaterial()
        {
            if (materialInstance != null && materialSource == maskMaterial)
            {
                if (maskImage.material != materialInstance) maskImage.material = materialInstance;
                return true;
            }
            ReleaseMaterial();
            if (maskMaterial != null) materialInstance = new Material(maskMaterial);
            else
            {
                Shader shader = Shader.Find("LifeLine/UI/Night Visibility");
                if (shader == null) return false;
                materialInstance = new Material(shader);
            }
            materialInstance.name = "Night visibility (instance)";
            materialInstance.hideFlags = HideFlags.DontSave;
            materialSource = maskMaterial;
            maskImage.material = materialInstance;
            return true;
        }

        void OnDestroy() => ReleaseMaterial();
        void ReleaseMaterial()
        {
            if (materialInstance == null) return;
            if (maskImage != null && maskImage.material == materialInstance) maskImage.material = maskMaterial;
            if (Application.isPlaying) Destroy(materialInstance); else DestroyImmediate(materialInstance);
            materialInstance = null; materialSource = null;
        }
    }
}
