#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lifeline.Editor
{
    /// <summary>Additive installer: authored references/layout remain intact and can be replaced in the inspector.</summary>
    public static class OnboardingUiInstaller
    {
        [MenuItem("LifeLine/UI/Install Onboarding and Captain Oxygen")]
        public static void InstallInOpenScene()
        {
            foreach (var hud in Object.FindObjectsByType<GameHUDController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Install(hud); EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            }
        }
        [MenuItem("LifeLine/UI/Apply Short Notices and Quick Transition (Preserve Custom Values)")]
        public static void ApplyTimingInOpenScenes()
        {
            foreach (var menu in Object.FindObjectsByType<MainMenuController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { if (UpdateKnownTiming(menu, null)) { EditorUtility.SetDirty(menu); EditorSceneManager.MarkSceneDirty(menu.gameObject.scene); } }
            foreach (var tutorial in Object.FindObjectsByType<TutorialProgress>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { if (UpdateKnownTiming(null, tutorial)) { EditorUtility.SetDirty(tutorial); EditorSceneManager.MarkSceneDirty(tutorial.gameObject.scene); } }
        }
        public static bool UpdateKnownTiming(MainMenuController menu, TutorialProgress tutorial)
        {
            bool changed = false;
            if (menu != null && Mathf.Approximately(menu.newGameFadeSeconds, .65f)) { menu.newGameFadeSeconds = .2f; changed = true; }
            if (tutorial != null && Mathf.Approximately(tutorial.noticeSeconds, 16)) { tutorial.noticeSeconds = 5; changed = true; }
            return changed;
        }

        public static void Install(GameHUDController hud)
        {
            if (hud == null || hud.session == null) return;
            if (hud.onboarding == null) hud.onboarding = hud.GetComponent<OnboardingView>();
            if (hud.onboarding == null) hud.onboarding = hud.gameObject.AddComponent<OnboardingView>();
            if (hud.nauticalView == null) hud.onboarding.BuildMissingViews();
            hud.onboarding.Initialize(hud);
            EditorUtility.SetDirty(hud); EditorUtility.SetDirty(hud.onboarding);
            var tutorial = hud.session.GetComponent<TutorialProgress>(); if (tutorial != null) EditorUtility.SetDirty(tutorial);
        }
    }
}
#endif
