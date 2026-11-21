using UnityEngine;

namespace Lifeline
{
    /// <summary>Solid contact with crew only. The weapon trigger stays separate; walls never constrain aquatic AI.</summary>
    public sealed class AquaticCrewCollision : MonoBehaviour
    {
        public LifelineEnemy enemy;
        public CapsuleCollider solid;
        public LayerMask crewLayers = 1 << 9;
        public void Initialize(LifelineEnemy owner)
        {
            enemy=owner;
            if(solid==null)
            {
                var root=new GameObject("Crew body collision / independent of weapon trigger");
                root.transform.SetParent(owner.transform,false);root.layer=owner.gameObject.layer;
                solid=root.AddComponent<CapsuleCollider>();
            }
            solid.direction=0; solid.isTrigger=false;
            float scale=owner.kind==EnemyKind.Piranha?owner.session.tuning.piranhaScaleMultiplier:owner.SizeMultiplier;
            solid.radius=(owner.kind==EnemyKind.Shadow?1.2f:.4f)*scale;
            solid.height=(owner.kind==EnemyKind.Shadow?7f:2.4f)*scale;
            solid.excludeLayers=~crewLayers.value;
        }
        public bool Touches(CrewActor actor) => DistanceTo(actor) <= .15f;

        public float DistanceTo(CrewActor actor)
        {
            if(solid==null||actor==null)return float.PositiveInfinity;
            var capsule=actor.GetComponent<CapsuleCollider>();if(capsule==null)return float.PositiveInfinity;
            Vector3 onEnemy=solid.ClosestPoint(capsule.bounds.center);
            return Vector3.Distance(onEnemy,capsule.ClosestPoint(onEnemy));
        }
        public Vector3 LimitMove(Vector3 direction,float step)
        {
            float allowed=step;
            foreach(var hit in enemy.body.SweepTestAll(direction,step+.015f,QueryTriggerInteraction.Ignore))
                if(hit.collider!=null&&hit.collider.GetComponentInParent<CrewActor>()!=null)
                    allowed=Mathf.Min(allowed,Mathf.Max(0,hit.distance-.015f));
            return enemy.transform.position+direction*allowed;
        }
    }
}
