using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lifeline.Editor
{
    internal sealed class PixelSpriteImporter : AssetPostprocessor
    {
        internal static bool NeedsCleanAlpha(string path) => path.StartsWith(PixelArtInstaller.Root + "/", StringComparison.Ordinal)
            && (path.Contains("/SpriteSheets/") || path.Contains("/Characters/StaticSprites/") || path.EndsWith("/EelGenerator.png", StringComparison.Ordinal));

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(PixelArtInstaller.Root + "/", StringComparison.Ordinal) ||
                assetImporter is not TextureImporter importer) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            if (importer.importSettingsMissing) importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }

        void OnPostprocessTexture(Texture2D texture)
        {
            if (NeedsCleanAlpha(assetPath)) PixelSpriteAlpha.RemoveMatte(texture);
        }
    }

    internal static partial class PixelArtInstaller
    {
        internal const string Root = "Assets/_LifeLine/Art";
        const string Prefabs = "Assets/_LifeLine/Prefabs";
        const string PixelBody = "Pixel Body";
        const string SpriteNode = "Pixel Sprite";
        static readonly string[] Categories = { "Characters", "Ship", "Stations", "Environment", "Items", "Enemies", "Effects", "UI" };

        [MenuItem("LifeLine/Art/Create Artwork Folders")]
        public static void CreateFolders()
        {
            Folder("Assets/_LifeLine", "Art");
            foreach (string category in Categories)
            {
                string path = Folder(Root, category);
                Folder(path, "StaticSprites");
                string spriteSheets = Folder(path, "SpriteSheets");
                if (category != "Characters") continue;
                foreach (string crew in new[] { "Captain", "Diver" })
                {
                    string actor = Folder(spriteSheets, crew);
                    foreach (string action in new[] { "Walk", "Swim" }) Folder(actor, action);
                }
            }
        }

        [MenuItem("LifeLine/Art/Install First Pixel Art Pass")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before installing sprite art.");
            CreateFolders();
            if (AssetDatabase.LoadAssetAtPath<Sprite>(StaticPath("Ship", "SalvageHull")) == null)
                throw new InvalidOperationException("Import the staged PNGs into Assets/_LifeLine/Art first.");

            InstallCrew("Captain");
            InstallCrew("Diver");
            InstallShip();
            foreach (StationKind station in Enum.GetValues(typeof(StationKind))) InstallStation(station);
            Sprite crate = Required("Items", "CargoCrate");
            EditPrefab("World/Crate", root =>
            {
                var visual = root.GetComponent<LootPickup>().visualRoot;
                if (visual.Find(SpriteNode) != null) return false;
                HideMeshes(visual);
                PixelWorldArt.PlaceSprite(visual, SpriteNode, crate, new Vector3(0, 0, -.36f), new Vector2(1.05f, .65f), root.layer);
                return true;
            });
            InstallTrapdoor();
            InstallWorld();
            AssetDatabase.SaveAssets();
            Debug.Log("LifeLine first pixel art pass installed. Existing collider, interaction and gameplay roots retained.");
        }

        static void InstallCrew(string name)
        {
            string animationPath = Root + "/Characters/SpriteSheets/" + name;
            Sprite idle = Required("Characters", name + "_Idle");
            Sprite downed = Required("Characters", name + "_Downed");
            var walk = Frames(animationPath + "/Walk");
            var swim = Frames(animationPath + "/Swim");
            string controllerPath = animationPath + "/" + name + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
                controller.AddParameter("Swimming", AnimatorControllerParameterType.Bool);
                controller.AddParameter("Downed", AnimatorControllerParameterType.Bool);
                var states = controller.layers[0].stateMachine;
                var idleState = State(states, "Idle", animationPath, new[] { idle }, false, 1.8f, true);
                var walkState = State(states, "Walk", animationPath + "/Walk", walk, true, 1.8f, true);
                var swimState = State(states, "Swim", animationPath + "/Swim", swim, true, 2.18f, false);
                var downedState = State(states, "Downed", animationPath, new[] { downed }, false, 1.8f, false);
                states.defaultState = idleState;
                Transition(states, downedState).AddCondition(AnimatorConditionMode.If, 0, "Downed");
                var swimTransition = Transition(states, swimState);
                swimTransition.AddCondition(AnimatorConditionMode.If, 0, "Swimming");
                swimTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "Downed");
                foreach (var state in new[] { walkState, idleState })
                {
                    var transition = Transition(states, state);
                    transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Downed");
                    transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Swimming");
                    transition.AddCondition(state == walkState ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, .18f, "Speed");
                }
            }
            EditPrefab("Characters/" + name, root =>
            {
                var actor = root.GetComponent<CrewActor>();
                if (actor.visualRoot.Find(PixelBody) != null) return false;
                HideMeshes(actor.visualRoot);
                var body = PixelWorldArt.PlaceSprite(actor.visualRoot, PixelBody, idle, new Vector3(0, 0, -.25f), new Vector2(4, 1.8f), root.layer);
                body.sortingOrder = 4;
                actor.animator = actor.visualRoot.GetComponent<Animator>();
                if (actor.animator == null) actor.animator = actor.visualRoot.gameObject.AddComponent<Animator>();
                actor.animator.runtimeAnimatorController = controller;
                actor.animator.applyRootMotion = false;
                var presentation = root.GetComponent<CrewPresentation>();
                presentation.animator = actor.animator;
                presentation.placeholderPoses = false;
                presentation.drawMeleeWithCustomPoses = true;
                presentation.rotateSwimmingSprite = true;
                return true;
            });
        }

        static AnimatorState State(AnimatorStateMachine states, string title, string path, Sprite[] frames, bool loop, float metres, bool upright)
        {
            var clip = new AnimationClip { frameRate = 8 };
            var keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int index = 0; index <= frames.Length; index++)
                keys[index] = new ObjectReferenceKeyframe { time = index / clip.frameRate, value = frames[index % frames.Length] };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(PixelBody, typeof(SpriteRenderer), "m_Sprite"), keys);
            float size = upright ? frames.Max(sprite => sprite.bounds.size.y) : frames.Max(sprite => sprite.bounds.size.x);
            float scale = metres / size;
            foreach (string axis in new[] { "x", "y", "z" })
                clip.SetCurve(PixelBody, typeof(Transform), "localScale." + axis, AnimationCurve.Constant(0, keys[^1].time, scale));
            // Match the sole of each walk frame to the capsule's feet, even when frame bounds differ.
            var vertical = frames.Append(frames[0]).Select((sprite, i) => new Keyframe(i / clip.frameRate,
                upright ? -.9f + sprite.bounds.size.y * scale * .5f : title == "Downed" ? -.58f : 0f,
                float.PositiveInfinity, float.PositiveInfinity)).ToArray();
            clip.SetCurve(PixelBody, typeof(Transform), "localPosition.y", new AnimationCurve(vertical));
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path + "/" + title + ".anim");
            var state = states.AddState(title);
            state.motion = clip;
            return state;
        }

        static AnimatorStateTransition Transition(AnimatorStateMachine states, AnimatorState destination)
        {
            var result = states.AddAnyStateTransition(destination);
            result.duration = 0f;
            result.hasExitTime = false;
            result.canTransitionToSelf = false;
            return result;
        }

        static void InstallShip()
        {
            Sprite hull = Required("Ship", "SalvageHull");
            EditPrefab("Ship/Ship", root =>
            {
                var visual = root.transform.Find("VisualRoot");
                if (visual.Find("Pixel Hull") != null) return false;
                var back = visual.Find("HullBack");
                if (back != null) HideMeshes(back);
                foreach (string name in new[] { "CabinWestWall", "CabinEastWall", "CabinFloor" })
                {
                    var physical = root.transform.Find(name);
                    if (physical != null) HideMeshes(physical);
                }
                var render = PixelWorldArt.PlaceSprite(visual, "Pixel Hull", hull, new Vector3(0, -2.64f, 1.18f), new Vector2(24, 4.5f), root.layer);
                render.transform.localScale = new Vector3(24f / hull.bounds.size.x, 4.5f / hull.bounds.size.y, 1);
                return true;
            });
        }

        static void InstallStation(StationKind kind)
        {
            Sprite sprite = Required("Stations", kind.ToString());
            EditPrefab("Stations/" + kind, root =>
            {
                var anchor = root.GetComponent<StationAnchor>();
                Transform visual = anchor.visualRoot;
                if (visual.Find(SpriteNode) != null) return false;
                HideMeshes(visual);
                var station = PixelWorldArt.PlaceSprite(visual, SpriteNode, sprite, Vector3.zero, new Vector2(2.25f, 1.5f), root.layer);
                Vector3 point = anchor.interactionPoint.localPosition;
                station.transform.localPosition = new Vector3(0, point.y - .9f + station.bounds.extents.y, -.04f);
                return true;
            });
        }

        static void InstallTrapdoor()
        {
            Sprite sprite = Required("Environment", "Trapdoor");
            EditPrefab("World/Trapdoor", root =>
            {
                var gate = root.GetComponent<TrapdoorGate>();
                if (gate.doorSprite != null) return false;
                HideMeshes(gate.visualRoot);
                gate.doorSprite = PixelWorldArt.PlaceSprite(gate.visualRoot, SpriteNode, sprite, new Vector3(0, 0, -.51f), Vector2.one, root.layer);
                gate.doorSprite.transform.localScale = new Vector3(1 / sprite.bounds.size.x, 1 / sprite.bounds.size.y, 1);
                return true;
            });
        }

        static void InstallWorld()
        {
            string dataPath = Root + "/Environment/UnderwaterSprites.asset";
            var art = AssetDatabase.LoadAssetAtPath<PixelWorldArt>(dataPath);
            var ruinWall = WallMaterial("RuinWall");
            var caveWall = WallMaterial("CaveWall");
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<PixelWorldArt>();
                art.wood = Required("Items", "Wood"); art.metal = Required("Items", "Metal");
                art.fiber = Required("Items", "Fiber"); art.fish = Required("Items", "Fish"); art.fuel = Required("Items", "Fuel");
                art.eelGenerator = Required("Items", "EelGenerator");
                art.lostCargo = Required("Items", "LostBackpack");
                art.cabinet = art.hideSpot = Required("Environment", "HideLocker");
                art.ruinWall = ruinWall;
                art.caveWall = caveWall;
                AssetDatabase.CreateAsset(art, dataPath);
            }
            string filterPath = Prefabs + "/World/FilterSalvageVisual.prefab";
            var filter = AssetDatabase.LoadAssetAtPath<GameObject>(filterPath);
            if (filter == null)
            {
                var root = new GameObject("Filter Salvage Sprite") { layer = 11 };
                try
                {
                    PixelWorldArt.PlaceSprite(root.transform, SpriteNode, Required("Items", "SalvagedFilter"), new Vector3(0, 0, -.3f), new Vector2(1.05f, .92f), root.layer);
                    filter = PrefabUtility.SaveAsPrefabAsset(root, filterPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            Scene scene = SceneManager.GetSceneByPath(GameProjectBuilder.PlayScene);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(GameProjectBuilder.PlayScene, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("Save Gameplay before binding the underwater art.");
                var world = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldGenerator>(true)).Single();
                if (world.pixelArt != null) return;
                world.pixelArt = art;
                if (world.filterSalvageVisualPrefab == null) world.filterSalvageVisualPrefab = filter;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save underwater art bindings.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        static Material WallMaterial(string title)
        {
            string directory = Folder(Root + "/Environment/StaticSprites", "Materials");
            string path = directory + "/" + title + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Texture texture = Required("Environment", title).texture;
            var shader = Shader.Find("LifeLine/Art/Pixel World Tile");
            if (shader == null) throw new InvalidOperationException("The Pixel World Tile shader did not import.");
            if (material != null)
            {
                if (material.shader.name == "Universal Render Pipeline/Unlit" && material.mainTexture == texture)
                {
                    material.shader = shader;
                    material.mainTexture = texture;
                    EditorUtility.SetDirty(material);
                }
                return material;
            }
            material = new Material(shader) { mainTexture = texture };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Sprite[] Frames(string directory)
        {
            var frames = AssetDatabase.FindAssets("t:Sprite", new[] { directory }).Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal).Select(AssetDatabase.LoadAssetAtPath<Sprite>).ToArray();
            if (frames.Length < 2) throw new InvalidOperationException("Expected an ordered frame group in " + directory);
            return frames;
        }

        static void EditPrefab(string name, Func<GameObject, bool> edit)
        {
            string path = Prefabs + "/" + name + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (edit(root)) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void HideMeshes(Transform visual)
        {
            foreach (var mesh in visual.GetComponentsInChildren<MeshRenderer>(true))
                if (mesh.GetComponent<TextMesh>() == null) mesh.enabled = false;
        }

        static Sprite Required(string category, string name)
        {
            string path = StaticPath(category, name);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return sprite != null ? sprite : throw new FileNotFoundException(path);
        }

        static string StaticPath(string category, string name) => Root + "/" + category + "/StaticSprites/" + name + ".png";

        static string Folder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
            return path;
        }
    }
}
