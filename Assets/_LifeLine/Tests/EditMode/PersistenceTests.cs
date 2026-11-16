using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Lifeline.Tests
{
    public sealed class PersistenceTests
    {
        GameObject root;
        GameSession game, previous;
        GameTuning tuning;
        RecipeDefinition recipe;
        GameSaveManager saves;
        string directory, path;
        float previousScale;
        UnityEngine.Random.State previousRandom;
        [SetUp] public void SetUp()
        {
            previous = GameSession.Current; previousScale = Time.timeScale; previousRandom = UnityEngine.Random.state;
            directory = Path.Combine(Path.GetTempPath(), "LifeLineSaveTests", Guid.NewGuid().ToString("N")); path = Path.Combine(directory, "save-v1.json");
            root = new GameObject("Isolated persistence fixture");
            game = root.AddComponent<GameSession>(); game.autoStart = false;
            tuning = ScriptableObject.CreateInstance<GameTuning>(); tuning.maximumSharks = 0; game.tuning = tuning;
            var boat = new GameObject("Ship"); boat.transform.SetParent(root.transform); boat.transform.position = new Vector3(1000, 0, 0);
            boat.AddComponent<Rigidbody>(); game.ship = boat.AddComponent<ShipMotor>();
            game.captain = CreateCrew(CrewRole.Captain); game.diver = CreateCrew(CrewRole.Diver);
            recipe = ScriptableObject.CreateInstance<RecipeDefinition>(); recipe.id = "persist.plank"; recipe.displayName = "Test plank";
            recipe.cost = new[] { new ResourceAmount(ResourceKind.Wood, 2) }; recipe.output = new[] { new ResourceAmount(ResourceKind.Plank, 1) };
            game.recipes = new[] { recipe }; game.Initialize();
            saves = root.AddComponent<GameSaveManager>(); saves.bootOnStart = false; saves.session = game; saves.pathOverride = path;
            GameSaveManager.RequestNewGame();
        }
        CrewActor CreateCrew(CrewRole role)
        {
            var actor = new GameObject(role.ToString()); actor.transform.SetParent(root.transform); actor.AddComponent<Rigidbody>(); actor.AddComponent<CapsuleCollider>();
            var crew = actor.AddComponent<CrewActor>(); crew.enabled = false; return crew;
        }
        [TearDown] public void TearDown()
        {
            GameSaveManager.RequestNewGame();
            if (game != null)
            {
                foreach (LifelineSpear spear in UnityEngine.Object.FindObjectsByType<LifelineSpear>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (spear.session == game) UnityEngine.Object.DestroyImmediate(spear.gameObject);
                foreach (LifelineEnemy enemy in game.enemies.ToArray()) if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy.gameObject);
            }
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(recipe); UnityEngine.Object.DestroyImmediate(tuning);
            GameSession.RestoreCurrentAfterValidation(previous); Time.timeScale = previousScale; UnityEngine.Random.state = previousRandom;
            string allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "LifeLineSaveTests")) + Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(directory);
            if (!target.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Refusing to delete a test directory outside LifeLineSaveTests.");
            if (Directory.Exists(target)) Directory.Delete(target, true);
        }
        [Test] public void FullRoundTripRestoresCrewEconomyJobsFaultsBuffsAndPuzzleWithoutNewGameReset()
        {
            game.AddResource(ResourceKind.Wood, 7); game.AddResource(ResourceKind.Metal, 9);
            game.upgradeLevels[(int)UpgradeKind.Workbench] = 3;
            Assert.That(game.TryCraft(recipe.id, game.captain)); Assert.That(game.TryCraft(recipe.id, game.diver));
            game.jobs[0].elapsed = 2; game.jobs[0].progress = 8; game.jobs[0].nextCue = 3; game.jobs[0].noteDirection = 2; game.jobs[0].attempted = true; game.jobs[0].hits = 2;
            game.hull = 77; game.power = 34; game.engineEverRepaired = true; game.engineRepairVersion = 3;
            game.engineFaultClean[2] = 1; game.engineFaultFixed[2] = true; game.repairAuthorized[(int)StationKind.Engine] = true;
            game.captainBuffs.move = 23; game.diverBuffs.attack = 41; game.radioStage = 2;
            game.ship.body.position = new Vector3(1037, 0, 0); game.ship.transform.position = game.ship.body.position;
            game.captain.RestorePose(true, game.ShipPoint(new Vector3(-2, 1, 0)), new Vector3(0, 2, 0), Vector2.left);
            game.diver.RestorePose(false, game.BoatAnchor + Vector3.down * 10, Vector3.down, Vector2.down);
            game.diver.oxygen = 18; game.diver.health = 62; game.diver.backpack.Add(ResourceKind.Fiber); game.diver.backpack.Add(ResourceKind.Metal);
            var hud = root.AddComponent<GameHUDController>(); hud.enabled = false; hud.filterPuzzle.Generate(17); hud.filterPuzzle.RotateTile(0); hud.circuitConnected[1] = true;
            GameSaveData data = game.CaptureSaveData(hud); int wood = game.GetResource(ResourceKind.Wood); int nextJob = game.craftingManager.nextJobId;
            Assert.That(GameSaveStore.TryWrite(path, data, out string fileError), Is.True, fileError);
            Assert.That(GameSaveStore.TryRead(path, out data, out fileError), Is.True, fileError);
            game.Initialize(); hud.circuitConnected[1] = false;
            Assert.That(game.TryRestoreSaveData(data, out string error, hud), Is.True, error);
            Assert.That(game.GetResource(ResourceKind.Wood), Is.EqualTo(wood)); Assert.That(game.GetResource(ResourceKind.Metal), Is.EqualTo(9));
            Assert.That(game.jobs.Count, Is.EqualTo(2)); Assert.That(game.jobs[0].recipe, Is.SameAs(recipe)); Assert.That(game.jobs[0].progress, Is.EqualTo(8));
            Assert.That(game.jobs[0].attempted, Is.True); Assert.That(game.craftingManager.nextJobId, Is.EqualTo(nextJob));
            Assert.That(game.diver.onboard, Is.False); Assert.That(game.diver.oxygen, Is.EqualTo(18)); Assert.That(game.diver.health, Is.EqualTo(62));
            Assert.That(game.diver.backpack, Is.EqualTo(new[] { ResourceKind.Fiber, ResourceKind.Metal })); Assert.That(game.captain.Facing, Is.EqualTo(Vector2.left));
            Assert.That(game.ship.Position.x, Is.EqualTo(1037)); Assert.That(game.engineRepairVersion, Is.EqualTo(3)); Assert.That(game.engineFaultFixed[2]);
            Assert.That(game.repairAuthorized[(int)StationKind.Engine]); Assert.That(game.captainBuffs.move, Is.EqualTo(23)); Assert.That(game.diverBuffs.attack, Is.EqualTo(41));
            Assert.That(hud.filterPuzzle.masks, Is.EqualTo(data.repairUI.filterPuzzle.masks)); Assert.That(hud.circuitConnected[1]);
            game.StartGame(); Assert.That(game.power, Is.EqualTo(34)); Assert.That(game.hull, Is.EqualTo(77)); Assert.That(game.jobs.Count, Is.EqualTo(2));
            data.state.bank[0].amount = 999; Assert.That(game.GetResource(ResourceKind.Wood), Is.EqualTo(wood), "loaded state owns detached copies");
        }
        [Test] public void SameDayAutoSaveDoesNotRepeatAndManualOverwritePreventsLaterAutoOverwrite()
        {
            Assert.That(saves.TryAutoSave(out string error), Is.True, error); string first = File.ReadAllText(path);
            game.power = 19; Assert.That(saves.TryAutoSave(out _), Is.False); Assert.That(File.ReadAllText(path), Is.EqualTo(first));
            game.Pause(true); game.power = 52;
            Assert.That(saves.TrySave(game, SaveReason.Manual, out error), Is.True, error); Assert.That(File.Exists(path + ".bak"));
            Assert.That(GameSaveStore.TryRead(path, out GameSaveData manual, out error), Is.True, error); Assert.That(manual.state.power, Is.EqualTo(52));
            game.Pause(false); Assert.That(saves.TryAutoSave(out _), Is.False);
            Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(first));
            game.elapsed = tuning.daySeconds + .1f; game.StepSimulation(.01f);
            Assert.That(saves.TryAutoSave(out error), Is.True, error); Assert.That(saves.lastAutoSavedDay, Is.EqualTo(2));
        }
        [Test] public void ManualBeforeFirstFrameAutoAndContinueBothPreserveSameDaySaveMarker()
        {
            game.Pause(true); game.power = 67;
            Assert.That(saves.TrySave(game, SaveReason.Manual, out string error), Is.True, error);
            game.Pause(false); Assert.That(saves.TryAutoSave(out _), Is.False);
            GameSaveManager.RequestContinue(); Assert.That(saves.BootSession(), Is.True, saves.LastError);
            Assert.That(game.power, Is.EqualTo(67)); Assert.That(game.GetResource(ResourceKind.Wood), Is.EqualTo(2));
            Assert.That(saves.TryAutoSave(out _), Is.False); Assert.That(game.day, Is.EqualTo(1));
        }
        [Test] public void InvalidWriteOrReadCannotReplaceValidSlotAndFailedContinueCannotStartFreshGame()
        {
            Assert.That(saves.TryAutoSave(out string error), Is.True, error); string valid = File.ReadAllText(path);
            GameSaveData bad = game.CaptureSaveData(); bad.state.power = float.NaN;
            Assert.That(GameSaveStore.TryWrite(path, bad, out error), Is.False); Assert.That(File.ReadAllText(path), Is.EqualTo(valid));
            File.WriteAllText(path, "{ broken"); game.AddResource(ResourceKind.Metal, 8); game.power = 47;
            GameSaveManager.RequestContinue(); Assert.That(saves.BootSession(), Is.False);
            Assert.That(game.phase, Is.EqualTo(SessionPhase.Paused)); Assert.That(game.GetResource(ResourceKind.Metal), Is.EqualTo(8)); Assert.That(game.power, Is.EqualTo(47));
            Assert.That(File.ReadAllText(path), Is.EqualTo("{ broken")); Assert.That(saves.LastError, Is.Not.Empty);
        }
        [Test] public void MissingRecipeIsRejectedBeforeChangingCurrentSession()
        {
            Assert.That(game.TryCraft(recipe.id, game.diver)); GameSaveData saved = game.CaptureSaveData(); saved.jobs[0].recipeId = "removed.recipe";
            int wood = game.GetResource(ResourceKind.Wood); game.power = 41;
            Assert.That(game.TryRestoreSaveData(saved, out string error), Is.False); Assert.That(error, Does.Contain("removed.recipe"));
            Assert.That(game.GetResource(ResourceKind.Wood), Is.EqualTo(wood)); Assert.That(game.power, Is.EqualTo(41)); Assert.That(game.phase, Is.EqualTo(SessionPhase.Playing));
        }
        [TestCase(false)] [TestCase(true)]
        public void UnopenedHudPuzzleAutoSavesThenGeneratesOnceAndPreservesRotatedProgress(bool sceneUsesEmptyArrays)
        {
            var hud = root.AddComponent<GameHUDController>(); hud.enabled = false; hud.session = game; saves.hud = hud;
            hud.filterPuzzle = new PipeRepairPuzzle
            {
                masks = sceneUsesEmptyArrays ? Array.Empty<int>() : null,
                solution = sceneUsesEmptyArrays ? Array.Empty<int>() : null
            };
            Assert.That(saves.TryAutoSave(out string error), Is.True, error);
            Assert.That(GameSaveStore.TryRead(path, out GameSaveData unopened, out error), Is.True, error);
            Assert.That(unopened.repairUI.present, Is.True, "exercise the actual HUD-bearing save, not a headless fixture");
            Assert.That(saves.TryLoad(game, out error), Is.True, error);
            Assert.That(hud.filterPuzzle.masks, Is.Null, "unused arrays must normalize for the existing panel's lazy generation");
            Assert.That(hud.filterPuzzle.solution, Is.Null);

            if (hud.filterPuzzle.masks == null) hud.filterPuzzle.Generate(game.worldSeed + 31);
            hud.filterPuzzle.RotateTile(0); int[] rotated = (int[])hud.filterPuzzle.masks.Clone();
            int[] solution = (int[])hud.filterPuzzle.solution.Clone();
            game.repairAuthorized[(int)StationKind.Filter] = true;
            game.Pause(true); Assert.That(saves.TrySave(game, SaveReason.Manual, out error), Is.True, error);
            Assert.That(saves.TryLoad(game, out error), Is.True, error);
            Assert.That(hud.filterPuzzle.masks, Is.EqualTo(rotated)); Assert.That(hud.filterPuzzle.solution, Is.EqualTo(solution));
            Assert.That(hud.filterPuzzle.turns, Is.EqualTo(1)); Assert.That(game.repairAuthorized[(int)StationKind.Filter], Is.True);

            string valid = File.ReadAllText(path); GameSaveData damaged = game.CaptureSaveData(hud);
            damaged.repairUI.filterPuzzle.masks = Array.Empty<int>(); // solution still contains a real generated route
            Assert.That(GameSaveStore.TryWrite(path, damaged, out error), Is.False, "half-generated progress must not be mistaken for unopened");
            Assert.That(File.ReadAllText(path), Is.EqualTo(valid));
        }
        [Test] public void EnemyTelegraphShadowHitsAndLodgedAlreadyHitSpearSurviveRoundTrip()
        {
            LifelineEnemy enemy = game.SpawnEnemy(EnemyKind.Shadow, game.BoatAnchor + Vector3.down * 8);
            enemy.state = EnemyState.Telegraph; enemy.stateTime = .8f; enemy.shadowHitCount = game.shadowHits = 7; enemy.health = 13;
            var projectile = new GameObject("Lodged spear"); projectile.transform.SetParent(enemy.transform); projectile.transform.position = enemy.transform.position;
            var spear = projectile.AddComponent<LifelineSpear>(); spear.session = game; spear.owner = game.diver; spear.hasHit = spear.lodged = true; spear.direction = Vector3.right; spear.age = 1;
            game.activeSpears[1] = spear;
            GameSaveData data = game.CaptureSaveData(); Assert.That(game.TryRestoreSaveData(data, out string error), Is.True, error);
            Assert.That(game.enemies.Count, Is.EqualTo(1)); Assert.That(game.enemies[0].state, Is.EqualTo(EnemyState.Telegraph)); Assert.That(game.enemies[0].stateTime, Is.EqualTo(.8f));
            Assert.That(game.shadowHits, Is.EqualTo(7)); Assert.That(game.enemies[0].shadowHitCount, Is.EqualTo(7));
            Assert.That(game.activeSpears[1].hasHit && game.activeSpears[1].lodged, Is.True); Assert.That(game.activeSpears[1].transform.parent, Is.EqualTo(game.enemies[0].transform));
        }
        [Test] public void WorldConsumedLootLoadedCargoLostBagAndOpenGateRestoreExactlyOnce()
        {
            var worldObject = new GameObject("World"); worldObject.transform.SetParent(root.transform);
            WorldGenerator world = worldObject.AddComponent<WorldGenerator>(); game.world = world; world.session = game; world.tuning = tuning;
            world.drawLabels = false; world.areaCenters = new[] { 0f }; world.generateCaves = false; world.cratesPerZone = 1; world.trapdoorChance = 1;
            tuning.shallowLootCount = tuning.middleLootCount = tuning.deepLootCount = 1; world.Generate(game.worldSeed);
            LootPickup consumed = world.pickups.First(pickup => !pickup.isCrate); consumed.consumed = true; consumed.amount = 0; consumed.gameObject.SetActive(false);
            LootPickup cargo = world.pickups.First(pickup => pickup.isCrate); cargo.contents.Add(ResourceKind.Fiber); cargo.contents.Add(ResourceKind.Metal); cargo.attached = true;
            var bagRoot = new GameObject("Unidentified lost bag"); bagRoot.transform.SetParent(world.transform); var bag = bagRoot.AddComponent<LootPickup>();
            bag.world = world; bag.isCrate = true; bag.contents.Add(ResourceKind.Fuel); world.pickups.Add(bag);
            Assert.That(world.trapdoors.Count, Is.GreaterThan(0)); world.trapdoors[0].SetOpen(true);
            GameSaveData data = game.CaptureSaveData(); string bagId = bag.saveId, cargoId = cargo.saveId, consumedId = consumed.saveId, gateId = world.trapdoors[0].saveId;
            Assert.That(game.TryRestoreSaveData(data, out string error), Is.True, error);
            Assert.That(world.pickups.Count(pickup => pickup.saveId == bagId), Is.EqualTo(1)); Assert.That(world.pickups.Single(pickup => pickup.saveId == bagId).contents, Is.EqualTo(new[] { ResourceKind.Fuel }));
            LootPickup loadedCargo = world.pickups.Single(pickup => pickup.saveId == cargoId); Assert.That(loadedCargo.attached); Assert.That(loadedCargo.contents, Is.EqualTo(new[] { ResourceKind.Fiber, ResourceKind.Metal }));
            Assert.That(world.pickups.Single(pickup => pickup.saveId == consumedId).consumed); Assert.That(world.trapdoors.Single(gate => gate.saveId == gateId).open);
            Assert.That(game.TryRestoreSaveData(data, out error), Is.True, error); Assert.That(world.pickups.Count(pickup => pickup.saveId == bagId), Is.EqualTo(1), "repeat load must not leave orphan dynamic cargo");
        }
    }
}
