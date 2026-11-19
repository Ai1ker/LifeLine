using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Lifeline.Tests
{
    public sealed class EngineHelmFeedbackTests
    {
        GameObject root;
        GameSession game, previous;
        GameTuning tuning;
        UnityEngine.Random.State random;
        float timeScale;

        [SetUp] public void SetUp()
        {
            previous = GameSession.Current; random = UnityEngine.Random.state; timeScale = Time.timeScale;
            root = new GameObject("Engine helm feedback fixture");
            game = root.AddComponent<GameSession>(); game.autoStart = false; game.enabled = false;
            tuning = ScriptableObject.CreateInstance<GameTuning>(); tuning.maximumSharks = 0; tuning.birdInterval = 10000;
            game.tuning = tuning;
            game.ship = Child("Ship").AddComponent<ShipMotor>(); game.ship.enabled = false;
            game.ship.transform.position = Vector3.right * 6000;
            game.captain = Actor("Captain"); game.diver = Actor("Diver"); game.Initialize();
            game.DisableTutorialForTests(); // This fixture tests engine/helm feedback, not the ruin gate.
        }

        GameObject Child(string name) { var child = new GameObject(name); child.transform.SetParent(root.transform); return child; }
        CrewActor Actor(string name)
        {
            var actor = Child(name).AddComponent<CrewActor>(); actor.enabled = false; return actor;
        }
        StationAnchor Helm(CrewActor actor)
        {
            var station = Child("Helm").AddComponent<StationAnchor>(); station.kind = StationKind.Helm;
            station.transform.position = actor.Position; return station;
        }

        [TearDown] public void TearDown()
        {
            game.ClearAreaStrikes(); Object.DestroyImmediate(root); Object.DestroyImmediate(tuning);
            GameSession.RestoreCurrentAfterValidation(previous); Time.timeScale = timeScale; UnityEngine.Random.state = random;
        }

        [TestCase(CrewRole.Captain)] [TestCase(CrewRole.Diver)]
        public void DamagedHelmStillReceivesInteractionAndExplainsItsBlocker(CrewRole role)
        {
            CrewActor actor = role == CrewRole.Captain ? game.captain : game.diver;
            StationAnchor station = Helm(actor); int stationRequests = 0; game.StationRequested += (kind, crew) => stationRequests++;
            game.power = 100; game.StatusMessage = "before";
            Assert.That(station.CanInteract(actor), Is.True);
            Assert.That(actor.NearbyStation, Is.SameAs(station));
            game.HandleCommand(actor, new CrewCommand { interact = true });
            Assert.That(game.StatusMessage, Is.EqualTo(GameSession.EngineUnavailableMessage));
            Assert.That(game.ship.helmOperator, Is.Null); Assert.That(actor.controlsCaptured, Is.False);
            Assert.That(stationRequests, Is.Zero, "a blocked helm must not open an unrelated station panel");
            Assert.That(game.TryConsumeSailing(1), Is.False); Assert.That(game.power, Is.EqualTo(100));
        }

        [TestCase(CrewRole.Captain)] [TestCase(CrewRole.Diver)]
        public void NearbyHelmWarningSurvivesSharedMessagesAndDisappearsAfterRepair(CrewRole role)
        {
            CrewActor actor = role == CrewRole.Captain ? game.captain : game.diver;
            StationAnchor station = Helm(actor); var hud = Child("HUD").AddComponent<GameHUDController>(); hud.session = game;
            var overlay = hud.gameObject.AddComponent<CrewScreenOverlay>();
            overlay.Initialize(hud);
            game.HandleCommand(actor, new CrewCommand { interact = true });
            game.Tell("another crew operation completed");
            string warning = UiLocalization.Text(GameSession.EngineUnavailableMessage);
            Assert.That(overlay.TryGetPrompt(actor, out var target, out string label), Is.True);
            Assert.That(target, Is.EqualTo(station.transform.position));
            Assert.That(label, Does.Contain("\n" + warning));
            Assert.That(label, Does.Contain(actor.input.GetBindingLabel("Interact")));
            game.MarkEngineRepaired();
            Assert.That(station.GetInteractionWarning(actor), Is.Empty);
            Assert.That(overlay.TryGetPrompt(actor, out _, out label), Is.True);
            Assert.That(label, Does.Not.Contain(warning));
        }

        [Test] public void DamageFeedbackPrecedesStaleOperatorAndDirectMotorEntryAlsoRejectsDamage()
        {
            Helm(game.captain); game.ship.helmOperator = game.diver;
            game.HandleCommand(game.captain, new CrewCommand { interact = true });
            Assert.That(game.StatusMessage, Is.EqualTo(GameSession.EngineUnavailableMessage)); Assert.That(game.ship.helmOperator, Is.Null);
            game.ship.SetHelm(game.diver);
            Assert.That(game.ship.helmOperator, Is.Null); Assert.That(game.StatusMessage, Is.EqualTo(GameSession.EngineUnavailableMessage));
        }

        [TestCase(CrewRole.Captain)] [TestCase(CrewRole.Diver)]
        public void RepairedZeroPowerHelmAllowsTakingAndLeavingButCannotSail(CrewRole role)
        {
            CrewActor actor = role == CrewRole.Captain ? game.captain : game.diver;
            Helm(actor); game.MarkEngineRepaired(); game.power = 0;
            game.HandleCommand(actor, new CrewCommand { interact = true });
            Assert.That(game.ship.helmOperator, Is.SameAs(actor)); Assert.That(game.TryConsumeSailing(1), Is.False);
            game.HandleCommand(actor, new CrewCommand { interact = true }); Assert.That(game.ship.helmOperator, Is.Null);
        }
    }
}
