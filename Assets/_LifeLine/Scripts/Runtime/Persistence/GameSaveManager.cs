using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lifeline
{
    /// <summary>Local single-slot save flow. Owns scene boot so Continue never races GameSession.Start.</summary>
    [DefaultExecutionOrder(-250)]
    [DisallowMultipleComponent]
    public sealed class GameSaveManager : MonoBehaviour
    {
        [Header("Scene dependencies / editable references")]
        public GameSession session;
        public GameHUDController hud;
        public MainMenuController mainMenu;
        public bool bootOnStart = true;
        [Tooltip("Blank uses Application.persistentDataPath/LifeLine/save-v1.json. Set only for isolated tests or custom profiles.")]
        public string pathOverride;
        [Header("Runtime save status")]
        public int lastAutoSavedDay;
        public string LastError;
        public bool loadFailed;
        [Min(1)] public float autoRetrySeconds = 5;
        [Min(0), Tooltip("Keep automatic save IO off the map-refresh / scene-initialization frame. Manual saves remain immediate.")]
        public float automaticSaveDelay = .25f;
        public event Action<string> SaveFailed;
        public event Action<SaveSummary> SaveCompleted;
        static bool continueRequested;
        int pendingAutoDay;
        float nextAutoAttempt;
        public string SavePath => string.IsNullOrWhiteSpace(pathOverride) ? GameSaveStore.DefaultPath : pathOverride;
        public static void RequestContinue() { continueRequested = true; }
        public static void RequestNewGame() { continueRequested = false; }

        void Awake()
        {
            if (session == null) session = GetComponent<GameSession>();
            if (session == null) session = FindAnyObjectByType<GameSession>();
            if (session != null) session.autoStart = false;
        }
        void Start()
        {
            if (hud == null) hud = FindAnyObjectByType<GameHUDController>();
            if (mainMenu == null) mainMenu = FindAnyObjectByType<MainMenuController>();
            if (session != null) { session.SessionReady += OnSessionReady; session.DayReady += OnDayReady; }
            if (hud != null) hud.ManualSaveRequested += ManualSave;
            if (mainMenu != null)
            {
                mainMenu.ContinueRequested += ContinueFromMenu;
                // This listener is attached before the menu's default-order Start adds its scene-load listener.
                mainMenu.localCoopButton?.onClick.AddListener(RequestNewGame);
                mainMenu.SetContinueAvailable(HasValidSave(out _));
            }
            if (session != null && bootOnStart) BootSession();
            RefreshManualSaveButton();
        }
        void OnDestroy()
        {
            if (session != null) { session.SessionReady -= OnSessionReady; session.DayReady -= OnDayReady; }
            if (hud != null) hud.ManualSaveRequested -= ManualSave;
            if (mainMenu != null) { mainMenu.ContinueRequested -= ContinueFromMenu; mainMenu.localCoopButton?.onClick.RemoveListener(RequestNewGame); }
        }
        void LateUpdate()
        {
            if (pendingAutoDay > 0 && session != null && !loadFailed && Time.unscaledTime >= nextAutoAttempt && session.phase == SessionPhase.Playing && !session.IsNight && !session.IsDusk)
            {
                if (session.day <= lastAutoSavedDay || TryAutoSave(out _)) pendingAutoDay = 0;
                else nextAutoAttempt = Time.unscaledTime + Mathf.Max(1, autoRetrySeconds);
            }
            RefreshManualSaveButton();
        }
        void RefreshManualSaveButton() =>
            hud?.SetManualSaveAvailable(!loadFailed && !OnlineVoyage.IsGuest && session != null && session.IsInitialized &&
                ManualSaveAllowed(session));

        static bool ManualSaveAllowed(GameSession game) => game != null &&
            (game.phase == SessionPhase.Paused || game.phase == SessionPhase.Playing &&
                OnlineVoyage.InProgress && !OnlineVoyage.IsGuest && OnlineVoyage.Current.LocalMenuOpen);
        public bool BootSession()
        {
            bool load = continueRequested; continueRequested = false;
            if (session == null) return Fail("未找到游戏会话。", out _);
            TutorialProgress.ForSession(session);
            if (OnlineVoyage.InProgress) return OnlineVoyage.Current.Boot(this);
            if (load)
            {
                if (!TryLoad(session, out string error))
                { loadFailed = true; session.phase = SessionPhase.Paused; Time.timeScale = 0; session.Tell("读档失败：" + error); hud?.Initialize(); hud?.SetPause(true); return false; }
            }
            else { lastAutoSavedDay = 0; loadFailed = false; session.Initialize(); }
            hud?.Initialize(); return true;
        }
        void ContinueFromMenu()
        {
            if (mainMenu == null || !HasValidSave(out _)) return;
            RequestContinue(); SceneManager.LoadScene(mainMenu.gameplaySceneName);
        }
        void ManualSave() { if (session != null) TrySave(session, SaveReason.Manual, out _); }
        void OnSessionReady(bool restored) { if (!restored && !OnlineVoyage.IsGuest) ScheduleAutomaticSave(session.day); }
        void OnDayReady(int day) { if (!OnlineVoyage.IsGuest && day > lastAutoSavedDay) ScheduleAutomaticSave(day); }
        void ScheduleAutomaticSave(int day)
        {
            pendingAutoDay = day;
            nextAutoAttempt = Time.unscaledTime + Mathf.Max(0, automaticSaveDelay);
        }
        public bool HasValidSave(out SaveSummary summary)
        {
            summary = null;
            if (OnlineVoyage.IsGuest) return false;
            if (!GameSaveStore.TryRead(SavePath, out GameSaveData data, out string error)) { LastError = error; mainMenu?.SetSaveStatus(File.Exists(SavePath) ? LastError : ""); return false; }
            summary = Summary(data); LastError = ""; mainMenu?.SetSaveStatus(""); return true;
        }
        public bool TryAutoSave(out string error)
        {
            error = "";
            if (session == null || !session.IsInitialized || loadFailed || session.day <= lastAutoSavedDay || session.phase != SessionPhase.Playing || session.IsNight || session.IsDusk) return false;
            return TrySave(session, SaveReason.Automatic, out error);
        }
        public bool TrySave(GameSession game, SaveReason reason, out string error)
        {
            bool allowed = reason == SaveReason.Manual ? ManualSaveAllowed(game) : game != null && game.phase == SessionPhase.Playing;
            if (OnlineVoyage.IsGuest || game == null || !game.IsInitialized || loadFailed || !allowed) return Fail("当前状态不能保存。", out error);
            if (reason == SaveReason.Automatic && (game.day <= lastAutoSavedDay || game.IsNight || game.IsDusk)) { error = ""; return false; }
            try
            {
                GameSaveData data = game.CaptureSaveData(hud);
                data.reason = reason; data.lastAutoSavedDay = Mathf.Max(game.day, lastAutoSavedDay);
                if (!GameSaveStore.TryWrite(SavePath, data, out error)) return Fail(error, out error);
                lastAutoSavedDay = data.lastAutoSavedDay; LastError = "";
                hud?.SetSaveStatus(reason == SaveReason.Manual ? "手动存档已保存。" : "已完成今日自动存档。");
                SaveCompleted?.Invoke(Summary(data)); return true;
            }
            catch (Exception exception) { return Fail("保存游戏失败：" + exception.Message, out error); }
        }
        public bool TryLoad(GameSession game, out string error)
        {
            if (!GameSaveStore.TryRead(SavePath, out GameSaveData data, out error)) { loadFailed = true; return Fail(error, out error); }
            if (game == null) { loadFailed = true; return Fail("未找到游戏会话。", out error); }
            if (!game.TryRestoreSaveData(data, out error, hud)) { loadFailed = true; return Fail(error, out error); }
            lastAutoSavedDay = data.lastAutoSavedDay; pendingAutoDay = 0; loadFailed = false; LastError = ""; return true;
        }
        bool Fail(string message, out string error) { error = LastError = message; hud?.SetSaveStatus(message); SaveFailed?.Invoke(message); Debug.LogWarning(message, this); return false; }
        static SaveSummary Summary(GameSaveData data) => new SaveSummary { day = data.state.day, elapsed = data.state.elapsed, savedUtc = data.savedUtc, reason = data.reason };
    }
}
