#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Lifeline.Editor
{
    public static class LifeLineAudioInstaller
    {
        const string Root = GameProjectBuilder.Root;
        const string LibraryPath = Root + "/Data/Audio/SoundLibrary.asset";
        const string PrefabPath = Root + "/Prefabs/Audio/VoyageAudio.prefab";

        [MenuItem("LifeLine/Audio/Install Delivered Audio")]
        public static void Install() => Debug.Log(Apply());

        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before installing audio.");
            EnsureFolder(Root + "/Data", "Audio");
            EnsureFolder(Root + "/Prefabs", "Audio");
            var library = BuildLibrary();
            var prefab = BuildAudioPrefab(library);
            string backup = Root + "/Documentation/SceneBackups/Audio-" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_ffff");
            Directory.CreateDirectory(backup);
            InstallButtonPrefabs();
            BindScene(GameProjectBuilder.MenuScene, prefab, backup);
            BindScene(GameProjectBuilder.PlayScene, prefab, backup);
            DefaultLocalizationBuilder.Build();
            AssetDatabase.SaveAssets();
            return $"Bound {library.sounds.Count} clips and both settings panels. Scene backups: {backup}";
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        static AudioLibrary BuildLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            foreach (var file in Directory.EnumerateFiles(Root + "/Audio", "*.wav", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
            {
                string stem = Path.GetFileNameWithoutExtension(file);
                string cueName = string.Concat((stem.StartsWith("sfx_", StringComparison.Ordinal) ? stem.Substring(4) : stem)
                    .Split('_').Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
                if (!Enum.TryParse<AudioCue>(cueName, out var cue)) continue;
                if (library.sounds.Exists(sound => sound != null && sound.cue == cue)) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(file.Replace('\\', '/'));
                if (clip == null) throw new InvalidOperationException("Could not import " + file);
                library.sounds.Add(new AudioLibrary.Sound { cue = cue, clip = clip });
            }
            int expected = Enum.GetValues(typeof(AudioCue)).Length - 1;
            if (library.sounds.Count(sound => sound != null && sound.clip != null) != expected)
                throw new InvalidOperationException($"The audio library must contain all {expected} cue bindings.");
            library.Refresh();
            EditorUtility.SetDirty(library);
            return library;
        }

        static GameObject BuildAudioPrefab(AudioLibrary library)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;
            var root = new GameObject("Voyage Audio");
            try
            {
                var audio = root.AddComponent<GameAudio>();
                audio.library = library;
                var music = new GameObject("BGM Manager").transform;
                music.SetParent(root.transform, false);
                audio.bgm = music.gameObject.AddComponent<BgmManager>();
                audio.bgm.first = Source(music, "Track A");
                audio.bgm.second = Source(music, "Track B");
                foreach (BgmContext context in Enum.GetValues(typeof(BgmContext)))
                    audio.bgm.tracks.Add(new BgmManager.Track { context = context });
                audio.effects = Voices(root.transform, "One-shot AudioSources", 16);
                audio.loops = Voices(root.transform, "Loop AudioSources", 24);
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static AudioSource[] Voices(Transform root, string name, int count)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(root, false);
            var sources = new AudioSource[count];
            for (int i = 0; i < count; i++) sources[i] = Source(holder, "Channel " + (i + 1));
            return sources;
        }

        static AudioSource Source(Transform parent, string name)
        {
            var source = new GameObject(name).AddComponent<AudioSource>();
            source.transform.SetParent(parent, false);
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.dopplerLevel = 0;
            return source;
        }

        static void BindScene(string path, GameObject prefab, string backup)
        {
            File.Copy(path, backup + "/" + Path.GetFileName(path) + ".bak");
            var oldActive = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                if (scene.isDirty && !EditorSceneManager.SaveScene(scene, backup + "/" + scene.name + "-open.unity", true))
                    throw new IOException("Could not back up " + scene.name);
                var audio = Find<GameAudio>(scene);
                if (audio == null)
                    audio = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<GameAudio>();
                audio.session = Find<GameSession>(scene);
                audio.hud = Find<GameHUDController>(scene);
                audio.saveManager = Find<GameSaveManager>(scene);
                PrefabUtility.RecordPrefabInstancePropertyModifications(audio);
                if (Find<AudioListener>(scene) == null) audio.gameObject.AddComponent<AudioListener>();
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var panel in root.GetComponentsInChildren<GameSettingsPanel>(true)) AudioSettingsAuthoring.Install(panel);
                    foreach (var button in root.GetComponentsInChildren<Button>(true)) BindButton(button);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
            }
            finally
            {
                if (oldActive.isLoaded) SceneManager.SetActiveScene(oldActive);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static T Find<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().Select(root => root.GetComponentInChildren<T>(true)).FirstOrDefault(component => component != null);

        static void InstallButtonPrefabs()
        {
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { Root + "/Prefabs/UI" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => AssetDatabase.GetDependencies(path).Length);
            foreach (string path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab.GetComponentsInChildren<Button>(true).Any(button => button.GetComponent<AudioUiFeedback>() == null)) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var button in root.GetComponentsInChildren<Button>(true)) BindButton(button);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        static void BindButton(Button button)
        {
            if (button.GetComponent<AudioUiFeedback>() != null) return;
            var feedback = button.gameObject.AddComponent<AudioUiFeedback>();
            string name = button.name.ToLowerInvariant();
            if (name.Contains("close") || name.Contains("back") || name.Contains("cancel") || name.Contains("resume"))
                feedback.click = AudioCue.UiBack;
            else if (name.Contains("tab")) feedback.click = AudioCue.UiTab;
            else if (name.Contains("toggle") || name.Contains("language") || name.Contains("cameramode"))
                feedback.click = AudioCue.UiToggle;
        }
    }
}
#endif
