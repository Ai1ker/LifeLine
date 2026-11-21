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
        const float StandingHeight = 1.8f;

        [MenuItem("LifeLine/Art/Install Ship Details and Align Sprite Poses")]
        public static void InstallShipDetailsAndAlignPoses()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before installing sprite art.");
            Sprite cabinet = Required("Items", "SalvageCabinet");
            Sprite ladder = Required("Ship", "CabinLadder");
            Sprite[] boatFrames = Frames(Root + "/Ship/SpriteSheets/SurfaceBob");
            InstallCabinLadder(ladder);
            InstallProxyAndCabinet(boatFrames, cabinet);
            foreach (string crew in new[] { "Captain", "Diver" })
            {
                string directory = Root + "/Characters/SpriteSheets/" + crew;
                var controller = FindCrewController(crew, directory);
                ConfigureClimb(controller, controller.layers[0].stateMachine, directory);
                ConfigureWaterFaint(controller, controller.layers[0].stateMachine, directory, Required("Characters", crew + "_WaterFaint"));
            }

            int crewFrames = 0, enemyFrames = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string crew in new[] { "Captain", "Diver" }) crewFrames += RegisterCrew(crew);
                foreach (string prefab in new[] { "Bird", "Piranha", "Shark", "Shadow" })
                    enemyFrames += RegisterEnemy(prefab);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.SaveAssets();
            Debug.Log($"LifeLine sprite alignment complete. {crewFrames} crew poses, {enemyFrames} enemy frames, {boatFrames.Length} registered boat frames.");
        }

        static void InstallCabinLadder(Sprite ladder)
        {
            EditPrefab("Ship/Ship", root =>
            {
                Transform visual = root.transform.Find("VisualRoot");
                if (visual.Find("Pixel cabin ladder") != null) return false;
                foreach (Transform part in visual)
                    if (part.name.StartsWith("LadderRail", StringComparison.Ordinal) ||
                        part.name.StartsWith("LadderRung", StringComparison.Ordinal))
                        HideMeshes(part);
                PixelWorldArt.PlaceSprite(visual, "Pixel cabin ladder", ladder, new Vector3(0, -1.79f, .59f),
                    new Vector2(1.08f, 4.48f), root.layer);
                return true;
            });
        }

        static void InstallProxyAndCabinet(Sprite[] boatFrames, Sprite cabinet)
        {
            var look = AssetDatabase.LoadAssetAtPath<PixelWorldArt>(Root + "/Environment/UnderwaterSprites.asset");
            if (look != null && (look.cabinet == null || look.cabinet == look.hideSpot))
            {
                look.cabinet = cabinet;
                EditorUtility.SetDirty(look);
            }

            Scene scene = SceneManager.GetSceneByPath(GameProjectBuilder.PlayScene);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(GameProjectBuilder.PlayScene, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("Save Gameplay before binding the surface boat.");
                var proxy = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<UnderwaterShipProxy>(true)).Single();
                if (proxy.surfaceFrames != null && proxy.surfaceFrames.Length > 0) return;
                proxy.surfaceFrames = boatFrames;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save surface boat bindings.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        static int RegisterCrew(string crew)
        {
            string directory = Root + "/Characters/SpriteSheets/" + crew;
            var controller = FindCrewController(crew, directory);
            var machine = controller.layers[0].stateMachine;
            var idle = Required("Characters", crew + "_Idle");
            bool captain = crew == "Captain";
            var neutral = new PixelPoseRegistration(idle);
            var neutralHead = neutral.CrewHead(captain, true);
            float restingPPU = neutral.height / StandingHeight;
            float headMetres = neutralHead.size / restingPPU;
            float dryHeadX = (neutralHead.center.x - neutral.width * .5f) / restingPPU;
            if (headMetres < .1f) throw new InvalidOperationException("Missing reference helmet landmark on " + crew + ".");
            float dryHeadY = neutralHead.center.y / restingPPU - StandingHeight * .5f;

            var swimReference = new PixelPoseRegistration(Frames(directory + "/Swim")[0]);
            var swimLandmark = swimReference.CrewHead(captain, false);
            float swimPPU = SwimPixelsPerMetre(swimReference, swimLandmark, headMetres, true);
            Vector2 swimOrigin = SwimOrigin(swimReference, swimLandmark, swimPPU);
            string[] ownStates = { "Idle", "Walk", "Swim", "Downed", "WaterFaint", "Paddle", "Harpoon", "Speargun",
                "PaddleSwim", "HarpoonSwim", "SpeargunSwim", "Climb" };
            int total = 0;
            foreach (var child in machine.states)
            {
                string state = child.state.name;
                if (!ownStates.Contains(state) || child.state.motion is not AnimationClip clip) continue;
                string path = AssetDatabase.GetAssetPath(clip);
                if (!path.StartsWith(directory + "/", StringComparison.Ordinal)) continue;
                bool downed = state == "Downed", swimming = state.EndsWith("Swim", StringComparison.Ordinal);
                var binding = EditorCurveBinding.PPtrCurve(PixelBody, typeof(SpriteRenderer), "m_Sprite");
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                foreach (Sprite sprite in keys.Select(frame => frame.value).OfType<Sprite>().Distinct())
                {
                    var pose = new PixelPoseRegistration(sprite);
                    bool upright = !downed && !swimming;
                    var head = pose.CrewHead(captain, upright);
                    float ppu;
                    Vector2 origin;
                    if (state == "WaterFaint")
                    {
                        // The expression sprite is painted over Swim_01. Match its exact physical dimensions and pivot.
                        ppu = swimPPU * pose.width / swimReference.width;
                        origin = new Vector2(swimOrigin.x * pose.width / swimReference.width, swimOrigin.y * pose.height / swimReference.height);
                    }
                    else if (swimming)
                    {
                        ppu = SwimPixelsPerMetre(pose, head, headMetres, state == "Swim");
                        origin = SwimOrigin(pose, head, ppu);
                    }
                    else if (downed)
                    {
                        ppu = pose.width / StandingHeight;
                        origin = new Vector2(pose.width * .5f, pose.height * .5f);
                    }
                    else
                    {
                        ppu = pose.height / StandingHeight;
                        float headX = head.size >= 10 ? head.center.x : pose.width * .5f;
                        origin = new Vector2(headX - dryHeadX * ppu, state == "Climb" && head.size >= 10 ?
                            head.center.y - dryHeadY * ppu : pose.height * .5f);
                    }
                    pose.SaveRegistration(sprite, ppu, origin);
                    total++;
                }
                foreach (var transformBinding in AnimationUtility.GetCurveBindings(clip))
                    if (transformBinding.path == PixelBody && transformBinding.type == typeof(Transform))
                        AnimationUtility.SetEditorCurve(clip, transformBinding, null);
                if (downed) clip.SetCurve(PixelBody, typeof(Transform), "localPosition.y", AnimationCurve.Constant(0, clip.length, -.58f));
                else clip.SetCurve(PixelBody, typeof(Transform), "localPosition.y", AnimationCurve.Constant(0, clip.length, 0));
                EditorUtility.SetDirty(clip);
            }
            EditPrefab("Characters/" + crew, root =>
            {
                Transform body = root.GetComponent<CrewActor>().visualRoot.Find(PixelBody);
                body.localPosition = new Vector3(0, 0, -.25f);
                body.localScale = Vector3.one;
                return true;
            });
            return total;
        }

        static float SwimPixelsPerMetre(PixelPoseRegistration pose, PixelPoseRegistration.Landmark head, float headMetres, bool unarmed)
        {
            float nominal = pose.width / (unarmed ? 2.15f : 2.65f);
            return head.size >= 10 ? Mathf.Clamp(head.size / headMetres, nominal * .79f, nominal * 1.32f) : nominal;
        }

        static Vector2 SwimOrigin(PixelPoseRegistration pose, PixelPoseRegistration.Landmark head, float ppu)
        {
            return head.size >= 10 ? head.center - new Vector2(.46f, .17f) * ppu : new Vector2(pose.width * .5f, pose.height * .5f);
        }

        static AnimatorController FindCrewController(string crew, string directory)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(directory + "/" + crew + ".controller");
            return controller != null ? controller : throw new InvalidOperationException("Install the first pixel pass for " + crew + ".");
        }

        static void ConfigureClimb(AnimatorController controller, AnimatorStateMachine machine, string directory)
        {
            if (machine.states.Any(item => item.state.name == "Climb")) return;
            const string climbing = "Climbing";
            if (!controller.parameters.Any(parameter => parameter.name == climbing))
                controller.AddParameter(climbing, AnimatorControllerParameterType.Bool);
            if (!controller.parameters.Any(parameter => parameter.name == "ClimbRate"))
                controller.AddParameter("ClimbRate", AnimatorControllerParameterType.Float);
            foreach (var transition in machine.anyStateTransitions)
                if (transition.destinationState != null && (transition.destinationState.name == "Walk" || transition.destinationState.name == "Idle"))
                {
                    transition.AddCondition(AnimatorConditionMode.IfNot, 0, climbing);
                    EditorUtility.SetDirty(transition);
                }
            string folder = directory + "/Climb";
            var state = State(machine, "Climb", folder, Frames(folder), true, StandingHeight, true);
            state.speedParameterActive = true;
            state.speedParameter = "ClimbRate";
            var enter = Transition(machine, state);
            enter.duration = .06f;
            enter.hasFixedDuration = true;
            enter.AddCondition(AnimatorConditionMode.If, 0, climbing);
            enter.AddCondition(AnimatorConditionMode.IfNot, 0, "Downed");
            if (controller.parameters.Any(parameter => parameter.name == "PixelAction")) enter.AddCondition(AnimatorConditionMode.IfNot, 0, "PixelAction");
            EditorUtility.SetDirty(state); EditorUtility.SetDirty(controller); EditorUtility.SetDirty(machine);
        }

        static void ConfigureWaterFaint(AnimatorController controller, AnimatorStateMachine machine, string directory, Sprite expression)
        {
            if (machine.states.Any(item => item.state.name == "WaterFaint")) return;
            foreach (var transition in machine.anyStateTransitions)
                if (transition.destinationState != null && transition.destinationState.name == "Downed")
                {
                    transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Swimming");
                    EditorUtility.SetDirty(transition);
                }
            var faint = State(machine, "WaterFaint", directory, new[] { expression }, false, 2.15f, false);
            var enter = Transition(machine, faint);
            enter.duration = .035f;
            enter.hasFixedDuration = true;
            enter.AddCondition(AnimatorConditionMode.If, 0, "Downed");
            enter.AddCondition(AnimatorConditionMode.If, 0, "Swimming");
            EditorUtility.SetDirty(faint); EditorUtility.SetDirty(controller); EditorUtility.SetDirty(machine);
        }

        static int RegisterEnemy(string prefab)
        {
            int count = 0;
            EditPrefab("Enemies/" + prefab, root =>
            {
                var view = root.GetComponent<PixelEnemyView>();
                if (view == null) return false;
                count = RegisterEnemyFrames(view.primary, prefab == "Bird", prefab == "Shadow") + RegisterEnemyFrames(view.variant, prefab == "Bird");
                view.body.transform.localScale = Vector3.one;
                return true;
            });
            return count;
        }

        static int RegisterEnemyFrames(PixelEnemyView.FrameSet art, bool bird, bool shadow = false)
        {
            if (art?.travel == null || art.travel.Length == 0) return 0;
            if (shadow) return RegisterShadowFrames(art);
            var neutral = new PixelPoseRegistration(art.travel[0]);
            var neutralCore = neutral.EnemyCore(bird);
            float basePPU = (bird ? art.travel.Max(frame => frame.rect.width) : neutral.width) / art.width;
            Vector2 target = (neutralCore.center - new Vector2(neutral.width * .5f, neutral.height * .5f)) / basePPU;
            var registered = new System.Collections.Generic.HashSet<Sprite>();
            foreach (Sprite[] sequence in new[] { art.travel, art.attack, art.dash, art.waterShot, art.basicAttack })
            {
                if (sequence == null || sequence.Length == 0) continue;
                float wingSpanPPU = sequence.Max(frame => frame.rect.width) / art.width;
                foreach (Sprite frame in sequence)
                {
                    if (!registered.Add(frame)) continue;
                    var pose = new PixelPoseRegistration(frame);
                    var core = pose.EnemyCore(bird);
                    float nominalPPU = bird ? wingSpanPPU : pose.width / art.width;
                    // Different bite silhouettes can include long open jaws or tall fins in the
                    // sample area. Keep a measured landmark within the authored creature size.
                    float ppu = neutralCore.size > 5 && core.size > 5 ?
                        Mathf.Clamp(basePPU * core.size / neutralCore.size, nominalPPU * .94f, nominalPPU * 1.07f) : nominalPPU;
                    pose.SaveRegistration(frame, ppu, core.center - target * ppu);
                }
            }
            art.registeredSprites = true;
            return registered.Count;
        }

        [MenuItem("LifeLine/Art/Register All Shadow Frames")]
        public static void RegisterShadowFrames()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before registering shadow frames.");
            int count = RegisterEnemy("Shadow");
            AssetDatabase.SaveAssets();
            Debug.Log($"Registered {count} shadow swim and attack frames against one visible body width.");
        }

        static int RegisterShadowFrames(PixelEnemyView.FrameSet art)
        {
            RectInt reference = new PixelPoseRegistration(art.travel[0]).OpaqueBounds();
            if (reference.width == 0) throw new InvalidOperationException("Shadow swim reference has no visible pixels.");
            float referenceHeight = art.width * reference.height / reference.width;
            var fits = new System.Collections.Generic.List<PixelEnemyView.FrameHeight>();
            foreach (Sprite sprite in new[] { art.travel, art.attack, art.dash, art.waterShot, art.basicAttack }
                .Where(sequence => sequence != null).SelectMany(sequence => sequence).Where(sprite => sprite != null).Distinct())
            {
                var pose = new PixelPoseRegistration(sprite);
                RectInt visible = pose.OpaqueBounds();
                if (visible.width == 0) throw new InvalidOperationException($"Shadow frame {sprite.name} has no visible pixels.");
                float ppu = visible.width / art.width;
                float height = visible.height / ppu;
                // Keep some room for jaw and fin movement while matching the silhouette across actions.
                float fittedHeight = Mathf.Clamp(height, referenceHeight * .94f, referenceHeight * 1.09f);
                pose.SaveRegistration(sprite, ppu, visible.center);
                fits.Add(new PixelEnemyView.FrameHeight { sprite = sprite, scale = fittedHeight / height });
            }
            art.frameHeightCorrections = fits.ToArray();
            art.registeredSprites = true;
            return fits.Count;
        }
    }
}
