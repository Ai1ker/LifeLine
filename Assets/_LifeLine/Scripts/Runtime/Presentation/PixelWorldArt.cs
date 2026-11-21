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

        public void ApplyPickup(LootPickup pickup)
        {
            if (pickup.GetComponent<FilterSalvage>() != null) return;
            if (pickup.isCrate && !pickup.isLostCargo) return;
            Sprite art = pickup.isLostCargo ? lostCargo : pickup.isCabinet ? cabinet : ResourceSprite(pickup.resource);
            if (art == null) return;
            Transform root = pickup.visualRoot != null ? pickup.visualRoot : pickup.transform;
            var current = root.Find("Pixel pickup");
            if (current != null)
            {
                current.GetComponent<SpriteRenderer>().sprite = art;
                return;
            }
            foreach (var mesh in root.GetComponentsInChildren<MeshRenderer>(true))
                if (mesh.GetComponent<TextMesh>() == null) mesh.enabled = false;
            if (pickup.isLostCargo)
            {
                var original = root.Find("Pixel Sprite")?.GetComponent<SpriteRenderer>();
                if (original != null) original.enabled = false;
            }
            Vector2 size = pickup.IsSwimmingFish ? new Vector2(.55f, .38f) : pickup.resource == ResourceKind.Eel ? new Vector2(1.35f, .9f) :
                pickup.isLostCargo || pickup.isCabinet ? new Vector2(.85f, .9f) : new Vector2(.58f, .53f);
            PlaceSprite(root, "Pixel pickup", art, new Vector3(0, 0, -.48f), size, pickup.gameObject.layer);
        }

        Sprite ResourceSprite(ResourceKind kind)
        {
            return kind switch
            {
                ResourceKind.Wood => wood,
                ResourceKind.Metal => metal,
                ResourceKind.Fiber => fiber,
                ResourceKind.Fish => fish,
                ResourceKind.Fuel => fuel,
                ResourceKind.Eel => eelGenerator,
                _ => null
            };
        }

        public static SpriteRenderer PlaceSprite(Transform parent, string title, Sprite sprite, Vector3 position, Vector2 size, int layer)
        {
            var visual = new GameObject(title) { layer = layer };
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = position;
            Vector3 bounds = sprite.bounds.size;
            visual.transform.localScale = Vector3.one * Mathf.Min(size.x / bounds.x, size.y / bounds.y);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            return renderer;
        }
    }
}
