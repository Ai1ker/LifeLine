#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Lifeline.Editor
{
    public static class NauticalUiMigration
    {
        const string ActionPath = GameProjectBuilder.Root + "/Prefabs/UI/ActionButton.prefab";
        const string RoomPath = GameProjectBuilder.Root + "/Prefabs/UI/OnlineRoomPanel.prefab";
        const string RepairThemePath = GameProjectBuilder.Root + "/Data/UI/RepairVisualTheme.asset";

        [MenuItem("LifeLine/UI/Install Authored Nautical Interface")]
        public static void InstallFromMenu() => Debug.Log(Apply());

        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before installing the interface.");
            string[] scenePaths = { GameProjectBuilder.MenuScene, GameProjectBuilder.PlayScene };
            foreach (string path in scenePaths)
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                if (scene.isLoaded && scene.isDirty) throw new InvalidOperationException("Save open scene edits before installing the interface.");
            }
            if (NauticalUiSkin.Sprite("Frame") == null)
                throw new InvalidOperationException("Nautical Sprite assets are missing. Import the UI artwork before installing the interface.");
            string backup = GameProjectBuilder.Root + "/Documentation/SceneBackups/NauticalUI-" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_ffff");
            Directory.CreateDirectory(backup);
            foreach (string path in new[] { scenePaths[0], scenePaths[1], ActionPath, RoomPath, RepairThemePath })
                if (File.Exists(path)) File.Copy(path, Path.Combine(backup, Path.GetFileName(path) + ".bak"));

            ApplyCommonAssets();
            EditScene<MainMenuController>(GameProjectBuilder.MenuScene, NauticalMenuAuthoring.Apply);
            EditScene<GameHUDController>(GameProjectBuilder.PlayScene, VoyageHudAuthoring.Install);
            DefaultLocalizationBuilder.Build();
            AssetDatabase.SaveAssets();
            return backup;
        }

        public static void ApplyCommonAssets()
        {
            EditPrefab<Button>(ActionPath, button =>
            {
                if (button.GetComponent<Image>().sprite == null) NauticalUiSkin.StyleButton(button, 17);
            });
            EditPrefab<OnlineRoomPanel>(RoomPath, NauticalMenuAuthoring.ApplyRoom);
            var repair = AssetDatabase.LoadAssetAtPath<RepairVisualTheme>(RepairThemePath);
            if (repair != null && repair.board == null && repair.background == new Color(.06f, .15f, .19f))
            {
                repair.background = new Color32(75, 48, 36, 255);
                EditorUtility.SetDirty(repair);
            }
        }

        static void EditPrefab<T>(string path, Action<T> update) where T : Component
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                update(prefab.GetComponent<T>());
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        static void EditScene<T>(string path, Action<T> update) where T : Component
        {
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    var owner = root.GetComponentInChildren<T>(true);
                    if (owner == null) continue;
                    update(owner);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
                    return;
                }
                throw new InvalidOperationException(typeof(T).Name + " is missing in " + path);
            }
            finally
            {
                if (previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
#endif
