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

        [Test] public void T4CostsTwoBundlesHasHundredMetreRopeAndRoundTripsWithoutAcceptingT5()
        {
            game.upgradeLevels[(int)UpgradeKind.Rope] = 3; game.ropeLength = 74;
            game.AddResource(ResourceKind.FiberBundle, 1); Assert.That(game.TryUpgrade(UpgradeKind.Rope), Is.False);
            game.AddResource(ResourceKind.FiberBundle, 1); Assert.That(game.TryUpgrade(UpgradeKind.Rope));
            Assert.That(game.ropeLength, Is.EqualTo(100)); Assert.That(game.GetResource(ResourceKind.FiberBundle), Is.Zero);
            Assert.That(game.IsElectricWinch); Assert.That(game.TryUpgrade(UpgradeKind.Rope), Is.False);
            var save = game.CaptureSaveData(); Assert.That(game.TryRestoreSaveData(save, out string error), Is.True, error);
            Assert.That(game.GetUpgrade(UpgradeKind.Rope), Is.EqualTo(4)); Assert.That(game.ropeLength, Is.EqualTo(100));
            save.state.upgradeLevels[(int)UpgradeKind.Rope] = 5;
            Assert.That(GameSaveValidation.Validate(save, out error), Is.False);
            tuning.ropeLengths = new[] { 31f, 47f, 60f, 81f };
            Lifeline.Editor.WinchT4Migration.UpdateTuning(tuning);
            Assert.That(tuning.ropeLengths, Is.EqualTo(new[] { 31f, 47f, 60f, 81f, 100f }));
            tuning.ropeLengths[4] = 105; Lifeline.Editor.WinchT4Migration.UpdateTuning(tuning);
            Assert.That(tuning.ropeLengths[4], Is.EqualTo(105));
        }

        [Test] public void UpgradePreviewShowsOnlyTheNextTierAndMaxHasNoFutureEffect()
        {
            Assert.That(UpgradeSummary.NextEffect(game, UpgradeKind.Rope), Does.Contain("44m").And.Not.Contain("74m").And.Not.Contain("100m"));
            game.upgradeLevels[(int)UpgradeKind.Rope] = 2;
            Assert.That(UpgradeSummary.NextEffect(game, UpgradeKind.Rope), Does.Contain("74m").And.Not.Contain("100m"));
            game.upgradeLevels[(int)UpgradeKind.Rope] = 3;
            Assert.That(UpgradeSummary.NextEffect(game, UpgradeKind.Rope), Does.Contain("100m").And.Contain("+20%"));
            game.upgradeLevels[(int)UpgradeKind.Rope] = 4;
            Assert.That(UpgradeSummary.NextEffect(game, UpgradeKind.Rope), Is.EqualTo(UiLocalization.Text("已满级")));
            Assert.That(UpgradeSummary.NextEffect(game, UpgradeKind.Workbench), Does.Contain("30s").And.Contain("+30%"));
            game.upgradeLevels[(int)UpgradeKind.Workbench] = 1;
            Assert.That(UpgradeSummary.NextEffect(game, UpgradeKind.Workbench), Does.Contain("20s").And.Not.Contain("+30%"));
            game.upgradeLevels[(int)UpgradeKind.Backpack] = 1;
            Assert.That(UpgradeSummary.NextEffect(game, UpgradeKind.Backpack), Does.Contain("+4").And.Not.Contain("25%"));
            Assert.That(UpgradeSummary.DisplayTier(UpgradeKind.Searchlight, 1), Is.EqualTo(2));
        }

        [TestCase(8417)] [TestCase(17017)] [TestCase(-514)] [TestCase(2026)]
        public void EveryRuinHasFourClearConnectedExitsAndCaveRetainsSingleEntrance(int seed)
        {
            var world = Child("World").AddComponent<WorldGenerator>(); world.tuning = tuning; world.session = game;
            world.geometryLayer = 20; world.pickupLayer = 21; world.drawLabels = false; world.forceCave = true; game.world = world;
            world.Generate(seed); Assert.That(world.layouts.Count, Is.EqualTo(10));
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
