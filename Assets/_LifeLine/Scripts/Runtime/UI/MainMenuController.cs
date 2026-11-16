using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Lifeline
{
    public class MainMenuController : MonoBehaviour
    {
        public string gameplaySceneName = "Gameplay";
        public Button localCoopButton, controlsButton, quitButton;
        public Button settingsButton;
        public Button continueButton;
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
            if (localCoopButton != null) localCoopButton.onClick.AddListener(() => SceneManager.LoadScene(gameplaySceneName));
            if (continueButton != null) continueButton.onClick.AddListener(RequestContinue);
            if (controlsButton != null) controlsButton.onClick.AddListener(() => controls.Open());
            if (settingsButton != null) settingsButton.onClick.AddListener(() => settings.Open());
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
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
            return false;
        }
        public void RequestContinue() { ContinueRequested?.Invoke(); }
        public void SetContinueAvailable(bool available) { if (continueButton != null) continueButton.interactable = available; }
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
