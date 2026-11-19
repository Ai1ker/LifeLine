#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lifeline.Development
{
    /// <summary>Optional self-contained test tools. Delete the Development folder to remove.</summary>
    public sealed class DeveloperTestTools : MonoBehaviour
    {
        public bool visible;
        public Rect window = new Rect(15, 100, 390, 540);
        public int resourceAmount = 10;
        public int testDay = 2;
        public bool skipInteractionQte;
        public bool infiniteOxygen;
        GameSession oxygenSession;
        Vector2 scroll;
        GameSaveManager save;
        string previousPath;
        float previousScale = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindAnyObjectByType<DeveloperTestTools>() != null) return;
            var obj = new GameObject("Developer test tools (F8, removable)");
            DontDestroyOnLoad(obj); obj.AddComponent<DeveloperTestTools>();
        }

        void Update()
        {
            if (OnlineVoyage.InProgress) { visible = false; return; }
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame) visible = !visible;
            // Test commands must not overwrite the player's automatic/manual slot.
            if (save != null && save.gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene()) ReleaseSave();
            if (!infiniteOxygen && oxygenSession != null) { oxygenSession.developerInfiniteOxygen = false; oxygenSession = null; }
            if (infiniteOxygen && GameSession.Current != null && GameSession.Current.IsInitialized && oxygenSession != GameSession.Current)
                SetInfiniteOxygen(GameSession.Current, true);
            if (skipInteractionQte) CompleteOpenInteractions();
        }
        void IsolateSave()
        {
            var manager = FindAnyObjectByType<GameSaveManager>();
            if (manager == null || manager == save) return;
            ReleaseSave(); save = manager; previousPath = manager.pathOverride;
            manager.pathOverride = System.IO.Path.Combine(Application.persistentDataPath, "LifeLine", "developer-test.json");
        }
        void ReleaseSave() { if (save != null) save.pathOverride = previousPath; save = null; }
        void OnDestroy() { if (oxygenSession != null) oxygenSession.developerInfiniteOxygen = false; ReleaseSave(); if (Time.timeScale > 1) Time.timeScale = previousScale; }
        string T(string english) => DeveloperToolsText.Get(english);
        void OnGUI()
        {
            if (OnlineVoyage.InProgress) return;
            if (visible) window = GUI.Window(1751801, window, Draw, T("Developer test tools / F8"));
        }
        void Draw(int id)
        {
            var game = GameSession.Current;
            if (game == null || !game.IsInitialized) { GUILayout.Label(T("Start a local game first.")); GUI.DragWindow(); return; }
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label(T("Commands use a separate developer save slot."));
            GUILayout.Label("DAY " + game.day + " · " + game.WinchTargetCount + " × " + game.WinchEfficiency.ToString("P0"));
            bool skip = GUILayout.Toggle(skipInteractionQte, T("Skip active interaction QTE"));
            if (skip != skipInteractionQte) { IsolateSave(); skipInteractionQte = skip; }
            bool unlimited = GUILayout.Toggle(infiniteOxygen, T("Infinite oxygen / both crew"));
            if (unlimited != infiniteOxygen) SetInfiniteOxygen(game, unlimited);
            if (GUILayout.Button(T("Complete current interaction"))) { IsolateSave(); CompleteOpenInteractions(); }
            GUILayout.BeginHorizontal();
            testDay = Mathf.Clamp(testDay, 1, 99);
            if (GUILayout.Button("-")) { testDay = Mathf.Max(1,testDay-1); SetTime(game,game.DayProgress); }
            GUILayout.Label("DAY " + testDay);
            if (GUILayout.Button("+")) { testDay=Mathf.Min(99,testDay+1); SetTime(game,game.DayProgress); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("Daylight"))) SetTime(game, .1f);
            if (GUILayout.Button(T("Thunderstorm"))) SetTime(game, game.tuning.dayFraction + game.tuning.duskFraction + .02f);
            GUILayout.EndHorizontal();
            if(GUILayout.Button(T("Next day with cave")))NextDayWithCave(game);
            GUI.enabled = !game.filterInstalled;
            if (GUILayout.Button(T(game.filterInstalled ? "Seawater filter already acquired" : "Next day with City seawater filter"))) NextDayWithFilter(game);
            GUI.enabled = true;
            if (GUILayout.Button(T("Move both crew into cabin")))
            {
                IsolateSave(); game.captain.RestorePose(true, game.ShipPoint(new Vector3(-3, -3.1f, 0)), Vector3.zero, Vector2.right);
                game.diver.RestorePose(true, game.ShipPoint(new Vector3(3, -3.1f, 0)), Vector3.zero, Vector2.left);
            }
            if (GUILayout.Button(T("Add materials / fuel")))
            {
                IsolateSave(); foreach (ResourceKind kind in new[] { ResourceKind.Wood, ResourceKind.Metal, ResourceKind.Fiber, ResourceKind.Plank, ResourceKind.Plate, ResourceKind.FiberBundle, ResourceKind.Fuel }) game.AddResource(kind, resourceAmount);
            }
            if (GUILayout.Button(T("Restore hull / power / oxygen / crew")))
            {
                IsolateSave(); game.hull = game.hullMax; game.power = game.tuning.maxPower;
                game.captain.Revive(); game.diver.Revive(); game.diverFainted = false;game.captain.fainted=game.diver.fainted=false;
                game.diver.oxygen = game.GetOxygenMax(); game.diverBites = 0;
            }
            if (GUILayout.Button(T("Create 2 attached cargo crates"))) CreateCargo(game);
            if (GUILayout.Button(T("Summon abyss shadow"))) { IsolateSave(); game.SetDayClock(Mathf.Max(4,game.day),.9f); game.SpawnEnemy(EnemyKind.Shadow,game.BoatAnchor+new Vector3(5,-5,0)); }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("Speed x1"))) Time.timeScale = game.phase == SessionPhase.Paused ? 0 : 1;
            if (GUILayout.Button(T("Speed x4"))) { previousScale = 1; if (game.phase == SessionPhase.Playing) Time.timeScale = 4; }
            GUILayout.EndHorizontal();
            GUILayout.Label(T("Delete Scripts/Runtime/Development to remove all tools."));
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, window.width, 24));
        }
        public void NextDayWithCave(GameSession game)
        {
            if(game==null||game.world==null)return;
            IsolateSave();testDay=game.day+1;game.world.forceCaveDay=testDay;game.SetDayClock(testDay,.1f);
        }
        public void SetInfiniteOxygen(GameSession game, bool enabled)
        {
            if (game == null) return;
            IsolateSave();
            if (oxygenSession != null && oxygenSession != game) oxygenSession.developerInfiniteOxygen = false;
            infiniteOxygen = enabled; oxygenSession = enabled ? game : null;
            game.developerInfiniteOxygen = enabled;
        }
        public void NextDayWithFilter(GameSession game)
        {
            if (game == null || game.world == null || game.filterInstalled) return;
            IsolateSave(); testDay = game.day + 1; game.world.forceFilterDay = testDay;
            game.SetDayClock(testDay, .1f);
        }
        public bool CompleteOpenInteractions()
        {
            var game = GameSession.Current;
            if (game == null || !game.IsInitialized || game.phase != SessionPhase.Playing) return false;
            IsolateSave(); bool completed = false;
            foreach (var hud in FindObjectsByType<GameHUDController>())
                if (hud.session == game)
                {
                    completed |= game.DeveloperCompleteInteraction(hud.captainPanel, game.captain, hud);
                    completed |= game.DeveloperCompleteInteraction(hud.diverPanel, game.diver, hud);
                }
            return completed;
        }
        public void SetTime(GameSession game, float fraction)
        {
            IsolateSave(); game.SetDayClock(testDay, fraction, fraction >= game.tuning.dayFraction + game.tuning.duskFraction);
        }
        public void CreateCargo(GameSession game)
        {
            if (game.world == null) return;
            IsolateSave();
            for (int i = 0; i < 2; i++)
            {
                Vector3 point = game.BoatAnchor + new Vector3(i == 0 ? -2 : 2, -4, 0);
                var obj = game.world.cratePrefab != null ? Instantiate(game.world.cratePrefab, point, Quaternion.identity, game.world.transform) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.name = "Developer cargo"; obj.transform.SetParent(game.world.transform, true); obj.transform.position = point;
                if (obj.GetComponent<Collider>() != null) obj.GetComponent<Collider>().isTrigger = true;
                var cargo = obj.GetComponent<LootPickup>(); if (cargo == null) cargo = obj.AddComponent<LootPickup>();
                cargo.world = game.world; cargo.isCrate = cargo.attached = true; cargo.consumed = false; cargo.amount = 0;
                cargo.capacity = game.tuning.crateCapacity; cargo.contents.Clear(); cargo.contents.Add(ResourceKind.Wood);
                game.world.pickups.Add(cargo); game.RegisterAttachedCargo(cargo); cargo.RefreshVisual();
            }
        }
    }
}
#endif
