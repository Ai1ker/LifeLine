#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lifeline.Editor
{
    /// <summary>Only replaces shipped defaults. Custom designer values and authored art are retained.</summary>
    public static class SurvivalBalanceMigration
    {
        public static void UpdateTuning(GameTuning t)
        {
            WinchT4Migration.UpdateTuning(t);
            if(Mathf.Approximately(t.boatSpeed,3.4f))t.boatSpeed=3;
            if(Mathf.Approximately(t.diveBoatMultiplier,.28f))t.diveBoatMultiplier=.3f;
            if(Mathf.Approximately(t.routeDistance,90))t.routeDistance=200;
            if(Mathf.Approximately(t.routePowerCost,80))t.routePowerCost=200;
            if(Mathf.Approximately(t.shallowBoundary,24))t.shallowBoundary=200*24f/90;
            if(Mathf.Approximately(t.deepBoundary,56))t.deepBoundary=200*56f/90;
            if(Mathf.Approximately(t.baseOxygen,40))t.baseOxygen=50;
            float[] oldOxygen={40,70,115,145}, oxygen={50,75,120,150};
            float[] oldCraft={40,35,25,20}, craft={40,30,20,15};
            ReplaceDefaults(t.oxygenLevels,oldOxygen,oxygen);ReplaceDefaults(t.craftSecondsByLevel,oldCraft,craft);
            if(Mathf.Approximately(t.upgradedCraftSeconds,25))t.upgradedCraftSeconds=20;
            if(Mathf.Approximately(t.fuelPower,25))t.fuelPower=15;
            if(Mathf.Approximately(t.lightningWarning,2.2f))t.lightningWarning=2;
            if(t.baseDiverHitCapacity==1)t.baseDiverHitCapacity=2;
            if(t.upgradedDiverHitCapacity==2)t.upgradedDiverHitCapacity=3;
            if(Mathf.Approximately(t.piranhaDamage,.5f))t.piranhaDamage=1;
        }
        static void ReplaceDefaults(float[] target,float[] oldValues,float[] values)
        {if(target==null||target.Length!=values.Length)return;for(int i=0;i<values.Length;i++)if(Mathf.Approximately(target[i],oldValues[i]))target[i]=values[i];}
        [MenuItem("LifeLine/Balance/Apply Survival Balance (Preserve Custom Values)")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play mode before migration.");
            foreach(string guid in AssetDatabase.FindAssets("t:GameTuning",new[]{GameProjectBuilder.Root}))
            {
                var t=AssetDatabase.LoadAssetAtPath<GameTuning>(AssetDatabase.GUIDToAssetPath(guid));
                Undo.RecordObject(t,"Survival balance");UpdateTuning(t);EditorUtility.SetDirty(t);
            }
            foreach(var game in Object.FindObjectsByType<GameSession>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                Undo.RecordObject(game,"Survival scene balance");
                if(Mathf.Approximately(game.rescueArrivalX,85))game.rescueArrivalX=200*85f/90;
                foreach(var cost in game.backpackUpgradeCost)if(cost.resource==ResourceKind.FiberBundle&&cost.amount==1)cost.amount=2;
                if(game.ship!=null){Undo.RecordObject(game.ship,"Longer voyage");if(Mathf.Approximately(game.ship.eastLimit,90))game.ship.eastLimit=200;EditorUtility.SetDirty(game.ship);}
                foreach(var actor in game.Crew)if(actor!=null){Undo.RecordObject(actor,"Longer water bounds");if(Mathf.Approximately(actor.rightWorldLimit,100))actor.rightWorldLimit=200*100f/90;EditorUtility.SetDirty(actor);}
                if(game.world!=null)
                {
                    var world=game.world;Undo.RecordObject(world,"Proportional sea regions");
                    if(world.areaCenters.Length==3){if(Mathf.Approximately(world.areaCenters[1],36))world.areaCenters[1]=80;if(Mathf.Approximately(world.areaCenters[2],72))world.areaCenters[2]=160;}
                    if(Mathf.Approximately(world.worldBounds.xMax,100)){var bounds=world.worldBounds;bounds.xMax=200*100f/90;world.worldBounds=bounds;}
                    EditorUtility.SetDirty(world);
                }
                EditorUtility.SetDirty(game);EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            }
            foreach(var rig in Object.FindObjectsByType<GameCameraRig>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {Undo.RecordObject(rig,"Longer water camera bounds");if(Mathf.Approximately(rig.underwaterViewBounds.xMax,100)){var b=rig.underwaterViewBounds;b.xMax=200*100f/90;rig.underwaterViewBounds=b;EditorUtility.SetDirty(rig);EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);}}
            foreach(string path in new[]{GameProjectBuilder.Root+"/Prefabs/Ship/Ship.prefab",GameProjectBuilder.Root+"/Prefabs/Characters/Captain.prefab",GameProjectBuilder.Root+"/Prefabs/Characters/Diver.prefab"})
            {
                if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null)continue;
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed=false;
                    var ship=root.GetComponent<ShipMotor>();if(ship!=null&&Mathf.Approximately(ship.eastLimit,90)){ship.eastLimit=200;changed=true;}
                    var actor=root.GetComponent<CrewActor>();if(actor!=null&&Mathf.Approximately(actor.rightWorldLimit,100)){actor.rightWorldLimit=200*100f/90;changed=true;}
                    if(changed)PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            DefaultLocalizationBuilder.Build();AssetDatabase.SaveAssets();
        }
    }
}
#endif
