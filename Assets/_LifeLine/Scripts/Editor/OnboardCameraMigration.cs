#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lifeline.Editor
{
    public static class OnboardCameraMigration
    {
        public static void UpdateRig(GameCameraRig rig)
        {
            // Upgrade shipped framing only. Designer-authored camera values remain editable.
            if(Mathf.Approximately(rig.sharedMinimumSize,7.5f))rig.sharedMinimumSize=5.4f;
            if(Mathf.Approximately(rig.sharedMaximumSize,24))rig.sharedMaximumSize=12;
            if(rig.sharedPadding==new Vector2(1.8f,1.3f))rig.sharedPadding=new Vector2(.8f,.65f);
            if(Mathf.Approximately(rig.shipHalfWidth,12))rig.shipHalfWidth=12.15f;
            if(rig.shipVerticalRange==new Vector2(-5,3))rig.shipVerticalRange=new Vector2(-4.5f,2.5f);
        }

        [MenuItem("LifeLine/Presentation/Apply Onboard Framing to Loaded Scenes (Preserve Custom Values)")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play mode before migration.");
            foreach(var rig in Object.FindObjectsByType<GameCameraRig>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                Undo.RecordObject(rig,"Tighten onboard framing");UpdateRig(rig);rig.BuildRuntimeRig();
                EditorUtility.SetDirty(rig);EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
            }
        }
    }
}
#endif
