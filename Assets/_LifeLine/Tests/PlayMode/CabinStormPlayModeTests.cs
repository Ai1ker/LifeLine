using System.Collections;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace Lifeline.Tests
{
    public sealed class CabinStormPlayModeTests
    {
        sealed class JumpInput : ICrewInputSource
        {
            public CrewCommand ReadCommand() => new CrewCommand { jump = true };
            public void SetEnabled(bool value) { }
        }

        [UnityTest] public IEnumerator StormCabinJumpsKeepCamerasOutsideTheCutawayAndCrewSheltered()
        {
            GameSession previous = GameSession.Current;
            float scale = Time.timeScale;
            var random = Random.state;
            var brains = Object.FindObjectsByType<CinemachineBrain>();
            var enabled = new bool[brains.Length];
            var root = new GameObject("Day 2 storm cabin jump regression");
            var tuning = ScriptableObject.CreateInstance<GameTuning>();
            try
            {
                for (int i = 0; i < brains.Length; i++) { enabled[i] = brains[i].enabled; brains[i].enabled = false; }
                var game = root.AddComponent<GameSession>(); game.autoStart = false; game.enabled = false;
                game.tuning = tuning; tuning.maximumSharks = 0; tuning.birdInterval = 10000;
                var shipObject = new GameObject("Ship"); shipObject.transform.SetParent(root.transform);
                shipObject.transform.position = new Vector3(5432, 4321, 0);
                game.ship = shipObject.AddComponent<ShipMotor>(); game.ship.enabled = false;
                foreach (float y in new[] { -4.2f, .2f })
                {
                    var solid = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    solid.transform.SetParent(shipObject.transform, false); solid.layer = 10;
                    solid.transform.localPosition = new Vector3(0, y, 0); solid.transform.localScale = new Vector3(24, .4f, 4);
                }
                game.captain = Crew(root.transform, "Captain"); game.diver = Crew(root.transform, "Diver");
                game.Initialize();
                game.elapsed = tuning.daySeconds * 1.9f; game.day = 2;
                game.captain.RestorePose(true, game.ShipPoint(new Vector3(-3, -3.1f, 0)), Vector3.zero, Vector2.right);
                game.diver.RestorePose(true, game.ShipPoint(new Vector3(3, .9f, 0)), Vector3.zero, Vector2.left);
                game.captain.SetExternalInput(new JumpInput());
                var cameraObject = new GameObject("Camera rig"); cameraObject.transform.SetParent(root.transform); cameraObject.SetActive(false);
                var rig = cameraObject.AddComponent<GameCameraRig>(); rig.obstacleMask = (1 << 8) | (1 << 10);
                rig.followDamping = Vector3.zero; rig.Initialize(game, game.captain, game.diver, game.ship);
                cameraObject.SetActive(true); rig.SetNetworkView(true, CrewRole.Captain);
                rig.captainVirtualCamera.Priority = rig.diverVirtualCamera.Priority = 10000;
                float minY = float.MaxValue, maxY = float.MinValue;
                Vector3[] original = { shipObject.transform.GetChild(0).localPosition, shipObject.transform.GetChild(1).localPosition };
                Assert.That(game.StormActive);
                for (int frame = 0; frame < 180; frame++)
                {
                    yield return new WaitForFixedUpdate();
                    // With the teammate on deck, a cabin jump moves the shared focus through
                    // the deck slab: this is the rendering regression, not just an empty camera test.
                    if (frame == 90)
                    {
                        game.diver.RestorePose(true, game.ShipPoint(new Vector3(3, -3.1f, 0)), Vector3.zero, Vector2.left);
                        game.diver.SetExternalInput(new JumpInput());
                    }
                    // Exercise both local viewport modes without changing player preferences.
                    rig.splitScreenOnShip = frame >= 90; rig.onlineView = false;
                    rig.RefreshViewNow(false);
                    game.lightningTarget = CrewRole.Captain; game.stormWarning = .001f;
                    game.StepSimulation(.002f);
                    minY = Mathf.Min(minY, game.captain.Position.y); maxY = Mathf.Max(maxY, game.captain.Position.y);
                    if (frame > 5)
                    {
                        Assert.That(Mathf.Abs(rig.captainCamera.transform.position.z), Is.EqualTo(rig.cameraDistance).Within(.3f));
                        if (rig.IsSplitScreen) Assert.That(Mathf.Abs(rig.diverCamera.transform.position.z), Is.EqualTo(rig.cameraDistance).Within(.3f));
                    }
                    Assert.That(game.captain.downed, Is.False, "A cabin jump must remain sheltered from lightning.");
                    for (int i = 0; i < 2; i++) Assert.That(shipObject.transform.GetChild(i).localPosition, Is.EqualTo(original[i]));
                }
                Assert.That(maxY - minY, Is.GreaterThan(.8f), "The actor must actually jump during the camera regression.");
            }
            finally
            {
                root.SetActive(false); Object.Destroy(root); Object.Destroy(tuning);
                for (int i = 0; i < brains.Length; i++) if (brains[i] != null) brains[i].enabled = enabled[i];
                GameSession.RestoreCurrentAfterValidation(previous); Time.timeScale = scale; Random.state = random;
            }
        }

        static CrewActor Crew(Transform parent, string name)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent); obj.layer = 9;
            obj.AddComponent<Rigidbody>(); obj.AddComponent<CapsuleCollider>().height = 1.8f;
            return obj.AddComponent<CrewActor>();
        }
    }
}
