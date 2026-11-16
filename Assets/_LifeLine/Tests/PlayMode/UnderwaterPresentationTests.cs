using NUnit.Framework;
using UnityEngine;

namespace Lifeline.Tests
{
    public class UnderwaterPresentationTests
    {
        [Test]
        public void DivingCameraHidesHullAndOnboardPeerWithoutChangingPhysicsLayers()
        {
            var root = new GameObject("Underwater layer regression");
            try
            {
                var shipRoot = new GameObject("Ship"); shipRoot.transform.SetParent(root.transform); shipRoot.layer = 10;
                shipRoot.AddComponent<BoxCollider>(); var ship = shipRoot.AddComponent<ShipMotor>();
                var hull = Visual(shipRoot.transform, 10);
                var captain = Actor(root.transform, CrewRole.Captain); var diver = Actor(root.transform, CrewRole.Diver);
                var capVisual = captain.GetComponentInChildren<MeshRenderer>(); var divVisual = diver.GetComponentInChildren<MeshRenderer>();
                // Reproduce a scene saved from an aboard editor preview, before runtime records exist.
                capVisual.gameObject.layer = divVisual.gameObject.layer = hull.gameObject.layer = 12;
                var rig = root.AddComponent<GameCameraRig>(); rig.Initialize(null, captain, diver, ship);
                rig.SetShipSplitPreference(false, false);
                Assert.That(rig.IsSharedView, Is.True);
                diver.onboard = false; rig.RefreshViewNow();
                Assert.That(rig.IsSplitScreen, Is.True);
                Assert.That(hull.gameObject.layer, Is.EqualTo(rig.shipPresentationLayer));
                Assert.That(capVisual.gameObject.layer, Is.EqualTo(rig.shipPresentationLayer));
                Assert.That(divVisual.gameObject.layer, Is.EqualTo(9));
                Assert.That(captain.gameObject.layer, Is.EqualTo(9), "crew collision layer stays unchanged");
                Assert.That(shipRoot.layer, Is.EqualTo(10), "ship collision layer stays unchanged");
                Assert.That(rig.diverCamera.cullingMask & ((1 << 10) | (1 << rig.shipPresentationLayer)), Is.Zero);
                Assert.That(rig.diverCamera.cullingMask & ((1 << 8) | (1 << 9)), Is.EqualTo((1 << 8) | (1 << 9)));
                Assert.That(rig.captainCamera.cullingMask & (1 << rig.shipPresentationLayer), Is.Not.Zero);
                diver.onboard = true; rig.RefreshViewNow();
                Assert.That(rig.IsSharedView, Is.True);
                Assert.That(rig.diverCamera.cullingMask & (1 << rig.shipPresentationLayer), Is.Not.Zero);
                rig.enabled = false;
                Assert.That(capVisual.gameObject.layer, Is.EqualTo(9), "disabling replacement rig restores authored layers");
                Assert.That(hull.gameObject.layer, Is.EqualTo(10));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static MeshRenderer Visual(Transform parent, int layer)
        {
            var visual = new GameObject("VisualRoot"); visual.transform.SetParent(parent); visual.layer = layer;
            return visual.AddComponent<MeshRenderer>();
        }
        static CrewActor Actor(Transform parent, CrewRole role)
        {
            var go = new GameObject(role.ToString()); go.transform.SetParent(parent); go.layer = 9;
            var actor = go.AddComponent<CrewActor>(); actor.enabled = false; actor.role = role; actor.onboard = true;
            Visual(go.transform, 9); return actor;
        }
    }
}
