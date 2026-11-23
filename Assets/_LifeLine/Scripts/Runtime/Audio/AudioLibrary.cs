using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    [CreateAssetMenu(menuName = "LifeLine/Audio/Sound Library")]
    public sealed class AudioLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Sound
        {
            public AudioCue cue;
            public AudioClip clip;
            [Range(0, 1)] public float volume = 1;
        }

        public List<Sound> sounds = new();
        readonly Dictionary<AudioCue, Sound> lookup = new();

        void OnEnable() => Refresh();
        void OnValidate() => Refresh();

        public void Refresh()
        {
            lookup.Clear();
            foreach (var sound in sounds)
                if (sound != null && sound.cue != AudioCue.None) lookup[sound.cue] = sound;
        }

        public Sound Find(AudioCue cue) => lookup.TryGetValue(cue, out var sound) ? sound : null;
    }
}
