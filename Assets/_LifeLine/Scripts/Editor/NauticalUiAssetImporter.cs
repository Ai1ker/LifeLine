#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lifeline.Editor
{
    public sealed class NauticalUiAssetImporter : AssetPostprocessor
    {
        const string ArtworkFolder = "Assets/_LifeLine/Art/UI/StaticSprites/Resources/LifeLineUI/Nautical";

        [MenuItem("LifeLine/UI/Reimport Nautical Artwork")]
        public static void ReimportArtwork()
        {
            if (!Directory.Exists(ArtworkFolder)) return;
            foreach (string path in Directory.GetFiles(ArtworkFolder, "*.png"))
                AssetDatabase.ImportAsset(path.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtworkFolder + "/", System.StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.spriteBorder = Vector4.one * (Path.GetFileNameWithoutExtension(assetPath) switch
            {
                "Frame" => 15,
                "Scroll" => 15,
                "Plate" or "Button" or "Title" => 8,
                _ => 0
            });
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
#endif
