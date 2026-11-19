using UnityEngine;

namespace Lifeline
{
    [CreateAssetMenu(fileName = "GameTuning", menuName = "LifeLine/Game Tuning")]
    public class GameTuning : ScriptableObject
    {
        [Header("角色 / Crew movement (metres, seconds)")]
        [Min(.1f)] public float playerShipSpeed = 5f;
        [Min(.1f)] public float playerSwimSpeed = 2.1f;
        [Min(1)] public float sprintMultiplier = 1.6f;
        public float jumpSpeed = 7f;
        public float gravity = 20f;
        [Min(1)] public int bagCapacity = 8;
        [Min(1), Tooltip("Captain-only fixed capacity; unaffected by backpack upgrades or fish capacity buffs.")] public int captainBagCapacity = 3;
        public float baseOxygen = 50f;
        public float sprintOxygenMultiplier = 2f;
        public float reviveDistance = 2.5f;
        [Min(.1f)] public float reviveSeconds = 4f;
        [Tooltip("Hit units: base 2, suit T1 3, T2/T3 4. T3 adds immunity.")]
        [Min(1)] public int baseDiverHitCapacity = 2;
        [Min(1)] public int upgradedDiverHitCapacity = 3;
        [Range(0, 1)] public float upgradedSuitImmunity = .25f;
        [Range(0, 1)] public float backpackCollectionBonus = .25f;
        public float collectSeconds = .72f;
        public float cabinetSeconds = 1.5f;
        public int crateCapacity = 3;
        public int suitT2HitCapacity = 4;
        public int[] backpackSlotsByLevel = { 0, 2, 6, 10 };
        public float upgradedFuelPower = 20f;
        public float sharkDamage = 2f, shadowDamage = 2f, waterShotHitDamage = 1f;
        public float lightningIntervalDayReduction = .5f, lightningMinFloor = 6f, lightningMaxFloor = 10f;
        public float lightningWarningDayReduction = .05f, lightningWarningFloor = 1.2f;
        public float birdMinimumInterval = 10f, birdIntervalDayReduction = 1f;
        public float birdExtraOneChance = .2f, birdExtraTwoChance = .1f, birdExtraDailyChance = .01f;
        public float birdExtraOneCap = .5f, birdExtraTwoCap = .25f;
        public float birdInterruptChance = .25f, armoredBirdInterruptChance = .5f;
        [Tooltip("Base / T1 / T2 / T3 / T4 / T5 rope length in metres. T3 retains electric recovery; T4 adds 20% recovery efficiency.")]
        public float[] ropeLengths = { 38f, 54f, 74f, 100f, 150f, 200f };

        [Header("船舶与动力 / Ship")]
        public float boatSpeed = 3f;
        [Range(.01f, 1)] public float diveBoatMultiplier = .3f;
        public float baseHull = 100f;
        [Min(0), Tooltip("Starting current durability; baseHull remains the upgradeable maximum.")] public float initialHull = 70f;
        public float initialPower = 0f;
        public float maxPower = 100f;
        public float routePowerCost = 200f;
        public float routeDistance = 200f;
        public float fuelPower = 15f;
        public float handPowerPerStroke = 1f;
        public float generatorStrokeInterval = 3f;
        public float eelHullMultiplier = 1.3f;
        public float eelSpeedMultiplier = 1.2f;
        public float[] hullLevels = { 100f, 150f, 220f };
        [Tooltip("Diver oxygen in seconds: base / T1 / T2 / T3 / T4. Captain always uses baseOxygen.")]
        public float[] oxygenLevels = { 50f, 90f, 135f, 175f, 250f };

        [Header("昼夜 / Day-night")]
        public float daySeconds = 200f;
        [Range(0, 1)] public float dayFraction = .703f;
        [Range(0, 1)] public float duskFraction = .117f;
        public float lightningIntervalMin = 12f;
        public float lightningIntervalMax = 18f;
        public float lightningWarning = 2f;
        [Range(0, 1)] public float lightningEngineRisk = .05f;
        [Range(0, 1)] public float lowPowerEngineRisk = .05f;
        public float lowPowerRiskInterval = 5f;
        public float lowPowerThreshold = 10f;
        [Tooltip("Signed drift is sin(elapsed*frequency)*speed; ShipMotor is the only movement owner.")]
        public float stormDriftSpeed = .4888889f;
        public float stormDriftFrequency = .19f;
        [Range(0, 1)] public float activeSteeringDriftMultiplier = .15f;
        [Range(0, 1)] public float sailT2DriftMultiplier = .4f;

        [Header("制作 / Automatic crafting")]
        public float craftSeconds = 40f;
        [Tooltip("Compatibility value for T2; prefer craftSecondsByLevel/GetCraftDuration.")]
        public float upgradedCraftSeconds = 20f;
        public float[] craftSecondsByLevel = { 40f, 30f, 20f, 15f };
        public float craftQteBonus = 3f;
        public float craftQteHalfWindow = .16f;
        public float workbenchWindowMultiplier = 1.3f;
        public float craftCueInterval = 1f;
        public int firstEngineRepairPlates = 2;
        public int engineFaultCount = 4;

        [Header("绞盘 / Winch")]
        public float winchCycle = 1.7f;
        [Range(0, 1)] public float winchCenter = .69f;
        [Range(.01f, 1)] public float winchOriginalWidth = .1f;
        public float winchBaseBonus = .1f;
        public float winchT2Bonus = .1f;
        public float winchPassiveSpeed = .14f;
        public float winchGoodDistance = 1.78f;
        public float winchBadDistance = .89f;
        public float winchElectricSpeed = 3.33f;
        [Tooltip("T4 positive recovery multiplier bonus. Applies before sharing between attached targets; failed calibration slip is unchanged.")]
        [Min(0)] public float winchT4RecoveryBonus = .2f;
        [Min(1)] public int electricWinchSignalCount = 3;
        [Min(.1f)] public float electricWinchSignalWindow = 1.1f;

        [Header("敌人与武器 / Encounters")]
        public float sharkDetectDelay = .2f;
        public float sharkWindup = .68f;
        public float sharkPursuitSeconds = 4f;
        public float sharkPatrolSpeed = 1.25f;
        [Tooltip("Metres. Initial diver length is 1.8m; authored art scaling never changes detection.")]
        public float sharkDetectionRange = 3.6f;
        public float sharkNightDetectionRange = 1.8f;
        public float sharkChaseSpeed = 3.36f;
        public float sharkBiteCooldown = 3.6f;
        public float sharkReacquireDelay = 3f;
        [Min(0)] public float sharkMinimumDepth = 25f;
        [Min(0)] public int maximumSharks = 8;
        [Min(1)] public int mutationStartDay = 3;
        [Range(0, 1)] public float armoredBirdChance = .35f;
        [Min(1)] public float sharkHealth = 2, whiteSharkHealth = 3, birdHealth = 1, armoredBirdHealth = 2;
        [Min(.1f)] public float sharkHitRetreatSeconds = 3f;
        [Header("城区食人鱼 / Middle-depth city schools")]
        [Range(0, 6)] public int maximumPiranhas = 6;
        [Range(0, 1)] public float piranhaSpawnChance = .6f;
        public Vector2 piranhaDepths = new Vector2(20, 35);
        public float piranhaDamage = 1f;
        public float piranhaScaleMultiplier = .5f;
        public float piranhaAttackDistance = .84f;
        public float piranhaWarningDistance = 1.54f;
        public float piranhaWindup = .544f;
        public float piranhaChaseSpeed = 3.696f;
        public float piranhaPatrolSpeed = 1.375f;
        [Header("洞穴守卫与鱼获 / Cave guards and separate fish loot")]
        [Range(1, 3)] public int caveGuardMaximum = 2;
        [Range(0, 1)] public float extraCaveGuardChance = .75f;
        [Range(0, 1)] public float underwaterFishChance = .12f;
        [Min(0)] public int minimumFishPerArea = 1;
        public float shadowWindup = 1.76f;
        public int shadowHitsRequired = 20;
        public float shadowSpeedMultiplier = .9f;
        public float shadowSweepHullDamage = 10f;
        public Vector2 shadowMeleeHullDamage = new Vector2(5,10);
        public float shadowBiteRadiusMultiplier = 1.5f, shadowDashRadiusMultiplier = .7f;
        public float shadowDashSpeedMultiplier = 3f, shadowDashSeconds = .45f;
        public float shadowWaterShotDamage = 5f, shadowWaterShotWarning = 2.2f;
        public float lightningRadius = 1.4f, randomStormChance = .10f, turretSharkDepth = 20f;
        public float deepSharkSpawnBonus = .15f;
        public float shadowAttackInterval = 6f;
        [Tooltip("Strictly more than this many complete calendar days, AND chapter 3/deep water/night.")]
        [Min(0)] public int shadowCompleteDaysRequired = 3;
        public float turretPowerInterval = 3f;
        public float turretShotInterval = 1.5f;
        public float birdHullDamage = 2f;
        public float birdInterval = 15f;
        [Range(0, 1)] public float upgradedLightBirdRate = .6f;
        public float meleeCooldown = .6f;
        public float meleeRange = 3f;
        public float spearSpeed = 10f;
        public float spearLifetime = 3.5f;
        public float baitSeconds = 8f;

        [Header("资源与钓鱼 / World and fishing")]
        [Tooltip("Depth-band room occupancy: 9/15=60%, 21/28=75%, 40/45≈89%. Resource type depends on sea area.")]
        public int shallowLootCount = 9;
        public int middleLootCount = 21;
        public int deepLootCount = 40;
        public float shallowBoundary = 53.333332f;
        public float deepBoundary = 124.44444f;
        [Range(0, 1)] public float caveChance = .05f;
        [Range(0, 1)] public float eelCaveChance = .05f;
        [Range(0, 1)] public float deepEventChancePerDayAfterThree = .02f;
        [Range(0, 1)] public float deepEventChanceCap = .15f;
        [Range(0, 1)] public float qualityFishShallow = .1f;
        [Range(0, 1)] public float qualityFishMiddle = .2f;
        [Range(0, 1)] public float qualityFishDeep = .3f;
        public float fishBuffSeconds = 120f;
        public float fishMoveBonus = .1f;
        public float fishQteBonus = .2f;
        public float fishOxygenBonus = .1f;
        public float fishHullHeal = .1f;
        public float fishAttackBonus = .1f;
    }
}
