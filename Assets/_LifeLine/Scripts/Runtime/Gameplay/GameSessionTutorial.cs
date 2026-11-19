using System;
using UnityEngine;

namespace Lifeline
{
    /// <summary>One-shot tutorial milestones that drive step cards from real gameplay events.</summary>
    public enum TutorialMilestone
    {
        FirstDive = 0,       // Diver/captain enters water for the first time.
        FirstCargoBanked = 1,// Backpack contents first deposited into the shared bank.
        FirstCraft = 2,      // First crafting job finished.
        CircuitRepaired = 3, // Circuit repair puzzle completed.
        ManualHint = 4,      // "Press H for facility manual" card, shown after TotalControl is cleaned.
        Count = 5
    }

    /// <summary>
    /// Opening tutorial: all stations begin as ruins. Cleaning a ruin is free, takes a
    /// 3-second hold of the interact key, unlocks the station and reveals its function card.
    /// Cleaning is separate from the existing device repair/puzzle rules (which still apply
    /// to circuit / filter / engine so their effects are unchanged).
    /// </summary>
    public partial class GameSession
    {
        public const float StationRestoreSeconds = 1f;
        public const int StationCount = 13; // StationKind has 13 members.

        public event Action<StationKind, CrewRole> StationRestoredByRole; // raised with actor role (drives player-specific hint card)
        public event Action<StationKind> StationRestored;      // raised when a ruin is cleaned (drives UI + hint card)
        public event Action<TutorialMilestone> MilestoneReached; // raised once per milestone (drives procedural step cards)
        public Func<CrewRole, bool> ConsumeTutorialInteract;   // delegate from TutorialUIController to consume interact edge

        CrewActor restoreActor;
        StationKind restoreStation;
        float restoreProgress;
        bool restoring;
        bool restoreInputLatch; // swallow input until interact is released after a clean completes
        bool restoreHoldCaptain, restoreHoldDiver; // cached interact-held from the last HandleCommand per actor

        /// <summary>True while holding interact would be consumed by a just-finished clean. Call from HandleCommand.</summary>
        internal bool ConsumeRestoreLatch(bool interactHeld)
        {
            if (!restoreInputLatch) return false;
            if (!interactHeld) { restoreInputLatch = false; return false; }
            return true; // still held: swallow this frame
        }

        internal void CacheInteractHeld(CrewActor actor, bool held)
        {
            if (actor == captain) restoreHoldCaptain = held;
            else if (actor == diver) restoreHoldDiver = held;
        }
        bool RestoreHoldOf(CrewActor actor) => actor == captain ? restoreHoldCaptain : actor == diver ? restoreHoldDiver : false;

        // Legacy saves made before the tutorial keep every station usable.
        public bool TutorialActive => tutorialVersion >= 1;

        /// <summary>Test helper: disable ruin gating so gameplay tests keep their original assumptions.</summary>
        public void DisableTutorialForTests()
        {
            tutorialVersion = 0;
            if (stationUnlocked == null || stationUnlocked.Length != StationCount) stationUnlocked = new bool[StationCount];
            for (int i = 0; i < StationCount; i++) stationUnlocked[i] = true;
        }
        /// <summary>Ruin gate only (independent from equipment-progression unlock in GameSessionEquipment).</summary>
        public bool IsStationRestored(StationKind kind)
        {
            int i = (int)kind;
            if (!TutorialActive) return true;
            return stationUnlocked != null && i >= 0 && i < stationUnlocked.Length && stationUnlocked[i];
        }
        public bool IsStationRuin(StationKind kind) => !IsStationRestored(kind);

        /// <summary>
        /// Unlocking a gated device skips the 3-second ruin clean: mark that station restored, refresh
        /// its visuals and fire the same hint-card events as a manual clean. Deliberately does NOT touch
        /// the active-clean state or input latch (unlike CompleteRestoration), so an unlock landing
        /// mid-clean cannot cancel what the player is doing. Guards keep locked devices ruined and make
        /// repeat calls harmless. Called from RefreshEquipmentVisibility for every equipment-gated device.
        /// </summary>
        public void AutoRestoreStationOnUnlock(StationKind kind, CrewRole role = CrewRole.Captain)
        {
            if (!TutorialActive) return;
            switch (kind)
            {
                case StationKind.Fishing:
                case StationKind.Searchlight:
                case StationKind.Turret:
                case StationKind.Generator:
                case StationKind.HullRepair:
                case StationKind.Filter:
                    break;
                default: return; // only the six equipment-gated devices
            }
            int i = (int)kind;
            if (stationUnlocked == null || i < 0 || i >= stationUnlocked.Length) return;
            if (!IsStationUnlocked(kind) || !IsStationRuin(kind)) return;

            stationUnlocked[i] = true;
            RefreshStationVisuals();
            StationRestored?.Invoke(kind);
            StationRestoredByRole?.Invoke(kind, role);
            Changed();
        }
        public float RestoreProgress => restoreProgress;
        public bool IsRestoring => restoring;
        public bool IsRestoringBy(CrewActor actor) => restoring && restoreActor == actor;
        public StationKind RestoringStation => restoreStation;

        void NormalizeTutorialRuinsAfterLoad()
        {
            if (stationUnlocked == null || stationUnlocked.Length != StationCount)
                stationUnlocked = new bool[StationCount];
            if (stationHintSeen == null || stationHintSeen.Length != StationCount)
                stationHintSeen = new bool[StationCount];
            if (tutorialMilestoneSeen == null || tutorialMilestoneSeen.Length != (int)TutorialMilestone.Count)
                tutorialMilestoneSeen = new bool[(int)TutorialMilestone.Count];
            if (tutorialVersion == 0)
            {
                // Old save: everything was usable before the tutorial existed. Keep it that way
                // and mark every milestone as already seen so no tutorial cards fire.
                for (int i = 0; i < StationCount; i++) stationUnlocked[i] = true;
                for (int i = 0; i < (int)TutorialMilestone.Count; i++) tutorialMilestoneSeen[i] = true;
            }
            RefreshStationVisuals();
        }

        /// <summary>Fire a gameplay milestone card once per save. No-op for legacy saves.</summary>
        public bool ReachMilestone(TutorialMilestone milestone)
        {
            if (!TutorialActive) return false;
            int i = (int)milestone;
            if (i < 0 || i >= (int)TutorialMilestone.Count) return false;
            if (tutorialMilestoneSeen == null || tutorialMilestoneSeen.Length != (int)TutorialMilestone.Count)
                tutorialMilestoneSeen = new bool[(int)TutorialMilestone.Count];
            if (tutorialMilestoneSeen[i]) return false;
            tutorialMilestoneSeen[i] = true;
            MilestoneReached?.Invoke(milestone);
            Changed();
            return true;
        }

        /// <summary>Called every simulation step to advance a held interact on a nearby ruin.</summary>
        internal void UpdateStationRestoration(float dt)
        {
            if (!restoring || restoreActor == null) return;
            // Cancel when the actor releases interact, walks away, or can no longer act.
            StationAnchor anchor = restoreActor.NearbyStation;
            if (!RestoreHoldOf(restoreActor) || anchor == null || anchor.kind != restoreStation || IsActorInputBlocked(restoreActor))
            {
                CancelRestoration();
                return;
            }
            restoreProgress += dt;
            if (restoreProgress >= StationRestoreSeconds)
            {
                CrewRole role = restoreActor != null ? restoreActor.role : CrewRole.Captain;
                CompleteRestoration(restoreStation, role);
            }
        }

        /// <summary>Begin cleaning a ruin. Returns true if cleaning actually started.</summary>
        public bool TryBeginRestoration(StationKind kind, CrewActor actor)
        {
            if (phase != SessionPhase.Playing || actor == null || !actor.onboard) return false;
            if (!IsStationRuin(kind)) return false;
            if (restoring && restoreActor == actor && restoreStation == kind) return true;
            restoreActor = actor; restoreStation = kind; restoreProgress = 0; restoring = true;
            Changed();
            return true;
        }

        public void CancelRestoration()
        {
            restoring = false; restoreActor = null; restoreProgress = 0;
            Changed();
        }

        /// <summary>Finish cleaning: unlock the station, fire UI/visual events, reveal the hint card once.</summary>
        public void CompleteRestoration(StationKind kind)
        {
            CrewRole role = restoreActor != null ? restoreActor.role : CrewRole.Captain;
            CompleteRestoration(kind, role);
        }

        public void CompleteRestoration(StationKind kind, CrewRole role)
        {
            int i = (int)kind;
            if (i < 0 || i >= StationCount) return;
            bool wasRuin = IsStationRuin(kind);
            stationUnlocked[i] = true;
            restoring = false; restoreActor = null; restoreProgress = 0;
            restoreInputLatch = true;
            RefreshStationVisuals();
            if (!wasRuin) { Changed(); return; }
            Tell(string.Format(UiLocalization.Text("已清理：{0}。可以开始使用了。"), GameNames.Station(kind)));
            StationRestored?.Invoke(kind);
            StationRestoredByRole?.Invoke(kind, role);
            Changed();
        }

        /// <summary>True the first time a station's hint card is shown; later calls return false.</summary>
        public bool MarkStationHintSeen(StationKind kind)
        {
            int i = (int)kind;
            if (i < 0 || i >= StationCount) return false;
            if (stationHintSeen == null || stationHintSeen.Length != StationCount)
                stationHintSeen = new bool[StationCount];
            if (stationHintSeen[i]) return false;
            stationHintSeen[i] = true;
            Changed();
            return true;
        }

        internal void RefreshStationVisuals()
        {
            foreach (StationAnchor anchor in FindObjectsByType<StationAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                anchor.SetRestored(IsStationRestored(anchor.kind));
        }

        public bool TryRestoreStation(StationKind kind) => IsStationRuin(kind);
    }
}
