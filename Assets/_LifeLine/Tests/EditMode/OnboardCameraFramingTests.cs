using NUnit.Framework;
using UnityEngine;

namespace Lifeline.Tests
{
    public sealed class OnboardCameraFramingTests
    {
        GameObject root;GameCameraRig rig;
        [SetUp] public void SetUp()
        {
            root=new GameObject("Onboard framing fixture");root.SetActive(false);
            rig=root.AddComponent<GameCameraRig>();
        }
        [TearDown] public void TearDown(){Object.DestroyImmediate(root);}

        [Test] public void CrewWalkingTogetherToEitherEndDoesNotZoomOut()
        {
            float centred=rig.GetSharedFraming(new Vector3(-1,.9f),new Vector3(1,.9f),Vector3.zero,16f/9,out Vector3 focus);
            foreach(float side in new[]{-1f,1f})
            {
                float atEnd=rig.GetSharedFraming(new Vector3(side*10,.9f),new Vector3(side*11,.9f),Vector3.zero,16f/9,out focus);
                Assert.That(atEnd,Is.EqualTo(centred).Within(.001f));Assert.That(Mathf.Abs(focus.x),Is.LessThan(.001f));
            }
            Assert.That(centred,Is.InRange(7.2f,7.4f));
        }

        [TestCase(16f/9)] [TestCase(4f/3)] [TestCase(21f/9)]
        public void JumpingCrewAndUsableCabinFitAndMoveWithShip(float aspect)
        {
            Vector3 captain=new Vector3(-11.6f,2.2f),diver=new Vector3(11.6f,-3.1f);
            float size=rig.GetSharedFraming(captain,diver,Vector3.zero,aspect,out Vector3 focus);
            Assert.That(size,Is.LessThanOrEqualTo(rig.sharedMaximumSize));
            foreach(var point in new[]{captain-(Vector3)rig.sharedCrewHalfExtents,captain+(Vector3)rig.sharedCrewHalfExtents,
                diver-(Vector3)rig.sharedCrewHalfExtents,diver+(Vector3)rig.sharedCrewHalfExtents,
                new Vector3(-rig.shipHalfWidth,rig.shipVerticalRange.x),new Vector3(rig.shipHalfWidth,rig.shipVerticalRange.y)})
            {
                Assert.That(Mathf.Abs(point.x-focus.x),Is.LessThanOrEqualTo(size*aspect));
                Assert.That(Mathf.Abs(point.y-focus.y),Is.LessThanOrEqualTo(size));
            }
            var offset=new Vector3(1075,-400,0);
            float translated=rig.GetSharedFraming(captain+offset,diver+offset,offset,aspect,out Vector3 shifted);
            Assert.That(translated,Is.EqualTo(size).Within(.001f));Assert.That(Vector3.Distance(shifted,focus+offset),Is.LessThan(.001f));
        }

        [Test] public void SharedViewHasHardZoomLimitAndMigrationPreservesCustomSettings()
        {
            Assert.That(rig.GetSharedFraming(new Vector3(-1000,0),new Vector3(1000,0),Vector3.zero,16f/9,out _),Is.EqualTo(rig.sharedMaximumSize));
            rig.sharedMinimumSize=7.5f;rig.sharedMaximumSize=24;rig.sharedPadding=new Vector2(1.8f,1.3f);
            rig.shipHalfWidth=16;rig.captainSize=5.8f;rig.diverSize=8;
            Lifeline.Editor.OnboardCameraMigration.UpdateRig(rig);
            Assert.That(rig.sharedMinimumSize,Is.EqualTo(5.4f));Assert.That(rig.sharedMaximumSize,Is.EqualTo(12));
            Assert.That(rig.sharedPadding,Is.EqualTo(new Vector2(.8f,.65f)));Assert.That(rig.shipHalfWidth,Is.EqualTo(16));
            Assert.That(rig.captainSize,Is.EqualTo(5.8f));Assert.That(rig.diverSize,Is.EqualTo(8));
        }
    }
}
