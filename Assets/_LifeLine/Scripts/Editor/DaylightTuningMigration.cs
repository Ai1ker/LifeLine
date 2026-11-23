#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Lifeline.Editor
{
    public static class DaylightTuningMigration
    {
        [MenuItem("LifeLine/Apply Current Day-Night Timing (Preserve Custom Values)")]
        public static void Apply() => Debug.Log("Day-night timing: " + ApplyDefaults() + " legacy tuning assets updated.");

        public static int ApplyDefaults()
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:GameTuning", new[] { GameProjectBuilder.Root }))
            {
                var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(AssetDatabase.GUIDToAssetPath(guid));
                if (tuning == null || (!HasTiming(tuning, 180f, .67f, .13f) && !HasTiming(tuning, 200f, .703f, .117f))) continue;
                Undo.RecordObject(tuning, "Apply current day-night timing");
                tuning.daySeconds = GameTuning.DefaultCycleSeconds;
                tuning.dayFraction = GameTuning.DefaultDaylightFraction;
                tuning.duskFraction = GameTuning.DefaultDuskFraction;
                EditorUtility.SetDirty(tuning);
                AssetDatabase.SaveAssetIfDirty(tuning);
                changed++;
            }
            return changed;
        }

        static bool HasTiming(GameTuning tuning, float cycle, float daylight, float dusk) =>
            Mathf.Approximately(tuning.daySeconds, cycle) && Mathf.Approximately(tuning.dayFraction, daylight)
            && Mathf.Approximately(tuning.duskFraction, dusk);
    }
}
#endif
