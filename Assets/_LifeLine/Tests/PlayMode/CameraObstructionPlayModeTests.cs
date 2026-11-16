using System.Collections;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace Lifeline.Tests
{
    public sealed class CameraObstructionPlayModeTests
    {
        [UnityTest]
        public IEnumerator RealCinemachineObstructionPullsCameraClearAndRestoresDistance()
        {
            Assert.That(Application.isPlaying, Is.True, "Run in the PlayMode tab.");
            float oldScale = Time.timeScale;
            bool hadPreference = PlayerPrefs.HasKey(GameCameraRig.ShipSplitPreferenceKey);
            int oldPreference = PlayerPrefs.GetInt(GameCameraRig.ShipSplitPreferenceKey, 0);
            var existingBrains = UnityEngine.Object.FindObjectsByType<CinemachineBrain>();
            var existingRigs = UnityEngine.Object.FindObjectsByType<GameCameraRig>();
            var brainEnabled = new bool[existingBrains.Length];
            var rigEnabled = new bool[existingRigs.Length];
            GameObject fixture = null;
            try
            {
                Time.timeScale = 1;
                for (int i = 0; i < existingRigs.Length; i++) { rigEnabled[i] = existingRigs[i].enabled; existingRigs[i].enabled = false; }
                for (int i = 0; i < existingBrains.Length; i++) { brainEnabled[i] = existingBrains[i].enabled; existingBrains[i].enabled = false; }
                // Use the default 3D physics scene, as the official CM extensions query Physics.
                // A remote XY location avoids the gameplay ship/world without changing those objects.
                fixture = new GameObject("Cinemachine obstruction regression"); fixture.SetActive(false);
                var cap = Actor(fixture.transform, "Captain", new Vector3(5432, 4321, 0));
                var div = Actor(fixture.transform, "Diver", new Vector3(5434, 4321, 0));
                var rig = fixture.AddComponent<GameCameraRig>();
                rig.obstacleMask = 1 << 8;
                rig.followDamping = Vector3.zero;
                rig.returnFromObstructionDamping = .08f;
                rig.shipViewBounds = new Rect(-10000, -10000, 20000, 20000);
                rig.underwaterViewBounds = rig.shipViewBounds;
                rig.Initialize(null, cap, div, null);
                rig.SetNetworkView(true, CrewRole.Captain);
                fixture.SetActive(true);
                // Awake also configures the rig. Switch to manual only after activation; each tick
                // still follows a real Unity frame, with CM's verified 6.6 delta-time overload.
                rig.enabled = false;
                rig.captainVirtualCamera.Priority = 10000;
                rig.diverVirtualCamera.Priority = 10000;
                var brain = rig.captainBrain;
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
                brain.IgnoreTimeScale = true;
                Assert.That(rig.captainFollow, Is.Not.Null);
                Assert.That(rig.captainDeoccluder.enabled && rig.captainDeoccluder.AvoidObstacles.Enabled, Is.True);
                Assert.That(rig.captainDecollider.enabled && rig.captainDecollider.Decollision.Enabled, Is.True);
                for (int frame = 0; frame < 8; frame++)
                {
                    yield return null;
                    rig.RefreshViewNow(false);
                    brain.ManualUpdate(Time.frameCount, 1f / 60);
                }
                Vector3 target = rig.captainFocus.position;
                float unobstructed = Mathf.Abs(rig.captainCamera.transform.position.z - target.z);
                Assert.That(unobstructed, Is.EqualTo(rig.cameraDistance).Within(.3f), "The real output camera must first reach its unobstructed follow distance.");

                var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.name = "Solid between CM focus and output camera"; obstacle.layer = 8;
                obstacle.transform.SetParent(fixture.transform, true);
                obstacle.transform.position = Vector3.Lerp(target, rig.captainCamera.transform.position, .5f);
                obstacle.transform.localScale = new Vector3(4, 4, 2);
                Physics.SyncTransforms();
                float greatestDisplacement = 0, nearestDistance = unobstructed;
                for (int frame = 0; frame < 60; frame++)
                {
                    yield return null;
                    rig.RefreshViewNow(false); Physics.SyncTransforms();
                    brain.ManualUpdate(Time.frameCount, 1f / 60);
                    // This public method is verified in the installed CM6.6 Deoccluder source.
                    greatestDisplacement = Mathf.Max(greatestDisplacement,
                        rig.captainDeoccluder.GetCameraDisplacementDistance(rig.captainVirtualCamera));
                    nearestDistance = Mathf.Min(nearestDistance, Mathf.Abs(rig.captainCamera.transform.position.z - target.z));
                    if (greatestDisplacement > .1f && nearestDistance < unobstructed - 1) break;
                }
                Assert.That(greatestDisplacement > .1f || nearestDistance < unobstructed - 1, Is.True,
                    "A real collider must change the official pipeline's displacement or target distance.");
                Assert.That(Mathf.Abs(rig.captainCamera.transform.position.z - target.z), Is.LessThan(unobstructed - 1),
                    "PullCameraForward should place the output camera on the target side of the obstacle.");
                Assert.That(Physics.CheckSphere(rig.captainCamera.transform.position, rig.cameraCollisionRadius * .9f,
                    1 << 8, QueryTriggerInteraction.Ignore), Is.False, "The real output camera must not remain inside the solid.");

                UnityEngine.Object.Destroy(obstacle);
                yield return null;
                float recovered = 0;
                for (int frame = 0; frame < 120; frame++)
                {
                    yield return null;
                    rig.RefreshViewNow(false); Physics.SyncTransforms();
                    brain.ManualUpdate(Time.frameCount, 1f / 60);
                    recovered = Mathf.Abs(rig.captainCamera.transform.position.z - target.z);
                    if (Mathf.Abs(recovered - unobstructed) < .25f) break;
                }
                Assert.That(recovered, Is.EqualTo(unobstructed).Within(.25f), "After removing the obstacle the camera must recover its follow distance.");
            }
            finally
            {
                if (fixture != null) { fixture.SetActive(false); UnityEngine.Object.Destroy(fixture); }
                for (int i = 0; i < existingRigs.Length; i++) if (existingRigs[i] != null) existingRigs[i].enabled = rigEnabled[i];
                for (int i = 0; i < existingBrains.Length; i++) if (existingBrains[i] != null) existingBrains[i].enabled = brainEnabled[i];
                if (hadPreference) PlayerPrefs.SetInt(GameCameraRig.ShipSplitPreferenceKey, oldPreference);
                else PlayerPrefs.DeleteKey(GameCameraRig.ShipSplitPreferenceKey);
                PlayerPrefs.Save();
                Time.timeScale = oldScale;
            }
        }

        static CrewActor Actor(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position;
            var actor = go.AddComponent<CrewActor>(); actor.enabled = false; actor.onboard = true;
            return actor;
        }
    }
}
