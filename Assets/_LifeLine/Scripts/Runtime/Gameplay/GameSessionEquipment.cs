using UnityEngine;

namespace Lifeline
{
    public partial class GameSession
    {
        [Header("建造解锁 / equipment progression")]
        public bool generatorInstalled;
        public bool repairBenchInstalled;
        public bool filterInstalled;

        public bool IsStationUnlocked(StationKind kind)
        {
            switch (kind)
            {
                case StationKind.Fishing: return hasRod;
                case StationKind.Searchlight: return GetUpgrade(UpgradeKind.Searchlight) > 0;
                case StationKind.Turret: return turretInstalled;
                case StationKind.Generator: return generatorInstalled;
                case StationKind.HullRepair: return repairBenchInstalled;
                case StationKind.Filter: return filterInstalled;
                default: return true;
            }
        }

        public bool HasEquipment(string toolId)
        {
            switch (toolId)
            {
                case "rod": return hasRod;
                case "harpoon": return hasHarpoon;
                case "speargun": return hasSpeargun;
                case "generator": return generatorInstalled;
                case "repair-bench": return repairBenchInstalled;
                case "filter": return filterInstalled;
                default: return false;
            }
        }

        /// <summary>Recipe outcomes pass through this one unlock seam; visuals contain no economy rules.</summary>
        public bool UnlockEquipment(string toolId)
        {
            if (string.IsNullOrEmpty(toolId) || HasEquipment(toolId)) return false;
            if (!SetEquipmentState(toolId, true)) return false;
            // Auto-clean at the logic seam, not via the scene-anchored visibility pass, so the
            // rule holds even without StationAnchor objects (tests, headless/authority flows).
            StationKind? station = StationForTool(toolId);
            if (station.HasValue) AutoRestoreStationOnUnlock(station.Value);
            RefreshEquipmentVisibility(); Changed(); return true;
        }

        /// <summary>Equipment tool id -> the station it reveals. Null for tool-only unlocks.</summary>
        static StationKind? StationForTool(string toolId)
        {
            switch (toolId)
            {
                case "rod": return StationKind.Fishing;
                case "generator": return StationKind.Generator;
                case "repair-bench": return StationKind.HullRepair;
                case "filter": return StationKind.Filter;
                default: return null; // harpoon / speargun reveal no station
            }
        }

        public bool ConsumeEquipment(string toolId)
        {
            if (!HasEquipment(toolId) || !SetEquipmentState(toolId, false)) return false;
            RefreshEquipmentVisibility(); Changed(); return true;
        }

        bool SetEquipmentState(string toolId, bool unlocked)
        {
            switch (toolId)
            {
                case "rod": hasRod = unlocked; break;
                case "harpoon": hasHarpoon = unlocked; break;
                case "speargun": hasSpeargun = unlocked; break;
                case "generator": generatorInstalled = unlocked; break;
                case "repair-bench": repairBenchInstalled = unlocked; break;
                case "filter": filterInstalled = unlocked; break;
                default: return false;
            }
            return true;
        }

        public bool AcquireFilter() => UnlockEquipment("filter");

        /// <summary>Find inactive station roots too, so a crafted upgrade can reveal them again.</summary>
        public void RefreshEquipmentVisibility()
        {
            Transform parent = ship != null ? ship.transform : transform;
            foreach (StationAnchor station in parent.GetComponentsInChildren<StationAnchor>(true))
            {
                bool gated = station.kind == StationKind.Fishing || station.kind == StationKind.Searchlight ||
                    station.kind == StationKind.Turret || station.kind == StationKind.Generator ||
                    station.kind == StationKind.HullRepair || station.kind == StationKind.Filter;
                if (gated)
                {
                    bool unlocked = IsStationUnlocked(station.kind);
                    station.gameObject.SetActive(unlocked);
                    // Unlock = auto-clean: skip the 3s ruin hold for devices that just appeared.
                    if (unlocked) AutoRestoreStationOnUnlock(station.kind);
                }
            }
        }

        /// <summary>Pre-progression saves already owned these fixtures. Never remove an existing save's ship.</summary>
        public static void UpgradeEquipmentSave(GameSaveData data)
        {
            if (data == null || data.state == null || data.equipmentProgressionVersion >= 1) return;
            data.state.generatorInstalled = data.state.repairBenchInstalled = data.state.filterInstalled = true;
            int index = (int)UpgradeKind.Searchlight;
            if (data.state.upgradeLevels != null && data.state.upgradeLevels.Length > index)
                data.state.upgradeLevels[index] = Mathf.Min(2, data.state.upgradeLevels[index] + 1);
            data.equipmentProgressionVersion = 1;
        }
    }
}
