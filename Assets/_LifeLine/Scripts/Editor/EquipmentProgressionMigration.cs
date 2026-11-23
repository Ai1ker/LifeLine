#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lifeline.Editor
{
    /// <summary>Explicit asset migration. Recognized shipped defaults change; custom prices and art remain intact.</summary>
    public static class EquipmentProgressionMigration
    {
        const string RecipeRoot = "Assets/_LifeLine/Data/Recipes";

        public static void UpdateTuning(GameTuning tuning)
        {
            if (tuning == null) return;
            if (Mathf.Approximately(tuning.initialPower, 20)) tuning.initialPower = 0;
            if (tuning.firstEngineRepairPlates == 3) tuning.firstEngineRepairPlates = 2;
        }

        static bool SameCost(ResourceAmount[] actual, params ResourceAmount[] expected)
        {
            if (actual == null || actual.Length != expected.Length || actual.Any(item => item == null)) return false;
            return expected.All(other => actual.Count(item => other.resource == item.resource && other.amount == item.amount) == expected.Count(item => other.resource == item.resource && other.amount == item.amount));
        }

        public static void UpdateKnownRecipeDefaults(RecipeDefinition recipe)
        {
            if (recipe == null) return;
            if (recipe.id == "harpoon")
            {
                if (SameCost(recipe.cost, new ResourceAmount(ResourceKind.Plate, 1))) recipe.cost = new[] { new ResourceAmount(ResourceKind.Plate, 2) };
                if (recipe.description == "永久近战工具，制作鱼枪时保留。") recipe.description = "近战鱼叉；制作射矛枪时消耗1把。";
            }
            if (recipe.id == "speargun")
            {
                if (SameCost(recipe.cost, new ResourceAmount(ResourceKind.Plate, 1), new ResourceAmount(ResourceKind.FiberBundle, 1)))
                    recipe.cost = new[] { new ResourceAmount(ResourceKind.Plate, 2), new ResourceAmount(ResourceKind.FiberBundle, 2) };
                if (recipe.consumedToolIds == null || recipe.consumedToolIds.Length == 0) recipe.consumedToolIds = new[] { "harpoon" };
                if (recipe.description == "先制作鱼叉；独立鱼枪键发射，再按召回。") recipe.description = "消耗1把鱼叉；独立鱼枪键发射，再按召回。";
            }
        }

        public static void UpdateSession(GameSession session, RecipeDefinition generator, RecipeDefinition repairBench)
        {
            if (session == null) return;
            if (Mathf.Approximately(session.filterMoveMultiplier, 1.1f)) session.filterMoveMultiplier = 1.2f;
            if (SameCost(session.filterRepairCost, new ResourceAmount(ResourceKind.Plate, 1))) session.filterRepairCost = new[] { new ResourceAmount(ResourceKind.Plate, 2) };
            var recipes = new List<RecipeDefinition>(session.recipes ?? Array.Empty<RecipeDefinition>());
            foreach (var recipe in new[] { generator, repairBench })
                if (recipe != null && !recipes.Any(existing => existing != null && existing.id == recipe.id)) recipes.Add(recipe);
            session.recipes = recipes.ToArray();
        }

        public static RecipeDefinition[] EnsureEquipmentRecipes()
        {
            string parent = "Assets";
            foreach (string part in RecipeRoot.Split('/').Skip(1))
            { string path = parent + "/" + part; if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, part); parent = path; }
            return new[]
            {
                EnsureRecipe("14_Generator", "generator", "手摇发电机", "2金属、2木材、1纤维建造船舱手摇发电机。",
                    new[] { new ResourceAmount(ResourceKind.Metal, 2), new ResourceAmount(ResourceKind.Wood, 2), new ResourceAmount(ResourceKind.Fiber, 1) }),
                EnsureRecipe("15_RepairBench", "repair-bench", "船体维修台", "1木材、1金属建造船体维修台。",
                    new[] { new ResourceAmount(ResourceKind.Wood, 1), new ResourceAmount(ResourceKind.Metal, 1) })
            };
        }

        static RecipeDefinition EnsureRecipe(string file, string id, string name, string description, ResourceAmount[] cost)
        {
            string path = RecipeRoot + "/" + file + ".asset";
            var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path);
            if (recipe != null) return recipe;
            recipe = ScriptableObject.CreateInstance<RecipeDefinition>(); recipe.id = id; recipe.toolId = id;
            recipe.displayName = name; recipe.description = description; recipe.cost = cost; recipe.hideWhenCompleted = true;
            AssetDatabase.CreateAsset(recipe, path); return recipe;
        }

        [MenuItem("LifeLine/Balance/Apply Equipment Progression (Preserve Custom Values)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before migrating equipment assets.");
            RecipeDefinition[] equipment = EnsureEquipmentRecipes();
            foreach (string guid in AssetDatabase.FindAssets("t:GameTuning", new[] { GameProjectBuilder.Root }))
            {
                var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(AssetDatabase.GUIDToAssetPath(guid));
                Undo.RecordObject(tuning, "Equipment progression tuning"); UpdateTuning(tuning); EditorUtility.SetDirty(tuning);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:RecipeDefinition", new[] { RecipeRoot }))
            {
                var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                Undo.RecordObject(recipe, "Equipment progression recipe"); UpdateKnownRecipeDefaults(recipe); EditorUtility.SetDirty(recipe);
            }
            Scene scene = SceneManager.GetSceneByPath(GameProjectBuilder.PlayScene);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(GameProjectBuilder.PlayScene, OpenSceneMode.Additive);
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects()) foreach (GameSession session in root.GetComponentsInChildren<GameSession>(true))
                {
                    Undo.RecordObject(session, "Equipment progression scene"); UpdateSession(session, equipment[0], equipment[1]);
                    EditorUtility.SetDirty(session); if (session.craftingManager != null) EditorUtility.SetDirty(session.craftingManager);
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
