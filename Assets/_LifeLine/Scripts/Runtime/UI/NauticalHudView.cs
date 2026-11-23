using UnityEngine;

namespace Lifeline
{
    /// <summary>Switches the authored HUD between shared, split and online camera viewports.</summary>
    [DisallowMultipleComponent]
    public sealed class NauticalHudView : MonoBehaviour
    {
        public GameHUDController hud;
        public NauticalStatusCard primary, secondary;
        public RectTransform viewportRoot, primaryViewport, secondaryViewport, guidance;
        public RectTransform captainStationViewport, diverStationViewport;
        [Header("Viewport-dependent offsets")]
        public float guidanceTop = 54f, narrowGuidanceTop = 274f;
        public float stationTopInset = 242f, narrowStationTopInset = 272f;
        public Vector2 splitStationWidth = new Vector2(.017f, .483f);
        public Vector2 singleStationWidth = new Vector2(.22f, .78f);
        public float stationBottom = .145f, onlineStationBottom = .10f;
        public float maximumStationTop = .78f;

        public void Refresh()
        {
            if (hud == null || hud.session == null || hud.session.tuning == null) return;
            var authority = hud.authority;
            var rig = authority != null ? authority.cameraRig : null;
            bool online = authority != null && authority.onlineView;
            bool split = !online && rig != null && rig.IsSplitScreen;
            bool show = !OnlineVoyage.IsGuest || OnlineVoyage.Current.GuestReady;
            Rect left = split && rig.captainCamera != null ? rig.captainCamera.rect : new Rect(0, 0, 1, 1);
            Rect right = split && rig.diverCamera != null ? rig.diverCamera.rect : new Rect(.5f, 0, .5f, 1);
            SetViewport(primaryViewport, left, show);
            SetViewport(secondaryViewport, right, show && split);
            if (show) primary.Refresh(hud.session, online ? authority.localRole : CrewRole.Captain, !online && !split);
            if (show && split) secondary.Refresh(hud.session, CrewRole.Diver, false);
            LayoutGuidance(left.width);
            bool twoPanels = !online && hud.captainPanel?.IsOpen == true && hud.diverPanel?.IsOpen == true;
            LayoutStations(split || twoPanels, online);
        }

        static void SetViewport(RectTransform root, Rect rect, bool visible)
        {
            if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
            if (!visible) return;
            root.anchorMin = rect.min;
            root.anchorMax = rect.max;
        }

        void LayoutGuidance(float leftFraction)
        {
            bool narrow = primary.NeedsTwoResourceRows(viewportRoot.rect.width * leftFraction);
            var position = guidance.anchoredPosition;
            position.y = narrow ? -narrowGuidanceTop : -guidanceTop;
            guidance.anchoredPosition = position;
        }

        void LayoutStations(bool columns, bool online)
        {
            float width = viewportRoot.rect.width * (columns ? .5f : 1);
            float inset = primary.NeedsTwoResourceRows(width) ? narrowStationTopInset : stationTopInset;
            float top = Mathf.Clamp(1 - inset / Mathf.Max(1, viewportRoot.rect.height), .58f, maximumStationTop);
            float bottom = online ? onlineStationBottom : stationBottom;
            var horizontal = columns ? splitStationWidth : singleStationWidth;
            captainStationViewport.anchorMin = new Vector2(horizontal.x, bottom);
            captainStationViewport.anchorMax = new Vector2(horizontal.y, top);
            if (columns) horizontal += Vector2.one * .5f;
            diverStationViewport.anchorMin = new Vector2(horizontal.x, bottom);
            diverStationViewport.anchorMax = new Vector2(horizontal.y, top);
        }
    }
}
