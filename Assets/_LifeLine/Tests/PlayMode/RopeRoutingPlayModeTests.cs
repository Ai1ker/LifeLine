using NUnit.Framework;
using UnityEngine;

namespace Lifeline.Tests
{
    public sealed class RopeRoutingPlayModeTests
    {
        GameObject root;
        CrewActor diver;
        DiverRope rope;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Routed lifeline physical fixture");
            var actorObject = new GameObject("Diver"); actorObject.transform.SetParent(root.transform); actorObject.layer = 9;
            var capsule = actorObject.AddComponent<CapsuleCollider>(); capsule.radius = .32f; capsule.height = 1.6f;
            diver = actorObject.AddComponent<CrewActor>(); diver.enabled = false; diver.onboard = false;
            rope = root.AddComponent<DiverRope>(); rope.Configure(diver); rope.frameBudgetMilliseconds = 1000;
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown() { if (root != null) Object.DestroyImmediate(root); }

        [Test]
        public void HangingShelfWrapsAndPullsAroundCornersWithoutEnteringTheWall()
        {
            Wall("Shelf", new Vector3(0, -1, 0), new Vector3(4, .4f, 2));
            Place(new Vector3(0, -4, 0)); Vector3 anchor = new Vector3(0, 2, 0);
            Assert.That(rope.UpdateRoute(anchor, diver.Position), Is.True);
            Assert.That(rope.ContactCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(rope.PathLength, Is.GreaterThan(Vector3.Distance(anchor, diver.Position) + 1));
            AssertSafeSegments();
            PullToAnchor(anchor);
            Assert.That(rope.ContactCount, Is.Zero);
        }

        [Test]
        public void UShapedRuinNeedsSeveralPulleysAndCanBeCompletelyReeledIn()
        {
            BuildU(); Place(new Vector3(0, -5, 0)); Vector3 anchor = new Vector3(0, 1, 0);
            Assert.That(rope.UpdateRoute(anchor, diver.Position), Is.True, "The route must exit the U before going around its exterior.");
            Assert.That(rope.ContactCount, Is.GreaterThanOrEqualTo(3)); AssertSafeSegments();
            PullToAnchor(anchor);
        }

        [Test]
        public void ContactsPersistAcrossRestoreThenUnwrapWhenDirectVisibilityReturns()
        {
            Wall("Shelf", new Vector3(0, -1, 0), new Vector3(4, .4f, 2));
            Vector3 anchor = new Vector3(0, 2, 0); Place(new Vector3(0, -4, 0));
            Assert.That(rope.UpdateRoute(anchor, diver.Position), Is.True);
            var contacts = rope.CaptureContacts(); float length = rope.PathLength;
            rope.ResetRoute(); rope.RestoreContacts(anchor, diver.Position, contacts);
            Assert.That(rope.PathLength, Is.EqualTo(length).Within(.001f));
            Assert.That(rope.CaptureContacts(), Is.EqualTo(contacts));
            Place(new Vector3(4, 2, 0)); Assert.That(rope.UpdateRoute(anchor, diver.Position), Is.True);
            Assert.That(rope.ContactCount, Is.Zero); Assert.That(rope.PathLength, Is.EqualTo(4).Within(.001f));
        }

        [Test]
        public void MovingAnchorAndLeashConsumeTheEntireBentPathRatherThanStraightDistance()
        {
            Wall("Shelf", new Vector3(0, -1, 0), new Vector3(4, .4f, 2));
            Vector3 anchor = new Vector3(0, 2, 0); Place(new Vector3(0, -4, 0));
            Assert.That(rope.UpdateRoute(anchor, diver.Position), Is.True);
            float length = rope.PathLength;
            Vector3 requested = new Vector3(18, 2, 0);
            Vector3 clamped = rope.ClampAnchorMove(requested, length + .5f);
            Assert.That(clamped.x, Is.LessThan(requested.x));
            Assert.That(rope.PathLength, Is.LessThanOrEqualTo(length + .502f));
            rope.ApplyLengthLimit(diver, rope.PathLength - 1);
            Assert.That(rope.IsBlocked, Is.False); AssertSafeSegments(); AssertNoPenetration();
        }

        [Test]
        public void ClosedTrapdoorStopsRecoveryAndOpeningItRestoresThePhysicalRoute()
        {
            Wall("Left", new Vector3(-2, -1, 0), new Vector3(.4f, 4.4f, 2));
            Wall("Right", new Vector3(2, -1, 0), new Vector3(.4f, 4.4f, 2));
            Wall("Floor", new Vector3(0, -3, 0), new Vector3(4.4f, .4f, 2));
            var barrier = Wall("Hatch", new Vector3(0, 1, 0), new Vector3(4.4f, .2f, 2));
            var hatch = barrier.gameObject.AddComponent<TrapdoorGate>(); hatch.barrier = barrier;
            hatch.SetOpen(true); Physics.SyncTransforms();
            Vector3 anchor = new Vector3(0, 4, 0); Place(new Vector3(0, -1, 0));
            Assert.That(rope.UpdateRoute(anchor, diver.Position), Is.True);
            hatch.SetOpen(false); Physics.SyncTransforms();
            Assert.That(rope.UpdateRoute(anchor, diver.Position), Is.False);
            Vector3 before = diver.Position; Assert.That(rope.Pull(diver, 20), Is.Zero); Assert.That(diver.Position, Is.EqualTo(before));
            hatch.SetOpen(true); Physics.SyncTransforms();
            Assert.That(rope.UpdateRoute(anchor, diver.Position), Is.True); PullToAnchor(anchor);
        }

        [Test]
        public void OnlyAuthorizedWinchCanBreakAClosedHatchPinchingTheRope()
        {
            Wall("Left", new Vector3(-2, -1, 0), new Vector3(.4f, 4.4f, 2));
            Wall("Right", new Vector3(2, -1, 0), new Vector3(.4f, 4.4f, 2));
            Wall("Floor", new Vector3(0, -3, 0), new Vector3(4.4f, .4f, 2));
            var barrier = Wall("Hatch", new Vector3(0, 1, 0), new Vector3(4.4f, .2f, 2));
            var hatch = barrier.gameObject.AddComponent<TrapdoorGate>(); hatch.barrier = barrier;
            hatch.SetOpen(false); Physics.SyncTransforms();
            Vector3 anchor = new Vector3(0, 4, 0); Place(new Vector3(0, -1, 0));
            Assert.That(rope.UpdateRoute(anchor, diver.Position), Is.False);
            Assert.That(rope.Pull(diver, 20), Is.Zero); Assert.That(hatch.destroyed, Is.False);
            Assert.That(rope.TryBreakBlockingGate(), Is.True);
            Assert.That(hatch.destroyed, Is.True); Assert.That(barrier.enabled, Is.False);
            Assert.That(rope.IsBlocked, Is.False); Assert.That(rope.TryBreakBlockingGate(), Is.False);
            PullToAnchor(anchor);
        }

        [Test]
        public void FailurePaysOutOnlyUntilCollisionAndCargoUsesTheSameBentRoute()
        {
            Wall("Seabed", new Vector3(0, -3, 0), new Vector3(12, .2f, 2));
            Place(new Vector3(0, -2, 0)); Vector3 anchor = new Vector3(0, 2, 0);
            Assert.That(rope.UpdateRoute(anchor, diver.Position), Is.True);
            rope.Pull(diver, -5); Assert.That(diver.Position.y, Is.GreaterThanOrEqualTo(-2.101f)); AssertNoPenetration();
            Object.DestroyImmediate(root.transform.Find("Seabed").gameObject);
            Wall("Shelf", new Vector3(0, -1, 0), new Vector3(4, .4f, 2)); Physics.SyncTransforms();
            Vector3 cargo = new Vector3(0, -4, 0); rope.ResetRoute();
            Assert.That(rope.UpdateRoute(anchor, cargo), Is.True);
            cargo = rope.PullPoint(cargo, 100);
            Assert.That(Vector3.Distance(cargo, anchor), Is.LessThan(.03f));
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator BudgetedSearchResumesAndRepeatedCallsInOneFrameAreCached()
        {
            rope.frameBudgetMilliseconds=.01f;
            BuildU(); Place(new Vector3(0,-5,0)); Vector3 anchor=new Vector3(0,1,0);
            bool found=rope.UpdateRoute(anchor,diver.Position);
            int queries=rope.totalQueries;
            for(int i=0;i<20;i++)rope.UpdateRoute(anchor,diver.Position);
            Assert.That(rope.totalQueries,Is.EqualTo(queries),"render, leash and winch must share the same frame route");
            for(int frame=0;!found&&frame<200;frame++)
            {
                yield return null;
                found=rope.UpdateRoute(anchor,diver.Position);
            }
            Assert.That(found,Is.True,"bounded work must eventually produce the same collision-free route");
            Assert.That(rope.ContactCount,Is.GreaterThanOrEqualTo(3));AssertSafeSegments();
            rope.frameBudgetMilliseconds=1000;PullToAnchor(anchor);
        }

        void BuildU()
        {
            Wall("Left arm", new Vector3(-2, 0, 0), new Vector3(.4f, 6, 2));
            Wall("Right arm", new Vector3(2, 0, 0), new Vector3(.4f, 6, 2));
            Wall("Base", new Vector3(0, -3, 0), new Vector3(4.4f, .4f, 2));
        }
        BoxCollider Wall(string name, Vector3 position, Vector3 size)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform); go.transform.position = position; go.layer = 8;
            var box = go.AddComponent<BoxCollider>(); box.size = size; Physics.SyncTransforms(); return box;
        }
        void Place(Vector3 point)
        {
            diver.body.position = point; diver.transform.position = point; diver.body.linearVelocity = Vector3.zero; Physics.SyncTransforms();
        }
        void PullToAnchor(Vector3 anchor)
        {
            for (int i = 0; i < 400 && Vector3.Distance(diver.Position, anchor) > .03f; i++)
            {
                float progress = rope.Pull(diver, .2f);
                Assert.That(progress, Is.GreaterThan(.00001f), "A traversable wrapped route must continue making reel-in progress.");
                Physics.SyncTransforms(); AssertNoPenetration(); AssertSafeSegments();
            }
            Assert.That(Vector3.Distance(diver.Position, anchor), Is.LessThan(.03f));
        }
        void AssertSafeSegments()
        {
            for (int i = 1; i < rope.Points.Count; i++)
                Assert.That(rope.IsSegmentClear(rope.Points[i - 1], rope.Points[i]), Is.True, "Every visual and tension segment must clear the actual capsule.");
        }
        void AssertNoPenetration()
        {
            var capsule = diver.GetComponent<CapsuleCollider>();
            foreach (var collider in root.GetComponentsInChildren<BoxCollider>())
            {
                if (!collider.enabled) continue;
                Vector3 direction; float depth;
                bool overlaps = Physics.ComputePenetration(capsule, diver.Position, diver.transform.rotation,
                    collider, collider.transform.position, collider.transform.rotation, out direction, out depth);
                Assert.That(overlaps && depth > .001f, Is.False, "Reeling must not cross " + collider.name);
            }
        }
    }
}
