using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    public enum SaveReason { Automatic, Manual }
    [Serializable] public sealed class SaveSummary
    {
        public int day; public float elapsed; public string savedUtc; public SaveReason reason;
    }
    [Serializable] public sealed class GameSaveData
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        // Version 0 saves predate the separate ship map. Their world coordinates already
        // match the logical water map. Version 1 normalizes ship-map positions on write.
        public int coordinateVersion;
        public int balanceVersion;
        public int equipmentProgressionVersion;
        public bool hasTutorial;
        public TutorialSave tutorial;
        public float routeDistance;
        public float cycleSeconds, daylightSeconds, duskSeconds;
        public AreaStrike[] areaStrikes = Array.Empty<AreaStrike>();
        public string savedUtc;
        public SaveReason reason;
        public int lastAutoSavedDay;
        public SessionSaveState state;
        public CrewSaveState captain, diver;
        public Vector3 shipPosition;
        public Vector3[] captainRopeContacts=Array.Empty<Vector3>();
        public Vector3[] diverRopeContacts = Array.Empty<Vector3>();
        public Vector3[] cargoRopeContacts = Array.Empty<Vector3>();
        public CraftSaveState[] jobs = Array.Empty<CraftSaveState>();
        public int nextJobId = 1;
        public WorldSaveState world = new WorldSaveState();
        public EnemySaveState[] enemies = Array.Empty<EnemySaveState>();
        public SpearSaveState[] spears = Array.Empty<SpearSaveState>();
        public RepairUISaveState repairUI = new RepairUISaveState();
    }
    // Explicit whitelist: tuning, costs, prefab references, translated UI and input objects are never copied.
    [Serializable] public sealed class SessionSaveState
    {
        public List<ResourceAmount> bank;
        public int[] upgradeLevels;
        public CrewBuffs captainBuffs, diverBuffs;
        public bool hasRod, hasHarpoon, hasSpeargun;
        public bool generatorInstalled, repairBenchInstalled, filterInstalled;
        public float hull, hullMax, power;
        public bool filterRepaired, circuitRepaired, engineRepaired, engineEverRepaired;
        public int engineRepairVersion, generatorTier, turretTier;
        public bool efficientFuelGenerator;
        public bool infinitePower, searchlightOn, turretInstalled, turretOn;
        public float lastCrankAt, lowPowerSailingSeconds;
        public float[] engineFaultClean;
        public bool[] engineFaultFixed;
        public int[] engineFaultTypes;
        public bool[] repairAuthorized;
        public float ropeLength;
        public bool diverFainted, diverHidden;
        public float diverBites;
        public float diverInvulnerable;
        public bool winchElectric, winchSignalAuthorized;
        public CrewRole winchOperatorRole;
        public float winchProgress, winchCycleTime, winchJam, winchStartDistance;
        public bool winchAttemptedThisCycle;
        public float winchTargetCenter, winchCueCooldown;
        public bool winchCenterInitialized;
        public string winchTargetKind;
        public string winchCargoId;
        public float lastSignalAt;
        public int signalCount;
        public int worldSeed;
        public float elapsed;
        public int day, radioStage;
        public bool rescueVoyage;
        public float rescueProgress, stormCountdown, stormWarning;
        public CrewRole lightningTarget;
        public bool shadowSpawnedThisNight, shadowRepelled;
        public int shadowHits;
        public float crewWipeTimer, birdCountdown, turretShotClock, turretPowerClock, baitTime;
        public Vector3 baitPosition;
        public float[] meleeCooldowns;
        public string StatusMessage;
        public List<string> storyLog;
    }
    [Serializable] public sealed class CrewSaveState
    {
        public CrewRole role;
        public bool onboard, downed, hidden, fainted;
        public Vector3 hidingPosition;
        public Vector3 position, velocity;
        public Vector2 facing;
        public float health, maxHealth, oxygen, oxygenMax;
        public List<ResourceKind> backpack;
    }
    [Serializable] public sealed class CraftSaveState
    {
        public int id; public string recipeId; public CrewRole owner;
        public float elapsed, progress, duration, nextCue, repairApplied;
        public int noteDirection, hits, misses;
        public bool attempted;
    }
    [Serializable] public sealed class WorldSaveState
    {
        public bool present;
        public int activeDay, generatedSeed;
        public int forceCaveDay=-1;
        public int forceFilterDay=-1;
        public PickupSaveState[] pickups = Array.Empty<PickupSaveState>();
        public GateSaveState[] gates = Array.Empty<GateSaveState>();
    }
    [Serializable] public sealed class PickupSaveState
    {
        public string id;
        public bool dynamic, isCrate, isCabinet, attached, consumed, persistAcrossDays, isLostCargo;
        public ResourceKind resource;
        public int amount, capacity, mazeIndex, mazeCell;
        public Vector3 position;
        public List<ResourceKind> contents;
        public Vector3[] ropeContacts = Array.Empty<Vector3>();
    }
    [Serializable] public sealed class GateSaveState { public string id; public bool open, destroyed; }
    [Serializable] public sealed class EnemySaveState
    {
        public string id; public EnemyKind kind; public EnemyState state;
        public Vector3 position, home, spawnPoint, targetPoint, facing;
        public float health, maxHealth, stateTime, pursuitTime, attackCooldown, reacquireTime;
        public int shadowHitCount, shadowAttackIndex, shadowPhase; public bool variantApplied;
        public float shadowAttackClock; public Vector3 shadowAim;
        public bool caveGuard, damagedRetreat, birdTargetsCrew;
        public CrewRole birdTargetRole;
        public Vector3 fleeDirection;
    }
    [Serializable] public sealed class SpearSaveState
    {
        public CrewRole owner;
        public Vector3 position, direction;
        public bool turretShot, ignoreShipColliders, returning, lodged, hasHit;
        public bool onShipMap;
        public float damage, age, travelled, maximumRange=10f;
        public string targetId, parentEnemyId;
    }
    [Serializable] public sealed class RepairUISaveState
    {
        public bool present;
        public PipeRepairPuzzle filterPuzzle;
        public bool[] circuitConnected;
    }
}
