using UnityEngine;

namespace Lifeline
{
    public class LifelineSpear : MonoBehaviour
    {
        [Header("可召回射矛 / actual swept collision projectile")]
        public GameSession session;
        public CrewActor owner;
        public Vector3 direction;
        public LifelineEnemy target;
        public bool turretShot;
        [Tooltip("Which independent gameplay map contains this projectile. Owner coordinates are mapped before recall.")]
        public bool onShipMap;
        [Tooltip("Mounted gun projectiles leave their own hull freely; world ruins still stop shots.")]
        public bool ignoreShipColliders;
        public bool returning;
        public bool lodged;
        public bool hasHit;
        public float damage = 2;
        public float age;
        public float maximumRange=10f, travelled;
        public float collisionRadius = .1f;
        public float returnMultiplier = 1.7f;
        public LayerMask collisionMask = ~0;

        public Vector3 RecallPoint => owner == null ? transform.position : session == null || owner.onboard == onShipMap
            ? owner.Position : onShipMap ? session.WaterToShipPoint(owner.Position) : session.ShipToWaterPoint(owner.Position);

        void Update()
        {
            if (session == null || owner == null) { Destroy(gameObject); return; }
            if (session.phase != SessionPhase.Playing) return;
            float dt = Time.deltaTime; age += dt;
            if (returning)
            {
                transform.SetParent(null, true);
                Vector3 recallPoint = RecallPoint;
                Vector3 towardOwner = recallPoint - transform.position;
                if (towardOwner.sqrMagnitude > .001f) transform.rotation = Quaternion.FromToRotation(Vector3.up, towardOwner.normalized);
                transform.position = Vector3.MoveTowards(transform.position, recallPoint, session.tuning.spearSpeed * returnMultiplier * dt);
                if (Vector3.Distance(transform.position, recallPoint) < .5f) Destroy(gameObject);
                return;
            }
            if (age > session.tuning.spearLifetime)
            {
                if (turretShot) Destroy(gameObject); else Recall();
                return;
            }
            if(!turretShot && (Vector3.Distance(transform.position,RecallPoint)>=maximumRange-.001f || travelled>=maximumRange)){Recall();return;}
            if (lodged) return;
            if (turretShot && target != null) direction = (target.transform.position - transform.position).normalized;
            if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            Vector3 step = direction.normalized * session.tuning.spearSpeed * dt;
            if(!turretShot)
            {
                Vector3 relative=transform.position-RecallPoint;
                float dot=Vector3.Dot(relative,direction.normalized);
                float edge=-dot+Mathf.Sqrt(Mathf.Max(0,dot*dot+maximumRange*maximumRange-relative.sqrMagnitude));
                step=direction.normalized*Mathf.Min(step.magnitude,Mathf.Max(0,Mathf.Min(edge,maximumRange-travelled)));
            }
            travelled+=step.magnitude;
            RaycastHit[] hits = Physics.SphereCastAll(transform.position, collisionRadius, direction.normalized, step.magnitude, collisionMask, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.collider.GetComponentInParent<CrewActor>() != null || hit.collider.GetComponentInParent<LifelineSpear>() != null) continue;
                if (ignoreShipColliders && hit.collider.GetComponentInParent<ShipMotor>() == session.ship) continue;
                LifelineEnemy enemy = hit.collider.GetComponentInParent<LifelineEnemy>();
                if (enemy != null)
                {
                    if (!enemy.IsAlive) continue;
                    if (!hasHit)
                    {
                        hasHit = true;
                        if (!turretShot && enemy.kind != EnemyKind.ArmoredBird)
                            GameAudio.At(session, AudioCue.SpearHitFlesh, hit.point, onShipMap);
                        enemy.ReceiveHit(!turretShot && enemy.IsAirborne ? damage * session.AirborneWeaponMultiplier(owner) : damage, owner);
                    }
                    transform.position = hit.point;
                    if (turretShot) Destroy(gameObject);
                    else { lodged = true; transform.SetParent(enemy.transform, true); }
                    return;
                }
                if (hit.collider.isTrigger) continue;
                if (!turretShot) GameAudio.At(session, AudioCue.SpearHitMetal, hit.point, onShipMap);
                transform.position = hit.point;
                if (turretShot) Destroy(gameObject); else lodged = true;
                return;
            }
            transform.position += step;
            if(!turretShot && (travelled>=maximumRange-.001f||Vector3.Distance(transform.position,RecallPoint)>=maximumRange-.001f))Recall();
        }

        public void Recall()
        {
            if (turretShot || returning) return;
            GameAudio.At(session, AudioCue.SpearRecall, transform.position, onShipMap);
            returning = true; lodged = false; transform.SetParent(null, true);
        }
        void OnDestroy()
        {
            if (session != null && owner != null && session.activeSpears[(int)owner.role] == this) session.activeSpears[(int)owner.role] = null;
        }
    }
}
