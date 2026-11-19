using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Lifeline
{
    public class MainMenuController : MonoBehaviour
    {
        public string gameplaySceneName = "Gameplay";
        [Min(0)] public float newGameFadeSeconds = .2f;
        [Min(0)] public float newGameFadeOutSeconds = .1f;
        public bool IsStartingGame { get; private set; }
        public Button localCoopButton, controlsButton, quitButton;
        public Button settingsButton;
        public Button continueButton;
        public Button onlineCoopButton;
        public OnlineRoomPanel onlineRoomPanel;
        public CanvasGroup menuCard;
        public Text saveStatusText;
        public event Action ContinueRequested;
        public GameSettingsPanel settings;
        public ControlsRebindPanel controls;
        public CrewInputReader captainInput, diverInput;
        public UnityEngine.InputSystem.InputActionAsset inputAsset;
        private void Start()
        {
            Time.timeScale = 1f;
            if (captainInput != null) captainInput.Initialize(CrewRole.Captain, inputAsset);
            if (diverInput != null) diverInput.Initialize(CrewRole.Diver, inputAsset);
            if (controls != null) { controls.captainInput = captainInput; controls.diverInput = diverInput; }
            if (localCoopButton != null) localCoopButton.onClick.AddListener(RequestStartGame);
            if (continueButton != null) continueButton.onClick.AddListener(RequestContinue);
            if (onlineCoopButton != null) onlineCoopButton.onClick.AddListener(() => OpenOnlineRoom());
            if (controlsButton != null) controlsButton.onClick.AddListener(() => controls.Open());
            if (settingsButton != null) settingsButton.onClick.AddListener(() => settings.Open());
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            string roomNotice = OnlineVoyage.ConsumeMenuNotice();
            if (!string.IsNullOrEmpty(roomNotice)) OpenOnlineRoom(roomNotice);
        }
        private void Update()
        {
            // Menu readers have no CrewActor to drain their UI commands.
            bool captainPause = captainInput != null && captainInput.ReadCommand().pause;
            bool diverPause = diverInput != null && diverInput.ReadCommand().pause;
            if (captainPause || diverPause) HandleEscape();
        }
        public bool HandleEscape()
        {
            if (controls != null && controls.IsOpen) return controls.HandleEscape();
            if (settings != null && settings.IsOpen) { settings.Close(); return true; }
            if (onlineRoomPanel != null && onlineRoomPanel.IsOpen) { onlineRoomPanel.GoBack(); return true; }
            return false;
        }
        public void OpenOnlineRoom(string noticeKey = null)
        {
            if (IsStartingGame || SceneFadeTransition.IsTransitioning || onlineRoomPanel == null) return;
            if (onlineRoomPanel.IsOpen && string.IsNullOrEmpty(noticeKey)) return;
            if (menuCard != null) menuCard.interactable = false;
            onlineRoomPanel.Open(this, noticeKey);
        }
        public void OnOnlineRoomClosed()
        {
            if (menuCard != null) menuCard.interactable = true;
            if (onlineCoopButton != null && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(onlineCoopButton.gameObject);
        }
        bool OnlineRoomOpen => onlineRoomPanel != null && onlineRoomPanel.IsOpen;
        public void RequestContinue() { if (!IsStartingGame && !OnlineRoomOpen) ContinueRequested?.Invoke(); }
        public void RequestStartGame()
        {
            if (IsStartingGame || SceneFadeTransition.IsTransitioning || OnlineRoomOpen) return;
            IsStartingGame = true; GameSaveManager.RequestNewGame();
            if (localCoopButton != null) localCoopButton.interactable = false;
            if (continueButton != null) continueButton.interactable = false;
            if (onlineCoopButton != null) onlineCoopButton.interactable = false;
            SceneFadeTransition.Begin(gameplaySceneName, newGameFadeSeconds, newGameFadeOutSeconds);
        }
        public void SetContinueAvailable(bool available)
        {
            if (continueButton == null) return;
            continueButton.gameObject.SetActive(available);
            continueButton.interactable = available && !IsStartingGame;
        }
        public void SetSaveStatus(string status) { if (saveStatusText != null) saveStatusText.text = status; }
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
