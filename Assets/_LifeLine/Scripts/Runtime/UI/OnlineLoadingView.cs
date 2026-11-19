using UnityEngine;
using UnityEngine.UI;

namespace Lifeline
{
    [DisallowMultipleComponent]
    public sealed class OnlineLoadingView : MonoBehaviour
    {
        public OnlineVoyage voyage;
        public Color backdropColor = new Color(.035f, .11f, .16f, .88f);
        public Color progressColor = new Color(.43f, .88f, .78f);
        Canvas canvas;
        RectTransform fill;
        Text stageLabel;

        void Update()
        {
            bool loading = voyage != null && voyage.TryGetLoadingProgress(out _, out _);
            if (!loading)
            {
                if (canvas != null) canvas.gameObject.SetActive(false);
                return;
            }
            if (canvas == null) Build();
            voyage.TryGetLoadingProgress(out float progress, out string stage);
            canvas.gameObject.SetActive(true);
            fill.anchorMax = new Vector2(progress, 1f);
            stageLabel.text = UiLocalization.Text(stage) + "   " + Mathf.RoundToInt(progress * 100f) + "%";
        }

        void Build()
        {
            var root = new GameObject("Online loading canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 140;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            Panel(root.transform, "Loading backdrop", Vector2.zero, Vector2.one, backdropColor, true);
            var track = Panel(root.transform, "Loading progress track", new Vector2(.25f, .45f), new Vector2(.75f, .475f),
                new Color(.11f, .28f, .34f), false);
            fill = Panel(track, "Progress fill", Vector2.zero, Vector2.one, progressColor, false);
            stageLabel = Rect(root.transform, "Loading stage", new Vector2(.14f, .49f), new Vector2(.86f, .56f)).gameObject.AddComponent<Text>();
            stageLabel.font = RuntimeUIFont.GetOrCreateFont();
            stageLabel.fontSize = 28;
            stageLabel.alignment = TextAnchor.MiddleCenter;
            stageLabel.color = Color.white;
            stageLabel.raycastTarget = false;
        }

        static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max, Color color, bool blocksInput)
        {
            var rect = Rect(parent, name, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = blocksInput;
            return rect;
        }

        static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
