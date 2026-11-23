#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Lifeline.NauticalUiSkin;

namespace Lifeline.Editor
{
    public static class VoyageHudAuthoring
    {
        public const string StatusCardPath = GameProjectBuilder.Root + "/Prefabs/UI/VoyageStatusCard.prefab";
        public const string SlotPath = GameProjectBuilder.Root + "/Prefabs/UI/BackpackSlot.prefab";
        static readonly ResourceKind[] ResourcesInOrder =
        {
            ResourceKind.Wood, ResourceKind.Metal, ResourceKind.Fiber, ResourceKind.Fish,
            ResourceKind.Plank, ResourceKind.Plate, ResourceKind.Bait, ResourceKind.FiberBundle, ResourceKind.Fuel
        };

        public static void Install(GameHUDController hud)
        {
            if (hud.nauticalView != null) return;
            OnboardingUiInstaller.Install(hud);
            var view = hud.gameObject.AddComponent<NauticalHudView>();
            hud.nauticalView = view;
            view.hud = hud;
            var root = Rect(hud.transform, "Nautical voyage HUD", Vector2.zero, Vector2.one);
            root.SetSiblingIndex(hud.transform.Find("NightWaterVisibility") != null ? 1 : 0);
            view.viewportRoot = root;
            view.primaryViewport = Rect(root, "Primary viewport HUD", Vector2.zero, Vector2.one);
            view.secondaryViewport = Rect(root, "Secondary viewport HUD", new Vector2(.5f, 0), Vector2.one);
            GameObject prefab = StatusCardPrefab(hud.session.world != null ? hud.session.world.pixelArt : null);
            view.primary = InstallCard(prefab, view.primaryViewport);
            view.secondary = InstallCard(prefab, view.secondaryViewport);
            SetSecondOxygen(view.primary, true);
            SetSecondOxygen(view.secondary, false);
            view.secondaryViewport.gameObject.SetActive(false);
            var pause = (RectTransform)hud.pauseButton.transform;
            pause.SetParent(root, false);
            Pin(pause, Vector2.up, Vector2.up, new Vector2(12, -10), new Vector2(43, 39));
            StyleButton(hud.pauseButton, 17, "Pause");
            RetirePlaceholderHud(hud);
            InstallGuidance(hud, view);
            InstallCrewOverlay(hud);
            InstallConnectionStatus(hud);
            NauticalMenuAuthoring.ApplyPause(hud);
            NauticalMenuAuthoring.ApplySettings(hud.settings);
            NauticalMenuAuthoring.ApplyControls(hud.controls);
            view.captainStationViewport = InstallStation(hud.captainPanel, hud.transform);
            view.diverStationViewport = InstallStation(hud.diverPanel, hud.transform);
        }

        static NauticalStatusCard InstallCard(GameObject prefab, Transform parent)
        {
            var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            return item.GetComponent<NauticalStatusCard>();
        }

        static void SetSecondOxygen(NauticalStatusCard card, bool shared)
        {
            card.gaugePlate.preferredHeight = shared ? card.bothRolesHeight : card.singleRoleHeight;
            card.secondaryOxygen.root.SetActive(shared);
            PrefabUtility.RecordPrefabInstancePropertyModifications(card.gaugePlate);
            PrefabUtility.RecordPrefabInstancePropertyModifications(card.secondaryOxygen.root);
        }

        static GameObject StatusCardPrefab(PixelWorldArt art)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StatusCardPath);
            if (existing != null) return existing;
            var root = Rect(null, "VoyageStatusCard", Vector2.zero, Vector2.one);
            try
            {
                var card = root.gameObject.AddComponent<NauticalStatusCard>();
                var stack = Rect(root, "Top right layout", Vector2.one, Vector2.one);
                Pin(stack, Vector2.one, Vector2.one, new Vector2(-13, -10), new Vector2(662, 250));
                var deck = stack.gameObject.AddComponent<VerticalLayoutGroup>();
                deck.spacing = 6;
                deck.childAlignment = TextAnchor.UpperRight;
                deck.childControlHeight = deck.childControlWidth = true;
                deck.childForceExpandHeight = deck.childForceExpandWidth = false;
                var resources = Rect(stack, "Resources", Vector2.zero, Vector2.one);
                Surface(resources.gameObject, "Plate");
                card.resourcePlate = Size(resources, 662, 39);
                var itemRoot = Rect(resources, "Item icons and quantities", Vector2.zero, Vector2.one);
                card.resourceGrid = itemRoot.gameObject.AddComponent<GridLayoutGroup>();
                card.resourceGrid.padding = new RectOffset(8, 8, 5, 5);
                card.resourceGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                card.resourceGrid.constraintCount = ResourcesInOrder.Length;
                card.resourceGrid.cellSize = new Vector2((662f - 16) / ResourcesInOrder.Length, 29);
                card.resources = new NauticalStatusCard.ResourceSlot[ResourcesInOrder.Length];
                for (int index = 0; index < ResourcesInOrder.Length; index++)
                    card.resources[index] = ResourceTile(itemRoot, ResourcesInOrder[index], art);

                var board = Rect(stack, "Voyage gauges", Vector2.zero, Vector2.one);
                Surface(board.gameObject, "Frame");
                card.gaugePlate = Size(board, 375, 108);
                var dial = Image(board, "Day and night clock", "Clock");
                Pin(dial.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(9, 8), new Vector2(78, 78));
                card.needle = Image(dial.transform, "Time of day needle", "Needle").rectTransform;
                card.needle.localRotation = Quaternion.Euler(0, 0, 90);
                card.day = Label(board, "Day count", 13, TextAnchor.MiddleCenter);
                card.day.text = "第 1 天";
                Pin(card.day.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(9, -40), new Vector2(78, 16));
                var readings = Rect(board, "Hull, power and oxygen", new Vector2(.268f, .09f), new Vector2(.973f, .91f));
                var rows = readings.gameObject.AddComponent<VerticalLayoutGroup>();
                rows.childControlHeight = rows.childControlWidth = true;
                rows.childForceExpandHeight = rows.childForceExpandWidth = true;
                card.hull = MakeGauge(readings, "Hull gauge", "Hull", "船体耐久", "70 / 100", .7f, new Color32(212, 79, 64, 255));
                card.power = MakeGauge(readings, "Power gauge", "Power", "电力", "0 / 100%", 0, new Color32(228, 165, 55, 255));
                card.primaryOxygen = MakeGauge(readings, "Primary oxygen gauge", "Oxygen", "船长氧气", "50 / 50s", 1, new Color32(56, 158, 197, 255));
                card.secondaryOxygen = MakeGauge(readings, "Diver oxygen gauge", "Oxygen", "潜水员氧气", "50 / 50s", 1, new Color32(64, 182, 183, 255));
                card.secondaryOxygen.root.SetActive(false);
                InstallRoute(card, stack);
                return PrefabUtility.SaveAsPrefabAsset(root.gameObject, StatusCardPath);
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }

        static NauticalStatusCard.ResourceSlot ResourceTile(Transform parent, ResourceKind kind, PixelWorldArt art)
        {
            var tile = Rect(parent, kind.ToString(), Vector2.zero, Vector2.one);
            var icon = Image(tile, "Item art");
            icon.sprite = kind switch
            {
                ResourceKind.Wood => art?.wood ?? AssetDatabase.LoadAssetAtPath<Sprite>(GameProjectBuilder.Root + "/Art/Items/StaticSprites/Wood.png"),
                ResourceKind.Metal => art?.metal ?? AssetDatabase.LoadAssetAtPath<Sprite>(GameProjectBuilder.Root + "/Art/Items/StaticSprites/Metal.png"),
                ResourceKind.Fiber => art?.fiber ?? AssetDatabase.LoadAssetAtPath<Sprite>(GameProjectBuilder.Root + "/Art/Items/StaticSprites/Fiber.png"),
                ResourceKind.Fish => art?.fish ?? AssetDatabase.LoadAssetAtPath<Sprite>(GameProjectBuilder.Root + "/Art/Items/StaticSprites/Fish.png"),
                ResourceKind.Fuel => art?.fuel ?? AssetDatabase.LoadAssetAtPath<Sprite>(GameProjectBuilder.Root + "/Art/Items/StaticSprites/Fuel.png"),
                ResourceKind.Plank => Sprite("Plank"),
                ResourceKind.Plate => Sprite("PlateItem"),
                ResourceKind.FiberBundle => Sprite("Bundle"),
                ResourceKind.Bait => Sprite("Bait"),
                _ => null
            };
            icon.preserveAspect = true;
            Stretch(icon.rectTransform, new Vector2(.02f, .15f), new Vector2(.285f, .87f));
            var label = Label(tile, "Item name", 12, TextAnchor.MiddleLeft);
            label.resizeTextMinSize = 7;
            label.text = GameNames.Resource(kind);
            Stretch(label.rectTransform, new Vector2(.29f, .47f), new Vector2(.99f, .99f));
            var amount = Label(tile, "Item count", 15, TextAnchor.MiddleLeft);
            amount.text = "0";
            Stretch(amount.rectTransform, new Vector2(.29f, .02f), new Vector2(.98f, .53f));
            return new NauticalStatusCard.ResourceSlot { kind = kind, itemName = label, amount = amount };
        }

        static LayoutElement Size(RectTransform rect, float width, float height)
        {
            var size = rect.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = width;
            size.preferredHeight = height;
            return size;
        }

        static NauticalStatusCard.Gauge MakeGauge(Transform parent, string name, string art, string title,
            string value, float previewFill, Color tint)
        {
            var root = Rect(parent, name, Vector2.zero, Vector2.one);
            var icon = Image(root, art + " icon", art);
            Pin(icon.rectTransform, new Vector2(0, .54f), new Vector2(0, .5f), Vector2.zero, new Vector2(17, 17));
            var label = Label(root, "Name", 15, TextAnchor.MiddleLeft);
            label.text = title;
            Stretch(label.rectTransform, new Vector2(.076f, .30f), new Vector2(.63f, .98f));
            var amount = Label(root, "Value", 15, TextAnchor.MiddleRight);
            amount.text = value;
            Stretch(amount.rectTransform, new Vector2(.61f, .30f), new Vector2(1, .98f));
            var track = Image(root, "Gauge track");
            Stretch(track.rectTransform, new Vector2(.078f, .09f), new Vector2(1, .32f));
            track.color = Track;
            var fill = Image(track.transform, "Gauge fill");
            fill.color = tint;
            Stretch(fill.rectTransform, Vector2.zero, new Vector2(previewFill, 1));
            var sheen = Image(fill.transform, "Gauge highlight");
            Stretch(sheen.rectTransform, new Vector2(0, .65f), Vector2.one);
            sheen.color = new Color32(255, 222, 170, 85);
            return new NauticalStatusCard.Gauge { root = root.gameObject, label = label, amount = amount, fill = fill.rectTransform };
        }

        static void InstallRoute(NauticalStatusCard card, Transform stack)
        {
            var route = Rect(stack, "Sea areas and boat position", Vector2.zero, Vector2.one);
            Surface(route.gameObject, "Plate");
            card.routePlate = Size(route, 375, 43);
            var waves = Rect(route, "Three sea regions", new Vector2(.043f, .49f), new Vector2(.957f, .76f));
            var palette = new Color32[] { new(111, 211, 165, 255), new(82, 185, 219, 255), new(67, 113, 178, 255) };
            card.seaAreas = new RectTransform[3];
            for (int i = 0; i < card.seaAreas.Length; i++)
            {
                var area = Image(waves, "Sea region " + i, "Wave");
                area.preserveAspect = false;
                area.color = palette[i];
                Stretch(area.rectTransform, new Vector2(i / 3f, 0), new Vector2((i + 1) / 3f, 1));
                card.seaAreas[i] = area.rectTransform;
            }
            var reach = Image(waves, "Range with current power");
            reach.color = new Color32(255, 233, 177, 135);
            card.reach = reach.rectTransform;
            Stretch(card.reach, new Vector2(0, .06f), new Vector2(0, .19f));
            card.boat = Image(waves, "Ship marker", "Boat").rectTransform;
            Pin(card.boat, new Vector2(0, .36f), new Vector2(.5f, 0), Vector2.zero, new Vector2(24, 23));
            card.routeLabel = Label(route, "Sea area and ship distance", 13, TextAnchor.MiddleCenter);
            card.routeLabel.text = "浅滩  ·  0 / 200 m";
            Stretch(card.routeLabel.rectTransform, new Vector2(.04f, .09f), new Vector2(.96f, .48f));
        }

        static void RetirePlaceholderHud(GameHUDController hud)
        {
            if (hud.bankText != null && hud.clockText != null && hud.bankText.transform.parent == hud.clockText.transform.parent &&
                hud.bankText.transform.parent != hud.transform)
                Object.DestroyImmediate(hud.bankText.transform.parent.gameObject);
            foreach (Image fill in new[] { hud.hullFill, hud.powerFill, hud.oxygenFill })
                if (fill != null && fill.transform.parent != hud.transform) Object.DestroyImmediate(fill.transform.parent.gameObject);
            foreach (var labels in hud.GetComponentsInChildren<StatusBarValuesView>(true)) Object.DestroyImmediate(labels.gameObject);
            foreach (var route in hud.GetComponentsInChildren<ShipRouteView>(true)) Object.DestroyImmediate(route.gameObject);
        }

        static void InstallGuidance(GameHUDController hud, NauticalHudView view)
        {
            view.guidance = Rect(view.viewportRoot, "Guide and chapter scrolls", Vector2.up, Vector2.up);
            Pin(view.guidance, Vector2.up, Vector2.up, new Vector2(12, -54), new Vector2(372, 280));
            var list = view.guidance.gameObject.AddComponent<VerticalLayoutGroup>();
            list.childControlWidth = list.childControlHeight = true;
            list.childForceExpandWidth = true;
            list.childForceExpandHeight = false;
            list.spacing = 4;
            var tutorial = hud.onboarding;
            tutorial.showContextualCaptainOxygen = false;
            if (tutorial.captainOxygenRoot != null) Object.DestroyImmediate(tutorial.captainOxygenRoot);
            Guide(tutorial.instructionPanel, tutorial.instructionText, view.guidance, 98);
            var quest = (RectTransform)hud.questText.transform.parent;
            quest.SetParent(view.guidance, false);
            Size(quest, 372, 85);
            Surface(quest.gameObject, "Scroll");
            StyleText(hud.questChapterText, 17, true);
            hud.questChapterText.text = "主线 · 第1章";
            Stretch(hud.questChapterText.rectTransform, new Vector2(.12f, .61f), new Vector2(.91f, .91f));
            StyleText(hud.questText, 15);
            Stretch(hud.questText.rectTransform, new Vector2(.12f, .12f), new Vector2(.91f, .60f));
            hud.questText.alignment = TextAnchor.MiddleLeft;
            Guide(tutorial.noticePanel, tutorial.noticeText, view.guidance, 71);
            tutorial.instructionText.text = "靠近制作台并互动，尝试制作一块木板。";
            tutorial.instructionPanel.SetActive(true);
            tutorial.noticePanel.SetActive(false);
        }

        static void Guide(GameObject panel, Text text, Transform parent, float height)
        {
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            Size(rect, 372, height);
            Surface(panel, "Scroll");
            StyleText(text, 16);
            text.alignment = TextAnchor.MiddleLeft;
            Stretch(text.rectTransform, new Vector2(.17f, .12f), new Vector2(.91f, .88f));
            Pin(Image(rect, "Guidance emblem", "Notice").rectTransform, new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(19, 0), new Vector2(24, 24));
        }

        static RectTransform InstallStation(CrewStationPanel station, Transform canvas)
        {
            var panel = (RectTransform)station.panel.transform;
            var wrapper = Rect(canvas, station.name + " viewport", new Vector2(.22f, .145f), new Vector2(.78f, .73f));
            wrapper.SetSiblingIndex(panel.GetSiblingIndex());
            panel.SetParent(wrapper, false);
            Stretch(panel, Vector2.zero, Vector2.one);
            Surface(station.panel, "Frame");
            var header = Image(panel, "Device title plate");
            Surface(header.gameObject, "Title");
            Stretch(header.rectTransform, new Vector2(.03f, .899f), new Vector2(.88f, .976f));
            header.transform.SetAsFirstSibling();
            StyleText(station.titleText, 24, true);
            Stretch(station.titleText.rectTransform, new Vector2(.057f, .902f), new Vector2(.86f, .97f));
            StyleText(station.detailText, 17);
            Stretch(station.detailText.rectTransform, new Vector2(.05f, .805f), new Vector2(.95f, .885f));
            StyleText(station.liveText, 17, true);
            station.liveText.color = SoftInk;
            StyleButton(station.closeButton, 22);
            Stretch((RectTransform)station.closeButton.transform, new Vector2(.895f, .901f), new Vector2(.966f, .975f));
            if (station.actionsRoot.parent.TryGetComponent(out Image scroll)) scroll.color = new Color32(153, 101, 69, 180);
            return wrapper;
        }

        static void InstallCrewOverlay(GameHUDController hud)
        {
            var overlay = hud.gameObject.AddComponent<CrewScreenOverlay>();
            hud.crewOverlay = overlay;
            overlay.screen = Rect(hud.transform, "Crew prompts and backpack", Vector2.zero, Vector2.one);
            overlay.footer = (RectTransform)hud.diverText.transform.parent;
            overlay.footer.GetComponent<Image>().color = new Color32(65, 43, 36, 242);
            hud.captainText.gameObject.SetActive(false);
            hud.diverText.gameObject.SetActive(false);
            hud.messageText.transform.SetParent(overlay.screen, false);
            Stretch(hud.messageText.rectTransform, new Vector2(.18f, .141f), new Vector2(.82f, .193f));
            hud.messageText.alignment = TextAnchor.MiddleCenter;
            hud.messageText.color = Color.white;
            AddOutline(hud.messageText);
            hud.messageText.gameObject.SetActive(false);
            for (int i = 0; i < 2; i++)
            {
                overlay.prompts[i] = WorldLabel(overlay.screen, "Interaction " + (CrewRole)i, 21, i == 0 ? overlay.captainPromptColor : overlay.diverPromptColor);
                overlay.names[i] = WorldLabel(overlay.screen, "Steam name " + (CrewRole)i, 21, Color.white);
                overlay.names[i].gameObject.AddComponent<LocalizationLiteralText>();
                overlay.ropeLabels[i] = WorldLabel(overlay.screen, "Rope length " + (CrewRole)i, 18, Color.white);
                overlay.craftNotices[i] = WorldLabel(overlay.screen, "Craft failure " + (CrewRole)i, 22, new Color(1f, .64f, .55f));
            }
            overlay.captainBag = WorldLabel(overlay.screen, "Captain dive backpack", 16, Color.white);
            overlay.onlineBag = Rect(overlay.screen, "Online backpack panel", new Vector2(.55f, .04f), new Vector2(.985f, .25f));
            Surface(overlay.onlineBag.gameObject, "Frame");
            overlay.onlineBag.GetComponent<Image>().color = new Color32(96, 77, 67, 247);
            overlay.onlineBag.gameObject.SetActive(false);
            overlay.bag = Rect(overlay.footer, "Diver backpack", new Vector2(.012f, .05f), new Vector2(.988f, .96f));
            overlay.bagTitle = Label(overlay.bag, "Backpack used slots", 19, TextAnchor.MiddleLeft);
            overlay.bagTitle.color = Color.white;
            overlay.bagTitle.text = "潜水员背包 0/8";
            AddOutline(overlay.bagTitle);
            Stretch(overlay.bagTitle.rectTransform, new Vector2(.01f, .74f), new Vector2(.99f, 1));
            overlay.inventorySlotPrefab = BackpackPrefab();
            int baseSlots = Mathf.Clamp(hud.session.tuning.bagCapacity, 1, 10);
            for (int i = 0; i < baseSlots; i++)
            {
                var tile = (GameObject)PrefabUtility.InstantiatePrefab(overlay.inventorySlotPrefab.gameObject, overlay.bag);
                var item = tile.GetComponentInChildren<Text>();
                item.text = $"{i + 1}  空";
                Stretch((RectTransform)tile.transform, new Vector2(i / (float)baseSlots + .004f, .045f),
                    new Vector2((i + 1f) / baseSlots - .004f, .72f));
                PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                overlay.slots.Add(item);
            }
        }

        static RectTransform BackpackPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPath);
            if (existing != null) return (RectTransform)existing.transform;
            var root = Rect(null, "BackpackSlot", Vector2.zero, Vector2.one);
            try
            {
                Surface(root.gameObject, "Plate");
                root.GetComponent<Image>().color = new Color32(222, 201, 180, 255);
                var item = Label(root, "Item", 17, TextAnchor.MiddleCenter);
                item.text = "空";
                item.resizeTextMinSize = 10;
                Stretch(item.rectTransform, new Vector2(.04f, 0), new Vector2(.96f, 1));
                return (RectTransform)PrefabUtility.SaveAsPrefabAsset(root.gameObject, SlotPath).transform;
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }

        static Text WorldLabel(RectTransform parent, string name, int size, Color color)
        {
            var label = Label(parent, name, size, TextAnchor.MiddleCenter);
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.color = color;
            label.supportRichText = false;
            Pin(label.rectTransform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(360, 68));
            AddOutline(label);
            label.gameObject.SetActive(false);
            return label;
        }

        static void AddOutline(Text label)
        {
            if (!label.TryGetComponent(out Outline outline)) outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.02f, .08f, .12f, .98f);
        }

        static void InstallConnectionStatus(GameHUDController hud)
        {
            hud.onlineStatus = Label(hud.transform, "Online connection status", 19, TextAnchor.LowerRight);
            hud.onlineStatus.color = Color.white;
            hud.onlineStatus.horizontalOverflow = HorizontalWrapMode.Overflow;
            Pin(hud.onlineStatus.rectTransform, Vector2.right, Vector2.right, new Vector2(-18, 14), new Vector2(400, 52));
            AddOutline(hud.onlineStatus);
            hud.onlineStatus.gameObject.SetActive(false);
        }
    }
}
#endif
