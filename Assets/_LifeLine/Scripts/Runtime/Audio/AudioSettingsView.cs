using UnityEngine;
using UnityEngine.UI;

namespace Lifeline
{
    public sealed class AudioSettingsView : MonoBehaviour
    {
        public Slider master, effects, music;
        public Text masterValue, effectsValue, musicValue;

        void OnEnable()
        {
            Refresh();
            master.onValueChanged.AddListener(SetMaster);
            effects.onValueChanged.AddListener(SetEffects);
            music.onValueChanged.AddListener(SetMusic);
        }

        void OnDisable()
        {
            master.onValueChanged.RemoveListener(SetMaster);
            effects.onValueChanged.RemoveListener(SetEffects);
            music.onValueChanged.RemoveListener(SetMusic);
            if (Application.isPlaying) AudioVolumePreferences.Save();
        }

        void SetMaster(float value) => Set(AudioBus.Master, value);
        void SetEffects(float value) => Set(AudioBus.Effects, value);
        void SetMusic(float value) => Set(AudioBus.Music, value);

        void Set(AudioBus bus, float value)
        {
            AudioVolumePreferences.Set(bus, value);
            Refresh();
            GameAudio.Local(AudioCue.UiSlider, .45f, .10f);
        }

        void Refresh()
        {
            Refresh(master, masterValue, AudioBus.Master);
            Refresh(effects, effectsValue, AudioBus.Effects);
            Refresh(music, musicValue, AudioBus.Music);
        }

        static void Refresh(Slider slider, Text label, AudioBus bus)
        {
            float value = AudioVolumePreferences.Get(bus);
            slider.SetValueWithoutNotify(value);
            label.text = Mathf.RoundToInt(value * 100) + "%";
        }
    }
}
