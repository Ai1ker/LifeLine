using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lifeline
{
    /// <summary>Presentation only: normalized timing always comes from the station's authoritative clock.</summary>
    public sealed class QteStationView : MonoBehaviour
    {
        public QteVisualTheme theme;
        public GameObject laneRoot, circleRoot;
        public Image laneBackground, laneWindow, laneHitLine;
        public Image laneKeycap, laneGlyph;
        public Text laneKeyLabel;
        public Image ringTrack, ringWindow, ringFace, ringNeedle, ringHub;
        public Image centerKeycap, centerGlyph;
        public Text centerKeyLabel, centerValue;
        public RectTransform needlePivot;
        [Range(0, 1)] public float phase, windowCenter, windowWidth;
        public string displayedBindingPath;
        public bool circular;

        public static float LanePosition(float elapsed, float nextCue, float interval, float hit, float travel)
            => Mathf.Clamp01(hit + (elapsed - nextCue) / Mathf.Max(.001f, interval) * travel);

        public void SetMode(bool circle)
        {
            circular = circle;
            if (laneRoot != null) laneRoot.SetActive(!circle);
            if (circleRoot != null) circleRoot.SetActive(circle);
        }

        public void ShowLane(CrewInputReader input, int direction, float elapsed, float nextCue, float interval,
            float halfWindowSeconds, float feedback, bool good)
        {
            if (theme == null) return;
            gameObject.SetActive(true); SetMode(false);
            phase = LanePosition(elapsed, nextCue, interval, theme.laneHitPosition, theme.laneTravelPerCycle);
            windowCenter = theme.laneHitPosition;
            windowWidth = halfWindowSeconds * 2 / Mathf.Max(.001f, interval) * theme.laneTravelPerCycle;
            laneBackground.sprite = theme.lane; laneBackground.color = Color.white;
            SetRange(laneWindow.rectTransform, windowCenter, windowWidth);
            laneWindow.color = new Color(theme.success.r, theme.success.g, theme.success.b, .22f);
            laneHitLine.sprite = theme.hitLine; laneHitLine.color = theme.gold;
            SetX(laneHitLine.rectTransform, windowCenter);
            SetX(laneKeycap.rectTransform, phase);
            BindKey(input, CrewInputDefaults.NoteAction(direction), CrewInputDefaults.NoteBinding(direction), laneKeycap, laneGlyph, laneKeyLabel, feedback, good);
        }

        public void ShowCircle(CrewInputReader input, float cursor, float center, float width, float progress,
            float feedback, bool good, bool automatic = false)
        {
            if (theme == null) return;
            gameObject.SetActive(true); SetMode(true);
            phase = Mathf.Repeat(cursor, 1); windowCenter = center; windowWidth = width;
            Color accent = feedback > 0 ? good ? theme.success : theme.failure : theme.gold;
            ringTrack.sprite = theme.ring; ringTrack.color = feedback > 0 && !good ? theme.failure : theme.trackColor;
            ringWindow.sprite = theme.ring; ringWindow.color = theme.success;
            ringWindow.type = Image.Type.Filled; ringWindow.fillMethod = Image.FillMethod.Radial360;
            ringWindow.fillOrigin = (int)Image.Origin360.Top; ringWindow.fillClockwise = true;
            ringWindow.fillAmount = Mathf.Clamp01(width);
            ringWindow.rectTransform.localRotation = Quaternion.Euler(0, 0, -360 * (center - width * .5f));
            ringFace.sprite = theme.ringFace; ringFace.color = theme.faceColor;
            ringNeedle.sprite = theme.needle; ringNeedle.color = accent;
            needlePivot.localRotation = Quaternion.Euler(0, 0, -360 * phase);
            ringHub.sprite = theme.hub; ringHub.color = accent;
            centerValue.text = progress.ToString("P0"); centerValue.color = accent;
            centerKeycap.gameObject.SetActive(!automatic);
            ringWindow.enabled = ringNeedle.enabled = !automatic;
            if (!automatic) BindKey(input, "Calibrate", 0, centerKeycap, centerGlyph, centerKeyLabel, feedback, good);
        }

        void BindKey(CrewInputReader input, string actionName, int index, Image cap, Image glyph, Text label, float feedback, bool good)
        {
            displayedBindingPath = input?.GetBindingPath(actionName, index) ?? "";
            string name = input != null ? input.GetBindingLabel(actionName, index) : "—";
            var artwork = theme.FindArtwork(displayedBindingPath);
            cap.sprite = artwork != null && artwork.sprite != null ? artwork.sprite : feedback > 0
                ? good ? theme.keycapSuccess : theme.keycapFailure : theme.keycap;
            cap.color = Color.white;
            glyph.sprite = artwork != null && artwork.sprite != null ? null : theme.Arrow(displayedBindingPath);
            Color ink = feedback > 0 ? good ? theme.successKeyText : theme.failureKeyText : theme.keyText;
            glyph.enabled = glyph.sprite != null; glyph.color = ink;
            label.enabled = !glyph.enabled && (artwork == null || artwork.sprite == null || artwork.showLabel);
            label.text = name.ToUpperInvariant(); label.color = ink;
            if (theme.keyFont != null) label.font = theme.keyFont;
            Vector2 size = theme.keySize;
            if (displayedBindingPath == "<Keyboard>/space" || name.Length > 3) size.x *= 1.7f;
            if (artwork != null && artwork.size.x > 0 && artwork.size.y > 0) size = artwork.size;
            cap.rectTransform.sizeDelta = size;
        }

        static void SetX(RectTransform rect, float position)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(position, .5f);
            rect.anchoredPosition = Vector2.zero;
        }
        static void SetRange(RectTransform rect, float center, float width)
        {
            rect.anchorMin = new Vector2(Mathf.Clamp01(center - width * .5f), 0);
            rect.anchorMax = new Vector2(Mathf.Clamp01(center + width * .5f), 1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
