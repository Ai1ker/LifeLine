#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Lifeline.Editor
{
    public static class WinchT4Migration
    {
        public static void UpdateTuning(GameTuning tuning)
        {
            // Append missing tiers without resetting any designer's existing rope lengths.
            if (tuning.ropeLengths != null && tuning.ropeLengths.Length >= 5) return;
            float[] expanded = { 28, 44, 54, 74, 100 };
            if (tuning.ropeLengths != null) System.Array.Copy(tuning.ropeLengths, expanded, tuning.ropeLengths.Length);
            tuning.ropeLengths = expanded;
        }

        [MenuItem("LifeLine/Balance/Apply Winch T4 (Preserve Custom Values)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode before migration.");
            foreach (string guid in AssetDatabase.FindAssets("t:GameTuning", new[] { GameProjectBuilder.Root }))
            {
                var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(AssetDatabase.GUIDToAssetPath(guid));
                Undo.RecordObject(tuning, "Append winch T4"); UpdateTuning(tuning); EditorUtility.SetDirty(tuning);
            }
            DefaultLocalizationBuilder.Build(); AssetDatabase.SaveAssets();
        }
    }
}
#endif
