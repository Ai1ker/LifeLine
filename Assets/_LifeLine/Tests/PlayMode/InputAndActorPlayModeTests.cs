using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lifeline.Tests
{
    // Real player action callbacks and isolated 3D PhysicsScenes require Play Mode.
    public sealed class InputAndActorPlayModeTests
    {
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        private readonly List<Scene> isolatedScenes = new List<Scene>();
        private Keyboard keyboard;
        private string prefix;
        private float oldScale;
        private InputSettings.UpdateMode oldUpdateMode;
        private InputSettings.BackgroundBehavior oldBackgroundBehavior;
        private bool settingsCaptured;
#if UNITY_EDITOR
        private InputSettings.EditorInputBehaviorInPlayMode oldEditorBehavior;
#endif

        [SetUp]
        public void SetUp()
        {
            Assert.That(Application.isPlaying, Is.True, "Run this assembly in the PlayMode tab.");
            prefix = "LifeLine.PlayTests." + Guid.NewGuid() + ".";
            oldScale = Time.timeScale;
            oldUpdateMode = InputSystem.settings.updateMode;
            oldBackgroundBehavior = InputSystem.settings.backgroundBehavior;
#if UNITY_EDITOR
            oldEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
#endif
            settingsCaptured = true;
            Time.timeScale = 1;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            keyboard = InputSystem.AddDevice<Keyboard>("LifeLinePlayTestKeyboard");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                Time.timeScale = 1;
                foreach (var item in created) if (item != null) UnityEngine.Object.Destroy(item);
                created.Clear();
                yield return null;
                foreach (var scene in isolatedScenes)
                    if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
                isolatedScenes.Clear();
            }
            finally
            {
                try
                {
                    if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                    if (prefix != null) { PlayerPrefs.DeleteKey(prefix + CrewRole.Captain); PlayerPrefs.DeleteKey(prefix + CrewRole.Diver); PlayerPrefs.Save(); }
                }
                finally
                {
                    if (settingsCaptured)
                    {
                        InputSystem.settings.updateMode = oldUpdateMode;
                        InputSystem.settings.backgroundBehavior = oldBackgroundBehavior;
#if UNITY_EDITOR
                        InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorBehavior;
#endif
                        Time.timeScale = oldScale; settingsCaptured = false;
                    }
                }
            }
        }

        private T Keep<T>(T item) where T : UnityEngine.Object { created.Add(item); return item; }
        private InputActionAsset Asset() => Keep(CrewInputDefaults.CreateAsset());
        private CrewInputReader Reader(CrewRole role, InputActionAsset asset)
        {
            var go = Keep(new GameObject("Input_" + role));
            var reader = go.AddComponent<CrewInputReader>(); reader.preferencesPrefix = prefix; reader.Initialize(role, asset); return reader;
        }
        private void Press(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }
        private Scene NewPhysicsScene(string name)
        {
            var scene = SceneManager.CreateScene(name, new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            isolatedScenes.Add(scene); return scene;
        }
        private static void Tick(MonoBehaviour target, string method)
        {
            // Invoke the actual tick while automatic behaviours are disabled; never double-simulate.
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        [UnityTest]
        public IEnumerator SharedKeyboardMapsKeepActionsSeparateAndConsumeButtonEdgesOnlyOnce()
        {
            var asset = Asset();
            var captain = Reader(CrewRole.Captain, asset);
            var diver = Reader(CrewRole.Diver, asset);
            Assert.That(captain.RuntimeActions.FindActionMap("Diver").enabled, Is.False);
            Assert.That(diver.RuntimeActions.FindActionMap("Captain").enabled, Is.False);
            Press(Key.W, Key.E, Key.Space, Key.Q);
            var a = captain.ReadCommand();
            var b = diver.ReadCommand();
            Assert.That(a.move, Is.EqualTo(Vector2.up));
            Assert.That(a.jump && a.interact && a.melee && a.fire, Is.True);
            Assert.That(a.interactHeld, Is.True);
            Assert.That(a.collect, Is.False);
            Assert.That(b.move, Is.EqualTo(Vector2.zero));
            Assert.That(b.jump || b.interact || b.melee || b.fire || b.collect, Is.False);
            a = captain.ReadCommand();
            Assert.That(a.move, Is.EqualTo(Vector2.up), "movement remains held through multiple physics ticks");
            Assert.That(a.jump || a.interact || a.melee || a.fire, Is.False, "button edges are drained once");
            Assert.That(a.interactHeld, Is.True, "revive keeps its held signal after the interact edge is consumed");
            Press(); captain.ReadCommand(); diver.ReadCommand();
            Assert.That(captain.ReadCommand().interactHeld, Is.False, "release cancels the continuous revive signal");
            Press(Key.UpArrow, Key.Enter, Key.Digit1, Key.Digit2, Key.Digit0, Key.RightShift, Key.Period, Key.Slash, Key.M);
            b = diver.ReadCommand(); a = captain.ReadCommand();
            Assert.That(b.move, Is.EqualTo(Vector2.up));
            Assert.That(b.jump && b.interact && b.collect && b.fire && b.melee && b.sprint && b.signal && b.bait && b.hide, Is.True);
            Assert.That(a.move, Is.EqualTo(Vector2.zero));
            Assert.That(a.interact || a.collect || a.fire || a.melee || a.sprint, Is.False);
            b = diver.ReadCommand();
            Assert.That(b.collect && b.sprint, Is.True, "collect and sprint remain held");
            Assert.That(b.interactHeld, Is.True);
            Assert.That(b.interact || b.fire || b.melee || b.signal || b.bait || b.hide, Is.False);
            Press(Key.Escape);
            Assert.That(captain.ReadCommand().pause && diver.ReadCommand().pause, Is.True);
            Assert.That(captain.ReadCommand().pause || diver.ReadCommand().pause, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CompositeDirectionOverridesSurviveFreshAssetAndDoNotModifyThePeerOrTemplate()
        {
            var original = Asset();
            var first = Reader(CrewRole.Captain, original);
            var move = first.RuntimeActions.FindAction("Captain/Move");
            Assert.That(move.bindings[0].isComposite, Is.True);
            Assert.That(move.bindings[1].isPartOfComposite, Is.True);
            move.ApplyBindingOverride(1, "<Keyboard>/h");
            first.SaveBindings();
            first.SetEnabled(false);
            var reloaded = Reader(CrewRole.Captain, Asset());
            var peer = Reader(CrewRole.Diver, original);
            Assert.That(reloaded.RuntimeActions.FindAction("Captain/Move").bindings[1].effectivePath, Is.EqualTo("<Keyboard>/h"));
            Assert.That(original.FindAction("Captain/Move").bindings[1].effectivePath, Is.EqualTo("<Keyboard>/w"));
            Assert.That(peer.RuntimeActions.FindAction("Diver/Move").bindings[1].effectivePath, Is.EqualTo("<Keyboard>/upArrow"));
            Press(Key.H);
            Assert.That(reloaded.ReadCommand().move, Is.EqualTo(Vector2.up));
            Assert.That(peer.ReadCommand().move, Is.EqualTo(Vector2.zero));
            string message = null;
            reloaded.BeginRebind("Move", 0, value => message = value);
            Assert.That(reloaded.IsRebinding, Is.False);
            Assert.That(message, Does.Contain("方向"), "the composite header cannot replace all four directions with one key");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PausedActorForwardsExternalCommandWithoutPhysicsOrLocalKeyboardOwnership()
        {
            var game = Keep(new GameObject("Session")).AddComponent<GameSession>(); game.enabled = false;
            game.autoStart = false;
            game.tuning = Keep(ScriptableObject.CreateInstance<GameTuning>());
            game.phase = SessionPhase.Paused;
            var actor = Keep(new GameObject("Actor")).AddComponent<CrewActor>(); actor.enabled = false;
            actor.Initialize(game, CrewRole.Captain, new Vector3(0, .9f, 0));
            var external = new ConstantInput { command = new CrewCommand { role = CrewRole.Captain, pause = true, move = Vector2.right } };
            actor.SetExternalInput(external);
            var calls = 0;
            game.CommandReceived += (source, command) => { Assert.That(source, Is.SameAs(actor)); Assert.That(command.pause, Is.True); calls++; };
            Time.timeScale = 0;
            Tick(actor, "Update");
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(actor.Position, Is.EqualTo(new Vector3(0, .9f, 0)));
            Assert.That(actor.input.ReadCommand().move, Is.EqualTo(Vector2.zero));
            actor.SetExternalInput(null);
            Assert.That(actor.input.RuntimeActions.FindActionMap("Captain").enabled, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MovingShipCarriesAnIdleHelmsmanOnceAndChargesOnlyForActualSailingDistance()
        {
            var scene = NewPhysicsScene("LifeLinePhysics_" + Guid.NewGuid());
                var gameObject = Keep(new GameObject("Session"));
                SceneManager.MoveGameObjectToScene(gameObject, scene);
                var game = gameObject.AddComponent<GameSession>(); game.enabled = false;
                game.autoStart = false; game.tuning = Keep(ScriptableObject.CreateInstance<GameTuning>());
                game.phase = SessionPhase.Playing; game.power = 100;
                var shipObject = Keep(new GameObject("Ship"));
                SceneManager.MoveGameObjectToScene(shipObject, scene);
                var ship = shipObject.AddComponent<ShipMotor>(); ship.enabled = false;
                game.ship = ship; ship.Initialize(game);
                var floor = Keep(GameObject.CreatePrimitive(PrimitiveType.Cube));
                SceneManager.MoveGameObjectToScene(floor, scene);
                floor.transform.SetParent(ship.transform);
                floor.transform.localPosition = new Vector3(0, -.2f, 0);
                floor.transform.localScale = new Vector3(24, .4f, 4);
                var actorObject = Keep(new GameObject("Helmsman"));
                SceneManager.MoveGameObjectToScene(actorObject, scene);
                var collider = actorObject.AddComponent<CapsuleCollider>(); collider.height = 1.8f;
                var actor = actorObject.AddComponent<CrewActor>(); actor.enabled = false;
                game.captain = actor;
                actor.Initialize(game, CrewRole.Captain, new Vector3(-1.2f, .9f, 0));
                actor.SetExternalInput(new ConstantInput { command = new CrewCommand { move = Vector2.right } });
                ship.SetHelm(actor);
                Tick(actor, "FixedUpdate");
                var startShip = ship.Position.x;
                var startLocal = actor.Position.x - ship.Position.x;
                for (var i = 0; i < 50; i++)
                {
                    Tick(ship, "FixedUpdate"); Tick(actor, "FixedUpdate");
                    scene.GetPhysicsScene().Simulate(Time.fixedDeltaTime);
                }
                var distance = ship.Position.x - startShip;
                Assert.That(distance, Is.GreaterThan(1f));
                Assert.That(actor.Position.x - ship.Position.x, Is.EqualTo(startLocal).Within(.08f), "parent motion and velocity must not transport the actor twice");
                Assert.That(100 - game.power, Is.EqualTo(distance / game.tuning.routeDistance * game.tuning.routePowerCost).Within(.001f));
                game.power = 0;
                var stopped = ship.Position.x;
                Tick(ship, "FixedUpdate"); Tick(actor, "FixedUpdate"); scene.GetPhysicsScene().Simulate(Time.fixedDeltaTime);
                Assert.That(ship.Position.x, Is.EqualTo(stopped).Within(.001f));
                Assert.That(ship.speed, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnderwaterWinchIgnoresOnlyTheShipAndRestoresItsCollisionAfterBoarding()
        {
            var scene = NewPhysicsScene("LifeLineWinch_" + Guid.NewGuid());
                var gameObject = Keep(new GameObject("Session")); SceneManager.MoveGameObjectToScene(gameObject, scene);
                var game = gameObject.AddComponent<GameSession>(); game.enabled = false; game.autoStart = false;
                game.tuning = Keep(ScriptableObject.CreateInstance<GameTuning>());
                var shipObject = Keep(new GameObject("Ship")); SceneManager.MoveGameObjectToScene(shipObject, scene);
                var ship = shipObject.AddComponent<ShipMotor>(); ship.enabled = false; game.ship = ship; ship.Initialize(game);
                var floor = Keep(GameObject.CreatePrimitive(PrimitiveType.Cube)); SceneManager.MoveGameObjectToScene(floor, scene);
                floor.transform.SetParent(ship.transform);
                floor.transform.localPosition = new Vector3(0, -4.2f, 0);
                floor.transform.localScale = new Vector3(24, .4f, 4);
                var actorObject = Keep(new GameObject("Diver")); SceneManager.MoveGameObjectToScene(actorObject, scene);
                var collider = actorObject.AddComponent<CapsuleCollider>(); collider.height = 1.8f;
                var actor = actorObject.AddComponent<CrewActor>(); actor.enabled = false; game.diver = actor;
                actor.Initialize(game, CrewRole.Diver, new Vector3(0, .9f, 0));
                actor.SetOnboard(false, new Vector3(0, -5.5f, 0));
                var shipCollider = floor.GetComponent<Collider>();
                Assert.That(Physics.GetIgnoreCollision(collider, shipCollider), Is.True);
                Physics.SyncTransforms();
                actor.MoveToward(new Vector3(0, -2, 0), 3.5f);
                Assert.That(actor.Position.y, Is.EqualTo(-2f).Within(.001f), "the cabin floor cannot trap the diver below the ship");
                var wall = Keep(GameObject.CreatePrimitive(PrimitiveType.Cube)); SceneManager.MoveGameObjectToScene(wall, scene);
                wall.transform.position = new Vector3(2, -2, 0); wall.transform.localScale = new Vector3(.4f, 6, 4);
                Physics.SyncTransforms();
                actor.MoveToward(new Vector3(4, -2, 0), 4f);
                Assert.That(actor.Position.x, Is.InRange(.5f, 1.6f), "the same winch must stop before solid world geometry");
                actor.SetOnboard(true, new Vector3(-3, .9f, 0));
                Assert.That(Physics.GetIgnoreCollision(collider, shipCollider), Is.False);
            yield return null;
        }
        private sealed class ConstantInput : ICrewInputSource
        {
            public CrewCommand command;
            public CrewCommand ReadCommand() => command;
            public void SetEnabled(bool value) { }
        }
    }

}
