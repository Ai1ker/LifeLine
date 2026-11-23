#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Lifeline.Editor
{
    internal static class UnderwaterPickupArtAuthoring
    {
        const string Root = GameProjectBuilder.Root;
        const string WorldPrefabs = Root + "/Prefabs/World";
        const string UnderwaterPrefabs = WorldPrefabs + "/UnderwaterArt";
        const string Folder = UnderwaterPrefabs + "/Pickups";
        const string SpriteNode = "Pickup Sprite";
        const string SharkBaitSpritePath = Root + "/Art/Items/StaticSprites/SharkBaitFish.png";
        const string SharkBaitPrefabPath = UnderwaterPrefabs + "/SharkBaitMarker.prefab";

        public static void Install(WorldGenerator world, PixelWorldArt art)
        {
            if (!AssetDatabase.IsValidFolder(UnderwaterPrefabs)) AssetDatabase.CreateFolder(WorldPrefabs, "UnderwaterArt");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(UnderwaterPrefabs, "Pickups");
            var basePickup = world.lootPrefab;
            if (basePickup == null) throw new InvalidOperationException("The Gameplay world needs its base Loot prefab.");

            var kinds = (ResourceKind[])Enum.GetValues(typeof(ResourceKind));
            Array.Resize(ref art.resourcePrefabs, (int)kinds[kinds.Length - 1] + 1);
            foreach (var kind in kinds)
            {
                if (art.ResourcePrefab(kind) != null) continue;
                var size = kind == ResourceKind.Fish ? new Vector2(.55f, .38f) :
                    kind == ResourceKind.Eel ? new Vector2(1.35f, .9f) : new Vector2(.58f, .53f);
                art.resourcePrefabs[(int)kind] = AuthorPrefab(basePickup, kind.ToString(), ResourceSprite(kind, art), size, kind);
            }
            if (art.cabinetPrefab == null)
                art.cabinetPrefab = AuthorPrefab(basePickup, "SalvageCabinet", art.cabinet, new Vector2(.85f, .9f), ResourceKind.Wood, cabinet: true);
            if (art.lostCargoPrefab == null)
                art.lostCargoPrefab = AuthorPrefab(basePickup, "LostCargo", art.lostCargo, new Vector2(.85f, .9f), ResourceKind.Wood, lostCargo: true);
            InstallBaitMarker(art, world.pickupLayer);

            world.pixelArt = art;
            PrefabUtility.RecordPrefabInstancePropertyModifications(world);
            EditorUtility.SetDirty(art);
            foreach (var pickup in world.GetComponentsInChildren<LootPickup>(true)) BindBakedVisual(world, pickup);
        }

        public static void InstallBaitMarker(PixelWorldArt art, int waterLayer)
        {
            if (art.sharkBaitMarkerPrefab != null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SharkBaitPrefabPath);
            if (prefab == null)
            {
                var importer = AssetImporter.GetAtPath(SharkBaitSpritePath) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing shark bait fish artwork.");
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SharkBaitSpritePath);
                if (sprite == null) throw new InvalidOperationException("Shark bait fish art could not be imported as a sprite.");
                var root = new GameObject("SharkBaitMarker") { layer = waterLayer };
                try
                {
                    var renderer = PixelWorldArt.PlaceSprite(root.transform, "Baitfish", sprite,
                        new Vector3(0, 0, -1.05f), new Vector2(.86f, .38f), waterLayer);
                    renderer.sortingOrder = 2;
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, SharkBaitPrefabPath);
                }
                finally { Object.DestroyImmediate(root); }
            }
            art.sharkBaitMarkerPrefab = prefab;
            EditorUtility.SetDirty(art);
        }

        static Sprite ResourceSprite(ResourceKind kind, PixelWorldArt art) => kind switch
        {
            ResourceKind.Wood => art.wood,
            ResourceKind.Metal => art.metal,
            ResourceKind.Fiber => art.fiber,
            ResourceKind.Fish => art.fish,
            ResourceKind.Bait => NauticalUiSkin.Sprite("Bait"),
            ResourceKind.Plank => NauticalUiSkin.Sprite("Plank"),
            ResourceKind.Plate => NauticalUiSkin.Sprite("PlateItem"),
            ResourceKind.FiberBundle => NauticalUiSkin.Sprite("Bundle"),
            ResourceKind.Fuel => art.fuel,
            ResourceKind.Eel => art.eelGenerator,
            _ => null
        };

        static LootPickup AuthorPrefab(GameObject template, string name, Sprite sprite, Vector2 size, ResourceKind kind,
            bool cabinet = false, bool lostCargo = false)
        {
            string path = Folder + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<LootPickup>();
            if (sprite == null) throw new InvalidOperationException("Missing pickup Sprite for " + name);

            var root = (GameObject)PrefabUtility.InstantiatePrefab(template);
            try
            {
                root.name = name;
                var pickup = root.GetComponent<LootPickup>();
                pickup.visualRoot = root.transform.Find("VisualRoot");
                pickup.resource = kind;
                pickup.isCabinet = cabinet;
                pickup.isLostCargo = pickup.isCrate = lostCargo;
                pickup.tintPlaceholderVisuals = false;
                if (lostCargo && root.TryGetComponent<BoxCollider>(out var box))
                {
                    box.size = new Vector3(.6f, .45f, .5f);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(box);
                }
                var renderer = PixelWorldArt.PlaceSprite(pickup.visualRoot, SpriteNode, sprite,
                    new Vector3(0, 0, -.48f), size, root.layer);
                HidePlaceholder(pickup.visualRoot, renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(pickup);
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<LootPickup>();
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void BindBakedVisual(WorldGenerator world, LootPickup pickup)
        {
            if ((pickup.isCrate && !pickup.isLostCargo) || pickup.GetComponent<FilterSalvage>() != null) return;
            if (PickupSprite(pickup.transform) != null) return;
            var prefab = world.PickupPrefabFor(pickup.resource, pickup.isCrate, pickup.isCabinet, pickup.isLostCargo);
            if (prefab == null) return;
            var source = PickupSprite(prefab.transform);
            var visuals = pickup.visualRoot != null ? pickup.visualRoot : pickup.transform.Find("VisualRoot");
            if (source == null || visuals == null) return;
            var renderer = Object.Instantiate(source.gameObject, visuals, false).GetComponent<SpriteRenderer>();
            renderer.name = SpriteNode;
            renderer.gameObject.layer = pickup.gameObject.layer;
            HidePlaceholder(visuals, renderer);
            pickup.tintPlaceholderVisuals = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(pickup);
        }

        static SpriteRenderer PickupSprite(Transform root) => root.Find("VisualRoot/" + SpriteNode)?.GetComponent<SpriteRenderer>();

        static void HidePlaceholder(Transform visuals, Renderer authored)
        {
            foreach (var renderer in visuals.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == authored || renderer.GetComponent<TextMesh>() != null) continue;
                renderer.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }
    }
}
#endif
