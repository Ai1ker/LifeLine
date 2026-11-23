using System.Collections.Generic;
using System.Linq;
using Lifeline.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Lifeline.Tests
{
    public sealed class EquipmentProgressionTests
    {
        readonly List<Object> assets = new List<Object>();
        GameObject root;
        GameSession game, previous;
        GameTuning tuning;
        Random.State random;
        float scale;

        [SetUp] public void SetUp()
        {
            previous = GameSession.Current; random = Random.state; scale = Time.timeScale;
            root = new GameObject("Equipment progression fixture"); game = root.AddComponent<GameSession>();
            game.autoStart = false; game.enabled = false;
            tuning = Keep(ScriptableObject.CreateInstance<GameTuning>()); tuning.maximumSharks = 0; game.tuning = tuning;
            GameObject ship = Child("Ship", root.transform); game.ship = ship.AddComponent<ShipMotor>(); game.ship.enabled = false;
            game.captain = Actor("Captain"); game.diver = Actor("Diver");
            game.Initialize();
        }
        T Keep<T>(T item) where T : Object { assets.Add(item); return item; }
        GameObject Child(string name, Transform parent) { var item = new GameObject(name); item.transform.SetParent(parent); return item; }
        CrewActor Actor(string name) { var actor = Child(name, root.transform).AddComponent<CrewActor>(); actor.enabled = false; return actor; }
        RecipeDefinition Recipe(string id, string tool = "", params ResourceAmount[] cost)
        {
            var recipe = Keep(ScriptableObject.CreateInstance<RecipeDefinition>()); recipe.id = id; recipe.displayName = id; recipe.toolId = tool; recipe.cost = cost;
            game.recipes = game.recipes.Concat(new[] { recipe }).ToArray(); return recipe;
        }
        [TearDown] public void TearDown()
        {
            game.ClearAreaStrikes(); foreach (var enemy in game.enemies.ToArray()) if (enemy != null) Object.DestroyImmediate(enemy.gameObject);
            Object.DestroyImmediate(root); foreach (Object asset in assets) Object.DestroyImmediate(asset); assets.Clear();
            GameSession.RestoreCurrentAfterValidation(previous); Time.timeScale = scale; Random.state = random;
        }

        [Test] public void NewShipStartsAtSeventyOfOneHundredWithNoPowerOrOptionalFixtures()
        {
            Assert.That(game.hull, Is.EqualTo(70)); Assert.That(game.hullMax, Is.EqualTo(100)); Assert.That(game.power, Is.Zero);
            Assert.That(tuning.firstEngineRepairPlates, Is.EqualTo(2));
            foreach (StationKind kind in new[] { StationKind.Fishing, StationKind.Searchlight, StationKind.Turret, StationKind.Generator, StationKind.HullRepair, StationKind.Filter })
            {
                var station = Child(kind.ToString(), game.ship.transform).AddComponent<StationAnchor>(); station.kind = kind;
                game.RefreshEquipmentVisibility(); Assert.That(station.gameObject.activeSelf, Is.False, kind.ToString());
                Assert.That(station.CanInteract(game.captain), Is.False);
            }
            Assert.That(game.IsStationUnlocked(StationKind.Workbench)); Assert.That(game.IsStationUnlocked(StationKind.Winch));
        }

        [Test] public void EquipmentRecipeConsumesRawResourcesAndOnlyRevealsFixtureOnCompletion()
        {
            var station = Child("Generator", game.ship.transform).AddComponent<StationAnchor>(); station.kind = StationKind.Generator; game.RefreshEquipmentVisibility();
            var recipe = Recipe("generator", "generator", new ResourceAmount(ResourceKind.Metal, 2), new ResourceAmount(ResourceKind.Wood, 2), new ResourceAmount(ResourceKind.Fiber, 1));
            recipe.hideWhenCompleted = true;
            game.AddResource(ResourceKind.Metal, 2); game.AddResource(ResourceKind.Fiber, 1);
            Assert.That(game.TryCraft("generator", game.captain)); Assert.That(game.generatorInstalled, Is.False);
            Assert.That(game.craftingManager.ShowsRecipe(recipe, StationKind.Workbench));
            Assert.That(game.GetResource(ResourceKind.Wood), Is.Zero); Assert.That(game.GetResource(ResourceKind.Metal), Is.Zero); Assert.That(game.GetResource(ResourceKind.Fiber), Is.Zero);
            game.craftingManager.Tick(40); Assert.That(game.generatorInstalled); Assert.That(station.gameObject.activeSelf);
            Assert.That(game.craftingManager.ShowsRecipe(recipe, StationKind.Workbench), Is.False);
            Assert.That(game.TryCraft("generator", game.captain), Is.False);
        }

        [Test] public void RadioArtAndWorkbenchRecipesFollowCompletedStagesAcrossAContinue()
        {
            var display = game.ship.gameObject.AddComponent<ShipRadioDisplay>();
            display.antenna = Child("Antenna", display.transform).AddComponent<SpriteRenderer>();
            display.receiver = Child("Receiver", display.transform).AddComponent<SpriteRenderer>();
            display.amplifier = Child("Amplifier", display.transform).AddComponent<SpriteRenderer>();
            game.RefreshEquipmentVisibility();
            game.circuitRepaired = true;
            var antenna = Recipe("antenna");
            var receiver = Recipe("receiver");
            var amplifier = Recipe("amplifier");
            var radios = new[] { antenna, receiver, amplifier };
            for (int i = 0; i < radios.Length; i++)
            {
                radios[i].radioStage = i + 1;
                radios[i].hideWhenCompleted = true;
            }

            var manager = game.craftingManager;
            Assert.That(game.TryCraft(antenna.id, game.captain));
            Assert.That(game.CancelCraft(game.jobs.Single().id));
            Assert.That(manager.VisibleRecipeCount(StationKind.Workbench), Is.EqualTo(3));
            Assert.That(display.antenna.gameObject.activeSelf, Is.False);
            Assert.That(game.TryCraft(antenna.id, game.captain));
            manager.Tick(manager.GetCraftDuration(antenna));
            Assert.That(manager.ShowsRecipe(antenna, StationKind.Workbench), Is.False);
            Assert.That(manager.ShowsRecipe(receiver, StationKind.Workbench));
            Assert.That(display.antenna.gameObject.activeSelf);
            Assert.That(display.receiver.gameObject.activeSelf, Is.False);
            var saved = game.CaptureSaveData();

            Assert.That(game.TryCraft(receiver.id, game.captain));
            manager.Tick(manager.GetCraftDuration(receiver));
            Assert.That(game.TryCraft(amplifier.id, game.captain));
            manager.Tick(manager.GetCraftDuration(amplifier));
            Assert.That(display.receiver.gameObject.activeSelf && display.amplifier.gameObject.activeSelf);
            Assert.That(manager.VisibleRecipeCount(StationKind.Workbench), Is.Zero);

            Assert.That(game.TryRestoreSaveData(saved, out string error), Is.True, error);
            Assert.That(manager.VisibleRecipeCount(StationKind.Workbench), Is.EqualTo(2));
            Assert.That(display.antenna.gameObject.activeSelf);
            Assert.That(display.receiver.gameObject.activeSelf || display.amplifier.gameObject.activeSelf, Is.False);
        }

        [Test] public void SpeargunConsumesOneExistingHarpoonAtomicallyAndDoesNotRefundOnCancellation()
        {
            var recipe = Recipe("speargun", "speargun", new ResourceAmount(ResourceKind.Plate, 2), new ResourceAmount(ResourceKind.FiberBundle, 2));
            recipe.consumedToolIds = new[] { "harpoon" };
            game.AddResource(ResourceKind.Plate, 2); game.AddResource(ResourceKind.FiberBundle, 1); game.UnlockEquipment("harpoon");
            Assert.That(game.TryCraft("speargun", game.captain), Is.False); Assert.That(game.hasHarpoon); Assert.That(game.GetResource(ResourceKind.Plate), Is.EqualTo(2));
            game.AddResource(ResourceKind.FiberBundle, 1); Assert.That(game.TryCraft("speargun", game.captain));
            Assert.That(game.hasHarpoon, Is.False); Assert.That(game.hasSpeargun, Is.False); Assert.That(game.GetResource(ResourceKind.Plate), Is.Zero);
            Assert.That(game.CancelCraft(game.jobs.Single().id)); Assert.That(game.hasHarpoon, Is.False); Assert.That(game.hasSpeargun, Is.False);
        }

        [Test] public void NewEquipmentAndConsumedToolSurviveAnInProgressSave()
        {
            var recipe = Recipe("speargun", "speargun"); recipe.consumedToolIds = new[] { "harpoon" };
            game.UnlockEquipment("generator"); game.UnlockEquipment("repair-bench"); game.AcquireFilter(); game.UnlockEquipment("harpoon");
            Assert.That(game.TryCraft("speargun", game.diver)); game.craftingManager.Tick(3);
            GameSaveData save = game.CaptureSaveData(); Assert.That(save.equipmentProgressionVersion, Is.EqualTo(1));
            Assert.That(game.TryRestoreSaveData(save, out string error), Is.True, error);
            Assert.That(game.generatorInstalled && game.repairBenchInstalled && game.filterInstalled); Assert.That(game.hasHarpoon, Is.False);
            Assert.That(game.jobs.Single().progress, Is.EqualTo(3)); game.craftingManager.Tick(40); Assert.That(game.hasSpeargun);
        }

        [Test] public void LegacySavePreservesInstalledFixturesAndMapsLightTiersOnce()
        {
            GameSaveData save = game.CaptureSaveData(); save.equipmentProgressionVersion = 0; save.state.upgradeLevels[(int)UpgradeKind.Searchlight] = 1;
            GameSession.UpgradeEquipmentSave(save);
            Assert.That(save.state.generatorInstalled && save.state.repairBenchInstalled && save.state.filterInstalled);
            Assert.That(save.state.upgradeLevels[(int)UpgradeKind.Searchlight], Is.EqualTo(2));
            GameSession.UpgradeEquipmentSave(save); Assert.That(save.state.upgradeLevels[(int)UpgradeKind.Searchlight], Is.EqualTo(2));
            GameSaveData fresh = game.CaptureSaveData(); GameSession.UpgradeEquipmentSave(fresh); Assert.That(fresh.state.generatorInstalled, Is.False);
        }

        [Test] public void FilterOnlyAcceleratesTheActivelyOperatedJobAndUnderwaterMovement()
        {
            Recipe("plank"); Recipe("plate"); game.upgradeLevels[(int)UpgradeKind.Workbench] = 3;
            Assert.That(game.TryCraft("plank", game.captain)); Assert.That(game.TryCraft("plate", game.diver));
            CraftJob active = game.jobs[0], automatic = game.jobs[1]; game.filterRepaired = true; game.captain.controlsCaptured = true;
            game.craftingManager.SetInteractionContext(game.captain, active.id, StationKind.Workbench); game.craftingManager.Tick(2);
            Assert.That(active.progress, Is.EqualTo(2.4f).Within(.001f)); Assert.That(active.elapsed, Is.EqualTo(2.4f).Within(.001f)); Assert.That(automatic.progress, Is.EqualTo(2));
            game.craftingManager.ClearInteractionContext(game.captain); game.craftingManager.Tick(2); Assert.That(active.progress, Is.EqualTo(4.4f).Within(.001f));
            Assert.That(game.GetMoveMultiplier(game.captain), Is.EqualTo(1)); game.BeginDive(game.captain); Assert.That(game.GetMoveMultiplier(game.captain), Is.EqualTo(1.2f));
        }

        [Test] public void HullRepairRequiresCraftedBenchAndFilterAcquisitionIsUnique()
        {
            var recipe = Recipe("patch"); recipe.repairAmount = 10;
            Assert.That(game.TryCraft("patch", game.captain), Is.False); game.UnlockEquipment("repair-bench"); Assert.That(game.TryCraft("patch", game.captain));
            Assert.That(game.AcquireFilter()); Assert.That(game.AcquireFilter(), Is.False); Assert.That(game.filterRepaired, Is.False);
        }

        [Test] public void ExplicitRecipeMigrationChangesOnlyRecognizedPrices()
        {
            var harpoon = Recipe("harpoon", "harpoon", new ResourceAmount(ResourceKind.Plate, 1)); EquipmentProgressionMigration.UpdateKnownRecipeDefaults(harpoon);
            Assert.That(harpoon.cost.Single().amount, Is.EqualTo(2)); harpoon.cost[0].amount = 9; EquipmentProgressionMigration.UpdateKnownRecipeDefaults(harpoon); Assert.That(harpoon.cost[0].amount, Is.EqualTo(9));
            var speargun = Recipe("speargun", "speargun", new ResourceAmount(ResourceKind.Plate, 1), new ResourceAmount(ResourceKind.FiberBundle, 1)); EquipmentProgressionMigration.UpdateKnownRecipeDefaults(speargun);
            Assert.That(speargun.cost.Select(cost => cost.amount), Is.EqualTo(new[] { 2, 2 })); Assert.That(speargun.consumedToolIds, Is.EqualTo(new[] { "harpoon" }));
        }
    }
}
