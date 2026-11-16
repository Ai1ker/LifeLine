#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace Lifeline.Editor
{
    public static class GameplayBatchMigration
    {
        const string Root=GameProjectBuilder.Root;
        [MenuItem("LifeLine/Migration/Apply Gameplay And Repair Batch (Preserve Custom Values)")]
        public static void Apply()
        {
            var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(active.isDirty)throw new InvalidOperationException("Save scene edits before migration.");
            string backup=Root+"/Documentation/GameplayBackups/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_ffff");Directory.CreateDirectory(backup);
            File.Copy(GameProjectBuilder.PlayScene,backup+"/Gameplay.unity.bak");
            var tuning=AssetDatabase.LoadAssetAtPath<GameTuning>(Root+"/Data/Tuning/GameTuning.asset");
            File.Copy(AssetDatabase.GetAssetPath(tuning),backup+"/GameTuning.asset.bak");
            if(Equal(tuning.ropeLengths,new[]{20f,26f,34f,44f})||Equal(tuning.ropeLengths,new[]{20f,26f,34f,74f})||Equal(tuning.ropeLengths,new[]{20f,26f,34f,54f})||Equal(tuning.ropeLengths,new[]{28f,38f,48f,68f}))tuning.ropeLengths=new[]{28f,44f,54f,74f};
            if(Equal(tuning.oxygenLevels,new[]{40f,70f,100f,130f}))tuning.oxygenLevels=new[]{40f,70f,115f,145f};
            if(Mathf.Approximately(tuning.shadowSweepHullDamage,25))tuning.shadowSweepHullDamage=10;
            EditorUtility.SetDirty(tuning);
            var input=AssetDatabase.LoadAssetAtPath<InputActionAsset>(Root+"/Input/LifelineControls.inputactions");
            if(input!=null)
            {
                var action=input.FindAction("Diver/Melee");if(action!=null&&action.bindings[0].path=="<Keyboard>/k")
                {File.Copy(AssetDatabase.GetAssetPath(input),backup+"/LifelineControls.inputactions.bak");action.ChangeBinding(0).WithPath("<Keyboard>/0");File.WriteAllText(AssetDatabase.GetAssetPath(input),input.ToJson());AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(input));}
            }
            var scene=EditorSceneManager.OpenScene(GameProjectBuilder.PlayScene);
            foreach(var panel in UnityEngine.Object.FindObjectsByType<CrewStationPanel>(FindObjectsInactive.Include))
            {InstallPanel(panel);if(Vector2.Distance(panel.fishMoveSpeeds,new Vector2(1.5f,2.15f))<.001f)panel.fishMoveSpeeds=new Vector2(1.35f,1.935f);EditorUtility.SetDirty(panel);}
            var session=UnityEngine.Object.FindFirstObjectByType<GameSession>();
            if(session!=null){session.areaWarningPrefab=WarningPrefab();EditorUtility.SetDirty(session);}
            var hud=UnityEngine.Object.FindFirstObjectByType<GameHUDController>();if(hud!=null)InstallBars(hud);
            DefaultLocalizationBuilder.Build();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);GameProjectBuilder.ExportCurrentTuning();
            Debug.Log("Gameplay/repair batch migrated. Backup: "+backup);
        }
        static bool Equal(float[] a,float[] b){if(a==null||a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(!Mathf.Approximately(a[i],b[i]))return false;return true;}
        public static void InstallPanel(CrewStationPanel panel)
        {
            string data=Root+"/Data/UI/RepairVisualTheme.asset";Directory.CreateDirectory(Path.GetDirectoryName(data));AssetDatabase.Refresh();
            var theme=AssetDatabase.LoadAssetAtPath<RepairVisualTheme>(data);if(theme==null){theme=ScriptableObject.CreateInstance<RepairVisualTheme>();AssetDatabase.CreateAsset(theme,data);}
            if(panel.repairTheme==null)panel.repairTheme=theme;
            if(panel.repairBoardPrefab==null)panel.repairBoardPrefab=SimplePrefab("UI/RepairBoardView",typeof(RepairBoardView),true).GetComponent<RepairBoardView>();
        }
        public static GameObject WarningPrefab()=>SimplePrefab("Effects/AreaWarning",typeof(AreaWarningView));
        static GameObject SimplePrefab(string name,Type component,bool rect=false)
        {
            string path=Root+"/Prefabs/"+name+".prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
            Directory.CreateDirectory(Path.GetDirectoryName(path));AssetDatabase.Refresh();var obj=rect?new GameObject(Path.GetFileName(name),typeof(RectTransform)):new GameObject(Path.GetFileName(name));obj.AddComponent(component);
            try{return PrefabUtility.SaveAsPrefabAsset(obj,path);}finally{UnityEngine.Object.DestroyImmediate(obj);}
        }
        public static void InstallBars(GameHUDController hud)
        {
            var existing=hud.GetComponentInChildren<StatusBarValuesView>(true);if(existing!=null){InstallBarBackground(existing);return;}
            var obj=new GameObject("HUD bar names and values",typeof(RectTransform));obj.transform.SetParent(hud.transform,false);
            var root=obj.GetComponent<RectTransform>();root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
            var view=obj.AddComponent<StatusBarValuesView>();view.session=hud.session;
            view.hull=BarLabel(root,"Hull label",.02f,.25f);view.power=BarLabel(root,"Power label",.28f,.51f);view.oxygen=BarLabel(root,"Oxygen label",.54f,.97f);
            InstallBarBackground(view);obj.transform.SetAsLastSibling();
        }
        static void InstallBarBackground(StatusBarValuesView view)
        {
            if(view.background!=null)return;
            var obj=new GameObject("Readable bar label strip",typeof(RectTransform));obj.transform.SetParent(view.transform,false);obj.transform.SetAsFirstSibling();
            var rect=obj.GetComponent<RectTransform>();rect.anchorMin=new Vector2(0,.799f);rect.anchorMax=new Vector2(1,.823f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            view.background=obj.AddComponent<Image>();view.background.color=new Color(.9f,.94f,.91f,.98f);view.background.raycastTarget=false;
        }
        static Text BarLabel(Transform parent,string name,float left,float right)
        {
            var obj=new GameObject(name,typeof(RectTransform));obj.transform.SetParent(parent,false);var rect=obj.GetComponent<RectTransform>();rect.anchorMin=new Vector2(left,.803f);rect.anchorMax=new Vector2(right,.823f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            var text=obj.AddComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=14;text.color=new Color(.07f,.18f,.2f);text.alignment=TextAnchor.MiddleLeft;text.raycastTarget=false;return text;
        }
    }
}
#endif
