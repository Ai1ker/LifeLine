using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Lifeline.Tests
{
    public sealed class DeviceInteractionRulesTests
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
            root = new GameObject("Device interaction rules fixture");
            game = root.AddComponent<GameSession>(); game.autoStart = false; game.enabled = false;
            tuning = ScriptableObject.CreateInstance<GameTuning>(); assets.Add(tuning);
            tuning.maximumSharks = 0; tuning.birdInterval = 10000; game.tuning = tuning;
            game.ship = Child("Ship").AddComponent<ShipMotor>(); game.ship.enabled = false;
            game.captain = Actor("Captain"); game.diver = Actor("Diver"); game.Initialize();
            var winch = Child("Winch").AddComponent<StationAnchor>(); winch.kind = StationKind.Winch;
            winch.transform.SetParent(game.ship.transform); winch.transform.position = game.captain.Position;
        }

        GameObject Child(string name) { var obj = new GameObject(name); obj.transform.SetParent(root.transform); return obj; }
        CrewActor Actor(string name) { var obj = Child(name); obj.layer = 9; var actor = obj.AddComponent<CrewActor>(); actor.enabled = false; return actor; }
        void StartManualWinch()
        {
            Assert.That(game.BeginDive());
            game.diver.RestorePose(false, game.BoatAnchor + Vector3.down * 20, Vector3.zero, Vector2.up);
            game.SetWinchActive(true); Assert.That(game.winchActive); Assert.That(game.winchElectric, Is.False);
        }
        RecipeDefinition Recipe(string id, ResourceAmount[] cost, ResourceAmount[] output = null, float repair = 0)
        {
            var recipe = ScriptableObject.CreateInstance<RecipeDefinition>(); assets.Add(recipe);
            recipe.id = recipe.displayName = id; recipe.cost = cost; recipe.output = output ?? new ResourceAmount[0]; recipe.repairAmount = repair;
            game.recipes = game.recipes.Concat(new[] { recipe }).ToArray(); return recipe;
        }
        CrewStationPanel DeveloperPanel(StationKind station)
        {
            var view = Child("Developer station view").AddComponent<CrewStationPanel>(); view.enabled = false;
            view.panel = Child("Active device panel");
            var detail = new GameObject("Detail", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)); detail.transform.SetParent(root.transform);
            view.detailText = detail.GetComponent<Text>(); view.Initialize(null, game, game.captain);
            view.station = station; view.panel.SetActive(true); game.captain.controlsCaptured = true; return view;
        }

        [TearDown] public void TearDown()
        {
            game.ClearAreaStrikes(); foreach (var enemy in game.enemies.ToArray()) if (enemy != null) Object.DestroyImmediate(enemy.gameObject);
            Object.DestroyImmediate(root); foreach (Object asset in assets) Object.DestroyImmediate(asset); assets.Clear();
            GameSession.RestoreCurrentAfterValidation(previous); Time.timeScale = scale; Random.state = random;
        }

        [Test] public void WinchTargetStaysAcrossIdleRevolutionsAndMovesAfterEitherResult()
        {
            StartManualWinch(); float previousCenter = game.WinchQteCenter;
            game.StepSimulation(tuning.winchCycle * 3 + .01f);
            Assert.That(game.WinchQteCenter, Is.EqualTo(previousCenter));
            previousCenter = game.WinchQteCenter;
            Assert.That(game.TryWinchQte(previousCenter)); Assert.That(game.winchFeedback, Does.Contain("成功"));
            Assert.That(GameSession.CircularQteDistance(game.WinchQteCenter, previousCenter), Is.GreaterThan(.14f));
            game.StepSimulation(.2f); previousCenter = game.WinchQteCenter;
            Assert.That(game.TryWinchQte(Mathf.Repeat(previousCenter + .5f, 1))); Assert.That(game.winchFeedback, Does.Contain("失败"));
            Assert.That(GameSession.CircularQteDistance(game.WinchQteCenter, previousCenter), Is.GreaterThan(.14f));
            previousCenter = game.WinchQteCenter;
            Assert.That(game.TryWinchQte(previousCenter), Is.False, "A rejected input during the miss jam must not move the window.");
            Assert.That(game.WinchQteCenter, Is.EqualTo(previousCenter));
        }

        [Test] public void WinchAcceptsTheCircularSeamAndRejectsRapidRepeatInputs()
        {
            Assert.That(GameSession.CircularQteDistance(.99f, .01f), Is.EqualTo(.02f).Within(.00001f));
            StartManualWinch(); game.winchTargetCenter = .99f; game.winchCenterInitialized = true;
            Assert.That(game.TryWinchQte(.01f)); Assert.That(game.winchFeedback, Does.Contain("成功"));
            float center = game.WinchQteCenter;
            Assert.That(game.TryWinchQte(center), Is.False); Assert.That(game.WinchQteCenter, Is.EqualTo(center));
            game.StepSimulation(.179f); Assert.That(game.TryWinchQte(game.WinchQteCenter), Is.False);
            game.StepSimulation(.002f); Assert.That(game.TryWinchQte(game.WinchQteCenter));
        }

        [Test] public void FilterImprovesDeckInteractionsAndSwimmingButDoesNotIncreaseDeckWalking()
        {
            Assert.That(game.GetDeviceInteractionEfficiency(game.captain), Is.EqualTo(1));
            game.filterRepaired = true;
            foreach (CrewActor actor in game.Crew)
            {
                Assert.That(game.GetDeviceInteractionEfficiency(actor), Is.EqualTo(1.2f));
                Assert.That(game.GetMoveMultiplier(actor), Is.EqualTo(1));
                actor.SetOnboard(false, game.BoatAnchor + Vector3.down * 3);
                Assert.That(game.GetDeviceInteractionEfficiency(actor), Is.EqualTo(1));
                Assert.That(game.GetMoveMultiplier(actor), Is.EqualTo(1.2f));
            }
        }

        [Test] public void InstalledGeneratorKeepsThreeSecondCrankAndUsesTwoPointFiveWithFilter()
        {
            Assert.That(game.CrankGenerator(true, game.captain), Is.False);
            game.UnlockEquipment("generator");
            Assert.That(game.GetGeneratorStrokeInterval(game.captain), Is.EqualTo(3));
            Assert.That(game.CrankGenerator(true, game.captain)); game.elapsed = 2.99f;
            Assert.That(game.CrankGenerator(true, game.captain), Is.False); game.elapsed = 3;
            Assert.That(game.CrankGenerator(true, game.captain)); Assert.That(game.power, Is.EqualTo(2));
            game.filterRepaired = true;
            Assert.That(game.GetGeneratorStrokeInterval(game.captain), Is.EqualTo(2.5f).Within(.0001f));
            game.elapsed = 5.49f; Assert.That(game.CrankGenerator(true, game.captain), Is.False);
            game.elapsed = 5.5f; Assert.That(game.CrankGenerator(true, game.captain)); Assert.That(game.power, Is.EqualTo(3));
        }

        [Test] public void FirstEngineRepairCostsTwoPlatesAndEngineGatesPoweredSailing()
        {
            game.power = 50; Assert.That(game.TryConsumeSailing(10), Is.False); Assert.That(game.power, Is.EqualTo(50));
            game.AddResource(ResourceKind.Plate, 1); Assert.That(game.TryRepairDevice(StationKind.Engine), Is.False);
            Assert.That(game.GetResource(ResourceKind.Plate), Is.EqualTo(1));
            game.AddResource(ResourceKind.Plate, 1); Assert.That(game.TryRepairDevice(StationKind.Engine)); Assert.That(game.GetResource(ResourceKind.Plate), Is.Zero);
            for (int i = 0; i < game.engineFaultFixed.Length; i++)
            { Assert.That(game.RevealEngineFault(i)); Assert.That(game.TryRepairEngineFault(i, game.engineFaultTypes[i])); }
            Assert.That(game.engineRepaired); Assert.That(game.TryConsumeSailing(10)); Assert.That(game.power, Is.EqualTo(40));
            Assert.That(game.DamageEngine("fixture")); Assert.That(game.TryConsumeSailing(1), Is.False);
            Assert.That(game.TryRepairDevice(StationKind.Engine)); Assert.That(game.GetResource(ResourceKind.Plate), Is.Zero, "later engine failures remain free to repair");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Test] public void DeveloperCompletionHonorsInstallationAndFirstRepairCosts()
        {
            CrewStationPanel view = DeveloperPanel(StationKind.Generator);
            Assert.That((bool)DeveloperTestAccess.Invoke(game, "DeveloperCompleteInteraction", view, game.captain, null), Is.False);
            game.UnlockEquipment("generator"); Assert.That((bool)DeveloperTestAccess.Invoke(game, "DeveloperCompleteInteraction", view, game.captain, null)); Assert.That(game.power, Is.EqualTo(1));
            view.station = StationKind.Filter; game.AddResource(ResourceKind.Plate, 2);
            Assert.That((bool)DeveloperTestAccess.Invoke(game, "DeveloperCompleteInteraction", view, game.captain, null), Is.False); Assert.That(game.GetResource(ResourceKind.Plate), Is.EqualTo(2));
            game.AcquireFilter(); Assert.That((bool)DeveloperTestAccess.Invoke(game, "DeveloperCompleteInteraction", view, game.captain, null));
            Assert.That(game.filterRepaired); Assert.That(game.GetResource(ResourceKind.Plate), Is.Zero);
            view.station = StationKind.Engine;
            Assert.That((bool)DeveloperTestAccess.Invoke(game, "DeveloperCompleteInteraction", view, game.captain, null), Is.False); Assert.That(game.engineRepaired, Is.False);
            game.AddResource(ResourceKind.Plate, 2); Assert.That((bool)DeveloperTestAccess.Invoke(game, "DeveloperCompleteInteraction", view, game.captain, null));
            Assert.That(game.engineRepaired); Assert.That(game.GetResource(ResourceKind.Plate), Is.Zero);
        }

        [Test] public void DeveloperCompletionAcceleratesAdmittedCraftAndRepairJobsWithoutRefundOrFreeOutput()
        {
            Recipe("plank", new[] { new ResourceAmount(ResourceKind.Wood, 2) }, new[] { new ResourceAmount(ResourceKind.Plank, 1) });
            Recipe("patch", new[] { new ResourceAmount(ResourceKind.Plank, 1), new ResourceAmount(ResourceKind.Plate, 1) }, repair: 10);
            CrewStationPanel view = DeveloperPanel(StationKind.Workbench);
            Assert.That((bool)DeveloperTestAccess.Invoke(game, "DeveloperCompleteInteraction", view, game.captain, null), Is.False); Assert.That(game.GetResource(ResourceKind.Plank), Is.Zero);
            Assert.That(game.TryCraft("plank", game.captain)); Assert.That(game.GetResource(ResourceKind.Wood), Is.Zero);
            Assert.That((bool)DeveloperTestAccess.Invoke(game, "DeveloperCompleteInteraction", view, game.captain, null)); Assert.That(game.GetResource(ResourceKind.Plank), Is.EqualTo(1)); Assert.That(game.jobs, Is.Empty);
            view.station = StationKind.HullRepair; game.AddResource(ResourceKind.Plate, 1);
            Assert.That(game.TryCraft("patch", game.captain), Is.False); Assert.That((bool)DeveloperTestAccess.Invoke(game, "DeveloperCompleteInteraction", view, game.captain, null), Is.False);
            game.UnlockEquipment("repair-bench"); Assert.That(game.TryCraft("patch", game.captain));
            Assert.That(game.GetResource(ResourceKind.Plank), Is.Zero); Assert.That(game.GetResource(ResourceKind.Plate), Is.Zero);
            Assert.That((bool)DeveloperTestAccess.Invoke(game, "DeveloperCompleteInteraction", view, game.captain, null)); Assert.That(game.hull, Is.EqualTo(80)); Assert.That(game.jobs, Is.Empty);
        }
#endif
    }
}
