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
        public bool drawMeleeWithCustomPoses;
        public bool rotateSwimmingSprite;
        [Min(0)] public float swimmingTurnSpeed = 360f;
        [Min(0)] public float paddlePoseSeconds = .28f, harpoonPoseSeconds = .28f, speargunPoseSeconds = .32f;
        [Min(0)] public float actionBlendSeconds = .025f;
        public float swingSeconds=.25f;
        public LineRenderer meleeStroke;
        public Material meleeMaterial;
        float swingTimer;
        float swingReach;
        Vector2 swingFacing;
        Vector2 actionFacing;
        float actionTime, swimmingAngle;
        float easedFootSpeed;
        float easedClimbRate;
        bool walking;
        bool wasDownedOnDeck;
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
        private Vector2 lastAnimatedPosition;
        private bool hadAnimatedPosition, wasOnboard;

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
            bool lying = actor.downed || actor.session != null && actor.session.IsFainted(actor);
            actionTime = lying ? 0f : Mathf.Max(0, actionTime - Time.deltaTime);
            Vector2 facing = actionTime > 0 ? actionFacing : actor.Facing;
            if (mirrorFacing && visualRoot != null && Mathf.Abs(facing.x) > .05f)
                visualRoot.localScale = new Vector3(Mathf.Abs(baseScale.x) * Mathf.Sign(facing.x), baseScale.y, baseScale.z);
            if(placeholderPoses&&visualRoot!=null)
            {
                float angle = !actor.onboard ? (actor.Facing.x < 0 ? 90 : -90) : lying ? 90 : 0;
                visualRoot.localRotation=Quaternion.Euler(0,0,angle);
            }
            else if (rotateSwimmingSprite && visualRoot != null)
            {
                float sign = Mathf.Sign(visualRoot.localScale.x);
                if (actor.onboard) swimmingAngle = 0;
                else if (!lying)
                {
                    float angle = Vector2.SignedAngle(Vector2.right, facing * sign);
                    swimmingAngle = Mathf.MoveTowardsAngle(swimmingAngle, angle, swimmingTurnSpeed * Time.deltaTime);
                }
                visualRoot.localRotation = Quaternion.Euler(0, 0, swimmingAngle);
            }
            DrawMelee();
            Vector2 motion = AnimationVelocity();
            float walkingSpeed = actor.onboard ? Mathf.Abs(motion.x) : motion.magnitude;
            SetFloat(speedParameter, walkingSpeed);
            easedFootSpeed = Mathf.Lerp(easedFootSpeed, walkingSpeed, 1 - Mathf.Exp(-Time.deltaTime * 22f));
            walking = easedFootSpeed > (walking ? .16f : .28f);
            SetBool("Moving", walking);
            bool climbing = actor.onboard && actor.IsClimbing && !lying;
            float climbRate = climbing ? Mathf.Clamp(motion.y / Mathf.Max(.01f, actor.ladderSpeed), -1.4f, 1.4f) : 0;
            easedClimbRate = Mathf.MoveTowards(easedClimbRate, climbRate, Time.deltaTime * 22f);
            SetBool("Climbing", climbing);
            SetFloat("ClimbRate", easedClimbRate);
            SetBool(swimmingParameter, !actor.onboard);
            SetBool(groundedParameter, actor.Grounded);
            SetBool(downedParameter, lying);
            SetBool("PixelAction", actionTime > 0);
            bool downedOnDeck = lying && actor.onboard;
            if (downedOnDeck && !wasDownedOnDeck && animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
            {
                int pose = Animator.StringToHash(animator.GetLayerName(0) + ".Downed");
                if (animator.HasState(0, pose)) animator.Play(pose, 0, 0);
            }
            wasDownedOnDeck = downedOnDeck;
        }

        Vector2 AnimationVelocity()
        {
            if (actor.isActiveAndEnabled || actor.session == null || actor.session.ship == null)
            {
                Vector2 local = actor.body != null ? (Vector2)actor.body.linearVelocity : Vector2.zero;
                if (actor.onboard && actor.session != null && actor.session.ship != null) local.x -= actor.session.ship.speed;
                return local;
            }
            // A guest discards local physics velocities and follows the host's interpolated pose.
            Vector2 position = actor.Position;
            if (actor.onboard) position -= (Vector2)actor.session.ship.Position;
            Vector2 movement = hadAnimatedPosition && wasOnboard == actor.onboard ? position - lastAnimatedPosition : Vector2.zero;
            lastAnimatedPosition = position;
            hadAnimatedPosition = true;
            wasOnboard = actor.onboard;
            return movement.sqrMagnitude <= 4f ? movement / Mathf.Max(.001f, Time.deltaTime) : Vector2.zero;
        }

        void DrawMelee()
        {
            swingTimer = Mathf.Max(0, swingTimer - Time.deltaTime);
            bool visible = (placeholderPoses || drawMeleeWithCustomPoses) && swingTimer > 0 && (actor.session == null || !actor.session.IsHidden(actor));
            if (meleeStroke == null && visible)
            {
                meleeStroke = new GameObject("Melee swing / replaceable").AddComponent<LineRenderer>();
                meleeStroke.transform.SetParent(actor.transform, false);
                meleeStroke.numCapVertices = 3;
                meleeStroke.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (meleeMaterial == null)
                {
                    generatedMeleeMaterial = new Material(Shader.Find("Sprites/Default"));
                    meleeMaterial = generatedMeleeMaterial;
                }
                meleeStroke.sharedMaterial = meleeMaterial;
            }
            if (meleeStroke == null) return;
            meleeStroke.enabled = visible;
            if (!visible) return;

            const int segments = 16;
            float progress = 1f - swingTimer / Mathf.Max(.01f, swingSeconds);
            float lastAngle = Mathf.Lerp(-MeleeArc.HalfAngle, MeleeArc.HalfAngle, Mathf.Clamp01(progress * 2f));
            Vector3 center = actor.transform.position + Vector3.back * 1.8f;
            meleeStroke.useWorldSpace = true;
            meleeStroke.positionCount = segments + 3;
            meleeStroke.startWidth = meleeStroke.endWidth = .055f;
            meleeStroke.startColor = meleeStroke.endColor = new Color(1f, .76f, .32f, Mathf.Clamp01(swingTimer * 12f));
            meleeStroke.SetPosition(0, center);
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(-MeleeArc.HalfAngle, lastAngle, i / (float)segments);
                meleeStroke.SetPosition(i + 1, center + (Vector3)(MeleeArc.Rotate(swingFacing, angle) * swingReach));
            }
            meleeStroke.SetPosition(segments + 2, center);
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
            if (kind == CrewVisualEvent.Melee)
            {
                swingTimer = swingSeconds;
                swingFacing = source.MeleeFacing;
                swingReach = source.session != null && source.session.tuning != null ? source.session.MeleeReach : 1.95f;
            }
            if (kind == CrewVisualEvent.Melee || kind == CrewVisualEvent.Fire)
            {
                if (!PlayPixelAction(source, kind) && Has(attackTrigger, AnimatorControllerParameterType.Trigger))
                    animator.SetTrigger(attackTrigger);
            }
            if (eventSinks == null) return;
            foreach (var component in eventSinks)
                if (component is ICrewPresentationSink sink) sink.OnCrewPresentationEvent(source, kind, source.Position, strength);
        }

        bool PlayPixelAction(CrewActor source, CrewVisualEvent kind)
        {
            if (!Has("PixelAction", AnimatorControllerParameterType.Bool) || !animator.isActiveAndEnabled) return false;
            bool fire = kind == CrewVisualEvent.Fire;
            bool harpoon = source.session != null && source.session.hasHarpoon;
            string action = fire ? "Speargun" : harpoon ? "Harpoon" : "Paddle";
            if (!source.onboard) action += "Swim";
            int state = Animator.StringToHash(animator.GetLayerName(0) + "." + action);
            if (!animator.HasState(0, state)) return false;
            actionFacing = fire ? source.SpeargunFacing : source.MeleeFacing;
            actionTime = fire ? speargunPoseSeconds : harpoon ? harpoonPoseSeconds : paddlePoseSeconds;
            SetBool("PixelAction", true);
            animator.CrossFadeInFixedTime(state, actionBlendSeconds, 0, 0);
            return true;
        }
    }
}
