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
    internal static partial class PixelArtInstaller
    {
        const string SurfaceBackdrops = Root + "/Ship/StaticSprites/Backdrop/";
        const string WaterBackdrops = Root + "/Environment/StaticSprites/Backdrop/";
        const string DecorSprites = Root + "/Environment/StaticSprites/Decor/";
        const string WeaponSprites = Root + "/Items/StaticSprites/Weapons/";

        [MenuItem("LifeLine/Art/Install Ocean, Weapons and Enemies")]
        public static void InstallOceanWeaponsAndEnemies()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before installing sprite art.");
            foreach (string crew in new[] { "Captain", "Diver" }) InstallCrewActions(crew);
            InstallEnemy("Bird", LoadEnemyFrames("Bird", "Fly", "Swoop", 1.65f), LoadEnemyFrames("ArmoredBird", "Fly", "Swoop", 1.78f));
            InstallEnemy("Piranha", LoadEnemyFrames("Piranha", "Swim", "Bite", 1.16f));
            InstallEnemy("Shark", LoadEnemyFrames("Shark", "Swim", "Bite", 3.2f), LoadEnemyFrames("WhiteShark", "Swim", "Bite", 3.45f));
            var shadow = LoadEnemyFrames("Shadow", "Swim", "Bite", 6.5f);
            shadow.dash = EnemyFrames("Shadow", "Dash");
            shadow.waterShot = EnemyFrames("Shadow", "WaterShot");
            shadow.basicAttack = EnemyFrames("Shadow", "BasicBite");
            InstallEnemy("Shadow", shadow);
            InstallSpear();
            InstallBackdrop();
            AssetDatabase.SaveAssets();
            Debug.Log("LifeLine ocean, weapons and enemy sprites installed.");
        }

        static void InstallCrewActions(string crew)
        {
            string folder = Root + "/Characters/SpriteSheets/" + crew;
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(folder + "/" + crew + ".controller");
            if (controller == null) throw new InvalidOperationException("Install the first pixel art pass for " + crew + " before weapon poses.");
            var machine = controller.layers[0].stateMachine;
            bool installed = controller.parameters.Any(p => p.name == "PixelAction");
            if (!installed) controller.AddParameter("PixelAction", AnimatorControllerParameterType.Bool);
            if (!controller.parameters.Any(p => p.name == "Moving")) controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);

            foreach (var transition in machine.anyStateTransitions)
            {
                if (transition.conditions.Any(condition => condition.parameter == "PixelAction")) continue;
                switch (transition.destinationState != null ? transition.destinationState.name : "")
                {
                    case "Downed": transition.duration = .035f; continue;
                    case "Walk": UpdatePace(transition, AnimatorConditionMode.If); break;
                    case "Idle": UpdatePace(transition, AnimatorConditionMode.IfNot); break;
                }
                transition.duration = .055f;
                transition.hasFixedDuration = true;
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, "PixelAction");
                EditorUtility.SetDirty(transition);
            }

            Sprite idle = Required("Characters", crew + "_Idle");
            float idleAnchor = (HeadAnchor(idle) - .5f) * idle.bounds.size.x * (1.8f / idle.bounds.size.y);
            var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/Walk/Walk.anim");
            if (!installed && walk != null)
            {
                Sprite[] frames = Frames(folder + "/Walk");
                float scale = 1.8f / frames.Max(frame => frame.bounds.size.y);
                AlignHead(walk, frames, scale, idleAnchor, true);
                EditorUtility.SetDirty(walk);
            }

            foreach (string weapon in new[] { "Paddle", "Harpoon", "Speargun" })
            {
                bool melee = weapon != "Speargun";
                foreach (bool water in new[] { false, true })
                {
                    string name = weapon + (water ? "Swim" : "");
                    if (machine.states.Any(state => state.state.name == name)) continue;
                    string directory = folder + "/" + name;
                    Sprite[] frames = Frames(directory);
                    var clip = BuildAttack(name, directory, frames, water, melee ? .28f : .32f, idleAnchor);
                    var state = machine.AddState(name);
                    state.motion = clip;
                    if (melee) state.tag = "Melee";
                    EditorUtility.SetDirty(state);
                }
            }
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
        }

        static void UpdatePace(AnimatorStateTransition transition, AnimatorConditionMode mode)
        {
            foreach (var condition in transition.conditions)
                if (condition.parameter == "Speed") transition.RemoveCondition(condition);
            transition.AddCondition(mode, 0, "Moving");
        }

        static AnimationClip BuildAttack(string name, string folder, Sprite[] frames, bool water, float duration, float idleAnchor)
        {
            string path = folder + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null) return clip;
            clip = new AnimationClip { frameRate = frames.Length / duration };
            var keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / clip.frameRate, value = frames[Mathf.Min(i, frames.Length - 1)] };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(PixelBody, typeof(SpriteRenderer), "m_Sprite"), keys);
            float size = frames.Max(frame => frame.bounds.size.y);
            float scale = (water ? .94f : 1.8f) / size;
            foreach (string axis in new[] { "x", "y", "z" })
                clip.SetCurve(PixelBody, typeof(Transform), "localScale." + axis, AnimationCurve.Constant(0, duration, scale));
            if (!water) AlignHead(clip, frames, scale, idleAnchor, false);
            else clip.SetCurve(PixelBody, typeof(Transform), "localPosition.x", AnimationCurve.Constant(0, duration, .05f));
            var vertical = frames.Concat(new[] { frames[^1] }).Select((frame, i) =>
                HoldKey(i / clip.frameRate, water ? 0 : -.9f + frame.bounds.size.y * scale * .5f)).ToArray();
            clip.SetCurve(PixelBody, typeof(Transform), "localPosition.y", new AnimationCurve(vertical));
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        static void AlignHead(AnimationClip clip, Sprite[] frames, float scale, float target, bool loop)
        {
            var keys = new Keyframe[frames.Length + 1];
            for (int i = 0; i < frames.Length; i++)
                keys[i] = HoldKey(i / clip.frameRate, target - (HeadAnchor(frames[i]) - .5f) * frames[i].bounds.size.x * scale);
            keys[^1] = HoldKey(frames.Length / clip.frameRate, keys[loop ? 0 : frames.Length - 1].value);
            clip.SetCurve(PixelBody, typeof(Transform), "localPosition.x", new AnimationCurve(keys));
        }

        static Keyframe HoldKey(float time, float value) => new Keyframe(time, value, float.PositiveInfinity, float.PositiveInfinity);

        static float HeadAnchor(Sprite sprite)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)))) return .5f;
                int width = texture.width, height = texture.height;
                var pixels = texture.GetPixels32();
                long sum = 0, count = 0;
                for (int y = height * 76 / 100; y < height * 89 / 100; y++)
                    for (int x = 0; x < width; x++)
                        if (pixels[y * width + x].a >= 180) { sum += x; count++; }
                return count > 0 ? (float)sum / (count * width) : .5f;
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        static Sprite[] EnemyFrames(string kind, string action) => Frames(Root + "/Enemies/SpriteSheets/" + kind + "/" + action);

        static PixelEnemyView.FrameSet LoadEnemyFrames(string kind, string travel, string attack, float width)
        {
            return new PixelEnemyView.FrameSet
            {
                travel = EnemyFrames(kind, travel),
                attack = EnemyFrames(kind, attack),
                width = width,
                framesPerSecond = kind == "Shadow" ? 5 : kind == "Bird" || kind == "ArmoredBird" ? 9 : 8
            };
        }

        static void InstallEnemy(string prefab, PixelEnemyView.FrameSet art, PixelEnemyView.FrameSet variant = null)
        {
            EditPrefab("Enemies/" + prefab, root =>
            {
                if (root.GetComponent<PixelEnemyView>() != null) return false;
                var enemy = root.GetComponent<LifelineEnemy>();
                HideMeshes(enemy.visualRoot);
                var view = root.AddComponent<PixelEnemyView>();
                view.enemy = enemy;
                view.body = PixelWorldArt.PlaceSprite(root.transform, "Pixel enemy", art.travel[0], new Vector3(0, 0, -.42f),
                    new Vector2(art.width, art.width), root.layer);
                view.primary = art;
                view.variant = variant;
                return true;
            });
        }

        static void InstallSpear()
        {
            Sprite bolt = LoadSprite(WeaponSprites, "SpeargunBolt");
            EditPrefab("Enemies/Spear", root =>
            {
                Transform visuals = root.transform.Find("VisualRoot");
                if (visuals.Find("Pixel bolt") != null) return false;
                HideMeshes(visuals);
                var projectile = PixelWorldArt.PlaceSprite(visuals, "Pixel bolt", bolt, new Vector3(0, .03f, -.24f),
                    new Vector2(1.05f, .19f), root.layer);
                // The existing projectile root defines its forward axis as +Y.
                projectile.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                return true;
            });
        }

        [MenuItem("LifeLine/Art/Install Sky Backdrops")]
        public static void InstallSkyBackdrops()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before installing the sky backdrops.");
            InstallBackdrop();
            AssetDatabase.SaveAssets();
        }

        static void InstallBackdrop()
        {
            string path = Root + "/Environment/OceanBackdrops.asset";
            var art = AssetDatabase.LoadAssetAtPath<PixelOceanArt>(path);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<PixelOceanArt>();
                art.backdropShader = Shader.Find("LifeLine/Art/Pixel Ocean Backdrop");
                if (art.backdropShader == null) throw new InvalidOperationException("Pixel ocean shader is missing.");
                art.sky = LoadSprite(SurfaceBackdrops, "CoastSky").texture;
                art.shallows = LoadSprite(WaterBackdrops, "Shallows").texture;
                art.city = LoadSprite(WaterBackdrops, "SunkenCity").texture;
                art.deep = LoadSprite(WaterBackdrops, "DeepWater").texture;
                art.cloud = LoadSprite(SurfaceBackdrops, "Cloud");
                art.haze = LoadSprite(SurfaceBackdrops, "CloudHaze");
                art.foam = LoadSprite(SurfaceBackdrops, "Seafoam");
                art.ripple = LoadSprite(SurfaceBackdrops, "Ripple");
                art.kelp = LoadSprite(DecorSprites, "Kelp");
                art.coral = LoadSprite(DecorSprites, "Coral");
                art.seagrass = LoadSprite(DecorSprites, "Seagrass");
                art.arch = LoadSprite(DecorSprites, "RuinedArch");
                art.rock = LoadSprite(DecorSprites, "RavineRock");
                art.bubbles = LoadSprite(DecorSprites, "Bubbles");
                AssetDatabase.CreateAsset(art, path);
            }
            bool skyChanged = BindSky(ref art.duskSky, SurfaceBackdrops, "CoastSkyDusk");
            skyChanged |= BindSky(ref art.cityCoast, SurfaceBackdrops, "FloodedCityCoast");
            skyChanged |= BindSky(ref art.nightSky, SurfaceBackdrops, "CoastSkyNight");
            skyChanged |= BindSky(ref art.waterlineSky, WaterBackdrops, "WaterlineSky");
            skyChanged |= BindSky(ref art.waterlineDuskSky, WaterBackdrops, "WaterlineDuskSky");
            skyChanged |= BindSky(ref art.waterlineNightSky, WaterBackdrops, "WaterlineNightSky");
            if (skyChanged) EditorUtility.SetDirty(art);

            Scene scene = SceneManager.GetSceneByPath(GameProjectBuilder.PlayScene);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(GameProjectBuilder.PlayScene, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("Save Gameplay before binding the backdrop.");
                var game = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameSession>(true)).Single();
                var backdrop = game.GetComponent<PixelOceanBackdrop>();
                if (game.world.oceanArt != null && backdrop != null && backdrop.surface != null && backdrop.underwater != null) return;
                if (game.world.oceanArt == null) game.world.oceanArt = art;
                MarineSceneArtInstaller.InstallBackdropObjects(game, game.world.oceanArt);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the ocean backdrop setup.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        static bool BindSky(ref Texture2D sky, string directory, string name)
        {
            if (sky != null) return false;
            sky = LoadSprite(directory, name).texture;
            return true;
        }

        static Sprite LoadSprite(string directory, string name)
        {
            string path = directory + name + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return sprite != null ? sprite : throw new FileNotFoundException(path);
        }
    }
}
