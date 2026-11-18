#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Lifeline.Editor
{
    /// <summary>Replace only the previous shipped tier values and append missing tiers.</summary>
    public static class WinchOxygenExpansionMigration
    {
        public static void UpdateTuning(GameTuning tuning)
        {
            tuning.ropeLengths=Expand(tuning.ropeLengths,new[]{28f,44f,54f,74f,100f},new[]{38f,54f,74f,100f,150f,200f});
            tuning.oxygenLevels=Expand(tuning.oxygenLevels,new[]{50f,75f,120f,150f},new[]{50f,90f,135f,175f,250f});
        }
        static float[] Expand(float[] current,float[] previous,float[] next)
        {
            // Once the new tier count is present, rerunning never rewrites authored values.
            if(current!=null&&current.Length>=next.Length)return current;
            float[] expanded=(float[])next.Clone();
            if(current==null)return expanded;
            for(int i=0;i<current.Length;i++)
                if(i>=previous.Length||!Mathf.Approximately(current[i],previous[i]))expanded[i]=current[i];
            return expanded;
        }
        [MenuItem("LifeLine/Balance/Apply Extended Winch and Oxygen (Preserve Custom Values)")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play mode before migration.");
            foreach(string guid in AssetDatabase.FindAssets("t:GameTuning",new[]{GameProjectBuilder.Root}))
            {
                var tuning=AssetDatabase.LoadAssetAtPath<GameTuning>(AssetDatabase.GUIDToAssetPath(guid));
                Undo.RecordObject(tuning,"Extend winch and oxygen tiers");UpdateTuning(tuning);EditorUtility.SetDirty(tuning);
            }
            DefaultLocalizationBuilder.Build();AssetDatabase.SaveAssets();
        }
    }
}
#endif
