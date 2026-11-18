using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Lifeline.Tests
{
    public sealed class DeveloperOxygenTests
    {
        GameObject root; GameSession game, previous; GameTuning tuning;
        float scale; Random.State random;
        [SetUp] public void SetUp()
        {
            previous = GameSession.Current; scale = Time.timeScale; random = Random.state;
            root = new GameObject("Development oxygen fixture"); game = root.AddComponent<GameSession>(); game.autoStart = false;
            tuning = ScriptableObject.CreateInstance<GameTuning>(); game.tuning = tuning; tuning.maximumSharks = 0;
            var ship = new GameObject("Ship"); ship.transform.SetParent(root.transform); ship.AddComponent<Rigidbody>(); game.ship = ship.AddComponent<ShipMotor>();
            game.captain = Actor(); game.diver = Actor(); game.Initialize();
        }
        CrewActor Actor()
        {
            var obj = new GameObject("Crew"); obj.transform.SetParent(root.transform); obj.AddComponent<Rigidbody>(); obj.AddComponent<CapsuleCollider>();
            var actor = obj.AddComponent<CrewActor>(); actor.enabled = false; return actor;
        }
        [TearDown] public void TearDown()
        {
            game.ClearAreaStrikes(); Object.DestroyImmediate(root); Object.DestroyImmediate(tuning);
            GameSession.RestoreCurrentAfterValidation(previous); Time.timeScale = scale; Random.state = random;
        }
        void TickOxygen(float seconds)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(GameSession).GetMethod("UpdateOxygen", flags).Invoke(game, new object[] { seconds });
            typeof(GameSession).GetMethod("UpdateCaptainOxygen", flags).Invoke(game, new object[] { seconds });
        }
        [Test] public void InfiniteOxygenPreventsLargeStepFaintingForBothCrewAndTurningOffRestoresDrain()
        {
            var tools = DeveloperTestAccess.AddTools(root);
            game.BeginDive(game.captain); game.BeginDive(game.diver); game.captain.oxygen = game.diver.oxygen = 1;
            DeveloperTestAccess.Invoke(tools, "SetInfiniteOxygen", game, true); TickOxygen(1000);
            foreach (var actor in game.Crew)
            {
                Assert.That(game.IsFainted(actor), Is.False); Assert.That(actor.oxygen, Is.EqualTo(actor.oxygenMax));
            }
            Assert.That(JsonUtility.ToJson(game.CaptureSaveData()), Does.Not.Contain("developerInfiniteOxygen"));
            DeveloperTestAccess.Invoke(tools, "SetInfiniteOxygen", game, false); TickOxygen(1);
            foreach (var actor in game.Crew) Assert.That(actor.oxygen, Is.EqualTo(actor.oxygenMax - 1).Within(.001f));
            game.captain.oxygen = game.diver.oxygen = .5f; TickOxygen(1);
            Assert.That(game.IsFainted(game.captain)); Assert.That(game.IsFainted(game.diver));
            DeveloperTestAccess.Invoke(tools, "SetInfiniteOxygen", game, true); TickOxygen(0);
            Assert.That(game.IsFainted(game.captain)); Assert.That(game.IsFainted(game.diver));
            // This EditMode fixture never awakens the tool, so Unity does not send OnDestroy.
            DeveloperTestAccess.Invoke(tools, "OnDestroy");
            Object.DestroyImmediate(tools); Assert.That(DeveloperTestAccess.InfiniteOxygen(game), Is.False);
        }
    }
}
