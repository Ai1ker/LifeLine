using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    public partial class GameSession
    {
        readonly Dictionary<string, LifelineEnemy> replicaEnemies = new();
        readonly Dictionary<string, LifelineSpear> replicaSpears = new();
        bool replicaCombatInitialized;

        public OnlineWorldFrame CaptureOnlineFrame(GameHUDController hud, bool baseline, bool includeWorld)
        {
            return new OnlineWorldFrame
            {
                baseline = baseline,
                phase = phase,
                state = baseline ? CaptureSaveData(hud) : CaptureState(hud, includeWorld, true),
                winchActive = winchActive,
                winchFeedback = winchFeedback,
                winchFeedbackTime = winchFeedbackTime,
                boarding = captainBoardHeld,
                boardingProgress = captainBoardProgress,
                reviveProgress = reviveProgress,
                reviving = new[] { reviveTargets[0] != null, reviveTargets[1] != null },
                helmsman = ship.helmOperator != null ? (int)ship.helmOperator.role : -1
            };
        }

        public void FreezeOnlineReplica()
        {
            enabled = false;
            ship.enabled = false;
            foreach (var actor in Crew)
            {
                actor.enabled = false;
                actor.body.isKinematic = true;
                actor.body.interpolation = RigidbodyInterpolation.None;
            }
            foreach (var enemy in enemies) FreezeReplicaEnemy(enemy);
            foreach (var spear in FindObjectsByType<LifelineSpear>())
                if (spear.session == this) spear.enabled = false;
            var tutorial = GetComponent<TutorialProgress>();
            if (tutorial != null) tutorial.enabled = false;
        }

        void FreezeReplicaEnemy(LifelineEnemy enemy)
        {
            if (enemy == null) return;
            enemy.enabled = false;
            if (enemy.crewCollision != null) enemy.crewCollision.enabled = false;
        }

        public void ResetOnlineReplicaCombat()
        {
            replicaEnemies.Clear();
            replicaSpears.Clear();
            replicaCombatInitialized = false;
        }

        public void ApplyOnlineFrame(OnlineWorldFrame frame, GameHUDController hud)
        {
            var data = frame.state;
            CopySaveFields(data.state, this);
            ship.body.position = ship.transform.position = WaterToShipPoint(data.shipPosition);
            if (data.world != null && data.world.present)
            {
                if (world.generatedSeed != data.world.generatedSeed || world.activeDay != data.world.activeDay)
                {
                    foreach (var old in world.pickups)
                        if (old != null && (world.generatedRoot == null || !old.transform.IsChildOf(world.generatedRoot)))
                            RemoveSavedObject(old.gameObject);
                    world.forceCaveDay = data.world.forceCaveDay;
                    world.forceFilterDay = data.world.forceFilterDay;
                    world.Generate(worldSeed);
                }
                RestoreWorld(data.world);
            }
            ApplyOnlineCrewPose(captain, data.captain);
            ApplyOnlineCrewPose(diver, data.diver);
            ship.helmOperator = frame.helmsman == (int)CrewRole.Captain ? captain
                : frame.helmsman == (int)CrewRole.Diver ? diver : null;
            if (data.repairUI != null && data.repairUI.present)
            {
                hud.circuitConnected = data.repairUI.circuitConnected;
                hud.filterPuzzle = data.repairUI.filterPuzzle;
            }
            var manager = GetOrCreateCraftingManager();
            manager.ResetQueue();
            manager.nextJobId = data.nextJobId;
            foreach (var job in data.jobs)
            {
                var recipe = manager.FindRecipe(job.recipeId);
                if (recipe == null) continue;
                var live = new CraftJob { recipe = recipe };
                CopySaveFields(job, live);
                manager.jobs.Add(live);
            }
            ApplyOnlineCombat(data);
            if (data.hasTutorial) GetComponent<TutorialProgress>()?.Restore(data.tutorial);
            winchActive = frame.winchActive;
            winchFeedback = frame.winchFeedback ?? "";
            winchFeedbackTime = frame.winchFeedbackTime;
            captainBoardHeld = frame.boarding;
            captainBoardProgress = frame.boardingProgress;
            if (frame.reviveProgress?.Length == 2) reviveProgress = frame.reviveProgress;
            reviveTargets[0] = frame.reviving?[0] == true ? diver : null;
            reviveTargets[1] = frame.reviving?[1] == true ? captain : null;
            phase = frame.phase;
            RefreshEquipmentVisibility();
            UpdateQuest();
            Changed();
        }

        void ApplyOnlineCrewPose(CrewActor actor, CrewSaveState saved)
        {
            var position = saved.onboard ? ShipPoint(saved.position) : saved.position;
            if (actor.onboard != saved.onboard) actor.SetOnboard(saved.onboard, position);
            actor.ApplyReplicatedPose(position, saved.facing);
            actor.oxygen = saved.oxygen;
            actor.oxygenMax = saved.oxygenMax;
            actor.health = saved.health;
            actor.maxHealth = saved.maxHealth;
            actor.backpack = saved.backpack;
            actor.SetDowned(saved.downed);
            actor.fainted = saved.fainted;
            actor.SetHidden(saved.hidden);
        }

        void ApplyOnlineCombat(GameSaveData data)
        {
            if (!replicaCombatInitialized)
            {
                ClearSavedCombat();
                replicaCombatInitialized = true;
            }
            var seen = new HashSet<string>();
            foreach (var saved in data.enemies)
            {
                seen.Add(saved.id);
                var position = saved.kind == EnemyKind.Bird || saved.kind == EnemyKind.ArmoredBird
                    ? WaterToShipPoint(saved.position) : saved.position;
                if (!replicaEnemies.TryGetValue(saved.id, out var enemy) || enemy == null)
                {
                    enemy = SpawnEnemy(saved.kind, position, true);
                    if (enemy == null) continue;
                    FreezeReplicaEnemy(enemy);
                    replicaEnemies[saved.id] = enemy;
                }
                enemy.transform.position = position;
                CopySaveFields(saved, enemy);
                if (enemy.visualRoot != null && Mathf.Abs(saved.facing.x) > .01f)
                    enemy.visualRoot.localRotation = Quaternion.Euler(0, saved.facing.x < 0 ? 180 : 0, 0);
            }
            foreach (var id in new List<string>(replicaEnemies.Keys))
                if (!seen.Contains(id))
                {
                    var enemy = replicaEnemies[id];
                    if (enemy != null) { enemies.Remove(enemy); RemoveSavedObject(enemy.gameObject); }
                    replicaEnemies.Remove(id);
                }
            var shots = new HashSet<string>();
            foreach (var saved in data.spears)
            {
                if ((!string.IsNullOrEmpty(saved.targetId) && !replicaEnemies.ContainsKey(saved.targetId)) ||
                    (!string.IsNullOrEmpty(saved.parentEnemyId) && !replicaEnemies.ContainsKey(saved.parentEnemyId))) continue;
                shots.Add(saved.id);
                if (!replicaSpears.TryGetValue(saved.id, out var spear) || spear == null)
                {
                    spear = RestoreSpear(saved, replicaEnemies);
                    spear.enabled = false;
                    replicaSpears[saved.id] = spear;
                }
                spear.transform.position = saved.onShipMap ? WaterToShipPoint(saved.position) : saved.position;
                CopySaveFields(saved, spear);
                if (saved.direction.sqrMagnitude > .001f)
                    spear.transform.rotation = Quaternion.FromToRotation(Vector3.up, saved.direction.normalized);
            }
            foreach (var id in new List<string>(replicaSpears.Keys))
                if (!shots.Contains(id))
                {
                    var spear = replicaSpears[id];
                    if (spear != null) RemoveSavedObject(spear.gameObject);
                    replicaSpears.Remove(id);
                }
            // Warning views are recreated only when the set of strikes changes.
            var incoming = data.areaStrikes ?? Array.Empty<AreaStrike>();
            bool same = incoming.Length == areaStrikes.Count;
            for (int i = 0; same && i < incoming.Length; i++)
                same = incoming[i].kind == areaStrikes[i].kind && incoming[i].point == areaStrikes[i].point
                    && incoming[i].remaining <= areaStrikes[i].remaining + .02f;
            if (!same)
            {
                ClearAreaStrikes();
                areaStrikes.AddRange(incoming);
            }
            for (int i = 0; i < incoming.Length; i++)
            {
                areaStrikes[i].remaining = incoming[i].remaining;
                EnsureWarning(areaStrikes[i]);
            }
        }
    }
}
