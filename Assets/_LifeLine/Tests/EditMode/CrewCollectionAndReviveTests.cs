using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lifeline.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Lifeline.Tests
{
    public sealed class CrewCollectionAndReviveTests
    {
        readonly List<Object> assets = new List<Object>();
        GameObject root;
        GameSession game, previous;
        GameTuning tuning;
        UnityEngine.Random.State random;
        float scale;

        [SetUp] public void SetUp()
        {
            previous = GameSession.Current; random = UnityEngine.Random.state; scale = Time.timeScale;
            root = new GameObject("Crew collection and revival fixture");
            game = root.AddComponent<GameSession>(); game.autoStart = false; game.enabled = false;
            tuning = Keep(ScriptableObject.CreateInstance<GameTuning>()); tuning.maximumSharks = 0; tuning.birdInterval = 10000; game.tuning = tuning;
            game.ship = Child("Ship").AddComponent<ShipMotor>(); game.ship.enabled = false;
            game.ship.transform.position = Vector3.right * 5000; // Avoid unrelated open-scene station scans.
            game.captain = Actor("Captain"); game.diver = Actor("Diver"); game.Initialize();
            game.DisableTutorialForTests(); // This fixture tests collection/revival, not the ruin gate.
        }
        T Keep<T>(T asset) where T : Object { assets.Add(asset); return asset; }
        GameObject Child(string name) { var obj = new GameObject(name); obj.transform.SetParent(root.transform); return obj; }
        CrewActor Actor(string name)
        {
            var obj = Child(name); obj.layer = 9; var actor = obj.AddComponent<CrewActor>(); actor.enabled = false;
            actor.visualRoot = Child(name + " visual").transform; actor.visualRoot.SetParent(obj.transform); return actor;
        }
        WorldGenerator World()
        {
            var world = Child("World").AddComponent<WorldGenerator>(); world.enabled = false; world.session = game; world.tuning = tuning;
            world.geometryLayer = 8; world.drawLabels = false; game.world = world; return world;
        }
        StationAnchor Station(StationKind kind)
        {
            var station = Child(kind.ToString()).AddComponent<StationAnchor>(); station.kind = kind;
            station.transform.SetParent(game.ship.transform); station.transform.position = game.captain.Position; return station;
        }

        [TearDown] public void TearDown()
        {
            game.ClearAreaStrikes(); foreach (var enemy in game.enemies.ToArray()) if (enemy != null) Object.DestroyImmediate(enemy.gameObject);
            Object.DestroyImmediate(root); foreach (Object asset in assets) if (asset != null) Object.DestroyImmediate(asset); assets.Clear();
            GameSession.RestoreCurrentAfterValidation(previous); Time.timeScale = scale; UnityEngine.Random.state = random;
        }

        [Test] public void CaptainHasThreeSlotsDespiteBackpackUpgradeAndCapacityFishBuff()
        {
            game.upgradeLevels[(int)UpgradeKind.Backpack] = 3; game.captainBuffs.capacity = 120; game.diverBuffs.capacity = 120;
            Assert.That(game.GetBagCapacity(game.captain), Is.EqualTo(3)); Assert.That(game.GetBagCapacity(game.diver), Is.EqualTo(19));
            Assert.That(game.GetCollectSpeedMultiplier(game.captain), Is.EqualTo(1));
        }

        [Test] public void HeldCaptainInteractionCollectsNearTheAnchorInsteadOfStartingBoarding()
        {
            WorldGenerator world = World(); Assert.That(game.BeginDive(game.captain));
            var pickup = Child("Loose wood").AddComponent<LootPickup>(); pickup.world = world; pickup.resource = ResourceKind.Wood;
            pickup.amount = 6; pickup.collectSeconds = .1f; pickup.transform.position = game.captain.Position; world.pickups.Add(pickup);
            game.HandleCommand(game.captain, new CrewCommand { interact = true, interactHeld = true });
            Assert.That(game.captainBoardHeld, Is.False, "nearby collectable must win over the self-boarding E hold");
            typeof(LootPickup).GetField("startedAt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pickup, Time.time - 1);
            game.HandleCommand(game.captain, new CrewCommand { interactHeld = true });
            Assert.That(game.captain.backpack, Has.Count.EqualTo(3)); Assert.That(pickup.amount, Is.EqualTo(3));
            Assert.That(game.captain.onboard, Is.False); Assert.That(game.captainBoardHeld, Is.False);
        }

        [Test] public void DiverUsesInteractForHidingAndIgnoresTheRetiredHideShortcut()
        {
            WorldGenerator world = World(); Assert.That(game.BeginDive());
            var hide = Child("Hide spot").AddComponent<UnderwaterHideSpot>(); hide.world = world; hide.transform.position = game.diver.Position;
            world.hideSpots.Add(hide);
            game.HandleCommand(game.diver, new CrewCommand { hide = true }); Assert.That(game.IsHidden(game.diver), Is.False);
            game.HandleCommand(game.diver, new CrewCommand { interact = true }); Assert.That(game.IsHidden(game.diver));
            game.HandleCommand(game.diver, new CrewCommand { hide = true }); Assert.That(game.IsHidden(game.diver));
            game.HandleCommand(game.diver, new CrewCommand { interact = true }); Assert.That(game.IsHidden(game.diver), Is.False);
        }

        [Test] public void UnconsciousDiverStaysDownAfterWinchingAndNeedsAnUninterruptedFourSecondRevive()
        {
            World(); Station(StationKind.Winch); Assert.That(game.BeginDive());
            game.diver.RestorePose(false, game.BoatAnchor + Vector3.down * 3, Vector3.zero, Vector2.up);
            game.FaintDiver("fixture"); game.SetWinchActive(true);
            for (int i = 0; i < 6 && !game.diver.onboard; i++)
            { game.TryWinchQte(game.WinchQteCenter); game.StepSimulation(tuning.winchCycle + .01f); }
            Assert.That(game.diver.onboard); Assert.That(game.diver.downed); Assert.That(game.IsActorInputBlocked(game.diver));
            Assert.That(game.diverFainted, Is.False, "water faint state is converted into a revivable deck downed state");
            game.StepSimulation(8); Assert.That(game.diver.downed, Is.True, "oxygen refill and elapsed time must not revive the rescued diver");
            game.captain.RestorePose(true, game.diver.Position + Vector3.left * .5f, Vector3.zero, Vector2.right);
            Assert.That(tuning.reviveSeconds, Is.EqualTo(4));
            game.HandleCommand(game.captain, new CrewCommand { interactHeld = true }); game.StepSimulation(3.9f); Assert.That(game.diver.downed);
            game.HandleCommand(game.captain, new CrewCommand()); game.StepSimulation(.1f); Assert.That(game.reviveProgress[(int)CrewRole.Captain], Is.Zero);
            game.HandleCommand(game.captain, new CrewCommand { interactHeld = true }); game.StepSimulation(3.99f); Assert.That(game.diver.downed);
            game.StepSimulation(.02f); Assert.That(game.diver.downed, Is.False); Assert.That(game.IsIncapacitated(game.diver), Is.False); Assert.That(game.diverBites, Is.Zero);
        }

        void RecoverUnconsciousCaptain(bool electric)
        {
            game.obstacleMask = 0;
            StationAnchor winch = Station(StationKind.Winch); winch.transform.position = game.diver.Position;
            Assert.That(game.BeginDive(game.captain));
            game.captain.RestorePose(false, game.BoatAnchor + Vector3.down * 6, Vector3.zero, Vector2.up);
            if (electric)
            {
                game.upgradeLevels[(int)UpgradeKind.Rope] = 3; game.circuitRepaired = true; game.power = 50;
                for (int i = 0; i < tuning.electricWinchSignalCount; i++) game.SignalWinch(game.captain);
                Assert.That(game.winchElectric); Assert.That(game.winchSignalAuthorized);
                Assert.That(game.winchActive, Is.False, "the powered recovery is independent of a manual operator");
                game.diver.RestorePose(true, game.diver.Position + Vector3.right * 20, Vector3.zero, Vector2.left);
                Assert.That(game.IsNearStation(game.diver, StationKind.Winch), Is.False);
            }
            game.captain.ReceiveDamage(tuning.baseDiverHitCapacity);
            Assert.That(game.IsFainted(game.captain)); Assert.That(game.IsActorInputBlocked(game.captain));
            if (!electric)
            {
                game.SetWinchActive(true, game.diver);
                Assert.That(game.winchActive); Assert.That(game.winchElectric, Is.False);
                Assert.That(game.winchOperatorRole, Is.EqualTo(CrewRole.Diver));
            }
            for (int i = 0; i < 12 && !game.captain.onboard; i++)
            {
                if (!electric) Assert.That(game.TryWinchQte(game.WinchQteCenter));
                game.StepSimulation(electric ? .25f : tuning.winchCycle + .01f);
            }
            Assert.That(game.captain.onboard); Assert.That(game.captain.downed);
            Assert.That(game.IsFainted(game.captain), Is.False, "water fainting becomes deck downed state instead of reviving the captain");
            Assert.That(game.IsActorInputBlocked(game.captain));
        }

        [TestCase(false)] [TestCase(true)]
        public void RecoveredUnconsciousCaptainRequiresAnUninterruptedFourSecondRevive(bool electric)
        {
            RecoverUnconsciousCaptain(electric);
            game.StepSimulation(8);
            Assert.That(game.captain.downed, Is.True, "boarding, oxygen refill and elapsed time must not revive the captain");
            Assert.That(game.IsActorInputBlocked(game.captain));
            Assert.That(game.BeginDive(game.captain), Is.False, "rescued captain cannot act before the teammate revives them");
            game.diver.RestorePose(true, game.captain.Position + Vector3.right * .5f, Vector3.zero, Vector2.left);
            Assert.That(tuning.reviveSeconds, Is.EqualTo(4));
            game.HandleCommand(game.diver, new CrewCommand { interactHeld = true }); game.StepSimulation(3.9f);
            Assert.That(game.captain.downed);
            game.HandleCommand(game.diver, new CrewCommand()); game.StepSimulation(.1f);
            Assert.That(game.reviveProgress[(int)CrewRole.Diver], Is.Zero);
            game.HandleCommand(game.diver, new CrewCommand { interactHeld = true }); game.StepSimulation(3.99f);
            Assert.That(game.captain.downed);
            game.StepSimulation(.02f);
            Assert.That(game.captain.downed, Is.False); Assert.That(game.IsIncapacitated(game.captain), Is.False);
            Assert.That(game.IsActorInputBlocked(game.captain), Is.False); Assert.That(game.captain.health, Is.GreaterThan(0));
        }

        [Test] public void SavedRecoveredCaptainRemainsDownAndRestartsTheFourSecondReviveAfterLoading()
        {
            RecoverUnconsciousCaptain(false);
            game.diver.RestorePose(true, game.captain.Position + Vector3.right * .5f, Vector3.zero, Vector2.left);
            game.HandleCommand(game.diver, new CrewCommand { interactHeld = true }); game.StepSimulation(2);
            Assert.That(game.reviveProgress[(int)CrewRole.Diver], Is.EqualTo(2));
            GameSaveData saved = game.CaptureSaveData();
            Assert.That(saved.captain.onboard); Assert.That(saved.captain.downed); Assert.That(saved.captain.fainted, Is.False);
            game.captain.Revive();
            Assert.That(game.TryRestoreSaveData(saved, out string error), Is.True, error);
            Assert.That(game.captain.onboard); Assert.That(game.captain.downed); Assert.That(game.IsActorInputBlocked(game.captain));
            Assert.That(game.reviveProgress[(int)CrewRole.Diver], Is.Zero, "loading cannot retain an abandoned rescue channel");
            game.StepSimulation(4.1f); Assert.That(game.captain.downed);
            game.HandleCommand(game.diver, new CrewCommand { interactHeld = true }); game.StepSimulation(3.99f);
            Assert.That(game.captain.downed);
            game.StepSimulation(.02f);
            Assert.That(game.captain.downed, Is.False); Assert.That(game.IsActorInputBlocked(game.captain), Is.False);
        }

        [Test] public void DamagedEngineGivesHelmFeedbackAndRepairedEngineAllowsTheSameInteraction()
        {
            Station(StationKind.Helm); game.HandleCommand(game.captain, new CrewCommand { interact = true });
            Assert.That(game.StatusMessage, Does.Contain("引擎损坏")); Assert.That(game.ship.helmOperator, Is.Null);
            game.power = 100; Assert.That(game.TryConsumeSailing(1), Is.False);
            game.MarkEngineRepaired(); game.HandleCommand(game.captain, new CrewCommand { interact = true });
            Assert.That(game.ship.helmOperator, Is.SameAs(game.captain)); Assert.That(game.TryConsumeSailing(1));
        }

        [Test] public void InputMigrationRemovesOnlyDiverHideAndPreservesOtherIdsAndOverrides()
        {
            InputActionAsset asset = Keep(CrewInputDefaults.CreateAsset());
            Assert.That(asset.FindAction("Captain/Interact").bindings[0].path, Is.EqualTo("<Keyboard>/e"));
            Assert.That(asset.FindAction("Captain/Collect").bindings[0].path, Is.EqualTo("<Keyboard>/e"));
            Assert.That(asset.FindAction("Diver/Interact").bindings[0].path, Is.EqualTo("<Keyboard>/enter")); Assert.That(asset.FindAction("Diver/Hide", false), Is.Null);
            InputAction collect = asset.FindAction("Captain/Collect"); collect.ChangeBinding(0).WithPath("<Keyboard>/r"); collect.ApplyBindingOverride(0, "<Keyboard>/g");
            asset.FindActionMap("Diver").AddAction("Hide", InputActionType.Button, "<Keyboard>/m");
            Guid pauseId = asset.FindAction("Diver/Pause").id, pauseBindingId = asset.FindAction("Diver/Pause").bindings[0].id;
            Assert.That(InputAssetBuilder.MigrateCrewInteractionBindings(asset, false), Has.Count.EqualTo(2)); Assert.That(asset.FindAction("Diver/Hide", false), Is.Not.Null);
            InputAssetBuilder.MigrateCrewInteractionBindings(asset, true);
            Assert.That(collect.bindings[0].path, Is.EqualTo("<Keyboard>/e")); Assert.That(collect.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/g"));
            Assert.That(asset.FindAction("Diver/Hide", false), Is.Null); Assert.That(asset.FindAction("Diver/Pause").id, Is.EqualTo(pauseId)); Assert.That(asset.FindAction("Diver/Pause").bindings[0].id, Is.EqualTo(pauseBindingId));
            Assert.That(InputAssetBuilder.MigrateCrewInteractionBindings(asset, true), Is.Empty);
        }

        [Test] public void RuntimeInputCopyRemovesLegacyHideFromRebindingWithoutMutatingSharedAsset()
        {
            InputActionAsset shared = Keep(CrewInputDefaults.CreateAsset()); shared.FindActionMap("Diver").AddAction("Hide", InputActionType.Button, "<Keyboard>/m");
            var reader = Child("Diver reader").AddComponent<CrewInputReader>(); reader.preferencesPrefix = "LifeLine.CrewRegression." + Guid.NewGuid() + "."; reader.Initialize(CrewRole.Diver, shared);
            Assert.That(reader.BindableActions.Any(action => action.name == "Hide"), Is.False); Assert.That(reader.RuntimeActions.FindAction("Diver/Hide", false), Is.Null);
            Assert.That(shared.FindAction("Diver/Hide", false), Is.Not.Null); Assert.That(reader.RuntimeActions.FindAction("Diver/Interact").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/enter"));
        }
    }
}
