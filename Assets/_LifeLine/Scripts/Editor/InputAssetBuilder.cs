#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lifeline.Editor
{
    public static class InputAssetBuilder
    {
        public const string DefaultPath = "Assets/_LifeLine/Input/LifelineControls.inputactions";

        // Existing inspector-authored bindings are retained unless explicitly regenerated.
        public static InputActionAsset CreateOrUpdateAsset(string assetPath = DefaultPath, bool overwrite = false)
        {
            if (!assetPath.StartsWith("Assets/") || Path.GetExtension(assetPath) != ".inputactions")
                throw new System.ArgumentException("The input asset must be an Assets/... .inputactions path.", nameof(assetPath));
            var existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(assetPath);
            if (existing != null && !overwrite) return existing;
            var directory = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var asset = CrewInputDefaults.CreateAsset();
            File.WriteAllText(assetPath, asset.ToJson());
            UnityEngine.Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(assetPath);
        }

        /// <summary>Repairs only the two recognized invalid base paths; binding IDs and player overrides are retained.</summary>
        public static List<string> MigrateLegacyDigitBindings(InputActionAsset asset, bool apply)
        {
            var changes = new List<string>();
            if (asset == null) return changes;
            foreach (string name in new[] { "Collect", "Fire" })
            {
                var action = asset.FindAction("Diver/" + name, false);
                if (action == null) continue;
                string oldPath = "<Keyboard>/digit" + (name == "Collect" ? "1" : "2");
                string newPath = "<Keyboard>/" + (name == "Collect" ? "1" : "2");
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if (action.bindings[i].path != oldPath) continue;
                    changes.Add("Diver/" + name + ": " + oldPath + " -> " + newPath);
                    if (apply) action.ChangeBinding(i).WithPath(newPath);
                }
            }
            return changes;
        }

        public static List<string> MigrateCrewInteractionBindings(InputActionAsset asset, bool apply) => CrewInputDefaults.MigrateInteractionBindings(asset, apply);

        [MenuItem("LifeLine/Migration/Apply Crew Interaction Bindings")]
        public static void ApplyCrewInteractionBindings()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(DefaultPath);
            if (asset == null || MigrateCrewInteractionBindings(asset, false).Count == 0) return;
            if (EditorUtility.IsDirty(asset)) throw new InvalidOperationException("Save the edited input asset before migrating crew interactions.");
            string backupFolder = "Assets/_LifeLine/Documentation/MigrationBackups/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            Directory.CreateDirectory(backupFolder);
            File.Copy(DefaultPath, Path.Combine(backupFolder, "LifelineControls.inputactions.bak"));
            var changes = MigrateCrewInteractionBindings(asset, true);
            File.WriteAllText(DefaultPath, asset.ToJson());
            AssetDatabase.ImportAsset(DefaultPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Debug.Log("Migrated crew interaction bindings; unrelated bindings and IDs retained.\n" + string.Join("\n", changes));
        }

        [MenuItem("LifeLine/Migration/Repair Legacy Digit Bindings")]
        private static void RepairLegacyDigitAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(DefaultPath);
            if (asset == null || MigrateLegacyDigitBindings(asset, false).Count == 0) { Debug.Log("No legacy digit bindings require repair."); return; }
            if (EditorUtility.IsDirty(asset)) { Debug.LogWarning("Save or discard the edited input asset before applying the digit migration."); return; }
            string backupFolder = "Assets/_LifeLine/Documentation/MigrationBackups/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            Directory.CreateDirectory(backupFolder);
            File.Copy(DefaultPath, Path.Combine(backupFolder, "LifelineControls.inputactions.bak"));
            if (File.Exists(DefaultPath + ".meta")) File.Copy(DefaultPath + ".meta", Path.Combine(backupFolder, "LifelineControls.inputactions.meta.bak"));
            var changes = MigrateLegacyDigitBindings(asset, true);
            File.WriteAllText(DefaultPath, asset.ToJson());
            AssetDatabase.ImportAsset(DefaultPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Debug.Log("Repaired input base paths; runtime PlayerPrefs overrides were not changed.\n" + string.Join("\n", changes));
        }
    }
}
#endif
