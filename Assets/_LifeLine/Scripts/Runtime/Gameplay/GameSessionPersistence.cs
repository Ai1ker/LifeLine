using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Lifeline
{
    public partial class GameSession
    {
        static readonly Dictionary<(Type, Type), (FieldInfo From, FieldInfo To)[]> saveFieldMappings = new();

        static void CopySaveFields(object source, object destination)
        {
            var types = (source.GetType(), destination.GetType());
            if (!saveFieldMappings.TryGetValue(types, out var mapping))
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
                var fields = new List<(FieldInfo, FieldInfo)>();
                foreach (FieldInfo field in types.Item1.GetFields(flags))
                {
                    FieldInfo target = types.Item2.GetField(field.Name, flags);
                    if (target != null && target.FieldType == field.FieldType) fields.Add((field, target));
                }
                mapping = fields.ToArray();
                saveFieldMappings.Add(types, mapping);
            }
            foreach (var field in mapping) field.To.SetValue(destination, field.From.GetValue(source));
        }
        static T CloneSave<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        public GameSaveData CaptureSaveData(GameHUDController hud = null) => CaptureState(hud, true, false);

        GameSaveData CaptureState(GameHUDController hud, bool includeWorld, bool networkFrame)
        {
            if (!networkFrame) RefreshLifelines();
            var data = new GameSaveData
            {
                areaStrikes=areaStrikes.ToArray(), coordinateVersion = 1, balanceVersion=BalanceSaveMigration.CurrentVersion, equipmentProgressionVersion=1, routeDistance=tuning.routeDistance, savedUtc = DateTime.UtcNow.ToString("O"), state = new SessionSaveState(),
                cycleSeconds = tuning.daySeconds, daylightSeconds = tuning.daySeconds * tuning.dayFraction, duskSeconds = tuning.daySeconds * tuning.duskFraction,
                shipPosition = ship != null ? ShipToWaterPoint(ship.Position) : Vector3.zero,
                captainRopeContacts=captainRope!=null?captainRope.CaptureContacts():Array.Empty<Vector3>(),
                diverRopeContacts = diverRope != null ? diverRope.CaptureContacts() : Array.Empty<Vector3>(),
                cargoRopeContacts = cargoRope != null ? cargoRope.CaptureContacts() : Array.Empty<Vector3>()
            };
            CopySaveFields(this, data.state);
            TutorialProgress tutorial = GetComponent<TutorialProgress>();
            data.hasTutorial = tutorial != null;
            if (tutorial != null) data.tutorial = tutorial.Capture();
            data.captain = CaptureCrew(captain); data.diver = CaptureCrew(diver);
            CraftingManager manager = GetOrCreateCraftingManager(); data.nextJobId = manager.nextJobId;
            var work = new List<CraftSaveState>();
            foreach (CraftJob job in manager.jobs)
            {
                if (job == null || job.completed || job.recipe == null) continue;
                var saved = new CraftSaveState { recipeId = job.recipe.id }; CopySaveFields(job, saved); work.Add(saved);
            }
            data.jobs = work.ToArray();
            if (world != null && includeWorld)
            {
                data.world.forceCaveDay=world.forceCaveDay;
                data.world.forceFilterDay=world.forceFilterDay;
                data.world.hasHidingSpots = true;
                if (!networkFrame) data.world.hidingSpots = world.CaptureHideSpots();
                else data.world.hasHidingSpots = false;
                data.world.present = true; data.world.activeDay = world.activeDay; data.world.generatedSeed = world.generatedSeed;
                var pickups = new List<PickupSaveState>();
                string prefix = world.generatedSeed + ":" + world.activeDay + ":";
                foreach (LootPickup pickup in world.pickups)
                {
                    if (pickup == null) continue;
                    if (string.IsNullOrEmpty(pickup.saveId)) pickup.saveId = "dynamic:" + Guid.NewGuid().ToString("N");
                    pickups.Add(new PickupSaveState { id = pickup.saveId, dynamic = pickup.mazeIndex < 0 || !pickup.saveId.StartsWith(prefix, StringComparison.Ordinal), resource = pickup.resource, amount = pickup.amount, capacity = pickup.capacity, persistAcrossDays=pickup.persistAcrossDays, isLostCargo=pickup.isLostCargo, isCrate = pickup.isCrate, isCabinet = pickup.isCabinet, contents = pickup.contents, attached = pickup.attached, consumed = pickup.consumed, position = pickup.SavePosition, mazeIndex = pickup.mazeIndex, mazeCell = pickup.mazeCell, ropeContacts = !networkFrame && pickup.attached && !pickup.consumed ? GetCargoLifeline(pickup).CaptureContacts() : Array.Empty<Vector3>() });
                }
                data.world.pickups = pickups.ToArray();
                var gates = new List<GateSaveState>();
                foreach (TrapdoorGate gate in world.trapdoors) if (gate != null) gates.Add(new GateSaveState { id = gate.saveId, open = gate.open, destroyed = gate.destroyed });
                data.world.gates = gates.ToArray();
                data.state.winchCargoId = winchCargo != null ? winchCargo.saveId : "";
            }
            var enemyIds = new Dictionary<LifelineEnemy, string>(); var savedEnemies = new List<EnemySaveState>();
            foreach (LifelineEnemy enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                string id = "enemy:" + (networkFrame ? enemy.GetEntityId().ToString() : savedEnemies.Count.ToString());
                enemyIds.Add(enemy, id);
                var saved = new EnemySaveState { id = id, position = enemy.transform.position };
                CopySaveFields(enemy, saved);
                if (enemy.IsAirborne)
                {
                    saved.position = ShipToWaterPoint(saved.position); saved.home = ShipToWaterPoint(saved.home);
                    saved.spawnPoint = ShipToWaterPoint(saved.spawnPoint); saved.targetPoint = ShipToWaterPoint(saved.targetPoint);
                }
                savedEnemies.Add(saved);
            }
            data.enemies = savedEnemies.ToArray();
            var projectiles = new List<SpearSaveState>();
            foreach (LifelineSpear spear in FindObjectsByType<LifelineSpear>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (spear.session != this || spear.owner == null) continue;
                var saved = new SpearSaveState { id = networkFrame ? spear.GetEntityId().ToString() : null, owner = spear.owner.role, position = spear.transform.position };
                CopySaveFields(spear, saved);
                if (spear.onShipMap) saved.position = ShipToWaterPoint(saved.position);
                if (spear.target != null && enemyIds.TryGetValue(spear.target, out string targetId)) saved.targetId = targetId;
                LifelineEnemy parent = spear.transform.parent != null ? spear.transform.parent.GetComponentInParent<LifelineEnemy>() : null;
                if (parent != null && enemyIds.TryGetValue(parent, out string parentId)) saved.parentEnemyId = parentId;
                projectiles.Add(saved);
            }
            data.spears = projectiles.ToArray();
            if (hud != null) data.repairUI = new RepairUISaveState { present = true, filterPuzzle = hud.filterPuzzle, circuitConnected = hud.circuitConnected };
            if (networkFrame) return data; // Encoded immediately on the Unity thread.
            data = CloneSave(data); // snapshots never retain mutable references into the live session.
            GameSaveValidation.NormalizeUninitializedPuzzle(data.repairUI);
            return data;
        }
        CrewSaveState CaptureCrew(CrewActor actor)
        {
            if (actor == null) return null;
            return new CrewSaveState { role = actor.role, onboard = actor.onboard, downed = actor.downed,hidden=IsHidden(actor),fainted=IsFainted(actor),hidingPosition=actor.hidingPosition, position = actor.onboard && ship != null ? ship.transform.InverseTransformPoint(actor.Position) : actor.Position, velocity = actor.body != null ? actor.body.linearVelocity : Vector3.zero, facing = actor.Facing, health = actor.health, maxHealth = actor.maxHealth, oxygen = actor.oxygen, oxygenMax = actor.oxygenMax, backpack = actor.backpack };
        }
        public bool TryRestoreSaveData(GameSaveData data, out string error, GameHUDController hud = null)
        {
            error = "";
            if (!GameSaveValidation.Validate(data, out error)) return false;
            if (tuning == null || captain == null || diver == null || ship == null) { error = "场景缺少读档所需的角色、船舶或调参资产。"; return false; }
            if (!SaveClock.TryMap(data, tuning, out float restoredElapsed)) { error = "存档日期与当前昼夜配置不兼容。"; return false; }
            CraftingManager manager = GetOrCreateCraftingManager();
            int repairJobs = 0, workbenchJobs = 0;
            foreach (CraftSaveState job in data.jobs)
            {
                var recipe = manager.FindRecipe(job.recipeId);
                if (recipe == null) { error = "存档配方不存在：" + job.recipeId; return false; }
                if (recipe.repairAmount > 0) repairJobs++;
                else workbenchJobs++;
                if (job.quantity > 1 && (recipe.repairAmount > 0 || !recipe.allowBatch))
                { error = "存档批量作业配方无效。"; return false; }
            }
            if (repairJobs > 1 || workbenchJobs > (data.state.upgradeLevels[(int)UpgradeKind.Workbench] >= 3 ? 2 : 1))
            { error = "存档设备作业槽数量无效。"; return false; }
            if (data.world.present && (world == null || data.world.activeDay < 1 || WorldGenerator.ComputeDaySeed(data.state.worldSeed, data.world.activeDay, world.dailySeedStride) != data.world.generatedSeed))
            { error = "存档世界种子与当前地图配置不兼容。"; return false; }
            // All semantic checks above happen before replacing the active world or its inventory.
            // JsonUtility embeds inline serializable classes and can inflate null into an empty
            // object. Preserve explicit presence separately so a legacy save skips basic lessons.
            bool tutorialPresent = data.hasTutorial && data.tutorial != null;
            data = CloneSave(data);
            if (!tutorialPresent) data.tutorial = null;
            bool migratedBalance=BalanceSaveMigration.Upgrade(data,tuning);
            UpgradeEquipmentSave(data);
            float clockOffset = restoredElapsed - data.state.elapsed;
            data.state.elapsed = restoredElapsed;
            data.state.lastCrankAt += clockOffset; data.state.lastSignalAt += clockOffset;
            GameSaveValidation.NormalizeUninitializedPuzzle(data.repairUI);
            phase = SessionPhase.Paused; Time.timeScale = 0; hud?.CloseTransientPanels();
            try
            {
                ClearSavedCombat(); ClearAreaStrikes();
                ship.session = this; ship.Initialize(this);
                Vector3 shipPosition = WaterToShipPoint(data.shipPosition);
                ship.body.position = shipPosition; ship.transform.position = shipPosition; ship.speed = 0;
                captain.Initialize(this, CrewRole.Captain, ShipPoint(captainSpawnLocal)); diver.Initialize(this, CrewRole.Diver, ShipPoint(diverSpawnLocal));
                CopySaveFields(data.state, this); Current = this;
                int ropeTier=Mathf.Min(GetUpgrade(UpgradeKind.Rope),tuning.ropeLengths.Length-1);
                float[] oldRope={20,26,34,54};if(ropeTier<oldRope.Length&&Mathf.Approximately(ropeLength,oldRope[ropeTier]))ropeLength=tuning.ropeLengths[ropeTier];
                manager.ResetQueue(); manager.nextJobId = data.nextJobId;
                foreach (CraftSaveState saved in data.jobs)
                {
                    var job = new CraftJob { recipe = manager.FindRecipe(saved.recipeId) };
                    CopySaveFields(saved, job); job.quantity = Mathf.Max(1, job.quantity); manager.jobs.Add(job);
                }
                if (data.world.present)
                {
                    foreach (LootPickup old in world.pickups.ToArray())
                        if (old != null && (world.generatedRoot == null || !old.transform.IsChildOf(world.generatedRoot))) RemoveSavedObject(old.gameObject);
                    world.session = this; world.tuning = tuning;world.forceCaveDay=data.world.forceCaveDay;world.forceFilterDay=data.world.forceFilterDay;
                    world.Generate(worldSeed, data.world.activeDay);
                    RestoreWorld(data.world);
                    if (data.world.hasHidingSpots) world.RestoreHideSpots(data.world.hidingSpots);
                    else
                        foreach (var savedCrew in new[] { data.captain, data.diver })
                            if (savedCrew.hidden && !savedCrew.onboard) world.PreserveLegacyHideLocation(savedCrew.position);
                }
                RestoreCrew(captain, data.captain); RestoreCrew(diver, data.diver); IgnoreCrewCollision();
                if(migratedBalance&&world!=null)world.ResolveCrewRefreshOverlap();
                if(data.areaStrikes!=null)areaStrikes.AddRange(data.areaStrikes);
                var enemyMap = new Dictionary<string, LifelineEnemy>();
                foreach (EnemySaveState saved in data.enemies)
                {
                    bool shipMap = saved.kind == EnemyKind.Bird || saved.kind == EnemyKind.ArmoredBird;
                    if (shipMap)
                    {
                        saved.position = WaterToShipPoint(saved.position); saved.home = WaterToShipPoint(saved.home);
                        saved.spawnPoint = WaterToShipPoint(saved.spawnPoint); saved.targetPoint = WaterToShipPoint(saved.targetPoint);
                    }
                    LifelineEnemy enemy = SpawnEnemy(saved.kind, saved.position, true);
                    if (enemy == null) throw new InvalidOperationException("存档敌人位置或数量与当前配置不兼容。");
                    float configuredHealth = enemy.maxHealth;
                    CopySaveFields(saved, enemy);
                    if (saved.kind == EnemyKind.Shadow)
                    {
                        enemy.maxHealth = configuredHealth;
                        enemy.health = Mathf.Clamp(configuredHealth - saved.shadowHitCount, 1, configuredHealth);
                    }
                    else if (enemy.IsAquaticHunter && saved.maxHealth < configuredHealth)
                    {
                        enemy.maxHealth = configuredHealth;
                        enemy.health = Mathf.Clamp(configuredHealth - (saved.maxHealth - saved.health), 1, configuredHealth);
                        if (enemy.damagedRetreat) enemy.targetPoint = enemy.transform.position;
                    }
                    enemyMap.Add(saved.id, enemy);
                }
                if (world != null && world.activeDay < day && HasLivingShadow) shadowWorldRefreshPending = true;
                foreach (SpearSaveState saved in data.spears)
                {
                    // Old saves had one map. A lodged/targeted projectile follows its enemy;
                    // otherwise the owner's saved location identifies its map.
                    if (data.coordinateVersion == 0)
                    {
                        LifelineEnemy reference = null;
                        if (!string.IsNullOrEmpty(saved.parentEnemyId)) enemyMap.TryGetValue(saved.parentEnemyId, out reference);
                        if (reference == null && !string.IsNullOrEmpty(saved.targetId)) enemyMap.TryGetValue(saved.targetId, out reference);
                        saved.onShipMap = reference != null ? reference.IsAirborne : saved.owner == CrewRole.Captain ? data.captain.onboard : data.diver.onboard;
                    }
                    RestoreSpear(saved, enemyMap);
                }
                winchCargo = null;
                if (world != null && !string.IsNullOrEmpty(data.state.winchCargoId)) winchCargo = world.pickups.Find(pickup => pickup != null && pickup.saveId == data.state.winchCargoId);
                RestoreLifelines(data.diverRopeContacts, data.cargoRopeContacts);
                if(!captain.onboard)GetCrewLifeline(captain).RestoreContacts(BoatAnchor,captain.Position,data.captainRopeContacts);
                if (world != null) foreach (var savedPickup in data.world.pickups)
                    if (savedPickup.attached && !savedPickup.consumed && savedPickup.ropeContacts != null && savedPickup.ropeContacts.Length > 0)
                    {
                        var cargo = world.pickups.Find(p => p != null && p.saveId == savedPickup.id);
                        if (cargo != null) GetCargoLifeline(cargo).RestoreContacts(BoatAnchor, cargo.transform.position, savedPickup.ropeContacts);
                    }
                winchActive = false; diverSprinting = false;captainBoardProgress=0;captainBoardHeld=false;
                reviveHeld = new bool[2]; reviveTargets = new CrewActor[2]; reviveProgress = new float[2];
                if (hud != null && data.repairUI != null && data.repairUI.present) { hud.filterPuzzle = data.repairUI.filterPuzzle; hud.circuitConnected = data.repairUI.circuitConnected; }
                RefreshEquipmentVisibility();
                TutorialProgress tutorial = GetComponent<TutorialProgress>();
                if (tutorial == null) tutorial = gameObject.AddComponent<TutorialProgress>();
                tutorial.Restore(data.tutorial);
                previousDay = day; initialized = true; notifyClock = 0;
                Physics.SyncTransforms();
                phase = SessionPhase.Playing; Time.timeScale = 1; hud?.SetPause(false); UpdateQuest(); Changed(); SessionReady?.Invoke(true); return true;
            }
            catch (Exception exception)
            {
                phase = SessionPhase.Paused; Time.timeScale = 0; error = "恢复存档失败：" + exception.Message;
                Tell(error); return false; // Never turn a failed Continue into a fresh game or autosave it.
            }
        }
        void RestoreCrew(CrewActor actor, CrewSaveState saved)
        {
            actor.maxHealth = saved.maxHealth; actor.health = saved.health; actor.oxygenMax = saved.oxygenMax; actor.oxygen = saved.oxygen;
            if(actor==diver)
            {
                float[] oldOxygen={40,70,100,130};int tier=Mathf.Min(GetUpgrade(UpgradeKind.Oxygen),oldOxygen.Length-1);
                float bonus=diverBuffs.oxygen>0?1+tuning.fishOxygenBonus:1;
                if(Mathf.Approximately(actor.oxygenMax,oldOxygen[tier]*bonus))
                {
                    float newMax=GetOxygenMax();actor.oxygen=Mathf.Min(newMax,actor.oxygen+Mathf.Max(0,newMax-actor.oxygenMax));actor.oxygenMax=newMax;
                }
            }
            actor.backpack = saved.backpack;
            actor.RestorePose(saved.onboard, saved.onboard ? ShipPoint(saved.position) : saved.position, saved.velocity, saved.facing);
            actor.SetDowned(saved.downed);actor.fainted=actor==captain&&saved.fainted;if(actor==diver)diverFainted|=saved.fainted;
            if(actor==captain){actor.oxygenMax=GetOxygenMax(actor);actor.oxygen=Mathf.Min(actor.oxygen,actor.oxygenMax);}
            bool restoreHidden=saved.hidden||actor==diver&&diverHidden;
            if(actor==diver)diverHidden=false;
            if(restoreHidden&&!actor.onboard&&world!=null&&world.IsAtHideSpot(actor)){actor.SetHidden(true);actor.hidingPosition=actor.Position;}
            EnsureCaptainLifelineView();
        }
        void RestoreWorld(WorldSaveState saved, bool onlyChanged = false)
        {
            var current = new Dictionary<string, LootPickup>();
            foreach (LootPickup pickup in world.pickups) if (pickup != null && !string.IsNullOrEmpty(pickup.saveId)) current.Add(pickup.saveId, pickup);
            var restored = new HashSet<string>();
            foreach (PickupSaveState state in saved.pickups)
            {
                restored.Add(state.id);
                if (onlyChanged && current.TryGetValue(state.id, out var existing) &&
                    existing.consumed == state.consumed && existing.attached == state.attached &&
                    existing.amount == state.amount && existing.capacity == state.capacity &&
                    existing.resource == state.resource && existing.contents.SequenceEqual(state.contents))
                {
                    if (existing.SavePosition != state.position)
                    {
                        existing.transform.position = state.position;
                        if (existing.IsSwimmingFish) world.PrepareRoomFish(existing);
                    }
                    continue;
                }
                if (!current.TryGetValue(state.id, out LootPickup pickup))
                {
                    if (!state.dynamic) throw new InvalidOperationException("存档物资ID与当前地图生成器不兼容：" + state.id);
                    GameObject prefab = state.isCrate ? world.cratePrefab : world.lootPrefab;
                    GameObject root = prefab != null ? Instantiate(prefab, state.position, Quaternion.identity, world.transform) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                    root.name = "Restored cargo " + state.id; root.transform.SetParent(world.transform, true);
                    pickup = root.GetComponent<LootPickup>(); if (pickup == null) pickup = root.AddComponent<LootPickup>();
                    Collider collider = root.GetComponent<Collider>(); if (collider == null) collider = root.AddComponent<BoxCollider>(); collider.isTrigger = true;
                    world.pickups.Add(pickup);
                }
                pickup.world = world; pickup.saveId = state.id; pickup.resource = state.resource; pickup.amount = state.amount; pickup.capacity = state.capacity;
                pickup.persistAcrossDays=state.persistAcrossDays;pickup.isLostCargo=state.isLostCargo; pickup.isCrate = state.isCrate; pickup.isCabinet = state.isCabinet; pickup.attached = state.attached; pickup.consumed = state.consumed;
                pickup.mazeIndex = state.mazeIndex; pickup.mazeCell = state.mazeCell; pickup.contents = state.contents; pickup.transform.position = state.position;
                pickup.gameObject.SetActive(!state.consumed); world.PrepareRestoredPickup(pickup);
            }
            foreach (LootPickup pickup in current.Values) if (!restored.Contains(pickup.saveId)) { pickup.consumed = true; pickup.gameObject.SetActive(false); }
            var gates = new Dictionary<string, TrapdoorGate>();
            foreach (TrapdoorGate gate in world.trapdoors) if (gate != null) gates.Add(gate.saveId, gate);
            foreach (GateSaveState state in saved.gates)
            {
                if (!gates.TryGetValue(state.id, out TrapdoorGate gate)) throw new InvalidOperationException("存档门ID与当前地图生成器不兼容：" + state.id);
                if (onlyChanged && gate.destroyed == state.destroyed && gate.open == state.open) continue;
                gate.destroyed = state.destroyed;
                gate.SetOpen(state.open);
            }
        }
        LifelineSpear RestoreSpear(SpearSaveState saved, Dictionary<string, LifelineEnemy> enemyMap)
        {
            GameObject root = spearPrefab != null ? Instantiate(spearPrefab) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "Restored projectile"; root.transform.position = saved.onShipMap ? WaterToShipPoint(saved.position) : saved.position;
            maps?.MoveGameplayObject(root, saved.onShipMap);
            if (spearPrefab == null) root.transform.localScale = new Vector3(.07f, .55f, .07f);
            Collider collider = root.GetComponent<Collider>(); if (collider != null) collider.isTrigger = true;
            LifelineSpear spear = root.GetComponent<LifelineSpear>(); if (spear == null) spear = root.AddComponent<LifelineSpear>();
            CopySaveFields(saved, spear); if(spear.maximumRange<=0)spear.maximumRange=10;
            if(!spear.turretShot && spear.GetComponent<SpearTetherView>()==null)spear.gameObject.AddComponent<SpearTetherView>().spear=spear; spear.session = this; spear.owner = saved.owner == CrewRole.Captain ? captain : diver;
            if (!string.IsNullOrEmpty(saved.targetId)) spear.target = enemyMap[saved.targetId];
            if (!string.IsNullOrEmpty(saved.parentEnemyId)) root.transform.SetParent(enemyMap[saved.parentEnemyId].transform, true);
            if (spear.direction.sqrMagnitude > .001f) root.transform.rotation = Quaternion.FromToRotation(Vector3.up, spear.direction.normalized);
            if (!spear.turretShot) activeSpears[(int)spear.owner.role] = spear;
            return spear;
        }
        void ClearSavedCombat()
        {
            foreach (LifelineSpear spear in FindObjectsByType<LifelineSpear>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (spear.session == this) RemoveSavedObject(spear.gameObject);
            activeSpears = new LifelineSpear[2];
            foreach (LifelineEnemy enemy in enemies.ToArray()) if (enemy != null) RemoveSavedObject(enemy.gameObject);
            enemies.Clear();
        }
        static void RemoveSavedObject(GameObject root)
        {
            root.SetActive(false); if (Application.isPlaying) Destroy(root); else DestroyImmediate(root);
        }
    }
}
