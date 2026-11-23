#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lifeline.Editor
{
    public static class RepairAndPickupArtInstaller
    {
        const string WorldArtPath = GameProjectBuilder.Root + "/Art/Environment/UnderwaterSprites.asset";

        [MenuItem("LifeLine/Art/Install Repair Boards and Pickup Prefabs")]
        public static void InstallFromMenu() => Debug.Log(Apply());

        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before authoring repair art.");
            var art = AssetDatabase.LoadAssetAtPath<PixelWorldArt>(WorldArtPath);
            if (art == null) throw new InvalidOperationException("Import UnderwaterSprites.asset first.");

            string backup = GameProjectBuilder.Root + "/Documentation/SceneBackups/RepairAndPickupArt-" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_ffff");
            Directory.CreateDirectory(backup);
            foreach (string path in new[] { WorldArtPath, RepairBoardAuthoring.ThemePath, RepairBoardAuthoring.BoardPath, GameProjectBuilder.PlayScene })
                if (File.Exists(path)) File.Copy(path, backup + "/" + Path.GetFileName(path) + ".bak");

            RepairBoardAuthoring.Install();
            BindGameplay(art, backup);
            AssetDatabase.SaveAssets();
            return backup;
        }

        static void BindGameplay(PixelWorldArt art, string backup)
        {
            var active = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(GameProjectBuilder.PlayScene);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(GameProjectBuilder.PlayScene, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                if (scene.isDirty && !EditorSceneManager.SaveScene(scene, backup + "/Gameplay-open.unity", true))
                    throw new IOException("Could not back up the open Gameplay scene.");
                var roots = scene.GetRootGameObjects();
                var session = roots.Select(root => root.GetComponentInChildren<GameSession>(true)).FirstOrDefault(found => found != null);
                if (session == null || session.world == null) throw new InvalidOperationException("Gameplay world is missing.");
                UnderwaterPickupArtAuthoring.Install(session.world, art);
                var board = AssetDatabase.LoadAssetAtPath<GameObject>(RepairBoardAuthoring.BoardPath).GetComponent<RepairBoardView>();
                var theme = AssetDatabase.LoadAssetAtPath<RepairVisualTheme>(RepairBoardAuthoring.ThemePath);
                foreach (var panel in roots.SelectMany(root => root.GetComponentsInChildren<CrewStationPanel>(true)))
                {
                    if (panel.repairBoardPrefab == null) panel.repairBoardPrefab = board;
                    if (panel.repairTheme == null) panel.repairTheme = theme;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Gameplay.");
            }
            finally
            {
                if (active.isLoaded) SceneManager.SetActiveScene(active);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
#endif
