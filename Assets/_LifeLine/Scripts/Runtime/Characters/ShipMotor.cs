using UnityEngine;

namespace Lifeline
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ShipMotor : MonoBehaviour
    {
        public GameSession session;
        public Rigidbody body;
        public CrewActor helmOperator;
        public float speed;
        public Vector3 StartPosition { get; private set; }
        public Vector3 Position => body != null ? body.position : transform.position;
        public float westLimit = -8f, eastLimit = 90f;
        private float steering;
        private bool hasStartPosition;

        public void Initialize(GameSession game)
        {
            session = game;
            if (body == null) body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            if (!hasStartPosition) { StartPosition = Position; hasStartPosition = true; }
            else { body.position = StartPosition; transform.position = StartPosition; }
            speed = steering = 0f;
            helmOperator = null;
        }

        public void SetHelm(CrewActor actor)
        {
            if (actor == helmOperator) actor = null;
            if (actor != null && (!actor.onboard || actor.downed)) return;
            helmOperator = actor;
            steering = 0f;
        }

        public void Drive(float axis) { steering = Mathf.Clamp(axis, -1f, 1f); }
        public Vector3 LocalToWorld(Vector3 local) => transform.TransformPoint(local);
        public void TranslateMapOrigin(Vector3 delta)
        {
            Vector3 position = Position + delta;
            if (body != null) body.position = position;
            transform.position = position;
            if (hasStartPosition) StartPosition += delta;
        }

        private void Update()
        {
            if (session != null && session.phase == SessionPhase.Playing) return;
            speed = steering = 0f;
        }

        private void FixedUpdate()
        {
            speed = 0f;
            if (body == null || session == null || session.phase != SessionPhase.Playing) { steering = 0f; return; }
            if (helmOperator == null || !helmOperator.onboard || helmOperator.downed || helmOperator.controlsCaptured)
            { helmOperator = null; steering = 0f; }
            var destination = body.position;
            // Environmental drift is applied once and never consumes propulsion power.
            var driftX = ConstrainX(destination.x + session.GetStormDriftSpeed() * Time.fixedDeltaTime);
            var poweredSpeed = steering * session.tuning.boatSpeed * session.GetBoatSpeedMultiplier();
            destination.x = ConstrainX(driftX + poweredSpeed * Time.fixedDeltaTime);
            var poweredDistance = Mathf.Abs(destination.x - driftX);
            if (poweredDistance > .00001f && !session.TryConsumeSailing(poweredDistance)) destination.x = driftX;
            if (Mathf.Abs(destination.x - body.position.x) < .00001f) return;
            speed = (destination.x - body.position.x) / Time.fixedDeltaTime;
            body.MovePosition(destination);
        }

        private float ConstrainX(float proposed)
        {
            return Mathf.Clamp(session.ClampBoatXToLeash(Mathf.Clamp(proposed, westLimit, eastLimit)), westLimit, eastLimit);
        }

        public void ResetToStart()
        {
            helmOperator = null;
            speed = steering = 0f;
            if (body != null) body.position = StartPosition;
            transform.position = StartPosition;
        }
    }
}
