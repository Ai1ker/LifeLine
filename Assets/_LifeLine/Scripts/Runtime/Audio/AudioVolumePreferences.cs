using System;
using UnityEngine;

namespace Lifeline
{
    public enum AudioBus { Master, Effects, Music }

    public static class AudioVolumePreferences
    {
        public static event Action Changed;
        const string Prefix = "lifeline.audio.";
        static readonly float[] volumes = { 1, 1, 1 };
        static bool loaded;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            foreach (AudioBus bus in Enum.GetValues(typeof(AudioBus)))
                volumes[(int)bus] = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + bus, 1));
        }

        public static float Get(AudioBus bus)
        {
            Load();
            return volumes[(int)bus];
        }

        public static void Set(AudioBus bus, float value)
        {
            Load();
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(volumes[(int)bus], value)) return;
            volumes[(int)bus] = value;
            PlayerPrefs.SetFloat(Prefix + bus, value);
            Changed?.Invoke();
        }

        public static void Save() => PlayerPrefs.Save();
        public static float Gain(AudioBus bus) => Get(AudioBus.Master) * Get(bus);
    }
}
