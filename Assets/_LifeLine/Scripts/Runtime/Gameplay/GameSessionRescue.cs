using UnityEngine;

namespace Lifeline
{
    public partial class GameSession
    {
        public float ClampBoatXToLeash(float proposedBoatX)
        {
            if (diver == null || diver.onboard || ship == null || ropeLength <= 0) return proposedBoatX;
            DiverRope route = GetOrCreateLifeline();
            route.UpdateRoute(BoatAnchor, diver.Position);
            Vector3 proposedAnchor = BoatAnchor + Vector3.right * (proposedBoatX - ship.Position.x);
            Vector3 allowed = route.ClampAnchorMove(proposedAnchor, ropeLength);
            return ship.Position.x + allowed.x - BoatAnchor.x;
        }

        public void ApplyDiverLeash(CrewActor actor)
        {
            if (actor == null || actor != diver || actor.onboard || ropeLength <= 0) return;
            DiverRope route = GetOrCreateLifeline();
            route.UpdateRoute(BoatAnchor, actor.Position);
            route.ApplyLengthLimit(actor, ropeLength);
        }

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
            diver.oxygen = Mathf.Max(0, diver.oxygen - dt * drain);
            if (diver.oxygen <= 0) FaintDiver("氧气耗尽");
        }

        public void FaintDiver(string reason)
        {
            if (diver == null || diver.onboard || diverFainted) return;
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
                world.pickups.Add(bag);
            }
            Tell(reason + "，潜水员昏厥。缓慢拖向船底，仍需船长用绞盘接回；一半背包物资留在原处。"); Changed();
        }

        public bool RegisterSharkBite()
        {
            return ReceiveDiverHit("鲨鱼咬伤", true);
        }

        public bool ReceiveDiverHit(string reason, bool dropResource = true)
        {
            if (diver == null || diver.onboard || diverFainted || diver.downed || diverInvulnerable > 0) return false;
            if (GetUpgrade(UpgradeKind.DivingSuit) >= 2 && UnityEngine.Random.value < tuning.upgradedSuitImmunity)
            {
                diverInvulnerable = biteInvulnerabilitySeconds;
                Tell("潜水服免疫了这次攻击。"); Changed(); return false;
            }
            diverBites++; diverInvulnerable = biteInvulnerabilitySeconds;
            if (dropResource && diver.backpack.Count > 0) diver.backpack.RemoveAt(diver.backpack.Count - 1);
            Tell(reason + "（" + diverBites + "/" + GetMaxDiverBites() + "）。" + (dropResource ? "掉落1件背包物资。" : "") + "用鱼叉反击或投放诱饵。");
            if (diverBites >= GetMaxDiverBites()) FaintDiver("潜水服承伤次数耗尽");
            return true;
        }

        public bool CaptainCanWinch()
        {
            if (winchElectric && winchSignalAuthorized) return true;
            if (captain == null || captain.downed || !captain.onboard) return false;
            if (winchElectric) return true;
            return IsNearStation(captain, StationKind.Winch);
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

        public void SetWinchActive(bool active)
        {
            if (!active) { winchActive = false; if (!IsElectricWinch) winchElectric = false; Changed(); return; }
            if (phase != SessionPhase.Playing || captain == null || captain.downed || !IsNearStation(captain, StationKind.Winch))
            { Tell("船长需要来到绞盘旁操作。"); return; }
            winchActive = true;
            winchSignalAuthorized = false;
            winchElectric = IsElectricWinch && circuitRepaired && (power > 0 || infinitePower);
            winchCycleTime = 0; winchAttemptedThisCycle = false;
            PrepareWinchTarget();
            Tell(winchElectric ? "船长启动了电动绞盘，关闭面板后仍会继续接回目标。" : "手动绞盘缓慢收绳。成功校准加进度，失败会回滑。"); Changed();
        }

        void PrepareWinchTarget()
        {
            if (winchCargo != null && (!winchCargo.attached || winchCargo.consumed)) winchCargo = null;
            if (winchSignalAuthorized) winchCargo = null;
            if (winchCargo == null && !winchSignalAuthorized && world != null && (signalCount != 1 || diver == null || diver.onboard))
            {
                foreach (LootPickup pickup in world.pickups)
                    if (pickup != null && pickup.isCrate && pickup.attached && !pickup.consumed)
                    { winchCargo = pickup; GetOrCreateLifeline(true).ResetRoute(); break; }
            }
            string kind = winchCargo != null ? "cargo" : diver != null && !diver.onboard ? "diver" : "";
            if (kind == "" && rescueVoyage && ship != null && ship.transform.position.x >= rescueArrivalX && day >= 4) kind = "survivors";
            if (kind != winchTargetKind)
            {
                winchTargetKind = kind; winchStartDistance = 0; winchProgress = 0;
            }
            if (kind == "diver" || kind == "cargo")
            {
                DiverRope route = GetOrCreateLifeline(winchCargo != null);
                route.UpdateRoute(BoatAnchor, WinchTargetPosition());
                float distance = route.PathLength;
                // A target can swim or be carried further away after the operator opened the panel.
                // Never clamp a subsequent QTE to the shorter original distance.
                winchStartDistance = Mathf.Max(winchStartDistance, distance);
            }
        }

        Vector3 WinchTargetPosition() => winchCargo != null ? winchCargo.transform.position : diver != null ? diver.Position : BoatAnchor;

        void UpdateWinch(float dt)
        {
            winchJam = Mathf.Max(0, winchJam - dt);
            winchFeedbackTime = Mathf.Max(0, winchFeedbackTime - dt);
            if (winchFeedbackTime <= 0 && !winchActive && !winchElectric && winchProgress >= 1) { winchTargetKind = ""; winchProgress = 0; }
            if (winchElectric && !infinitePower && power <= 0) { winchElectric = false; winchSignalAuthorized = false; }
            if (!winchActive && !winchElectric) return;
            if (!CaptainCanWinch()) { winchActive = winchElectric = false; return; }
            PrepareWinchTarget();
            float cycle = Mathf.Max(.1f, tuning.winchCycle);
            if (winchCycleTime + dt >= cycle) winchAttemptedThisCycle = false;
            winchCycleTime = Mathf.Repeat(winchCycleTime + dt, cycle);
            if (winchTargetKind == "" || winchTargetKind == "survivors" || winchJam > 0) return;
            MoveWinch(tuning.winchPassiveSpeed * dt + (winchElectric ? tuning.winchElectricSpeed * dt : 0));
        }

        public float WinchQteWidth => tuning.winchOriginalWidth * (1 + tuning.winchBaseBonus + (GetUpgrade(UpgradeKind.Rope) >= 2 ? tuning.winchT2Bonus : 0));

        public bool TryWinchQte(float normalizedAngle)
        {
            if (phase != SessionPhase.Playing || (!winchActive && !winchElectric) || !CaptainCanWinch() || winchJam > 0 || winchAttemptedThisCycle) return false;
            PrepareWinchTarget();
            if (string.IsNullOrEmpty(winchTargetKind)) return false;
            winchAttemptedThisCycle = true;
            bool good = Mathf.Abs(Mathf.Repeat(normalizedAngle, 1) - tuning.winchCenter) <= WinchQteWidth * .5f;
            winchFeedback = good ? "校准成功 +" + tuning.winchGoodDistance.ToString("0.00") + "m" : "校准失败 −" + tuning.winchBadDistance.ToString("0.00") + "m";
            winchFeedbackTime = 1.2f;
            if (winchTargetKind == "survivors")
            {
                float delta = good ? tuning.winchGoodDistance : -tuning.winchBadDistance;
                rescueProgress = Mathf.Clamp01(rescueProgress + delta / Mathf.Max(1, rescueWinchSeconds)); winchProgress = rescueProgress;
            }
            else MoveWinch(good ? tuning.winchGoodDistance : -tuning.winchBadDistance);
            if (!good) { winchJam = .3f; AttractEnemies(BoatAnchor); }
            Changed(); return true; // accepted mistakes also return true so the UI can show failure feedback.
        }

        void MoveWinch(float pullDistance)
        {
            if (winchTargetKind != "diver" && winchTargetKind != "cargo") return;
            DiverRope route = GetOrCreateLifeline(winchCargo != null);
            route.UpdateRoute(BoatAnchor, WinchTargetPosition());
            if (pullDistance > 0) route.TryBreakBlockingGate();
            float distance = route.PathLength;
            winchStartDistance = Mathf.Max(winchStartDistance, distance);
            if (pullDistance < 0) pullDistance = -Mathf.Min(-pullDistance, Mathf.Max(0, ropeLength - distance));
            if (winchCargo != null) winchCargo.transform.position = route.PullPoint(winchCargo.transform.position, pullDistance);
            else if (diver != null) route.Pull(diver, pullDistance);
            route.UpdateRoute(BoatAnchor, WinchTargetPosition());
            float actualDistance = route.PathLength;
            winchProgress = Mathf.Clamp01(1 - actualDistance / Mathf.Max(boardDistance, winchStartDistance));
            if (route.IsBlocked || actualDistance > boardDistance || pullDistance <= 0) return;
            if (winchCargo != null)
            {
                winchCargo.Deliver(this); winchCargo = null; signalCount = 0;
                Tell("货箱吊运完成，只有实际装入的物资进入了仓库。");
            }
            else BankDiver();
            winchProgress = 1; winchFeedback = "接回完成"; winchFeedbackTime = 1.2f;
            winchStartDistance = 0; winchTargetKind = "";
            if (!winchElectric) winchActive = false;
            Changed();
        }

        void BankDiver()
        {
            if (diver == null || diver.onboard) return;
            foreach (ResourceKind item in diver.backpack) AddResource(item, 1);
            int delivered = diver.backpack.Count; diver.backpack.Clear();
            diver.SetOnboard(true, ShipPoint(bankArrivalLocal));
            if (diverRope != null) diverRope.ResetRoute();
            diverFainted = false; diverHidden = false; diverSprinting = false; diverBites = 0; diverInvulnerable = 0;
            diver.oxygenMax = GetOxygenMax(); diver.oxygen = diver.oxygenMax;
            if (winchSignalAuthorized) { winchElectric = false; winchSignalAuthorized = false; }
            signalCount = 0;
            Tell("船长接回潜水员，" + delivered + "件物资已卸入共享仓库。" + (diver.downed ? "靠近倒地潜水员按交互进行救助。" : ""));
        }

        public void SignalCaptain()
        {
            signalCount = elapsed - lastSignalAt <= tuning.electricWinchSignalWindow ? signalCount + 1 : 1;
            lastSignalAt = elapsed;
            if (signalCount == 1) { winchCargo = null; winchTargetKind = ""; }
            Tell(signalCount == 1 ? "潜水员信号：请求船长收绳。" : signalCount == 2 ? "潜水员双信号：优先吊运已挂载货箱。" : "潜水员紧急三信号：请船长立即操作绞盘！");
            AttractEnemies(diver != null ? diver.Position : BoatAnchor);
            if (signalCount >= tuning.electricWinchSignalCount && IsElectricWinch && diver != null && !diver.onboard && circuitRepaired && (power > 0 || infinitePower))
            {
                winchCargo = null; winchTargetKind = ""; winchActive = false; winchElectric = true; winchSignalAuthorized = true;
                winchStartDistance = 0; winchProgress = 0; signalCount = 0;
                PrepareWinchTarget(); Tell("连续救生信号已触发电动绞盘，正在自动接回潜水员。"); Changed();
            }
        }

        public bool TryReviveTeammate(CrewActor actor)
        {
            if (actor == null || actor.downed || actor == diver && diverFainted) return false;
            CrewActor other = actor == captain ? diver : captain;
            if (other == null || !other.downed) return false;
            if (!CanReviveFrom(actor, other)) return false;
            int index = (int)actor.role;
            if (reviveTargets[index] != other) { reviveTargets[index] = other; reviveProgress[index] = 0; Tell("按住交互5秒，扶起" + GameNames.Role(other.role) + "；松开或离开会中断。"); }
            return true;
        }

        bool CanReviveFrom(CrewActor actor, CrewActor other)
        {
            bool nearby = actor.onboard == other.onboard && Vector3.Distance(actor.Position, other.Position) <= tuning.reviveDistance;
            // Do not deadlock when the captain is down on deck and the only conscious teammate is submerged.
            // The diver can assist from the nearby outer rail without being permitted to self-board.
            if (!nearby && actor == diver && !actor.onboard && other.onboard && ship != null)
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
                if (!reviveHeld[i] || actor == null || actor.downed || actor == diver && diverFainted || !target.downed || !CanReviveFrom(actor, target))
                { reviveTargets[i] = null; reviveProgress[i] = 0; continue; }
                reviveProgress[i] += dt;
                if (reviveProgress[i] >= tuning.reviveSeconds)
                {
                    target.Revive(); reviveTargets[i] = null; reviveProgress[i] = 0;
                    Tell(GameNames.Role(actor.role) + "扶起了" + GameNames.Role(target.role) + "。"); Changed();
                }
            }
            bool wipe = captain != null && diver != null && captain.downed && (diver.downed || diverFainted);
            crewWipeTimer = wipe ? crewWipeTimer + dt : 0;
            if (crewWipeTimer < crewWipeDelay) return;
            crewWipeTimer = 0;
            foreach (ResourceAmount slot in bank) slot.amount = 0;
            captain.backpack.Clear(); diver.backpack.Clear(); GetOrCreateCraftingManager().ClearQueue();
            if (world != null) foreach (LootPickup pickup in world.pickups) if (pickup != null && pickup.attached) { pickup.consumed = true; pickup.gameObject.SetActive(false); }
            captain.SetOnboard(true, ShipPoint(captainSpawnLocal)); diver.SetOnboard(true, ShipPoint(diverSpawnLocal));
            ResetLifelines();
            captain.controlsCaptured = diver.controlsCaptured = false;
            captain.Revive(); diver.Revive(); diverFainted = false; diverBites = 0;
            diver.oxygen = GetOxygenMax(); hull = Mathf.Max(hull, hullMax * .3f);
            winchActive = winchElectric = winchSignalAuthorized = false; winchCargo = null; winchTargetKind = ""; winchProgress = winchStartDistance = 0;
            if (ship != null && ship.helmOperator != null) ship.SetHelm(ship.helmOperator);
            Tell("两名船员在救生平台苏醒：仓库、背包、挂载货物与制作中的物资遗失；已安装设备与升级保留。"); Changed();
        }
    }
}
