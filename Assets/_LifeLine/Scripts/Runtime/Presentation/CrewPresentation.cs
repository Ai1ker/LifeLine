using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    [DisallowMultipleComponent]
    public sealed class CrewPresentation : MonoBehaviour
    {
        public CrewActor actor;
        public Transform visualRoot;
        public Animator animator;
        [Tooltip("Optional sound/VFX receivers implementing ICrewPresentationSink.")]
        public MonoBehaviour[] eventSinks;
        public bool mirrorFacing = true;
        public float movementDamping = .1f;
        [Header("Optional Animator parameters")]
        public string speedParameter = "Speed";
        public string swimmingParameter = "Swimming", groundedParameter = "Grounded", downedParameter = "Downed";
        public string attackTrigger = "AttackTrigger";
        private readonly Dictionary<int, AnimatorControllerParameterType> parameters = new Dictionary<int, AnimatorControllerParameterType>();
        private CrewActor subscribedActor;
        private Animator cachedAnimator;
        private RuntimeAnimatorController cachedController;
        private Vector3 baseScale = Vector3.one;
        private Transform cachedVisual;

        private void OnEnable() => Resolve();
        private void OnDisable()
        {
            if (subscribedActor != null) subscribedActor.PresentationEvent -= OnPresentationEvent;
            subscribedActor = null;
        }

        private void Resolve()
        {
            if (actor == null) actor = GetComponentInParent<CrewActor>();
            if (actor != subscribedActor)
            {
                if (subscribedActor != null) subscribedActor.PresentationEvent -= OnPresentationEvent;
                subscribedActor = actor;
                if (subscribedActor != null) subscribedActor.PresentationEvent += OnPresentationEvent;
            }
            if (visualRoot == null && actor != null) visualRoot = actor.visualRoot;
            if (animator == null && actor != null) animator = actor.animator;
            if (visualRoot != cachedVisual)
            { cachedVisual = visualRoot; baseScale = visualRoot != null ? visualRoot.localScale : Vector3.one; }
            if (cachedAnimator != animator || cachedController != (animator != null ? animator.runtimeAnimatorController : null))
            {
                cachedAnimator = animator;
                cachedController = animator != null ? animator.runtimeAnimatorController : null;
                parameters.Clear();
                if (animator != null && animator.runtimeAnimatorController != null)
                    foreach (var parameter in animator.parameters) parameters[parameter.nameHash] = parameter.type;
            }
        }

        private void Update()
        {
            Resolve();
            if (actor == null) return;
            if (mirrorFacing && visualRoot != null && Mathf.Abs(actor.Facing.x) > .05f)
                visualRoot.localScale = new Vector3(Mathf.Abs(baseScale.x) * Mathf.Sign(actor.Facing.x), baseScale.y, baseScale.z);
            SetFloat(speedParameter, actor.RelativeMoveSpeed);
            SetBool(swimmingParameter, !actor.onboard);
            SetBool(groundedParameter, actor.Grounded);
            SetBool(downedParameter, actor.downed);
        }

        private bool Has(string name, AnimatorControllerParameterType type)
        {
            return animator != null && !string.IsNullOrEmpty(name) && parameters.TryGetValue(Animator.StringToHash(name), out var found) && found == type;
        }
        private void SetFloat(string name, float value)
        { if (Has(name, AnimatorControllerParameterType.Float)) animator.SetFloat(name, value, movementDamping, Time.deltaTime); }
        private void SetBool(string name, bool value)
        { if (Has(name, AnimatorControllerParameterType.Bool)) animator.SetBool(name, value); }
        private void OnPresentationEvent(CrewActor source, CrewVisualEvent kind, float strength)
        {
            if ((kind == CrewVisualEvent.Melee || kind == CrewVisualEvent.Fire) && Has(attackTrigger, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(attackTrigger);
            if (eventSinks == null) return;
            foreach (var component in eventSinks)
                if (component is ICrewPresentationSink sink) sink.OnCrewPresentationEvent(source, kind, source.Position, strength);
        }
    }
}
