using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lifeline
{
    /// <summary>Replaceable UGUI view; all world mutations are delegated to the session.</summary>
    public partial class CrewStationPanel : MonoBehaviour
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
        public float fishRiseAcceleration = 1.44f, fishFallAcceleration = .99f, fishMaxVelocity = .63f;
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
        RecipeDefinition batchRecipe;
        int batchQuantity = 1;

        public void Initialize(GameHUDController owner, GameSession game, CrewActor crew)
        {
            hud = owner; session = game; actor = crew;
            if (closeButton != null) { closeButton.onClick.RemoveListener(RequestClose); closeButton.onClick.AddListener(RequestClose); }
            panel.SetActive(false);
        }
        public void Open(StationKind kind)
        {
            if (session == null || session.phase != SessionPhase.Playing || actor == null || actor.downed || !actor.onboard || !session.IsStationUnlocked(kind)) return;
            if (IsOpen && station == kind) return;
            Close(); station = kind; actor.controlsCaptured = true; previousMove = Vector2.zero;
            panel.SetActive(true); titleText.text = GameNames.Role(actor.role) + " / " + GameNames.Station(kind);
            repairStarted = session.repairAuthorized[(int)kind];
            if (kind == StationKind.Winch && !ReplicaPanel) session.SetWinchActive(true,actor);
            if (kind == StationKind.Engine) engineVersion = session.engineRepairVersion;
            BuildActions();
        }
        public void Close()
        {
            if (actor != null) { actor.controlsCaptured = false; session?.craftingManager?.ClearInteractionContext(actor); }
            if (session != null && IsOpen && station == StationKind.Winch && !ReplicaPanel) session.SetWinchActive(false);
            if (panel != null) panel.SetActive(false);
            fishingActive = false; heldButton = null; previousMove = Vector2.zero;
            batchRecipe = null; batchQuantity = 1; hasStationMotion = false; remoteHeld = false;
            selectedJobId=0;lastJobCount=-1;generatorFeedback=0;repairStarted=false;
            pointerHeldUntil = 0; pointerFault = -1;
            ClearActions();repairBoard=null;
            if(qteView!=null)qteView.gameObject.SetActive(false);
            if(liveText!=null)liveText.text="";if(detailText!=null)detailText.text="";
        }
        private void Update()
        {
            if (!IsOpen || session == null || actor == null) return;
            if (ReplicaPanel) { UpdateReplicaStation(); return; }
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
            if (OnlineVoyage.InProgress && OnlineVoyage.Current.LocalRole != actor.role) return remoteHeld;
            return actor.input != null && actor.input.IsActionHeld(action);
        }
        public void HandleCommand(CrewCommand command)
        {
            if (!IsOpen || session.phase != SessionPhase.Playing || actor == null || session.IsIncapacitated(actor)) return;
            remoteHeld = command.interactHeld;
            if (station == StationKind.Workbench || station == StationKind.HullRepair)
            {
                int notes = command.noteKeys;
                if (notes == 0 && command.move.sqrMagnitude > .2f && (previousMove.sqrMagnitude < .2f || Vector2.Dot(previousMove, command.move) < .5f))
                    notes = 1 << CrewInputDefaults.DirectionNote(command.move);
                if (batchRecipe != null)
                {
                    if ((notes & 1) != 0) AdjustBatch(1);
                    if ((notes & 4) != 0) AdjustBatch(-1);
                    if (command.interact) ConfirmBatch();
                }
                else if (notes != 0 && ActiveJob() is CraftJob job)
                {
                    int direction = 0;
                    while (direction < 5 && (notes & (1 << direction)) == 0) direction++;
                    session.craftingManager.CraftQteAtStation(job.id, direction, station,
                        InputRewind(command) * session.craftingManager.GetInteractionRate(job));
                }
            }
            else if (station == StationKind.Engine && repairStarted && command.move.sqrMagnitude>.2f && previousMove.sqrMagnitude<.2f)
            { int part=Mathf.Abs(command.move.y)>=Mathf.Abs(command.move.x)?command.move.y>0?0:2:command.move.x<0?1:3; session.TryRepairEngineFault((int)selectedFault,part); BuildActions(); }
            else if (station == StationKind.Winch && command.calibrate) session.TryWinchQte(WinchAngle(InputRewind(command)));
            else if (station == StationKind.Generator && command.calibrate) Crank(InputRewind(command));
            else if (station == StationKind.Fishing && command.interact) StartFishing();
            previousMove = command.move;
        }
        private float WinchAngle(float rewind = 0)
        {
            float clock = HasMotion ? replicaMotion.workClock + replicaMotion.rate * OnlineSampleAge
                : session.winchCycleTime - rewind * session.GetDeviceInteractionEfficiency(actor);
            return Mathf.Repeat(clock / Mathf.Max(.1f, session.tuning.winchCycle), 1f);
        }
        private void ClearActions()
        {
            foreach (GameObject button in buttons) if (button != null) { button.SetActive(false); Destroy(button); }
            buttons.Clear(); heldButton = null;
            onlineActions.Clear();
            actionIndex = 0;
        }
        private Button Add(string label, Action action, bool enabled = true)
        {
            Button button = Instantiate(actionButtonPrefab, actionsRoot); button.gameObject.SetActive(true);
            button.GetComponentInChildren<Text>().text = label; button.interactable = enabled;
            button.onClick.RemoveAllListeners();
            BindOnlineAction(button, "a" + actionIndex++, action);
            buttons.Add(button.gameObject); return button;
        }
        private void ActionAndRefresh(Action action) { action(); BuildActions(); }
        private void BuildActions()
        {
            if (!ReplicaPanel) actionRevision++;
            ClearActions();
            var manager = session.GetOrCreateCraftingManager();
            lastJobCount = manager.SlotCount(station);
            ConfigureQtePresentation();
            if(station==StationKind.Filter||station==StationKind.Circuit||station==StationKind.Engine){if(rhythmFill!=null)rhythmFill.transform.parent.gameObject.SetActive(false);}
            switch (station)
            {
                case StationKind.Workbench:
                case StationKind.HullRepair:
                    if (batchRecipe != null)
                    {
                        Add("增加数量", () => AdjustBatch(1));
                        Add("减少数量", () => AdjustBatch(-1));
                        Add("确认制作", ConfirmBatch);
                        Add("返回配方", () => { batchRecipe = null; BuildActions(); });
                        UpdateBatchText();
                        break;
                    }
                    detailText.text = station == StationKind.Workbench
                        ? string.Format(UiLocalization.Text("制作作业 {0}秒。校准成功加进度，离开仍继续，不返还已投入材料。"), session.GetCraftDuration().ToString("0"))
                        : "普通作业 " + session.tuning.craftSeconds.ToString("0") + "秒；快速修补用时减半。校准成功加进度，离开仍继续，不返还已投入材料。";
                    if (ActiveJob() == null) selectedJobId = 0;
                    foreach (CraftJob job in session.jobs)
                    {
                        if (!JobBelongsHere(job)) continue;
                        int id = job.id; Add("选择校准：" + job.recipe.displayName + " ×" + Mathf.Max(1, job.quantity), () => { selectedJobId = id; session.craftingManager?.SetInteractionContext(actor, id, station); });
                        if (selectedJobId == 0) selectedJobId = id;
                    }
                    foreach (RecipeDefinition recipe in session.recipes)
                    {
                        if (recipe == null || (station == StationKind.HullRepair) != (recipe.repairAmount > 0)) continue;
                        RecipeDefinition chosen = recipe;
                        Add(recipe.displayName + "  " + RecipeCosts(recipe) + " / " + session.GetCraftDuration(recipe).ToString("0.#") + "s",
                            () => SelectRecipe(chosen), manager.SlotCount(station) < manager.SlotCapacity(station));
                    }
                    if (!ReplicaPanel) session.craftingManager?.SetInteractionContext(actor, selectedJobId, station);
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
                    Add("节奏校准 [" + actor.input.GetBindingLabel("Calibrate") + "]", () => session.TryWinchQte(WinchAngle()), true);
                    break;
                case StationKind.Generator:
                    detailText.text = "手摇每3秒成功 +1%；燃油机每桶 +15%，再用3铁板升级至 +20%。保留手摇。";
                    if (!session.infinitePower)
                    {
                        Add("节奏发力 [" + actor.input.GetBindingLabel("Calibrate") + "]", () => Crank());
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
            if (repairTheme != null) repairBoard.theme = repairTheme;
            repairBoard.panel=this;repairBoard.session=session;repairBoard.hud=hud;repairBoard.actor=actor;
            repairBoard.Build(station);
        }
        public void RebuildRepair()=>BuildActions();
        public void ScrubEngineAt(int index,float seconds)
        {
            if (ReplicaPanel) { HoldOnlineEngineScrub(index); return; }
            if(session==null||session.phase!=SessionPhase.Playing||!IsOpen||station!=StationKind.Engine||!repairStarted||index<0||index>=session.engineFaultClean.Length)return;
            selectedFault=index;session.engineFaultClean[index]=Mathf.Clamp01(session.engineFaultClean[index]+seconds*session.GetDeviceInteractionEfficiency(actor)/Mathf.Max(.1f,scrubSecondsPerFault));
        }

        private void ScrubSelected(float delta)
        {
            int fault = Mathf.Clamp((int)selectedFault, 0, session.engineFaultFixed.Length - 1);
            ScrubEngineAt(fault,delta);
        }
        private void Crank(float rewind = 0)
        {
            float interval = session.GetGeneratorStrokeInterval(actor);
            float beat = Mathf.Repeat(session.elapsed - rewind, interval) / interval;
            float previous = session.lastCrankAt;
            bool good = session.CrankGenerator(beat >= generatorWindow.x && beat <= generatorWindow.y, actor);
            if (session.lastCrankAt != previous) { generatorGood = good; generatorFeedback = qteView != null && qteView.theme != null ? qteView.theme.feedbackSeconds : .65f; }
        }
        void SelectRecipe(RecipeDefinition recipe)
        {
            if (station == StationKind.Workbench && session.craftingManager.CanBatch(recipe))
            {
                batchRecipe = recipe;
                batchQuantity = 1;
                session.craftingManager.ClearInteractionContext(actor);
                BuildActions();
                return;
            }
            ActionAndRefresh(() => session.TryCraft(recipe.id, actor));
        }

        void AdjustBatch(int direction) => batchQuantity = Mathf.Clamp(batchQuantity + direction, 1, CraftingManager.MaximumBatch);

        void ConfirmBatch()
        {
            if (batchRecipe == null || !session.craftingManager.TryCraft(batchRecipe.id, actor, batchQuantity)) return;
            batchRecipe = null;
            BuildActions();
        }

        void UpdateBatchText()
        {
            detailText.text = string.Format(UiLocalization.Text("{0} ×{1} · {2} · {3:0.#} 秒"),
                UiLocalization.Text(batchRecipe.displayName), batchQuantity, Costs(batchRecipe.cost, batchQuantity),
                session.GetCraftDuration(batchRecipe) * batchQuantity);
            liveText.text = string.Format(UiLocalization.Text("[{0}] 增加 / [{1}] 减少 / [{2}] 确认制作"),
                actor.input.GetBindingLabel("Move", 1), actor.input.GetBindingLabel("Move", 3), actor.input.GetBindingLabel("Interact"));
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
            fishingFish = FishPosition(fishingTime);
            bool inRange = Mathf.Abs(fishingFish - fishingZone) <= fishZoneHalfWidth;
            fishingProgress = Mathf.Clamp01(fishingProgress + (inRange ? fishingQuality ? fishGainRates.y * session.GetDeviceInteractionEfficiency(actor) : fishGainRates.x * session.GetDeviceInteractionEfficiency(actor) : -(fishingQuality ? fishLossRates.y : fishLossRates.x)) * dt);
            ShowFishingRange(fishingZone, fishZoneHalfWidth, fishingFish);
            liveText.text = FishingText(fishingProgress);
            if (fishingProgress >= 1) { fishingActive = false; session.TryCatchFish(fishingQuality, actor); }
            else if (fishingProgress <= 0 || fishingTime - fishWait > fishTimeoutSeconds) { fishingActive = false; liveText.text = "鱼儿逃脱，重新抛竿。"; }
        }
        float FishPosition(float time) => Mathf.Clamp01(.5f +
            Mathf.Sin(time * (fishingQuality ? fishMoveSpeeds.y : fishMoveSpeeds.x)) * .3f + Mathf.Sin(time * 3.42f) * .12f);

        string FishingText(float progress) => string.Format(UiLocalization.Text("{0} · 捕获 {1:P0}\n按住 [{2}] 上升，松开下降"),
            UiLocalization.Text(fishingQuality ? "优质鱼" : "普通鱼"), progress, actor.input.GetBindingLabel("Interact"));

        void ShowFishingRange(float zone, float halfWidth, float fish)
        {
            SetRhythmRange(zone, halfWidth * 2, fish);
            if (rhythmWindow != null) rhythmWindow.color = new Color(1f, 1f, 1f, .72f);
            if (rhythmCursor != null) rhythmCursor.color = new Color(1f, .68f, .18f, 1f);
        }

        bool JobBelongsHere(CraftJob job) => CraftingManager.JobStation(job) == station;
        CraftJob ActiveJob() => session.jobs.Find(candidate => candidate != null && candidate.id == selectedJobId && JobBelongsHere(candidate));
        private void UpdateLive()
        {
            if (rhythmWindow != null) rhythmWindow.gameObject.SetActive(false);
            if (rhythmCursor != null) rhythmCursor.gameObject.SetActive(false);
            if (station == StationKind.Workbench || station == StationKind.HullRepair)
            {
                if (batchRecipe != null) { UpdateBatchText(); return; }
                var manager = session.craftingManager;
                int active = manager.SlotCount(station);
                if (lastJobCount != active) BuildActions();
                CraftJob job = ActiveJob();
                if (!ReplicaPanel) session.craftingManager?.SetInteractionContext(actor, job != null ? job.id : 0, station);
                if (job == null) { liveText.text = "作业槽 " + active + "/" + manager.SlotCapacity(station); rhythmFill.fillAmount = 0; if (qteView != null) qteView.gameObject.SetActive(false); return; }
                bool sampled = HasMotion && replicaMotion.job == job.id;
                float clock = sampled ? replicaMotion.workClock + replicaMotion.rate * OnlineSampleAge : job.elapsed;
                float cue = sampled ? replicaMotion.cueCenter : job.nextCue;
                int note = sampled ? replicaMotion.note : job.noteDirection;
                float feedback = sampled ? Mathf.Max(0f, replicaMotion.feedbackTime - OnlineSampleAge) : job.feedbackTime;
                float offset = cue - clock;
                rhythmFill.fillAmount = Mathf.Clamp01(1 - offset);
                SetRhythmRange(.5f, session.GetCraftQteWindow(job) * 2 / session.tuning.craftCueInterval, .5f - offset / session.tuning.craftCueInterval);
                if (qteView != null) qteView.ShowLane(actor.input, note, clock, cue,
                    session.tuning.craftCueInterval, session.GetCraftQteWindow(job), feedback, sampled ? replicaMotion.good : job.feedback != null && job.feedback.StartsWith("+"));
                string key = actor.input.GetBindingLabel(CrewInputDefaults.NoteAction(note), CrewInputDefaults.NoteBinding(note));
                liveText.text = job.recipe.displayName + " ×" + Mathf.Max(1, job.quantity) + " · " + job.NormalizedProgress.ToString("P0") + "\n" + "下个音符 [" + key + "] · " + Mathf.Max(0, offset).ToString("0.00") + "s" + "  " + (feedback > 0 ? job.feedback : "指针进入绿色区域时按键");
            }
            else if (station == StationKind.Winch)
            {
                float bonus = session.tuning.winchBaseBonus + (session.GetUpgrade(UpgradeKind.Rope) >= 2 ? session.tuning.winchT2Bonus : 0);
                float half = session.tuning.winchOriginalWidth * (1 + bonus) * .5f;
                rhythmFill.fillAmount = WinchAngle();
                float center = HasMotion ? replicaMotion.cueCenter : session.WinchQteCenter;
                SetRhythmRange(center, half * 2, WinchAngle());
                if (qteView != null) qteView.ShowCircle(actor.input, WinchAngle(), center,
                    half * 2, session.winchProgress, session.winchFeedbackTime, session.winchFeedback.Contains("成功") || session.winchProgress >= 1,
                    false);
                rhythmFill.color = session.winchFeedbackTime > 0 ? session.winchFeedback.Contains("成功") ? new Color(.3f,.9f,.5f) : new Color(.95f,.4f,.3f) : new Color(.83f,.68f,.35f);
                liveText.text = session.WinchTargetCount + " × " + session.WinchEfficiency.ToString("P0") + " · " + session.winchProgress.ToString("P0") + " · " + session.winchFeedback + "\n指针 " + WinchAngle().ToString("0.00") + " / 绿色窗口 " + (session.WinchQteCenter - half).ToString("0.000") + " — " + (session.WinchQteCenter + half).ToString("0.000");
            }
            else if (station == StationKind.Generator)
            {
                float interval = session.GetGeneratorStrokeInterval(actor);
                float beat = Mathf.Repeat(HasMotion ? replicaMotion.workClock + OnlineSampleAge : session.elapsed, interval) / interval;
                rhythmFill.fillAmount = beat;
                SetRhythmRange((generatorWindow.x + generatorWindow.y) * .5f, generatorWindow.y - generatorWindow.x, beat);
                if (qteView != null && !session.infinitePower) qteView.ShowCircle(actor.input, beat,
                    (generatorWindow.x + generatorWindow.y) * .5f, generatorWindow.y - generatorWindow.x,
                    session.power / Mathf.Max(1, session.tuning.maxPower),
                    HasMotion ? Mathf.Max(0, replicaMotion.feedbackTime - OnlineSampleAge) : generatorFeedback,
                    HasMotion ? replicaMotion.good : generatorGood);
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
            bool work = station == StationKind.Workbench || station == StationKind.HullRepair;
            bool lane = work && batchRecipe == null && session.craftingManager.SlotCount(station) > 0;
            bool circle = station == StationKind.Winch || station == StationKind.Generator && !session.infinitePower;
            bool graphical = lane || circle;
            bool repair = station == StationKind.Filter || station == StationKind.Circuit || station == StationKind.Engine;
            qteView.gameObject.SetActive(graphical); qteView.SetMode(circle);
            if (rhythmFill != null) rhythmFill.transform.parent.gameObject.SetActive(!graphical && !work);
            if (rhythmFill != null) rhythmFill.enabled = station != StationKind.Fishing && batchRecipe == null;
            var root = (RectTransform)qteView.transform;
            root.anchorMin = new Vector2(.05f, lane ? .44f : .30f); root.anchorMax = new Vector2(.95f, .63f);
            root.offsetMin = root.offsetMax = Vector2.zero;
            liveText.rectTransform.anchorMin = new Vector2(.04f, repair ? .765f : graphical ? .64f : work ? .725f : .57f);
            liveText.rectTransform.anchorMax = new Vector2(.96f, repair ? .805f : graphical ? .75f : work ? .795f : .73f);
            liveText.fontSize = repair ? 14 : 18;
            if (actionsRoot != null && actionsRoot.parent is RectTransform scroll)
            {
                scroll.anchorMax = new Vector2(.965f, repair ? .76f : lane ? .43f : circle ? .29f : work ? .72f : .52f);
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
        private static string Costs(ResourceAmount[] costs, int quantity = 1)
        {
            string text = "";
            if (costs != null) foreach (ResourceAmount cost in costs)
                if (cost != null) text += UiLocalization.Text(GameNames.Resource(cost.resource)) + "×" + (cost.amount * quantity) + " ";
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
