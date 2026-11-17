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
        public float baseOxygen = 40f;
        public float sprintOxygenMultiplier = 2f;
        public float reviveDistance = 2.5f;
        [Min(.1f)] public float reviveSeconds = 5f;
        [Tooltip("The first effective hit exhausts the basic suit; T1 survives until its second hit.")]
        [Min(1)] public int baseDiverHitCapacity = 1;
        [Min(1)] public int upgradedDiverHitCapacity = 2;
        [Range(0, 1)] public float upgradedSuitImmunity = .25f;
        [Range(0, 1)] public float backpackCollectionBonus = .25f;
        public float collectSeconds = .72f;
        public float cabinetSeconds = 1.5f;
        public int crateCapacity = 3;
        [Tooltip("T3 is 74m so the new 50m buildings remain physically reachable.")]
        public float[] ropeLengths = { 28f, 44f, 54f, 74f };

        [Header("船舶与动力 / Ship")]
        public float boatSpeed = 3.4f;
        [Range(.01f, 1)] public float diveBoatMultiplier = .28f;
        public float baseHull = 100f;
        public float initialPower = 20f;
        public float maxPower = 100f;
        public float routePowerCost = 80f;
        public float routeDistance = 90f;
        public float fuelPower = 25f;
        public float handPowerPerStroke = 1f;
        public float generatorStrokeInterval = 3f;
        public float eelHullMultiplier = 1.3f;
        public float eelSpeedMultiplier = 1.2f;
        public float[] hullLevels = { 100f, 150f, 220f };
        public float[] oxygenLevels = { 40f, 70f, 115f, 145f };

        [Header("昼夜 / Day-night")]
        public float daySeconds = 200f;
        [Range(0, 1)] public float dayFraction = .703f;
        [Range(0, 1)] public float duskFraction = .117f;
        public float lightningIntervalMin = 12f;
        public float lightningIntervalMax = 18f;
        public float lightningWarning = 2.2f;
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
        public float upgradedCraftSeconds = 25f;
        public float[] craftSecondsByLevel = { 40f, 35f, 25f, 20f };
        public float craftQteBonus = 3f;
        public float craftQteHalfWindow = .16f;
        public float workbenchWindowMultiplier = 1.3f;
        public float craftCueInterval = 1f;
        public int firstEngineRepairPlates = 3;
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
        public float piranhaDamage = .5f;
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
        public float shallowBoundary = 24f;
        public float deepBoundary = 56f;
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
