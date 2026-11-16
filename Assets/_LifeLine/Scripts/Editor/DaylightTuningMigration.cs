#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Lifeline.Editor
{
    public static class DaylightTuningMigration
    {
        [MenuItem("LifeLine/Apply Daylight Extension (Preserve Custom Values)")]
        public static void Apply()
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:GameTuning", new[] { GameProjectBuilder.Root }))
            {
                var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(AssetDatabase.GUIDToAssetPath(guid));
                if (!Mathf.Approximately(tuning.daySeconds, 180f) || !Mathf.Approximately(tuning.dayFraction, .67f)
                    || !Mathf.Approximately(tuning.duskFraction, .13f)) continue;
                Undo.RecordObject(tuning, "Extend daylight by 20 seconds");
                tuning.daySeconds = 200f; tuning.dayFraction = .703f; tuning.duskFraction = .117f;
                EditorUtility.SetDirty(tuning); changed++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Daylight extension: " + changed + " legacy tuning assets updated; custom/already updated values preserved.");
        }
    }
}
#endif
