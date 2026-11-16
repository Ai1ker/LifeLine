using UnityEngine;
using UnityEngine.UI;

namespace Lifeline
{
    /// <summary>Replaceable linear sea-area map. The view reads the simulation and never moves the ship.</summary>
    public sealed class ShipRouteView : MonoBehaviour
    {
        public GameSession session;
        public RectTransform track, boatMarker, reachableRange;
        public RectTransform[] seaSegments = new RectTransform[3];
        public Text positionText;
        public Text[] areaLabels = new Text[3];
        public Color shallowColor = new Color(.34f, .76f, .72f);
        public Color middleColor = new Color(.28f, .62f, .77f);
        public Color deepColor = new Color(.22f, .43f, .63f);
        public Color ink = new Color(.12f, .23f, .26f);
        public Vector2 ReachableNormalized { get; private set; }

        public void BuildView(GameSession owner)
        {
            session = owner;
            if (track == null) track = Rect("Sea areas", transform, new Vector2(0, .40f), new Vector2(1, .58f));
            var colors = new[] { shallowColor, middleColor, deepColor };
            for (int i = 0; i < 3; i++)
            {
                if (seaSegments[i] == null) seaSegments[i] = Rect("Sea area " + i, track, Vector2.zero, Vector2.one);
                Image image = seaSegments[i].GetComponent<Image>();
                if (image == null) image = seaSegments[i].gameObject.AddComponent<Image>();
                image.color = colors[i]; image.raycastTarget = false;
                if (areaLabels[i] == null) areaLabels[i] = Label("Area name " + i, transform, Vector2.zero, Vector2.one, 12, TextAnchor.MiddleCenter);
            }
            if (reachableRange == null)
            {
                reachableRange = Rect("Reachable with current power", track, Vector2.zero, Vector2.one);
                var image = reachableRange.gameObject.AddComponent<Image>(); image.color = new Color(.91f, 1, .76f, .47f); image.raycastTarget = false;
            }
            if (boatMarker == null)
            {
                boatMarker = Rect("Boat position", track, Vector2.zero, Vector2.one);
                var image = boatMarker.gameObject.AddComponent<Image>(); image.color = new Color(1, .73f, .23f); image.raycastTarget = false;
            }
            if (positionText == null) positionText = Label("Position and area", transform, new Vector2(0, .60f), Vector2.one, 15, TextAnchor.MiddleLeft);
            RefreshNow();
        }

        void LateUpdate() => RefreshNow();
        public void RefreshNow()
        {
            if (session == null || session.ship == null || session.tuning == null || track == null) return;
            float distance = Mathf.Max(1, session.tuning.routeDistance);
            float x = session.ship.Position.x;
            float p = Mathf.Clamp01(x / distance);
            float first = Mathf.Clamp01(session.tuning.shallowBoundary / distance);
            float second = Mathf.Clamp(session.tuning.deepBoundary / distance, first, 1);
            float[] edges = { 0, first, second, 1 };
            string[] names = { "浅滩", "城区", "深水区" };
            for (int i = 0; i < 3; i++)
            {
                if (seaSegments[i] != null) Stretch(seaSegments[i], new Vector2(edges[i], 0), new Vector2(edges[i + 1], 1));
                if (areaLabels[i] != null)
                {
                    Stretch(areaLabels[i].rectTransform, new Vector2(edges[i], 0), new Vector2(edges[i + 1], .40f));
                    areaLabels[i].text = names[i];
                }
            }
            float reach = session.infinitePower ? distance : Mathf.Max(0, session.power) / Mathf.Max(.01f, session.tuning.routePowerCost) * distance;
            ReachableNormalized = new Vector2(Mathf.Clamp01((x - reach) / distance), Mathf.Clamp01((x + reach) / distance));
            if (reachableRange != null) Stretch(reachableRange, new Vector2(ReachableNormalized.x, .25f), new Vector2(ReachableNormalized.y, .75f));
            if (boatMarker != null)
            {
                Stretch(boatMarker, new Vector2(p, -.35f), new Vector2(p, 1.35f));
                boatMarker.sizeDelta = new Vector2(5, 0);
            }
            if (positionText != null) positionText.text = "船位 " + x.ToString("0.0") + " / " + distance.ToString("0") + "m · " + names[x < session.tuning.shallowBoundary ? 0 : x < session.tuning.deepBoundary ? 1 : 2] + " · 可达范围";
        }

        static void Stretch(RectTransform rect, Vector2 min, Vector2 max) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); Stretch(rect, min, max); return rect;
        }
        Text Label(string name, Transform parent, Vector2 min, Vector2 max, int size, TextAnchor alignment)
        {
            var label = Rect(name, parent, min, max).gameObject.AddComponent<Text>();
            label.font = Application.isPlaying ? RuntimeUIFont.GetOrCreateFont() : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size; label.color = ink; label.alignment = alignment; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Truncate; return label;
        }
    }
}
