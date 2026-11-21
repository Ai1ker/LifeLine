using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lifeline
{
    /// <summary>The authoritative, UI-independent local cooperative game simulation.</summary>
    public partial class GameSession : MonoBehaviour
    {
        public static GameSession Current { get; private set; }
        internal static void RestoreCurrentAfterValidation(GameSession previous) { Current = previous; }

        [Header("场景依赖 / editable scene references")]
        public GameTuning tuning;
        public CrewActor captain;
        public CrewActor diver;
        public ShipMotor ship;
        public WorldGenerator world;
        [Tooltip("Independent ship and underwater map spaces share this authoritative session.")]
        public GameMapPartition maps;
        [Tooltip("Collision contact paths used by the diver tether and cargo winch.")]
        public DiverRope diverRope, cargoRope;
        [Tooltip("The independent owner of recipe SO references, jobs, QTE rules and crafting outcomes.")]
        public CraftingManager craftingManager;
        [SerializeField, HideInInspector, FormerlySerializedAs("recipes")]
        RecipeDefinition[] legacyRecipes = new RecipeDefinition[0];
        [SerializeField, HideInInspector, FormerlySerializedAs("jobs")]
        List<CraftJob> legacyJobs = new List<CraftJob>();
        // Compatibility views stay pure on read: migration previews must not add components to old scenes.
        public RecipeDefinition[] recipes
        {
            get
            {
                CraftingManager manager = craftingManager != null ? craftingManager : GetComponent<CraftingManager>();
                if (manager != null && manager.recipeDefinitions != null && (manager.recipeDefinitions.Length > 0 || legacyRecipes == null || legacyRecipes.Length == 0)) return manager.recipeDefinitions;
                return legacyRecipes ?? new RecipeDefinition[0];
            }
            set { GetOrCreateCraftingManager().SetRecipeDefinitions(value); legacyRecipes = new RecipeDefinition[0]; }
        }
        public List<CraftJob> jobs
        {
            get
            {
                CraftingManager manager = craftingManager != null ? craftingManager : GetComponent<CraftingManager>();
                if (manager != null && manager.jobs != null && (manager.jobs.Count > 0 || legacyJobs == null || legacyJobs.Count == 0)) return manager.jobs;
                return legacyJobs;
            }
            set { GetOrCreateCraftingManager().ReplaceJobs(value); legacyJobs = new List<CraftJob>(); }
        }
        [Tooltip("Editable collision roots with a replaceable VisualRoot child. Null uses geometric fallback.")]
        public GameObject sharkPrefab, birdPrefab, shadowPrefab, spearPrefab, piranhaPrefab;
        public bool autoStart = true;
        public int worldSeed = 8417;

        [Header("共享经济 / authoritative bank and upgrades")]
        public List<ResourceAmount> bank = new List<ResourceAmount>();
        [Tooltip("Indexed by UpgradeKind. Workbench: 0 base, 1 wider QTE, 2 faster, 3 two jobs.")]
        public int[] upgradeLevels = new int[8];
        public ResourceAmount[] initialResources = { new ResourceAmount(ResourceKind.Wood, 2) };
        [Tooltip("Cost for rope T1-T3; crafted materials, not raw loot.")]
        public ResourceAmount[] ropeUpgradeCost = { new ResourceAmount(ResourceKind.FiberBundle, 2) };
        [Tooltip("Cost for EACH of rope T4 (150m) and T5 (200m).")]
        public ResourceAmount[] longRopeUpgradeCost = { new ResourceAmount(ResourceKind.FiberBundle, 3) };
        public ResourceAmount[] oxygenUpgradeCost = { new ResourceAmount(ResourceKind.Plate, 2) };
        public ResourceAmount[] hullUpgradeCost = { new ResourceAmount(ResourceKind.Plate, 3) };
        public ResourceAmount[] sailUpgradeCost = { new ResourceAmount(ResourceKind.Plank, 1), new ResourceAmount(ResourceKind.FiberBundle, 1) };
        public ResourceAmount[] lightUpgradeCost = { new ResourceAmount(ResourceKind.Plank, 2), new ResourceAmount(ResourceKind.Plate, 2) };
        public ResourceAmount[] workbenchUpgradeCost = { new ResourceAmount(ResourceKind.Plank, 2) };
        public ResourceAmount[] backpackUpgradeCost = { new ResourceAmount(ResourceKind.FiberBundle, 2) };
        public ResourceAmount[] divingSuitUpgradeCost = { new ResourceAmount(ResourceKind.Plate, 1), new ResourceAmount(ResourceKind.FiberBundle, 2) };
        public ResourceAmount[] generatorUpgradeCost = { new ResourceAmount(ResourceKind.Plate, 1) };
        public ResourceAmount[] turretBuildCost = { new ResourceAmount(ResourceKind.Plank, 3), new ResourceAmount(ResourceKind.Plate, 3), new ResourceAmount(ResourceKind.FiberBundle, 1) };
        public CrewBuffs captainBuffs = new CrewBuffs();
        public CrewBuffs diverBuffs = new CrewBuffs();
        public bool hasRod;
        public bool hasHarpoon;
        public bool hasSpeargun;

        [Header("船舶状态 / hull, power and devices")]
        public SessionPhase phase = SessionPhase.Menu;
        public float hull;
        public float hullMax;
        [Range(0, 100)] public float power;
        public bool filterRepaired;
        public bool circuitRepaired;
        public bool engineRepaired;
        public bool engineEverRepaired;
        public int engineRepairVersion;
        [Tooltip("0 hand crank, 1 fuel, 2 installed eel / unlimited electricity.")]
        public int generatorTier;
        public bool efficientFuelGenerator;
        public ResourceAmount[] generatorEfficiencyCost = { new ResourceAmount(ResourceKind.Plate, 3) };
        public bool infinitePower;
        public bool searchlightOn;
        public bool turretInstalled;
        public bool turretOn;
        public int turretTier;
        public ResourceAmount[] turretUpgradeCost = { new ResourceAmount(ResourceKind.Plate,1), new ResourceAmount(ResourceKind.FiberBundle,3), new ResourceAmount(ResourceKind.Plank,1) };
        public float filterMoveMultiplier = 1.2f;
        public float repairedEngineSpeedMultiplier = 1.4f;
        public float sailSpeedBonus = .15f;
        public float lastCrankAt = -100;
        public float lowPowerSailingSeconds;

        [Header("引擎故障 / four individually cleaned, matching repairs")]
        public float[] engineFaultClean = new float[4];
        public bool[] engineFaultFixed = new bool[4];
        public int[] engineFaultTypes = { 0, 1, 2, 3 };
        public bool[] repairAuthorized = new bool[14];
        public ResourceAmount[] filterRepairCost = { new ResourceAmount(ResourceKind.Plate, 2) };
        public ResourceAmount[] circuitRepairCost = { new ResourceAmount(ResourceKind.FiberBundle, 1) };

        [Header("潜水与绞盘 / metres, seconds")]
        public float ropeLength;
        public Vector3 boatAnchorLocal = new Vector3(0, -2.2f, 0);
        public Vector3 captainSpawnLocal = new Vector3(-3, .9f, 0);
        public Vector3 diverSpawnLocal = new Vector3(6, .9f, 0);
        public Vector3 bankArrivalLocal = new Vector3(6, .9f, 0);
        public float boardDistance = 1.15f;
        public bool diverFainted;
        public float diverBites;
        public float diverInvulnerable;
        public float biteInvulnerabilitySeconds = 2.8f;
        public bool diverSprinting;
        public bool diverHidden;
        public bool winchActive;
        public bool winchElectric;
        [Tooltip("Installed electric winch may automatically recover a diver after the configured signal sequence.")]
        public bool winchSignalAuthorized;
        public float winchProgress;
        public float winchCycleTime;
        public bool winchAttemptedThisCycle;
        public float winchJam;
        public float winchStartDistance;
        public float winchFeedbackTime;
        public string winchFeedback;
        public LootPickup winchCargo;
        public string winchTargetKind;
        [Tooltip("Passive unconscious drift does not permit boarding.")]
        public float faintDriftSpeed = .3f;
        public float faintOxygenRecovery = 12f;
        public float diverRecallDepth = -2.2f;
        public float lastSignalAt = -100;
        public int signalCount;

        [Header("昼夜、天气与主线 / story clock")]
        public float elapsed;
        public int day = 1;
        public int radioStage;
        public bool rescueVoyage;
        public float rescueProgress;
        public float rescueWinchSeconds = 12f;
        public float rescueArrivalX = 188.88889f;
        public float stormCountdown;
        public float stormWarning;
        public CrewRole lightningTarget;
        public bool shadowSpawnedThisNight;
        public int shadowHits;
        public bool shadowRepelled;
        public bool shadowWorldRefreshPending;
        public float crewWipeTimer;
        public float crewWipeDelay = 2.5f;
        [Header("持续扶起 / hold interaction to revive")]
        public bool[] reviveHeld = new bool[2];
        public float[] reviveProgress = new float[2];
        public CrewActor[] reviveTargets = new CrewActor[2];
        [TextArea] public string StatusMessage;
        public int StatusAudienceRole { get; internal set; } = -1;
        [TextArea] public string QuestText;
        public List<string> storyLog = new List<string>();

        [Header("战斗配置 / public balance and collision masks")]
        public List<LifelineEnemy> enemies = new List<LifelineEnemy>();
        public LayerMask obstacleMask = ~0;
        public Material piranhaMaterial;
        public Material sharkMaterial;
        public Material whiteSharkMaterial;
        public Material birdMaterial;
        public Material shadowMaterial;
        public Material spearMaterial;
        public float nightOxygenMultiplier = 2f;
        [Tooltip("Prototype has no extra standby drain. Set above zero only for a deliberate balance variant.")]
        public float electricWinchPowerPerSecond = 0;
        public float searchlightPowerPerSecond = 0;
        public float meleeDamage = 1f;
        public float harpoonDamage = 2f;
        public float spearDamage = 2f;
        public float hullContactDamage = 4f;
        public float lightningHullDamage = 5f;
        public float lightningRadius = 2.2f;
        public float birdCountdown;
        public float turretShotClock;
        public float turretPowerClock;
        public float baitTime;
        public Vector3 baitPosition;
        public float[] meleeCooldowns = new float[2];
        public LifelineSpear[] activeSpears = new LifelineSpear[2];

        public event Action<string> Message;
        public event Action<StationKind, CrewActor> StationRequested;
        public event Action StateChanged;
        public event Action<CrewActor, CrewCommand> CommandReceived;
        public event Action<bool> SessionReady;
        public event Action<int> DayReady;
        public bool IsInitialized => initialized;
        float notifyClock;
        bool initialized;
        int previousDay;

        public float DayProgress => tuning != null ? Mathf.Repeat(elapsed, Mathf.Max(1, tuning.daySeconds)) / Mathf.Max(1, tuning.daySeconds) : 0;
        public bool IsNight => tuning != null && DayProgress >= tuning.dayFraction + tuning.duskFraction;
        public bool IsDusk => tuning != null && DayProgress >= tuning.dayFraction && !IsNight;
        public Vector3 ShipToWaterPoint(Vector3 position) => maps != null ? maps.ShipToWaterPoint(position) : position;
        public Vector3 WaterToShipPoint(Vector3 position) => maps != null ? maps.WaterToShipPoint(position) : position;
        public Vector3 WaterShipPoint(Vector3 local) => ShipToWaterPoint(ShipPoint(local));
        public Vector3 BoatAnchor => WaterShipPoint(boatAnchorLocal);
        public int CraftCapacity => craftingManager != null ? craftingManager.Capacity : GetUpgrade(UpgradeKind.Workbench) >= 3 ? 2 : 1;
        public int forceStormDay = -1;
        public bool StormActive => IsNight && (forceStormDay == day || day == 2 || day >= 4 && WeatherRoll(day, worldSeed) < tuning.randomStormChance);
        public static float WeatherRoll(int day, int seed) { unchecked { uint x=(uint)(seed ^ day*73856093); x ^= x>>16; x*=0x7feb352d; x ^= x>>15; return (x%10000)/10000f; } }
        public void SetDayClock(int requestedDay, float fraction, bool forceStorm = false)
        {
            bool dayChanged = day != Mathf.Max(1, requestedDay);
            day = previousDay = Mathf.Max(1, requestedDay); elapsed = (day-1)*tuning.daySeconds + Mathf.Clamp(fraction,0,.9999f)*tuning.daySeconds;
            forceStormDay = forceStorm ? day : -1; stormWarning=0; stormCountdown=.2f; ClearAreaStrikes();
            if (dayChanged || shadowWorldRefreshPending) RefreshWorldForDay(day);
            else if (!AnyCrewUnderwater && !HasLivingShadow) world?.RefreshDay(day);
            UpdateQuest(); Changed();
        }
        public bool LightEffective => searchlightOn && circuitRepaired && (infinitePower || power > 0);
        public bool IsElectricWinch => GetUpgrade(UpgradeKind.Rope) >= 3;
        public int QuestChapter => rescueVoyage ? 4 : radioStage >= 2 ? 3 : radioStage >= 1 ? 2 : 1;
        public bool IsShadowSpawnAllowed => tuning != null && elapsed > tuning.daySeconds * tuning.shadowCompleteDaysRequired && QuestChapter >= 3 && ship != null && ship.Position.x >= tuning.deepBoundary && IsNight;

        void Awake() { Current = this; }
        void Start() { if (autoStart && phase == SessionPhase.Menu) StartGame(); }
        void OnDestroy() { if (Current == this) Current = null; }

        public CraftingManager GetOrCreateCraftingManager()
        {
            if (craftingManager == null) craftingManager = GetComponent<CraftingManager>();
            if (craftingManager == null) craftingManager = gameObject.AddComponent<CraftingManager>();
            craftingManager.Initialize(this);
            if (legacyRecipes != null && legacyRecipes.Length > 0 && craftingManager.recipeDefinitions.Length == 0) craftingManager.SetRecipeDefinitions(legacyRecipes);
            if (legacyJobs != null && legacyJobs.Count > 0 && craftingManager.jobs.Count == 0) craftingManager.ReplaceJobs(legacyJobs);
            // Migration buffers never remain a second authority after the explicit handover.
            if (legacyRecipes != null && legacyRecipes.Length > 0) legacyRecipes = new RecipeDefinition[0];
            if (legacyJobs != null && legacyJobs.Count > 0) legacyJobs = new List<CraftJob>();
            return craftingManager;
        }

        public void Initialize()
        {
            if (maps == null) maps = GetComponent<GameMapPartition>();
            if (maps != null) maps.Initialize(this);
            if (tuning == null) tuning = ScriptableObject.CreateInstance<GameTuning>();
            Current = this;
            Time.timeScale = 1;
            bank.Clear();
            foreach (ResourceKind kind in Enum.GetValues(typeof(ResourceKind))) bank.Add(new ResourceAmount(kind, 0));
            foreach (ResourceAmount initial in initialResources) if (initial != null) { ResourceAmount slot = bank.Find(x => x.resource == initial.resource); if (slot != null) slot.amount += Mathf.Max(0, initial.amount); }
            upgradeLevels = new int[Enum.GetValues(typeof(UpgradeKind)).Length];
            GetOrCreateCraftingManager().ResetQueue();
            captainBuffs = new CrewBuffs(); diverBuffs = new CrewBuffs();
            hasRod = hasHarpoon = hasSpeargun = false;
            hullMax = tuning.baseHull; hull = Mathf.Clamp(tuning.initialHull, 0, hullMax); power = tuning.initialPower;
            filterRepaired = circuitRepaired = engineRepaired = engineEverRepaired = false;
            generatorInstalled = repairBenchInstalled = filterInstalled = false;
            engineRepairVersion = 0; generatorTier = 0; efficientFuelGenerator = false;
            infinitePower = searchlightOn = turretInstalled = turretOn = false;
            lowPowerSailingSeconds = 0; lastCrankAt = -100;
            repairAuthorized = new bool[Enum.GetValues(typeof(StationKind)).Length];
            ResetEngineFaults();
            elapsed = 0; day = previousDay = 1; radioStage = 0;
            rescueVoyage = false; rescueProgress = 0;
            stormWarning = 0; stormCountdown = UnityEngine.Random.Range(tuning.lightningIntervalMin, tuning.lightningIntervalMax);
            shadowSpawnedThisNight = shadowRepelled = shadowWorldRefreshPending = false; shadowHits = 0; crewWipeTimer = 0;
            reviveHeld = new bool[2]; reviveProgress = new float[2]; reviveTargets = new CrewActor[2];
            storyLog.Clear();
            ropeLength = tuning.ropeLengths[0];
            diverFainted = diverSprinting = diverHidden = false; diverBites = 0; diverInvulnerable = 0;
            winchActive = winchElectric = winchSignalAuthorized = false; winchJam = winchProgress = winchStartDistance = 0;
            winchCycleTime = 0; ResetWinchTarget(); winchAttemptedThisCycle = false; winchCargo = null; winchTargetKind = ""; winchFeedback = "";
            baitTime = 0; signalCount = 0; lastSignalAt = -100;
            birdCountdown = tuning.birdInterval;
            turretShotClock = turretPowerClock = 0;
            meleeCooldowns = new float[2];
            for (int i = 0; i < activeSpears.Length; i++) if (activeSpears[i] != null) Destroy(activeSpears[i].gameObject);
            activeSpears = new LifelineSpear[2];
            foreach (LifelineEnemy enemy in enemies.ToArray()) if (enemy != null) Destroy(enemy.gameObject);
            enemies.Clear();
            if (ship != null) { ship.session = this; ship.Initialize(this); }
            if (captain != null) captain.Initialize(this, CrewRole.Captain, ShipPoint(captainSpawnLocal));
            if (diver != null) diver.Initialize(this, CrewRole.Diver, ShipPoint(diverSpawnLocal));
            IgnoreCrewCollision(); ClearAreaStrikes(); turretTier=0; forceStormDay=-1;
            captainSprinting=false;captainBoardHeld=false;captainBoardProgress=0;winchOperatorRole=CrewRole.Captain;
            ResetLifelines();
            EnsureCaptainLifelineView();
            if (world != null) { world.session = this; world.tuning = tuning; world.Generate(worldSeed); }
            SpawnInitialSharks();
            phase = SessionPhase.Playing; initialized = true;
            Tell("失联第1天：打捞物资，修好电路与引擎。潜水员只能由船长绞盘接回。");
            RefreshEquipmentVisibility();
            UpdateQuest(); Changed(); SessionReady?.Invoke(false);
        }

        public void StartGame() { if (!initialized) Initialize(); else { phase = SessionPhase.Playing; Time.timeScale = 1; Changed(); } }
        public void Pause(bool paused) { if (phase == SessionPhase.Won || phase == SessionPhase.Lost) return; if(paused){captainBoardHeld=false;captainBoardProgress=0;captainSprinting=diverSprinting=false;}phase = paused ? SessionPhase.Paused : SessionPhase.Playing; Time.timeScale = paused ? 0 : 1; Changed(); }
        public void ReturnToMenu() { Time.timeScale = 1; phase = SessionPhase.Menu; winchActive = winchElectric = false; Changed(); }

        void Update()
        {
            StepSimulation(Time.deltaTime);
        }

        /// <summary>One authoritative simulation step; also usable by deterministic integration checks.</summary>
        public void StepSimulation(float dt)
        {
            if (phase != SessionPhase.Playing || tuning == null || dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            elapsed += dt;
            day = 1 + Mathf.FloorToInt(elapsed / Mathf.Max(1, tuning.daySeconds));
            captainBuffs.Tick(dt); diverBuffs.Tick(dt);
            for (int i = 0; i < meleeCooldowns.Length; i++) meleeCooldowns[i] = Mathf.Max(0, meleeCooldowns[i] - dt);
            baitTime = Mathf.Max(0, baitTime - dt);
            UpdateCrafts(dt); UpdateOxygen(dt); UpdateCaptainOxygen(dt); UpdateCaptainBoard(dt); UpdateWinch(dt); UpdateElectricity(dt);
            UpdateWeather(dt); UpdateAreaStrikes(dt); UpdateEncounters(dt); UpdateRecovery(dt); UpdateRescue(dt);
            bool dayAdvanced = day != previousDay;
            if (dayAdvanced)
            {
                RefreshWorldForDay(day);
                previousDay = day;
                if (day == 2) Tell("第2天：天气正在恶化。躲入船舱，雷击会使船员倒地。");
                else if (day == 3) Tell("第3天：声呐下方出现巨影。准备鱼叉、射矛枪或甲板炮塔。");
                else if (day == 4) Tell("第4天：搜救频道开启。完成三级电台，向东航行接回幸存者。");
                else Tell("新的一天：继续发送求救信号，合作守住这条船。");
            }
            else if (shadowWorldRefreshPending && !HasLivingShadow && !AnyCrewUnderwater)
                RefreshWorldForDay(day);
            UpdateQuest();
            notifyClock -= dt;
            if (notifyClock <= 0) { notifyClock = .15f; Changed(); }
            if (hull <= 0) EndSession(false, "船体失去浮力。救援未能抵达。");
            if (dayAdvanced && phase == SessionPhase.Playing) DayReady?.Invoke(day);
        }

        void FixedUpdate() { if(phase==SessionPhase.Playing)foreach(var actor in Crew)if(actor!=null&&!actor.onboard)ApplyDiverLeash(actor); }

        public void HandleCommand(CrewActor actor, CrewCommand command)
        {
            if (actor == null) return;
            int role = (int)actor.role;
            bool interacting = command.interactHeld || command.interact;
            if (phase == SessionPhase.Playing && !command.pause && interacting && !IsHidden(actor) && TryReviveTeammate(actor))
            {
                reviveHeld[role] = true;
                world?.TryCatchRoomFish(actor, false);
                if (actor == captain) { captainBoardHeld = false; captainBoardProgress = 0; }
                return;
            }
            bool capturedBeforeEvent = actor.controlsCaptured;
            CommandReceived?.Invoke(actor, command);
            reviveHeld[role] = false;
            if (phase != SessionPhase.Playing || !interacting || actor.onboard || capturedBeforeEvent || actor.controlsCaptured ||
                IsHidden(actor) || IsIncapacitated(actor) || command.hide)
                world?.TryCatchRoomFish(actor, false);
            if (phase != SessionPhase.Playing) return;
            if(IsHidden(actor))
            {
                if (command.hide || command.interact && !command.hideSeparate)
                    world?.TryHide(actor);
                return;
            }
            if (interacting && !actor.onboard && !capturedBeforeEvent && !actor.controlsCaptured && !command.hide &&
                world != null && world.TryCatchRoomFish(actor, true))
            {
                if (actor == captain) { captainBoardHeld = false; captainBoardProgress = 0; captainSprinting = command.sprint; }
                else if (actor == diver) diverSprinting = command.sprint;
                return;
            }
            if(actor==captain)
            {
                captainSprinting=command.sprint;
                captainBoardHeld=!actor.onboard&&!IsIncapacitated(actor)&&!actor.controlsCaptured
                    &&(command.interactHeld||command.interact)&&!HasNearbyCaptainWaterInteraction(actor, !command.hideSeparate)&&command.move.sqrMagnitude<.01f&&!command.melee&&!command.fire&&Vector3.Distance(actor.Position,BoatAnchor)<=3;
                if(captainBoardHeld)return;
                captainBoardProgress=0;
            }
            if (IsActorInputBlocked(actor)) return;
            if(actor==diver)diverSprinting=command.sprint;
            if (command.hide && !actor.onboard && world != null && world.TryHide(actor)) return;
            if (capturedBeforeEvent || actor.controlsCaptured) return;
            if (command.melee) MeleeAttack(actor);
            if (command.fire) FireSpear(actor);
            if (!actor.onboard)
            {
                if (command.signal) SignalWinch(actor);
                if (command.bait) ThrowBait(actor);
                if ((command.collect || actor == captain && (command.interactHeld || command.interact)) && world != null) world.TryCollect(actor);
                if (command.interact)
                {
                    if (world != null && world.TryLoadCrate(actor)) return;
                    if (world != null && world.TryInteractTrapdoor(actor)) return;
                    if (!command.hideSeparate && world != null && world.TryHide(actor)) return;
                    if (actor == diver && Vector3.Distance(actor.Position, BoatAnchor) < 3) Tell("已经抵达船底。请船长打开绞盘接回；潜水员无法自行登船。", actor);
                }
                return;
            }
            if (!command.interact || !actor.onboard) return;
            StationAnchor station = actor.NearbyStation;
            if (station == null || !IsStationUnlocked(station.kind)) return;
            if (station.kind == StationKind.DivePreparation) { BeginDive(actor); return; }
            if (station.kind == StationKind.Helm) { TryInteractHelm(actor); return; }
            StationRequested?.Invoke(station.kind, actor);
        }

        public bool BeginDive()=>BeginDive(diver);
        public bool BeginDive(CrewActor actor)
        {
            if(phase!=SessionPhase.Playing||IsIncapacitated(actor)||!actor.onboard)return false;
            if(ship!=null&&ship.helmOperator==actor)ship.SetHelm(actor);
            // Entering water is an explicit new operation. A powered rescue left running for
            // the fainted teammate must not immediately bank the new swimmer at the anchor.
            winchActive=winchElectric=winchSignalAuthorized=false;
            winchCargo=null;winchTargetKind="";signalCount=0;
            actor.controlsCaptured=false;
            actor.SetHidden(false);actor.fainted=false;actor.oxygenMax=GetOxygenMax(actor);actor.oxygen=actor.oxygenMax;
            if(actor==diver){diverBites=0;diverInvulnerable=0;diverFainted=diverHidden=false;}
            else{captainBoardHeld=false;captainBoardProgress=0;}
            actor.SetOnboard(false,BoatAnchor+Vector3.down*.5f);
            GetCrewLifeline(actor).ResetRoute();winchStartDistance=0;winchProgress=0;
            EnsureCaptainLifelineView();Tell(GameNames.Role(actor.role)+"入水。");Changed();return true;
        }

        public int GetResource(ResourceKind kind)
        {
            int result = 0; foreach (ResourceAmount item in bank) if (item != null && item.resource == kind) result += item.amount; return result;
        }

        public void AddResource(ResourceKind kind, int amount)
        {
            if (amount <= 0) return;
            ResourceAmount slot = bank.Find(x => x != null && x.resource == kind);
            if (slot == null) bank.Add(new ResourceAmount(kind, amount)); else slot.amount += amount;
            Changed();
        }

        public bool Spend(ResourceAmount[] cost)
        {
            if (cost == null) return true;
            var required = new Dictionary<ResourceKind, int>();
            foreach (ResourceAmount item in cost)
            {
                if (item == null || item.amount < 0) return false;
                if (!required.ContainsKey(item.resource)) required[item.resource] = 0;
                required[item.resource] += item.amount;
            }
            foreach (var pair in required) if (GetResource(pair.Key) < pair.Value) return false;
            foreach (var pair in required)
            {
                int left = pair.Value;
                foreach (ResourceAmount item in bank)
                {
                    if (item == null || item.resource != pair.Key) continue;
                    int take = Mathf.Min(item.amount, left); item.amount -= take; left -= take;
                    if (left <= 0) break;
                }
            }
            Changed(); return true;
        }

        public int GetUpgrade(UpgradeKind kind) => upgradeLevels != null && (int)kind < upgradeLevels.Length ? upgradeLevels[(int)kind] : 0;
        public static int UpgradeLevelLimit(UpgradeKind kind) => kind == UpgradeKind.Rope ? 5 : kind == UpgradeKind.Oxygen ? 4 : kind == UpgradeKind.Hull || kind == UpgradeKind.Sail ? 2 : kind == UpgradeKind.Searchlight ? 2 : 3;
        public int GetMaxUpgrade(UpgradeKind kind) => UpgradeLevelLimit(kind);
        public int MaxUpgradeLevel(UpgradeKind kind) => GetMaxUpgrade(kind);
        public ResourceAmount[] GetUpgradeCost(UpgradeKind kind)
        {
            if (GetUpgrade(kind) >= GetMaxUpgrade(kind)) return new ResourceAmount[0];
            switch (kind)
            {
                case UpgradeKind.Rope: return GetUpgrade(kind) >= 3 ? longRopeUpgradeCost : ropeUpgradeCost;
                case UpgradeKind.Oxygen: return oxygenUpgradeCost;
                case UpgradeKind.Hull: return hullUpgradeCost;
                case UpgradeKind.Sail: return sailUpgradeCost;
                case UpgradeKind.Searchlight: return lightUpgradeCost;
                case UpgradeKind.Backpack: return backpackUpgradeCost;
                case UpgradeKind.DivingSuit: return divingSuitUpgradeCost;
                default: return workbenchUpgradeCost;
            }
        }

        public bool TryUpgrade(UpgradeKind kind)
        {
            if (phase != SessionPhase.Playing || GetUpgrade(kind) >= GetMaxUpgrade(kind)) return false;
            if (upgradeLevels.Length < Enum.GetValues(typeof(UpgradeKind)).Length) Array.Resize(ref upgradeLevels, Enum.GetValues(typeof(UpgradeKind)).Length);
            if (!Spend(GetUpgradeCost(kind))) { Tell("升级材料不足。先打捞并制作铁板与木板。"); return false; }
            float oldMax = hullMax;
            upgradeLevels[(int)kind]++;
            if (kind == UpgradeKind.Rope) ropeLength = tuning.ropeLengths[Mathf.Min(GetUpgrade(kind), tuning.ropeLengths.Length - 1)];
            if (kind == UpgradeKind.Hull) { hullMax = tuning.hullLevels[Mathf.Min(GetUpgrade(kind), tuning.hullLevels.Length - 1)] * (infinitePower ? tuning.eelHullMultiplier : 1); hull += hullMax - oldMax; }
            if (kind == UpgradeKind.Oxygen && diver != null) { float delta = GetOxygenMax() - diver.oxygenMax; diver.oxygenMax = GetOxygenMax(); diver.oxygen += Mathf.Max(0, delta); }
            RefreshEquipmentVisibility();
            Tell("升级完成：" + kind + " T" + GetUpgrade(kind)); Changed(); return true;
        }

        public CrewBuffs Buffs(CrewActor actor) => actor == captain ? captainBuffs : diverBuffs;
        public int GetBagCapacity(CrewActor actor) => actor == captain ? tuning != null ? tuning.captainBagCapacity : 3 : tuning == null ? 8 : tuning.bagCapacity + (actor == diver ? tuning.backpackSlotsByLevel[Mathf.Clamp(GetUpgrade(UpgradeKind.Backpack), 0, tuning.backpackSlotsByLevel.Length-1)] : 0) + (Buffs(actor).capacity > 0 ? 1 : 0);
        public float GetCollectSpeedMultiplier(CrewActor actor) => actor == diver ? 1 + tuning.backpackCollectionBonus * (GetUpgrade(UpgradeKind.Backpack) >= 3 ? 2 : GetUpgrade(UpgradeKind.Backpack) >= 1 ? 1 : 0) : 1;
        public int GetMaxDiverBites() => GetUpgrade(UpgradeKind.DivingSuit) >= 2 ? tuning.suitT2HitCapacity : GetUpgrade(UpgradeKind.DivingSuit) >= 1 ? tuning.upgradedDiverHitCapacity : tuning.baseDiverHitCapacity;
        public int DiverHitCapacity => GetMaxDiverBites();
        public float GetMoveMultiplier(CrewActor actor) => (actor != null && !actor.onboard && filterRepaired ? filterMoveMultiplier : 1) * (Buffs(actor).move > 0 ? 1 + tuning.fishMoveBonus : 1);
        public float GetBoatSpeedMultiplier() => (AnyCrewUnderwater ? tuning.diveBoatMultiplier : 1) * (engineRepaired ? repairedEngineSpeedMultiplier : 1) * (GetUpgrade(UpgradeKind.Sail) >= 1 ? 1 + sailSpeedBonus : 1) * (infinitePower ? tuning.eelSpeedMultiplier : 1);
        public float GetOxygenMax() => tuning.oxygenLevels[Mathf.Min(GetUpgrade(UpgradeKind.Oxygen), tuning.oxygenLevels.Length - 1)] * (diverBuffs.oxygen > 0 ? 1 + tuning.fishOxygenBonus : 1);
        public bool IsActorInputBlocked(CrewActor actor) => actor == null || phase != SessionPhase.Playing || actor.downed || IsFainted(actor) || IsHidden(actor) || (actor==captain&&captainBoardHeld) || (reviveTargets[(int)actor.role] != null && reviveHeld[(int)actor.role]);
        public Vector3 ShipPoint(Vector3 local) => ship != null ? ship.LocalToWorld(local) : local;

        public void Tell(string text, CrewActor audience = null)
        {
            StatusMessage = text;
            StatusAudienceRole = audience != null ? (int)audience.role : -1;
            if (storyLog.Count == 0 || storyLog[storyLog.Count - 1] != text) { storyLog.Add(text); if (storyLog.Count > 60) storyLog.RemoveAt(0); }
            Message?.Invoke(text);
        }
        public void Changed() => StateChanged?.Invoke();
        public void DamageHull(float amount) { if (phase != SessionPhase.Playing) return; hull = Mathf.Max(0, hull - Mathf.Max(0, amount)); Changed(); }

        public void EndSession(bool won, string text)
        {
            if (phase == SessionPhase.Won || phase == SessionPhase.Lost) return;
            phase = won ? SessionPhase.Won : SessionPhase.Lost;
            winchActive = winchElectric = false; turretOn = false;
            if (ship != null && ship.helmOperator != null) ship.SetHelm(ship.helmOperator);
            Tell(text); Changed();
        }

        void UpdateQuest()
        {
            if (!circuitRepaired) QuestText = "打捞 → 船长绞盘接回入库 → 制作铁板/纤维束 → 修复电路";
            else if (radioStage < 3) QuestText = "制作求救天线 → 接收器 → 信号放大器（" + radioStage + "/3）；修复引擎可提升航速";
            else if (!rescueVoyage) QuestText = "总控启动救援航程；修好引擎并备足燃油，接回潜水员后向东航行";
            else if (day < 4) QuestText = "等待第4日救援频道，先修船备物资；当前第" + day + "日";
            else if (ship != null && ship.transform.position.x < rescueArrivalX) QuestText = "救援航程：向东航行至信号浮标（" + Mathf.RoundToInt(ship.transform.position.x) + "/" + rescueArrivalX + "m）";
            else QuestText = "船长操作绞盘接回幸存者：" + Mathf.RoundToInt(rescueProgress * 100) + "%";
        }
    }
}
