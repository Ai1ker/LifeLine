using UnityEngine;

namespace Lifeline
{
    /// <summary>
    /// Runtime pixel-art skin: attaches a camera-facing SpriteRenderer under a visual root,
    /// optionally hiding the placeholder primitives. Sprites live in Resources/Art so no
    /// scene/prefab edits are needed and generator rebuilds cannot overwrite them.
    /// Purely cosmetic - never touches colliders, physics or gameplay state.
    /// </summary>
    public static class PixelArtDecor
    {
        public const string ArtFolder = "Art/";

        // IMPORTANT: never share ONE Material across all SpriteRenderers. SpriteRenderer writes
        // the sprite texture into its material; when all renderers share a single material the
        // last one rendered overwrites the texture for everyone (captains all look like divers,
        // cleaning a station turns it into the crew portrait, spawning a bird turns everything
        // into a bird). Each renderer must own its own material instance.
        const string OwnMaterialName = "PixelArtDecor_Own";
        static Material CreateUnlitMaterial()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            var mat = new Material(shader);
            mat.name = OwnMaterialName; // marker: this material belongs to ONE renderer only
            return mat;
        }

        static Sprite Load(string name)
        {
            var s = Resources.Load<Sprite>(ArtFolder + name);
            if (s == null) Debug.LogWarning("[PixelArtDecor] Missing sprite: " + ArtFolder + name);
            return s;
        }

        static Sprite greyBoxSprite;
        /// <summary>
        /// Fallback 1x1 grey sprite built at runtime (no resource file needed). Used when the
        /// optional pixel-art PNGs are absent, so ruins remain visible as grey boxes instead of
        /// disappearing entirely.
        /// </summary>
        public static Sprite GreyBox()
        {
            if (greyBoxSprite != null) return greyBoxSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.name = "PixelArtDecor_GreyBox";
            tex.SetPixel(0, 0, new Color(.32f, .32f, .36f, 1f));
            tex.Apply();
            greyBoxSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
            greyBoxSprite.name = "PixelArtDecor_GreyBox";
            return greyBoxSprite;
        }

        /// <summary>
        /// Attach a pixel sprite under <paramref name="visualRoot"/>. Idempotent: re-applying the
        /// same sprite name is a no-op. The sprite is placed slightly toward the camera (-Z).
        /// </summary>
        public static SpriteRenderer Apply(Transform visualRoot, string spriteName, float worldWidth,
            bool hidePlaceholders = true, Vector3? localOffset = null, int sortingOrder = 2)
        {
            // Purely cosmetic: skip in EditMode. Resources.Load + SpriteRenderer on a
            // non-playing test fixture yields fake-null component errors, and tests
            // never need the art. PlayMode (Application.isPlaying) still applies it.
            if (!Application.isPlaying) return null;
            if (visualRoot == null) return null;
            var sprite = Load(spriteName);
            if (sprite == null) return null;

            Transform existing = null;
            for (int i = 0; i < visualRoot.childCount; i++)
            {
                var child = visualRoot.GetChild(i);
                if (child.name == "PixelArtDecor") { existing = child; break; }
            }
            SpriteRenderer sr = existing != null ? existing.GetComponent<SpriteRenderer>() : null;
            if (sr != null && sr.sprite == sprite) return sr; // already applied
            if (existing == null)
            {
                var go = new GameObject("PixelArtDecor");
                go.transform.SetParent(visualRoot, false);
                existing = go.transform;
            }
            if (sr == null) sr = existing.gameObject.AddComponent<SpriteRenderer>();
            if (sr == null) return null; // defensive: component could not be created
            sr.sprite = sprite;
            // Force an unlit sprite material. The project's default Sprite-Lit-Default needs a 2D
            // light and can render black/invisible; Sprites/Default always shows the pixel colors.
            // Each renderer must get its OWN material instance. The default Sprite material is
            // shared by Unity and causes cross-sprite texture bleed.
            if (sr.sharedMaterial == null || sr.sharedMaterial.name != OwnMaterialName)
            {
                var mat = CreateUnlitMaterial();
                if (mat != null) sr.sharedMaterial = mat;
            }
            sr.sortingOrder = sortingOrder; // default 2 = above placeholder primitives; hull passes a negative value as background
            var ppu = sprite.pixelsPerUnit > 0 ? sprite.pixelsPerUnit : 100f;
            float scale = worldWidth * ppu / Mathf.Max(1f, sprite.rect.width);
            existing.localScale = Vector3.one * scale;
            existing.localPosition = localOffset ?? new Vector3(0, 0, -0.35f);
            existing.localRotation = Quaternion.identity;

            if (hidePlaceholders)
            {
                // Hide the original primitive meshes but keep this decorator visible.
                foreach (Renderer r in visualRoot.GetComponentsInChildren<Renderer>(true))
                    if (r.gameObject != existing.gameObject) r.enabled = false;
            }
            return sr;
        }

        public static string CrewSprite(CrewRole role) => role == CrewRole.Diver ? "Actor_Diver" : "Actor_Captain";

        public static string EnemySprite(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Bird:
                case EnemyKind.ArmoredBird: return "Enemy_Bird";
                case EnemyKind.WhiteShark: return "Enemy_WhiteShark";
                case EnemyKind.Piranha: return "Enemy_Piranha";
                case EnemyKind.Shadow: return "Enemy_Shadow";
                default: return "Enemy_Shark";
            }
        }

        // _v2 suffix forces Unity to reimport from the corrected PNGs (old cached artifacts
        // held an early, wrongly-cropped version that showed a captain instead of the device).
        public static string StationSprite(StationKind kind) => "Station_" + kind + "_v2";
    }
}
