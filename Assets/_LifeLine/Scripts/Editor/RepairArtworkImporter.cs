#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lifeline.Editor
{
    public sealed class RepairArtworkImporter : AssetPostprocessor
    {
        public const string Folder = GameProjectBuilder.Root + "/Art/UI/StaticSprites/Repair";

        public static void Reimport()
        {
            if (!Directory.Exists(Folder)) return;
            foreach (string path in Directory.GetFiles(Folder, "*.png", SearchOption.AllDirectories))
            {
                string asset = path.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(asset) as TextureImporter;
                if (importer != null && AssetDatabase.LoadAssetAtPath<Sprite>(asset) == null)
                    AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder + "/", System.StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            if (Path.GetFileNameWithoutExtension(assetPath) == "ServicePanel")
                importer.spriteBorder = Vector4.one * 10;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
        }
    }
}
#endif
