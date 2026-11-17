using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Lifeline
{
    public static class GameSaveValidation
    {
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static bool EnumValue<T>(T value) where T : struct => Enum.IsDefined(typeof(T), value);
        public static bool Validate(GameSaveData data, out string error)
        {
            error = "";
            try
            {
                Require(data != null && data.version == GameSaveData.CurrentVersion, "存档版本不受支持。");
                Require(data.coordinateVersion >= 0 && data.coordinateVersion <= 1, "存档版本不受支持。");
                Require((data.diverRopeContacts == null || data.diverRopeContacts.Length <= 256) && (data.cargoRopeContacts == null || data.cargoRopeContacts.Length <= 256), "存档世界数据不完整。");
                CheckFinite(data, new HashSet<object>());
                Require(data.cycleSeconds >= 0 && (data.cycleSeconds == 0 || data.daylightSeconds > 0 && data.duskSeconds > 0 && data.daylightSeconds + data.duskSeconds < data.cycleSeconds), "存档时间无效。");
                Require(data.areaStrikes==null||data.areaStrikes.Length<=128,"存档战斗数据不完整。");
                if(data.areaStrikes!=null)foreach(var strike in data.areaStrikes)Require(strike!=null&&EnumValue(strike.kind)&&strike.remaining>=0&&strike.duration>0&&strike.radius>0&&strike.hullDamage>=0,"存档战斗数据不完整。");
                SessionSaveState state = data.state;
                Require(state != null && state.day >= 1 && state.elapsed >= 0 && data.lastAutoSavedDay >= 0 && data.lastAutoSavedDay <= state.day, "存档时间无效。");
                Require(state.bank != null && state.upgradeLevels != null && state.upgradeLevels.Length == Enum.GetValues(typeof(UpgradeKind)).Length, "存档经济数据不完整。");
                foreach (ResourceAmount amount in state.bank) Require(amount != null && amount.amount >= 0 && EnumValue(amount.resource), "存档物资数量无效。");
                for (int i = 0; i < state.upgradeLevels.Length; i++)
                {
                    UpgradeKind kind = (UpgradeKind)i;
                    int maximum = kind == UpgradeKind.Hull || kind == UpgradeKind.Sail || kind == UpgradeKind.DivingSuit ? 2 : kind == UpgradeKind.Searchlight ? 1 : 3;
                    Require(state.upgradeLevels[i] >= 0 && state.upgradeLevels[i] <= maximum, "存档升级等级无效。");
                }
                Require(state.hullMax > 0 && state.hull >= 0 && state.hull <= state.hullMax && state.power >= 0 && state.ropeLength > 0, "存档船舶状态无效。");
                Require(state.captainBuffs != null && state.diverBuffs != null && state.radioStage >= 0 && state.radioStage <= 3 && state.generatorTier >= 0 && state.generatorTier <= 2, "存档设备状态无效。");
                Require(state.engineFaultClean != null && state.engineFaultFixed != null && state.engineFaultTypes != null && state.engineFaultClean.Length == state.engineFaultFixed.Length && state.engineFaultClean.Length == state.engineFaultTypes.Length && state.engineFaultClean.Length >= 1 && state.engineFaultClean.Length <= 4, "存档引擎故障数据不完整。");
                Require(state.repairAuthorized != null && state.repairAuthorized.Length == Enum.GetValues(typeof(StationKind)).Length && state.meleeCooldowns != null && state.meleeCooldowns.Length == 2, "存档角色与维修数据不完整。");
                Require(state.storyLog != null && state.shadowHits >= 0 && state.rescueProgress >= 0 && state.rescueProgress <= 1, "存档任务数据无效。");
                ValidateCrew(data.captain, CrewRole.Captain); ValidateCrew(data.diver, CrewRole.Diver);
                Require(data.captainRopeContacts==null||data.captainRopeContacts.Length<=256,"存档世界数据不完整。");
                Require(EnumValue(state.winchOperatorRole),"存档角色与维修数据不完整。");
                Require(data.jobs != null && data.jobs.Length <= (state.upgradeLevels[(int)UpgradeKind.Workbench] >= 3 ? 2 : 1) && data.nextJobId >= 1, "存档制作队列无效。");
                var jobIds = new HashSet<int>();
                foreach (CraftSaveState job in data.jobs)
                    Require(job != null && job.id > 0 && jobIds.Add(job.id) && job.id < data.nextJobId && !string.IsNullOrEmpty(job.recipeId) && EnumValue(job.owner) && job.duration > 0 && job.progress >= 0 && job.progress < job.duration && job.elapsed >= 0 && job.nextCue >= 0 && job.noteDirection >= 0 && job.noteDirection < 4 && job.hits >= 0 && job.misses >= 0, "存档作业数据无效。");
                Require(data.world != null && data.world.pickups != null && data.world.gates != null, "存档世界数据不完整。");
                var pickupIds = new HashSet<string>();
                foreach (PickupSaveState pickup in data.world.pickups)
                {
                    Require(pickup != null && !string.IsNullOrEmpty(pickup.id) && pickupIds.Add(pickup.id) && EnumValue(pickup.resource) && pickup.amount >= 0 && pickup.capacity > 0 && pickup.contents != null && (!pickup.isCrate || pickup.contents.Count <= pickup.capacity) && (!pickup.attached || pickup.isCrate), "存档货物数据无效。");
                    Require(pickup.ropeContacts == null || pickup.ropeContacts.Length <= 256, "存档世界数据不完整。");
                    foreach (ResourceKind kind in pickup.contents) Require(EnumValue(kind), "存档货物类型无效。");
                }
                if (!string.IsNullOrEmpty(state.winchCargoId)) Require(pickupIds.Contains(state.winchCargoId), "存档绞盘目标不存在。");
                var gateIds = new HashSet<string>();
                foreach (GateSaveState gate in data.world.gates) Require(gate != null && !string.IsNullOrEmpty(gate.id) && gateIds.Add(gate.id), "存档门数据无效。");
                Require(data.enemies != null && data.spears != null, "存档战斗数据不完整。");
                var enemyIds = new HashSet<string>();
                foreach (EnemySaveState enemy in data.enemies)
                    Require(enemy != null && !string.IsNullOrEmpty(enemy.id) && enemyIds.Add(enemy.id) && EnumValue(enemy.kind) && EnumValue(enemy.state) && enemy.state != EnemyState.Defeated && enemy.health > 0 && enemy.maxHealth >= enemy.health && enemy.shadowHitCount >= 0, "存档敌人数据无效。");
                var spearOwners = new HashSet<CrewRole>();
                foreach (SpearSaveState spear in data.spears)
                    Require(spear != null && EnumValue(spear.owner) && (spear.turretShot || spearOwners.Add(spear.owner)) && (string.IsNullOrEmpty(spear.targetId) || enemyIds.Contains(spear.targetId)) && (string.IsNullOrEmpty(spear.parentEnemyId) || enemyIds.Contains(spear.parentEnemyId)) && spear.age >= 0 && spear.damage >= 0, "存档投射物数据无效。");
                if (data.repairUI != null && data.repairUI.present)
                {
                    Require(data.repairUI.circuitConnected != null && data.repairUI.circuitConnected.Length == 4 && data.repairUI.filterPuzzle != null, "存档维修小游戏数据不完整。");
                    PipeRepairPuzzle puzzle = data.repairUI.filterPuzzle;
                    Require(puzzle.columns > 0 && puzzle.rows > 0 && puzzle.columns <= 400 / puzzle.rows, "存档导流拼图数据无效。");
                    // Scene serialization and JsonUtility may represent an unopened puzzle as [] instead
                    // of null. Both arrays empty is ungenerated; only one empty is damaged saved progress.
                    if (!IsUninitializedPipePuzzle(puzzle))
                    {
                        Require(puzzle.masks != null && puzzle.solution != null && puzzle.masks.Length == puzzle.columns * puzzle.rows && puzzle.solution.Length == puzzle.masks.Length && puzzle.sourceRow >= 0 && puzzle.sourceRow < puzzle.rows && puzzle.exitRow >= 0 && puzzle.exitRow < puzzle.rows, "存档导流拼图数据无效。");
                        foreach (int mask in puzzle.masks) Require(mask >= 0 && mask <= 15, "存档导流拼图数据无效。");
                        foreach (int mask in puzzle.solution) Require(mask >= 0 && mask <= 15, "存档导流拼图数据无效。");
                    }
                }
                return true;
            }
            catch (InvalidOperationException exception) { error = exception.Message; return false; }
        }
        internal static bool IsUninitializedPipePuzzle(PipeRepairPuzzle puzzle) => puzzle != null
            && (puzzle.masks == null || puzzle.masks.Length == 0) && (puzzle.solution == null || puzzle.solution.Length == 0);

        internal static void NormalizeUninitializedPuzzle(RepairUISaveState state)
        {
            if (state == null || !IsUninitializedPipePuzzle(state.filterPuzzle)) return;
            state.filterPuzzle.masks = null; state.filterPuzzle.solution = null;
        }
        static void ValidateCrew(CrewSaveState actor, CrewRole expected)
        {
            Require(actor != null && actor.role == expected && actor.maxHealth > 0 && actor.health >= 0 && actor.health <= actor.maxHealth && actor.oxygenMax > 0 && actor.oxygen >= 0 && actor.oxygen <= actor.oxygenMax + .001f && actor.backpack != null, "存档船员状态无效。");
            foreach (ResourceKind kind in actor.backpack) Require(EnumValue(kind), "存档背包类型无效。");
        }
        static void CheckFinite(object value, HashSet<object> visited)
        {
            if (value == null || value is string) return;
            if (value is float number) { Require(!float.IsNaN(number) && !float.IsInfinity(number), "存档包含无效数值。"); return; }
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || !type.IsValueType && !visited.Add(value)) return;
            if (value is IEnumerable list) { foreach (object item in list) CheckFinite(item, visited); return; }
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) CheckFinite(field.GetValue(value), visited);
        }
    }
}
