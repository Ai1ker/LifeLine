using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Lifeline
{
    [DisallowMultipleComponent]
    public sealed class CrewInputReader : MonoBehaviour, ICrewInputSource
    {
        public InputActionAsset actions;
        public CrewRole role;
        public string preferencesPrefix = "Lifeline.Bindings.v1.";
        public event Action BindingsChanged;
        public bool IsRebinding => rebind != null;
        public InputActionAsset RuntimeActions => runtimeActions;
        public IEnumerable<InputAction> BindableActions
        {
            get
            {
                if (runtimeActions == null) Initialize(role, actions);
                return map.actions;
            }
        }

        private InputActionAsset runtimeActions;
        private InputActionMap map;
        private InputAction moveAction, interactAction, collectAction, sprintAction;
        private InputActionRebindingExtensions.RebindingOperation rebind;
        private CrewCommand queued;
        private bool inputEnabled = true;
        private float ignoreUntil;

        public void Initialize(CrewRole crewRole, InputActionAsset sharedAsset)
        {
            DisposeActions();
            role = crewRole;
            actions = sharedAsset;
            runtimeActions = sharedAsset != null ? Instantiate(sharedAsset) : CrewInputDefaults.CreateAsset();
            runtimeActions.hideFlags = HideFlags.DontSave;
            runtimeActions.Disable();
            map = runtimeActions.FindActionMap(role.ToString(), true);
            var saved = PlayerPrefs.GetString(preferencesPrefix + role, "");
            if (!string.IsNullOrEmpty(saved))
            {
                try { runtimeActions.LoadBindingOverridesFromJson(saved); }
                catch (Exception error) { Debug.LogWarning("Could not load " + role + " input overrides: " + error.Message, this); }
            }
            CrewInputDefaults.MigrateInteractionBindings(runtimeActions, true);
            moveAction = map.FindAction("Move", true);
            interactAction = map.FindAction("Interact", true);
            collectAction = map.FindAction("Collect", true);
            sprintAction = map.FindAction("Sprint", true);
            map.actionTriggered += BufferButton;
            ClearQueued();
            if (isActiveAndEnabled && inputEnabled) map.Enable();
        }

        private void OnEnable()
        {
            if (runtimeActions == null && actions != null) Initialize(role, actions);
            else if (map != null && inputEnabled && !IsRebinding) map.Enable();
        }

        private void OnDisable()
        {
            CancelRebind();
            map?.Disable();
            ClearQueued();
        }

        private void OnDestroy() => DisposeActions();

        private void BufferButton(InputAction.CallbackContext context)
        {
            if (context.phase != InputActionPhase.Performed || !inputEnabled || Time.unscaledTime < ignoreUntil) return;
            switch (context.action.name)
            {
                case "Jump": queued.jump = true; break;
                case "Interact": queued.interact = true; break;
                case "Melee": queued.melee = true; break;
                case "Fire": queued.fire = true; break;
                case "Signal": queued.signal = true; break;
                case "Bait": queued.bait = true; break;
                case "Hide": queued.hide = true; break;
                case "Pause": queued.pause = true; break;
            }
        }

        public CrewCommand ReadCommand()
        {
            if (runtimeActions == null) Initialize(role, actions);
            if (!inputEnabled || !isActiveAndEnabled || IsRebinding || Time.unscaledTime < ignoreUntil)
            {
                ClearQueued();
                return queued;
            }
            var command = queued;
            command.role = role;
            command.move = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
            command.collect = collectAction.IsPressed();
            command.interactHeld = interactAction.IsPressed();
            command.sprint = sprintAction.IsPressed();
            ClearQueued();
            return command;
        }

        public void SetEnabled(bool value)
        {
            inputEnabled = value;
            ClearQueued();
            if (map == null) return;
            if (value && isActiveAndEnabled && !IsRebinding) map.Enable();
            else map.Disable();
        }

        /// <summary>A shared UI pause consumes the peer's already-buffered copy without discarding movement or other buttons.</summary>
        public void DiscardPauseEdge() { queued.pause = false; }

        public string GetBindingLabel(string actionName, int bindingIndex = 0)
        {
            var action = map?.FindAction(actionName, false);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) return "未绑定";
            return action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontIncludeInteractions);
        }

        public void BeginRebind(string actionName, int bindingIndex, Action<string> completed)
        {
            if (runtimeActions == null) Initialize(role, actions);
            CancelRebind();
            var action = map.FindAction(actionName, false);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            { completed?.Invoke("绑定不存在"); return; }
            if (action.bindings[bindingIndex].isComposite)
            { completed?.Invoke("请选择上、下、左、右中的一个方向"); return; }
            map.Disable();
            ClearQueued();
            rebind = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithControlsExcluding("<Mouse>")
                .OnMatchWaitForAnother(.1f)
                .OnComplete(operation => FinishRebind(operation, actionName, bindingIndex, completed, true))
                .OnCancel(operation => FinishRebind(operation, actionName, bindingIndex, completed, false));
            if (action.bindings[bindingIndex].isPartOfComposite) rebind.WithExpectedControlType<ButtonControl>();
            rebind.Start();
        }

        public void CancelRebind() { rebind?.Cancel(); }

        private void FinishRebind(InputActionRebindingExtensions.RebindingOperation operation, string actionName,
            int bindingIndex, Action<string> completed, bool success)
        {
            rebind = null;
            operation.Dispose();
            ClearQueued();
            ignoreUntil = Time.unscaledTime + .15f;
            if (success)
                SaveBindings();
            if (isActiveAndEnabled && inputEnabled) map.Enable();
            BindingsChanged?.Invoke();
            completed?.Invoke(success ? GetBindingLabel(actionName, bindingIndex) : "已取消");
        }

        public void SaveBindings()
        {
            if (runtimeActions == null) return;
            PlayerPrefs.SetString(preferencesPrefix + role, runtimeActions.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public void ResetBindings()
        {
            CancelRebind();
            if (runtimeActions == null) Initialize(role, actions);
            runtimeActions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(preferencesPrefix + role);
            PlayerPrefs.Save();
            ClearQueued();
            BindingsChanged?.Invoke();
        }

        private void ClearQueued() { queued = new CrewCommand { role = role }; }
        private void DisposeActions()
        {
            CancelRebind();
            if (map != null) map.actionTriggered -= BufferButton;
            map = null;
            if (runtimeActions != null)
            {
                runtimeActions.Disable();
                if (Application.isPlaying) Destroy(runtimeActions);
                else DestroyImmediate(runtimeActions);
            }
            runtimeActions = null;
        }
    }
}
