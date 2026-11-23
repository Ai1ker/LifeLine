using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    public enum BgmContext { Menu, Day, Night, Storm, Underwater, Cave, Rescue }

    public sealed class BgmManager : MonoBehaviour
    {
        [Serializable]
        public sealed class Track
        {
            public BgmContext context;
            public AudioClip clip;
            [Range(0, 1)] public float volume = .68f;
        }

        public List<Track> tracks = new();
        public AudioSource first, second;
        [Min(0)] public float fadeSeconds = 1.25f;

        AudioSource active, retiring;
        float activeGain, targetGain, retiringStart, fadeProgress;

        public void Play(BgmContext context)
        {
            var track = tracks.Find(item => item != null && item.context == context);
            var clip = track?.clip;
            if (active != null && clip != null && active.clip == clip)
            {
                targetGain = track.volume;
                return;
            }
            if (clip == null && active == null) return;
            if (retiring != null) retiring.Stop();
            retiring = active;
            retiringStart = activeGain;
            active = clip == null ? null : retiring == first ? second : first;
            fadeProgress = activeGain = 0;
            targetGain = clip == null ? 0 : track.volume;
            if (active == null) return;
            active.clip = clip;
            active.loop = true;
            active.volume = 0;
            active.Play();
        }

        void Update()
        {
            fadeProgress = fadeSeconds <= 0 ? 1 : Mathf.MoveTowards(fadeProgress, 1, Time.unscaledDeltaTime / fadeSeconds);
            float volume = AudioVolumePreferences.Gain(AudioBus.Music);
            if (active != null)
            {
                activeGain = targetGain * fadeProgress;
                active.volume = volume * activeGain;
            }
            if (retiring == null) return;
            retiring.volume = volume * retiringStart * (1 - fadeProgress);
            if (fadeProgress < 1) return;
            retiring.Stop();
            retiring = null;
        }

        public void Stop() => PlaySilence();

        void PlaySilence()
        {
            if (active == null) return;
            if (retiring != null) retiring.Stop();
            retiring = active;
            retiringStart = activeGain;
            active = null;
            fadeProgress = 0;
        }
    }
}
