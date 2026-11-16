using UnityEngine;
using UnityEngine.UI;

namespace Lifeline
{
    /// <summary>Replaceable settings view. Preferences are independent from a voyage save.</summary>
    public sealed class GameSettingsPanel : MonoBehaviour
    {
        public GameObject panel;
        public Button languageButton, cameraButton, bindingsButton, closeButton;
        public Text languageLabel, cameraLabel;
        public ControlsRebindPanel controls;
        public GameCameraRig cameraRig;
        public bool IsOpen => panel != null && panel.activeSelf;

        void Awake()
        {
            languageButton?.onClick.AddListener(CycleLanguage);
            cameraButton?.onClick.AddListener(ToggleShipSplit);
            bindingsButton?.onClick.AddListener(() => { if (controls != null) controls.Open(); });
            closeButton?.onClick.AddListener(Close);
        }
        void OnEnable() { LocalizationManager.LanguageChanged += RefreshLabels; RefreshLabels(); }
        void OnDisable() { LocalizationManager.LanguageChanged -= RefreshLabels; }
        public void Open()
        {
            if (panel == null) return;
            panel.SetActive(true); panel.transform.SetAsLastSibling();
            if (cameraRig == null) cameraRig = FindAnyObjectByType<GameCameraRig>();
            RefreshLabels();
        }
        public void Close() { if (panel != null) panel.SetActive(false); }
        public void CycleLanguage()
        {
            LocalizationManager.SetLanguage((GameLanguage)(((int)LocalizationManager.LanguagePreference + 1) % 3));
            RefreshLabels();
        }
        public void ToggleShipSplit()
        {
            bool next = !GameCameraRig.LoadShipSplitPreference();
            if (cameraRig != null) cameraRig.SetShipSplitPreference(next);
            else GameCameraRig.SaveShipSplitPreference(next);
            RefreshLabels();
        }
        public void RefreshLabels()
        {
            if (languageLabel != null)
                languageLabel.text = LocalizationManager.LanguagePreference == GameLanguage.System ? "语言：跟随系统" : LocalizationManager.LanguagePreference == GameLanguage.Chinese ? "语言：简体中文" : "语言：英语";
            if (cameraLabel != null) cameraLabel.text = GameCameraRig.LoadShipSplitPreference() ? "船上镜头：双人分屏" : "船上镜头：双人同屏";
        }
    }
}
