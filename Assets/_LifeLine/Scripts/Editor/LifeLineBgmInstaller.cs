#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Lifeline.Editor
{
    public static class LifeLineBgmInstaller
    {
        const string Folder = GameProjectBuilder.Root + "/Audio/BGM/";
        const string PrefabPath = GameProjectBuilder.Root + "/Prefabs/Audio/VoyageAudio.prefab";

        [MenuItem("LifeLine/Audio/Bind Voyage Music Tracks")]
        public static void Install() => Debug.Log(Apply());

        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before binding music.");
            var delivered = new[]
            {
                Track(BgmContext.Menu, "menuloop.mp3"),
                Track(BgmContext.OnlineRoom, "8bitloop.wav"),
                Track(BgmContext.ShallowsDay, "piano1.wav"),
                Track(BgmContext.CityDay, "piano2.wav"),
                Track(BgmContext.DeepDay, "piano3.wav"),
                Track(BgmContext.Dusk, "dusk2.wav"),
                Track(BgmContext.Night, "m2.wav"),
                Track(BgmContext.Night, "m3.wav")
            };

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var manager = root.GetComponentInChildren<BgmManager>(true);
                manager.tracks.RemoveAll(track => track == null ||
                    (track.clip == null && !Enum.IsDefined(typeof(BgmContext), track.context)));
                foreach (var track in delivered)
                {
                    if (manager.tracks.Exists(item => item.context == track.context && item.clip == track.clip)) continue;
                    var empty = manager.tracks.Find(item => item.context == track.context && item.clip == null);
                    if (empty == null) manager.tracks.Add(track);
                    else empty.clip = track.clip;
                }
                foreach (var source in new[] { manager.first, manager.second })
                    if (source.priority == 128) source.priority = 32;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
                if (!saved) throw new InvalidOperationException("Could not save VoyageAudio.prefab.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return $"Bound {delivered.Length} BGM clips to VoyageAudio.";
        }

        static BgmManager.Track Track(BgmContext context, string filename)
        {
            string path = Folder + filename;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null || clip.length <= 0) throw new InvalidOperationException("Missing or invalid BGM clip: " + path);
            if (clip.length >= 30 && AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                var settings = importer.defaultSampleSettings;
                if (settings.loadType != AudioClipLoadType.Streaming)
                {
                    settings.loadType = AudioClipLoadType.Streaming;
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    settings.quality = .8f;
                    importer.defaultSampleSettings = settings;
                    importer.SaveAndReimport();
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }
            }
            return new BgmManager.Track { context = context, clip = clip };
        }
    }
}
#endif
