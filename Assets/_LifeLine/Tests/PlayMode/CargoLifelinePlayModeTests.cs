using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Lifeline.Tests
{
    public sealed class CargoLifelinePlayModeTests
    {
        GameObject root;
        GameSession game, previous;
        GameTuning tuning;
        float previousScale;
        UnityEngine.Random.State previousRandom;

        [SetUp] public void Setup()
        {
            previous = GameSession.Current; previousScale = Time.timeScale; previousRandom = UnityEngine.Random.state;
            Time.timeScale = 1;
            root = new GameObject("Cargo recovery integration");
            game = root.AddComponent<GameSession>(); game.autoStart = false; game.enabled = false;
            tuning = ScriptableObject.CreateInstance<GameTuning>(); tuning.maximumSharks = 0; game.tuning = tuning;
            game.ship = Child("Ship").AddComponent<ShipMotor>(); game.ship.enabled = false;
            game.captain = Crew("Captain"); game.diver = Crew("Diver"); game.Initialize();
            game.world = Child("Water").AddComponent<WorldGenerator>(); game.world.session = game; game.world.geometryLayer = 8;
            var station = Child("Winch").AddComponent<StationAnchor>(); station.kind = StationKind.Winch;
            station.transform.SetParent(game.ship.transform); station.transform.position = game.captain.Position;
            game.diver.RestorePose(false, game.BoatAnchor + Vector3.down * 5, Vector3.zero, Vector2.down);
            game.diver.oxygen = game.diver.oxygenMax = 1000;
        }

        GameObject Child(string name) { var obj = new GameObject(name); obj.transform.SetParent(root.transform); return obj; }
        CrewActor Crew(string name)
        {
            var obj = Child(name); var capsule = obj.AddComponent<CapsuleCollider>(); capsule.radius = .32f; capsule.height = 1.6f;
            var actor = obj.AddComponent<CrewActor>(); actor.enabled = false; return actor;
        }

        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(root); Object.DestroyImmediate(tuning);
            GameSession.RestoreCurrentAfterValidation(previous); Time.timeScale = previousScale; UnityEngine.Random.state = previousRandom;
        }

        [UnityTest] public IEnumerator LoadingAndHoldingCollectAttachesAVisibleRopeAndWinchDeliversExactContents()
        {
            var cargo = Child("Crate").AddComponent<LootPickup>(); cargo.isCrate = true; cargo.world = game.world;
            cargo.attachSeconds = .04f; cargo.transform.position = game.diver.Position; game.world.pickups.Add(cargo);
            game.diver.backpack.Add(ResourceKind.Wood); game.diver.backpack.Add(ResourceKind.Metal);
            Assert.That(cargo.TryLoad(game.diver)); Assert.That(cargo.TryLoad(game.diver));
            Assert.That(cargo.TryCollect(game.diver));
            yield return new WaitForSeconds(.06f);
            Assert.That(cargo.TryCollect(game.diver)); Assert.That(cargo.attached);
            Assert.That(game.winchCargo, Is.SameAs(cargo), "attachment immediately registers the actual winch target");
            var proxy = Child("Surface proxy").AddComponent<UnderwaterShipProxy>(); proxy.Initialize(game, null);
            var view = cargo.GetComponent<DiverLifelineView>(); view.RefreshNow();
            Assert.That(view.rope.enabled); Assert.That(view.rope.positionCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(Vector2.Distance(view.rope.GetPosition(0), proxy.WinchPoint), Is.LessThan(.001f));
            Assert.That(Vector2.Distance(view.rope.GetPosition(view.rope.positionCount - 1), cargo.transform.TransformPoint(view.cargoAttachmentLocal)), Is.LessThan(.001f));
            var lead = Child("Main lifeline view").AddComponent<DiverLifelineView>(); lead.Initialize(game); lead.RefreshNow();
            Assert.That(lead.shipLead, Is.Not.Null); Assert.That(lead.shipLead.enabled);
            Assert.That(lead.shipLead.sharedMaterial,Is.Not.Null,"an aboard diver must still provide a valid material for the shared outgoing ship cable");
            Assert.That(lead.shipLead.transform.parent, Is.SameAs(game.ship.transform));
            Assert.That(Vector2.Distance(lead.shipLead.GetPosition(0), lead.winchAttachment.position), Is.LessThan(.001f));
            Assert.That(Vector2.Distance(lead.shipLead.GetPosition(1), game.WaterToShipPoint(game.BoatAnchor)), Is.LessThan(.001f));
            int wood = game.GetResource(ResourceKind.Wood), metal = game.GetResource(ResourceKind.Metal);
            game.SetWinchActive(true);
            for (int i = 0; i < 900 && !cargo.consumed; i++) game.StepSimulation(.1f);
            Assert.That(cargo.consumed); Assert.That(game.GetResource(ResourceKind.Wood), Is.EqualTo(wood + 1));
            Assert.That(game.GetResource(ResourceKind.Metal), Is.EqualTo(metal + 1));
            Assert.That(game.diver.onboard, Is.True, "the same operation must recover cargo and diver together");
            Assert.That(game.WinchTargetCount, Is.Zero);
        }

        [Test] public void CaptainReelingDestroysAClosedBlockingHatchAndRecoversTheDiver()
        {
            var wall = Child("Blocking hatch"); wall.layer = 8; wall.transform.position = game.BoatAnchor + Vector3.down * 2;
            var barrier = wall.AddComponent<BoxCollider>(); barrier.size = new Vector3(80, .2f, 2);
            var hatch = wall.AddComponent<TrapdoorGate>(); hatch.barrier = barrier; hatch.SetOpen(false);
            Physics.SyncTransforms(); game.SetWinchActive(true); game.StepSimulation(.1f);
            Assert.That(hatch.destroyed); Assert.That(barrier.enabled, Is.False);
            for (int i = 0; i < 900 && !game.diver.onboard; i++) game.StepSimulation(.1f);
            Assert.That(game.diver.onboard);
        }

        LootPickup Cargo(float distance = 5)
        {
            var cargo = Child("Cargo").AddComponent<LootPickup>(); cargo.isCrate = cargo.attached = true;
            cargo.world = game.world; cargo.transform.position = game.BoatAnchor + Vector3.down * distance;
            cargo.contents.Add(ResourceKind.Wood); game.world.pickups.Add(cargo); game.RegisterAttachedCargo(cargo);
            return cargo;
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void PassiveReelingSharesTotalDistanceAcrossDiverAndEveryCargo(int crates)
        {
            for (int i = 0; i < crates; i++) Cargo();
            game.SignalCaptain(); game.SignalCaptain(); game.SetWinchActive(true);
            float share = tuning.winchPassiveSpeed / (crates + 1);
            game.StepSimulation(1);
            Assert.That(game.WinchTargetCount, Is.EqualTo(crates + 1));
            Assert.That(game.DiverRopeLength, Is.EqualTo(5 - share).Within(.005f));
            foreach (var cargo in game.world.pickups)
                Assert.That(game.GetCargoLifeline(cargo).PathLength, Is.EqualTo(5 - share).Within(.005f));
        }

        [TestCase(true)] [TestCase(false)]
        public void QteSuccessAndFailureAffectAllTargetsAtOneThirdEfficiency(bool good)
        {
            Cargo(); Cargo(); game.SetWinchActive(true);
            Assert.That(game.TryWinchQte(good ? tuning.winchCenter : 0));
            float distance = 5 - (good ? tuning.winchGoodDistance : -tuning.winchBadDistance) / 3;
            Assert.That(game.DiverRopeLength, Is.EqualTo(distance).Within(.005f));
            foreach (var cargo in game.world.pickups)
                Assert.That(game.GetCargoLifeline(cargo).PathLength, Is.EqualTo(distance).Within(.005f));
        }

        [Test] public void ElectricSignalReelsAllTargetsAndContinuesAfterDiverBoards()
        {
            Cargo(8); Cargo(8);
            game.diver.RestorePose(false, game.BoatAnchor + Vector3.down * .91f, Vector3.zero, Vector2.down);
            game.upgradeLevels[(int)UpgradeKind.Rope] = 3; game.circuitRepaired = true; game.power = 100;
            game.SignalCaptain(); game.SignalCaptain(); game.SignalCaptain();
            Assert.That(game.winchElectric && game.winchSignalAuthorized);
            float speed = tuning.winchPassiveSpeed + tuning.winchElectricSpeed;
            game.StepSimulation(.5f);
            Assert.That(game.diver.onboard);
            Assert.That(game.winchElectric, "boarding the diver must not stop the other targets");
            Assert.That(game.WinchTargetCount, Is.EqualTo(2));
            float before = game.GetCargoLifeline(game.world.pickups[0]).PathLength;
            game.StepSimulation(.5f);
            Assert.That(game.GetCargoLifeline(game.world.pickups[0]).PathLength, Is.EqualTo(before - speed * .5f / 2).Within(.005f));
        }
    }
}
