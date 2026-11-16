using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Lifeline.Editor;

namespace Lifeline.Tests
{
    public sealed class InputAndActorTests
    {
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        private string prefix;
        private float oldScale;

        [SetUp]
        public void SetUp()
        {
            prefix = "LifeLine.Tests." + Guid.NewGuid() + ".";
            oldScale = Time.timeScale;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            created.Clear();
            PlayerPrefs.DeleteKey(prefix + CrewRole.Captain);
            PlayerPrefs.DeleteKey(prefix + CrewRole.Diver);
            Time.timeScale = oldScale;
        }

        private T Keep<T>(T item) where T : UnityEngine.Object { created.Add(item); return item; }
        private InputActionAsset Asset() => Keep(CrewInputDefaults.CreateAsset());
        private CrewInputReader Reader(CrewRole role, InputActionAsset asset)
        {
            var go = Keep(new GameObject("Input_" + role));
            var reader = go.AddComponent<CrewInputReader>();
            reader.preferencesPrefix = prefix;
            reader.Initialize(role, asset);
            return reader;
        }

        [Test]
        public void ResettingOneRoleRetainsTheOtherRolesPersistedBinding()
        {
            var asset = Asset();
            var captain = Reader(CrewRole.Captain, asset);
            var diver = Reader(CrewRole.Diver, asset);
            captain.RuntimeActions.FindAction("Captain/Melee").ApplyBindingOverride(0, "<Keyboard>/h");
            diver.RuntimeActions.FindAction("Diver/Melee").ApplyBindingOverride(0, "<Keyboard>/j");
            captain.SaveBindings(); diver.SaveBindings();
            captain.ResetBindings();
            captain.SetEnabled(false); diver.SetEnabled(false);
            var restoredCaptain = Reader(CrewRole.Captain, asset);
            var restoredDiver = Reader(CrewRole.Diver, asset);
            Assert.That(restoredCaptain.RuntimeActions.FindAction("Captain/Melee").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/space"));
            Assert.That(restoredDiver.RuntimeActions.FindAction("Diver/Melee").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/j"));
        }

        [Test]
        public void RemoteCommandsKeepInteractHeldWhileConsumingTheInteractEdge()
        {
            var source = new RemoteCrewInputSource { role = CrewRole.Diver };
            source.Push(new CrewCommand { role = CrewRole.Diver, interact = true, interactHeld = true, move = Vector2.left, collect = true });
            var first = source.ReadCommand();
            var second = source.ReadCommand();
            Assert.That(first.interact && first.interactHeld, Is.True);
            Assert.That(second.interact, Is.False);
            Assert.That(second.interactHeld && second.collect, Is.True, "a five-second revive requires continuous held state, not repeated press edges");
            Assert.That(second.move, Is.EqualTo(Vector2.left));
            source.Push(new CrewCommand { role = CrewRole.Diver });
            Assert.That(source.ReadCommand().interactHeld, Is.False, "a release packet ends the held interaction");
            source.Push(new CrewCommand { interactHeld = true });
            source.SetEnabled(false);
            Assert.That(source.ReadCommand().interactHeld, Is.False);
        }

        [Test]
        public void RuleMigrationIsPreviewableIdempotentAndRetainsCustomValuesAndRecipes()
        {
            var tuning = Keep(ScriptableObject.CreateInstance<GameTuning>());
            tuning.baseOxygen = 60; tuning.oxygenLevels = new[] { 60f, 90f, 120f, 150f };
            tuning.ropeLengths = new[] { 20f, 26f, 34f, 44f }; tuning.generatorStrokeInterval = 1;
            tuning.upgradedCraftSeconds = 30; tuning.caveChance = .55f; tuning.eelCaveChance = .75f;
            Assert.That(PrefabLibraryBuilder.MigrateKnownTuningDefaults(tuning, false).Count, Is.GreaterThan(5));
            Assert.That(tuning.baseOxygen, Is.EqualTo(60), "preview cannot mutate legacy tuning");
            Assert.That(tuning.generatorStrokeInterval, Is.EqualTo(1));
            PrefabLibraryBuilder.MigrateKnownTuningDefaults(tuning, true);
            Assert.That(tuning.baseOxygen, Is.EqualTo(40));
            Assert.That(tuning.oxygenLevels, Is.EqualTo(new[] { 40f, 70f, 100f, 130f }));
            Assert.That(tuning.ropeLengths[3], Is.EqualTo(54));
            Assert.That(tuning.generatorStrokeInterval, Is.EqualTo(3));
            Assert.That(tuning.caveChance, Is.EqualTo(.05f)); Assert.That(tuning.eelCaveChance, Is.EqualTo(.05f));
            Assert.That(PrefabLibraryBuilder.MigrateKnownTuningDefaults(tuning, true), Is.Empty);
            var authoredOxygen = new[] { 50f, 85f, 110f, 170f };
            tuning.baseOxygen = 52; tuning.oxygenLevels = authoredOxygen; tuning.generatorStrokeInterval = 2;
            tuning.caveChance = .3f; tuning.eelCaveChance = .4f; tuning.ropeLengths[3] = 60;
            PrefabLibraryBuilder.MigrateKnownTuningDefaults(tuning, true);
            Assert.That(tuning.baseOxygen, Is.EqualTo(52)); Assert.That(tuning.oxygenLevels, Is.SameAs(authoredOxygen));
            Assert.That(tuning.generatorStrokeInterval, Is.EqualTo(2)); Assert.That(tuning.caveChance, Is.EqualTo(.3f));
            Assert.That(tuning.eelCaveChance, Is.EqualTo(.4f)); Assert.That(tuning.ropeLengths[3], Is.EqualTo(60));

            var game = Keep(new GameObject("MigrationSession")).AddComponent<GameSession>(); game.autoStart = false;
            var custom = Keep(ScriptableObject.CreateInstance<RecipeDefinition>()); custom.id = "artist-recipe";
            var quick = Keep(ScriptableObject.CreateInstance<RecipeDefinition>()); quick.id = "quick-hull";
            game.recipes = new[] { custom };
            game.workbenchUpgradeCost = new[] { new ResourceAmount(ResourceKind.Plate, 1), new ResourceAmount(ResourceKind.Plank, 1) };
            PrefabLibraryBuilder.MigrateKnownSessionDefaults(game, quick, false);
            Assert.That(game.recipes.Length, Is.EqualTo(1)); Assert.That(game.workbenchUpgradeCost.Length, Is.EqualTo(2));
            PrefabLibraryBuilder.MigrateKnownSessionDefaults(game, quick, true);
            Assert.That(game.recipes, Is.EqualTo(new[] { custom, quick }));
            Assert.That(game.workbenchUpgradeCost.Length, Is.EqualTo(1));
            Assert.That(game.workbenchUpgradeCost[0].resource, Is.EqualTo(ResourceKind.Plank));
            Assert.That(game.workbenchUpgradeCost[0].amount, Is.EqualTo(2));
            Assert.That(PrefabLibraryBuilder.MigrateKnownSessionDefaults(game, quick, true), Is.Empty);
            game.workbenchUpgradeCost = new[] { new ResourceAmount(ResourceKind.Plank, 7) };
            PrefabLibraryBuilder.MigrateKnownSessionDefaults(game, quick, true);
            Assert.That(game.workbenchUpgradeCost[0].amount, Is.EqualTo(7));
        }

    }
}
