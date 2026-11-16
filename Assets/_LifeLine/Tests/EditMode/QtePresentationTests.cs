using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lifeline.Tests
{
    public sealed class QtePresentationTests
    {
        GameObject root;
        QteStationView view;
        QteVisualTheme theme;
        CrewInputReader input;
        Texture2D texture;
        Sprite sprite;
        [SetUp] public void Setup()
        {
            root = new GameObject("QTE view fixture", typeof(RectTransform));
            view = root.AddComponent<QteStationView>(); theme = ScriptableObject.CreateInstance<QteVisualTheme>(); view.theme = theme;
            texture = new Texture2D(8,8); sprite = Sprite.Create(texture,new Rect(0,0,8,8),Vector2.one*.5f);
            theme.keycap = theme.keycapSuccess = theme.keycapFailure = theme.ring = theme.needle = sprite;
            theme.lane = theme.hitLine = theme.ringFace = theme.hub = sprite;
            theme.upArrow = theme.leftArrow = theme.downArrow = theme.rightArrow = sprite;
            input = root.AddComponent<CrewInputReader>(); input.preferencesPrefix = "QtePresentationTests.unused.";
            input.Initialize(CrewRole.Captain, null);
            view.laneRoot = Child("Lane"); view.circleRoot = Child("Circle");
            view.laneBackground = Image(); view.laneWindow = Image(); view.laneHitLine = Image();
            view.laneKeycap = Image(); view.laneGlyph = Image(); view.laneKeyLabel = Text();
            view.ringTrack = Image(); view.ringWindow = Image(); view.ringFace = Image(); view.ringNeedle = Image(); view.ringHub = Image();
            view.centerKeycap = Image(); view.centerGlyph = Image(); view.centerKeyLabel = Text(); view.centerValue = Text();
            view.needlePivot = (RectTransform)Child("Pivot").transform;
        }
        GameObject Child(string name) { var obj = new GameObject(name,typeof(RectTransform)); obj.transform.SetParent(root.transform); return obj; }
        Image Image() => Child("Image").AddComponent<Image>();
        Text Text() => Child("Text").AddComponent<Text>();
        [TearDown] public void Cleanup() { Object.DestroyImmediate(root); Object.DestroyImmediate(theme); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }

        [Test] public void NoteArrivesAtGoldLineAtTheRealCueAndWindowUsesTheSameSeconds()
        {
            view.ShowLane(input,0,1,1,1,.16f,0,false);
            Assert.That(view.phase, Is.EqualTo(.75f)); Assert.That(view.laneKeyLabel.text, Is.EqualTo("W"));
            Assert.That(view.laneRoot.activeSelf && !view.circleRoot.activeSelf);
            view.ShowLane(input,0,.84f,1,1,.16f,0,false);
            Assert.That(view.phase, Is.EqualTo(view.laneWindow.rectTransform.anchorMin.x).Within(.0001f));
            view.ShowLane(input,0,1.16f,1,1,.16f,0,false);
            Assert.That(view.phase, Is.EqualTo(view.laneWindow.rectTransform.anchorMax.x).Within(.0001f));
        }

        [Test] public void ReboundDirectionImmediatelyShowsTheEffectiveKey()
        {
            input.RuntimeActions.FindAction("Captain/Move").ApplyBindingOverride(1,"<Keyboard>/i");
            view.ShowLane(input,0,1,1,1,.16f,0,false);
            Assert.That(view.displayedBindingPath, Is.EqualTo("<Keyboard>/i"));
            Assert.That(view.laneKeyLabel.text, Is.EqualTo("I")); Assert.That(view.laneGlyph.enabled, Is.False);
        }

        [Test] public void DiverUsesArrowArtworkAndOwnZeroActionBinding()
        {
            input.Initialize(CrewRole.Diver,null); view.ShowLane(input,0,1,1,1,.16f,0,false);
            Assert.That(view.laneGlyph.enabled); Assert.That(view.laneGlyph.sprite, Is.SameAs(theme.upArrow));
            Assert.That(view.laneKeyLabel.enabled, Is.False);
            view.ShowCircle(input,.8f,.8f,.2f,.2f,0,false);
            Assert.That(view.displayedBindingPath, Is.EqualTo("<Keyboard>/0"));
        }

        [Test] public void RadialWindowAndNeedleShareTopOriginClockwiseTiming()
        {
            view.ShowCircle(input,.69f,.69f,.11f,.4f,0,false);
            Assert.That(view.ringWindow.fillAmount, Is.EqualTo(.11f));
            Assert.That(view.ringWindow.fillOrigin, Is.EqualTo((int)UnityEngine.UI.Image.Origin360.Top));
            Assert.That(view.ringWindow.fillClockwise);
            Assert.That(Mathf.DeltaAngle(view.needlePivot.eulerAngles.z,-.69f*360), Is.EqualTo(0).Within(.001f));
            Assert.That(Mathf.DeltaAngle(view.ringWindow.transform.eulerAngles.z,-(.69f-.055f)*360), Is.EqualTo(0).Within(.001f));
        }

        [Test] public void CustomBindingArtworkCanContainItsOwnLabelAndAutomaticWinchHidesInput()
        {
            theme.bindingArtwork.Add(new QteVisualTheme.BindingArtwork {controlPath="<Keyboard>/space",sprite=sprite,showLabel=false,size=new Vector2(120,40)});
            view.ShowCircle(input,.8f,.8f,.2f,.4f,0,false);
            Assert.That(view.centerKeycap.sprite, Is.SameAs(sprite)); Assert.That(view.centerKeyLabel.enabled, Is.False);
            Assert.That(view.centerKeycap.rectTransform.sizeDelta, Is.EqualTo(new Vector2(120,40)));
            theme.bindingArtwork[0].sprite = null;
            view.ShowCircle(input,.8f,.8f,.2f,.4f,0,false);
            Assert.That(view.centerKeyLabel.enabled, Is.True, "Missing artwork must fall back to the real key label.");
            view.ShowCircle(input,.8f,.8f,.2f,.4f,0,false,true);
            Assert.That(view.centerKeycap.gameObject.activeSelf, Is.False); Assert.That(view.ringNeedle.enabled, Is.False);
        }

        [Test] public void CircleFeedbackUsesSuccessFailureAndReturnsToGold()
        {
            view.ShowCircle(input,.8f,.8f,.2f,.4f,.4f,true); Assert.That(view.ringNeedle.color, Is.EqualTo(theme.success));
            Assert.That(view.centerKeyLabel.color, Is.EqualTo(theme.successKeyText));
            view.ShowCircle(input,.8f,.8f,.2f,.4f,.4f,false); Assert.That(view.ringNeedle.color, Is.EqualTo(theme.failure));
            view.ShowCircle(input,.8f,.8f,.2f,.4f,0,false); Assert.That(view.ringNeedle.color, Is.EqualTo(theme.gold));
        }
    }
}
