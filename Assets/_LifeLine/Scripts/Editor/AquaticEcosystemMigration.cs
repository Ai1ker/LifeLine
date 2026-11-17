#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lifeline.Editor
{
    /// <summary>Explicit migration; preserves custom tuning, scenes and authored art.</summary>
    public static class AquaticEcosystemMigration
    {
        [MenuItem("LifeLine/World/Apply Aquatic Ecosystem (Preserve Custom Values)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode before migration.");
            var library = PrefabLibraryBuilder.Build();
            foreach (string guid in AssetDatabase.FindAssets("t:GameTuning", new[] { GameProjectBuilder.Root }))
            {
                var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(AssetDatabase.GUIDToAssetPath(guid));
                Undo.RecordObject(tuning, "Update aquatic ecosystem tuning");
                if (tuning.maximumSharks == 6) tuning.maximumSharks = 8;
                if (Mathf.Approximately(tuning.whiteSharkHealth, 4)) tuning.whiteSharkHealth = 3;
                if (Mathf.Approximately(tuning.sharkDetectionRange, 6.7f)) tuning.sharkDetectionRange = 3.6f;
                if (Mathf.Approximately(tuning.sharkNightDetectionRange, 9.6f)) tuning.sharkNightDetectionRange = 1.8f;
                EditorUtility.SetDirty(tuning);
            }
            var wall = Material("CaveWall", library.worldPalette[0], new Color(.9f, .6f, .3f));
            var background = Material("CaveBackground", library.worldPalette[0], new Color(.18f, .13f, .24f));
            foreach (var game in Object.FindObjectsByType<GameSession>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.RecordObject(game, "Bind piranha assets");
                if (game.piranhaPrefab == null) game.piranhaPrefab = library.piranha;
                if (game.piranhaMaterial == null) game.piranhaMaterial = library.piranhaMaterial;
                EditorUtility.SetDirty(game);
                if (game.world != null)
                {
                    Undo.RecordObject(game.world, "Bind cave and piranha assets");
                    if (game.world.piranhaPrefab == null) game.world.piranhaPrefab = library.piranha;
                    if (game.world.caveWallMaterial == null) game.world.caveWallMaterial = wall;
                    if (game.world.caveBackgroundMaterial == null) game.world.caveBackgroundMaterial = background;
                    EditorUtility.SetDirty(game.world);
                }
                EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            }
            AssetDatabase.SaveAssets();
        }
        static Material Material(string name, Material template, Color color)
        {
            string path = GameProjectBuilder.Root + "/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
            var material = new Material(template) { name = name }; material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path); return material;
        }
    }
}
#endif
