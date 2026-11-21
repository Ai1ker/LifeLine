using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    /// <summary>Two art-only planes, one per map. All work is independent of camera layout and daily map regeneration.</summary>
    public sealed class PixelOceanBackdrop : MonoBehaviour
    {
        GameSession game;
        PixelOceanArt art;
        Material surface, underwater;
        Mesh plane;
        Transform surfaceRoot, waterRoot;
        readonly List<SpriteRenderer> clouds = new(), ripples = new(), bubbles = new();
        readonly List<Vector2> bubbleOrigins = new();
        Vector2 lastTintWeights = new Vector2(-1, -1);
        Color tint = Color.white, deckTint = Color.white;
        float visualTime;

        void Start()
        {
            game = GetComponent<GameSession>();
            if (game == null || game.world == null || game.ship == null) return;
            art = game.world.oceanArt;
            if (art == null || art.backdropShader == null) return;
            visualTime = game.elapsed;
            Rect bounds = game.world.worldBounds;
            plane = new Mesh { name = "Ocean backdrop quad" };
            plane.vertices = new[] { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(-.5f, .5f), new Vector3(.5f, .5f) };
            plane.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            plane.RecalculateBounds();
            surface = new Material(art.backdropShader) { name = "Surface backdrop (runtime)" };
            underwater = new Material(art.backdropShader) { name = "Underwater backdrop (runtime)" };
            surface.SetFloat("_IsSurface", 1);
            surface.SetFloat("_DeckSkyHorizon", art.deckSkyHorizon);
            var coast = new Vector4(art.cityCoastHorizon, art.cityCoastHeight, art.cityCoast != null ? 1 : 0, 0);
            foreach (Material view in new[] { surface, underwater })
            {
                view.SetTexture("_CityCoast", art.cityCoast);
                view.SetVector("_CityCoastMapping", coast);
            }
            underwater.SetTexture("_Shallows", art.shallows);
            underwater.SetTexture("_City", art.city);
            underwater.SetTexture("_Deep", art.deep);
            underwater.SetFloat("_UnderwaterColorBlend", art.underwaterColorBlend);
            underwater.SetColor("_UnderwaterShadows", art.underwaterShadows);
            underwater.SetColor("_UnderwaterHighlights", art.underwaterHighlights);
            foreach (Material material in new[] { surface, underwater })
                material.SetFloat("_PanoramaWidth", art.panoramaWidth);
            underwater.SetVector("_Regions", new Vector4(game.tuning.shallowBoundary, game.tuning.deepBoundary,
                art.regionBlendMetres, game.world.WaterSurface - bounds.yMin));
            underwater.SetFloat("_HorizonY", game.world.WaterSurface);
            var rig = game.GetComponent<SessionAuthority>()?.cameraRig;
            if (rig == null) rig = FindFirstObjectByType<GameCameraRig>();
            int surfaceLayer = rig != null ? rig.shipPresentationLayer : 12;
            int waterLayer = rig != null ? rig.underwaterProxyLayer : 14;
            surfaceRoot = CreatePlane("Pixel ocean - sky and surface", surface, new Vector3(bounds.center.x, game.ship.Position.y, 9f), new Vector2(bounds.width + 90f, 64f), surfaceLayer, true);
            waterRoot = CreatePlane("Pixel ocean - depth layers", underwater, new Vector3(bounds.center.x, bounds.center.y, 9f), new Vector2(bounds.width + 90f, bounds.height + 35f), waterLayer, false);
            for (int i = 0; i < 12; i++)
            {
                AddSprite(clouds, surfaceRoot, i % 2 == 0 ? art.cloud : art.haze, new Vector2(8.5f, 2.4f));
                AddSprite(ripples, surfaceRoot, i % 2 == 0 ? art.foam : art.ripple, new Vector2(8.2f, .34f));
            }
            for (int i = 0; i < 26; i++)
            {
                if (art.bubbles == null) break;
                AddSprite(bubbles, waterRoot, art.bubbles, new Vector2(.88f, 1.25f));
                bubbleOrigins.Add(new Vector2(bounds.xMin + (i + .4f) * bounds.width / 26f, 7 + (i * 13 % 41)));
            }
            LateUpdate();
        }

        Transform CreatePlane(string name, Material material, Vector3 position, Vector2 size, int layer, bool aboard)
        {
            var root = new GameObject(name) { layer = layer };
            root.transform.position = position;
            game.GetComponent<GameMapPartition>()?.MoveGameplayObject(root, aboard);
            var visual = new GameObject("Backdrop texture plane") { layer = layer };
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = new Vector3(size.x, size.y, 1);
            visual.AddComponent<MeshFilter>().sharedMesh = plane;
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root.transform;
        }

        static void AddSprite(List<SpriteRenderer> group, Transform parent, Sprite sprite, Vector2 size)
        {
            if (sprite != null) group.Add(PixelWorldArt.PlaceSprite(parent, sprite.name, sprite, Vector3.zero, size, parent.gameObject.layer));
        }

        void LateUpdate()
        {
            if (surface == null || game == null || game.ship == null || game.world == null) return;
            // In a guest view the voyage clock arrives in snapshots. Scenery advances on
            // the local render clock so clouds, foam and bubbles do not jump between packets.
            if (game.phase == SessionPhase.Playing) visualTime += Time.deltaTime;
            float t = visualTime;
            Vector3 ship = game.ship.Position;
            float horizon = ship.y + game.world.WaterSurface;
            surface.SetFloat("_HorizonY", horizon);
            float border = Mathf.Max(1f, art.regionBlendMetres);
            float coast = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(game.tuning.shallowBoundary - border, game.tuning.shallowBoundary + border, ship.x))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(game.tuning.deepBoundary - border, game.tuning.deepBoundary + border, ship.x)));
            float storm = game.StormActive ? 1f : 0f;
            surface.SetVector("_Motion", new Vector4(t, ship.x, coast, storm));
            underwater.SetVector("_Motion", new Vector4(t, ship.x, coast, storm));
            RefreshLighting();
            for (int i = 0; i < clouds.Count; i++)
            {
                float x = ship.x + Mathf.Repeat(i * 12f - ship.x * .2f + t * art.cloudSpeed, 144f) - 72f;
                clouds[i].transform.position = new Vector3(x, horizon + 6.3f + i % 3 * 2.35f, 7.9f);
                clouds[i].color = new Color(deckTint.r, deckTint.g, deckTint.b, .80f);
            }
            for (int i = 0; i < ripples.Count; i++)
            {
                float x = ship.x + Mathf.Repeat(i * 6.1f - ship.x * .68f - t * art.waveSpeed, 73.2f) - 36.6f;
                float y = horizon - i % 3 * 1.4f + Mathf.Sin(t * 1.8f + i) * .10f;
                ripples[i].transform.position = new Vector3(x, y, 7.5f);
                ripples[i].color = new Color(deckTint.r, deckTint.g, deckTint.b, i % 3 == 0 ? .77f : .33f);
            }
            for (int i = 0; i < bubbles.Count; i++)
            {
                Vector2 origin = bubbleOrigins[i];
                float rise = Mathf.Repeat(t * (.4f + i % 4 * .06f) + i, 6f);
                bubbles[i].transform.position = new Vector3(origin.x + Mathf.Sin(t * .65f + i) * .65f,
                    game.world.WaterSurface - origin.y + rise, 6.3f);
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
            Texture2D deckPhaseSky = night ? art.nightSky : dusk ? art.duskSky : null;
            surface.SetTexture("_Sky", deckPhaseSky != null ? deckPhaseSky : art.sky);
            surface.SetColor("_SkyTint", deckPhaseSky != null ? stormShade : tint);
            surface.SetColor("_SceneTint", deckTint);

            Texture2D waterlinePhaseSky = night ? art.waterlineNightSky : dusk ? art.waterlineDuskSky : null;
            Texture2D waterlineSky = waterlinePhaseSky != null ? waterlinePhaseSky : art.waterlineSky;
            surface.SetTexture("_WaterlineSky", waterlineSky);
            float height = Mathf.Max(1f, art.waterlineSkyHeight);
            float aspect = waterlineSky != null ? (float)waterlineSky.width / waterlineSky.height : 1f;
            underwater.SetTexture("_WaterlineSky", waterlineSky);
            underwater.SetVector("_WaterlineSkyMapping", new Vector4(height * aspect, height, art.cloudSpeed, waterlineSky != null ? 1 : 0));
            underwater.SetColor("_SkyTint", waterlinePhaseSky != null ? stormShade : tint);
            underwater.SetColor("_SceneTint", tint);
        }

        void OnDestroy()
        {
            Dispose(surfaceRoot != null ? surfaceRoot.gameObject : null);
            Dispose(waterRoot != null ? waterRoot.gameObject : null);
            Dispose(plane); Dispose(surface); Dispose(underwater);
        }

        static void Dispose(Object asset)
        {
            if (asset != null) Destroy(asset);
        }
    }
}
