using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Lifeline.Tests
{
    public class PauseRoutingPlayModeTests
    {
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        Keyboard keyboard;
        GameSession game;
        GameHUDController hud;
        float oldScale;
        InputSettings.UpdateMode oldUpdateMode;
        InputSettings.BackgroundBehavior oldBackgroundBehavior;
#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode oldEditorBehavior;
#endif
        T Keep<T>(T obj) where T : UnityEngine.Object { created.Add(obj); return obj; }
        [SetUp] public void Setup()
        {
            oldScale = Time.timeScale;
            oldUpdateMode = InputSystem.settings.updateMode;
            oldBackgroundBehavior = InputSystem.settings.backgroundBehavior;
#if UNITY_EDITOR
            oldEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
#endif
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            keyboard = InputSystem.AddDevice<Keyboard>();
            game = Keep(new GameObject("Pause routing session")).AddComponent<GameSession>(); game.autoStart = false; game.enabled = false;
            game.phase = SessionPhase.Playing;
            var asset = Keep(CrewInputDefaults.CreateAsset());
            game.captain = Actor(CrewRole.Captain, asset); game.diver = Actor(CrewRole.Diver, asset);
            hud = Keep(new GameObject("Pause routing HUD")).AddComponent<GameHUDController>(); hud.enabled = false; hud.session = game;
            hud.pausePanel = Keep(new GameObject("Pause"));
            hud.captainPanel = Panel(game.captain); hud.diverPanel = Panel(game.diver);
            var settings = Keep(new GameObject("Settings")).AddComponent<GameSettingsPanel>(); settings.panel = settings.gameObject; hud.settings = settings;
            var controls = Keep(new GameObject("Controls")).AddComponent<ControlsRebindPanel>(); controls.panel = controls.gameObject; hud.controls = controls;
            settings.panel.SetActive(false); controls.panel.SetActive(false); hud.Initialize();
            Time.timeScale = 1;
        }
        CrewActor Actor(CrewRole role, InputActionAsset asset)
        {
            var go = Keep(new GameObject(role.ToString())); var a = go.AddComponent<CrewActor>(); a.enabled = false;
            a.role = role; a.session = game; a.input = go.AddComponent<CrewInputReader>();
            a.input.preferencesPrefix = "LifeLine.PauseTests." + Guid.NewGuid() + "."; a.input.Initialize(role, asset); return a;
        }
        CrewStationPanel Panel(CrewActor actor)
        {
            var go = Keep(new GameObject(actor.role + " station")); var p = go.AddComponent<CrewStationPanel>(); p.enabled = false;
            p.panel = go; p.closeButton = Keep(new GameObject("Close", typeof(RectTransform), typeof(Button))).GetComponent<Button>(); return p;
        }
        void Press(params Key[] keys) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); }
        void Send(CrewActor actor) => game.HandleCommand(actor, actor.input.ReadCommand());
        [TearDown] public void Cleanup()
        {
            try
            {
                Time.timeScale = 1;
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            }
            finally
            {
                created.Clear();
                InputSystem.settings.updateMode = oldUpdateMode;
                InputSystem.settings.backgroundBehavior = oldBackgroundBehavior;
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorBehavior;
#endif
                Time.timeScale = oldScale;
            }
        }
        [UnityTest] public IEnumerator SharedEscapeDoesNotRemainBufferedInThePeerAfterPhaseChanges()
        {
            Press(Key.Escape); Send(game.captain);
            Assert.That(game.phase, Is.EqualTo(SessionPhase.Paused)); Assert.That(hud.pausePanel.activeSelf, Is.True);
            Assert.That(game.diver.input.ReadCommand().pause, Is.False, "peer edge must be drained even if its physics tick was skipped");
            yield return null;
            Press(); Press(Key.Escape); Send(game.diver);
            Assert.That(game.phase, Is.EqualTo(SessionPhase.Playing)); Assert.That(hud.pausePanel.activeSelf, Is.False);
            Assert.That(game.captain.input.ReadCommand().pause, Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator EscapeClosesStationBeforePausingAndDismissesSettingsBeforeResuming()
        {
            hud.captainPanel.panel.SetActive(true); game.captain.controlsCaptured = true;
            Press(Key.Escape); Send(game.captain);
            Assert.That(hud.captainPanel.IsOpen, Is.False); Assert.That(game.captain.controlsCaptured, Is.False);
            Assert.That(game.phase, Is.EqualTo(SessionPhase.Playing)); Assert.That(hud.pausePanel.activeSelf, Is.False);
            yield return null;
            hud.OpenSettings(); Assert.That(hud.settings.IsOpen, Is.True); Assert.That(game.phase, Is.EqualTo(SessionPhase.Paused));
            Press(); Press(Key.Escape); Send(game.diver);
            Assert.That(hud.settings.IsOpen, Is.False); Assert.That(game.phase, Is.EqualTo(SessionPhase.Paused));
            yield return null;
            Press(); Press(Key.Escape); Send(game.captain);
            Assert.That(game.phase, Is.EqualTo(SessionPhase.Playing)); Assert.That(hud.pausePanel.activeSelf, Is.False);
        }
        [UnityTest] public IEnumerator EscapeDismissesControlsWithoutSkippingThePauseLayer()
        {
            hud.SetPause(true); hud.controls.panel.SetActive(true);
            Press(Key.Escape); Send(game.diver);
            Assert.That(hud.controls.IsOpen, Is.False); Assert.That(game.phase, Is.EqualTo(SessionPhase.Paused));
            yield return null;
            Press(); Press(Key.Escape); Send(game.captain); Assert.That(game.phase, Is.EqualTo(SessionPhase.Playing));
        }
    }
}
