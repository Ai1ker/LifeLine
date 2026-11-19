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
        [Min(0)] public float foregroundOffset = .65f;
        public bool placeholderPoses = true;
        public float swingSeconds=.25f;
        public LineRenderer meleeStroke;
        public Material meleeMaterial;
        float swingTimer;
        [Tooltip("Tag the replacement Animator melee state with this tag; it blocks retriggering until the state finishes.")]
        public string meleeStateTag = "Melee";
        public bool MeleeAnimationPlaying => swingTimer > 0 || animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null
            && animator.GetCurrentAnimatorStateInfo(0).IsTag(meleeStateTag)
            && animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1;
        Material generatedMeleeMaterial;
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
        private Vector3 basePosition;
        private readonly Dictionary<SpriteRenderer, int> baseSorting = new();

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
            {
                cachedVisual = visualRoot;
                baseScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
                basePosition = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
                baseSorting.Clear();
                if (visualRoot != null)
                    foreach (var sprite in visualRoot.GetComponentsInChildren<SpriteRenderer>(true)) baseSorting.Add(sprite, sprite.sortingOrder);
            }
            if (cachedAnimator != animator || cachedController != (animator != null ? animator.runtimeAnimatorController : null))
            {
                cachedAnimator = animator;
                cachedController = animator != null ? animator.runtimeAnimatorController : null;
                parameters.Clear();
                if (animator != null && animator.runtimeAnimatorController != null)
                    foreach (var parameter in animator.parameters) parameters[parameter.nameHash] = parameter.type;
            }
            // Cosmetic pixel skin over the placeholder primitives (no gameplay effect).
            if (visualRoot != null && actor != null)
                PixelArtDecor.Apply(visualRoot, PixelArtDecor.CrewSprite(actor.role), 1.3f,
                    hidePlaceholders: true, localOffset: new Vector3(0, 0.05f, -0.35f));
        }

        public void SetFront(bool foreground)
        {
            Resolve();
            if (visualRoot == null) return;
            visualRoot.localPosition = basePosition + (foreground ? Vector3.back * foregroundOffset : Vector3.zero);
            foreach (var entry in baseSorting)
                if (entry.Key != null) entry.Key.sortingOrder = entry.Value + (foreground ? 100 : 0);
        }

        private void Update()
        {
            Resolve();
            if (actor == null) return;
            if (mirrorFacing && visualRoot != null && Mathf.Abs(actor.Facing.x) > .05f)
                visualRoot.localScale = new Vector3(Mathf.Abs(baseScale.x) * Mathf.Sign(actor.Facing.x), baseScale.y, baseScale.z);
            if(placeholderPoses&&visualRoot!=null)
            {
                bool lying=actor.downed||actor.session!=null&&actor.session.IsFainted(actor);
                float angle=lying?90:!actor.onboard?actor.Facing.x<0?90:-90:0;
                visualRoot.localRotation=Quaternion.Euler(0,0,angle);
            }
            DrawMelee();
            SetFloat(speedParameter, actor.RelativeMoveSpeed);
            SetBool(swimmingParameter, !actor.onboard);
            SetBool(groundedParameter, actor.Grounded);
            SetBool(downedParameter, actor.downed);
        }

        void DrawMelee()
        {
            swingTimer=Mathf.Max(0,swingTimer-Time.deltaTime);
            if(meleeStroke==null && swingTimer>0)
            {
                meleeStroke=new GameObject("Melee swing / replaceable").AddComponent<LineRenderer>();meleeStroke.transform.SetParent(actor.transform,false);
                meleeStroke.useWorldSpace=true;meleeStroke.numCapVertices=3;meleeStroke.positionCount=3;
                if(meleeMaterial==null){generatedMeleeMaterial=new Material(Shader.Find("Sprites/Default"));meleeMaterial=generatedMeleeMaterial;}meleeStroke.sharedMaterial=meleeMaterial;
            }
            if(meleeStroke==null)return;
            meleeStroke.enabled=placeholderPoses&&swingTimer>0;
            if(!meleeStroke.enabled)return;
            float sign=actor.Facing.x<0?-1:1;
            float angle=Mathf.Lerp(-65,70,1-swingTimer/Mathf.Max(.01f,swingSeconds))*Mathf.Deg2Rad;
            Vector3 hand=actor.Position+new Vector3(sign*.25f,.1f,-1.8f),tip=hand+new Vector3(sign*Mathf.Cos(angle),Mathf.Sin(angle),0)*1.4f;
            meleeStroke.startWidth=actor.session!=null&&actor.session.hasHarpoon?.07f:.13f;meleeStroke.endWidth=.05f;
            meleeStroke.startColor=meleeStroke.endColor=new Color(1,.76f,.32f);
            meleeStroke.SetPosition(0,hand);meleeStroke.SetPosition(1,tip);meleeStroke.SetPosition(2,tip+new Vector3(-sign*.15f,.1f,0));
        }
        void OnDestroy(){if(generatedMeleeMaterial!=null)Destroy(generatedMeleeMaterial);}
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
            if(kind==CrewVisualEvent.Melee)swingTimer=swingSeconds;
            if ((kind == CrewVisualEvent.Melee || kind == CrewVisualEvent.Fire) && Has(attackTrigger, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(attackTrigger);
            if (eventSinks == null) return;
            foreach (var component in eventSinks)
                if (component is ICrewPresentationSink sink) sink.OnCrewPresentationEvent(source, kind, source.Position, strength);
        }
    }
}
