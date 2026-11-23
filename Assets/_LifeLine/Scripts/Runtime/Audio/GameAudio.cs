using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    [Serializable]
    public struct OnlineAudioCue
    {
        public int sequence;
        public AudioCue cue;
        public Vector2 point;
        public int space, audience;
    }

    /// <summary>Scene-level AudioSources for effects, ambience and UI. Music has its own sources.</summary>
    public sealed partial class GameAudio : MonoBehaviour
    {
        public static GameAudio Active { get; private set; }
        public AudioLibrary library;
        public GameSession session;
        public BgmManager bgm;
        public AudioSource[] effects, loops;
        [Min(1)] public float audibleDistance = 16;
        [Range(0, 1)] public float ambienceLevel = .65f;
        [Range(0, 1)] public float effectLevel = .88f;
        [Min(.05f)] public float loopFadeSeconds = .24f;

        const int Surface = 1, Water = 2;
        const int CueHistoryLimit = 24;
        struct TimedCue { public OnlineAudioCue value; public float time; }
        sealed class LoopVoice
        {
            public AudioCue cue;
            public int owner;
            public AudioSource source;
            public float target, gain, until;
        }
        readonly List<TimedCue> history = new(CueHistoryLimit);
        readonly Dictionary<AudioCue, float> lastLocal = new();
        readonly System.Random variation = new();
        LoopVoice[] loopVoices;
        float[] effectGains;
        int effectIndex, nextSequence, receivedSequence;

        void Awake()
        {
            Active = this;
            effectGains = new float[effects.Length];
            loopVoices = Array.ConvertAll(loops, source => new LoopVoice { source = source });
        }

        void OnEnable() => AudioVolumePreferences.Changed += ApplyVolume;
        void OnDisable() => AudioVolumePreferences.Changed -= ApplyVolume;
        void OnDestroy()
        {
            UnsubscribeVoyage();
            if (Active == this) Active = null;
        }

        public static void Local(AudioCue cue, float gain = 1, float minimumInterval = 0)
        {
            var audio = Active;
            if (audio == null || !audio.AllowCue(cue, minimumInterval)) return;
            audio.PlayEffect(cue, gain, 0);
        }

        public static void ForLocalPanel(CrewActor actor, AudioCue cue, float gain = 1, float minimumInterval = 0)
        {
            if (actor != null && (!OnlineVoyage.InProgress || OnlineVoyage.Current.LocalRole == actor.role))
                Local(cue, gain, minimumInterval);
        }

        public static void Global(GameSession game, AudioCue cue, CrewActor audience = null, float minimumInterval = 0) =>
            Emit(game, cue, default, 0, audience != null ? (int)audience.role + 1 : 0, minimumInterval);

        public static void At(GameSession game, AudioCue cue, Vector3 point, bool onShip) =>
            Emit(game, cue, onShip && game != null ? game.ShipToWaterPoint(point) : point, onShip ? Surface : Water, 0);

        public static void At(CrewActor actor, AudioCue cue)
        {
            if (actor != null) At(actor.session, cue, actor.Position, actor.onboard);
        }

        bool AllowCue(AudioCue cue, float minimumInterval)
        {
            if (minimumInterval <= 0) return true;
            float now = Time.unscaledTime;
            if (lastLocal.TryGetValue(cue, out float last) && now - last < minimumInterval) return false;
            lastLocal[cue] = now;
            return true;
        }

        static void Emit(GameSession game, AudioCue cue, Vector2 point, int space, int audience, float minimumInterval = 0)
        {
            var audio = Active;
            if (audio == null || game == null || audio.session != game || !game.IsInitialized || OnlineVoyage.IsGuest) return;
            if (!audio.AllowCue(cue, minimumInterval)) return;
            var value = new OnlineAudioCue { cue = cue, point = point, space = space, audience = audience };
            audio.PlayNotice(value);
            if (!OnlineVoyage.InProgress) return;
            value.sequence = ++audio.nextSequence;
            if (audio.history.Count == CueHistoryLimit) audio.history.RemoveAt(0);
            audio.history.Add(new TimedCue { value = value, time = Time.unscaledTime });
        }

        public static OnlineAudioCue[] CaptureRecent(GameSession game, bool baseline)
        {
            var audio = Active;
            if (audio == null || audio.session != game) return null;
            if (baseline) { audio.history.Clear(); return null; }
            int expired = 0;
            foreach (var entry in audio.history)
            {
                if (Time.unscaledTime - entry.time <= .46f) break;
                expired++;
            }
            if (expired > 0) audio.history.RemoveRange(0, expired);
            if (audio.history.Count == 0) return null;
            var cues = new OnlineAudioCue[audio.history.Count];
            for (int i = 0; i < cues.Length; i++) cues[i] = audio.history[i].value;
            return cues;
        }

        public static void PlayRecent(GameSession game, OnlineAudioCue[] cues)
        {
            var audio = Active;
            if (audio == null || audio.session != game || cues == null) return;
            foreach (var cue in cues)
            {
                if (cue.sequence <= audio.receivedSequence) continue;
                audio.receivedSequence = cue.sequence;
                audio.PlayNotice(cue);
            }
        }

        void PlayNotice(OnlineAudioCue value)
        {
            if (value.audience > 0 && OnlineVoyage.InProgress && (int)OnlineVoyage.Current.LocalRole + 1 != value.audience) return;
            float gain = 1, pan = 0;
            if (value.space != 0) gain = Audible(value.point, value.space == Surface, out pan);
            PlayEffect(value.cue, gain, pan);
        }

        void PlayEffect(AudioCue cue, float gain, float pan)
        {
            var sound = library != null ? library.Find(cue) : null;
            if (sound?.clip == null || gain <= .005f || effects.Length == 0) return;
            int index = effectIndex++ % effects.Length;
            var source = effects[index];
            source.clip = sound.clip;
            source.panStereo = pan;
            source.pitch = 1;
            effectGains[index] = gain * sound.volume;
            source.volume = EffectGain * effectGains[index];
            source.Play();
        }

        float EffectGain => effectLevel * AudioVolumePreferences.Gain(AudioBus.Effects);

        void ApplyVolume()
        {
            float gain = EffectGain;
            for (int i = 0; i < effects.Length; i++) effects[i].volume = gain * effectGains[i];
            foreach (var voice in loopVoices) voice.source.volume = gain * voice.gain;
        }

        float Audible(Vector2 point, bool onShip, out float pan)
        {
            pan = 0;
            if (session == null) return 1;
            var local = OnlineVoyage.InProgress ? OnlineVoyage.Current.LocalRole : (CrewRole?)null;
            float captainGain = Hear(session.captain, local, point, onShip, out float captainPan);
            float diverGain = Hear(session.diver, local, point, onShip, out float diverPan);
            pan = captainGain >= diverGain ? captainPan : diverPan;
            return Mathf.Max(captainGain, diverGain);
        }

        float Hear(CrewActor actor, CrewRole? local, Vector2 point, bool onShip, out float pan)
        {
            pan = 0;
            if (actor == null || (local.HasValue && actor.role != local.Value)) return 0;
            Vector2 origin = actor.onboard ? session.ShipToWaterPoint(actor.Position) : actor.Position;
            Vector2 offset = point - origin;
            pan = Mathf.Clamp(offset.x / audibleDistance, -.66f, .66f);
            float gain = Mathf.Clamp01(1 - offset.magnitude / audibleDistance);
            return actor.onboard == onShip ? gain : gain * .18f;
        }

        void KeepAt(AudioCue cue, int owner, Vector3 point, bool onShip, float gain = 1, float pitch = 1)
        {
            float audible = Audible(onShip ? session.ShipToWaterPoint(point) : point, onShip, out float pan);
            Keep(cue, owner, gain * audible, pan, pitch);
        }

        void Keep(AudioCue cue, int owner, float gain, float pan = 0, float pitch = 1, float hold = .12f)
        {
            var sound = library != null ? library.Find(cue) : null;
            if (sound?.clip == null || gain <= .005f) return;
            LoopVoice voice = null, free = null;
            foreach (var candidate in loopVoices)
            {
                if (candidate.cue == cue && candidate.owner == owner) { voice = candidate; break; }
                if (candidate.cue == AudioCue.None && free == null) free = candidate;
            }
            if (voice == null)
            {
                if (free == null) return;
                voice = free;
                voice.cue = cue;
                voice.owner = owner;
                voice.source.clip = sound.clip;
                voice.source.loop = true;
                voice.source.volume = 0;
                voice.source.Play();
            }
            voice.until = Time.unscaledTime + hold;
            voice.target = sound.volume * gain;
            voice.source.panStereo = pan;
            voice.source.pitch = pitch;
        }

        void UpdateLoops()
        {
            float step = Time.unscaledDeltaTime / Mathf.Max(.05f, loopFadeSeconds);
            foreach (var voice in loopVoices)
            {
                if (voice.cue == AudioCue.None) continue;
                float target = Time.unscaledTime <= voice.until ? voice.target : 0;
                voice.gain = Mathf.MoveTowards(voice.gain, target, step);
                voice.source.volume = voice.gain * EffectGain;
                if (voice.gain > 0 || target > 0) continue;
                voice.source.Stop();
                voice.cue = AudioCue.None;
            }
        }
    }
}
