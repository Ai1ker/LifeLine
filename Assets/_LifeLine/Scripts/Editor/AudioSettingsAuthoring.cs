#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using static Lifeline.NauticalUiSkin;

namespace Lifeline.Editor
{
    internal static class AudioSettingsAuthoring
    {
        public static void Install(GameSettingsPanel settings)
        {
            if (settings.languageButton == null || settings.panel.GetComponentInChildren<AudioSettingsView>(true) != null) return;
            NauticalMenuAuthoring.ApplySettings(settings);
            var card = (RectTransform)settings.languageButton.transform.parent;
            Stretch(card, new Vector2(.28f, .101f), new Vector2(.72f, .899f));
            Place(settings.languageButton, .745f, .827f);
            Place(settings.cameraButton, .65f, .732f);
            Place(settings.bindingsButton, .555f, .637f);
            Stretch((RectTransform)settings.closeButton.transform, new Vector2(.62f,.061f), new Vector2(.892f,.149f));

            var volume = Rect(card, "Audio sliders", new Vector2(.105f,.164f), new Vector2(.895f,.521f));
            var view = volume.gameObject.AddComponent<AudioSettingsView>();
            view.master = Row(volume, "Master", "主音量", 0, out view.masterValue);
            view.effects = Row(volume, "Effects", "音效音量", 1, out view.effectsValue);
            view.music = Row(volume, "Music", "音乐音量", 2, out view.musicValue);
        }

        static void Place(Button button, float bottom, float top)
        {
            if (button == null) return;
            Stretch((RectTransform)button.transform, new Vector2(.105f,bottom), new Vector2(.895f,top));
        }

        static Slider Row(Transform parent, string name, string title, int index, out Text value)
        {
            float top = 1 - index / 3f;
            var row = Rect(parent, name, new Vector2(0,top - .30f), new Vector2(1,top));
            Surface(row.gameObject, "Plate");
            var heading = Label(row, "Label", 21, TextAnchor.MiddleLeft);
            heading.text = title;
            Stretch(heading.rectTransform, new Vector2(.06f,0), new Vector2(.35f,1));
            value = Label(row, "Value", 20, TextAnchor.MiddleRight);
            value.text = "100%";
            Stretch(value.rectTransform, new Vector2(.79f,0), new Vector2(.945f,1));

            var area = Rect(row, "Slider", new Vector2(.33f,.03f), new Vector2(.78f,.97f));
            var back = Image(area, "Track", "Plate");
            back.color = Track;
            back.raycastTarget = true;
            back.preserveAspect = false;
            Stretch(back.rectTransform, new Vector2(.02f,.33f), new Vector2(.98f,.66f));
            var fillArea = Rect(area, "Fill area", new Vector2(.04f,.38f), new Vector2(.96f,.62f));
            var fill = Image(fillArea, "Volume fill");
            fill.color = new Color32(197, 131, 55, 255);
            var handleArea = Rect(area, "Handle area", new Vector2(.045f,0), new Vector2(.955f,1));
            var handle = Image(handleArea, "Brass handle", "Frame");
            handle.color = new Color32(247, 213, 143, 255);
            handle.rectTransform.sizeDelta = new Vector2(21, -10);
            handle.raycastTarget = true;
            var slider = area.gameObject.AddComponent<Slider>();
            slider.targetGraphic = handle;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = 1;
            return slider;
        }
    }
}
#endif
