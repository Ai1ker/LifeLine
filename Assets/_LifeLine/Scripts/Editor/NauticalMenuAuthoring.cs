#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Lifeline.NauticalUiSkin;

namespace Lifeline.Editor
{
    public static class NauticalMenuAuthoring
    {
        public const string LoadingPath = GameProjectBuilder.Root + "/Prefabs/UI/OnlineLoadingCanvas.prefab";
        static readonly Color Dim = new Color(.045f, .074f, .11f, .76f);

        public static void Apply(MainMenuController menu)
        {
            if (menu == null) return;
            var card = menu.menuCard != null ? (RectTransform)menu.menuCard.transform : (RectTransform)menu.localCoopButton.transform.parent;
            if (card.Find("Main title plate") == null) ApplyMainCard(menu, card);
            CenterButtonLabels(menu.localCoopButton, menu.continueButton, menu.onlineCoopButton,
                menu.settingsButton, menu.controlsButton, menu.quitButton);
            var harbor = menu.transform.Find("Harbor at dusk") as RectTransform;
            if (harbor != null && harbor.Find("Deck details") == null) SetHarborWaterline(harbor);
            ApplySettings(menu.settings);
            ApplyControls(menu.controls);
            if (menu.onlineLoadingViewPrefab == null) menu.onlineLoadingViewPrefab = LoadingPrefab();
        }

        static void ApplyMainCard(MainMenuController menu, RectTransform card)
        {
            var backdrop = Rect(menu.transform, "Harbor at dusk", Vector2.zero, Vector2.one);
            backdrop.SetAsFirstSibling();
            var baseImage = Image(backdrop, "Sky", null);
            baseImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GameProjectBuilder.Root + "/Art/Ship/StaticSprites/Backdrop/CoastSkyDusk.png");
            baseImage.color = baseImage.sprite != null ? Color.white : new Color32(64, 53, 93, 255);
            var sea = Image(backdrop, "Open water");
            Stretch(sea.rectTransform, Vector2.zero, new Vector2(1, .27f));
            sea.color = new Color32(33, 60, 87, 255);
            var wake = Image(sea.transform, "Water ripples");
            wake.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GameProjectBuilder.Root + "/Art/Ship/StaticSprites/Backdrop/Ripple.png");
            if (wake.sprite != null) { wake.type = UnityEngine.UI.Image.Type.Tiled; wake.color = new Color(1, 1, 1, .35f); }
            else wake.enabled = false;
            var ship = Image(backdrop, "Rescue vessel");
            ship.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GameProjectBuilder.Root + "/Art/Ship/StaticSprites/SalvageHull.png");
            ship.preserveAspect = true;
            ship.enabled = ship.sprite != null;
            Stretch(ship.rectTransform, new Vector2(.49f, .16f), new Vector2(.97f, .66f));
            var dim = Image(backdrop, "Dusk tint");
            dim.color = new Color(.035f, .044f, .08f, .17f);

            Stretch(card, new Vector2(.075f, .115f), new Vector2(.535f, .86f));
            Surface(card.gameObject, "Frame");
            DecorateHeading(card, "Main title plate", new Vector2(.06f, .795f), new Vector2(.94f, .99f), 40);
            var helm = Image(card.parent, "Menu brass helm", "Wheel");
            Pin(helm.rectTransform, new Vector2(.305f, .86f), new Vector2(.5f, .5f), new Vector2(0, 19), new Vector2(114, 114));
            helm.transform.SetSiblingIndex(card.GetSiblingIndex());
            var description = card.Find("Description")?.GetComponent<Text>();
            if (description != null)
            {
                StyleText(description, 19);
                description.alignment = TextAnchor.MiddleCenter;
                Stretch(description.rectTransform, new Vector2(.09f, .64f), new Vector2(.91f, .78f));
            }
            var voyageButtons = Rect(card, "Local voyages", new Vector2(.08f, .49f), new Vector2(.92f, .60f));
            var layout = voyageButtons.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            layout.spacing = 12;
            menu.localCoopButton.transform.SetParent(voyageButtons, false);
            menu.continueButton.transform.SetParent(voyageButtons, false);
            StyleButton(menu.localCoopButton, 21, "Resume");
            StyleButton(menu.continueButton, 21, "Save");
            PlaceButton(menu.onlineCoopButton, new Vector2(.08f, .35f), new Vector2(.92f, .46f), 22, "Boat");
            PlaceButton(menu.settingsButton, new Vector2(.08f, .21f), new Vector2(.485f, .32f), 20, "Settings");
            PlaceButton(menu.controlsButton, new Vector2(.515f, .21f), new Vector2(.92f, .32f), 20, "Keys");
            PlaceButton(menu.quitButton, new Vector2(.32f, .07f), new Vector2(.68f, .17f), 18, "Exit");
            if (menu.saveStatusText != null)
            {
                StyleText(menu.saveStatusText, 13);
                Stretch(menu.saveStatusText.rectTransform, new Vector2(.09f, .601f), new Vector2(.91f, .64f));
            }
            var oldHint = card.Find("ControlsHint");
            if (oldHint != null) oldHint.gameObject.SetActive(false);
        }

        static void SetHarborWaterline(RectTransform harbor)
        {
            var ship = (RectTransform)harbor.Find("Rescue vessel");
            var sea = harbor.Find("Open water");
            Stretch(ship, new Vector2(.49f, .07f), new Vector2(.97f, .57f));
            sea.Find("Water ripples").GetComponent<Image>().color = new Color(1, 1, 1, .18f);
            var deck = Rect(harbor, "Deck details", Vector2.zero, Vector2.one);
            deck.SetSiblingIndex(harbor.Find("Dusk tint").GetSiblingIndex());
            SetDeckSprite(deck, "Wheel on deck", "Stations/StaticSprites/Helm.png", .594f, .635f, .466f);
            SetDeckSprite(deck, "Captain on deck", "Characters/StaticSprites/Captain_Idle.png", .634f, .674f, .491f);
            SetDeckSprite(deck, "Winch on deck", "Stations/StaticSprites/Winch.png", .674f, .744f, .479f);
            SetDeckSprite(deck, "Diver on deck", "Characters/StaticSprites/Diver_Idle.png", .787f, .836f, .489f);
        }

        static void SetDeckSprite(Transform parent, string name, string path, float left, float right, float top)
        {
            var art = Image(parent, name);
            art.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GameProjectBuilder.Root + "/Art/" + path);
            art.preserveAspect = true;
            art.enabled = art.sprite != null;
            Stretch(art.rectTransform, new Vector2(left, .394f), new Vector2(right, top));
        }

        public static void ApplyPause(GameHUDController hud)
        {
            if (hud.pausePanel == null || hud.resumeButton == null) return;
            var card = (RectTransform)hud.resumeButton.transform.parent;
            if (card.Find("Pause title plate") != null) return;
            hud.pausePanel.GetComponent<Image>().color = Dim;
            Stretch(card, new Vector2(.255f, .205f), new Vector2(.745f, .765f));
            var wheel = Image(card.parent, "Brass helm", "Wheel");
            Pin(wheel.rectTransform, new Vector2(.5f, .765f), new Vector2(.5f, .5f), new Vector2(0, 17), new Vector2(122, 122));
            wheel.transform.SetSiblingIndex(card.GetSiblingIndex());
            Surface(card.gameObject, "Frame");
            DecorateHeading(card, "Pause title plate", new Vector2(.22f, .84f), new Vector2(.78f, 1.035f), 33);
            PauseAction(hud.resumeButton, 0, false, "Resume");
            PauseAction(hud.saveButton, 0, true, "Save");
            PauseAction(hud.settingsButton, 1, false, "Settings");
            PauseAction(hud.controlsButton, 1, true, "Keys");
            PauseAction(hud.restartButton, 2, false, "Restart");
            PauseAction(hud.menuButton, 2, true, "Exit");
            StyleText(hud.saveStatusText, 16);
            if (hud.saveStatusText != null)
            {
                hud.saveStatusText.alignment = TextAnchor.MiddleCenter;
                Stretch(hud.saveStatusText.rectTransform, new Vector2(.10f, .055f), new Vector2(.90f, .168f));
            }
            GameHUDController.ConfigureModalLayer(hud.pausePanel, 100);
            if (hud.endingPanel != null)
            {
                Surface(hud.endingPanel, "Frame");
                StyleText(hud.endingText, 28, true);
                StyleButton(hud.endingRestartButton, 20, "Restart");
                StyleButton(hud.endingMenuButton, 20, "Exit");
                GameHUDController.ConfigureModalLayer(hud.endingPanel, 110);
            }
        }

        static void PauseAction(Button button, int row, bool right, string art)
        {
            float high = .76f - row * .193f;
            PlaceButton(button, new Vector2(right ? .511f : .078f, high - .145f),
                new Vector2(right ? .922f : .489f, high), 23, art);
        }

        public static void ApplySettings(GameSettingsPanel panel)
        {
            if (panel == null || panel.languageButton == null) return;
            var card = (RectTransform)panel.languageButton.transform.parent;
            if (card.Find("Settings title plate") != null) return;
            panel.panel.GetComponent<Image>().color = Dim;
            Stretch(card, new Vector2(.285f, .19f), new Vector2(.715f, .81f));
            Surface(card.gameObject, "Frame");
            DecorateHeading(card, "Settings title plate", new Vector2(.17f, .815f), new Vector2(.83f, .97f), 31);
            PlaceButton(panel.languageButton, new Vector2(.105f, .623f), new Vector2(.895f, .765f), 22);
            PlaceButton(panel.cameraButton, new Vector2(.105f, .45f), new Vector2(.895f, .592f), 22);
            PlaceButton(panel.bindingsButton, new Vector2(.105f, .277f), new Vector2(.895f, .419f), 22, "Keys");
            PlaceButton(panel.closeButton, new Vector2(.59f, .073f), new Vector2(.895f, .195f), 19);
            GameHUDController.ConfigureModalLayer(panel.panel, 120);
        }

        public static void ApplyControls(ControlsRebindPanel panel)
        {
            if (panel == null || panel.closeButton == null) return;
            var card = (RectTransform)panel.closeButton.transform.parent;
            if (card.Find("Bindings title plate") != null) return;
            panel.panel.GetComponent<Image>().color = Dim;
            Surface(card.gameObject, "Frame");
            Stretch(card, new Vector2(.16f, .075f), new Vector2(.84f, .925f));
            DecorateHeading(card, "Bindings title plate", new Vector2(.17f, .875f), new Vector2(.83f, .97f), 28);
            StyleButton(panel.captainTab, 20);
            StyleButton(panel.diverTab, 20);
            StyleText(panel.statusText, 17);
            StyleButton(panel.resetButton, 19, "Restart");
            StyleButton(panel.closeButton, 19);
            var viewport = panel.rowsRoot.parent.GetComponent<Image>();
            if (viewport != null) viewport.color = new Color32(153, 101, 69, 180);
            InstallBindingPreview(panel);
            GameHUDController.ConfigureModalLayer(panel.panel, 130);
        }

        static void InstallBindingPreview(ControlsRebindPanel panel)
        {
            if (panel.rowWidgets.Count != 0 || panel.rowButtonPrefab == null) return;
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetBuilder.DefaultPath);
            var captain = input?.FindActionMap("Captain");
            if (captain == null) return;
            foreach (var action in captain.actions)
            {
                if (action.name == "Backpack") continue;
                for (int index = 0; index < action.bindings.Count; index++)
                {
                    var binding = action.bindings[index];
                    if (binding.isComposite) continue;
                    var row = (GameObject)PrefabUtility.InstantiatePrefab(panel.rowButtonPrefab.gameObject, panel.rowsRoot);
                    row.SetActive(true);
                    string name = binding.isPartOfComposite ? action.name + "/" + binding.name : action.name;
                    var label = row.GetComponentInChildren<Text>();
                    label.text = name + "    [ " + action.GetBindingDisplayString(index) + " ]";
                    PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                    panel.rowWidgets.Add(row.GetComponent<Button>());
                }
            }
        }

        public static void ApplyRoom(OnlineRoomPanel panel)
        {
            if (panel == null || panel.homePage == null) return;
            var card = (RectTransform)panel.homePage.transform.parent;
            if (card.Find("Room title plate") != null) return;
            panel.GetComponent<Image>().color = Dim;
            Stretch(card, new Vector2(.19f, .16f), new Vector2(.81f, .84f));
            Surface(card.gameObject, "Frame");
            DecorateHeading(card, "Room title plate", new Vector2(.19f, .845f), new Vector2(.81f, .975f), 33);
            foreach (var text in card.GetComponentsInChildren<Text>(true))
                if (text.GetComponentInParent<Button>(true) == null && text.gameObject.name != "Title")
                    StyleText(text, text.fontSize);
            foreach (var row in panel.playerRows) Surface(row.nickname.transform.parent.gameObject, "Plate");
            StyleButton(panel.createButton, 24, "Boat");
            StyleButton(panel.joinButton, 24, "Keys");
            StyleButton(panel.resumeVoyageButton, 22, "Save");
            StyleButton(panel.newVoyageButton, 22, "Resume");
            StyleButton(panel.confirmJoinButton, 22, "Resume");
            StyleButton(panel.retryButton, 17);
            StyleButton(panel.copyCodeButton, 16);
            StyleButton(panel.startButton, 20);
            StyleButton(panel.backButton, 18);
            StyleButton(panel.captainButton, 20);
            StyleButton(panel.diverButton, 20);
            Surface(panel.codeInput.gameObject, "Plate");
            panel.codePlaceholder.color = SoftInk;
            panel.codeInput.caretColor = Ink;
            panel.codeInput.customCaretColor = true;
            panel.codeInput.selectionColor = new Color(.65f, .44f, .27f, .45f);
            panel.homePage.SetActive(true);
            panel.createPage.SetActive(false);
            panel.joinPage.SetActive(false);
            panel.roomPage.SetActive(false);
            CenterRoomLabels(panel);
        }

        public static void CenterRoomLabels(OnlineRoomPanel panel)
        {
            CenterButtonLabels(panel.createButton, panel.joinButton, panel.resumeVoyageButton,
                panel.newVoyageButton, panel.confirmJoinButton);
        }

        static void CenterButtonLabels(params Button[] buttons)
        {
            foreach (var button in buttons)
            {
                if (button == null) continue;
                var label = button.GetComponentInChildren<Text>(true);
                if (label == null) continue;
                label.alignment = TextAnchor.MiddleCenter;
                var rect = label.rectTransform;
                rect.anchorMin = new Vector2(.14f, rect.anchorMin.y);
                rect.anchorMax = new Vector2(.86f, rect.anchorMax.y);
                var minimum = rect.offsetMin;
                minimum.x = 0;
                rect.offsetMin = minimum;
                var maximum = rect.offsetMax;
                maximum.x = 0;
                rect.offsetMax = maximum;
                PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            }
        }

        static void DecorateHeading(RectTransform card, string name, Vector2 min, Vector2 max, int fontSize)
        {
            var plate = Image(card, name);
            Surface(plate.gameObject, "Title");
            Stretch(plate.rectTransform, min, max);
            plate.transform.SetAsFirstSibling();
            var title = card.Find("Title")?.GetComponent<Text>();
            if (title == null) return;
            StyleText(title, fontSize, true);
            title.alignment = TextAnchor.MiddleCenter;
            Stretch(title.rectTransform, min + new Vector2(.024f, .01f), max - new Vector2(.024f, .01f));
        }

        static void PlaceButton(Button button, Vector2 min, Vector2 max, int size, string art = null)
        {
            if (button == null) return;
            Stretch((RectTransform)button.transform, min, max);
            StyleButton(button, size, art);
        }

        public static OnlineLoadingView LoadingPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(LoadingPath)?.GetComponent<OnlineLoadingView>();
            if (existing != null) return existing;
            var root = new GameObject("OnlineLoadingCanvas");
            try
            {
                var view = root.AddComponent<OnlineLoadingView>();
                var rect = Rect(root.transform, "Online loading canvas", Vector2.zero, Vector2.one);
                var canvas = rect.gameObject.AddComponent<Canvas>();
                canvas.sortingOrder = 140;
                rect.gameObject.AddComponent<GraphicRaycaster>();
                rect.gameObject.AddComponent<RuntimeUIFont>();
                var scaler = rect.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1600, 900);
                var veil = Image(rect, "Loading backdrop");
                veil.color = Dim;
                veil.raycastTarget = true;
                var card = Rect(rect, "Loading progress card", new Vector2(.27f, .405f), new Vector2(.73f, .595f));
                Surface(card.gameObject, "Frame");
                view.stageLabel = Label(card, "Loading stage", 24, TextAnchor.MiddleCenter);
                view.stageLabel.text = "online.loading.prepare";
                Stretch(view.stageLabel.rectTransform, new Vector2(.07f, .48f), new Vector2(.93f, .85f));
                var track = Image(card, "Loading track");
                track.color = Track;
                Stretch(track.rectTransform, new Vector2(.09f, .22f), new Vector2(.91f, .40f));
                var fill = Image(track.transform, "Loading fill");
                fill.color = new Color32(220, 153, 53, 255);
                view.fill = fill.rectTransform;
                view.canvasRoot = rect.gameObject;
                rect.gameObject.SetActive(false);
                return PrefabUtility.SaveAsPrefabAsset(root, LoadingPath).GetComponent<OnlineLoadingView>();
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
#endif
