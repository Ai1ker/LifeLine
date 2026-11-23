using UnityEngine;

namespace Lifeline
{
    [CreateAssetMenu(menuName = "LifeLine/Art/Underwater Sprites", fileName = "UnderwaterSprites")]
    public sealed class PixelWorldArt : ScriptableObject
    {
        public Sprite wood, metal, fiber, fish, fuel;
        public Sprite eelGenerator;
        public Sprite lostCargo, cabinet, hideSpot;
        public Material ruinWall, caveWall;
        [Header("Authored visual prefabs")]
        public MeshRenderer ruinWallVisual, caveWallVisual, seabedVisual, caveBackdropVisual;
        public UnderwaterHideSpot hideSpotPrefab;
        [Tooltip("Pickup prefabs indexed by ResourceKind. The SpriteRenderer is authored inside each prefab.")]
        public LootPickup[] resourcePrefabs;
        public LootPickup cabinetPrefab, lostCargoPrefab;

        public LootPickup ResourcePrefab(ResourceKind kind)
        {
            int index = (int)kind;
            return resourcePrefabs != null && index >= 0 && index < resourcePrefabs.Length ? resourcePrefabs[index] : null;
        }

        public static SpriteRenderer PlaceSprite(Transform parent, string title, Sprite sprite, Vector3 position, Vector2 size, int layer,
            SpriteRenderer prefab = null)
        {
            SpriteRenderer renderer;
            if (prefab != null) renderer = Instantiate(prefab, parent, false);
            else
            {
                var visual = new GameObject(title);
                visual.transform.SetParent(parent, false);
                renderer = visual.AddComponent<SpriteRenderer>();
            }
            renderer.name = title;
            renderer.gameObject.layer = layer;
            renderer.transform.localPosition = position;
            Vector3 bounds = sprite.bounds.size;
            renderer.transform.localScale = Vector3.one * Mathf.Min(size.x / bounds.x, size.y / bounds.y);
            renderer.sprite = sprite;
            return renderer;
        }
    }
}
