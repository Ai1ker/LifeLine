using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lifeline.Tests
{
    public sealed class MapSeparationPlayModeTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        readonly List<GameMapPartition> partitions = new List<GameMapPartition>();
        readonly List<Scene> scenes = new List<Scene>();

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var maps in partitions) if (maps != null) maps.ReleaseRuntimeMaps();
            foreach (var root in objects) if (root != null) Object.Destroy(root);
            yield return null;
            foreach (Scene scene in scenes)
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            objects.Clear(); partitions.Clear(); scenes.Clear();
        }

        GameObject Keep(string name)
        {
            var root = new GameObject(name); objects.Add(root); return root;
        }

        GameSession Session(out GameMapPartition maps)
        {
            var root = Keep("Separate maps regression"); root.SetActive(false);
            var session = root.AddComponent<GameSession>(); session.autoStart = false; session.phase = SessionPhase.Paused;
            session.ship = Keep("Actual cabin map ship").AddComponent<ShipMotor>();
            session.ship.body = session.ship.GetComponent<Rigidbody>(); session.ship.body.isKinematic = true;
            session.world = Keep("Actual underwater map").AddComponent<WorldGenerator>();
            session.captain = Actor("Captain", CrewRole.Captain, new Vector3(-3, .9f, 0));
            session.diver = Actor("Diver", CrewRole.Diver, new Vector3(6, .9f, 0));
            maps = root.AddComponent<GameMapPartition>(); partitions.Add(maps); maps.session = session; session.maps = maps;
            return session;
        }

        CrewActor Actor(string name, CrewRole role, Vector3 position)
        {
            var root = Keep(name); root.transform.position = position;
            var actor = root.AddComponent<CrewActor>(); actor.enabled = false; actor.role = role; actor.onboard = true;
            return actor;
        }

        void KeepScenes(GameMapPartition maps)
        {
            scenes.Add(maps.ShipScene); scenes.Add(maps.UnderwaterScene);
        }

        [UnityTest]
        public IEnumerator AuthoredMapOffsetIsIdempotentAndCrewTransfersBetweenOwnedAdditiveScenes()
        {
            var session = Session(out var maps);
            int before = SceneManager.sceneCount;
            maps.InitializeForAuthoring(session);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(before), "authoring retains one serialized Gameplay scene");
            Assert.That(session.ship.Position.y, Is.EqualTo(1000).Within(.001));
            Assert.That(session.captain.Position.y, Is.EqualTo(1000.9f).Within(.001));
            Assert.That(session.BoatAnchor.y, Is.EqualTo(-2.2f).Within(.001));
            maps.InitializeForAuthoring(session); maps.Initialize(session); KeepScenes(maps); maps.Initialize(session);
            Assert.That(session.ship.Position.y, Is.EqualTo(1000).Within(.001), "serialized offset is never added twice");
            Assert.That(maps.RuntimeMapsReady, Is.True);
            Assert.That(maps.ShipScene, Is.Not.EqualTo(maps.UnderwaterScene));
            Assert.That(session.ship.gameObject.scene, Is.EqualTo(maps.ShipScene));
            Assert.That(session.world.gameObject.scene, Is.EqualTo(maps.UnderwaterScene));
            Assert.That(session.captain.gameObject.scene, Is.EqualTo(maps.ShipScene));
            session.diver.SetOnboard(false, session.BoatAnchor + Vector3.down);
            maps.MoveCrewToMap(session.diver);
            Assert.That(session.diver.gameObject.scene, Is.EqualTo(maps.UnderwaterScene));
            Assert.That(session.diver.Position.y, Is.LessThan(0));
            Vector3 water = new Vector3(31, -5, 0);
            Assert.That(maps.ShipToWaterPoint(maps.WaterToShipPoint(water)), Is.EqualTo(water));
            session.diver.SetOnboard(true, session.ShipPoint(session.bankArrivalLocal));
            maps.MoveCrewToMap(session.diver);
            Assert.That(session.diver.gameObject.scene, Is.EqualTo(maps.ShipScene));
            Scene shipScene = maps.ShipScene, waterScene = maps.UnderwaterScene;
            maps.ReleaseRuntimeMaps();
            yield return null;
            for (int i = 0; i < 30 && (shipScene.isLoaded || waterScene.isLoaded); i++) yield return null;
            Assert.That(shipScene.isLoaded || waterScene.isLoaded, Is.False, "return/menu cleanup leaves no map scene alive");
        }

        [UnityTest]
        public IEnumerator DestroyingSessionUnloadsOnlyItsOwnAdditiveMaps()
        {
            var session = Session(out var maps); maps.Initialize(session); KeepScenes(maps);
            session.gameObject.SetActive(true);
            var other = Session(out var otherMaps); otherMaps.Initialize(other); KeepScenes(otherMaps);
            Scene shipScene = maps.ShipScene, waterScene = maps.UnderwaterScene;
            Assert.That(shipScene, Is.Not.EqualTo(otherMaps.ShipScene));
            Assert.That(waterScene, Is.Not.EqualTo(otherMaps.UnderwaterScene));
            Object.Destroy(session.gameObject);
            for (int i = 0; i < 30 && (shipScene.isLoaded || waterScene.isLoaded); i++) yield return null;
            Assert.That(shipScene.isLoaded || waterScene.isLoaded, Is.False);
            Assert.That(otherMaps.RuntimeMapsReady, Is.True, "one fixture/session never borrows another's map scene");
        }

        [UnityTest]
        public IEnumerator PartitionCreatedBeforeShipWaitsAndPreservesAnAlreadyInitializedShipStartPose()
        {
            var root = Keep("Late map dependencies");
            var session = root.AddComponent<GameSession>(); session.autoStart = false; session.phase = SessionPhase.Paused;
            var maps = root.AddComponent<GameMapPartition>(); partitions.Add(maps); maps.session = session; session.maps = maps;
            Assert.That(maps.AppliedShipOffset, Is.EqualTo(Vector3.zero), "missing ship cannot consume the pending map offset");
            session.ship = Keep("Previously initialized ship").AddComponent<ShipMotor>(); session.ship.Initialize(session);
            session.captain = Actor("Late captain", CrewRole.Captain, new Vector3(-3, .9f, 0));
            session.diver = Actor("Late diver", CrewRole.Diver, new Vector3(6, .9f, 0));
            maps.Initialize(session); KeepScenes(maps);
            Assert.That(session.ship.StartPosition.y, Is.EqualTo(1000).Within(.001));
            Assert.That(session.ship.Position.y, Is.EqualTo(1000).Within(.001));
            session.ship.ResetToStart(); session.ship.Initialize(session); maps.Initialize(session);
            Assert.That(session.ship.Position.y, Is.EqualTo(1000).Within(.001), "restart cannot undo the map separation");
            Assert.That(session.captain.Position.y, Is.EqualTo(1000.9f).Within(.001));
            yield return null;
        }

        [UnityTest]
        public IEnumerator WaterCameraCannotSeePhysicalCabinEvenWhenItsRendererAndColliderUseWorldLayer()
        {
            var session = Session(out var maps); maps.Initialize(session); KeepScenes(maps);
            var cabin = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(cabin);
            cabin.name = "Cabin renderer missed by old layer isolation"; cabin.transform.SetParent(session.ship.transform, false);
            cabin.transform.localPosition = new Vector3(0, -3, 0); cabin.transform.localScale = new Vector3(24, 5, 1);
            cabin.layer = 0; // Includes its collider, so renderer-only reassignment deliberately skips it.
            session.diver.SetOnboard(false, new Vector3(0, -5, 0)); maps.MoveCrewToMap(session.diver);
            var rig = Keep("Map-aware Cinemachine rig").AddComponent<GameCameraRig>();
            rig.Initialize(session); rig.RefreshViewNow();
            for (int i = 0; i < 4; i++) yield return null;
            Assert.That(rig.diverCamera.cullingMask & 1, Is.Not.Zero, "the world layer remains visible to the diver");
            Assert.That(GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(rig.diverCamera), cabin.GetComponent<Renderer>().bounds), Is.False,
                "physical map separation protects against every cabin layer/collider combination");
            Assert.That(rig.captainFocus.position.y - rig.diverFocus.position.y, Is.GreaterThan(900));
            Assert.That(rig.diverCamera.cullingMask & (1 << rig.underwaterProxyLayer), Is.Not.Zero);
            Assert.That(rig.captainCamera.cullingMask & (1 << rig.underwaterProxyLayer), Is.Zero);
        }

        [UnityTest]
        public IEnumerator SmallSurfaceProxyHasNoPhysicsAndDeepDiveMarkerStaysInsideDiverViewport()
        {
            var session = Session(out var maps); maps.Initialize(session); KeepScenes(maps);
            session.diver.SetOnboard(false, new Vector3(0, -35, 0)); maps.MoveCrewToMap(session.diver);
            var rig = Keep("Proxy Cinemachine rig").AddComponent<GameCameraRig>(); rig.Initialize(session);
            var proxy = Keep("Small ship proxy").AddComponent<UnderwaterShipProxy>(); proxy.Initialize(session, rig);
            for (int i = 0; i < 4; i++) yield return null;
            proxy.RefreshNow();
            Assert.That(proxy.gameObject.scene, Is.EqualTo(maps.UnderwaterScene));
            Assert.That(proxy.IsSurfaceProxyVisible, Is.True);
            Assert.That(proxy.IsEdgeMarkerVisible, Is.True);
            foreach (Collider collider in proxy.GetComponentsInChildren<Collider>(true)) Assert.That(collider.enabled, Is.False);
            foreach (StationAnchor station in proxy.GetComponentsInChildren<StationAnchor>(true)) Assert.Fail("A proxy must never copy cabin stations: " + station.name);
            var bounds = proxy.visualRoot.GetComponentInChildren<Renderer>().bounds;
            Assert.That(bounds.size.x, Is.LessThan(4));
            Rect pixels = rig.diverCamera.pixelRect; Vector3 marker = proxy.edgeMarker.position;
            Assert.That(marker.x, Is.InRange(pixels.xMin, pixels.xMax));
            Assert.That(marker.y, Is.InRange(pixels.yMin, pixels.yMax));
            session.ship.body.position += Vector3.right * 18; session.ship.transform.position = session.ship.body.position;
            proxy.RefreshNow();
            Assert.That(proxy.transform.position.x, Is.EqualTo(session.BoatAnchor.x).Within(.001));
            session.diver.SetOnboard(true, session.ShipPoint(session.bankArrivalLocal)); maps.MoveCrewToMap(session.diver);
            rig.RefreshViewNow(); proxy.RefreshNow();
            Assert.That(proxy.IsSurfaceProxyVisible || proxy.IsEdgeMarkerVisible, Is.False, "boarding restores only the ship map view");
        }
    }
}
