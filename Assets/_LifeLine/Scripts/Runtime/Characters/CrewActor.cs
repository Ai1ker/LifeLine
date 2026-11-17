using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lifeline
{
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class CrewActor : MonoBehaviour
    {
        public CrewRole role;
        public GameSession session;
        public CrewInputReader input;
        public Rigidbody body;
        public Transform visualRoot;
        public Animator animator;
        public bool onboard = true, downed, controlsCaptured;
        public bool hidden, fainted;
        public Vector3 hidingPosition;
        bool visibleBeforeHiding;
        public float oxygen, oxygenMax;
        public float health = 100f, maxHealth = 100f;
        public List<ResourceKind> backpack = new List<ResourceKind>();
        [Header("Physics / metres")]
        public LayerMask groundMask = ~0;
        public float groundProbe = .13f;
        public float ladderHalfWidth = 1.15f;
        public float ladderSpeed = 3.8f;
        public float deckY = 0f, cabinY = -4f;
        public float waterSurfaceY = -1.5f;
        public float waterBottomY = -54f;
        public float leftWorldLimit = -22f, rightWorldLimit = 222.22223f;
        public float collisionSkin = .035f;

        public Vector2 Facing { get; private set; } = Vector2.right;
        public bool Grounded { get; private set; }
        public bool IsAtLadder { get; private set; }
        public bool IsClimbing { get; private set; }
        public Vector3 Position => body != null ? body.position : transform.position;
        public int BagCapacity => session != null ? session.GetBagCapacity(this) : 8;
        public CrewCommand LastCommand { get; private set; }
        public float RelativeMoveSpeed => body == null ? 0f : onboard
            ? Mathf.Abs(body.linearVelocity.x - (session != null && session.ship != null ? session.ship.speed : 0f))
            : new Vector2(body.linearVelocity.x, body.linearVelocity.y).magnitude;
        public StationAnchor NearbyStation
        {
            get
            {
                if (!onboard || downed) return null;
                if (Time.unscaledTime >= nextStationScan)
                {
                    nextStationScan = Time.unscaledTime + .5f;
                    stations = FindObjectsByType<StationAnchor>(FindObjectsSortMode.None);
                }
                StationAnchor closest = null;
                var distance = float.PositiveInfinity;
                foreach (var station in stations)
                {
                    if (station == null || !station.CanInteract(this)) continue;
                    var candidate = (station.InteractionPosition - Position).sqrMagnitude;
                    if (candidate < distance) { closest = station; distance = candidate; }
                }
                return closest;
            }
        }
        public event Action<CrewActor, CrewVisualEvent, float> PresentationEvent;

        private ICrewInputSource externalInput;
        private CapsuleCollider capsule;
        private StationAnchor[] stations = Array.Empty<StationAnchor>();
        private readonly RaycastHit[] groundHits = new RaycastHit[16];
        private Collider[] ignoredShipColliders = Array.Empty<Collider>();
        private PhysicsMaterial frictionless;
        private float nextStationScan, nextJumpAt;
        private float frozenZ;

        private void Awake() => ConfigureBody();

        private void ConfigureBody()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            if (body == null) return;
            body.isKinematic = false;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.mass = 1f;
            if (capsule != null)
            {
                if (capsule.height < 1.2f) capsule.height = 1.6f;
                capsule.radius = .32f;
                capsule.direction = 1;
                frictionless = new PhysicsMaterial("Crew frictionless") { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum, bounciness = 0f };
                capsule.sharedMaterial = frictionless;
            }
            frozenZ = transform.position.z;
        }

        public void Initialize(GameSession game, CrewRole crewRole, Vector3 spawn)
        {
            SetHidden(false);
            fainted=false; session = game;
            role = crewRole;
            if (body == null) ConfigureBody();
            if (input == null) input = GetComponent<CrewInputReader>();
            if (input == null) input = gameObject.AddComponent<CrewInputReader>();
            input.Initialize(role, input.actions);
            health = maxHealth;
            oxygenMax = session != null && session.tuning != null ? session.tuning.baseOxygen : 60f;
            oxygen = oxygenMax;
            backpack.Clear();
            downed = false;
            controlsCaptured = false;
            Facing = Vector2.right;
            nextJumpAt = 0f;
            nextStationScan = 0f;
            SetOnboard(true, spawn);
        }

        /// <summary>Restores a saved pose through the same ship-collision transition used during play.</summary>
        public void RestorePose(bool isOnboard, Vector3 position, Vector3 velocity, Vector2 facing)
        {
            SetOnboard(isOnboard, position);
            Facing = facing.sqrMagnitude > .001f ? facing.normalized : Vector2.right;
            if (body != null) body.linearVelocity = velocity;
            controlsCaptured = false; LastCommand = default;
            Grounded = false; IsAtLadder = IsClimbing = false; nextJumpAt = 0; nextStationScan = 0;
            if (input != null) { input.SetEnabled(false); input.SetEnabled(true); }
        }

        public void SetExternalInput(ICrewInputSource source)
        {
            if (externalInput != null && externalInput != source) externalInput.SetEnabled(false);
            externalInput = source;
            if (input != null) input.SetEnabled(source == null);
            source?.SetEnabled(true);
        }

        private CrewCommand ReadCommand()
        {
            var source = externalInput ?? (ICrewInputSource)input;
            var command = source != null ? source.ReadCommand() : new CrewCommand();
            command.role = role;
            LastCommand = command;
            return command;
        }

        private void Update()
        {
            // Paused UI still receives Escape, even when the app pauses scaled physics time.
            if (session != null && session.phase != SessionPhase.Playing)
                session.HandleCommand(this, ReadCommand());
        }

        private void FixedUpdate()
        {
            if (session == null || session.phase != SessionPhase.Playing || body == null)
            {
                if (body != null) body.linearVelocity = Vector3.zero;
                return;
            }
            var command = ReadCommand();
            session.HandleCommand(this, command);
            if (session.phase != SessionPhase.Playing) { body.linearVelocity = Vector3.zero; return; }
            if(session.IsHidden(this)) { body.position=hidingPosition;transform.position=hidingPosition;body.linearVelocity=Vector3.zero;return; }
            var blocked = downed || controlsCaptured || session.IsActorInputBlocked(this);
            var atHelm = session.ship != null && session.ship.helmOperator == this;
            if (atHelm) session.ship.Drive(blocked ? 0f : command.move.x);
            var movement = blocked || atHelm ? Vector2.zero : Vector2.ClampMagnitude(command.move, 1f);
            if (movement.sqrMagnitude > .01f)
                Facing = onboard ? new Vector2(Mathf.Sign(Mathf.Abs(movement.x) > .01f ? movement.x : Facing.x), 0f) : movement.normalized;
            if (onboard) MoveOnShip(movement, command.jump && !blocked && !atHelm);
            else Swim(movement, command.sprint && !blocked);

        }

        private void MoveOnShip(Vector2 movement, bool jump)
        {
            var tuning = session.tuning;
            var ship = session.ship;
            var shipVelocity = ship != null ? ship.speed : 0f;
            var ladderX = ship != null ? ship.LocalToWorld(Vector3.zero).x : 0f;
            var shipY = ship != null ? ship.Position.y : 0f;
            var worldDeckY = shipY + deckY;
            var worldCabinY = shipY + cabinY;
            IsAtLadder = Mathf.Abs(Position.x - ladderX) <= ladderHalfWidth && Position.y >= worldCabinY - .2f && Position.y <= worldDeckY + 1.8f;
            var wasGrounded = Grounded;
            Grounded = ProbeGround();
            if (!wasGrounded && Grounded) EmitPresentationEvent(CrewVisualEvent.Land);

            if (IsAtLadder && Mathf.Abs(movement.y) > .2f) IsClimbing = true;
            if (!IsAtLadder || Mathf.Abs(movement.x) > .15f || downed || controlsCaptured) IsClimbing = false;
            if (IsClimbing)
            {
                var climbingUp = movement.y > 0f;
                var foot = capsule != null ? capsule.bounds.min.y : Position.y - .8f;
                if (climbingUp && foot >= worldDeckY - .08f || movement.y < 0f && foot <= worldCabinY + .05f)
                {
                    var landingFloor = climbingUp ? worldDeckY : worldCabinY;
                    var footOffset = Position.y - foot;
                    var landing = Position;
                    landing.y = landingFloor + footOffset + .035f;
                    body.position = landing;
                    transform.position = landing;
                    IsClimbing = false;
                    body.linearVelocity = new Vector3(shipVelocity, -1f, 0f);
                }
                else
                {
                    // Ladder colliders are triggers. Clamp toward its centre without changing the physics root hierarchy.
                    body.linearVelocity = new Vector3(shipVelocity + Mathf.Clamp((ladderX - Position.x) * 6f, -3f, 3f), movement.y * ladderSpeed, 0f);
                    return;
                }
            }

            var vertical = body.linearVelocity.y - (tuning != null ? tuning.gravity : 20f) * Time.fixedDeltaTime;
            if (Grounded && vertical < 0f) vertical = -1f;
            if (jump && Grounded && !IsAtLadder && Time.time >= nextJumpAt)
            {
                vertical = tuning != null ? tuning.jumpSpeed : 7f;
                nextJumpAt = Time.time + .18f;
                Grounded = false;
                EmitPresentationEvent(CrewVisualEvent.Jump);
            }
            var moveSpeed = (tuning != null ? tuning.playerShipSpeed : 5f) * session.GetMoveMultiplier(this);
            var horizontal = movement.x * moveSpeed + shipVelocity;
            if (ship != null)
            {
                var local = Position.x - ship.Position.x;
                if (local <= -11.4f && movement.x < 0f || local >= 11.4f && movement.x > 0f) horizontal = shipVelocity;
            }
            body.linearVelocity = new Vector3(horizontal, vertical, 0f);
        }

        private bool ProbeGround()
        {
            if (capsule == null) return false;
            var bounds = capsule.bounds;
            var origin = new Vector3(bounds.center.x, bounds.min.y + .24f, frozenZ);
            var count = gameObject.scene.GetPhysicsScene().SphereCast(origin, .16f, Vector3.down, groundHits, groundProbe + .24f, groundMask, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (hit.collider == null || hit.collider.attachedRigidbody == body || hit.normal.y < .45f) continue;
                if (hit.collider.GetComponentInParent<CrewActor>() != null) continue;
                return true;
            }
            return false;
        }

        private void Swim(Vector2 movement, bool sprint)
        {
            Grounded = false;
            IsAtLadder = IsClimbing = false;
            var tuning = session.tuning;
            var velocity = movement * (tuning != null ? tuning.playerSwimSpeed : 2.1f) * session.GetMoveMultiplier(this);
            if (sprint) velocity *= tuning != null ? tuning.sprintMultiplier : 1.6f;
            if (Position.y >= waterSurfaceY - .3f && velocity.y > 0f) velocity.y = 0f;
            if (Position.y <= waterBottomY && velocity.y < 0f) velocity.y = 0f;
            if (Position.x <= leftWorldLimit && velocity.x < 0f || Position.x >= rightWorldLimit && velocity.x > 0f) velocity.x = 0f;
            body.linearVelocity = new Vector3(velocity.x, velocity.y, 0f);
            session.ApplyDiverLeash(this);
        }

        public void SetOnboard(bool value, Vector3 worldPosition)
        {
            if(value)SetHidden(false);
            var changed = onboard != value;
            onboard = value;
            IsClimbing = IsAtLadder = false;
            // Parent transforms never transport this dynamic body; ship velocity is added once in MoveOnShip.
            if (transform.parent != null && transform.parent.GetComponentInParent<ShipMotor>() != null) transform.SetParent(null, true);
            worldPosition.z = frozenZ;
            if (body != null) { body.position = worldPosition; body.linearVelocity = Vector3.zero; body.WakeUp(); }
            transform.position = worldPosition;
            SetShipCollision(!value);
            if (session != null && session.maps != null) session.maps.MoveCrewToMap(this);
            session?.IgnoreCrewCollision();
            if (changed) EmitPresentationEvent(value ? CrewVisualEvent.Board : CrewVisualEvent.Dive);
        }

        private void SetShipCollision(bool ignore)
        {
            if (capsule == null) capsule = GetComponent<CapsuleCollider>();
            if (capsule == null) return;
            foreach (var shipCollider in ignoredShipColliders)
                if (shipCollider != null && shipCollider != capsule) Physics.IgnoreCollision(capsule, shipCollider, false);
            ignoredShipColliders = Array.Empty<Collider>();
            if (!ignore || session == null || session.ship == null) return;
            ignoredShipColliders = session.ship.GetComponentsInChildren<Collider>(true);
            foreach (var shipCollider in ignoredShipColliders)
                if (shipCollider != null && shipCollider != capsule) Physics.IgnoreCollision(capsule, shipCollider, true);
        }

        /// <summary>Collision-aware winch translation; only GameSession decides when captain-authorized boarding occurs.</summary>
        public void MoveToward(Vector3 target, float maxDistance)
        {
            if (body == null || maxDistance <= 0f) return;
            target.z = frozenZ;
            var delta = target - body.position;
            var distance = Mathf.Min(maxDistance, delta.magnitude);
            if (distance < .0001f) return;
            var direction = delta.normalized;
            foreach (var hit in body.SweepTestAll(direction, distance + collisionSkin, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && (hit.collider.GetComponentInParent<CrewActor>() != null || hit.collider.GetComponentInParent<LifelineEnemy>() != null)) continue;
                if (hit.collider == null || !onboard && session != null && session.ship != null && hit.collider.transform.IsChildOf(session.ship.transform)) continue;
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - collisionSkin));
            }
            if (distance > 0f)
            {
                var next = body.position + direction * distance;
                body.position = next;
                transform.position = next;
            }
            body.linearVelocity = Vector3.zero;
            EmitPresentationEvent(CrewVisualEvent.Winched, distance);
        }

        public void SetHidden(bool value)
        {
            if(hidden==value)return;
            if(value){hidingPosition=Position;visibleBeforeHiding=visualRoot!=null&&visualRoot.gameObject.activeSelf;}
            hidden=value;
            if(visualRoot!=null)visualRoot.gameObject.SetActive(value?false:visibleBeforeHiding);
            if(body!=null)body.linearVelocity=Vector3.zero;
            if(session!=null&&this==session.diver){session.diverHidden=value;if(value)session.diverSprinting=false;}
            if(session!=null&&this==session.captain&&value)session.captainSprinting=false;
        }

        public void SetDowned(bool value)
        {
            if (downed == value) return;
            downed = value;
            IsClimbing = false;
            if (value && body != null) body.linearVelocity = Vector3.zero;
            EmitPresentationEvent(value ? CrewVisualEvent.Downed : CrewVisualEvent.Revived);
        }

        public void Revive()
        {
            fainted=false;
            health = Mathf.Max(health, maxHealth * .5f);
            oxygen = Mathf.Max(oxygen, oxygenMax * .5f);
            SetDowned(false);
        }

        public void ReceiveDamage(float amount)
        {
            if (amount <= 0f || downed || (session!=null?session.IsFainted(this)||session.IsHidden(this):fainted)) return;
            if (role == CrewRole.Captain)
            {
                // Captain always has base hit capacity; suit upgrades only apply to the diver.
                health = Mathf.Max(0, health - maxHealth * amount / (session != null ? session.tuning.baseDiverHitCapacity : 2));
                if (health <= 0) { if (!onboard && session != null) session.FaintCrew(this, "船长受伤"); else SetDowned(true); }
                EmitPresentationEvent(CrewVisualEvent.Hit, amount); return;
            }
            if (role == CrewRole.Diver && session != null)
            {
                if (session.ReceiveDiverHit("水下受击", false, amount)) EmitPresentationEvent(CrewVisualEvent.Hit, amount);
                return;
            }
            health = Mathf.Max(0f, health - amount);
            EmitPresentationEvent(CrewVisualEvent.Hit, amount);
            if (health <= 0f) SetDowned(true);
        }

        public void EmitPresentationEvent(CrewVisualEvent kind, float strength = 1f) => PresentationEvent?.Invoke(this, kind, strength);
        private void OnDestroy()
        {
            SetShipCollision(false);
            if (frictionless == null) return;
            if (Application.isPlaying) Destroy(frictionless);
            else DestroyImmediate(frictionless);
        }
    }
}
