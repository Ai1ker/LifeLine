using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    public enum BgmContext
    {
        Menu = 0, ShallowsDay = 1, Night = 2,
        OnlineRoom = 7, Dusk = 8, CityDay = 9, DeepDay = 10
    }

    /// <summary>Crossfades music across scenes and schedules overlapping iterations of the same clip.</summary>
    public sealed class BgmManager : MonoBehaviour
    {
        [Serializable]
        public sealed class Track
        {
            public BgmContext context;
            public AudioClip clip;
            [Range(0, 1)] public float volume = .68f;
        }

        public static BgmManager Current { get; private set; }
        public AudioClip CurrentClip => active != null ? active.clip : null;
        public List<Track> tracks = new();
        public AudioSource first, second;
        [Min(0)] public float fadeSeconds = 1.25f;
        [Min(0)] public float loopCrossfadeSeconds = .35f;

        // The DSP clock keeps the next iteration independent of Update timing.
        const double SchedulingLookahead = .35;
        const double MinimumScheduleLead = .05;
        AudioSource active, retiring, scheduledLoop;
        Track requestedTrack, activeTrack;
        double activeEndsAt, transitionStartsAt, fadeInEndsAt, fadeOutEndsAt, loopStartsAt;
        float activeGain, retiringGain;

        void Awake()
        {
            if (Current != null && Current != this)
            {
                Destroy(gameObject);
                return;
            }
            Current = this;
            transform.SetParent(null, false);
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public void Play(BgmContext context, int? selectionSeed = null)
        {
            if (!selectionSeed.HasValue)
            {
                requestedTrack = tracks.Find(track => Matches(track, context));
                return;
            }
            int count = 0;
            foreach (var track in tracks)
                if (Matches(track, context)) count++;
            if (count == 0) { Stop(); return; }
            int index = new System.Random(selectionSeed.Value).Next(count);
            foreach (var track in tracks)
            {
                if (!Matches(track, context)) continue;
                if (index-- == 0) { requestedTrack = track; return; }
            }
        }

        static bool Matches(Track track, BgmContext context) => track != null && track.context == context && track.clip != null;
        public void Stop() => requestedTrack = null;

        void Update()
        {
            if (Current != this || first == null || second == null) return;
            double now = AudioSettings.dspTime;
            if (scheduledLoop != null && now >= loopStartsAt)
            {
                var next = scheduledLoop;
                scheduledLoop = null;
                BeginFade(activeTrack, next, loopStartsAt, LoopOverlap(next.clip));
            }

            ApplyGains(now);
            // Finish the current fade before applying the newest request, keeping both audible voices smooth.
            if (retiring != null) return;
            if (requestedTrack != activeTrack)
            {
                if (scheduledLoop != null) { scheduledLoop.Stop(); scheduledLoop = null; }
                double start = now + MinimumScheduleLead;
                var next = requestedTrack == null ? null : Prepare(OtherSource(), requestedTrack.clip, start);
                float fade = next == null ? fadeSeconds : Mathf.Min(fadeSeconds, next.clip.length * .5f);
                BeginFade(requestedTrack, next, start, fade);
                return;
            }
            if (active == null || scheduledLoop != null) return;
            double startOfRepeat = activeEndsAt - LoopOverlap(active.clip);
            if (now + SchedulingLookahead < startOfRepeat) return;
            loopStartsAt = Math.Max(startOfRepeat, now + MinimumScheduleLead);
            scheduledLoop = Prepare(OtherSource(), active.clip, loopStartsAt);
        }

        AudioSource OtherSource() => active == first ? second : first;
        float LoopOverlap(AudioClip clip) => Mathf.Clamp(loopCrossfadeSeconds, 0, clip.length * .25f);

        static AudioSource Prepare(AudioSource source, AudioClip clip, double start)
        {
            source.Stop();
            source.clip = clip;
            source.loop = false;
            source.volume = 0;
            source.PlayScheduled(start);
            return source;
        }

        void BeginFade(Track track, AudioSource next, double start, float duration)
        {
            retiring = active;
            retiringGain = activeGain;
            fadeOutEndsAt = Math.Min(activeEndsAt, start + duration);
            transitionStartsAt = start;
            fadeInEndsAt = start + duration;
            active = next;
            activeTrack = track;
            activeGain = 0;
            if (next != null) activeEndsAt = start + next.clip.length;
        }

        void ApplyGains(double now)
        {
            float bus = AudioVolumePreferences.Gain(AudioBus.Music);
            if (active != null)
            {
                activeGain = activeTrack.volume * Fade(now, transitionStartsAt, fadeInEndsAt);
                active.volume = bus * activeGain;
            }
            if (retiring == null) return;
            retiring.volume = bus * retiringGain * (1 - Fade(now, transitionStartsAt, fadeOutEndsAt));
            if (now < fadeOutEndsAt) return;
            retiring.Stop();
            retiring = null;
        }

        static float Fade(double now, double start, double end)
        {
            if (now < start) return 0;
            if (end <= start) return 1;
            return Mathf.SmoothStep(0, 1, (float)((now - start) / (end - start)));
        }
    }
}
