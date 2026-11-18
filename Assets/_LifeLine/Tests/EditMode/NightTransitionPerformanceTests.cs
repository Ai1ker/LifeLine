using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Lifeline.Tests
{
    public sealed class NightTransitionPerformanceTests
    {
        [Test]
        public void TwoUnderwaterMasksKeepTheirOrderAcrossNightRefreshesAndViewportChanges()
        {
            var previous = GameSession.Current;
            var root = new GameObject("Night transition fixture");
            var tuning = ScriptableObject.CreateInstance<GameTuning>();
            try
            {
                var game = Child(root, "Session").AddComponent<GameSession>(); game.enabled = false; game.autoStart = false;
                game.tuning = tuning; game.phase = SessionPhase.Playing; game.elapsed = tuning.daySeconds * .99f;
                game.captain = Child(root, "Captain").AddComponent<CrewActor>(); game.captain.enabled = false; game.captain.role = CrewRole.Captain; game.captain.onboard = false;
                game.diver = Child(root, "Diver").AddComponent<CrewActor>(); game.diver.enabled = false; game.diver.role = CrewRole.Diver; game.diver.onboard = false;
                var rig = Child(root, "Rig").AddComponent<GameCameraRig>(); rig.enabled = false;
                rig.captainCamera = Child(root, "Left camera").AddComponent<Camera>(); rig.captainCamera.rect = new Rect(0, 0, .5f, 1);
                rig.diverCamera = Child(root, "Right camera").AddComponent<Camera>(); rig.diverCamera.rect = new Rect(.5f, 0, .5f, 1);
                var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)); canvas.transform.SetParent(root.transform);
                var left = Mask(canvas, "Captain mask", game, rig, game.captain);
                var right = Mask(canvas, "Diver mask", game, rig, game.diver);
                int leftIndex = left.maskImage.transform.GetSiblingIndex(), rightIndex = right.maskImage.transform.GetSiblingIndex();
                var leftMaterial = left.RuntimeMaterial; var rightMaterial = right.RuntimeMaterial;
                for (int i = 0; i < 60; i++)
                {
                    left.RefreshNow();
                    Assert.That(left.maskImage.transform.GetSiblingIndex(), Is.EqualTo(leftIndex));
                    Assert.That(right.maskImage.transform.GetSiblingIndex(), Is.EqualTo(rightIndex));
                    right.RefreshNow();
                    Assert.That(left.maskImage.transform.GetSiblingIndex(), Is.EqualTo(leftIndex));
                    Assert.That(right.maskImage.transform.GetSiblingIndex(), Is.EqualTo(rightIndex));
                }
                Assert.That(left.MaskVisible && right.MaskVisible);
                Assert.That(left.RuntimeMaterial, Is.SameAs(leftMaterial)); Assert.That(right.RuntimeMaterial, Is.SameAs(rightMaterial));
                rig.captainCamera.rect = new Rect(0, 0, 1, 1); left.RefreshNow();
                Assert.That(left.maskImage.rectTransform.anchorMax.x, Is.EqualTo(1));
                game.elapsed = 0; left.RefreshNow(); right.RefreshNow();
                Assert.That(left.MaskVisible || right.MaskVisible, Is.False);
                Assert.That(left.maskImage.transform.GetSiblingIndex(), Is.EqualTo(leftIndex));
                Assert.That(right.maskImage.transform.GetSiblingIndex(), Is.EqualTo(rightIndex));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(tuning); GameSession.RestoreCurrentAfterValidation(previous); }
        }
        static GameObject Child(GameObject root, string name) { var obj = new GameObject(name); obj.transform.SetParent(root.transform); return obj; }
        static NightVisibilityView Mask(GameObject canvas, string name, GameSession game, GameCameraRig rig, CrewActor actor)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image)); obj.transform.SetParent(canvas.transform);
            var view = obj.AddComponent<NightVisibilityView>(); view.enabled = false;
            view.Initialize(game, rig, actor, obj.GetComponent<Image>(), null); return view;
        }
    }
}
