using UnityEngine;

namespace Lifeline
{
    /// <summary>Geometric enemy with real hit colliders, telegraphed attacks and bounded pursuit.</summary>
    public class LifelineEnemy : MonoBehaviour
    {
        [Header("依赖与状态 / inspector-visible enemy state")]
        public GameSession session;
        public EnemyKind kind;
        public EnemyState state;
        public Rigidbody body;
        public Collider hitCollider;
        public Transform visualRoot;
        public float health;
        public float maxHealth;
        public int shadowHitCount;
        public float stateTime;
        public float pursuitTime;
        public float attackCooldown;
        public float reacquireTime;
        public Vector3 home;
        public Vector3 spawnPoint;
        public bool variantApplied;
        public Vector3 targetPoint;
        public Vector3 facing = Vector3.right;

        [Header("碰撞与巡游 / metres")]
        public LayerMask geometryMask = ~0;
        public float hitRadius = .65f;
        public float collisionRadius = .28f;
        public float patrolRadius = 4f;
        public float birdSpeed = 3f;
        public float birdAttackDistance = 1.6f;
        public float sharkAttackDistance = 1.2f;
        public float sharkWarningDistance = 2.2f;
        public float sharkLungeSeconds = .38f;
        public float shadowAttackDistance = 7.5f;
        public float damagedRetreatSeconds = 1.2f;
        public float shadowRetreatSeconds = 6f;
        public float destroyDelay = 1f;
        public float damageFlash;
        public bool IsAlive => state != EnemyState.Defeated && health > 0;
        public bool IsAirborne => kind == EnemyKind.Bird || kind == EnemyKind.ArmoredBird;
        public bool IsShark => kind == EnemyKind.Shark || kind == EnemyKind.WhiteShark;
        Vector3 originalVisualScale;
        Renderer visualRenderer;
        Color baseColor;

        public void Initialize(GameSession owner, EnemyKind type)
        {
            session = owner; kind = type; home = spawnPoint = transform.position; state = EnemyState.Patrol;
            variantApplied = type == EnemyKind.WhiteShark;
            health = maxHealth = type == EnemyKind.WhiteShark ? owner.tuning.whiteSharkHealth : type == EnemyKind.ArmoredBird ? owner.tuning.armoredBirdHealth : type == EnemyKind.Shadow ? owner.tuning.shadowHitsRequired : type == EnemyKind.Shark ? owner.tuning.sharkHealth : owner.tuning.birdHealth;
            hitRadius = type == EnemyKind.Shadow ? 2.8f : IsAirborne ? .5f : 1;
            body = GetComponent<Rigidbody>(); if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false; body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            hitCollider = GetComponent<Collider>();
            if (hitCollider == null) hitCollider = gameObject.AddComponent<SphereCollider>();
            hitCollider.isTrigger = true;
            SphereCollider sphere = hitCollider as SphereCollider; if (sphere != null) sphere.radius = hitRadius;
            Transform existing = transform.Find("VisualRoot");
            visualRoot = existing != null ? existing : BuildVisual();
            originalVisualScale = visualRoot.localScale;
            visualRenderer = visualRoot.GetComponentInChildren<Renderer>();
            Material material = kind == EnemyKind.Shadow ? owner.shadowMaterial : kind == EnemyKind.WhiteShark ? owner.whiteSharkMaterial : IsAirborne ? owner.birdMaterial : owner.sharkMaterial;
            if (visualRenderer != null)
            {
                if (material != null) visualRenderer.sharedMaterial = material;
                else
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) shader = Shader.Find("Standard");
                    if (shader != null)
                    {
                        Material placeholder = new Material(shader);
                        placeholder.color = kind == EnemyKind.Shadow ? new Color(.14f, .05f, .2f) : kind == EnemyKind.WhiteShark ? new Color(.8f, .88f, .9f) : IsAirborne ? new Color(.25f, .2f, .24f) : new Color(.25f, .45f, .52f);
                        visualRenderer.sharedMaterial = placeholder;
                    }
                }
                baseColor = visualRenderer.sharedMaterial != null ? visualRenderer.sharedMaterial.color : Color.white;
            }
        }

        Transform BuildVisual()
        {
            Renderer renderer = GetComponent<Renderer>(); if (renderer != null) renderer.enabled = false;
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "VisualRoot"; visual.transform.SetParent(transform, false);
            Collider collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            }
            visual.transform.localScale = kind == EnemyKind.Shadow ? new Vector3(7, 2.4f, 1.6f) : IsAirborne ? new Vector3(1.1f, .5f, .65f) : new Vector3(2.4f, .8f, .8f);
            return visual.transform;
        }

        void Update()
        {
            if (session == null || session.phase != SessionPhase.Playing || !IsAlive) return;
            float dt = Time.deltaTime;
            if (kind == EnemyKind.Shark && !variantApplied && session.day >= session.tuning.mutationStartDay && spawnPoint.x >= session.tuning.deepBoundary)
            {
                variantApplied = true; kind = EnemyKind.WhiteShark; health = maxHealth = session.tuning.whiteSharkHealth;
                if (visualRenderer != null && session.whiteSharkMaterial != null) visualRenderer.sharedMaterial = session.whiteSharkMaterial;
            }
            stateTime += dt; attackCooldown = Mathf.Max(0, attackCooldown - dt); reacquireTime = Mathf.Max(0, reacquireTime - dt); damageFlash = Mathf.Max(0, damageFlash - dt);
            if (kind == EnemyKind.Shadow) TickShadow(dt);
            else if (IsAirborne) TickBird(dt);
            else TickShark(dt);
            if (visualRoot != null)
            {
                float pulse = state == EnemyState.Telegraph ? 1 + .13f * Mathf.Sin(stateTime * 25) : 1;
                visualRoot.localScale = originalVisualScale * pulse;
                if (facing.x != 0) visualRoot.localRotation = Quaternion.Euler(0, facing.x < 0 ? 180 : 0, 0);
            }
        }

        void TickShark(float dt)
        {
            CrewActor diver = session.diver;
            float surface = session.world != null ? session.world.WaterSurface : -1.5f;
            bool available = diver != null && !diver.onboard && !diver.downed && !session.diverFainted && surface - diver.Position.y >= session.tuning.sharkMinimumDepth;
            float distance = available ? Vector3.Distance(transform.position, diver.Position) : float.MaxValue;
            float detection = session.IsNight ? session.tuning.sharkNightDetectionRange : session.tuning.sharkDetectionRange;
            if (session.diverHidden) detection *= .4f;
            if (session.baitTime > 0 && Vector3.Distance(transform.position, session.baitPosition) < detection * 1.5f)
            {
                targetPoint = session.baitPosition;
                Move(targetPoint, session.tuning.sharkPatrolSpeed * dt);
                if (state != EnemyState.Patrol) ChangeState(EnemyState.Patrol);
                reacquireTime = Mathf.Max(reacquireTime, .5f); return;
            }
            if (state == EnemyState.Retreat)
            {
                Move(home, session.tuning.sharkPatrolSpeed * 1.5f * dt);
                if (stateTime >= damagedRetreatSeconds) ChangeState(EnemyState.Patrol);
                return;
            }
            if (!available || distance > detection * 1.7f)
            {
                if (state == EnemyState.Pursuit || state == EnemyState.Telegraph || state == EnemyState.Lunge) GiveUp();
                else if (state != EnemyState.Patrol) ChangeState(EnemyState.Patrol);
            }
            if (state == EnemyState.Pursuit || state == EnemyState.Telegraph || state == EnemyState.Lunge)
            {
                pursuitTime += dt;
                if (pursuitTime >= session.tuning.sharkPursuitSeconds || !available) { GiveUp(); return; }
            }
            if (state == EnemyState.Patrol)
            {
                Vector3 patrol = home + new Vector3(Mathf.Sin(session.elapsed * .33f + home.x) * patrolRadius, Mathf.Sin(session.elapsed * .21f) * .6f, 0);
                Move(patrol, session.tuning.sharkPatrolSpeed * dt);
                if (available && distance <= detection && reacquireTime <= 0 && HasLineOfSight(diver.Position)) ChangeState(EnemyState.Detecting);
            }
            else if (state == EnemyState.Detecting && stateTime >= session.tuning.sharkDetectDelay) { pursuitTime = 0; ChangeState(EnemyState.Pursuit); }
            else if (state == EnemyState.Telegraph && stateTime >= session.tuning.sharkWindup) ChangeState(EnemyState.Lunge);
            else if (state == EnemyState.Pursuit)
            {
                targetPoint = diver.Position;
                float speed = session.tuning.playerSwimSpeed * session.GetMoveMultiplier(diver) * session.tuning.sprintMultiplier;
                Move(targetPoint, speed * dt);
                if (Vector3.Distance(transform.position, diver.Position) <= sharkWarningDistance && attackCooldown <= 0)
                {
                    attackCooldown = session.tuning.sharkBiteCooldown;
                    // Hold the point before the windup. The diver can dodge instead of the lunge tracking them forever.
                    targetPoint = diver.Position; ChangeState(EnemyState.Telegraph);
                }
            }
            else if (state == EnemyState.Lunge)
            {
                float speed = session.tuning.playerSwimSpeed * session.GetMoveMultiplier(diver) * session.tuning.sprintMultiplier;
                Move(targetPoint, speed * dt);
                if (Vector3.Distance(transform.position, diver.Position) <= sharkAttackDistance)
                {
                    session.RegisterSharkBite(); GiveUp();
                }
                else if (stateTime >= sharkLungeSeconds) ChangeState(EnemyState.Pursuit);
            }
        }

        void GiveUp()
        {
            reacquireTime = session.tuning.sharkReacquireDelay;
            home = transform.position + new Vector3(-Mathf.Sign(facing.x) * 2, -1, 0);
            ChangeState(EnemyState.Retreat);
        }

        void TickBird(float dt)
        {
            if (!session.IsNight) { Move(transform.position + Vector3.up * 20, birdSpeed * dt); if (transform.position.y > session.ShipPoint(Vector3.up * 14).y) Destroy(gameObject); return; }
            Vector3 attackPoint = session.ShipPoint(new Vector3(0, 1.2f, 0));
            if (state == EnemyState.Telegraph)
            {
                if (stateTime >= .55f)
                {
                    session.DamageHull(session.tuning.birdHullDamage);
                    CrewActor actor = session.captain;
                    if (actor != null && actor.onboard && Vector3.Distance(actor.Position, transform.position) < 2) actor.ReceiveDamage(12);
                    ChangeState(EnemyState.Retreat);
                }
            }
            else if (state == EnemyState.Retreat)
            {
                Move(transform.position + new Vector3(facing.x * 10, 10, 0), birdSpeed * dt);
                if (stateTime > 3) Destroy(gameObject);
            }
            else
            {
                Move(attackPoint, birdSpeed * dt);
                if (Vector3.Distance(transform.position, attackPoint) < birdAttackDistance) ChangeState(EnemyState.Telegraph);
            }
        }

        void TickShadow(float dt)
        {
            if (!session.IsNight || state == EnemyState.Retreat)
            {
                if (state != EnemyState.Retreat) ChangeState(EnemyState.Retreat);
                Move(transform.position + Vector3.down * 20, session.tuning.playerSwimSpeed * dt);
                if (stateTime > shadowRetreatSeconds) Destroy(gameObject);
                return;
            }
            CrewActor diver = session.diver;
            targetPoint = diver != null && !diver.onboard && !diver.downed ? diver.Position : session.BoatAnchor + Vector3.down * 2;
            float speed = session.tuning.playerSwimSpeed * session.tuning.shadowSpeedMultiplier;
            if (state == EnemyState.Telegraph)
            {
                if (stateTime >= session.tuning.shadowWindup)
                {
                    if (Vector3.Distance(transform.position, session.BoatAnchor) <= shadowAttackDistance + 3) session.DamageHull(session.tuning.shadowSweepHullDamage);
                    if (diver != null && !diver.onboard && Vector3.Distance(transform.position, diver.Position) <= shadowAttackDistance)
                    { session.ReceiveDiverHit("深渊巨影横扫", false); }
                    attackCooldown = session.tuning.shadowAttackInterval; ChangeState(EnemyState.Pursuit);
                }
            }
            else
            {
                Move(targetPoint, speed * dt);
                bool nearTarget = Vector3.Distance(transform.position, targetPoint) <= shadowAttackDistance;
                if (nearTarget && attackCooldown <= 0) ChangeState(EnemyState.Telegraph);
            }
        }

        public void Alert(Vector3 position)
        {
            targetPoint = position;
            if (IsShark && state == EnemyState.Patrol && reacquireTime <= 0) ChangeState(EnemyState.Detecting);
        }

        public void ReceiveHit(float damage, CrewActor source)
        {
            if (!IsAlive || session == null || session.phase != SessionPhase.Playing) return;
            damageFlash = .2f;
            if (kind == EnemyKind.Shadow)
            {
                shadowHitCount++; session.shadowHits = shadowHitCount;
                health = Mathf.Max(0, session.tuning.shadowHitsRequired - shadowHitCount);
                if (shadowHitCount >= session.tuning.shadowHitsRequired) { session.shadowRepelled = true; session.Tell("第20次命中！深渊巨影退回了黑暗，救援航道暂时安全。"); Defeat(); }
            }
            else
            {
                health = Mathf.Max(0, health - Mathf.Max(0, damage));
                if (health <= 0) Defeat();
                else if (IsShark) GiveUp();
            }
        }

        void Defeat()
        {
            state = EnemyState.Defeated;
            if (hitCollider != null) hitCollider.enabled = false;
            if (kind != EnemyKind.Shadow) session.Tell(kind == EnemyKind.WhiteShark ? "变异白鲨已击退。" : IsAirborne ? "海鸟已驱离。" : "鲨鱼已驱离。");
            Destroy(gameObject, destroyDelay);
        }

        bool HasLineOfSight(Vector3 target)
        {
            RaycastHit hit;
            if (!Physics.Linecast(transform.position, target, out hit, geometryMask, QueryTriggerInteraction.Ignore)) return true;
            return hit.collider.GetComponentInParent<CrewActor>() != null;
        }

        void Move(Vector3 target, float distance)
        {
            Vector3 delta = target - transform.position; delta.z = 0;
            if (delta.sqrMagnitude < .00001f) return;
            Vector3 direction = delta.normalized; facing = direction;
            float step = Mathf.Min(distance, delta.magnitude);
            RaycastHit hit;
            if (Physics.SphereCast(transform.position, collisionRadius, direction, out hit, step + .03f, geometryMask, QueryTriggerInteraction.Ignore))
            {
                step = Mathf.Max(0, hit.distance - .03f);
                if (step <= .001f) return;
            }
            Vector3 next = transform.position + direction * step; next.z = 0;
            if (IsShark && session != null)
            {
                float surface = session.world != null ? session.world.WaterSurface : -1.5f;
                next.y = Mathf.Min(next.y, surface - session.tuning.sharkMinimumDepth);
            }
            if (body != null) body.position = next;
            transform.position = next;
        }

        void ChangeState(EnemyState next) { state = next; stateTime = 0; }
    }
}
