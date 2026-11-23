#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Lifeline.Editor
{
    /// <summary>Writes editable marine art into the project while retaining the authored map and collision layout.</summary>
    public static class MarineSceneArtInstaller
    {
        const string Root = GameProjectBuilder.Root;
        const string ShipPath = Root + "/Prefabs/Ship/Ship.prefab";
        const string RoomPath = Root + "/Prefabs/UI/OnlineRoomPanel.prefab";
        const string WorldArtPath = Root + "/Art/Environment/UnderwaterSprites.asset";
        const string OceanArtPath = Root + "/Art/Environment/OceanBackdrops.asset";
        const string Templates = Root + "/Prefabs/World/UnderwaterArt";
        const string Materials = Root + "/Art/Environment/StaticSprites/Materials";
        const float CabinFloorFromTop = 282f / 367f;
        static readonly string[] DeckNodes = { "DeckWest", "DeckEast", "LadderHatch" };

        [MenuItem("LifeLine/Art/Save Editable Marine Art")]
        public static void InstallFromMenu() => Debug.Log(Apply());

        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before editing marine assets.");
            var worldArt = AssetDatabase.LoadAssetAtPath<PixelWorldArt>(WorldArtPath);
            var oceanArt = AssetDatabase.LoadAssetAtPath<PixelOceanArt>(OceanArtPath);
            if (worldArt == null || oceanArt == null) throw new InvalidOperationException("Import the existing ocean and underwater artwork first.");
            if (worldArt.ruinWall == null || worldArt.caveWall == null || worldArt.hideSpot == null || oceanArt.backdropShader == null)
                throw new InvalidOperationException("The underwater artwork needs both wall materials, the locker sprite and the ocean shader.");
            string backup = Root + "/Documentation/SceneBackups/MarineArt-" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_ffff");
            Directory.CreateDirectory(backup);
            foreach (string path in new[] { ShipPath, RoomPath, WorldArtPath, OceanArtPath, GameProjectBuilder.PlayScene, GameProjectBuilder.MenuScene })
                if (File.Exists(path)) File.Copy(path, backup + "/" + Path.GetFileName(path) + ".bak");

            EditPrefab(ShipPath, AlignDeckArt);
            EditPrefab(RoomPath, root =>
            {
                NauticalMenuAuthoring.CenterRoomLabels(root.GetComponent<OnlineRoomPanel>());
                return true;
            });
            InstallTerrainPrefabs(worldArt, oceanArt);
            EditScene<GameSession>(GameProjectBuilder.PlayScene, backup, game =>
            {
                game.world.pixelArt = worldArt;
                game.world.oceanArt = oceanArt;
                InstallBackdropObjects(game, oceanArt);
                BindBakedPreviewMaterials(game.world, worldArt);
                var hud = FindInScene<GameHUDController>(game.gameObject.scene);
                if (hud != null && hud.messageText != null)
                {
                    hud.messageText.color = Color.white;
                    if (!hud.messageText.TryGetComponent(out Outline outline))
                        outline = hud.messageText.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color32(8, 19, 30, 248);
                    outline.effectDistance = new Vector2(1.5f, -1.5f);
                }
            });
            EditScene<MainMenuController>(GameProjectBuilder.MenuScene, backup, NauticalMenuAuthoring.Apply);
            AssetDatabase.SaveAssets();
            return backup;
        }

        public static bool AlignDeckArt(GameObject ship)
        {
            var hull = ship.transform.Find("VisualRoot/Pixel Hull")?.GetComponent<SpriteRenderer>();
            if (hull == null || hull.sprite == null) return false;
            var deck = ship.transform.Find("DeckWest").GetComponent<BoxCollider>();
            var floor = ship.transform.Find("CabinFloor").GetComponent<BoxCollider>();
            var artSpace = hull.transform.parent;
            float deckY = TopInSpace(deck, artSpace);
            float cabinY = TopInSpace(floor, artSpace);
            Bounds bounds = hull.sprite.bounds;
            var scale = hull.transform.localScale;
            scale.y = (deckY - cabinY) / (bounds.size.y * CabinFloorFromTop);
            var position = hull.transform.localPosition;
            position.y = deckY - bounds.max.y * scale.y;
            bool changed = hull.transform.localScale != scale || hull.transform.localPosition != position;
            hull.transform.localScale = scale;
            hull.transform.localPosition = position;
            foreach (string node in DeckNodes)
            {
                var part = ship.transform.Find(node);
                var renderer = part.GetComponent<MeshRenderer>();
                var mesh = part.GetComponent<MeshFilter>();
                if (renderer != null) { Object.DestroyImmediate(renderer); changed = true; }
                if (mesh != null) { Object.DestroyImmediate(mesh); changed = true; }
            }
            return changed;
        }

        static float TopInSpace(BoxCollider collider, Transform space)
        {
            var top = collider.center + Vector3.up * (collider.size.y * .5f);
            return space.InverseTransformPoint(collider.transform.TransformPoint(top)).y;
        }

        static void InstallTerrainPrefabs(PixelWorldArt world, PixelOceanArt ocean)
        {
            var bed = MaterialAsset("Seabed", () => TintedTile(world.caveWall, new Color(.48f, .59f, .76f)));
            var backing = MaterialAsset("CaveBackdrop", () => TintedTile(world.caveWall, new Color(.20f, .28f, .42f)));
            if (world.ruinWallVisual == null) world.ruinWallVisual = SurfacePrefab("RuinWall", world.ruinWall, PrimitiveType.Cube);
            if (world.caveWallVisual == null) world.caveWallVisual = SurfacePrefab("CaveWall", world.caveWall, PrimitiveType.Cube);
            if (world.seabedVisual == null) world.seabedVisual = SurfacePrefab("Seabed", bed, PrimitiveType.Cube);
            if (world.caveBackdropVisual == null) world.caveBackdropVisual = SurfacePrefab("CaveBackdrop", backing, PrimitiveType.Cube);
            if (world.hideSpotPrefab == null) world.hideSpotPrefab = Prefab("HideLocker", () =>
            {
                var root = new GameObject("HideLocker");
                var hide = root.AddComponent<UnderwaterHideSpot>();
                hide.visualRoot = new GameObject("VisualRoot").transform;
                hide.visualRoot.SetParent(root.transform, false);
                PixelWorldArt.PlaceSprite(hide.visualRoot, "Locker sprite", world.hideSpot,
                    new Vector3(0, -.12f, -.5f), new Vector2(.8f, 1.35f), 11);
                hide.label = new GameObject("Hide label").AddComponent<TextMesh>();
                hide.label.transform.SetParent(root.transform, false);
                hide.label.transform.localPosition = new Vector3(0, .7f, -1.5f);
                hide.label.anchor = TextAnchor.MiddleCenter;
                hide.label.fontSize = 40;
                hide.label.characterSize = .08f;
                hide.label.gameObject.SetActive(false);
                return root;
            }).GetComponent<UnderwaterHideSpot>();
            if (ocean.plantVisual == null) ocean.plantVisual = SpritePrefab("RoomPlant", ocean.kelp, true);
            if (ocean.distantRockVisual == null) ocean.distantRockVisual = SpritePrefab("DistantRock", ocean.rock, false);
            EditorUtility.SetDirty(world);
            EditorUtility.SetDirty(ocean);
        }

        public static void InstallBackdropObjects(GameSession game, PixelOceanArt art)
        {
            var view = game.GetComponent<PixelOceanBackdrop>();
            if (view == null) view = game.gameObject.AddComponent<PixelOceanBackdrop>();
            if (view.surface != null && view.underwater != null) return;
            var rig = FindInScene<GameCameraRig>(game.gameObject.scene);
            int surfaceLayer = rig != null ? rig.shipPresentationLayer : 12;
            int waterLayer = rig != null ? rig.underwaterProxyLayer : 14;
            var world = game.world;
            Rect bounds = world.worldBounds;
            Material surface = MaterialAsset("SurfacePanorama", () => PanoramaMaterial(game, art, true));
            Material underwater = MaterialAsset("UnderwaterPanorama", () => PanoramaMaterial(game, art, false));
            view.surfaceRoot = new GameObject("Surface Panorama").transform;
            view.surfaceRoot.position = new Vector3(bounds.center.x, game.ship.Position.y, 9);
            view.surfaceRoot.gameObject.layer = surfaceLayer;
            view.waterRoot = new GameObject("Underwater Panorama").transform;
            view.waterRoot.position = new Vector3(bounds.center.x, bounds.center.y, 9);
            view.waterRoot.gameObject.layer = waterLayer;
            view.surface = PanoramaPlane(view.surfaceRoot, surface, new Vector2(bounds.width + 90, 64), surfaceLayer);
            view.underwater = PanoramaPlane(view.waterRoot, underwater, new Vector2(bounds.width + 90, bounds.height + 35), waterLayer);

            float horizon = game.ship.Position.y + world.WaterSurface;
            var clouds = new List<SpriteRenderer>();
            var ripples = new List<SpriteRenderer>();
            var bubbles = new List<SpriteRenderer>();
            for (int i = 0; i < 12; i++)
            {
                AddScenery(clouds, view.surfaceRoot, i % 2 == 0 ? art.cloud : art.haze, new Vector3(i * 12 - 72, horizon + 6.3f + i % 3 * 2.35f, 7.9f), new Vector2(8.5f, 2.4f));
                AddScenery(ripples, view.surfaceRoot, i % 2 == 0 ? art.foam : art.ripple, new Vector3(i * 6.1f - 36.6f, horizon - i % 3 * 1.4f, 7.5f), new Vector2(8.2f, .34f));
            }
            for (int i = 0; i < 26; i++)
                AddScenery(bubbles, view.waterRoot, art.bubbles,
                    new Vector3(bounds.xMin + (i + .4f) * bounds.width / 26f, world.WaterSurface - 7 - i * 13 % 41, 6.3f), new Vector2(.88f, 1.25f));
            view.clouds = clouds.ToArray();
            view.ripples = ripples.ToArray();
            view.bubbles = bubbles.ToArray();
        }

        static void AddScenery(List<SpriteRenderer> group, Transform parent, Sprite sprite, Vector3 position, Vector2 size)
        {
            if (sprite != null)
                group.Add(PixelWorldArt.PlaceSprite(parent, sprite.name, sprite, parent.InverseTransformPoint(position), size, parent.gameObject.layer));
        }

        static Material PanoramaMaterial(GameSession game, PixelOceanArt art, bool surface)
        {
            var material = new Material(art.backdropShader);
            material.SetFloat("_IsSurface", surface ? 1 : 0);
            material.SetFloat("_HorizonY", surface ? game.ship.Position.y + game.world.WaterSurface : game.world.WaterSurface);
            material.SetFloat("_DeckSkyHorizon", art.deckSkyHorizon);
            material.SetFloat("_PanoramaWidth", art.panoramaWidth);
            material.SetTexture("_Sky", art.sky);
            material.SetTexture("_WaterlineSky", art.waterlineSky);
            material.SetTexture("_CityCoast", art.cityCoast);
            material.SetVector("_CityCoastMapping", new Vector4(art.cityCoastHorizon, art.cityCoastHeight, art.cityCoast != null ? 1 : 0, 0));
            material.SetTexture("_Shallows", art.shallows);
            material.SetTexture("_City", art.city);
            material.SetTexture("_Deep", art.deep);
            material.SetFloat("_UnderwaterColorBlend", art.underwaterColorBlend);
            material.SetColor("_UnderwaterShadows", art.underwaterShadows);
            material.SetColor("_UnderwaterHighlights", art.underwaterHighlights);
            material.SetVector("_Regions", new Vector4(game.tuning.shallowBoundary, game.tuning.deepBoundary,
                art.regionBlendMetres, game.world.WaterSurface - game.world.worldBounds.yMin));
            float aspect = art.waterlineSky != null ? (float)art.waterlineSky.width / art.waterlineSky.height : 1;
            material.SetVector("_WaterlineSkyMapping", new Vector4(art.waterlineSkyHeight * aspect, art.waterlineSkyHeight,
                art.cloudSpeed, art.waterlineSky != null ? 1 : 0));
            return material;
        }

        static MeshRenderer PanoramaPlane(Transform parent, Material material, Vector2 size, int layer)
        {
            var renderer = SurfacePrefab("PanoramaQuad", material, PrimitiveType.Quad);
            var plane = (GameObject)PrefabUtility.InstantiatePrefab(renderer.gameObject, parent);
            plane.name = "Backdrop texture plane";
            plane.layer = layer;
            plane.transform.localScale = new Vector3(size.x, size.y, 1);
            var target = plane.GetComponent<MeshRenderer>();
            target.sharedMaterial = material;
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            return target;
        }

        static MeshRenderer SurfacePrefab(string name, Material material, PrimitiveType primitive)
        {
            return Prefab(name, () =>
            {
                var root = GameObject.CreatePrimitive(primitive);
                root.name = name;
                Object.DestroyImmediate(root.GetComponent<Collider>());
                var renderer = root.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                return root;
            }).GetComponent<MeshRenderer>();
        }

        static SpriteRenderer SpritePrefab(string name, Sprite sprite, bool sway)
        {
            return Prefab(name, () =>
            {
                var root = new GameObject(name);
                root.AddComponent<SpriteRenderer>().sprite = sprite;
                if (sway) root.AddComponent<PixelPlantSway>();
                return root;
            }).GetComponent<SpriteRenderer>();
        }

        static Material TintedTile(Material source, Color color)
        {
            var material = new Material(source);
            material.SetColor("_Tint", color);
            return material;
        }

        static Material MaterialAsset(string name, Func<Material> create)
        {
            if (!AssetDatabase.IsValidFolder(Materials)) AssetDatabase.CreateFolder(Root + "/Art/Environment/StaticSprites", "Materials");
            string path = Materials + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = create();
            material.name = name;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static GameObject Prefab(string name, Func<GameObject> create)
        {
            if (!AssetDatabase.IsValidFolder(Templates)) AssetDatabase.CreateFolder(Root + "/Prefabs/World", "UnderwaterArt");
            string path = Templates + "/" + name + ".prefab";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset != null) return asset;
            var root = create();
            try { return PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { Object.DestroyImmediate(root); }
        }

        static void BindBakedPreviewMaterials(WorldGenerator world, PixelWorldArt art)
        {
            if (world.solidRoot == null) return;
            foreach (var collider in world.solidRoot.GetComponentsInChildren<BoxCollider>(true))
            {
                var wall = collider.transform;
                var view = wall.Find("VisualRoot")?.GetComponent<MeshRenderer>();
                if (view == null) continue;
                var parent = wall.parent;
                var source = parent == world.solidRoot
                    ? wall.name.StartsWith("Cave connector", StringComparison.Ordinal) ? art.caveWallVisual : art.seabedVisual
                    : parent.name.Contains("BRANCH CAVE") ? art.caveWallVisual : art.ruinWallVisual;
                view.sharedMaterial = source.sharedMaterial;
            }
        }

        static void EditPrefab(string path, Func<GameObject, bool> update)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (update(root)) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }

        static void EditScene<T>(string path, string backup, Action<T> update) where T : Component
        {
            var active = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                if (scene.isDirty && !EditorSceneManager.SaveScene(scene, backup + "/" + scene.name + "-open.unity", true))
                    throw new IOException("Could not back up the open scene " + path);
                var target = FindInScene<T>(scene);
                if (target == null) throw new InvalidOperationException(typeof(T).Name + " missing from " + path);
                update(target);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
            }
            finally
            {
                if (active.isLoaded) SceneManager.SetActiveScene(active);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
#endif
