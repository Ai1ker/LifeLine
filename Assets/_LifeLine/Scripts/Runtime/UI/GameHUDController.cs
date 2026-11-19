using System;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Lifeline
{
    public class GameHUDController : MonoBehaviour
    {
        public GameSession session;
        public SessionAuthority authority;
        public Text bankText, shipText, captainText, diverText, questText, messageText, clockText;
        public Text questChapterText;
        public OnboardingView onboarding;
        public CrewScreenOverlay crewOverlay;
        public Image hullFill, powerFill, oxygenFill;
        public CrewStationPanel captainPanel, diverPanel;
        public Button pauseButton, controlsButton, resumeButton, restartButton, menuButton;
        public Button settingsButton;
        public Button saveButton;
        public Text saveStatusText;
        public event Action ManualSaveRequested;
        public GameSettingsPanel settings;
        public GameObject pausePanel, endingPanel;
        public Text endingText;
        public Button endingRestartButton, endingMenuButton;
        public ControlsRebindPanel controls;
        public TutorialUIController tutorial;
        public PipeRepairPuzzle filterPuzzle = new PipeRepairPuzzle();
        public bool[] circuitConnected = new bool[4];
        public string menuSceneName = "MainMenu";
        private bool endingShown;
        private float nextHudUpdate;
        private int lastPauseFrame = -1;
        private bool initialized;
        string shownNotice;
        float noticeExpires;

        private void Start() => Initialize();
        public void Initialize()
        {
            if (initialized) return;
            if (session == null) session = FindAnyObjectByType<GameSession>();
            if (session == null) return;
            captainPanel?.Initialize(this, session, session.captain);
            diverPanel?.Initialize(this, session, session.diver);
            session.StationRequested += OpenStation;
            session.CommandReceived += OnCommand;
            session.OperationInterrupted += OnOperationInterrupted;
            ConfigureModalLayer(pausePanel, 100); ConfigureModalLayer(endingPanel, 110);
            if(settings!=null)ConfigureModalLayer(settings.panel,120);
            if(controls!=null)ConfigureModalLayer(controls.panel,130);
            if (controls != null) { controls.captainInput = session.captain != null ? session.captain.input : null; controls.diverInput = session.diver != null ? session.diver.input : null; }
            pauseButton?.onClick.AddListener(TogglePause);
            controlsButton?.onClick.AddListener(OpenControls);
            settingsButton?.onClick.AddListener(OpenSettings);
            saveButton?.onClick.AddListener(RequestManualSave);
            resumeButton?.onClick.AddListener(() => SetPause(false));
            restartButton?.onClick.AddListener(Restart);
            menuButton?.onClick.AddListener(ReturnMenu);
            endingRestartButton?.onClick.AddListener(Restart);
            endingMenuButton?.onClick.AddListener(ReturnMenu);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (endingPanel != null) endingPanel.SetActive(false);
            // Tutorial UI is built at runtime (no scene/prefab edits). Attach to this HUD object.
            if (tutorial == null) tutorial = gameObject.AddComponent<TutorialUIController>();
            tutorial.Initialize(session);
            // Teammate's onboarding (8-step lessons + captain oxygen) coexists for now.
            if (onboarding == null) onboarding = GetComponent<OnboardingView>();
            if (onboarding == null) onboarding = gameObject.AddComponent<OnboardingView>();
            onboarding.Initialize(this);
            if (crewOverlay == null) crewOverlay = GetComponent<CrewScreenOverlay>();
            if (crewOverlay == null) crewOverlay = gameObject.AddComponent<CrewScreenOverlay>();
            crewOverlay.Initialize(this);
            session.captain?.GetComponent<CrewPresentation>()?.SetFront(true);
            session.diver?.GetComponent<CrewPresentation>()?.SetFront(false);
            initialized = true;
        }
        private void OnDestroy()
        {
            if (session == null) return;
            session.StationRequested -= OpenStation;
            session.CommandReceived -= OnCommand;
            session.OperationInterrupted -= OnOperationInterrupted;
        }
        private void Update()
        {
            // Facility manual hotkey (H). Not bound to either crew role, so it never conflicts.
            if (tutorial != null && UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.hKey.wasPressedThisFrame)
                tutorial.ToggleManual();
            if (session == null || session.tuning == null || OnlineVoyage.IsGuest && !OnlineVoyage.Current.GuestReady) return;
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = session.phase != SessionPhase.Playing;
            if (endingShown && session.phase != SessionPhase.Won && session.phase != SessionPhase.Lost)
            {
                endingShown = false;
                endingPanel?.SetActive(false);
            }
            if ((session.phase == SessionPhase.Won || session.phase == SessionPhase.Lost) && !endingShown)
            {
                endingShown = true; captainPanel.Close(); diverPanel.Close(); endingPanel.SetActive(true);endingPanel.transform.SetAsLastSibling();ConfigureModalLayer(endingPanel,110);
                if(session.phase==SessionPhase.Lost){var save=FindAnyObjectByType<GameSaveManager>(); endingRestartButton.interactable=save!=null&&save.HasValidSave(out _);endingRestartButton.GetComponentInChildren<Text>().text=UiLocalization.Text("回退到上个存档");}
                endingText.text = session.phase == SessionPhase.Won ? "从获救者，成为救援者\n幸存者已登船。你们守住了同一根绳。" : session.StatusMessage;
            }
            if (Time.unscaledTime < nextHudUpdate) return;
            nextHudUpdate = Time.unscaledTime + .1f;
            var bank = new StringBuilder();
            foreach (ResourceKind resource in System.Enum.GetValues(typeof(ResourceKind)))
                bank.Append(GameNames.Resource(resource)).Append(' ').Append(session.GetResource(resource)).Append("   ");
            bankText.text = bank.ToString();
            shipText.text = "船体 " + session.hull.ToString("0") + "/" + session.hullMax.ToString("0") + "   电力 " + session.power.ToString("0") + "%   航线 " + Mathf.Clamp01(session.ship.Position.x / session.tuning.routeDistance).ToString("P0");
            crewOverlay?.RefreshBackpack();
            questText.text = session.QuestText;
            if (questChapterText != null) questChapterText.text = string.Format("主线 · 第{0}章", session.QuestChapter);
            RefreshNotice();
            clockText.text = "DAY " + session.day + " · " + (session.IsNight ? "夜晚" : session.IsDusk ? "黄昏" : "白昼") + "  " + (session.elapsed % session.tuning.daySeconds).ToString("0") + "s";
            hullFill.fillAmount = session.hullMax > 0 ? session.hull / session.hullMax : 0;
            powerFill.fillAmount = session.power / Mathf.Max(1, session.tuning.maxPower);
            if(oxygenFill!=null && session.diver!=null)oxygenFill.fillAmount=session.diver.oxygenMax>0?session.diver.oxygen/session.diver.oxygenMax:0;
        }
        void RefreshNotice()
        {
            if (messageText == null) return;
            bool waiting = OnlineVoyage.WaitingForCrew;
            string text = OnlineVoyage.IsGuest && waiting ? UiLocalization.Text("online.waiting.host") : session.StatusMessage;
            if (!waiting && authority != null && authority.onlineView && session.StatusAudienceRole >= 0 &&
                session.StatusAudienceRole != (int)authority.localRole) text = null;
            if (shownNotice != text)
            {
                shownNotice = text;
                noticeExpires = Time.unscaledTime + 3f;
            }
            bool show = !string.IsNullOrEmpty(text) && (waiting || Time.unscaledTime < noticeExpires);
            messageText.gameObject.SetActive(show);
            if (show) messageText.text = UiLocalization.Text(text);
        }
        private void OpenStation(StationKind kind, CrewActor actor)
        {
            CrewStationPanel own = actor.role == CrewRole.Captain ? captainPanel : diverPanel;
            CrewStationPanel other = actor.role == CrewRole.Captain ? diverPanel : captainPanel;
            if (session.phase != SessionPhase.Playing) return;
            if (other != null && other.IsOpen && other.station == kind) { session.Tell("同伴正在操作这个站点。", actor); return; }
            own?.Open(kind);
        }
        private void OnOperationInterrupted(CrewActor actor) => (actor.role == CrewRole.Captain ? captainPanel : diverPanel)?.Close();
        public static void ConfigureModalLayer(GameObject panel, int order)
        {
            if(panel==null)return;
            var canvas=panel.GetComponent<Canvas>();if(canvas==null)canvas=panel.AddComponent<Canvas>();
            canvas.overrideSorting=true;canvas.sortingOrder=order;
            if(panel.GetComponent<GraphicRaycaster>()==null)panel.AddComponent<GraphicRaycaster>();
        }
        private void OnCommand(CrewActor actor, CrewCommand command)
        {
            if (command.pause) { HandleEscape(); return; }
            if (session.phase != SessionPhase.Playing) return;
            if (command.backpack && authority != null && authority.onlineView && actor.role == authority.localRole)
                crewOverlay?.ToggleBackpack();
            (actor.role == CrewRole.Captain ? captainPanel : diverPanel)?.HandleCommand(command);
        }
        /// <summary>A shared keyboard Escape dismisses one UI layer; both role maps consume the same frame once.</summary>
        public bool HandleEscape()
        {
            // The first actor may change phase in FixedUpdate before the peer gets a physics tick.
            // Clear only the mirrored pause edge now, so it cannot resume the game on a later Update.
            if (session != null)
            {
                session.captain?.input?.DiscardPauseEdge();
                session.diver?.input?.DiscardPauseEdge();
            }
            if (lastPauseFrame == Time.frameCount) return true;
            lastPauseFrame = Time.frameCount;
            if (controls != null && controls.IsOpen) { controls.HandleEscape(); return true; }
            if (settings != null && settings.IsOpen) { settings.Close(); return true; }
            if (tutorial != null && tutorial.ManualOpen) { tutorial.ToggleManual(); return true; }
            if (OnlineVoyage.InProgress)
            {
                var localPanel = OnlineVoyage.Current.LocalRole == CrewRole.Captain ? captainPanel : diverPanel;
                if (localPanel != null && localPanel.IsOpen) { localPanel.RequestClose(); return true; }
                if (session.phase == SessionPhase.Playing || session.phase == SessionPhase.Paused)
                {
                    TogglePause();
                    return true;
                }
                return false;
            }
            if ((captainPanel != null && captainPanel.IsOpen) || (diverPanel != null && diverPanel.IsOpen))
            { CloseStationPanels(); return true; }
            if (session == null || session.phase != SessionPhase.Playing && session.phase != SessionPhase.Paused) return false;
            SetPause(session.phase == SessionPhase.Playing); return true;
        }
        private void CloseStationPanels() { captainPanel?.Close(); diverPanel?.Close(); }
        public void CloseTransientPanels()
        {
            controls?.Close(); settings?.Close(); CloseStationPanels();
        }
        public void OpenControls() { SetPause(true); controls?.Open(); }
        public void OpenSettings() { SetPause(true); settings?.Open(); }
        public void TogglePause() { if (session != null) SetPause(OnlineVoyage.InProgress ? !OnlineVoyage.Current.LocalMenuOpen : session.phase == SessionPhase.Playing); }
        public void SetPause(bool paused)
        {
            if (session == null) return;
            if (session.phase == SessionPhase.Won || session.phase == SessionPhase.Lost) return;
            if (OnlineVoyage.InProgress)
            {
                OnlineVoyage.Current.LocalMenuOpen = paused;
                if (OnlineVoyage.IsGuest)
                {
                    if (!paused) CloseTransientPanels();
                    SetPausePanelVisible(paused);
                    return;
                }
                if (!paused && !OnlineVoyage.Current.CanResumePlay)
                {
                    CloseTransientPanels();
                    SetPausePanelVisible(false);
                    return;
                }
            }
            if (paused) CloseStationPanels();
            else CloseTransientPanels();
            session.Pause(paused);
            SetPausePanelVisible(paused);
        }
        void SetPausePanelVisible(bool paused)
        {
            if (pausePanel == null) return;
            pausePanel.SetActive(paused);
            if (paused) { pausePanel.transform.SetAsLastSibling(); ConfigureModalLayer(pausePanel, 100); }
        }
        public void RequestManualSave() { ManualSaveRequested?.Invoke(); }
        public void SetManualSaveAvailable(bool available) { if (saveButton != null) saveButton.interactable = available; }
        public void SetSaveStatus(string status) { if (saveStatusText != null) saveStatusText.text = status; }
        public void Restart()
        {
            if (OnlineVoyage.InProgress)
            {
                if (OnlineVoyage.Current.ReloadHostSave()) { endingShown = false; endingPanel?.SetActive(false); }
                return;
            }
            if (session != null && session.phase == SessionPhase.Lost) GameSaveManager.RequestContinue(); else GameSaveManager.RequestNewGame();
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        public void ReturnMenu()
        {
            if (OnlineVoyage.InProgress) { OnlineVoyage.Current.ExitToMenu(); return; }
            Time.timeScale = 1;
            SceneManager.LoadScene(menuSceneName);
        }
    }
}
