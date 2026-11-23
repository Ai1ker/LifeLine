#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Lifeline.Editor
{
    public static class ShipRadioArtInstaller
    {
        const string Root = GameProjectBuilder.Root;
        const string ShipPrefab = Root + "/Prefabs/Ship/Ship.prefab";
        const string SpriteFolder = Root + "/Art/Stations/StaticSprites/Radio";
        const string RecipeFolder = Root + "/Data/Recipes";

        [MenuItem("LifeLine/Art/Install Ship Radio Equipment")]
        public static void InstallFromMenu() => Debug.Log(Apply());

        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before editing the ship prefab.");

            var antenna = LoadSprite("Antenna");
            var receiver = LoadSprite("Receiver");
            var amplifier = LoadSprite("Amplifier");
            var root = PrefabUtility.LoadPrefabContents(ShipPrefab);
            try
            {
                if (root.GetComponent<ShipRadioDisplay>() == null)
                {
                    var anchors = root.GetComponentsInChildren<StationAnchor>(true);
                    var control = anchors.Single(anchor => anchor.kind == StationKind.TotalControl);
                    var helm = anchors.Single(anchor => anchor.kind == StationKind.Helm);
                    var deck = root.transform.Find("DeckWest").GetComponent<BoxCollider>();
                    float deckTop = root.transform.InverseTransformPoint(
                        deck.transform.TransformPoint(deck.center + Vector3.up * (deck.size.y * .5f))).y;
                    float midpoint = (root.transform.InverseTransformPoint(control.transform.position).x +
                        root.transform.InverseTransformPoint(helm.transform.position).x) * .5f;

                    var equipment = new GameObject("Radio Equipment") { layer = root.layer };
                    equipment.transform.SetParent(root.transform, false);
                    equipment.transform.localPosition = new Vector3(midpoint, deckTop, .38f);
                    var display = root.AddComponent<ShipRadioDisplay>();
                    display.antenna = Place(equipment.transform, "Antenna", antenna, -.53f, new Vector2(.64f, 1.38f));
                    display.receiver = Place(equipment.transform, "Receiver", receiver, .02f, new Vector2(.60f, .52f));
                    display.amplifier = Place(equipment.transform, "Amplifier", amplifier, .66f, new Vector2(.59f, .46f));
                    display.ApplyStage(0);
                    PrefabUtility.SaveAsPrefabAsset(root, ShipPrefab, out bool saved);
                    if (!saved) throw new IOException("Could not save the ship radio display.");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            int configured = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:RecipeDefinition", new[] { RecipeFolder }))
            {
                var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (recipe == null || !UsesCompletionHiding(recipe.toolId, recipe.radioStage)) continue;
                if (!recipe.hideWhenCompleted)
                {
                    recipe.hideWhenCompleted = true;
                    EditorUtility.SetDirty(recipe);
                }
                configured++;
            }
            AssetDatabase.SaveAssets();
            return $"Ship radio sprites linked. Completion hiding configured for {configured} recipes.";
        }

        internal static bool UsesCompletionHiding(string tool, int radioStage) =>
            radioStage > 0 || tool == "generator" || tool == "repair-bench";

        static Sprite LoadSprite(string name)
        {
            string path = SpriteFolder + "/" + name + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return sprite != null ? sprite : throw new InvalidOperationException("Missing radio sprite: " + path);
        }

        static SpriteRenderer Place(Transform parent, string name, Sprite sprite, float x, Vector2 size)
        {
            var renderer = PixelWorldArt.PlaceSprite(parent, name, sprite, Vector3.zero, size, parent.gameObject.layer);
            renderer.transform.localPosition = new Vector3(x, sprite.bounds.extents.y * renderer.transform.localScale.y, 0);
            return renderer;
        }
    }
}
#endif
