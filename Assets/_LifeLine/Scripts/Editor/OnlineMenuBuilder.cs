#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Lifeline.Editor
{
    public static partial class GameProjectBuilder
    {
        const string OnlinePanelPrefabPath = Root + "/Prefabs/UI/OnlineRoomPanel.prefab";

        [MenuItem("LifeLine/Online/Apply Steam Co-op Menu")]
        public static void ApplyOnlineMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene menuScene = SceneManager.GetSceneByPath(MenuScene);
            bool openedHere = !menuScene.isLoaded;
            if (openedHere) menuScene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Additive);
            try
            {
                foreach (var sceneRoot in menuScene.GetRootGameObjects())
                {
                    var menu = sceneRoot.GetComponentInChildren<MainMenuController>(true);
                    if (menu == null || menu.localCoopButton == null) continue;
                    actionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/UI/ActionButton.prefab").GetComponent<Button>();
                    InstallOnlineMenu(menu, (RectTransform)menu.localCoopButton.transform.parent);
                    DefaultLocalizationBuilder.Build();
                    EditorUtility.SetDirty(menu);
                    EditorSceneManager.SaveScene(menuScene);
                    return;
                }
                Debug.LogError("The LifeLine MainMenuController or local play button is missing.");
            }
            finally
            {
                if (openedHere && menuScene.isLoaded) EditorSceneManager.CloseScene(menuScene, true);
            }
        }

        static void InstallOnlineMenu(MainMenuController menu, RectTransform card)
        {
            if (menu.onlineRoomPanel != null && menu.onlineCoopButton != null) return;
            if (menu.menuCard == null) menu.menuCard = card.GetComponent<CanvasGroup>();
            if (menu.menuCard == null) menu.menuCard = card.gameObject.AddComponent<CanvasGroup>();
            if (menu.onlineCoopButton == null)
                menu.onlineCoopButton = Button("SteamCoop", card, "online.menu.entry", new Vector2(.06f, .09f), new Vector2(.49f, .21f));

            // The original controls hint occupied the last left-hand button slot.
            var hint = card.Find("ControlsHint") as RectTransform;
            if (hint != null && hint.anchorMin == new Vector2(.06f, .015f) && hint.anchorMax == new Vector2(.49f, .21f))
            {
                hint.anchorMax = new Vector2(.94f, .085f);
                var hintText = hint.GetComponent<Text>();
                if (hintText != null) hintText.fontSize = 15;
            }

            if (menu.onlineRoomPanel != null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OnlinePanelPrefabPath);
            if (prefab == null) prefab = CreateOnlineRoomPrefab();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, menu.transform);
            instance.SetActive(false);
            menu.onlineRoomPanel = instance.GetComponent<OnlineRoomPanel>();
        }

        static GameObject CreateOnlineRoomPrefab()
        {
            var overlay = Rect("OnlineRoomPanel", null, Vector2.zero, Vector2.one);
            try
            {
                overlay.gameObject.AddComponent<Image>().color = new Color(.08f, .20f, .28f, .55f);
                overlay.gameObject.AddComponent<RuntimeUIFont>();
                var panel = overlay.gameObject.AddComponent<OnlineRoomPanel>();
                var card = Rect("Card", overlay, new Vector2(.20f, .16f), new Vector2(.80f, .84f));
                card.gameObject.AddComponent<Image>().color = panelColor;
                Text("Title", card, "online.menu.title", new Vector2(.06f, .84f), new Vector2(.94f, .965f), 37, TextAnchor.MiddleLeft);

                var home = OnlinePage("Home", card);
                panel.homePage = home.gameObject;
                panel.createButton = RoomButton("CreateRoom", home, "online.menu.create", .17f, .62f, .83f, .87f);
                panel.joinButton = RoomButton("JoinRoom", home, "online.menu.join", .17f, .25f, .83f, .50f);

                var create = OnlinePage("Create", card);
                panel.createPage = create.gameObject;
                panel.saveSummary = Text("SavedVoyage", create, "", new Vector2(.08f, .72f), new Vector2(.92f, .96f), 24, TextAnchor.MiddleCenter);
                panel.resumeVoyageButton = RoomButton("Resume", create, "online.menu.resumeGame", .13f, .46f, .87f, .66f);
                panel.newVoyageButton = RoomButton("New", create, "online.menu.newGame", .13f, .17f, .87f, .37f);

                var join = OnlinePage("Join", card);
                panel.joinPage = join.gameObject;
                Text("RoomCodePrompt", join, "online.menu.codePrompt", new Vector2(.10f, .70f), new Vector2(.90f, .95f), 26, TextAnchor.MiddleCenter);
                panel.codeInput = RoomCodeField(join, out Text placeholder);
                panel.codePlaceholder = placeholder;
                panel.confirmJoinButton = RoomButton("ConfirmJoin", join, "online.menu.join", .18f, .16f, .82f, .36f);

                var room = OnlinePage("Room", card);
                panel.roomPage = room.gameObject;
                Text("CodeLabel", room, "online.menu.codeLabel", new Vector2(0, .81f), new Vector2(.19f, .98f), 19, TextAnchor.MiddleLeft);
                panel.roomCode = Text("RoomCode", room, "", new Vector2(.19f, .81f), new Vector2(.46f, .98f), 36, TextAnchor.MiddleLeft);
                panel.roomCode.gameObject.AddComponent<LocalizationLiteralText>();
                panel.copyCodeButton = RoomButton("CopyCode", room, "online.menu.copyCode", .47f, .815f, .66f, .975f, 16);
                panel.playerCount = Text("PlayerCount", room, "", new Vector2(.01f, .67f), new Vector2(.65f, .81f), 22, TextAnchor.MiddleLeft);
                panel.playerRows = new[] { RoomPlayer("PlayerOne", room, .435f, .66f), RoomPlayer("PlayerTwo", room, .18f, .405f) };
                Text("RoleHeader", room, "online.menu.roles", new Vector2(.69f, .69f), new Vector2(1, .82f), 22, TextAnchor.MiddleLeft);
                panel.captainButton = RoomButton("Captain", room, "role.captain", .69f, .48f, 1, .665f);
                panel.diverButton = RoomButton("Diver", room, "role.diver", .69f, .25f, 1, .435f);
                panel.startButton = RoomButton("Start", room, "online.menu.start", .69f, .025f, 1, .20f);
                panel.roomHint = Text("RoomHint", room, "", new Vector2(.01f, .01f), new Vector2(.65f, .16f), 18, TextAnchor.MiddleLeft);

                panel.status = Text("Status", card, "", new Vector2(.06f, .14f), new Vector2(.94f, .243f), 17, TextAnchor.MiddleLeft);
                panel.retryButton = RoomButton("RetrySteam", card, "online.menu.retry", .06f, .04f, .44f, .132f, 17);
                panel.backButton = RoomButton("Back", card, "online.menu.back", .69f, .04f, .94f, .132f, 17);
                NauticalMenuAuthoring.ApplyRoom(panel);
                overlay.gameObject.SetActive(false);
                return PrefabUtility.SaveAsPrefabAsset(overlay.gameObject, OnlinePanelPrefabPath);
            }
            finally { Object.DestroyImmediate(overlay.gameObject); }
        }

        static RectTransform OnlinePage(string name, Transform parent)
            => Rect(name, parent, new Vector2(.06f, .255f), new Vector2(.94f, .83f));

        static Button RoomButton(string name, Transform parent, string key, float left, float bottom, float right, float top, int size = 22)
        {
            var button = Button(name, parent, key, new Vector2(left, bottom), new Vector2(right, top));
            var text = button.GetComponentInChildren<Text>(true);
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        static OnlineRoomPanel.PlayerRow RoomPlayer(string name, Transform parent, float bottom, float top)
        {
            var row = Rect(name, parent, new Vector2(0, bottom), new Vector2(.66f, top));
            row.gameObject.AddComponent<Image>().color = new Color(.81f, .89f, .87f);
            var nickname = Text("SteamNickname", row, "", new Vector2(.035f, .46f), new Vector2(.965f, .96f), 23, TextAnchor.MiddleLeft);
            nickname.supportRichText = false;
            nickname.verticalOverflow = VerticalWrapMode.Truncate;
            nickname.gameObject.AddComponent<LocalizationLiteralText>();
            return new OnlineRoomPanel.PlayerRow
            {
                nickname = nickname,
                role = Text("Role", row, "", new Vector2(.035f, .04f), new Vector2(.65f, .44f), 18, TextAnchor.MiddleLeft),
                membership = Text("Membership", row, "", new Vector2(.66f, .04f), new Vector2(.965f, .44f), 18, TextAnchor.MiddleRight)
            };
        }

        static InputField RoomCodeField(Transform parent, out Text placeholder)
        {
            var rect = Rect("RoomCodeInput", parent, new Vector2(.18f, .43f), new Vector2(.82f, .66f));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.81f, .89f, .87f);
            var field = rect.gameObject.AddComponent<InputField>();
            field.targetGraphic = image;
            field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = OnlineRoomService.RoomCodeLength;
            field.textComponent = Text("Code", rect, "", new Vector2(.04f, 0), new Vector2(.96f, 1), 29, TextAnchor.MiddleCenter);
            field.textComponent.supportRichText = false;
            placeholder = Text("Placeholder", rect, "", new Vector2(.04f, 0), new Vector2(.96f, 1), 24, TextAnchor.MiddleCenter);
            placeholder.color = new Color(.26f, .38f, .41f);
            field.placeholder = placeholder;
            return field;
        }
    }
}
#endif
