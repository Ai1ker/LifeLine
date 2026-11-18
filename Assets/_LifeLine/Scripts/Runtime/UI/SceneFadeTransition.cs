using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Lifeline
{
    /// <summary>Disposable transition overlay. Replace the Image/sprite or animation without changing menu/save flow.</summary>
    public sealed class SceneFadeTransition : MonoBehaviour
    {
        public Image image;
        [Min(0)] public float fadeSeconds = .2f;
        [Min(0)] public float fadeOutSeconds = .1f;
        public static bool IsTransitioning => active != null;
        public float Alpha => image != null ? image.color.a : 0;
        static SceneFadeTransition active;

        public static bool Begin(string sceneName, float seconds, float outSeconds = .1f)
        {
            if (IsTransitioning || string.IsNullOrEmpty(sceneName)) return false;
            var root = new GameObject("New game / replaceable scene fade", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10000;
            root.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var transition = root.AddComponent<SceneFadeTransition>(); active = transition;
            transition.fadeSeconds = Mathf.Max(0, seconds); transition.fadeOutSeconds = Mathf.Max(0, outSeconds);
            var visual = new GameObject("Fade / replaceable visual", typeof(RectTransform), typeof(Image)); visual.transform.SetParent(root.transform, false);
            var rect = (RectTransform)visual.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            transition.image = visual.GetComponent<Image>(); transition.image.color = new Color(0, 0, 0, 0); transition.image.raycastTarget = true;
            DontDestroyOnLoad(root); transition.StartCoroutine(transition.Load(sceneName)); return true;
        }
        public static float FadeStep(float alpha, float target, float deltaSeconds, float duration)
            => duration <= 0 ? target : Mathf.MoveTowards(alpha, target, Mathf.Max(0, deltaSeconds) / duration);
        IEnumerator FadeTo(float target, float seconds)
        {
            while (!Mathf.Approximately(Alpha, target))
            {
                var color = image.color; color.a = FadeStep(color.a, target, Time.unscaledDeltaTime, seconds); image.color = color;
                yield return null;
            }
        }
        IEnumerator Load(string sceneName)
        {
            // Prepare the scene while the menu fades, but never reveal it before an opaque frame.
            AsyncOperation loading = null;
            try
            {
                loading = SceneManager.LoadSceneAsync(sceneName);
                if (loading != null) loading.allowSceneActivation = false;
            }
            catch (System.Exception exception) { Debug.LogError("Cannot load gameplay scene: " + exception.Message); }
            yield return FadeTo(1, fadeOutSeconds);
            if (loading != null) { loading.allowSceneActivation = true; yield return loading; }
            // One render-safe frame lets the save boot/HUD Start callbacks finish; no timed black hold.
            yield return null;
            var session = FindAnyObjectByType<GameSession>();
            while (session != null && !session.IsInitialized && session.phase != SessionPhase.Paused) yield return null;
            yield return FadeTo(0, fadeSeconds);
            Destroy(gameObject);
        }
        void OnDestroy() { if (active == this) active = null; }
    }
}
