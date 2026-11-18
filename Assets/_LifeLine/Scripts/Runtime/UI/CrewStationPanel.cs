using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lifeline
{
    /// <summary>Replaceable UGUI view; all world mutations are delegated to the session.</summary>
    public class CrewStationPanel : MonoBehaviour
    {
        public GameObject panel;
        public Text titleText, detailText, liveText;
        public RectTransform actionsRoot;
        public Button actionButtonPrefab, closeButton;
        public Image rhythmFill;
        public Image rhythmWindow, rhythmCursor;
        public QteStationView qteView;
        public RepairBoardView repairBoardPrefab;
        public RepairVisualTheme repairTheme;
        RepairBoardView repairBoard;
        private float generatorFeedback;
        private bool generatorGood;
        [Header("可替换小游戏视图 / public tuning")]
        public Vector2 generatorWindow = new Vector2(.7f, .9f);
        public Vector2 fishWaitSeconds = new Vector2(2.5f, 5f);
        public Vector2 fishHalfWidths = new Vector2(.135f, .095f);
        public Vector2 fishMoveSpeeds = new Vector2(1.35f, 1.935f);
        public Vector2 fishGainRates = new Vector2(.21f, .15f);
        public Vector2 fishLossRates = new Vector2(.11f, .14f);
        public float fishRiseAcceleration = 1.6f, fishFallAcceleration = 1.1f, fishMaxVelocity = .7f;
        public float fishTimeoutSeconds = 45f;
        public StationKind station;
        public bool IsOpen => panel != null && panel.activeSelf;
        public float fishingZone = .5f, fishingFish = .5f, fishingProgress, fishingVelocity;
        public float fishingTime;
        public bool fishingActive, fishingQuality;
        public float fishZoneHalfWidth = .135f;
        public float selectedFault;
        public float scrubSecondsPerFault = 2f;
        public int selectedJobId;
        private GameHUDController hud;
        private GameSession session;
        private CrewActor actor;
        private Vector2 previousMove;
        private readonly List<GameObject> buttons = new List<GameObject>();
        private int engineVersion = -1;
        private int lastJobCount = -1;
        private float fishWait;
        private HoldActionButton heldButton;
        private bool repairStarted;

        public void Initialize(GameHUDController owner, GameSession game, CrewActor crew)
        {
            hud = owner; session = game; actor = crew;
            if (closeButton != null) { closeButton.onClick.RemoveListener(Close); closeButton.onClick.AddListener(Close); }
            panel.SetActive(false);
        }
        public void Open(StationKind kind)
        {
            if (session == null || session.phase != SessionPhase.Playing || actor == null || actor.downed || !actor.onboard || !session.IsStationUnlocked(kind)) return;
            if (IsOpen && station == kind) return;
            Close(); station = kind; actor.controlsCaptured = true; previousMove = Vector2.zero;
            panel.SetActive(true); titleText.text = GameNames.Role(actor.role) + " / " + GameNames.Station(kind);
            repairStarted = session.repairAuthorized[(int)kind];
            if (kind == StationKind.Winch) session.SetWinchActive(true,actor);
            if (kind == StationKind.Engine) engineVersion = session.engineRepairVersion;
            BuildActions();
        }
        public void Close()
        {
            if (actor != null) { actor.controlsCaptured = false; session?.craftingManager?.ClearInteractionContext(actor); }
            if (session != null && IsOpen && station == StationKind.Winch) session.SetWinchActive(false);
            if (panel != null) panel.SetActive(false);
            fishingActive = false; heldButton = null; previousMove = Vector2.zero;
            selectedJobId=0;lastJobCount=-1;generatorFeedback=0;repairStarted=false;
            ClearActions();repairBoard=null;
            if(qteView!=null)qteView.gameObject.SetActive(false);
            if(liveText!=null)liveText.text="";if(detailText!=null)detailText.text="";
        }
        private void Update()
        {
            if (!IsOpen || session == null || actor == null) return;
            if (actor.downed || !actor.onboard || session.phase == SessionPhase.Won || session.phase == SessionPhase.Lost)
            { Close(); return; }
            if (station == StationKind.Engine && engineVersion != session.engineRepairVersion)
            { engineVersion = session.engineRepairVersion; repairStarted = false; BuildActions(); }
            if (session.phase != SessionPhase.Playing) return;
            generatorFeedback = Mathf.Max(0, generatorFeedback - Time.deltaTime);
            UpdateLive();
            if (station == StationKind.Fishing) UpdateFishing(Time.deltaTime);
            if (station == StationKind.Engine && repairStarted && IsHeld("Interact")) ScrubSelected(Time.deltaTime);
        }
        private bool IsHeld(string action)
        {
            if (heldButton != null && heldButton.held) return true;
            InputActionAsset asset = actor.input != null ? actor.input.RuntimeActions : null;
            return asset != null && (asset.FindActionMap(actor.role.ToString(), false)?.FindAction(action, false)?.IsPressed() ?? false);
        }
        public void HandleCommand(CrewCommand command)
        {
            if (!IsOpen || session.phase != SessionPhase.Playing || actor == null || session.IsIncapacitated(actor)) return;
            if (station == StationKind.Workbench || station == StationKind.HullRepair)
            {
                if (command.move.sqrMagnitude > .2f && (previousMove.sqrMagnitude < .2f || Vector2.Dot(previousMove, command.move) < .5f))
                {
                    int direction = Mathf.Abs(command.move.y) >= Mathf.Abs(command.move.x) ? command.move.y > 0 ? 0 : 2 : command.move.x < 0 ? 1 : 3;
                    session.GetOrCreateCraftingManager().CraftQteAtStation(selectedJobId, direction, station);
                }
            }
            else if (station == StationKind.Engine && repairStarted && command.move.sqrMagnitude>.2f && previousMove.sqrMagnitude<.2f)
            { int part=Mathf.Abs(command.move.y)>=Mathf.Abs(command.move.x)?command.move.y>0?0:2:command.move.x<0?1:3; session.TryRepairEngineFault((int)selectedFault,part); BuildActions(); }
            else if (station == StationKind.Winch && command.melee) session.TryWinchQte(WinchAngle());
            else if (station == StationKind.Generator && command.melee) Crank();
            else if (station == StationKind.Fishing && command.interact) StartFishing();
            previousMove = command.move;
        }
        private float WinchAngle() => Mathf.Repeat(session.winchCycleTime / Mathf.Max(.1f, session.tuning.winchCycle), 1f);
        private void ClearActions()
        {
            foreach (GameObject button in buttons) if (button != null) { button.SetActive(false); Destroy(button); }
            buttons.Clear(); heldButton = null;
        }
        private Button Add(string label, Action action, bool enabled = true)
        {
            Button button = Instantiate(actionButtonPrefab, actionsRoot); button.gameObject.SetActive(true);
            button.GetComponentInChildren<Text>().text = label; button.interactable = enabled;
            button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => { action(); });
            buttons.Add(button.gameObject); return button;
        }
        private void ActionAndRefresh(Action action) { action(); BuildActions(); }
        private void BuildActions()
        {
            ClearActions(); lastJobCount = session.jobs.Count;
            ConfigureQtePresentation();
            if(station==StationKind.Filter||station==StationKind.Circuit||station==StationKind.Engine){if(rhythmFill!=null)rhythmFill.transform.parent.gameObject.SetActive(false);}
            switch (station)
            {
                case StationKind.Workbench:
                case StationKind.HullRepair:
                    detailText.text = station == StationKind.Workbench
                        ? string.Format(UiLocalization.Text("制作作业 {0}秒。校准成功加进度，离开仍继续，不返还已投入材料。"), session.GetCraftDuration().ToString("0"))
                        : "普通作业 " + session.GetCraftDuration().ToString("0") + "秒；快速修补用时减半。校准成功加进度，离开仍继续，不返还已投入材料。";
                    foreach (CraftJob job in session.jobs)
                    {
                        if (!JobBelongsHere(job)) continue;
                        int id = job.id; Add("选择校准：" + job.recipe.displayName, () => { selectedJobId = id; session.craftingManager?.SetInteractionContext(actor, id, station); });
                        if (selectedJobId == 0) selectedJobId = id;
                    }
                    foreach (RecipeDefinition recipe in session.recipes)
                    {
                        if (recipe == null || (station == StationKind.HullRepair) != (recipe.repairAmount > 0)) continue;
                        RecipeDefinition chosen = recipe;
                        Add(recipe.displayName + "  " + RecipeCosts(recipe) + " / " + session.GetCraftDuration(recipe).ToString("0.#") + "s", () => ActionAndRefresh(() => session.TryCraft(chosen.id, actor)), session.jobs.Count < session.CraftCapacity);
                    }
                    session.craftingManager?.SetInteractionContext(actor, selectedJobId, station);
                    break;
                case StationKind.TotalControl:
                    detailText.text = "设施升级和救援航程。航行请前往旁边独立船舵。";
                    foreach (UpgradeKind upgrade in Enum.GetValues(typeof(UpgradeKind)))
                    {
                        UpgradeKind selected = upgrade;
                        int level = session.GetUpgrade(upgrade);
                        bool canUpgrade = level < session.MaxUpgradeLevel(upgrade);
                        int displayTier = UpgradeSummary.DisplayTier(upgrade, canUpgrade ? level + 1 : level);
                        string label = UiLocalization.Text(UpgradeLabel(upgrade)) + (canUpgrade ? " → T" : " T") + displayTier + " / " + UpgradeSummary.NextEffect(session, upgrade);
                        if (canUpgrade) label += "\n" + Costs(session.GetUpgradeCost(upgrade));
                        Add(label, () => ActionAndRefresh(() => session.TryUpgrade(selected)), canUpgrade);
                    }
                    Add(session.turretInstalled ? "炮塔 " + (session.turretOn ? "关闭" : "开启") : "建造炮塔：3木板 3铁板 1纤维束", () => ActionAndRefresh(() => { if (session.turretInstalled) session.ToggleTurret(); else session.BuildTurret(); }));
                    if(session.turretInstalled && session.turretTier<2)Add("升级炮塔T2：1铁板 3纤维束 1木板",()=>ActionAndRefresh(()=>session.UpgradeTurret()));
                    Add("完成电台后：向东启程救援", () => ActionAndRefresh(() => session.StartRescueVoyage()), session.radioStage >= 3 && session.diver.onboard);
                    break;
                case StationKind.Winch:
                    detailText.text = "绿色判定区仅在每次判定后换位，指针转圈不会换位。成功收绳，失败回滑。";
                    Add("节奏校准 [" + actor.input.GetBindingLabel("Melee") + "]", () => session.TryWinchQte(WinchAngle()), true);
                    break;
                case StationKind.Generator:
                    detailText.text = "手摇每3秒成功 +1%；燃油机每桶 +15%，再用3铁板升级至 +20%。保留手摇。";
                    if (!session.infinitePower)
                    {
                        Add("节奏发力 [" + actor.input.GetBindingLabel("Melee") + "]", Crank);
                        if (session.generatorTier == 0) Add("【升级】燃油发电机：1 铁板", () => ActionAndRefresh(() => session.UpgradeGenerator()));
                        else
                        {
                            Add(string.Format(UiLocalization.Text("消耗1燃油 / +{0}%电力"), session.FuelPower.ToString("0")), () => ActionAndRefresh(() => session.BurnFuel()));
                            if(!session.efficientFuelGenerator)Add("【升级】高效燃油机：3 铁板 / 每桶20%",()=>ActionAndRefresh(()=>session.UpgradeGenerator()));
                        }
                    }
                    Add("安装电鳗：无限动力 / +30%耐久 / +20%航速", () => ActionAndRefresh(() => session.InstallEel()), session.GetResource(ResourceKind.Eel) > 0 && !session.infinitePower);
                    break;
                case StationKind.Searchlight:
                    detailText.text = "电路与电量充足时有效；T2夜间全视野，夜鸟刷新减少40%。";
                    Add(session.searchlightOn ? "关闭探照灯" : "开启探照灯", () => ActionAndRefresh(() => session.ToggleSearchlight()));
                    break;
                case StationKind.Turret:
                    detailText.text = "优先深渊之影，其次海鸥和0–20米鲨鱼；每3秒消耗1%电力。";
                    if(session.turretInstalled && session.turretTier<2)Add("升级炮塔T2：1铁板 3纤维束 1木板",()=>ActionAndRefresh(()=>session.UpgradeTurret()));
                    Add(session.turretInstalled ? session.turretOn ? "关闭炮塔" : "开启炮塔" : "建造：3木板 3铁板 1纤维束", () => ActionAndRefresh(() => { if (session.turretInstalled) session.ToggleTurret(); else session.BuildTurret(); }));
                    break;
                case StationKind.Fishing:
                    detailText.text = "先制作钓鱼竿。优质概率：浅滩10% / 中层20% / 深水30%；钓起自动享用。";
                    Add("抛竿 / 收线", StartFishing, session.hasRod);
                    Button hold = Add("按住收线 / " + actor.input.GetBindingLabel("Interact"), () => { }, session.hasRod);
                    heldButton = hold.gameObject.AddComponent<HoldActionButton>();
                    break;
                case StationKind.Filter:
                case StationKind.Circuit:
                case StationKind.Engine:
                    BuildRepair();
                    break;
                default:
                    detailText.text = "站点已准备。";
                    break;
            }
        }
        private void BuildRepair()
        {
            bool repaired = station == StationKind.Filter ? session.filterRepaired : station == StationKind.Circuit ? session.circuitRepaired : session.engineRepaired;
            if (repaired) { detailText.text = "设备运行正常。"; return; }
            if (!repairStarted)
            {
                detailText.text = station == StationKind.Filter ? "消耗2铁板，旋转管道把左侧入口连到右侧出口。" : station == StationKind.Circuit ? "消耗1纤维束，逐条连接四路同名端子。" : session.engineEverRepaired ? "免费重新检修：清理四处油污并匹配四种故障零件。" : "初次消耗2铁板：清理四处油污并匹配四种故障零件。";
                Add("开始维修", () => { if (session.TryRepairDevice(station)) { repairStarted = true; BuildActions(); } });
                return;
            }
            var obj=repairBoardPrefab!=null?Instantiate(repairBoardPrefab.gameObject,actionsRoot):new GameObject("Repair board / replaceable",typeof(RectTransform));
            obj.transform.SetParent(actionsRoot,false);obj.SetActive(true);buttons.Add(obj);
            repairBoard=obj.GetComponent<RepairBoardView>();if(repairBoard==null)repairBoard=obj.AddComponent<RepairBoardView>();
            repairBoard.theme=repairTheme;repairBoard.panel=this;repairBoard.session=session;repairBoard.hud=hud;repairBoard.actor=actor;
            repairBoard.Build(station);
        }
        public void RebuildRepair()=>BuildActions();
        public void ScrubEngineAt(int index,float seconds)
        {
            if(session==null||session.phase!=SessionPhase.Playing||!IsOpen||station!=StationKind.Engine||!repairStarted||index<0||index>=session.engineFaultClean.Length)return;
            selectedFault=index;session.engineFaultClean[index]=Mathf.Clamp01(session.engineFaultClean[index]+seconds*session.GetDeviceInteractionEfficiency(actor)/Mathf.Max(.1f,scrubSecondsPerFault));
        }

        private void ScrubSelected(float delta)
        {
            int fault = Mathf.Clamp((int)selectedFault, 0, session.engineFaultFixed.Length - 1);
            ScrubEngineAt(fault,delta);
        }
        private void Crank()
        {
            float interval = session.GetGeneratorStrokeInterval(actor);
            float beat = Mathf.Repeat(session.elapsed, interval) / interval;
            float previous = session.lastCrankAt;
            bool good = session.CrankGenerator(beat >= generatorWindow.x && beat <= generatorWindow.y, actor);
            if (session.lastCrankAt != previous) { generatorGood = good; generatorFeedback = qteView != null && qteView.theme != null ? qteView.theme.feedbackSeconds : .65f; }
        }
        private static string RecipeCosts(RecipeDefinition recipe)
        {
            string label = Costs(recipe.cost);
            if (recipe.consumedToolIds != null) foreach (string id in recipe.consumedToolIds)
                label += UiLocalization.Text(id == "harpoon" ? "鱼叉" : id) + "×1 ";
            return label;
        }
        private void StartFishing()
        {
            if (!session.hasRod || fishingActive) return;
            float x = session.ship.Position.x;
            float chance = x < session.tuning.shallowBoundary ? session.tuning.qualityFishShallow : x < session.tuning.deepBoundary ? session.tuning.qualityFishMiddle : session.tuning.qualityFishDeep;
            fishingQuality = UnityEngine.Random.value < chance;
            fishingActive = true; fishWait = UnityEngine.Random.Range(fishWaitSeconds.x, fishWaitSeconds.y) / session.GetDeviceInteractionEfficiency(actor);
            fishingTime = 0; fishingProgress = .35f; fishingZone = .5f; fishingVelocity = 0;
        }
        private void UpdateFishing(float dt)
        {
            if (!fishingActive) return;
            fishingTime += dt;
            if (fishingTime < fishWait) { liveText.text = "浮标等待鱼讯…"; return; }
            fishZoneHalfWidth = fishingQuality ? fishHalfWidths.y : fishHalfWidths.x;
            fishingVelocity += (IsHeld("Interact") ? fishRiseAcceleration : -fishFallAcceleration) * dt;
            fishingVelocity = Mathf.Clamp(fishingVelocity, -fishMaxVelocity, fishMaxVelocity);
            fishingZone = Mathf.Clamp(fishingZone + fishingVelocity * dt, fishZoneHalfWidth, 1 - fishZoneHalfWidth);
            fishingFish = Mathf.Clamp01(.5f + Mathf.Sin(fishingTime * (fishingQuality ? fishMoveSpeeds.y : fishMoveSpeeds.x)) * .3f + Mathf.Sin(fishingTime * 3.42f) * .12f);
            bool inRange = Mathf.Abs(fishingFish - fishingZone) <= fishZoneHalfWidth;
            fishingProgress = Mathf.Clamp01(fishingProgress + (inRange ? fishingQuality ? fishGainRates.y * session.GetDeviceInteractionEfficiency(actor) : fishGainRates.x * session.GetDeviceInteractionEfficiency(actor) : -(fishingQuality ? fishLossRates.y : fishLossRates.x)) * dt);
            rhythmFill.fillAmount = fishingProgress;
            SetRhythmRange(fishingZone, fishZoneHalfWidth * 2, fishingFish);
            liveText.text = (fishingQuality ? "优质鱼" : "普通鱼") + " · 区域 " + fishingZone.ToString("P0") + " / 鱼 " + fishingFish.ToString("P0") + "\n捕获 " + fishingProgress.ToString("P0") + " · 按住互动上升，松开下降";
            if (fishingProgress >= 1) { fishingActive = false; session.TryCatchFish(fishingQuality, actor); }
            else if (fishingProgress <= 0 || fishingTime - fishWait > fishTimeoutSeconds) { fishingActive = false; liveText.text = "鱼儿逃脱，重新抛竿。"; }
        }
        bool JobBelongsHere(CraftJob job) => CraftingManager.JobStation(job) == station;
        private void UpdateLive()
        {
            if (rhythmWindow != null) rhythmWindow.gameObject.SetActive(false);
            if (rhythmCursor != null) rhythmCursor.gameObject.SetActive(false);
            if (station == StationKind.Workbench || station == StationKind.HullRepair)
            {
                if (lastJobCount != session.jobs.Count) { selectedJobId = 0; BuildActions(); }
                CraftJob job = session.jobs.Find(candidate => candidate.id == selectedJobId && JobBelongsHere(candidate));
                session.craftingManager?.SetInteractionContext(actor, job != null ? job.id : 0, station);
                if (job == null) { liveText.text = "作业槽 " + session.jobs.Count + "/" + session.CraftCapacity; rhythmFill.fillAmount = 0; if (qteView != null) qteView.gameObject.SetActive(false); return; }
                float offset = job.nextCue - job.elapsed;
                rhythmFill.fillAmount = Mathf.Clamp01(1 - offset);
                SetRhythmRange(.5f, session.GetCraftQteWindow(job) * 2 / session.tuning.craftCueInterval, .5f - offset / session.tuning.craftCueInterval);
                if (qteView != null) qteView.ShowLane(actor.input, job.noteDirection, job.elapsed, job.nextCue,
                    session.tuning.craftCueInterval, session.GetCraftQteWindow(job), job.feedbackTime, job.feedback != null && job.feedback.StartsWith("+"));
                string key = actor.input.GetBindingLabel("Move", job.noteDirection + 1);
                liveText.text = job.recipe.displayName + " · " + job.NormalizedProgress.ToString("P0") + "\n" + "下个音符 [" + key + "] · " + Mathf.Max(0, offset).ToString("0.00") + "s" + "  " + (job.feedbackTime > 0 ? job.feedback : "指针进入绿色区域时按键");
            }
            else if (station == StationKind.Winch)
            {
                float bonus = session.tuning.winchBaseBonus + (session.GetUpgrade(UpgradeKind.Rope) >= 2 ? session.tuning.winchT2Bonus : 0);
                float half = session.tuning.winchOriginalWidth * (1 + bonus) * .5f;
                rhythmFill.fillAmount = WinchAngle();
                SetRhythmRange(session.WinchQteCenter, half * 2, WinchAngle());
                if (qteView != null) qteView.ShowCircle(actor.input, WinchAngle(), session.WinchQteCenter,
                    half * 2, session.winchProgress, session.winchFeedbackTime, session.winchFeedback.Contains("成功") || session.winchProgress >= 1,
                    false);
                rhythmFill.color = session.winchFeedbackTime > 0 ? session.winchFeedback.Contains("成功") ? new Color(.3f,.9f,.5f) : new Color(.95f,.4f,.3f) : new Color(.83f,.68f,.35f);
                liveText.text = session.WinchTargetCount + " × " + session.WinchEfficiency.ToString("P0") + " · " + session.winchProgress.ToString("P0") + " · " + session.winchFeedback + "\n指针 " + WinchAngle().ToString("0.00") + " / 绿色窗口 " + (session.WinchQteCenter - half).ToString("0.000") + " — " + (session.WinchQteCenter + half).ToString("0.000");
            }
            else if (station == StationKind.Generator)
            {
                float interval = session.GetGeneratorStrokeInterval(actor);
                float beat = Mathf.Repeat(session.elapsed, interval) / interval;
                rhythmFill.fillAmount = beat;
                SetRhythmRange((generatorWindow.x + generatorWindow.y) * .5f, generatorWindow.y - generatorWindow.x, beat);
                if (qteView != null && !session.infinitePower) qteView.ShowCircle(actor.input, beat,
                    (generatorWindow.x + generatorWindow.y) * .5f, generatorWindow.y - generatorWindow.x,
                    session.power / Mathf.Max(1, session.tuning.maxPower), generatorFeedback, generatorGood);
                liveText.text = "电力 " + session.power.ToString("0") + "% · " + (session.infinitePower ? "电鳗供能" : session.generatorTier > 0 ? "燃油 / 手摇双模式" : "手摇") + "\n指针进入绿色区域时发力 / 当前 " + beat.ToString("0.00");
            }
            else if (station == StationKind.Engine && repairStarted)
            {
                int fault = Mathf.Clamp((int)selectedFault, 0, session.engineFaultFixed.Length - 1);
                rhythmFill.fillAmount = session.engineFaultClean[fault];
                liveText.text = "故障 " + (fault + 1) + " 清理 " + session.engineFaultClean[fault].ToString("P0");
            }
            else if (station != StationKind.Fishing) { liveText.text = ""; rhythmFill.fillAmount = 0; }
        }
        private void ConfigureQtePresentation()
        {
            if (qteView == null || qteView.theme == null) return;
            bool lane = station == StationKind.Workbench || station == StationKind.HullRepair;
            bool circle = station == StationKind.Winch || station == StationKind.Generator && !session.infinitePower;
            bool graphical = lane || circle;
            bool repair = station == StationKind.Filter || station == StationKind.Circuit || station == StationKind.Engine;
            qteView.gameObject.SetActive(graphical); qteView.SetMode(circle);
            if (rhythmFill != null) rhythmFill.transform.parent.gameObject.SetActive(!graphical);
            var root = (RectTransform)qteView.transform;
            root.anchorMin = new Vector2(.05f, lane ? .44f : .30f); root.anchorMax = new Vector2(.95f, .63f);
            root.offsetMin = root.offsetMax = Vector2.zero;
            liveText.rectTransform.anchorMin = new Vector2(.04f, repair ? .77f : graphical ? .64f : .57f);
            liveText.rectTransform.anchorMax = new Vector2(.96f, repair ? .81f : graphical ? .75f : .73f);
            liveText.fontSize = repair ? 14 : 18;
            if (actionsRoot != null && actionsRoot.parent is RectTransform scroll)
            {
                scroll.anchorMax = new Vector2(.965f, repair ? .76f : lane ? .43f : circle ? .29f : .52f);
                scroll.offsetMin = scroll.offsetMax = Vector2.zero;
            }
        }

        private void SetRhythmRange(float center, float width, float cursor)
        {
            if (rhythmWindow != null)
            {
                rhythmWindow.gameObject.SetActive(true);
                RectTransform range = rhythmWindow.rectTransform;
                range.anchorMin = new Vector2(Mathf.Clamp01(center - width * .5f), 0);
                range.anchorMax = new Vector2(Mathf.Clamp01(center + width * .5f), 1);
                range.offsetMin = range.offsetMax = Vector2.zero;
            }
            if (rhythmCursor != null)
            {
                rhythmCursor.gameObject.SetActive(true);
                RectTransform marker = rhythmCursor.rectTransform;
                marker.anchorMin = new Vector2(Mathf.Clamp01(cursor), 0); marker.anchorMax = new Vector2(Mathf.Clamp01(cursor), 1);
                marker.offsetMin = new Vector2(-2, -3); marker.offsetMax = new Vector2(2, 3);
            }
        }
        private static string Costs(ResourceAmount[] costs)
        {
            string text = "";
            foreach (ResourceAmount cost in costs) if (cost != null) text += GameNames.Resource(cost.resource) + "×" + cost.amount + " ";
            return text;
        }
        private static string UpgradeLabel(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Rope: return "绳索 / 绞盘";
                case UpgradeKind.Oxygen: return "氧气设备";
                case UpgradeKind.Hull: return "船体加固";
                case UpgradeKind.Sail: return "船帆 / 舵";
                case UpgradeKind.Searchlight: return "探照灯";
                case UpgradeKind.Backpack: return "潜水背包";
                case UpgradeKind.DivingSuit: return "潜水服";
                default: return "制作台";
            }
        }
    }
}
