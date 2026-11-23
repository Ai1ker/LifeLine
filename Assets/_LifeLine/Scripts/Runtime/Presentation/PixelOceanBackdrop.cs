using UnityEngine;

namespace Lifeline
{
    /// <summary>Animates the authored surface and underwater backdrop renderers.</summary>
    public sealed class PixelOceanBackdrop : MonoBehaviour
    {
        public Transform surfaceRoot, waterRoot;
        public MeshRenderer surface, underwater;
        public SpriteRenderer[] clouds, ripples, bubbles;
        GameSession game;
        PixelOceanArt art;
        MaterialPropertyBlock surfaceProperties, waterProperties;
        Vector3[] bubbleOrigins;
        Texture surfaceDaySky, waterlineDaySky;
        Vector2 lastTintWeights = new Vector2(-1, -1);
        Color tint = Color.white, deckTint = Color.white;
        float visualTime;

        void Start()
        {
            game = GetComponent<GameSession>();
            if (game == null || game.world == null || game.ship == null || surface == null || underwater == null) return;
            art = game.world.oceanArt;
            if (art == null || surface.sharedMaterial == null || underwater.sharedMaterial == null) return;

            var partition = game.GetComponent<GameMapPartition>();
            partition?.MoveGameplayObject(surfaceRoot.gameObject, true);
            partition?.MoveGameplayObject(waterRoot.gameObject, false);
            visualTime = game.elapsed;
            surfaceDaySky = surface.sharedMaterial.GetTexture("_Sky");
            waterlineDaySky = underwater.sharedMaterial.GetTexture("_WaterlineSky");
            surfaceProperties = new MaterialPropertyBlock();
            waterProperties = new MaterialPropertyBlock();
            surface.GetPropertyBlock(surfaceProperties);
            underwater.GetPropertyBlock(waterProperties);
            waterProperties.SetVector("_Regions", new Vector4(game.tuning.shallowBoundary, game.tuning.deepBoundary,
                art.regionBlendMetres, game.world.WaterSurface - game.world.worldBounds.yMin));
            waterProperties.SetFloat("_HorizonY", game.world.WaterSurface);
            bubbleOrigins = new Vector3[bubbles.Length];
            for (int i = 0; i < bubbles.Length; i++) bubbleOrigins[i] = bubbles[i].transform.position;
            LateUpdate();
        }

        void LateUpdate()
        {
            if (surfaceProperties == null || game == null || game.ship == null || game.world == null) return;
            // A guest animates the backdrop on its render clock between voyage snapshots.
            if (game.phase == SessionPhase.Playing) visualTime += Time.deltaTime;
            float time = visualTime;
            Vector3 ship = game.ship.Position;
            float horizon = ship.y + game.world.WaterSurface;
            surfaceProperties.SetFloat("_HorizonY", horizon);
            float border = Mathf.Max(1f, art.regionBlendMetres);
            float coast = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(game.tuning.shallowBoundary - border, game.tuning.shallowBoundary + border, ship.x))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(game.tuning.deepBoundary - border, game.tuning.deepBoundary + border, ship.x)));
            var motion = new Vector4(time, ship.x, coast, game.StormActive ? 1 : 0);
            surfaceProperties.SetVector("_Motion", motion);
            waterProperties.SetVector("_Motion", motion);
            RefreshLighting();
            surface.SetPropertyBlock(surfaceProperties);
            underwater.SetPropertyBlock(waterProperties);

            for (int i = 0; i < clouds.Length; i++)
            {
                float x = ship.x + Mathf.Repeat(i * 12f - ship.x * .2f + time * art.cloudSpeed, 144f) - 72f;
                clouds[i].transform.position = new Vector3(x, horizon + 6.3f + i % 3 * 2.35f, 7.9f);
                clouds[i].color = new Color(deckTint.r, deckTint.g, deckTint.b, .80f);
            }
            for (int i = 0; i < ripples.Length; i++)
            {
                float x = ship.x + Mathf.Repeat(i * 6.1f - ship.x * .68f - time * art.waveSpeed, 73.2f) - 36.6f;
                float y = horizon - i % 3 * 1.4f + Mathf.Sin(time * 1.8f + i) * .10f;
                ripples[i].transform.position = new Vector3(x, y, 7.5f);
                ripples[i].color = new Color(deckTint.r, deckTint.g, deckTint.b, i % 3 == 0 ? .77f : .33f);
            }
            for (int i = 0; i < bubbles.Length; i++)
            {
                Vector3 origin = bubbleOrigins[i];
                float rise = Mathf.Repeat(time * (.4f + i % 4 * .06f) + i, 6f);
                bubbles[i].transform.position = origin + new Vector3(Mathf.Sin(time * .65f + i) * .65f, rise, 0);
                bubbles[i].color = new Color(tint.r, tint.g, tint.b, Mathf.Sin(Mathf.PI * rise / 6f) * .33f);
            }
        }

        void RefreshLighting()
        {
            bool night = game.IsNight, dusk = game.IsDusk, storm = game.StormActive;
            var weights = new Vector2(night ? 1 : dusk ? .5f : 0, storm ? 1 : 0);
            if (lastTintWeights == weights) return;
            lastTintWeights = weights;

            Color stormShade = storm ? art.stormTint : Color.white;
            tint = storm ? art.stormTint : night ? art.nightTint : dusk ? art.duskTint : Color.white;
            deckTint = (night ? art.deckNightTint : dusk ? art.deckDuskTint : Color.white) * stormShade;
            Texture deckPhaseSky = night ? art.nightSky : dusk ? art.duskSky : null;
            Texture surfaceSky = deckPhaseSky != null ? deckPhaseSky : surfaceDaySky;
            if (surfaceSky != null) surfaceProperties.SetTexture("_Sky", surfaceSky);
            surfaceProperties.SetColor("_SkyTint", deckPhaseSky != null ? stormShade : tint);
            surfaceProperties.SetColor("_SceneTint", deckTint);

            Texture waterlinePhaseSky = night ? art.waterlineNightSky : dusk ? art.waterlineDuskSky : null;
            Texture sky = waterlinePhaseSky != null ? waterlinePhaseSky : waterlineDaySky;
            float height = Mathf.Max(1f, art.waterlineSkyHeight);
            float aspect = sky != null ? (float)sky.width / sky.height : 1f;
            if (sky != null) waterProperties.SetTexture("_WaterlineSky", sky);
            waterProperties.SetVector("_WaterlineSkyMapping", new Vector4(height * aspect, height, art.cloudSpeed, sky != null ? 1 : 0));
            waterProperties.SetColor("_SkyTint", waterlinePhaseSky != null ? stormShade : tint);
            waterProperties.SetColor("_SceneTint", tint);
        }
    }
}
