using UnityEngine;

namespace Lifeline
{
    [CreateAssetMenu(menuName = "LifeLine/Art/Ocean Backdrops", fileName = "OceanBackdrops")]
    public sealed class PixelOceanArt : ScriptableObject
    {
        public Shader backdropShader;
        public Texture2D sky, duskSky, nightSky;
        public Texture2D cityCoast;
        [Range(0, .5f)] public float cityCoastHorizon = .226f;
        [Min(1)] public float cityCoastHeight = 8f;
        [Range(0, .5f)] public float deckSkyHorizon = .12f;
        public Color deckDuskTint = new Color(.68f, .45f, .59f);
        public Color deckNightTint = new Color(.24f, .30f, .48f);
        public Texture2D shallows, city, deep;
        [Tooltip("Sky panorama visible above the miniature boat in the underwater map.")]
        public Texture2D waterlineSky, waterlineDuskSky, waterlineNightSky;
        [Min(1)] public float waterlineSkyHeight = 2.5f;
        [Range(0, 1)] public float underwaterColorBlend = .85f;
        public Color underwaterShadows = new Color(.045f, .115f, .215f);
        public Color underwaterHighlights = new Color(.29f, .54f, .83f);
        public Sprite cloud, haze, foam, ripple;
        public Sprite kelp, coral, seagrass, arch, rock, bubbles;
        [Tooltip("Authored SpriteRenderer templates for the seeded room decorations.")]
        public SpriteRenderer plantVisual, distantRockVisual;
        [Min(1)] public float regionBlendMetres = 8f;
        [Min(3)] public float panoramaWidth = 55f;
        [Min(0)] public float waveSpeed = 1.15f, cloudSpeed = .27f;
        public Color duskTint = new Color(.76f, .76f, .87f);
        public Color nightTint = new Color(.40f, .53f, .73f);
        public Color stormTint = new Color(.55f, .66f, .79f);

        public void DressRoom(Transform parent, MazeLayout maze, WorldGenerator world)
        {
            int westX = Mathf.RoundToInt(maze.topLeft.x * 10);
            int northY = Mathf.RoundToInt(maze.topLeft.y * 10);
            var random = new System.Random(unchecked(world.generatedSeed ^ westX * 7919 ^ northY * 2137));
            float shallowBoundary = world.tuning != null ? world.tuning.shallowBoundary : 53f;
            float deepBoundary = world.tuning != null ? world.tuning.deepBoundary : 124f;
            for (int cell = 0; cell < maze.Count; cell++)
            {
                Vector3 center = maze.Center(cell);
                float depth = world.WaterSurface - center.y;
                int region = center.x < shallowBoundary ? 0 : center.x < deepBoundary ? 1 : 2;
                if (random.NextDouble() > (region == 0 ? .38 : region == 1 ? .29 : .16)) continue;
                int roll = random.Next(4);
                bool plant = region == 0 && depth < 37f || region == 1 && roll == 0 && depth < 30f;
                bool distantRock = region == 2;
                if (!plant && !distantRock) continue;
                Sprite art = plant ? (roll == 0 ? coral : roll == 1 ? seagrass : kelp) : (roll <= 1 ? rock : arch);
                if (art == null) continue;
                float size = maze.cellSize * (distantRock ? 1.25f : plant ? .38f : .46f);
                float offset = (float)(random.NextDouble() - .5) * maze.cellSize * .5f;
                var decoration = PixelWorldArt.PlaceSprite(parent, art.name, art,
                    new Vector3(center.x + offset, center.y, distantRock ? 7.1f : 2.2f) - parent.position,
                    new Vector2(size * (distantRock ? 1.85f : 1.45f), size), world.geometryLayer,
                    plant ? plantVisual : distantRockVisual);
                var position = decoration.transform.localPosition;
                position.y -= maze.cellSize * .5f - decoration.bounds.extents.y - .11f;
                decoration.transform.localPosition = position;
                decoration.flipX = random.Next(2) == 0;
                decoration.color = distantRock ? new Color(.39f, .56f, .74f, .27f) : new Color(.77f, .84f, .87f,
                    Mathf.Lerp(.86f, .52f, Mathf.InverseLerp(12f, 50f, depth)));
                if (plant)
                {
                    var sway = decoration.GetComponent<PixelPlantSway>();
                    if (sway == null) sway = decoration.gameObject.AddComponent<PixelPlantSway>();
                    sway.Initialize((float)random.NextDouble() * 6.28f);
                }
            }
        }
    }
}
