using UnityEngine;
using UnityEngine.UI;

namespace Lifeline
{
    [DisallowMultipleComponent]
    public sealed class OnlineLoadingView : MonoBehaviour
    {
        public OnlineVoyage voyage;
        public GameObject canvasRoot;
        public RectTransform fill;
        public Text stageLabel;

        void Update()
        {
            float progress = 0;
            string stage = "";
            bool loading = voyage != null && voyage.TryGetLoadingProgress(out progress, out stage);
            if (canvasRoot.activeSelf != loading) canvasRoot.SetActive(loading);
            if (!loading) return;
            fill.anchorMax = new Vector2(progress, fill.anchorMax.y);
            stageLabel.text = UiLocalization.Text(stage) + "   " + Mathf.RoundToInt(progress * 100f) + "%";
        }
    }
}
