using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lifeline
{
    public enum CardTarget
    {
        Center,
        Captain,
        Diver
    }

    /// <summary>
    /// Runtime-built UI for the opening tutorial: supports split-screen player-specific hint cards,
    /// a shared center card for major milestones, and an in-game "Facility Manual".
    /// Captain's cards (left half-screen) are controlled by E, Diver's cards (right half-screen)
    /// by Enter, and center cards can be confirmed by either E or Enter.
    /// </summary>
    public class TutorialUIController : MonoBehaviour
    {
        GameSession session;
        Canvas canvas;
        bool inited;

        CardSlot centerSlot;
        CardSlot captainSlot;
        CardSlot diverSlot;

        GameObject manualRoot;

        int lastCloseFrameCaptain = -1;
        int lastCloseFrameDiver = -1;

        public struct CardData
        {
            public string title, body, note;
            public CardData(string t, string b, string n) { title = t; body = b; note = n; }
        }

        public class CardSlot
        {
            public CardTarget target;
            public GameObject root;
            public Text titleText, bodyText, stepText, buttonText;
            public Button closeButton;
            public GameObject stepBar;
            public readonly Queue<CardData> queue = new Queue<CardData>();
            public bool isVisible;
            public float shownTime;

            public void Enqueue(CardData data)
            {
                queue.Enqueue(data);
                if (!isVisible) ShowNext();
            }

            public void ShowNext()
            {
                if (queue.Count == 0)
                {
                    isVisible = false;
                    if (root != null) root.SetActive(false);
                    return;
                }
                CardData c = queue.Dequeue();
                if (titleText != null) titleText.text = UiLocalization.Text(c.title);
                if (bodyText != null) bodyText.text = UiLocalization.Text(c.body);
                bool hasNote = !string.IsNullOrEmpty(c.note);
                if (stepText != null)
                {
                    stepText.gameObject.SetActive(hasNote);
                    if (hasNote) stepText.text = UiLocalization.Text(c.note);
                }
                if (stepBar != null) stepBar.SetActive(hasNote);
                if (root != null)
                {
                    root.SetActive(true);
                    root.transform.SetAsLastSibling();
                }
                isVisible = true;
                shownTime = Time.unscaledTime;
            }

            public void CloseCard()
            {
                if (queue.Count > 0) ShowNext();
                else
                {
                    isVisible = false;
                    if (root != null) root.SetActive(false);
                }
            }
        }

        // Mainline tutorial step cards (step index -> title/body). Step -1 is the opening card.
        static readonly string[] StepTitles = { "开局", "潜水采集", "绞盘接回", "制作台", "修复电路" };
        static readonly string[] StepBodies =
        {
            "船丢了，只剩一堆废料。先按住互动键清理潜水准备台，下水找物资。",
            "在水下按住采集键取物。氧气会持续消耗（水面 50 秒），注意回头。",
            "船长操作绞盘把潜水员接回。回到船上的物资会自动存入共享仓库。",
            "制作台能把 2 份原料合成 1 份板材/纤维。这是修复设备的材料来源。",
            "电路恢复！探照灯、求救电台、炮塔现在能用了。接下来按主线目标制作三级电台。"
        };

        // Shown right after TotalControl is cleaned: teaches the H-key facility manual.
        const string ManualCardTitle = "设施手册";
        const string ManualCardBody = "游戏中随时按 H 键可打开设施手册，查看全部 13 个工位的功能说明。忘记某个设备的作用时，打开它查一查。";

        // One-line function description per station, indexed by StationKind (13).
        static readonly string[] StationHints =
        {
            "制作台：把 2 份原料合成 1 份成品或工具，是修复设备的材料来源。",
            "设备总控：升级全船设施，也是完成电台后登记启程救援的地方；航行在独立船舵。",
            "航行船舵：站在这里掌舵才能横向操船、抵御风暴漂移，一次只能一人掌舵。",
            "救援绞盘：用节奏校准把潜水员和货箱接回船，物资会自动存入共享仓库。",
            "潜水准备台：两位角色都能在这里入水；水下氧气有限，别忘了及时回来。",
            "探照灯：夜间照明，需电路修复且电力充足才有效，能恢复潜水员夜间的索敌范围。",
            "垂钓点：先做钓鱼竿才能抛竿；不同水深出品质鱼的概率不同，钓起会自动食用。",
            "船体维修台：船体受损时在这里用木板做快速修补或大修，离开面板后作业仍会继续。",
            "海水过滤器：消耗铁板做导流拼图维修，修好后全船移动速度永久提升。",
            "电路系统：消耗纤维束连接四路端子，修好后探照灯、电动绞盘和求救电台才有效。",
            "动力发电机：手摇发电维持电力，可升级燃油机或装电鳗获得无限电力。",
            "船用引擎：修好后航速大幅提升，首次需铁板清理油污并匹配故障零件。",
            "甲板炮塔：建造后自动攻击深渊之影、海鸥和浅水鲨鱼，需先修好电路并消耗电力。"
        };

        public void Initialize(GameSession s)
        {
            if (inited) return;
            session = s;
            if (session == null) return;
            BuildUI();
            session.StationRestoredByRole += OnStationRestoredByRole;
            session.MilestoneReached += OnMilestone;
            session.ConsumeTutorialInteract = TryConsumeInteract;
            inited = true;
            // Opening card (step 0) -> Center slot
            EnqueueCard(CardTarget.Center, StepTitles[0], StepBodies[0], "教学 1 / 5");
        }

        void OnDestroy()
        {
            if (session == null) return;
            session.StationRestoredByRole -= OnStationRestoredByRole;
            session.MilestoneReached -= OnMilestone;
            if (session.ConsumeTutorialInteract == TryConsumeInteract)
                session.ConsumeTutorialInteract = null;
        }

        void OnStationRestoredByRole(StationKind kind, CrewRole role)
        {
            // Cleaning a ruin reveals that station's function description on the cleaner's half-screen.
            if (!session.MarkStationHintSeen(kind)) return;
            int i = (int)kind;
            string hint = i >= 0 && i < StationHints.Length ? StationHints[i] : GameNames.Station(kind);
            CardTarget target = (role == CrewRole.Diver) ? CardTarget.Diver : CardTarget.Captain;
            EnqueueCard(target, string.Format("{0} · 功能", GameNames.Station(kind)), hint, null);
            // TotalControl cleaning also teaches the facility manual
            if (kind == StationKind.TotalControl)
                EnqueueCard(target, ManualCardTitle, ManualCardBody, null);
        }

        void OnMilestone(TutorialMilestone milestone)
        {
            int idx;
            switch (milestone)
            {
                case TutorialMilestone.FirstDive: idx = 1; break;
                case TutorialMilestone.FirstCargoBanked: idx = 2; break;
                case TutorialMilestone.FirstCraft: idx = 3; break;
                case TutorialMilestone.CircuitRepaired: idx = 4; break;
                default: return;
            }
            // Shared milestones pop up in the center screen
            EnqueueCard(CardTarget.Center, StepTitles[idx], StepBodies[idx],
                string.Format("教学 {0} / {1}", idx + 1, StepTitles.Length));
        }

        /// <summary>Queue a card to the center slot (backwards-compatible overload).</summary>
        public void EnqueueCard(string title, string body, string note)
        {
            EnqueueCard(CardTarget.Center, title, body, note);
        }

        /// <summary>Queue a card to a specific slot (Center / Captain / Diver).</summary>
        public void EnqueueCard(CardTarget target, string title, string body, string note)
        {
            CardSlot slot = GetSlot(target);
            if (slot == null) return;
            slot.Enqueue(new CardData(title, body, note));
        }

        public CardSlot GetSlot(CardTarget target)
        {
            switch (target)
            {
                case CardTarget.Captain: return captainSlot;
                case CardTarget.Diver: return diverSlot;
                default: return centerSlot;
            }
        }

        /// <summary>Close current card in center slot (backwards-compatible overload).</summary>
        public void CloseCard()
        {
            if (centerSlot != null && centerSlot.isVisible) centerSlot.CloseCard();
            else if (captainSlot != null && captainSlot.isVisible) captainSlot.CloseCard();
            else if (diverSlot != null && diverSlot.isVisible) diverSlot.CloseCard();
        }

        public void CloseCard(CardTarget target)
        {
            GetSlot(target)?.CloseCard();
        }

        /// <summary>
        /// Consumes an interact button press from a player:
        /// - Captain (E key): closes Captain's card first; if none, closes Center card.
        /// - Diver (Enter key): closes Diver's card first; if none, closes Center card.
        /// Returns true if a card was active (swallowing the interact command from gameplay).
        /// </summary>
        public bool TryConsumeInteract(CrewRole role)
        {
            float now = Time.unscaledTime;
            const float Debounce = 0.15f;

            if (role == CrewRole.Captain)
            {
                if (Time.frameCount == lastCloseFrameCaptain) return true;

                // 1. Captain slot first
                if (captainSlot != null && captainSlot.isVisible)
                {
                    if (now - captainSlot.shownTime >= Debounce)
                    {
                        lastCloseFrameCaptain = Time.frameCount;
                        captainSlot.CloseCard();
                    }
                    return true;
                }

                // 2. Center slot fallback
                if (centerSlot != null && centerSlot.isVisible)
                {
                    if (now - centerSlot.shownTime >= Debounce)
                    {
                        lastCloseFrameCaptain = Time.frameCount;
                        centerSlot.CloseCard();
                    }
                    return true;
                }
            }
            else if (role == CrewRole.Diver)
            {
                if (Time.frameCount == lastCloseFrameDiver) return true;

                // 1. Diver slot first
                if (diverSlot != null && diverSlot.isVisible)
                {
                    if (now - diverSlot.shownTime >= Debounce)
                    {
                        lastCloseFrameDiver = Time.frameCount;
                        diverSlot.CloseCard();
                    }
                    return true;
                }

                // 2. Center slot fallback
                if (centerSlot != null && centerSlot.isVisible)
                {
                    if (now - centerSlot.shownTime >= Debounce)
                    {
                        lastCloseFrameDiver = Time.frameCount;
                        centerSlot.CloseCard();
                    }
                    return true;
                }
            }

            return false;
        }

        void Update()
        {
            // Direct keyboard fallback in case HandleCommand wasn't routed (e.g. during certain input states)
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.eKey.wasPressedThisFrame)
                {
                    TryConsumeInteract(CrewRole.Captain);
                }
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                {
                    TryConsumeInteract(CrewRole.Diver);
                }
            }
        }

        static Font DefaultFont() => RuntimeUIFont.GetOrCreateFont();
        static Color PanelColor => new Color(0.06f, 0.12f, 0.18f, 0.94f);
        static Color AccentColor => new Color(0.95f, 0.78f, 0.25f, 1f);

        const string SpriteFolder = "Art/";
        static Sprite LoadSprite(string name)
        {
            var s = Resources.Load<Sprite>(SpriteFolder + name);
            if (s == null) Debug.LogWarning("[TutorialUI] Missing sprite: " + SpriteFolder + name);
            return s;
        }
        static void ApplySkin(Image image, string spriteName)
        {
            if (image == null) return;
            var sprite = LoadSprite(spriteName);
            if (sprite == null) return;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }

        GameObject Panel(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            go.GetComponent<Image>().color = PanelColor;
            return go;
        }

        Text MakeText(GameObject parent, string name, int size, Color color, TextAnchor align, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            var t = go.GetComponent<Text>();
            t.font = DefaultFont(); t.fontSize = size; t.color = color; t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        Button MakeButton(GameObject parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, out Text labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            go.GetComponent<Image>().color = AccentColor;
            labelText = MakeText(go, "Label", 18, new Color(0.05f, 0.08f, 0.12f, 1f), TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            labelText.text = UiLocalization.Text(label);
            return go.GetComponent<Button>();
        }

        CardSlot BuildCardSlot(CardTarget target, string rootName, Vector2 anchor, Vector2 size, Vector2 pos, string buttonLabel)
        {
            var slot = new CardSlot();
            slot.target = target;
            slot.root = Panel(rootName, anchor, anchor, size, pos);
            slot.stepText = MakeText(slot.root, "Step", 17, AccentColor, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -40), new Vector2(-100, -8));
            slot.titleText = MakeText(slot.root, "Title", 28, AccentColor, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -84), new Vector2(-20, -40));
            slot.bodyText = MakeText(slot.root, "Body", 20, Color.white, TextAnchor.UpperLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, 60), new Vector2(-20, -92));

            float btnWidth = target == CardTarget.Center ? 210f : 170f;
            Text btnText;
            slot.closeButton = MakeButton(slot.root, "Close", buttonLabel, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-btnWidth - 16, 16), new Vector2(-16, 60), out btnText);
            slot.buttonText = btnText;
            slot.closeButton.onClick.AddListener(() => CloseCard(target));

            ApplySkin(slot.root.GetComponent<Image>(), "TutorialPanelLarge");
            ApplySkin(slot.closeButton.GetComponent<Image>(), "TutorialButtonWide");

            var stepBgGo = new GameObject("StepBar", typeof(RectTransform), typeof(Image));
            stepBgGo.transform.SetParent(slot.root.transform, false);
            var srt = (RectTransform)stepBgGo.transform;
            srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1);
            srt.offsetMin = new Vector2(16, -42); srt.offsetMax = new Vector2(-100, -6);
            ApplySkin(stepBgGo.GetComponent<Image>(), "TutorialBarTeal");
            stepBgGo.transform.SetAsFirstSibling();
            slot.stepBar = stepBgGo;

            slot.root.SetActive(false);
            return slot;
        }

        void BuildUI()
        {
            var canvasGo = new GameObject("TutorialUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150; // above panels (100-130) so cards are always visible
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // 1. Center slot — bottom center (X: 0.5), confirmed by E or Enter
            string centerLabel = UiLocalization.Text("知道了") + " [E / Enter]";
            centerSlot = BuildCardSlot(CardTarget.Center, "HintCard_Center", new Vector2(0.5f, 0f), new Vector2(720, 260), new Vector2(0, 200), centerLabel);

            // 2. Captain slot — left half-screen (X: 0.25), confirmed by E
            string captainLabel = UiLocalization.Text("知道了") + " [E]";
            captainSlot = BuildCardSlot(CardTarget.Captain, "HintCard_Captain", new Vector2(0.25f, 0f), new Vector2(660, 260), new Vector2(0, 200), captainLabel);

            // 3. Diver slot — right half-screen (X: 0.75), confirmed by Enter
            string diverLabel = UiLocalization.Text("知道了") + " [Enter]";
            diverSlot = BuildCardSlot(CardTarget.Diver, "HintCard_Diver", new Vector2(0.75f, 0f), new Vector2(660, 260), new Vector2(0, 200), diverLabel);

            // Facility manual — full-screen modal in center, opened via H key or ESC menu
            manualRoot = Panel("FacilityManual", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(1000, 760), Vector2.zero);
            var title = MakeText(manualRoot, "Title", 34, AccentColor, TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -60), new Vector2(0, -12));
            title.text = UiLocalization.Text("设施手册");
            var sb = new StringBuilder();
            for (int i = 0; i < StationHints.Length; i++) sb.Append(StationHints[i]).Append("\n\n");
            var list = MakeText(manualRoot, "List", 20, Color.white, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(28, 80), new Vector2(-28, -70));
            list.text = UiLocalization.Text(sb.ToString());
            Text manualCloseLabel;
            var manualClose = MakeButton(manualRoot, "Close", "关闭", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(-80, 16), new Vector2(80, 60), out manualCloseLabel);
            manualClose.onClick.AddListener(ToggleManual);
            ApplySkin(manualRoot.GetComponent<Image>(), "TutorialPanelLarge");
            ApplySkin(manualClose.GetComponent<Image>(), "TutorialButtonWide");
            manualRoot.SetActive(false);
        }

        public void ToggleManual()
        {
            if (manualRoot == null) return;
            bool show = !manualRoot.activeSelf;
            manualRoot.SetActive(show);
            if (show) manualRoot.transform.SetAsLastSibling();
        }

        public bool ManualOpen => manualRoot != null && manualRoot.activeSelf;
    }
}
