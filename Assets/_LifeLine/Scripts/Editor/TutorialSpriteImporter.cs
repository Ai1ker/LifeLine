using UnityEditor;
using UnityEngine;

namespace Lifeline.Editor
{
    /// <summary>
    /// Ensures all runtime-loaded pixel art imports as point-filtered Sprites.
    /// Every sprite (world + UI) lives in one folder: Resources/Art, so it is easy to
    /// upload/replace as a single self-contained asset bundle.
    /// </summary>
    public sealed class TutorialSpriteImporter : AssetPostprocessor
    {
        const string ArtFolder = "Assets/_LifeLine/Resources/Art/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtFolder, System.StringComparison.OrdinalIgnoreCase)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            // Only the UI frame/button sprites need nine-slice borders.
            if (assetPath.Contains("PanelLarge")) importer.spriteBorder = new Vector4(28, 26, 28, 26);
            else if (assetPath.Contains("PanelSmall")) importer.spriteBorder = new Vector4(16, 16, 16, 16);
            else if (assetPath.Contains("ButtonWide")) importer.spriteBorder = new Vector4(24, 14, 24, 14);
            else if (assetPath.Contains("ButtonSquare")) importer.spriteBorder = new Vector4(14, 12, 14, 12);
            else importer.spriteBorder = Vector4.zero;
        }

        [MenuItem("LifeLine/Tutorial/Reimport UI Sprites")]
        public static void ReimportAll()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            Debug.Log("All pixel-art sprites reimported (Sprite / Point / no mipmaps).");
        }
    }
}
