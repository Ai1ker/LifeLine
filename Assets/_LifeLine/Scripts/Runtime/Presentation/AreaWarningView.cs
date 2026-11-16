using UnityEngine;
namespace Lifeline
{
    /// <summary>Replace the ring/impact sprite or prefab without changing the fixed damage area.</summary>
    public sealed class AreaWarningView : MonoBehaviour
    {
        public GameSession session;
        public AreaStrike strike;
        public LineRenderer boundary, countdown;
        public SpriteRenderer icon;
        public Material material;
        public Color lightningColor=new Color(1,.7f,.15f), shadowColor=new Color(1,.3f,.35f);
        public int segments=48;
        Material generated;
        public Transform waterShotVisual;
        void LateUpdate()
        {
            if(strike==null||session==null)return;
            Vector3 center=strike.onShipMap?session.WaterToShipPoint(strike.point):strike.point;center.z=-2.2f;
            transform.position=center;
            if(boundary==null)boundary=Create("Fixed impact boundary");if(countdown==null)countdown=Create("Countdown");
            Color color=strike.kind==AreaStrikeKind.Lightning?lightningColor:shadowColor;
            if(strike.kind==AreaStrikeKind.WaterShot)
            {
                if(waterShotVisual==null){var drop=GameObject.CreatePrimitive(PrimitiveType.Sphere);drop.GetComponent<Collider>().enabled=false;waterShotVisual=drop.transform;waterShotVisual.SetParent(transform,false);waterShotVisual.localScale=Vector3.one*.3f;}
                waterShotVisual.localPosition=Vector3.up*(strike.remaining/Mathf.Max(.01f,strike.duration))*8;
            }
            Draw(boundary,strike.radius,1,color);Draw(countdown,strike.radius*.88f,Mathf.Clamp01(strike.remaining/Mathf.Max(.01f,strike.duration)),color);
        }
        LineRenderer Create(string name)
        {
            var obj=new GameObject(name);obj.transform.SetParent(transform,false);var line=obj.AddComponent<LineRenderer>();
            line.useWorldSpace=false;line.startWidth=line.endWidth=.07f;line.numCapVertices=2;
            if(material==null){generated=new Material(Shader.Find("Sprites/Default"));material=generated;}line.sharedMaterial=material;return line;
        }
        void Draw(LineRenderer line,float radius,float portion,Color color)
        {
            line.startColor=line.endColor=color;line.positionCount=segments+1;
            for(int i=0;i<=segments;i++){float a=i/(float)segments*Mathf.PI*2*portion;line.SetPosition(i,new Vector3(Mathf.Sin(a)*radius,Mathf.Cos(a)*radius,0));}
        }
        void OnDestroy(){if(generated!=null)Destroy(generated);}
    }
}
