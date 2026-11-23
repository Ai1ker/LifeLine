using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lifeline
{
    /// <summary>Updates data on the authored VoyageStatusCard prefab.</summary>
    public sealed class NauticalStatusCard : MonoBehaviour
    {
        [Serializable]
        public sealed class ResourceSlot
        {
            public ResourceKind kind;
            public Text itemName, amount;
        }

        [Serializable]
        public sealed class Gauge
        {
            public GameObject root;
            public Text label, amount;
            public RectTransform fill;

            public void Set(string title, float current, float max, string unit, bool unlimited = false)
            {
                label.text = UiLocalization.Text(title);
                amount.text = unlimited ? "∞" : $"{current:0} / {max:0}{unit}";
                fill.anchorMax = new Vector2(unlimited ? 1 : max > 0 ? Mathf.Clamp01(current / max) : 0, 1);
            }
        }

        public ResourceSlot[] resources;
        public Gauge hull, power, primaryOxygen, secondaryOxygen;
        public Text day, routeLabel;
        public RectTransform needle, boat, reach;
        public RectTransform[] seaAreas;
        public GridLayoutGroup resourceGrid;
        public LayoutElement resourcePlate, gaugePlate, routePlate;
        public float resourceMaxWidth = 662;
        public float resourceMinWidthForSingleRow = 630;
        public float resourceWidthReserve = 65;
        public float singleRowHeight = 39, stackedRowHeight = 68;
        public float singleRoleHeight = 108, bothRolesHeight = 133;
        public static readonly string[] AreaNames = { "浅滩", "城区", "深水区" };
        float previousWidth = -1;
        bool previousShared;

        public void Refresh(GameSession game, CrewRole role, bool showBoth)
        {
            RefreshLayout(showBoth);
            foreach (ResourceSlot slot in resources)
            {
                if (slot.kind == ResourceKind.Eel) continue;
                slot.itemName.text = UiLocalization.Text(GameNames.Resource(slot.kind));
                slot.amount.text = game.GetResource(slot.kind).ToString();
            }
            hull.Set("船体耐久", game.hull, game.hullMax, "");
            power.Set("电力", game.power, game.tuning.maxPower, "%", game.infinitePower);
            bool captain = role == CrewRole.Captain || showBoth;
            UpdateOxygen(primaryOxygen, captain ? game.captain : game.diver, captain);
            if (showBoth) UpdateOxygen(secondaryOxygen, game.diver, false);
            day.text = string.Format(UiLocalization.Text("hud.nautical.day"), game.day);
            needle.localRotation = Quaternion.Euler(0, 0, 90 - 360 * game.DayProgress);
            RefreshRoute(game);
        }

        public bool NeedsTwoResourceRows(float viewportWidth) => viewportWidth - resourceWidthReserve < resourceMinWidthForSingleRow;

        void RefreshLayout(bool showBoth)
        {
            float width = ((RectTransform)transform).rect.width;
            if (Mathf.Approximately(width, previousWidth) && showBoth == previousShared) return;
            previousWidth = width;
            previousShared = showBoth;
            resourcePlate.preferredWidth = Mathf.Clamp(width - resourceWidthReserve, 255, resourceMaxWidth);
            bool stacked = NeedsTwoResourceRows(width);
            resourcePlate.preferredHeight = stacked ? stackedRowHeight : singleRowHeight;
            gaugePlate.preferredHeight = showBoth ? bothRolesHeight : singleRoleHeight;
            secondaryOxygen.root.SetActive(showBoth);
            int columns = stacked ? 5 : resources.Length;
            resourceGrid.constraintCount = columns;
            // Fit authored cells into the actual camera viewport, including narrow windows.
            Vector2 spacing = resourceGrid.spacing;
            var cellWidth = (resourcePlate.preferredWidth - resourceGrid.padding.horizontal - (columns - 1) * spacing.x) / columns;
            int rows = stacked ? 2 : 1;
            var cellHeight = (resourcePlate.preferredHeight - resourceGrid.padding.vertical - (rows - 1) * spacing.y) / rows;
            resourceGrid.cellSize = new Vector2(cellWidth, cellHeight);
        }

        static void UpdateOxygen(Gauge gauge, CrewActor actor, bool captain)
        {
            if (actor != null) gauge.Set(captain ? "船长氧气" : "潜水员氧气", actor.oxygen, actor.oxygenMax, "s");
        }

        void RefreshRoute(GameSession game)
        {
            if (game.ship == null) return;
            var tuning = game.tuning;
            float length = Mathf.Max(1, tuning.routeDistance);
            float x = game.ship.Position.x;
            float position = Mathf.Clamp01(x / length);
            float first = Mathf.Clamp01(tuning.shallowBoundary / length);
            float second = Mathf.Clamp(tuning.deepBoundary / length, first, 1);
            SetSpan(seaAreas[0], 0, first);
            SetSpan(seaAreas[1], first, second);
            SetSpan(seaAreas[2], second, 1);
            float range = game.infinitePower ? 1 : game.power / Mathf.Max(.01f, tuning.routePowerCost);
            SetSpan(reach, Mathf.Clamp01(position - range), Mathf.Clamp01(position + range));
            boat.anchorMin = new Vector2(position, boat.anchorMin.y);
            boat.anchorMax = new Vector2(position, boat.anchorMax.y);
            int area = x < tuning.shallowBoundary ? 0 : x < tuning.deepBoundary ? 1 : 2;
            routeLabel.text = UiLocalization.Text(AreaNames[area]) + $"  ·  {x:0.#} / {length:0} m";
        }

        static void SetSpan(RectTransform rect, float start, float end)
        {
            rect.anchorMin = new Vector2(start, rect.anchorMin.y);
            rect.anchorMax = new Vector2(end, rect.anchorMax.y);
        }
    }
}
