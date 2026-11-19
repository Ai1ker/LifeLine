using UnityEngine;

namespace Lifeline
{
    public partial class GameSession
    {
        public float ClampBoatXToLeash(float proposedBoatX)
        {
            if (ship == null || ropeLength <= 0) return proposedBoatX;
            Vector3 proposedAnchor = BoatAnchor + Vector3.right * (proposedBoatX - ship.Position.x);
            foreach(var actor in Crew)if(actor!=null&&!actor.onboard)
            {
                var route = GetCrewLifeline(actor); route.UpdateRoute(BoatAnchor, actor.Position);
                proposedAnchor = route.ClampAnchorMove(proposedAnchor, ropeLength);
            }
            if (world != null) foreach (var cargo in world.pickups)
            {
                if (!IsWinchCargo(cargo)) continue;
                var route = GetCargoLifeline(cargo); route.UpdateRoute(BoatAnchor, cargo.transform.position);
                proposedAnchor = route.ClampAnchorMove(proposedAnchor, ropeLength);
            }
            return ship.Position.x + proposedAnchor.x - BoatAnchor.x;
        }

        public void ApplyDiverLeash(CrewActor actor)
        {
            if (actor == null || (actor != diver && actor != captain) || actor.onboard || IsHidden(actor) || ropeLength <= 0) return;
            DiverRope route = GetCrewLifeline(actor);
            route.UpdateRoute(BoatAnchor, actor.Position);
            route.ApplyLengthLimit(actor, ropeLength);
        }

        // Optional implementation lives in the removable Development folder.
        partial void OverrideOxygenDrain(CrewActor actor, ref float drain);

        void UpdateOxygen(float dt)
        {
            if (diver == null) return;
            diverInvulnerable = Mathf.Max(0, diverInvulnerable - dt);
            diver.oxygenMax = GetOxygenMax();
            diver.oxygen = Mathf.Min(diver.oxygen, diver.oxygenMax);
            if (diver.onboard)
            {
                diver.oxygen = Mathf.MoveTowards(diver.oxygen, diver.oxygenMax, faintOxygenRecovery * dt);
                return;
            }
            if (diverFainted)
            {
                // This slow safety tow never boards the diver. The captain must finish the recovery.
                DiverRope route = GetOrCreateLifeline();
                route.UpdateRoute(BoatAnchor, diver.Position);
                route.Pull(diver, faintDriftSpeed * dt);
                return;
            }
            if (diver.downed) return;
            float drain = (diverSprinting ? tuning.sprintOxygenMultiplier : 1) * (IsNight ? nightOxygenMultiplier : 1);
            OverrideOxygenDrain(diver, ref drain);
            diver.oxygen = Mathf.Max(0, diver.oxygen - dt * drain);
            if (diver.oxygen <= 0) FaintDiver("氧气耗尽");
        }

        public void FaintDiver(string reason)
        {
            if (diver == null || diver.onboard || diverFainted) return;
            diver.SetHidden(false);
            diverFainted = true; diverHidden = false;
            if (diver.body != null) diver.body.linearVelocity = Vector3.zero;
            int dropCount = diver.backpack.Count / 2;
            if (dropCount > 0 && world != null)
            {
                GameObject bagObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bagObject.name = "LostDiverBag_ExactContents";
                bagObject.transform.position = diver.Position; bagObject.transform.localScale = new Vector3(.6f, .45f, .5f);
                Collider collider = bagObject.GetComponent<Collider>(); if (collider != null) collider.isTrigger = true;
                LootPickup bag = bagObject.AddComponent<LootPickup>();
                bag.world = world; bag.capacity = Mathf.Max(tuning.crateCapacity, dropCount);
                bag.isCrate = true; bag.isCabinet = false; bag.amount = 0;
                for (int i = 0; i < dropCount; i++) { int last = diver.backpack.Count - 1; bag.contents.Add(diver.backpack[last]); diver.backpack.RemoveAt(last); }
                bag.persistAcrossDays = true; bag.isLostCargo = true;
                bag.transform.SetParent(world.transform, true); world.pickups.Add(bag); world.PrepareRestoredPickup(bag);
            }
            Tell(reason + "，潜水员昏厥。缓慢拖向船底，仍需船长用绞盘接回；一半背包物资留在原处。"); Changed();
        }

        public bool RegisterSharkBite()
        {
            return ReceiveDiverHit("鲨鱼咬伤", true, tuning.sharkDamage);
        }

        public bool ReceiveDiverHit(string reason, bool dropResource = true, float hitAmount = 1f)
        {
            if (hitAmount <= 0 || diver == null || IsHidden(diver) || diverFainted || diver.downed || diverInvulnerable > 0) return false;
            if (GetUpgrade(UpgradeKind.DivingSuit) >= 3 && UnityEngine.Random.value < tuning.upgradedSuitImmunity)
            {
                diverInvulnerable = biteInvulnerabilitySeconds;
                Tell("潜水服免疫了这次攻击。"); Changed(); return false;
            }
            diverBites += hitAmount; diverInvulnerable = biteInvulnerabilitySeconds;
            if (dropResource && diver.backpack.Count > 0) diver.backpack.RemoveAt(diver.backpack.Count - 1);
            Tell(reason + "（" + diverBites + "/" + GetMaxDiverBites() + "）。" + (dropResource ? "掉落1件背包物资。" : "") + "用鱼叉反击或投放诱饵。");
            if (diverBites >= GetMaxDiverBites()) { if (diver.onboard) diver.SetDowned(true); else FaintDiver("潜水服承伤次数耗尽"); }
            return true;
        }

        public bool CaptainCanWinch()
        {
            if(winchElectric&&IsElectricWinch&&circuitRepaired&&(power>0||infinitePower))return true;
            CrewActor owner=winchOperatorRole==CrewRole.Captain?captain:diver;
            return owner!=null&&!IsIncapacitated(owner)&&owner.onboard&&IsNearStation(owner,StationKind.Winch);
        }

        public bool IsNearStation(CrewActor actor, StationKind kind)
        {
            if (actor == null || !actor.onboard) return false;
            if (actor.NearbyStation != null && actor.NearbyStation.kind == kind) return true;
            if (ship == null) return false;
            foreach (StationAnchor station in ship.GetComponentsInChildren<StationAnchor>())
            {
                if (station.kind != kind) continue;
                Vector3 point = station.interactionPoint != null ? station.interactionPoint.position : station.transform.position;
                if (Vector3.Distance(actor.Position, point) < station.interactionRadius + .8f) return true;
            }
            return false;
        }

        public void SetWinchActive(bool active)=>SetWinchActive(active,captain);
        public void SetWinchActive(bool active,CrewActor actor)
        {
            if (!active) { winchActive = false; if (!IsElectricWinch) winchElectric = false; Changed(); return; }
            if (phase != SessionPhase.Playing || IsIncapacitated(actor) || !actor.onboard || !IsNearStation(actor, StationKind.Winch))
            { Tell("操作绞盘的船员需要留在绞盘旁。"); return; }
            winchOperatorRole=actor.role;winchActive = true;
            winchSignalAuthorized = false;
            winchElectric = IsElectricWinch && circuitRepaired && (power > 0 || infinitePower);
            winchCycleTime = 0; winchAttemptedThisCycle = false; ResetWinchTarget();
            PrepareWinchTarget();
            Tell(winchElectric ? "船长启动了电动绞盘，关闭面板后仍会继续接回目标。" : "手动绞盘缓慢收绳。成功校准加进度，失败会回滑。"); Changed();
        }

        public int WinchTargetCount
        {
            get
            {
                int count = 0;foreach(var actor in Crew)if(actor!=null&&!actor.onboard)count++;
                if (world != null) foreach (var cargo in world.pickups) if (IsWinchCargo(cargo)) count++;
                return count;
            }
        }
        public float WinchRecoveryMultiplier => GetUpgrade(UpgradeKind.Rope) >= 4 ? 1 + tuning.winchT4RecoveryBonus : 1;
        public float WinchEfficiency => WinchRecoveryMultiplier / Mathf.Max(1, WinchTargetCount);
        static bool IsWinchCargo(LootPickup cargo) => cargo != null && cargo.isCrate && cargo.attached && !cargo.consumed && cargo.gameObject.activeInHierarchy;

        void PrepareWinchTarget()
        {
            winchCargo = null;
            float distance = 0;
            foreach(var actor in Crew)if(actor!=null&&!actor.onboard)
            {
                var route = GetCrewLifeline(actor); route.UpdateRoute(BoatAnchor, actor.Position); distance += route.PathLength;
            }
            if (world != null) foreach (var cargo in world.pickups)
            {
                if (!IsWinchCargo(cargo)) continue;
                if (winchCargo == null) winchCargo = cargo;
                var route = GetCargoLifeline(cargo); route.UpdateRoute(BoatAnchor, cargo.transform.position); distance += route.PathLength;
            }
            string kind = WinchTargetCount > 0 ? (winchCargo != null ? "cargo" : "diver") : "";
            if (kind == "" && rescueVoyage && ship != null && ship.Position.x >= rescueArrivalX && day >= 4) kind = "survivors";
            if (kind != winchTargetKind && winchStartDistance <= 0) winchProgress = 0;
            winchTargetKind = kind; winchStartDistance = Mathf.Max(winchStartDistance, distance);
        }

        void UpdateWinch(float dt)
        {
            winchJam = Mathf.Max(0, winchJam - dt);
            winchCueCooldown = Mathf.Max(0, winchCueCooldown - dt);
            winchFeedbackTime = Mathf.Max(0, winchFeedbackTime - dt);
            if (winchFeedbackTime <= 0 && !winchActive && !winchElectric && winchProgress >= 1) { winchTargetKind = ""; winchProgress = 0; }
            if (winchElectric && !infinitePower && power <= 0) { winchElectric = false; winchSignalAuthorized = false; }
            if (!winchActive && !winchElectric) return;
            if (!CaptainCanWinch()) { winchActive = winchElectric = false; return; }
            PrepareWinchTarget();
            float cycle = Mathf.Max(.1f, tuning.winchCycle);
            float interactionDt = dt * WinchInteractionSpeed;
            if (!winchCenterInitialized) ResetWinchTarget();
            winchCycleTime = Mathf.Repeat(winchCycleTime + interactionDt, cycle);
            if (winchTargetKind == "" || winchTargetKind == "survivors" || winchJam > 0) return;
            MoveWinch(tuning.winchPassiveSpeed * interactionDt + (winchElectric ? tuning.winchElectricSpeed * interactionDt : 0));
        }

        public float WinchQteWidth => tuning.winchOriginalWidth * (1 + tuning.winchBaseBonus + (GetUpgrade(UpgradeKind.Rope) >= 2 ? tuning.winchT2Bonus : 0));

        public bool TryWinchQte(float normalizedAngle)
        {
            if (phase != SessionPhase.Playing || (!winchActive && !winchElectric) || !CaptainCanWinch() || winchJam > 0 || winchCueCooldown > 0) return false;
            PrepareWinchTarget();
            if (string.IsNullOrEmpty(winchTargetKind)) return false;
            winchAttemptedThisCycle = true;
            bool good = CircularQteDistance(normalizedAngle, WinchQteCenter) <= WinchQteWidth * .5f;
            winchCueCooldown = winchMinimumInputInterval;
            winchFeedback = good ? "校准成功 +" + (tuning.winchGoodDistance * WinchEfficiency).ToString("0.00") + "m" : "校准失败 −" + (tuning.winchBadDistance / Mathf.Max(1, WinchTargetCount)).ToString("0.00") + "m";
            winchFeedbackTime = 1.2f;
            if (winchTargetKind == "survivors")
            {
                float delta = good ? tuning.winchGoodDistance * WinchRecoveryMultiplier : -tuning.winchBadDistance;
                rescueProgress = Mathf.Clamp01(rescueProgress + delta / Mathf.Max(1, rescueWinchSeconds)); winchProgress = rescueProgress;
            }
            else MoveWinch(good ? tuning.winchGoodDistance : -tuning.winchBadDistance);
            if (!good) { winchJam = .3f; AttractEnemies(BoatAnchor); }
            RefreshWinchTarget();
            Changed(); return true; // accepted mistakes also return true so the UI can show failure feedback.
        }

        void MoveWinch(float pullDistance)
        {
            int count = WinchTargetCount;
            if (count == 0) return;
            float share = pullDistance * (pullDistance > 0 ? WinchRecoveryMultiplier : 1) / count;
            if (world != null) foreach (var cargo in world.pickups)
            {
                if (!IsWinchCargo(cargo)) continue;
                var route = GetCargoLifeline(cargo); route.UpdateRoute(BoatAnchor, cargo.transform.position);
                if (share > 0) route.TryBreakBlockingGate();
                float movement = share < 0 ? -Mathf.Min(-share, Mathf.Max(0, ropeLength - route.PathLength)) : share;
                cargo.transform.position = route.PullPoint(cargo.transform.position, movement);
                route.UpdateRoute(BoatAnchor, cargo.transform.position);
                if (share > 0 && !route.IsBlocked && route.PathLength <= boardDistance) cargo.Deliver(this);
            }
            foreach(var actor in Crew)
            {
                if(actor==null||actor.onboard||IsHidden(actor))continue;
                var route=GetCrewLifeline(actor);route.UpdateRoute(BoatAnchor,actor.Position);
                if(share>0)route.TryBreakBlockingGate();
                float movement=share<0?-Mathf.Min(-share,Mathf.Max(0,ropeLength-route.PathLength)):share;
                route.Pull(actor,movement);route.UpdateRoute(BoatAnchor,actor.Position);
                if(share>0&&!route.IsBlocked&&route.PathLength<=boardDistance)BankCrew(actor);
            }
            PrepareWinchTarget();
            float remaining = 0;
            foreach(var actor in Crew)if(actor!=null&&!actor.onboard)remaining+=GetCrewLifeline(actor).PathLength;
            if (world != null) foreach (var cargo in world.pickups) if (IsWinchCargo(cargo)) remaining += GetCargoLifeline(cargo).PathLength;
            winchProgress = Mathf.Clamp01(1 - remaining / Mathf.Max(boardDistance, winchStartDistance));
            if (WinchTargetCount == 0)
            {
                winchProgress = 1; winchFeedback = "接回完成"; winchFeedbackTime = 1.2f;
                winchStartDistance = 0; winchTargetKind = ""; winchCargo = null;
                winchActive = winchElectric = winchSignalAuthorized = false;
            }
            Changed();
        }

        void BankDiver()
        {
            if (diver == null || diver.onboard) return;
            bool needsRevive = IsIncapacitated(diver);
            foreach (ResourceKind item in diver.backpack) AddResource(item, 1);
            int delivered = diver.backpack.Count; diver.backpack.Clear();
            diver.SetHidden(false);diver.fainted=false;
            diver.SetOnboard(true, ShipPoint(bankArrivalLocal));
            if (diverRope != null) diverRope.ResetRoute();
            diverFainted = false; diverHidden = false; diverSprinting = false; diverBites = needsRevive ? GetMaxDiverBites() : 0; diverInvulnerable = 0;
            diver.SetDowned(needsRevive);
            diver.oxygenMax = GetOxygenMax(); diver.oxygen = diver.oxygenMax;
            signalCount = 0;
            Tell("船长接回潜水员，" + delivered + "件物资已卸入共享仓库。" + (diver.downed ? "靠近倒地潜水员按交互进行救助。" : ""));
            if (delivered > 0) ReachMilestone(TutorialMilestone.FirstCargoBanked);
        }

        public void SignalCaptain()
        {
            signalCount = elapsed - lastSignalAt <= tuning.electricWinchSignalWindow ? signalCount + 1 : 1;
            lastSignalAt = elapsed;
            Tell(signalCount == 1 ? "潜水员信号：请求船长收绳。" : signalCount == 2 ? "潜水员双信号：请求同时回收潜水员和全部挂载货箱。" : "潜水员紧急三信号：请船长立即操作绞盘！");
            AttractEnemies(diver != null ? diver.Position : BoatAnchor);
            if (signalCount >= tuning.electricWinchSignalCount && IsElectricWinch && diver != null && !diver.onboard && circuitRepaired && (power > 0 || infinitePower))
            {
                winchCargo = null; winchTargetKind = ""; winchActive = false; winchElectric = true; winchSignalAuthorized = true;
                winchStartDistance = 0; winchProgress = 0; signalCount = 0;
                PrepareWinchTarget(); Tell("连续救生信号已触发电动绞盘，正在同时回收潜水员和全部挂载货箱。"); Changed();
            }
        }

        public bool TryReviveTeammate(CrewActor actor)
        {
            CrewActor other = FindReviveTarget(actor);
            if (other == null) return false;
            int index = (int)actor.role;
            if (reviveTargets[index] != other) { reviveTargets[index] = other; reviveProgress[index] = 0; Tell(string.Format("按住交互{0}秒，扶起{1}；松开或离开会中断。", tuning.reviveSeconds.ToString("0.#"), GameNames.Role(other.role)), actor); }
            return true;
        }

        public CrewActor FindReviveTarget(CrewActor actor)
        {
            if (IsIncapacitated(actor)) return null;
            CrewActor other = actor == captain ? diver : captain;
            return other != null && other.downed && CanReviveFrom(actor, other) ? other : null;
        }

        bool CanReviveFrom(CrewActor actor, CrewActor other)
        {
            bool nearby = actor.onboard == other.onboard && Vector3.Distance(actor.Position, other.Position) <= tuning.reviveDistance;
            // Do not deadlock when the captain is down on deck and the only conscious teammate is submerged.
            // The diver can assist from the nearby outer rail without being permitted to self-board.
            if (!nearby && !actor.onboard && other.onboard && ship != null)
            {
                Vector3 rail = new Vector3(other.Position.x, BoatAnchor.y, 0);
                nearby = Vector3.Distance(actor.Position, rail) <= tuning.reviveDistance;
            }
            return nearby;
        }

        void UpdateRecovery(float dt)
        {
            for (int i = 0; i < reviveTargets.Length; i++)
            {
                CrewActor actor = i == (int)CrewRole.Captain ? captain : diver;
                CrewActor target = reviveTargets[i];
                if (target == null) continue;
                if (!reviveHeld[i] || actor == null || actor.downed || IsFainted(actor) || !target.downed || !CanReviveFrom(actor, target))
                { reviveTargets[i] = null; reviveProgress[i] = 0; continue; }
                reviveProgress[i] += dt;
                if (reviveProgress[i] >= tuning.reviveSeconds)
                {
                    target.Revive(); if(target==diver) diverBites=0; reviveTargets[i] = null; reviveProgress[i] = 0;
                    Tell(GameNames.Role(actor.role) + "扶起了" + GameNames.Role(target.role) + "。"); Changed();
                }
            }
            bool wipe = captain != null && diver != null && IsIncapacitated(captain) && IsIncapacitated(diver);
            crewWipeTimer = wipe ? crewWipeTimer + dt : 0;
            if (crewWipeTimer < Mathf.Min(crewWipeDelay,.15f)) return;
            crewWipeTimer = 0;
            EndSession(false, "两名船员均已晕厥。读取上个存档或返回主菜单。");
        }
    }
}
