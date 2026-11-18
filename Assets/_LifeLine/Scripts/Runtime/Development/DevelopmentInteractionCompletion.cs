#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace Lifeline
{
    // Deliberately lives beside the removable F8 tools. The player build has no test dependency.
    public partial class GameSession
    {
        public bool DeveloperCompleteInteraction(CrewStationPanel panel, CrewActor actor, GameHUDController hud)
        {
            if (phase != SessionPhase.Playing || panel == null || !panel.IsOpen || actor == null || !actor.onboard || !IsStationUnlocked(panel.station)) return false;
            switch (panel.station)
            {
                case StationKind.Workbench:
                case StationKind.HullRepair:
                    bool completed = false;
                    foreach (var job in jobs.ToArray())
                        if (CraftingManager.JobStation(job) == panel.station) { job.progress = job.duration; completed = true; }
                    if (completed) craftingManager.Tick(.001f);
                    return completed;
                case StationKind.Filter:
                case StationKind.Circuit:
                case StationKind.Engine:
                    var kind = panel.station;
                    if (kind == StationKind.Filter && filterRepaired || kind == StationKind.Circuit && circuitRepaired || kind == StationKind.Engine && engineRepaired) return false;
                    if (!repairAuthorized[(int)kind] && !TryRepairDevice(kind)) return false;
                    if (kind == StationKind.Filter && hud != null)
                    {
                        if (hud.filterPuzzle.masks == null || hud.filterPuzzle.masks.Length == 0) hud.filterPuzzle.Generate(worldSeed + 31);
                        hud.filterPuzzle.masks = (int[])hud.filterPuzzle.solution.Clone();
                    }
                    if (kind == StationKind.Circuit && hud != null) for (int i = 0; i < hud.circuitConnected.Length; i++) hud.circuitConnected[i] = true;
                    CompleteRepair(kind); panel.RebuildRepair(); return true;
                case StationKind.Winch:
                    if (!CaptainCanWinch()) return false;
                    PrepareWinchTarget();
                    if (winchTargetKind == "survivors") { rescueProgress = 1; UpdateRescue(0); return true; }
                    int before = WinchTargetCount;
                    bool diverWasUnderwater = diver != null && !diver.onboard;
                    for (int i = 0; i < 128 && WinchTargetCount > 0; i++)
                    {
                        float previousProgress = winchProgress; int previousCount = WinchTargetCount;
                        MoveWinch(Mathf.Max(100, ropeLength) * Mathf.Max(1, WinchTargetCount));
                        if (WinchTargetCount >= previousCount && winchProgress <= previousProgress + .000001f) break;
                    }
                    if (diverWasUnderwater && diver.onboard && actor == captain) GetComponent<TutorialProgress>()?.Observe(TutorialAction.CaptainWinchRecovery);
                    return before > 0 && WinchTargetCount == 0;
                case StationKind.Generator: return CrankGenerator(true, actor);
                case StationKind.Fishing:
                    if (!panel.fishingActive) return false;
                    panel.fishingActive = false;
                    return TryCatchFish(panel.fishingQuality, actor);
                default: return false;
            }
        }
    }
}
#endif
