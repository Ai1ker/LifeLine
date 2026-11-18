using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Lifeline.Tests
{
    public sealed class WinchAndRuinExitTests
    {
        GameObject root; GameSession game, previous; GameTuning tuning; float scale; Random.State random;
        [SetUp] public void SetUp()
        {
            previous = GameSession.Current; scale = Time.timeScale; random = Random.state;
            root = new GameObject("Winch / exterior route regression");
            game = root.AddComponent<GameSession>(); game.autoStart = false; game.enabled = false;
            tuning = ScriptableObject.CreateInstance<GameTuning>(); tuning.maximumSharks = 0; game.tuning = tuning;
            game.ship = Child("Ship").AddComponent<ShipMotor>(); game.ship.enabled = false;
            game.captain = Child("Captain").AddComponent<CrewActor>(); game.captain.enabled = false;
            game.diver = Child("Diver").AddComponent<CrewActor>(); game.diver.enabled = false; game.Initialize();
        }
        GameObject Child(string name) { var obj = new GameObject(name); obj.transform.SetParent(root.transform); return obj; }
        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(root); Object.DestroyImmediate(tuning); GameSession.RestoreCurrentAfterValidation(previous);
            Time.timeScale = scale; Random.state = random;
        }

        [Test] public void ExtendedRopeTiersUseThreeBundlesAndRoundTripWithoutAcceptingT6()
        {
            Assert.That(game.ropeLength, Is.EqualTo(38));
            game.AddResource(ResourceKind.FiberBundle, 6);
            for(int tier=1;tier<=3;tier++)
            {
                Assert.That(game.GetUpgradeCost(UpgradeKind.Rope).Single().amount, Is.EqualTo(2));
                Assert.That(game.TryUpgrade(UpgradeKind.Rope));Assert.That(game.ropeLength, Is.EqualTo(tuning.ropeLengths[tier]));
            }
            game.AddResource(ResourceKind.FiberBundle, 2); Assert.That(game.TryUpgrade(UpgradeKind.Rope), Is.False);
            game.AddResource(ResourceKind.FiberBundle, 1); Assert.That(game.TryUpgrade(UpgradeKind.Rope));
            Assert.That(game.ropeLength, Is.EqualTo(150)); Assert.That(game.GetResource(ResourceKind.FiberBundle), Is.Zero);
            Assert.That(game.GetUpgradeCost(UpgradeKind.Rope).Single().amount, Is.EqualTo(3));
            game.AddResource(ResourceKind.FiberBundle,3);Assert.That(game.TryUpgrade(UpgradeKind.Rope));
            Assert.That(game.ropeLength, Is.EqualTo(200));
            Assert.That(game.IsElectricWinch); Assert.That(game.TryUpgrade(UpgradeKind.Rope), Is.False);
            var save = game.CaptureSaveData(); Assert.That(game.TryRestoreSaveData(save, out string error), Is.True, error);
            Assert.That(game.GetUpgrade(UpgradeKind.Rope), Is.EqualTo(5)); Assert.That(game.ropeLength, Is.EqualTo(200));
            Assert.That(game.GetUpgradeCost(UpgradeKind.Rope), Is.Empty);
            save.state.upgradeLevels[(int)UpgradeKind.Rope] = 6;
            Assert.That(GameSaveValidation.Validate(save, out error), Is.False);
        }

        [Test] public void OriginalT4MigrationPreservesCustomLengths()
        {
            tuning.ropeLengths = new[] { 31f, 47f, 60f, 81f };
            Lifeline.Editor.WinchT4Migration.UpdateTuning(tuning);
            Assert.That(tuning.ropeLengths, Is.EqualTo(new[] { 31f, 47f, 60f, 81f, 100f }));
            tuning.ropeLengths[4] = 105; Lifeline.Editor.WinchT4Migration.UpdateTuning(tuning);
            Assert.That(tuning.ropeLengths[4], Is.EqualTo(105));
        }

        [Test] public void OxygenHasFourUpgradesAndCaptainRetainsFiftySeconds()
        {
            game.AddResource(ResourceKind.Plate,8);
            for(int tier=1;tier<=4;tier++)
            {
                Assert.That(game.GetUpgradeCost(UpgradeKind.Oxygen).Single().amount,Is.EqualTo(2));
                Assert.That(game.TryUpgrade(UpgradeKind.Oxygen));
                Assert.That(game.GetOxygenMax(),Is.EqualTo(tuning.oxygenLevels[tier]));
                Assert.That(game.captain.oxygenMax,Is.EqualTo(50));
            }
            Assert.That(game.GetOxygenMax(),Is.EqualTo(250));Assert.That(game.GetResource(ResourceKind.Plate),Is.Zero);
            Assert.That(game.TryUpgrade(UpgradeKind.Oxygen),Is.False);Assert.That(game.GetUpgradeCost(UpgradeKind.Oxygen),Is.Empty);
            var save=game.CaptureSaveData();Assert.That(game.TryRestoreSaveData(save,out string error),Is.True,error);
            Assert.That(game.GetUpgrade(UpgradeKind.Oxygen),Is.EqualTo(4));Assert.That(game.GetOxygenMax(),Is.EqualTo(250));
            save.state.upgradeLevels[(int)UpgradeKind.Oxygen]=5;Assert.That(GameSaveValidation.Validate(save,out error),Is.False);
        }

        [Test] public void ExpandedAssetMigrationPreservesAuthoredValuesAndIsIdempotent()
        {
            tuning.ropeLengths=new[]{28f,49f,54f,74f,100f};tuning.oxygenLevels=new[]{50f,80f,120f,150f};
            Lifeline.Editor.WinchOxygenExpansionMigration.UpdateTuning(tuning);
            Assert.That(tuning.ropeLengths,Is.EqualTo(new[]{38f,49f,74f,100f,150f,200f}));
            Assert.That(tuning.oxygenLevels,Is.EqualTo(new[]{50f,80f,135f,175f,250f}));
            tuning.ropeLengths[0]=28;tuning.oxygenLevels[1]=75;
            Lifeline.Editor.WinchOxygenExpansionMigration.UpdateTuning(tuning);
            Assert.That(tuning.ropeLengths[0],Is.EqualTo(28));Assert.That(tuning.oxygenLevels[1],Is.EqualTo(75));
        }

        [Test] public void PreviousTierSaveMigratesOnceWithoutRefillingOxygenOrChangingCaller()
        {
            game.upgradeLevels[(int)UpgradeKind.Rope]=4;game.ropeLength=100;
            game.upgradeLevels[(int)UpgradeKind.Oxygen]=3;game.diver.oxygenMax=150;game.diver.oxygen=75;
            var save=game.CaptureSaveData();save.balanceVersion=1;
            Assert.That(game.TryRestoreSaveData(save,out string error),Is.True,error);
            Assert.That(game.ropeLength,Is.EqualTo(150));Assert.That(game.diver.oxygenMax,Is.EqualTo(175));
            Assert.That(game.diver.oxygen,Is.EqualTo(87.5f));Assert.That(game.captain.oxygenMax,Is.EqualTo(50));
            Assert.That(save.state.ropeLength,Is.EqualTo(100));Assert.That(save.diver.oxygenMax,Is.EqualTo(150));
            Assert.That(save.balanceVersion,Is.EqualTo(1));
            // Versioned saves can contain a developer's custom value equal to an old default.
            game.ropeLength=100;game.diver.oxygenMax=150;game.diver.oxygen=75;
            var current=game.CaptureSaveData();Assert.That(current.balanceVersion,Is.EqualTo(BalanceSaveMigration.CurrentVersion));
            Assert.That(game.TryRestoreSaveData(current,out error),Is.True,error);
            Assert.That(game.ropeLength,Is.EqualTo(100));Assert.That(game.diver.oxygenMax,Is.EqualTo(150));Assert.That(game.diver.oxygen,Is.EqualTo(75));
        }

        [Test] public void UpgradePreviewShowsOnlyTheNextTierAndMaxHasNoFutureEffect()
        {
            Assert.That(UpgradeSummary.NextEffect(game, UpgradeKind.Rope), Does.Contain("54m").And.Not.Contain("74m").And.Not.Contain("100m"));
            game.upgradeLevels[(int)UpgradeKind.Rope] = 2;
            Assert.That(UpgradeSummary.NextEffect(game, UpgradeKind.Rope), Does.Contain("100m").And.Not.Contain("150m"));
            game.upgradeLevels[(int)UpgradeKind.Rope] = 3;
            Assert.That(UpgradeSummary.NextEffect(game, UpgradeKind.Rope), Does.Contain("150m").And.Contain("+20%"));
            game.upgradeLevels[(int)UpgradeKind.Rope] = 4;
            Assert.That(UpgradeSummary.NextEffect(game, UpgradeKind.Rope), Does.Contain("200m").And.Not.Contain("+20%"));
            game.upgradeLevels[(int)UpgradeKind.Rope] = 5;
            Assert.That(UpgradeSummary.NextEffect(game, UpgradeKind.Rope), Is.EqualTo(UiLocalization.Text("已满级")));
            Assert.That(UpgradeSummary.NextEffect(game, UpgradeKind.Workbench), Does.Contain("30s").And.Contain("+30%"));
            game.upgradeLevels[(int)UpgradeKind.Workbench] = 1;
            Assert.That(UpgradeSummary.NextEffect(game, UpgradeKind.Workbench), Does.Contain("20s").And.Not.Contain("+30%"));
            game.upgradeLevels[(int)UpgradeKind.Backpack] = 1;
            Assert.That(UpgradeSummary.NextEffect(game, UpgradeKind.Backpack), Does.Contain("+4").And.Not.Contain("25%"));
            Assert.That(UpgradeSummary.DisplayTier(UpgradeKind.Searchlight, 1), Is.EqualTo(1));
        }

        [TestCase(8417)] [TestCase(17017)] [TestCase(-514)] [TestCase(2026)]
        public void EveryRuinHasFourClearConnectedExitsAndCaveRetainsSingleEntrance(int seed)
        {
            var world = Child("World").AddComponent<WorldGenerator>(); world.tuning = tuning; world.session = game;
            world.geometryLayer = 20; world.pickupLayer = 21; world.drawLabels = false; world.forceCave = true; game.world = world;
            world.Generate(seed); Assert.That(world.layouts.Count, Is.EqualTo(27));
            foreach (var maze in world.layouts)
            {
                var exits = maze.ExteriorExits().ToArray();
                Assert.That(maze.ReachableCellCount(), Is.EqualTo(maze.Count));
                if (maze.entranceOnLeft) { Assert.That(exits.Length, Is.EqualTo(1)); continue; }
                Assert.That(exits.Select(e => e.direction).Distinct().Count(), Is.EqualTo(4), maze.name);
                Assert.That(exits.Length, Is.GreaterThanOrEqualTo(4));
            }
            Assert.That(world.regionalConnections.Count(c => c.name == "Top-bottom depth passage"), Is.EqualTo(6));
            Assert.That(world.ValidateGeneratedWorld(out string report), Is.True, report);
        }
    }
}
