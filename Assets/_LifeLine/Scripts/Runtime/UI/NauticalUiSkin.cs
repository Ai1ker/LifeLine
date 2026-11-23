#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Lifeline
{
    public static class NauticalUiSkin
    {
        public static readonly Color Ink = new Color32(68, 38, 28, 255);
        public static readonly Color SoftInk = new Color32(108, 61, 36, 255);
        public static readonly Color Parchment = new Color32(246, 212, 154, 255);
        public static readonly Color Basin = new Color32(180, 122, 77, 255);
        public static readonly Color Track = new Color32(90, 62, 45, 255);

        public static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_LifeLine/Art/UI/StaticSprites/Resources/LifeLineUI/Nautical/" + name + ".png");

        public static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            Stretch(rect, min, max);
            return rect;
        }

        public static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public static void Pin(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static Image Image(Transform parent, string name, string glyph = null)
        {
            var image = Rect(parent, name, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            if (glyph != null)
            {
                image.sprite = Sprite(glyph);
                image.preserveAspect = true;
            }
            return image;
        }

        public static void Surface(GameObject target, string art)
        {
            if (target == null) return;
            var image = target.GetComponent<Image>();
            if (image == null)
            {
                image = target.AddComponent<Image>();
                image.raycastTarget = false;
            }
            image.sprite = Sprite(art);
            image.type = image.sprite != null && image.sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.color = image.sprite != null ? Color.white : Parchment;
            image.pixelsPerUnitMultiplier = art == "Frame" ? .82f : 1;
        }

        public static Text Label(Transform parent, string name, int size, TextAnchor alignment)
        {
            var text = Rect(parent, name, Vector2.zero, Vector2.one).gameObject.AddComponent<Text>();
            text.alignment = alignment;
            text.raycastTarget = false;
            StyleText(text, size);
            return text;
        }

        public static void StyleText(Text text, int size, bool strong = false)
        {
            if (text == null) return;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Ink;
            text.fontSize = size;
            text.fontStyle = strong ? FontStyle.Bold : FontStyle.Normal;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Min(12, size);
            text.resizeTextMaxSize = size;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        public static void StyleButton(Button button, int size, string icon = null)
        {
            if (button == null) return;
            Surface(button.gameObject, "Button");
            button.targetGraphic = button.GetComponent<Image>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(255, 239, 197, 255);
            colors.pressedColor = new Color32(224, 187, 142, 255);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color32(184, 157, 132, 190);
            button.colors = colors;
            var text = button.GetComponentInChildren<Text>(true);
            StyleText(text, size, true);
            if (text != null) text.alignment = TextAnchor.MiddleCenter;
            if (icon == null) return;
            var glyph = Image(button.transform, icon + " emblem", icon);
            if (text != null && icon != "Pause")
            {
                Pin(glyph.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(16, 0), new Vector2(27, 27));
                Stretch(text.rectTransform, new Vector2(.20f, .09f), new Vector2(.95f, .91f));
            }
            else
            {
                Stretch(glyph.rectTransform, new Vector2(.19f, .19f), new Vector2(.81f, .81f));
                if (text != null) text.enabled = false;
            }
        }
    }
}
#endif
