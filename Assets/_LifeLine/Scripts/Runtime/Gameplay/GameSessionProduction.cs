using System;
using UnityEngine;

namespace Lifeline
{
    public partial class GameSession
    {
        // Compatibility API. CraftingManager owns every crafting rule and the only live queue.
        public float GetCraftDuration(RecipeDefinition recipe = null) => GetOrCreateCraftingManager().GetCraftDuration(recipe);
        public float GetCraftQteWindow(CraftJob job = null) => GetOrCreateCraftingManager().GetCraftQteWindow(job);
        public bool TryCraft(string recipeId, CrewActor actor) => GetOrCreateCraftingManager().TryCraft(recipeId, actor);
        public bool CancelCraft(int jobId) => GetOrCreateCraftingManager().CancelCraft(jobId);
        public bool CraftQte(int jobId, int direction) => GetOrCreateCraftingManager().CraftQte(jobId, direction);
        void UpdateCrafts(float dt) => GetOrCreateCraftingManager().Tick(dt);
        public bool TryConsumeSailing(float distance)
        {
            if (phase != SessionPhase.Playing || distance < 0 || float.IsNaN(distance) || float.IsInfinity(distance)) return false;
            if (distance == 0) return true;
            if (!engineRepaired) return false;
            float cost = distance / Mathf.Max(.01f, tuning.routeDistance) * tuning.routePowerCost;
            if (!infinitePower && power + .00001f < cost) return false;
            float startPower = power;
            if (!infinitePower) power = Mathf.Max(0, power - cost);
            if (engineRepaired && !infinitePower && (power < tuning.lowPowerThreshold || startPower < tuning.lowPowerThreshold))
            {
                // Drive calls this once per physics step. This is elapsed powered movement, not distance or render frames.
                lowPowerSailingSeconds += Time.fixedDeltaTime;
                if (lowPowerSailingSeconds >= tuning.lowPowerRiskInterval)
                {
                    lowPowerSailingSeconds -= tuning.lowPowerRiskInterval;
                    if (UnityEngine.Random.value < tuning.lowPowerEngineRisk) DamageEngine("低电量航行使引擎过载");
                }
            }
            else lowPowerSailingSeconds = 0;
            return true;
        }

        public bool CrankGenerator(bool success, CrewActor actor = null)
        {
            if (phase != SessionPhase.Playing || !generatorInstalled || infinitePower || power >= tuning.maxPower || elapsed - lastCrankAt < GetGeneratorStrokeInterval(actor) - .00001f) return false;
            lastCrankAt = elapsed;
            if (success) { power = Mathf.Min(tuning.maxPower, power + tuning.handPowerPerStroke); Tell("手摇发电 +" + tuning.handPowerPerStroke.ToString("0.#") + "%"); }
            else Tell("节奏错过，没有产生电力。");
            Changed(); return success;
        }

        public bool UpgradeGenerator()
        {
            if (phase != SessionPhase.Playing || !generatorInstalled || infinitePower || generatorTier > 1 || efficientFuelGenerator) return false;
            if (generatorTier == 1)
            {
                if (!Spend(generatorEfficiencyCost)) return false;
                efficientFuelGenerator = true; Changed(); return true;
            }
            if (!Spend(generatorUpgradeCost)) return false;
            generatorTier = 1; Tell("燃油机已安装，可消耗燃油为整船发电。"); Changed(); return true;
        }

        public float FuelPower => efficientFuelGenerator ? tuning.upgradedFuelPower : tuning.fuelPower;

        public bool BurnFuel()
        {
            if (phase != SessionPhase.Playing || !generatorInstalled || generatorTier < 1 || infinitePower || power >= tuning.maxPower || !Spend(new[] { new ResourceAmount(ResourceKind.Fuel, 1) })) return false;
            power = Mathf.Min(tuning.maxPower, power + FuelPower); Tell("燃油发电 +" + FuelPower.ToString("0") + "%"); Changed(); return true;
        }

        public bool InstallEel()
        {
            if (phase != SessionPhase.Playing || infinitePower || !Spend(new[] { new ResourceAmount(ResourceKind.Eel, 1) })) return false;
            generatorTier = 2; infinitePower = true; power = tuning.maxPower;
            generatorInstalled = true; AutoRestoreStationOnUnlock(StationKind.Generator); RefreshEquipmentVisibility();
            float previous = hullMax; hullMax *= tuning.eelHullMultiplier; hull += hullMax - previous;
            Tell("电鳗成为整船无限电源：船体上限提升30%，航速提升20%。"); Changed(); return true;
        }

        void UpdateElectricity(float dt)
        {
            if (infinitePower) { power = tuning.maxPower; return; }
            if (LightEffective) power = Mathf.Max(0, power - tuning.maxPower * .01f * searchlightPowerPerSecond * dt);
            if (winchElectric && power > 0) power = Mathf.Max(0, power - electricWinchPowerPerSecond * dt);
            if (power <= 0 && winchElectric) { winchElectric = false; winchSignalAuthorized = false; Tell("电力耗尽，电动绞盘停止。船长仍可手动收绳。"); }
        }

        public bool ToggleSearchlight()
        {
            if (!IsStationUnlocked(StationKind.Searchlight)) return false;
            if (!circuitRepaired) { Tell("修复电路后才能启用探照灯。"); return false; }
            searchlightOn = !searchlightOn; Tell(searchlightOn ? "探照灯开启，照亮船下水域。" : "探照灯关闭。"); Changed(); return true;
        }

        public bool TryRepairDevice(StationKind kind)
        {
            if (phase != SessionPhase.Playing || captain == null) return false;
            if (kind != StationKind.Filter && kind != StationKind.Circuit && kind != StationKind.Engine) return false;
            if (!IsStationUnlocked(kind)) return false;
            if (kind == StationKind.Filter && filterRepaired || kind == StationKind.Circuit && circuitRepaired || kind == StationKind.Engine && engineRepaired) return false;
            int index = (int)kind;
            if (!repairAuthorized[index])
            {
                ResourceAmount[] cost = kind == StationKind.Filter ? filterRepairCost : kind == StationKind.Circuit ? circuitRepairCost : engineEverRepaired ? new ResourceAmount[0] : new[] { new ResourceAmount(ResourceKind.Plate, tuning.firstEngineRepairPlates) };
                if (!Spend(cost)) { Tell("修理材料不足。引擎首次需要2块铁板，之后损坏可免费重修。"); return false; }
                repairAuthorized[index] = true;
            }
            CrewActor owner = captain;
            if (diver != null && diver.controlsCaptured && diver.NearbyStation != null && diver.NearbyStation.kind == kind) owner = diver;
            StationRequested?.Invoke(kind, owner); Changed(); return true;
        }

        public void CompleteRepair(StationKind kind)
        {
            if (phase != SessionPhase.Playing || (int)kind >= repairAuthorized.Length || !repairAuthorized[(int)kind]) return;
            if (kind == StationKind.Filter) { filterRepaired = true; Tell("海水过滤器启用：船上设备交互效率提升20%，水下移动速度提升20%。"); }
            else if (kind == StationKind.Circuit) { circuitRepaired = true; Tell("电路修复。探照灯、求救电台与炮塔现在可以工作。"); ReachMilestone(TutorialMilestone.CircuitRepaired); }
            else if (kind == StationKind.Engine) { MarkEngineRepaired(); return; }
            else return;
            repairAuthorized[(int)kind] = false; Changed();
        }

        public void MarkEngineRepaired()
        {
            engineRepaired = engineEverRepaired = true;
            lowPowerSailingSeconds = 0; repairAuthorized[(int)StationKind.Engine] = false;
            for (int i = 0; i < engineFaultFixed.Length; i++) { engineFaultFixed[i] = true; engineFaultClean[i] = 1; }
            Tell("四处引擎故障已排除，航速提升40%。后续受损不再消耗材料。"); Changed();
        }

        public bool DamageEngine(string reason)
        {
            if (!engineRepaired) return false;
            engineRepaired = false; engineRepairVersion++; lowPowerSailingSeconds = 0;
            repairAuthorized[(int)StationKind.Engine] = false; ResetEngineFaults();
            Tell(reason + "。四处新故障已出现，可免费重修。"); Changed(); return true;
        }

        void ResetEngineFaults()
        {
            int count = tuning != null ? Mathf.Clamp(tuning.engineFaultCount, 1, 4) : 4;
            engineFaultClean = new float[count]; engineFaultFixed = new bool[count]; engineFaultTypes = new int[count];
            for (int i = 0; i < count; i++) engineFaultTypes[i] = i;
            for (int i = count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); int temp = engineFaultTypes[i]; engineFaultTypes[i] = engineFaultTypes[j]; engineFaultTypes[j] = temp; }
        }

        public bool RevealEngineFault(int faultIndex)
        {
            if (!repairAuthorized[(int)StationKind.Engine] || faultIndex < 0 || faultIndex >= engineFaultClean.Length) return false;
            engineFaultClean[faultIndex] = 1; Changed(); return true;
        }

        public bool TryRepairEngineFault(int faultIndex, int part)
        {
            if (!repairAuthorized[(int)StationKind.Engine] || faultIndex < 0 || faultIndex >= engineFaultFixed.Length || engineFaultClean[faultIndex] < 1 || engineFaultFixed[faultIndex]) return false;
            if (engineFaultTypes[faultIndex] != part) { Tell("维修部件不匹配；先识别油污下的故障类型。"); return false; }
            engineFaultFixed[faultIndex] = true;
            bool complete = true; foreach (bool fixedFault in engineFaultFixed) complete &= fixedFault;
            if (complete) CompleteRepair(StationKind.Engine);
            Changed(); return true;
        }

        public bool TryCatchFish(bool quality, CrewActor actor)
        {
            if (phase != SessionPhase.Playing || !hasRod || actor == null || !actor.onboard || actor.downed) return false;
            if (!quality) { AddResource(ResourceKind.Fish, 1); Tell("钓获普通鱼，可在制作台加工成诱饵。"); return true; }
            CrewBuffs buffs = Buffs(actor);
            int effect = UnityEngine.Random.Range(0, 6);
            string label;
            switch (effect)
            {
                case 0: buffs.move = tuning.fishBuffSeconds; label = "移动速度提升10%"; break;
                case 1: buffs.qte = tuning.fishBuffSeconds; label = "制作QTE增益提升20%"; break;
                case 2: buffs.attack = tuning.fishBuffSeconds; label = "攻击力提升10%"; break;
                case 3: diverBuffs.capacity = tuning.fishBuffSeconds; label = "潜水员背包容量增加1格"; break;
                case 4: hull = Mathf.Min(hullMax, hull + hullMax * tuning.fishHullHeal); label = "船体恢复10%"; break;
                default:
                    diverBuffs.oxygen = tuning.fishBuffSeconds;
                    if (diver != null) { float previousMax = diver.oxygenMax; diver.oxygenMax = GetOxygenMax(); diver.oxygen = Mathf.Min(diver.oxygenMax, diver.oxygen + Mathf.Max(0, diver.oxygenMax - previousMax)); }
                    label = "潜水员氧气上限增加10%"; break;
            }
            Tell("品质鱼已食用：" + GameNames.Role(actor.role) + label + "，持续120秒。"); Changed(); return true;
        }

        public float QualityFishChance => ship == null || ship.transform.position.x < tuning.shallowBoundary ? tuning.qualityFishShallow : ship.transform.position.x < tuning.deepBoundary ? tuning.qualityFishMiddle : tuning.qualityFishDeep;

        public bool StartRescueVoyage()
        {
            if (phase != SessionPhase.Playing || rescueVoyage) return false;
            if (!circuitRepaired || radioStage < 3 || diver == null || !diver.onboard)
            { Tell("救援航程需要完整三级电台与修复电路，并接回潜水员。修复引擎后才能航行。"); return false; }
            rescueVoyage = true;
            Tell(day < 4 ? "救援航程已登记。等待第4日救援频道，先修船、准备物资和动力；游戏时间继续自然流逝。" : "第4章：搜救频道锁定东侧信号。向东航行，用船长绞盘接回幸存者。");
            Changed(); return true;
        }

        void UpdateRescue(float dt)
        {
            if (!rescueVoyage || day < 4 || ship == null || ship.transform.position.x < rescueArrivalX || diver == null || !diver.onboard) return;
            if ((winchActive || winchElectric) && CaptainCanWinch())
            {
                rescueProgress = Mathf.Clamp01(rescueProgress + dt * WinchRecoveryMultiplier / Mathf.Max(1, rescueWinchSeconds));
                winchProgress = rescueProgress; winchTargetKind = "survivors";
                if (rescueProgress >= 1) EndSession(true, "救生索接住了最后一名幸存者。失联结束——你们共同活到了救援到来。");
            }
        }
    }
}

