using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Lifeline
{
    /// <summary>Projects crew prompts into the relevant camera viewport and keeps the dive inventory local.</summary>
    [DisallowMultipleComponent]
    public sealed class CrewScreenOverlay : MonoBehaviour
    {
        public Color captainPromptColor = new Color(1f, .85f, .48f);
        public Color diverPromptColor = new Color(.63f, 1f, .93f);
        public float promptHeight = 1.15f;
        public float nameHeight = 1.1f;
        GameHUDController hud;
        public RectTransform screen, footer, bag, onlineBag;
        public Text bagTitle, captainBag;
        public Text[] prompts = new Text[2], names = new Text[2], ropeLabels = new Text[2], craftNotices = new Text[2];
        public List<Text> slots = new();
        public RectTransform inventorySlotPrefab;
        public Color occupiedSlotColor = Color.white;
        public Color emptySlotColor = new Color32(222, 201, 180, 255);
        bool online, bagOpen;
        int arrangedCapacity = -1;
        public bool BackpackOpen => online && bagOpen;

        public void Initialize(GameHUDController owner)
        {
            hud = owner;
            Configure(false);
        }

        public void Configure(bool useOnlineLayout)
        {
            online = useOnlineLayout;
            if (footer != null) footer.gameObject.SetActive(!online);
            if (bag == null) return;
            bag.SetParent(online ? onlineBag : footer != null ? footer : screen, false);
            arrangedCapacity = -1;
            RefreshBackpack();
        }

        public void ToggleBackpack()
        {
            if (!online || hud.authority.localRole != CrewRole.Diver || hud.session.phase != SessionPhase.Playing) return;
            bagOpen = !bagOpen;
            RefreshBackpack();
        }

        public void RefreshBackpack()
        {
            if (bag == null || onlineBag == null || bagTitle == null) return;
            var actor = hud.session.diver;
            bool guestWaiting = OnlineVoyage.IsGuest && OnlineVoyage.WaitingForCrew;
            bool show = actor != null && (!online || hud.authority.localRole == CrewRole.Diver && bagOpen && !guestWaiting);
            onlineBag.gameObject.SetActive(online && show);
            bag.gameObject.SetActive(show);
            if (!show) return;
            int capacity = Mathf.Max(actor.BagCapacity, actor.backpack.Count);
            bagTitle.text = string.Format(UiLocalization.Text("潜水员背包 {0}/{1}"), actor.backpack.Count, actor.BagCapacity);
            if (arrangedCapacity != capacity) ArrangeSlots(capacity);
            for (int index = 0; index < capacity; index++)
            {
                bool filled = index < actor.backpack.Count;
                string item = filled ? GameNames.Resource(actor.backpack[index]) : "空";
                slots[index].text = (index + 1) + "  " + UiLocalization.Text(item);
                slots[index].rectTransform.parent.GetComponent<Image>().color = filled
                    ? occupiedSlotColor : emptySlotColor;
            }
        }

        void ArrangeSlots(int capacity)
        {
            arrangedCapacity = capacity;
            int columns = Mathf.Clamp(capacity, 1, online ? 6 : 10);
            int rows = Mathf.Max(1, Mathf.CeilToInt(capacity / (float)columns));
            while (slots.Count < capacity)
            {
                var tile = Instantiate(inventorySlotPrefab, bag);
                slots.Add(tile.GetComponentInChildren<Text>(true));
            }
            for (int index = 0; index < slots.Count; index++)
            {
                var tile = (RectTransform)slots[index].rectTransform.parent;
                tile.gameObject.SetActive(index < capacity);
                if (index >= capacity) continue;
                int row = index / columns, column = index % columns;
                float top = .72f - row * .72f / rows;
                Stretch(tile, new Vector2(column / (float)columns + .004f, top - .72f / rows + .045f),
                    new Vector2((column + 1f) / columns - .004f, top));
            }
        }

        void LateUpdate()
        {
            if (screen == null || hud == null || hud.authority?.cameraRig == null) return;
            var game = hud.session;
            var rig = hud.authority.cameraRig;
            bool active = game.phase == SessionPhase.Playing &&
                (!OnlineVoyage.IsGuest || OnlineVoyage.Current.GuestReady && !OnlineVoyage.WaitingForCrew) &&
                (!online || OnlineVoyage.Current == null || !OnlineVoyage.Current.LocalMenuOpen);
            CrewActor viewer = online && hud.authority.localRole == CrewRole.Diver ? game.diver : game.captain;
            Camera onlineCamera = viewer == game.diver ? rig.diverCamera : rig.captainCamera;
            Vector3 firstPrompt = default;
            bool hasFirstPrompt = false;

            foreach (var actor in game.Crew)
            {
                if (actor == null) continue;
                int index = (int)actor.role;
                Camera view = online ? onlineCamera : rig.IsSplitScreen && actor.role == CrewRole.Diver ? rig.diverCamera : rig.captainCamera;
                bool owned = active && (!online || actor == viewer);
                bool underwater = owned && !actor.onboard && !game.IsHidden(actor);
                var pose = actor.transform.position;
                ropeLabels[index].gameObject.SetActive(underwater && Place(ropeLabels[index], view, pose + Vector3.down * .95f));
                if (underwater) ropeLabels[index].text = Format("绳长 {0:0.0}/{1:0.0}", game.GetDisplayedRopeLength(actor), game.ropeLength);
                if (actor.role == CrewRole.Captain)
                {
                    bool showBag = underwater && !online;
                    captainBag.gameObject.SetActive(showBag && Place(captainBag, view, pose + Vector3.up * .72f));
                    if (showBag) captainBag.text = Format("背包{0}/{1}", actor.backpack.Count, actor.BagCapacity);
                }
                bool craftFailed = owned && Time.unscaledTime < actor.craftFailureUntil;
                craftNotices[index].gameObject.SetActive(craftFailed && Place(craftNotices[index], view, pose + Vector3.up * (online ? 1.75f : 1.25f)));
                if (craftFailed) craftNotices[index].text = UiLocalization.Text(CraftingManager.CraftFailureNotice);
                bool showName = active && online && viewer != null && actor.onboard == viewer.onboard && !game.IsHidden(actor);
                float nameOffset = nameHeight;
                if (showName && actor != viewer && (actor.transform.position - viewer.transform.position).sqrMagnitude < .6f)
                    nameOffset += .55f;
                names[index].gameObject.SetActive(showName && Place(names[index], view, actor.transform.position + Vector3.up * nameOffset));
                if (showName) names[index].text = hud.authority.NameFor(actor.role);
                var panel = actor.role == CrewRole.Captain ? hud.captainPanel : hud.diverPanel;
                bool canShowPrompt = owned && (panel == null || !panel.IsOpen || game.FindReviveTarget(actor) != null);
                if (canShowPrompt && TryGetPrompt(actor, out Vector3 point, out string prompt))
                {
                    prompts[index].text = prompt;
                    float height = promptHeight;
                    if (hasFirstPrompt && !rig.IsSplitScreen && (point - firstPrompt).sqrMagnitude < .6f) height += .8f;
                    Vector3 labelPoint = point + Vector3.up * height;
                    if (actor.onboard && game.ship != null)
                    {
                        float deck = game.ShipPoint(Vector3.up * actor.deckY).y;
                        if (point.y < deck - 1.4f) labelPoint.y = Mathf.Min(labelPoint.y, deck - .45f);
                    }
                    bool visible = Place(prompts[index], view, labelPoint);
                    prompts[index].gameObject.SetActive(visible);
                    if (visible) { firstPrompt = point; hasFirstPrompt = true; }
                }
                else prompts[index].gameObject.SetActive(false);
            }
        }

        public bool TryGetPrompt(CrewActor actor, out Vector3 target, out string caption)
        {
            target = default;
            caption = null;
            var game = hud.session;
            if (game.IsIncapacitated(actor) || actor.input == null) return false;
            string interact = actor.input.GetBindingLabel("Interact");
            string hideKey = actor.input.HasBinding("Hide") ? actor.input.GetBindingLabel("Hide") : interact;
            if (game.IsHidden(actor))
            {
                target = actor.hidingPosition;
                caption = Format("[{0}] 离开藏匿处", hideKey);
                return true;
            }

            var teammate = game.FindReviveTarget(actor);
            if (teammate != null)
            {
                target = actor.onboard == teammate.onboard ? teammate.transform.position : game.BoatAnchor;
                caption = Format("按住 [{0}] 扶起同伴 · {1:0.0}/{2:0.#} 秒", interact, game.reviveProgress[(int)actor.role], game.tuning.reviveSeconds);
                return true;
            }
            if (actor.controlsCaptured) return false;
            if (actor.onboard)
            {
                var station = actor.NearbyStation;
                if (station == null) return false;
                target = station.InteractionPosition;
                var warning = station.GetInteractionWarning(actor);
                caption = "[" + interact + "] " + UiLocalization.Text(GameNames.Station(station.kind));
                if (!string.IsNullOrEmpty(warning)) caption += "\n" + UiLocalization.Text(warning);
                return true;
            }
            return TryGetWaterPrompt(actor, interact, out target, out caption);
        }

        bool TryGetWaterPrompt(CrewActor actor, string interact, out Vector3 target, out string caption)
        {
            target = default;
            caption = null;
            var game = hud.session;
            var world = game.world;
            if (world == null) return false;
            string collect = actor.input.GetBindingLabel("Collect");
            if (game.TryGetCollectionProgress(actor, out var collecting, out float fraction))
            {
                target = collecting.transform.position;
                caption = CollectionCaption(collecting, interact, collect) + " · " +
                    Mathf.Min(99, Mathf.RoundToInt(fraction * 100f)) + "%";
                return true;
            }
            var fish = world.FindCatchableFish(actor);
            if (fish != null)
            {
                target = fish.transform.position;
                caption = CollectionCaption(fish, interact, collect);
                return true;
            }
            var crate = world.FindNearest(actor, true);
            bool canLoad = crate != null && crate.contents.Count < crate.capacity && actor.backpack.Count > 0;
            var hatch = world.FindTrapdoor(actor);
            var hide = world.FindHideSpot(actor);
            var pickup = world.FindNearest(actor, false);
            if (canLoad)
            {
                target = crate.transform.position;
                caption = Format("[{0}] 装载货箱", interact);
                if (pickup == crate && crate.contents.Count > 0) caption += "\n" + Format("按住 [{0}] 挂绳", collect);
                return true;
            }
            if (hatch != null)
            {
                target = hatch.transform.position;
                if (!hatch.horizontal && hatch.barrier != null) target.y += hatch.barrier.size.y * .5f;
                caption = Format(hatch.open ? "[{0}] 关闭活板门" : "[{0}] 打开活板门", interact);
                return true;
            }
            if (hide != null)
            {
                target = hide.position;
                caption = Format("[{0}] 藏匿", actor.input.HasBinding("Hide") ? actor.input.GetBindingLabel("Hide") : interact);
                return true;
            }
            if (pickup != null)
            {
                target = pickup.transform.position;
                bool usesBackpack = !pickup.isCrate && pickup.GetComponent<FilterSalvage>() == null;
                caption = usesBackpack && actor.backpack.Count >= actor.BagCapacity ? UiLocalization.Text("背包已满") :
                    CollectionCaption(pickup, interact, collect);
                return true;
            }
            if (Vector3.Distance(actor.Position, game.BoatAnchor) > 3f) return false;
            target = game.BoatAnchor;
            caption = actor == game.captain ? Format("按住 [{0}] 回船 · {1:0.0}/{2:0.#} 秒", interact, game.captainBoardProgress, game.captainBoardSeconds) :
                UiLocalization.Text("等待队友用绞盘接回");
            return true;
        }

        static string CollectionCaption(LootPickup pickup, string interact, string collect)
        {
            if (pickup.IsSwimmingFish) return Format("按住 [{0}] 捕捞小鱼", interact);
            if (pickup.isCrate) return Format("按住 [{0}] 挂绳", collect);
            if (pickup.GetComponent<FilterSalvage>() != null) return Format("按住 [{0}] 打捞海水过滤器", collect);
            return Format("按住 [{0}] 收集 · {1}", collect, UiLocalization.Text(pickup.isCabinet ? "残骸物资" : GameNames.Resource(pickup.resource)));
        }

        bool Place(Text label, Camera view, Vector3 point)
        {
            if (view == null || !view.enabled) return false;
            Vector3 viewport = view.WorldToViewportPoint(point);
            if (viewport.z <= 0 || viewport.x < .01f || viewport.x > .99f || viewport.y < .03f || viewport.y > .97f) return false;
            var canvas = hud.GetComponentInParent<Canvas>();
            var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(screen, view.ViewportToScreenPoint(viewport), uiCamera, out Vector2 position)) return false;
            label.rectTransform.anchoredPosition = position;
            return true;
        }

        static string Format(string text, params object[] args) => string.Format(UiLocalization.Text(text), args);
        static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
