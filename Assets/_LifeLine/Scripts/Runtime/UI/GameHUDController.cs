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
        public PipeRepairPuzzle filterPuzzle = new PipeRepairPuzzle();
        public bool[] circuitConnected = new bool[4];
        public string menuSceneName = "MainMenu";
        private bool endingShown;
        private float nextHudUpdate;
        private int lastPauseFrame = -1;
        private bool initialized;

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
            if (session == null || session.tuning == null) return;
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = session.phase != SessionPhase.Playing;
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
            captainText.text = CrewLabel(session.captain);
            diverText.text = CrewLabel(session.diver);
            questText.text = session.QuestText;
            if (questChapterText != null) questChapterText.text = string.Format("主线 · 第{0}章", session.QuestChapter);
            messageText.text = session.StatusMessage;
            clockText.text = "DAY " + session.day + " · " + (session.IsNight ? "夜晚" : session.IsDusk ? "黄昏" : "白昼") + "  " + (session.elapsed % session.tuning.daySeconds).ToString("0") + "s";
            hullFill.fillAmount = session.hullMax > 0 ? session.hull / session.hullMax : 0;
            powerFill.fillAmount = session.power / Mathf.Max(1, session.tuning.maxPower);
            var oxygenActor=session.captain!=null&&!session.captain.onboard?session.captain:session.diver;
            oxygenFill.fillAmount=oxygenActor.oxygenMax>0?oxygenActor.oxygen/oxygenActor.oxygenMax:0;
        }
        private string CrewLabel(CrewActor actor)
        {
            string name = authority != null && authority.onlineView ? authority.NameFor(actor.role) : GameNames.Role(actor.role);
            int roleIndex = (int)actor.role;
            if (session.reviveTargets[roleIndex] != null)
                return name + " · 扶起同伴 " + session.reviveProgress[roleIndex].ToString("0.0") + "/" + session.tuning.reviveSeconds.ToString("0") + "s · 持续按住互动";
            if(actor==session.captain&&session.captainBoardHeld)return name+" · 回船 "+session.captainBoardProgress.ToString("0.0")+"/"+session.captainBoardSeconds.ToString("0.#")+"s";
            if(session.IsHidden(actor))return name+" · 藏匿中，互动离开";
            if (actor.downed||session.IsFainted(actor)) return name + " · 倒地，等待同伴扶起";
            if (actor.onboard)
            {
                var station = actor.NearbyStation;
                return name + " · 船上  " + (station != null ? GameNames.Station(station.kind) + " [" + actor.input.GetBindingLabel("Interact") + "]" : "行走 / 跳跃 / 梯子");
            }
            return name + " · 水下  O₂ " + actor.oxygen.ToString("0") + "s  背包 " + actor.backpack.Count + "/" + actor.BagCapacity + "  绳长 " + session.CrewRopeLength(actor).ToString("0.0") + "/" + session.ropeLength.ToString("0") + "m";
        }
        private void OpenStation(StationKind kind, CrewActor actor)
        {
            CrewStationPanel own = actor.role == CrewRole.Captain ? captainPanel : diverPanel;
            CrewStationPanel other = actor.role == CrewRole.Captain ? diverPanel : captainPanel;
            if (session.phase != SessionPhase.Playing) return;
            if (other != null && other.IsOpen && other.station == kind) { if (messageText != null) messageText.text = "同伴正在操作这个站点。"; return; }
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
        public void TogglePause() { if (session != null) SetPause(session.phase == SessionPhase.Playing); }
        public void SetPause(bool paused)
        {
            if (session == null) return;
            if (session.phase == SessionPhase.Won || session.phase == SessionPhase.Lost) return;
            if (paused) CloseStationPanels();
            else CloseTransientPanels();
            session.Pause(paused);
            if (pausePanel != null) { pausePanel.SetActive(paused); if (paused) { pausePanel.transform.SetAsLastSibling(); ConfigureModalLayer(pausePanel,100); } }
        }
        public void RequestManualSave() { ManualSaveRequested?.Invoke(); }
        public void SetManualSaveAvailable(bool available) { if (saveButton != null) saveButton.interactable = available; }
        public void SetSaveStatus(string status) { if (saveStatusText != null) saveStatusText.text = status; }
        public void Restart() { if(session!=null&&session.phase==SessionPhase.Lost)GameSaveManager.RequestContinue();else GameSaveManager.RequestNewGame(); Time.timeScale = 1; SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
        public void ReturnMenu() { Time.timeScale = 1; SceneManager.LoadScene(menuSceneName); }
    }
}
