using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Lifeline.Editor
{
    internal static partial class PixelArtInstaller
    {
        [MenuItem("LifeLine/Art/Install Marine Polish")]
        public static void InstallMarinePolish()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before installing marine art.");

            var worldArt = AssetDatabase.LoadAssetAtPath<PixelWorldArt>(Root + "/Environment/UnderwaterSprites.asset");
            worldArt.eelGenerator = Required("Items", "EelGenerator");
            worldArt.ruinWall = WallMaterial("RuinWall");
            worldArt.caveWall = WallMaterial("CaveWall");
            foreach (var wall in new[] { worldArt.ruinWall, worldArt.caveWall })
            {
                wall.SetVector("_TileRegion", new Vector4(0, 0, 1, 1));
                wall.SetVector("_TileSize", new Vector4(3.2f, 3.2f, 0, 0));
                EditorUtility.SetDirty(wall);
            }
            EditorUtility.SetDirty(worldArt);

            var scenery = AssetDatabase.LoadAssetAtPath<PixelOceanArt>(Root + "/Environment/OceanBackdrops.asset");
            scenery.cityCoast = LoadSprite(SurfaceBackdrops, "FloodedCityCoast").texture;
            EditorUtility.SetDirty(scenery);

            EditPrefab("Ship/Ship", root =>
            {
                var hatch = root.transform.Find("LadderHatch").GetComponent<Renderer>();
                var deck = root.transform.Find("DeckWest").GetComponent<Renderer>();
                if (hatch.sharedMaterial == deck.sharedMaterial) return false;
                hatch.sharedMaterial = deck.sharedMaterial;
                return true;
            });
            EditPrefab("Enemies/Shadow", root =>
            {
                var view = root.GetComponent<PixelEnemyView>();
                view.primary.basicAttack = EnemyFrames("Shadow", "BasicBite");
                RegisterEnemyFrames(view.primary, false, true);
                return true;
            });
            foreach (string guid in AssetDatabase.FindAssets("t:GameTuning", new[] { GameProjectBuilder.Root }))
            {
                var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(AssetDatabase.GUIDToAssetPath(guid));
                if (Mathf.Approximately(tuning.sharkHealth, 2)) tuning.sharkHealth = 4;
                if (Mathf.Approximately(tuning.whiteSharkHealth, 3)) tuning.whiteSharkHealth = 6;
                if (tuning.shadowHitsRequired == 20) tuning.shadowHitsRequired = 30;
                EditorUtility.SetDirty(tuning);
            }

            var textures = AssetDatabase.FindAssets("t:Texture2D", new[] { Root })
                .Select(AssetDatabase.GUIDToAssetPath).Where(PixelSpriteImporter.NeedsCleanAlpha).ToArray();
            AssetDatabase.StartAssetEditing();
            try { foreach (string path in textures) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
            finally { AssetDatabase.StopAssetEditing(); }
            DefaultLocalizationBuilder.Build();
            AssetDatabase.SaveAssets();
            Debug.Log($"Marine art installed and alpha reimported for {textures.Length} animations, poses and sprites.");
        }
    }
}
