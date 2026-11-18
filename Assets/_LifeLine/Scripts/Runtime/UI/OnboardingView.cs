using UnityEngine;
using UnityEngine.UI;

namespace Lifeline
{
    /// <summary>Replaceable presentation for onboarding and the captain's contextual oxygen gauge.</summary>
    [DisallowMultipleComponent]
    public sealed class OnboardingView : MonoBehaviour
    {
        public GameHUDController hud;
        public TutorialProgress progress;
        public GameObject instructionPanel, noticePanel, captainOxygenRoot;
        public Text instructionText, noticeText, captainOxygenText;
        public Image captainOxygenFill;
        [Header("Placeholder layout / replace with authored UI")]
        public bool buildMissingViews = true;
        public bool hideLessonsWhileUsingDevices = true;
        public Color panelColor = new Color(.04f, .14f, .20f, .68f);
        public Color textColor = new Color(.91f, .98f, 1f);
        public Sprite panelSprite;
        [Min(12)] public int fontSize = 18;
        public Vector2 instructionMin = new Vector2(.02f, .56f), instructionMax = new Vector2(.49f, .744f);
        public Vector2 noticeMin = new Vector2(.02f, .40f), noticeMax = new Vector2(.49f, .55f);
        public Vector2 captainOxygenMin = new Vector2(.02f, .753f), captainOxygenMax = new Vector2(.25f, .797f);

        public void Initialize(GameHUDController owner)
        {
            hud = owner;
            if (hud == null || hud.session == null) return;
            progress = hud.session.GetComponent<TutorialProgress>();
            if (progress == null) progress = hud.session.gameObject.AddComponent<TutorialProgress>();
            progress.Initialize(hud.session);
            if (buildMissingViews) BuildMissingViews();
            RefreshView();
        }
        void Update() { RefreshView(); }
        public void BuildMissingViews()
        {
            if (instructionPanel == null) instructionPanel = Panel("Onboarding / replaceable lesson", instructionMin, instructionMax);
            if (instructionText == null) instructionText = Label(instructionPanel.transform, "Lesson", new Vector2(.035f, .06f), new Vector2(.965f, .94f));
            if (noticePanel == null) noticePanel = Panel("Onboarding / independent event reminders", noticeMin, noticeMax);
            if (noticeText == null) noticeText = Label(noticePanel.transform, "First encounter notice", new Vector2(.035f, .06f), new Vector2(.965f, .94f));
            if (captainOxygenRoot == null) captainOxygenRoot = Panel("Captain oxygen / underwater only", captainOxygenMin, captainOxygenMax);
            if (captainOxygenText == null) captainOxygenText = Label(captainOxygenRoot.transform, "Captain oxygen value", new Vector2(.02f, .25f), new Vector2(.98f, 1));
            if (captainOxygenFill == null)
            {
                var background = Rect("Oxygen track", captainOxygenRoot.transform, new Vector2(.02f, .04f), new Vector2(.98f, .18f));
                background.gameObject.AddComponent<Image>().color = new Color(.12f, .24f, .30f);
                var fill = Rect("Replaceable fill", background, Vector2.zero, Vector2.one);
                captainOxygenFill = fill.gameObject.AddComponent<Image>(); captainOxygenFill.color = new Color(.4f, .8f, 1f);
                captainOxygenFill.raycastTarget = false;
            }
        }
        public void RefreshView()
        {
            if (hud == null || hud.session == null) return;
            var session = hud.session;
            bool playing = session.phase == SessionPhase.Playing;
            bool usingDevice = hideLessonsWhileUsingDevices && (hud.captainPanel != null && hud.captainPanel.IsOpen || hud.diverPanel != null && hud.diverPanel.IsOpen);
            if(progress!=null)progress.remindersVisible=playing && !usingDevice;
            bool lesson = playing && !usingDevice && progress != null && !progress.Complete;
            if (instructionPanel != null) instructionPanel.SetActive(lesson);
            if (lesson && instructionText != null)
            {
                var actor = progress.Step <= 1 || progress.Step == 7 ? session.captain : session.diver;
                string interact = actor != null && actor.input != null ? actor.input.GetBindingLabel("Interact") : actor == session.captain ? "E" : "Enter";
                string collect = actor != null && actor.input != null ? actor.input.GetBindingLabel("Collect") : "1";
                instructionText.text = string.Format(UiLocalization.Text(progress.InstructionSource), interact, collect);
            }
            bool notice = playing && !usingDevice && progress != null && !string.IsNullOrEmpty(progress.NoticeSource);
            if (noticePanel != null) noticePanel.SetActive(notice);
            if (notice && noticeText != null) noticeText.text = UiLocalization.Text(progress.NoticeSource);
            var captain = session.captain;
            bool showOxygen = playing && captain != null && !captain.onboard;
            if (captainOxygenRoot != null) captainOxygenRoot.SetActive(showOxygen);
            if (showOxygen)
            {
                if (captainOxygenText != null) captainOxygenText.text = UiLocalization.Text("船长氧气") + " " + captain.oxygen.ToString("0") + " / " + captain.oxygenMax.ToString("0") + "s";
                if (captainOxygenFill != null)
                {
                    float fill = captain.oxygenMax > 0 ? Mathf.Clamp01(captain.oxygen / captain.oxygenMax) : 0;
                    var rect = captainOxygenFill.rectTransform; rect.anchorMax = new Vector2(fill, 1);
                }
            }
        }
        GameObject Panel(string name, Vector2 min, Vector2 max)
        {
            var rect = Rect(name, transform, min, max);
            var image = rect.gameObject.AddComponent<Image>(); image.color = panelColor; image.sprite = panelSprite; image.raycastTarget = false;
            return rect.gameObject;
        }
        Text Label(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var text = Rect(name, parent, min, max).gameObject.AddComponent<Text>();
            text.font = RuntimeUIFont.GetOrCreateFont(); text.fontSize = fontSize; text.color = textColor;
            text.alignment = TextAnchor.UpperLeft; text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow; text.raycastTarget = false; return text;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
        }
    }
}
