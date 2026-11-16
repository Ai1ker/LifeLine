#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Lifeline.Editor
{
    /// <summary>Explicit one-time authoring tool. Normal play never regenerates authored scenes/prefabs.</summary>
    public static class GameProjectBuilder
    {
        public const string Root = "Assets/_LifeLine";
        public const string MenuScene = Root + "/Scenes/MainMenu.unity";
        public const string PlayScene = Root + "/Scenes/Gameplay.unity";
        static Color background = new Color(.55f, .8f, .88f, 1);
        static Color panelColor = new Color(.9f, .94f, .91f, .98f);
        static Color textColor = new Color(.12f, .23f, .26f);
        static Button actionPrefab;

        [MenuItem("LifeLine/Create First Playable (Preserve Existing)")]
        public static void BuildFirstPlayable() => Build(false);

        public static void Build(bool overwriteGeneratedScenes)
        {
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Documentation");
            EnsureLayer(8, "World"); EnsureLayer(9, "Crew"); EnsureLayer(10, "Ship");
            EnsureLayer(11, "Pickup"); EnsureLayer(12, "ShipPresentation"); EnsureLayer(13, "Enemy");
            EnsureLayer(14, "WaterShipProxy");
            var library = PrefabLibraryBuilder.Build();
            actionPrefab = library.actionButton;
            if (overwriteGeneratedScenes)
            {
                string backup = Root + "/Documentation/SceneBackups/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_ffff");
                foreach (string path in new[] { MenuScene, PlayScene })
                    if (File.Exists(path))
                    {
                        Directory.CreateDirectory(backup); File.Copy(path, backup + "/" + Path.GetFileName(path) + ".bak");
                        if (File.Exists(path + ".meta")) File.Copy(path + ".meta", backup + "/" + Path.GetFileName(path) + ".meta.bak");
                    }
            }
            if (!File.Exists(PlayScene) || overwriteGeneratedScenes) BuildGameplay();
            if (!File.Exists(MenuScene) || overwriteGeneratedScenes) BuildMenu();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MenuScene, true), new EditorBuildSettingsScene(PlayScene, true) };
            PlayerSettings.productName = "LifeLine";
            PlayerSettings.companyName = "LifeLine Team";
            PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(MenuScene);
            Debug.Log("LifeLine first playable ready: " + MenuScene + " / " + PlayScene);
        }

        [MenuItem("LifeLine/Open Main Menu")]
        public static void OpenMenu() => EditorSceneManager.OpenScene(MenuScene);

        static void BuildMenu()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Single-scene replacement may unload assets referenced only by the old scene.
            var library = PrefabLibraryBuilder.Build(); actionPrefab = library.actionButton;
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 0, -24);
            camera.orthographic = true; camera.orthographicSize = 8; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = background;
            camera.gameObject.AddComponent<AudioListener>();
            AddLight();
            RectTransform canvas = CreateCanvas("MainMenuCanvas");
            var controller = canvas.gameObject.AddComponent<MainMenuController>();
            controller.inputAsset = library.input;
            controller.captainInput = new GameObject("Captain menu input").AddComponent<CrewInputReader>();
            controller.captainInput.role = CrewRole.Captain; controller.captainInput.actions = library.input;
            controller.diverInput = new GameObject("Diver menu input").AddComponent<CrewInputReader>();
            controller.diverInput.role = CrewRole.Diver; controller.diverInput.actions = library.input;
            RectTransform card = Rect("MainMenu", canvas, new Vector2(.12f, .12f), new Vector2(.88f, .88f));
            card.gameObject.AddComponent<Image>().color = panelColor;
            Text("Title", card, "一线生机 / LIFELINE", new Vector2(.06f, .73f), new Vector2(.94f, .94f), 48, TextAnchor.MiddleLeft);
            Text("Description", card, "本地双人合作\n船长守住船体，潜水员探索废墟。\n返航时，只有同伴能拉紧那根绳。", new Vector2(.06f, .52f), new Vector2(.94f, .75f), 25, TextAnchor.UpperLeft);
            controller.saveStatusText = Text("SaveStatus", card, "", new Vector2(.06f, .50f), new Vector2(.94f, .56f), 14, TextAnchor.MiddleLeft);
            controller.localCoopButton = Button("StartLocalCoop", card, "开始本地双人", new Vector2(.06f, .37f), new Vector2(.49f, .49f));
            controller.continueButton = Button("ContinueGame", card, "继续游戏", new Vector2(.53f, .37f), new Vector2(.91f, .49f));
            controller.continueButton.interactable = false;
            controller.settingsButton = Button("Settings", card, "设置", new Vector2(.06f, .23f), new Vector2(.49f, .35f));
            controller.controlsButton = Button("Controls", card, "键位 / 换绑", new Vector2(.53f, .23f), new Vector2(.91f, .35f));
            controller.quitButton = Button("Quit", card, "退出", new Vector2(.53f, .09f), new Vector2(.91f, .21f));
            Text("ControlsHint", card, "P1：WASD / E互动 / 空格近战 / Q鱼枪   P2：方向键 / Enter互动 / 1采集 / 2鱼枪\n所有角色键位可在换绑界面调整；Esc 暂停。", new Vector2(.06f, .015f), new Vector2(.49f, .21f), 17, TextAnchor.MiddleLeft);
            controller.controls = CreateControls(canvas, library.actionButton);
            controller.settings = CreateSettings(canvas, controller.controls);
            new GameObject("Save flow").AddComponent<GameSaveManager>().mainMenu = controller;
            CreateLocalization();
            EditorSceneManager.SaveScene(scene, MenuScene);
        }

        static void BuildGameplay()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var library = PrefabLibraryBuilder.Build(); actionPrefab = library.actionButton;
            AddLight();
            GameSession session = new GameObject("GameSession - authoritative simulation").AddComponent<GameSession>();
            session.tuning = library.tuning; session.GetOrCreateCraftingManager().SetRecipeDefinitions(library.recipes); session.phase = SessionPhase.Menu;
            session.autoStart = false; session.worldSeed = 8417;
            session.ship = ((GameObject)PrefabUtility.InstantiatePrefab(library.ship)).GetComponent<ShipMotor>();
            session.ship.name = "Ship"; session.ship.session = session;
            session.captain = ((GameObject)PrefabUtility.InstantiatePrefab(library.captain)).GetComponent<CrewActor>();
            session.diver = ((GameObject)PrefabUtility.InstantiatePrefab(library.diver)).GetComponent<CrewActor>();
            ConfigureCrew(session.captain, session, CrewRole.Captain, session.captainSpawnLocal);
            ConfigureCrew(session.diver, session, CrewRole.Diver, session.diverSpawnLocal);
            ShipLadderHatch hatch = session.ship.GetComponent<ShipLadderHatch>();
            if (hatch != null) hatch.crew = new[] { session.captain, session.diver };
            session.obstacleMask = (1 << 8) | (1 << 10);
            session.sharkMaterial = library.sharkMaterial; session.whiteSharkMaterial = library.whiteSharkMaterial;
            session.birdMaterial = library.birdMaterial; session.shadowMaterial = library.shadowMaterial; session.spearMaterial = library.spearMaterial;
            session.sharkPrefab = library.shark; session.birdPrefab = library.bird;
            session.shadowPrefab = library.shadow; session.spearPrefab = library.spear;
            WorldGenerator world = new GameObject("World - seeded ruins").AddComponent<WorldGenerator>();
            session.world = world; world.session = session; world.tuning = library.tuning;
            world.geometryLayer = 8; world.pickupLayer = 11; world.placeholderMaterials = library.worldPalette;
            world.resourceMaterials = library.resourcePalette;
            world.lootPrefab = library.loot; world.cratePrefab = library.crate;
            world.trapdoorPrefab = library.trapdoor;
            world.sharkPrefab = library.shark; world.shadowPrefab = library.shadow;
            session.maps = session.gameObject.AddComponent<GameMapPartition>();
            session.maps.InitializeForAuthoring(session);
            session.GetOrCreateLifeline(); session.GetOrCreateLifeline(true);
            world.Generate(session.worldSeed);
            GameCameraRig rig = new GameObject("Cameras - Cinemachine local coop").AddComponent<GameCameraRig>();
            rig.obstacleMask = (1 << 8) | (1 << 10); rig.Initialize(session, session.captain, session.diver, session.ship);
            SessionAuthority authority = new GameObject("Networking - reserved transport boundary").AddComponent<SessionAuthority>();
            authority.session = session; authority.cameraRig = rig;
            GameHUDController hud = CreateHUD(session, authority);
            var saves = session.gameObject.AddComponent<GameSaveManager>(); saves.session = session; saves.hud = hud;
            new GameObject("Diver lifeline presentation").AddComponent<DiverLifelineView>().Initialize(session);
            CreateUnderwaterProxy(session, rig, library);
            NightVisibilityView visibility = hud.gameObject.AddComponent<NightVisibilityView>();
            Image mask = Rect("NightWaterVisibility", hud.transform, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            mask.raycastTarget = false; mask.color = Color.white; mask.transform.SetAsFirstSibling();
            mask.enabled = false;
            Material nightMaterial = library.nightMaskMaterial;
            if (nightMaterial == null)
            {
                Shader shader = Shader.Find("LifeLine/UI/Night Visibility");
                if (shader != null)
                {
                    string path = Root + "/Materials/NightVisibility.mat";
                    nightMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (nightMaterial == null) { nightMaterial = new Material(shader); AssetDatabase.CreateAsset(nightMaterial, path); }
                }
            }
            visibility.session = session; visibility.cameraRig = rig; visibility.diver = session.diver;
            visibility.maskImage = mask; visibility.maskMaterial = nightMaterial;
            CreateLocalization();
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, PlayScene);
            ExportNumericReference(session, world, rig, library.tuning);
        }

        static void ConfigureCrew(CrewActor actor, GameSession session, CrewRole role, Vector3 spawn)
        {
            actor.name = role.ToString(); actor.session = session; actor.role = role;
            actor.transform.position = spawn;
            actor.groundMask = (1 << 8) | (1 << 10);
            actor.waterBottomY = -54f;
            actor.input.role = role;
            if (actor.body != null) actor.body.position = spawn;
        }

        static UnderwaterShipProxy CreateUnderwaterProxy(GameSession session, GameCameraRig rig, PrefabLibraryBuilder.Library library)
        {
            var proxy = new GameObject("Underwater map - small surface ship").AddComponent<UnderwaterShipProxy>();
            proxy.hullMaterial = library.resourcePalette[(int)ResourceKind.Plank];
            proxy.deckMaterial = library.worldPalette[6];
            proxy.mastMaterial = library.worldPalette[0];
            proxy.Initialize(session, rig); return proxy;
        }

        [MenuItem("LifeLine/Migration/Separate Ship And Water Maps")]
        public static void ApplyMapAndRopeUpdate()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before upgrading the scene.");
            var session = UnityEngine.Object.FindAnyObjectByType<GameSession>();
            if (session == null || session.ship == null || session.diver == null) throw new InvalidOperationException("Open the Gameplay scene first.");
            var scene = session.gameObject.scene;
            if (scene.isDirty) throw new InvalidOperationException("Save existing scene edits before upgrading.");
            string backup = Root + "/Documentation/SceneBackups/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_ffff");
            Directory.CreateDirectory(backup);
            File.Copy(scene.path, backup + "/" + Path.GetFileName(scene.path) + ".bak");
            if (File.Exists(scene.path + ".meta")) File.Copy(scene.path + ".meta", backup + "/" + Path.GetFileName(scene.path) + ".meta.bak");
            EnsureLayer(14, "WaterShipProxy");
            if (session.maps == null) session.maps = session.GetComponent<GameMapPartition>();
            if (session.maps == null) session.maps = session.gameObject.AddComponent<GameMapPartition>();
            session.maps.InitializeForAuthoring(session);
            session.GetOrCreateLifeline(); session.GetOrCreateLifeline(true);
            var rig = UnityEngine.Object.FindAnyObjectByType<GameCameraRig>();
            if (rig != null)
            {
                if (rig.underwaterSurfaceHeadroom <= 2.8f) rig.underwaterSurfaceHeadroom = 4.5f;
                rig.RefreshViewNow();
            }
            if (UnityEngine.Object.FindAnyObjectByType<UnderwaterShipProxy>() == null) CreateUnderwaterProxy(session, rig, PrefabLibraryBuilder.Build());
            if (UnityEngine.Object.FindAnyObjectByType<DiverLifelineView>() == null)
                new GameObject("Diver lifeline presentation").AddComponent<DiverLifelineView>().Initialize(session);
            foreach (var line in UnityEngine.Object.FindObjectsByType<DiverLifelineView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (line.foregroundZ > -1.7f) line.foregroundZ = -1.7f;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            ExportCurrentTuning(); AssetDatabase.SaveAssets();
        }

        static GameHUDController CreateHUD(GameSession session, SessionAuthority authority)
        {
            RectTransform canvas = CreateCanvas("GameplayCanvas");
            GameHUDController hud = canvas.gameObject.AddComponent<GameHUDController>(); hud.session = session; hud.authority = authority;
            RectTransform top = Rect("HUD/Top", canvas, new Vector2(0, .84f), Vector2.one);
            top.gameObject.AddComponent<Image>().color = background;
            hud.clockText = Text("Clock", top, "DAY 1", new Vector2(.01f, .70f), new Vector2(.23f, .99f), 19, TextAnchor.MiddleLeft);
            hud.shipText = Text("HullPowerRoute", top, "船体 / 动力 / 航线", new Vector2(.23f, .70f), new Vector2(.62f, 1), 19, TextAnchor.MiddleLeft);
            hud.bankText = Text("SharedBank", top, "共享仓库", new Vector2(.01f, .39f), new Vector2(.70f, .68f), 17, TextAnchor.MiddleLeft);
            hud.pauseButton = Button("Pause", top, "暂停", new Vector2(.635f, .72f), new Vector2(.70f, .98f));
            RectTransform footer = Rect("HUD/Bottom", canvas, Vector2.zero, new Vector2(1, .13f)); footer.gameObject.AddComponent<Image>().color = background;
            hud.captainText = Text("CaptainStatus", footer, "船长", new Vector2(.01f, .54f), new Vector2(.49f, .98f), 17, TextAnchor.MiddleLeft);
            hud.diverText = Text("DiverStatus", footer, "潜水员", new Vector2(.51f, .54f), new Vector2(.99f, .98f), 17, TextAnchor.MiddleLeft);
            hud.messageText = Text("StatusMessage", footer, "", new Vector2(.01f, .02f), new Vector2(.99f, .50f), 17, TextAnchor.MiddleCenter);
            RectTransform quest = Rect("QuestProgressCard", canvas, new Vector2(.72f, .85f), new Vector2(.99f, .995f));
            quest.gameObject.AddComponent<Image>().color = panelColor;
            hud.questChapterText = Text("Chapter", quest, "当前主线", new Vector2(.05f, .70f), new Vector2(.95f, .98f), 19, TextAnchor.MiddleLeft);
            hud.questText = Text("CurrentGoal", quest, "当前主线", new Vector2(.05f, .03f), new Vector2(.95f, .70f), 16, TextAnchor.UpperLeft);
            Rect("ShipRoute", canvas, new Vector2(.02f, .84f), new Vector2(.70f, .89f)).gameObject.AddComponent<ShipRouteView>().BuildView(session);
            hud.hullFill = Bar("Hull", canvas, new Vector2(.02f, .823f), new Vector2(.25f, .832f), new Color(.85f, .68f, .35f));
            hud.powerFill = Bar("Power", canvas, new Vector2(.28f, .823f), new Vector2(.51f, .832f), new Color(.4f, .8f, .7f));
            hud.oxygenFill = Bar("Oxygen", canvas, new Vector2(.54f, .823f), new Vector2(.97f, .832f), new Color(.4f, .65f, .9f));
            hud.captainPanel = CreateStationPanel(canvas, CrewRole.Captain); hud.diverPanel = CreateStationPanel(canvas, CrewRole.Diver);
            RectTransform pauseOverlay = Rect("PauseOverlay", canvas, Vector2.zero, Vector2.one);
            pauseOverlay.gameObject.AddComponent<Image>().color = new Color(.55f, .8f, .88f, .94f);
            RectTransform pause = Rect("PauseCard", pauseOverlay, new Vector2(.25f, .18f), new Vector2(.75f, .82f)); pause.gameObject.AddComponent<Image>().color = panelColor;
            Text("Title", pause, "航程暂停", new Vector2(.08f, .81f), new Vector2(.92f, .96f), 34, TextAnchor.MiddleCenter);
            hud.resumeButton = Button("Resume", pause, "继续航程", new Vector2(.08f, .62f), new Vector2(.49f, .76f));
            hud.saveButton = Button("ManualSave", pause, "手动存档", new Vector2(.51f, .62f), new Vector2(.92f, .76f));
            hud.saveButton.interactable = false;
            hud.settingsButton = Button("Settings", pause, "设置", new Vector2(.08f, .41f), new Vector2(.49f, .55f));
            hud.controlsButton = Button("Controls", pause, "键位 / 换绑", new Vector2(.51f, .41f), new Vector2(.92f, .55f));
            hud.restartButton = Button("Restart", pause, "重新开始", new Vector2(.08f, .20f), new Vector2(.49f, .34f));
            hud.menuButton = Button("MainMenu", pause, "返回主菜单", new Vector2(.51f, .20f), new Vector2(.92f, .34f));
            hud.saveStatusText = Text("SaveStatus", pause, "", new Vector2(.08f, .035f), new Vector2(.92f, .17f), 17, TextAnchor.MiddleCenter);
            hud.pausePanel = pauseOverlay.gameObject; hud.pausePanel.SetActive(false);
            RectTransform end = Rect("EndingOverlay", canvas, new Vector2(.19f, .2f), new Vector2(.81f, .8f)); end.gameObject.AddComponent<Image>().color = panelColor;
            hud.endingText = Text("Ending", end, "航程结束", new Vector2(.07f, .38f), new Vector2(.93f, .93f), 30, TextAnchor.MiddleCenter);
            hud.endingRestartButton = Button("Restart", end, "再试一局", new Vector2(.1f, .12f), new Vector2(.47f, .30f));
            hud.endingMenuButton = Button("MainMenu", end, "返回主菜单", new Vector2(.53f, .12f), new Vector2(.9f, .30f));
            hud.endingPanel = end.gameObject; hud.endingPanel.SetActive(false);
            hud.controls = CreateControls(canvas, actionPrefab);
            hud.controls.captainInput = session.captain.input; hud.controls.diverInput = session.diver.input;
            hud.settings = CreateSettings(canvas, hud.controls); hud.settings.cameraRig = authority.cameraRig;
            return hud;
        }

        static CrewStationPanel CreateStationPanel(RectTransform parent, CrewRole role)
        {
            float left = role == CrewRole.Captain ? 0 : .5f;
            RectTransform panel = Rect(role + "StationPanel", parent, new Vector2(left, .13f), new Vector2(left + .5f, .82f));
            panel.gameObject.AddComponent<Image>().color = panelColor;
            CrewStationPanel controller = panel.gameObject.AddComponent<CrewStationPanel>(); controller.panel = panel.gameObject;
            controller.titleText = Text("Title", panel, "作业站", new Vector2(.04f, .88f), new Vector2(.83f, .99f), 24, TextAnchor.MiddleLeft);
            controller.closeButton = Button("CloseStation", panel, "×", new Vector2(.87f, .90f), new Vector2(.98f, .98f));
            controller.detailText = Text("Mechanism", panel, "", new Vector2(.04f, .74f), new Vector2(.96f, .88f), 17, TextAnchor.UpperLeft);
            controller.liveText = Text("LiveFeedback", panel, "", new Vector2(.04f, .57f), new Vector2(.96f, .73f), 18, TextAnchor.MiddleLeft);
            controller.rhythmFill = Bar("Rhythm", panel, new Vector2(.04f, .54f), new Vector2(.96f, .56f), new Color(.83f, .68f, .35f));
            controller.rhythmWindow = Rect("SuccessWindow", controller.rhythmFill.transform.parent, new Vector2(.63f, 0), new Vector2(.75f, 1)).gameObject.AddComponent<Image>();
            controller.rhythmWindow.color = new Color(.25f, .9f, .5f, .65f); controller.rhythmWindow.raycastTarget = false;
            controller.rhythmCursor = Rect("MovingPointer", controller.rhythmFill.transform.parent, new Vector2(.5f, 0), new Vector2(.5f, 1)).gameObject.AddComponent<Image>();
            controller.rhythmCursor.color = Color.white; controller.rhythmCursor.raycastTarget = false;
            controller.actionsRoot = ScrollContent("Actions", panel, new Vector2(.035f, .02f), new Vector2(.965f, .52f));
            controller.actionButtonPrefab = actionPrefab; panel.gameObject.SetActive(false);
            return controller;
        }

        static void CreateLocalization()
        {
            var root = new GameObject("Localization - editable Chinese English table");
            var manager = root.AddComponent<LocalizationManager>();
            manager.table = DefaultLocalizationBuilder.Build();
            manager.languageOverride = GameLanguage.System;
            var binder = root.AddComponent<LocalizationTextBinder>(); binder.scanWholeScene = true;
        }

        static GameSettingsPanel CreateSettings(RectTransform canvas, ControlsRebindPanel controls)
        {
            RectTransform overlay = Rect("GameSettings", canvas, Vector2.zero, Vector2.one);
            overlay.gameObject.AddComponent<Image>().color = new Color(.55f, .8f, .88f, .98f);
            RectTransform panel = Rect("SettingsCard", overlay, new Vector2(.22f, .17f), new Vector2(.78f, .83f));
            panel.gameObject.AddComponent<Image>().color = panelColor;
            var settings = overlay.gameObject.AddComponent<GameSettingsPanel>(); settings.panel = overlay.gameObject; settings.controls = controls;
            Text("Title", panel, "设置", new Vector2(.07f, .81f), new Vector2(.93f, .97f), 32, TextAnchor.MiddleLeft);
            settings.languageButton = Button("Language", panel, "语言：跟随系统", new Vector2(.07f, .61f), new Vector2(.93f, .76f));
            settings.languageLabel = settings.languageButton.GetComponentInChildren<Text>();
            settings.cameraButton = Button("ShipCamera", panel, "船上镜头：双人同屏", new Vector2(.07f, .43f), new Vector2(.93f, .58f));
            settings.cameraLabel = settings.cameraButton.GetComponentInChildren<Text>();
            settings.bindingsButton = Button("Bindings", panel, "角色键位 / 换绑", new Vector2(.07f, .25f), new Vector2(.93f, .40f));
            settings.closeButton = Button("Close", panel, "返回", new Vector2(.57f, .05f), new Vector2(.93f, .20f));
            overlay.gameObject.SetActive(false); return settings;
        }

        static ControlsRebindPanel CreateControls(RectTransform canvas, Button buttonPrefab)
        {
            RectTransform overlay = Rect("ControlsRebindPanel", canvas, Vector2.zero, Vector2.one);
            overlay.gameObject.AddComponent<Image>().color = new Color(.55f, .8f, .88f, .98f);
            RectTransform panel = Rect("BindingsCard", overlay, new Vector2(.12f, .08f), new Vector2(.88f, .92f));
            panel.gameObject.AddComponent<Image>().color = panelColor;
            ControlsRebindPanel controller = overlay.gameObject.AddComponent<ControlsRebindPanel>();
            controller.panel = overlay.gameObject; controller.rowButtonPrefab = buttonPrefab;
            Text("Title", panel, "角色键位 / INPUT SYSTEM", new Vector2(.04f, .88f), new Vector2(.96f, .98f), 27, TextAnchor.MiddleLeft);
            controller.captainTab = Button("Captain", panel, "船长", new Vector2(.04f, .79f), new Vector2(.48f, .87f));
            controller.diverTab = Button("Diver", panel, "潜水员", new Vector2(.52f, .79f), new Vector2(.96f, .87f));
            controller.statusText = Text("Status", panel, "", new Vector2(.04f, .70f), new Vector2(.96f, .79f), 17, TextAnchor.MiddleLeft);
            controller.rowsRoot = ScrollContent("Bindings", panel, new Vector2(.04f, .17f), new Vector2(.96f, .70f));
            controller.resetButton = Button("Reset", panel, "恢复该角色默认键位", new Vector2(.04f, .035f), new Vector2(.65f, .13f));
            controller.closeButton = Button("Close", panel, "返回", new Vector2(.71f, .035f), new Vector2(.96f, .13f));
            overlay.gameObject.SetActive(false); return controller;
        }

        static RectTransform CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(RuntimeUIFont));
            Canvas canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            return go.GetComponent<RectTransform>();
        }
        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
        }
        static Text Text(string name, Transform parent, string content, Vector2 min, Vector2 max, int size, TextAnchor align)
        {
            Text text = Rect(name, parent, min, max).gameObject.AddComponent<Text>(); text.text = content; text.color = textColor;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size; text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow; text.raycastTarget = false;
            return text;
        }
        static Button Button(string name, Transform parent, string label, Vector2 min, Vector2 max)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(actionPrefab.gameObject, parent); go.name = name; go.SetActive(true);
            RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            Button button = go.GetComponent<Button>(); button.GetComponentInChildren<Text>().text = label; return button;
        }
        static RectTransform ScrollContent(string name, Transform parent, Vector2 min, Vector2 max)
        {
            RectTransform root = Rect(name + "Scroll", parent, min, max);
            root.gameObject.AddComponent<Image>().color = new Color(.8f, .88f, .88f, .95f);
            root.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.viewport = root; scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 28;
            RectTransform content = Rect(name + "Content", root, new Vector2(0, 1), new Vector2(1, 1));
            content.pivot = new Vector2(.5f, 1);
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 7; layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content; scroll.movementType = ScrollRect.MovementType.Clamped; return content;
        }
        static Image Bar(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            RectTransform root = Rect(name + "Track", parent, min, max); root.gameObject.AddComponent<Image>().color = new Color(.66f, .77f, .8f);
            Image fill = Rect(name + "Fill", root, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            fill.sprite = FlatFillSprite();
            fill.color = color; fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.fillAmount = 1; fill.raycastTarget = false; return fill;
        }
        static Sprite FlatFillSprite()
        {
            const string folder = Root + "/Textures/UI";
            const string path = folder + "/FlatFill.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;
            Directory.CreateDirectory(folder);
            if (!File.Exists(path))
            {
                var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                var pixels = new Color[64]; for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point; importer.spriteBorder = Vector4.zero;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static void AddLight()
        {
            Light light = new GameObject("Main Light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1;
            light.transform.rotation = Quaternion.Euler(40, -25, 0);
        }
        static void EnsureLayer(int index, string name)
        {
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = settings.FindProperty("layers");
            if (layers.GetArrayElementAtIndex(index).stringValue == "" || layers.GetArrayElementAtIndex(index).stringValue == name)
            { layers.GetArrayElementAtIndex(index).stringValue = name; settings.ApplyModifiedPropertiesWithoutUndo(); }
            else Debug.LogWarning("LifeLine expected layer " + index + "=" + name + "; existing layer preserved.");
        }

        static void ExportNumericReference(GameSession session, WorldGenerator world, GameCameraRig rig, GameTuning tuning)
        {
            var text = new StringBuilder("# 实际数值参考（编辑器从资产与场景导出）\n\n单位：米、秒、百分比点；初始值来自实际 ScriptableObject/场景组件。后续编辑 Inspector 后可重新导出。\n\n");
            AppendFields(text, tuning); AppendFields(text, session); AppendFields(text, session.captain); AppendFields(text, session.diver); AppendFields(text, session.ship); AppendFields(text, world); AppendFields(text, rig);
            AppendFields(text, session.craftingManager);
            AppendFields(text, session.maps);
            AppendFields(text, session.GetOrCreateLifeline());
            AppendFields(text, session.GetOrCreateLifeline(true));
            AppendFields(text, UnityEngine.Object.FindAnyObjectByType<UnderwaterShipProxy>());
            foreach (CrewStationPanel view in UnityEngine.Object.FindObjectsByType<CrewStationPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None)) AppendFields(text, view);
            AppendFields(text, UnityEngine.Object.FindFirstObjectByType<NightVisibilityView>());
            AppendFields(text, UnityEngine.Object.FindAnyObjectByType<ShipRouteView>());
            AppendFields(text, UnityEngine.Object.FindAnyObjectByType<DiverLifelineView>());
            AppendFields(text, UnityEngine.Object.FindAnyObjectByType<GameSaveManager>());
            var hud = UnityEngine.Object.FindAnyObjectByType<GameHUDController>();
            if (hud != null) AppendFields(text, hud.filterPuzzle);
            text.Append("\n## 可替换预制体的参数\n\n以下为预制体 Inspector 值。敌人生命等运行初始化值另见机制文档，不以此处未生成的运行状态代替。\n");
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Root + "/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                text.Append("\n资产：`").Append(path).Append("`\n");
                foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component != null && component.GetType().Namespace == "Lifeline") AppendFields(text, component);
            }
            foreach (RecipeDefinition recipe in session.recipes)
            {
                text.Append("\n## 配方 ").Append(recipe.id).Append(" / ").Append(recipe.displayName).Append("\n\n");
                text.Append("成本：").Append(Amounts(recipe.cost)).Append("；产出：").Append(Amounts(recipe.output)).Append("；工具=").Append(recipe.toolId).Append("；电台阶段=").Append(recipe.radioStage).Append("；船体恢复=").Append(recipe.repairAmount).Append("；制作时间倍率=").Append(recipe.craftTimeMultiplier).Append("。\n");
            }
            File.WriteAllText(Root + "/Documentation/02_实际数值参考.md", text.ToString());
        }
        [MenuItem("LifeLine/Documentation/Export Current Tuning")]
        public static void ExportCurrentTuning()
        {
            GameSession session = UnityEngine.Object.FindFirstObjectByType<GameSession>();
            if (session == null) { Debug.LogWarning("请先打开 Gameplay 场景。"); return; }
            ExportNumericReference(session, session.world, UnityEngine.Object.FindFirstObjectByType<GameCameraRig>(), session.tuning); AssetDatabase.Refresh();
        }
        static void AppendFields(StringBuilder builder, object component)
        {
            if (component == null) return;
            builder.Append("\n## ").Append(component.GetType().Name);
            if (component is UnityEngine.Object unityObject) builder.Append(" · ").Append(unityObject.name);
            builder.Append("\n\n| public字段 | 当前值 |\n| --- | --- |\n");
            foreach (FieldInfo field in component.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Type type = field.FieldType;
                if (!(type.IsPrimitive || type.IsEnum || type == typeof(LayerMask) || type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4) || type == typeof(Vector2Int) || type == typeof(Rect) || type == typeof(Color)
                    || type == typeof(Vector2Int[]) || (type.IsArray && (type.GetElementType().IsPrimitive || type.GetElementType().IsEnum)) || type == typeof(ResourceAmount[]))) continue;
                object value = field.GetValue(component); string rendered;
                if (value is LayerMask mask) rendered = mask.value.ToString(CultureInfo.InvariantCulture);
                else if (value is ResourceAmount[] amounts) rendered = Amounts(amounts);
                else if (value is Array values) { string[] items = new string[values.Length]; for (int i = 0; i < values.Length; i++) items[i] = Convert.ToString(values.GetValue(i), CultureInfo.InvariantCulture); rendered = string.Join(", ", items); }
                else rendered = Convert.ToString(value, CultureInfo.InvariantCulture);
                builder.Append('|').Append(field.Name).Append('|').Append(rendered).Append("|\n");
            }
        }
        static string Amounts(ResourceAmount[] amounts)
        {
            string result = ""; if (amounts == null) return "无";
            foreach (ResourceAmount amount in amounts) if (amount != null) result += GameNames.Resource(amount.resource) + "×" + amount.amount + " ";
            return string.IsNullOrEmpty(result) ? "无" : result;
        }
        [MenuItem("LifeLine/Validation/Run Gameplay Rules Checks")]
        public static void RunRulesChecks() { Debug.Log(GameplayRulesChecks.Run()); Debug.Log(WorldGenerationChecks.ValidateMazeSeeds()); }
        [MenuItem("LifeLine/Developer/Add Test Supplies (Play Mode)")]
        public static void AddTestSupplies()
        {
            if (!Application.isPlaying || GameSession.Current == null) return;
            foreach (ResourceKind kind in Enum.GetValues(typeof(ResourceKind))) GameSession.Current.AddResource(kind, 15);
            GameSession.Current.Tell("编辑器测试补给已加入（非正式初始资源）。");
        }
    }
}
#endif
