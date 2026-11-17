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
        public int shadowAttackIndex, shadowPhase;
        public float shadowAttackClock;
        public Vector3 shadowAim;
        public float stateTime;
        public float pursuitTime;
        public float attackCooldown;
        public float reacquireTime;
        public Vector3 home;
        public Vector3 spawnPoint;
        public bool variantApplied;
        public bool caveGuard, damagedRetreat;
        public Vector3 fleeDirection;
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
        public bool IsAquaticHunter => IsShark || kind == EnemyKind.Piranha;
        public float AttackRange => kind == EnemyKind.Piranha ? session.tuning.piranhaAttackDistance : sharkAttackDistance;
        public float WarningRange => kind == EnemyKind.Piranha ? session.tuning.piranhaWarningDistance : sharkWarningDistance;
        public float AttackWindup => kind == EnemyKind.Piranha ? session.tuning.piranhaWindup : session.tuning.sharkWindup;
        public float PatrolSpeed => kind == EnemyKind.Piranha ? session.tuning.piranhaPatrolSpeed : session.tuning.sharkPatrolSpeed;
        public float ChaseSpeed => kind == EnemyKind.Piranha ? session.tuning.piranhaChaseSpeed : session.tuning.sharkChaseSpeed;
        public float DetectionRadius => session.IsNight && !session.LightEffective ? session.tuning.sharkNightDetectionRange : session.tuning.sharkDetectionRange;
        Vector3 originalVisualScale;
        Renderer visualRenderer;
        Color baseColor;

        public void Initialize(GameSession owner, EnemyKind type)
        {
            session = owner; kind = type; home = spawnPoint = transform.position; state = EnemyState.Patrol;
            variantApplied = type == EnemyKind.WhiteShark;
            health = maxHealth = type == EnemyKind.WhiteShark ? owner.tuning.whiteSharkHealth : type == EnemyKind.ArmoredBird ? owner.tuning.armoredBirdHealth : type == EnemyKind.Shadow ? owner.tuning.shadowHitsRequired : type == EnemyKind.Shark ? owner.tuning.sharkHealth : type == EnemyKind.Piranha ? 1 : owner.tuning.birdHealth;
            hitRadius = type == EnemyKind.Shadow ? 2.8f : IsAirborne ? .5f : type == EnemyKind.Piranha ? owner.tuning.piranhaScaleMultiplier : 1;
            damagedRetreatSeconds = owner.tuning.sharkHitRetreatSeconds;
            damagedRetreat = false; fleeDirection = Vector3.zero;
            body = GetComponent<Rigidbody>(); if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false; body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            hitCollider = GetComponent<Collider>();
            if (hitCollider == null) hitCollider = gameObject.AddComponent<SphereCollider>();
            hitCollider.isTrigger = true;
            SphereCollider sphere = hitCollider as SphereCollider; if (sphere != null) sphere.radius = hitRadius;
            Transform existing = transform.Find("VisualRoot");
            visualRoot = existing != null ? existing : BuildVisual();
            if (type == EnemyKind.Piranha) visualRoot.localScale *= owner.tuning.piranhaScaleMultiplier;
            originalVisualScale = visualRoot.localScale;
            if (type == EnemyKind.Piranha) collisionRadius *= owner.tuning.piranhaScaleMultiplier;
            visualRenderer = visualRoot.GetComponentInChildren<Renderer>();
            Material material = kind == EnemyKind.Piranha ? owner.piranhaMaterial : kind == EnemyKind.Shadow ? owner.shadowMaterial : kind == EnemyKind.WhiteShark ? owner.whiteSharkMaterial : IsAirborne ? owner.birdMaterial : owner.sharkMaterial;
            if (visualRenderer != null)
            {
                if (material != null) visualRenderer.sharedMaterial = material;
                else
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) shader = Shader.Find("Standard");
                    if (shader != null)
                    {
                        Material placeholder = new Material(shader);
                        placeholder.color = kind == EnemyKind.Piranha ? new Color(.95f,.4f,.25f) : kind == EnemyKind.Shadow ? new Color(.14f, .05f, .2f) : kind == EnemyKind.WhiteShark ? new Color(.8f, .88f, .9f) : IsAirborne ? new Color(.25f, .2f, .24f) : new Color(.25f, .45f, .52f);
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

        void Update() => StepEnemy(Time.deltaTime);
        public void StepEnemy(float dt)
        {
            if (session == null || session.phase != SessionPhase.Playing || !IsAlive || dt <= 0) return;
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
            CrewActor diver = session.FindWaterTarget(transform.position);
            float surface = session.world != null ? session.world.WaterSurface : -1.5f;
            bool available = diver != null && !diver.onboard && !diver.downed && !session.IsFainted(diver) && surface - diver.Position.y >= 0;
            float distance = available ? Vector3.Distance(transform.position, diver.Position) : float.MaxValue;
            float detection = DetectionRadius;
            if (state == EnemyState.Retreat)
            {
                if (damagedRetreat)
                {
                    if (available) { var away = transform.position - diver.Position; away.z = 0; if (away.sqrMagnitude > .001f) fleeDirection = away.normalized; }
                    if (fleeDirection.sqrMagnitude < .001f) fleeDirection = Vector3.right;
                    Move(transform.position + fleeDirection * 10, ChaseSpeed * dt);
                }
                else Move(home, PatrolSpeed * 1.5f * dt);
                if (stateTime >= (damagedRetreat ? session.tuning.sharkHitRetreatSeconds : 1.2f))
                {
                    reacquireTime = damagedRetreat ? 0 : session.tuning.sharkReacquireDelay; damagedRetreat = false;
                    if (!caveGuard) home = transform.position;
                    ChangeState(EnemyState.Patrol);
                }
                return;
            }
            if (session.baitTime > 0 && Vector3.Distance(transform.position, session.baitPosition) < detection * 1.5f)
            {
                targetPoint = session.baitPosition;
                Move(targetPoint, PatrolSpeed * dt);
                if (state != EnemyState.Patrol) ChangeState(EnemyState.Patrol);
                reacquireTime = Mathf.Max(reacquireTime, .5f); return;
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
                Move(patrol, PatrolSpeed * dt);
                if (available && distance <= detection && reacquireTime <= 0 && HasLineOfSight(diver.Position)) ChangeState(EnemyState.Detecting);
            }
            else if (state == EnemyState.Detecting && stateTime >= session.tuning.sharkDetectDelay) { pursuitTime = 0; ChangeState(EnemyState.Pursuit); }
            else if (state == EnemyState.Telegraph && stateTime >= AttackWindup) ChangeState(EnemyState.Lunge);
            else if (state == EnemyState.Pursuit)
            {
                targetPoint = diver.Position;
                float speed = ChaseSpeed;
                Move(targetPoint, speed * dt);
                if (Vector3.Distance(transform.position, diver.Position) <= WarningRange && attackCooldown <= 0)
                {
                    attackCooldown = session.tuning.sharkBiteCooldown;
                    // Hold the point before the windup. The diver can dodge instead of the lunge tracking them forever.
                    targetPoint = diver.Position; ChangeState(EnemyState.Telegraph);
                }
            }
            else if (state == EnemyState.Lunge)
            {
                float speed = ChaseSpeed;
                Move(targetPoint, speed * dt);
                if (Vector3.Distance(transform.position, diver.Position) <= AttackRange)
                {
                    if (kind == EnemyKind.Piranha) diver.ReceiveDamage(session.tuning.piranhaDamage);
                    else if (diver == session.diver) session.RegisterSharkBite(); else diver.ReceiveDamage(1);
                    GiveUp();
                }
                else if (stateTime >= sharkLungeSeconds) ChangeState(EnemyState.Pursuit);
            }
        }

        void GiveUp()
        {
            damagedRetreat = false; reacquireTime = session.tuning.sharkReacquireDelay;
            if (!caveGuard) home = transform.position + new Vector3(-Mathf.Sign(facing.x) * 2, -1, 0);
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
            CrewActor diver=session.FindWaterTarget(transform.position);
            float length=diver!=null&&diver.GetComponent<CapsuleCollider>()!=null?diver.GetComponent<CapsuleCollider>().height*diver.transform.lossyScale.y:1.8f;
            float normal=session.tuning.playerSwimSpeed*session.tuning.shadowSpeedMultiplier;
            Vector3 target=diver!=null&&!diver.onboard&&!diver.downed?diver.Position:session.BoatAnchor+Vector3.down*2;
            if(shadowPhase==0)
            {
                Move(target,normal*dt);
                float reach=shadowAttackIndex==1?shadowAttackDistance:length*session.tuning.shadowBiteRadiusMultiplier+1;
                if(attackCooldown>0||Vector2.Distance(transform.position,target)>reach)return;
                shadowAim=target;shadowAttackClock=0;shadowPhase=1;ChangeState(EnemyState.Telegraph);
                if(shadowAttackIndex==0)
                {
                    Vector3 center=transform.position+(target-transform.position).normalized*length*.5f;
                    session.QueueAreaStrike(AreaStrikeKind.ShadowBite,center,false,length*session.tuning.shadowBiteRadiusMultiplier,session.tuning.shadowWindup,Random.Range(session.tuning.shadowMeleeHullDamage.x,session.tuning.shadowMeleeHullDamage.y));
                }
                else if(shadowAttackIndex==1)
                    session.QueueAreaStrike(AreaStrikeKind.ShadowDash,shadowAim,false,length*session.tuning.shadowDashRadiusMultiplier,session.tuning.shadowWindup+session.tuning.shadowDashSeconds,Random.Range(session.tuning.shadowMeleeHullDamage.x,session.tuning.shadowMeleeHullDamage.y));
                else for(int i=0;i<2;i++)
                {
                    float x=Random.Range(i==0?-10f:1f,i==0?-1f:10f);
                    Vector3 point=session.ShipToWaterPoint(session.ShipPoint(new Vector3(x,.9f,0)));
                    session.QueueAreaStrike(AreaStrikeKind.WaterShot,point,true,session.tuning.lightningRadius,session.tuning.shadowWaterShotWarning,session.tuning.shadowWaterShotDamage);
                }
            }
            shadowAttackClock+=dt;
            if(shadowAttackIndex==1&&shadowAttackClock>=session.tuning.shadowWindup)
            {
                if(shadowPhase==1){shadowPhase=2;ChangeState(EnemyState.Lunge);}
                Move(shadowAim,Mathf.Max(normal*session.tuning.shadowDashSpeedMultiplier,Vector3.Distance(transform.position,shadowAim)/Mathf.Max(.01f,session.tuning.shadowWindup+session.tuning.shadowDashSeconds-shadowAttackClock))*dt);
            }
            float duration=shadowAttackIndex==2?session.tuning.shadowWaterShotWarning:session.tuning.shadowWindup+(shadowAttackIndex==1?session.tuning.shadowDashSeconds:0);
            if(shadowAttackClock>=duration){shadowAttackIndex=(shadowAttackIndex+1)%3;shadowPhase=0;attackCooldown=session.tuning.shadowAttackInterval;ChangeState(EnemyState.Pursuit);}
        }

        public void Alert(Vector3 position)
        {
            targetPoint = position;
            if (IsAquaticHunter && state == EnemyState.Patrol && reacquireTime <= 0) ChangeState(EnemyState.Detecting);
        }

        public void ReceiveHit(float damage, CrewActor source)
        {
            if (!IsAlive || session == null || session.phase != SessionPhase.Playing || damage <= 0) return;
            damageFlash = .2f;
            if (kind == EnemyKind.Shadow)
            {
                shadowHitCount++; session.shadowHits = shadowHitCount;
                health = Mathf.Max(0, session.tuning.shadowHitsRequired - shadowHitCount);
                if (shadowHitCount >= session.tuning.shadowHitsRequired) { session.shadowRepelled = true; session.Tell("第20次命中！深渊巨影退回了黑暗，救援航道暂时安全。"); Defeat(); }
            }
            else
            {
                // Sharks survive a fixed count of successful hits; weapon damage buffs do not bypass the count.
                health = kind == EnemyKind.Piranha ? 0 : Mathf.Max(0, health - (IsShark ? 1 : damage));
                if (health <= 0) Defeat();
                else if (IsShark)
                {
                    Vector3 threat = source != null && !source.onboard ? source.Position : session.BoatAnchor;
                    fleeDirection = transform.position - threat; fleeDirection.z = 0;
                    if (fleeDirection.sqrMagnitude < .001f) fleeDirection = -facing;
                    fleeDirection.Normalize(); damagedRetreat = true; pursuitTime = 0;
                    attackCooldown = Mathf.Max(attackCooldown, session.tuning.sharkHitRetreatSeconds);
                    reacquireTime = session.tuning.sharkHitRetreatSeconds;
                    ChangeState(EnemyState.Retreat);
                }
            }
        }

        void Defeat()
        {
            state = EnemyState.Defeated;
            if (hitCollider != null) hitCollider.enabled = false;
            if (kind != EnemyKind.Shadow) session.Tell(kind == EnemyKind.WhiteShark ? "变异白鲨已击退。" : IsAirborne ? "海鸟已驱离。" : kind == EnemyKind.Piranha ? "食人鱼已击杀。" : "鲨鱼已驱离。");
            if (Application.isPlaying) Destroy(gameObject, destroyDelay);
        }

        bool HasLineOfSight(Vector3 target)
        {
            if (!IsAirborne) return true; // Underwater enemies deliberately pass through architecture.
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
            if (IsAirborne && Physics.SphereCast(transform.position, collisionRadius, direction, out hit, step + .03f, geometryMask, QueryTriggerInteraction.Ignore))
            {
                step = Mathf.Max(0, hit.distance - .03f);
                if (step <= .001f) return;
            }
            Vector3 next = transform.position + direction * step; next.z = 0;
            if (body != null) body.position = next;
            transform.position = next;
        }

        void ChangeState(EnemyState next) { state = next; stateTime = 0; }
    }
}
