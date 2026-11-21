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
        public Color lightningColor=new Color(1,.77f,.38f), shadowColor=new Color(1,.42f,.43f);
        [Min(.1f)] public float lightningSeconds = .36f;
        public SpriteRenderer impactSprite;
        public int segments=48;
        Material generated;
        public Transform waterShotVisual;
        LineRenderer outline, bolt, boltGlow, fork;
        float flashTime;
        readonly Vector3[] boltPoints = new Vector3[9];
        void LateUpdate()
        {
            if (bolt != null) { AnimateLightning(); return; }
            if(strike==null||session==null)return;
            Vector3 center=strike.onShipMap?session.WaterToShipPoint(strike.point):strike.point;center.z=-2.2f;
            transform.position=center;
            if (outline == null) outline = Create("Dark backing", .15f);
            if(boundary==null)boundary=Create("Fixed impact boundary");if(countdown==null)countdown=Create("Countdown", .095f);
            Color color=strike.kind==AreaStrikeKind.Lightning?lightningColor:shadowColor;
            if(strike.kind==AreaStrikeKind.WaterShot)
            {
                if(waterShotVisual==null){var drop=GameObject.CreatePrimitive(PrimitiveType.Sphere);drop.GetComponent<Collider>().enabled=false;waterShotVisual=drop.transform;waterShotVisual.SetParent(transform,false);waterShotVisual.localScale=Vector3.one*.3f;}
                waterShotVisual.localPosition=Vector3.up*(strike.remaining/Mathf.Max(.01f,strike.duration))*8;
            }
            float remaining = Mathf.Clamp01(strike.remaining/Mathf.Max(.01f,strike.duration));
            Draw(outline, strike.radius, 1, new Color(.10f,.06f,.14f,.90f));
            Draw(boundary,strike.radius,1,new Color(color.r,color.g,color.b,.75f));
            Draw(countdown,strike.radius*.83f,remaining,color);
        }
        public void Complete()
        {
            if (strike == null || strike.kind != AreaStrikeKind.Lightning || !Application.isPlaying)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }
            if (bolt != null) return;
            Vector3 center = session.WaterToShipPoint(strike.point); center.z = -2.2f;
            transform.position = center;
            if (outline != null) outline.enabled = false;
            if (boundary != null) boundary.enabled = false;
            if (countdown != null) countdown.enabled = false;
            boltGlow = Create("Lightning outer glow", .26f);
            bolt = Create("Lightning core", .10f);
            fork = Create("Lightning fork", .065f);
            if (impactSprite != null) impactSprite.enabled = true;
            float direction = Mathf.Sin(strike.point.x * 33.7f) < 0 ? -1f : 1f;
            for (int i = 0; i < boltPoints.Length - 1; i++)
            {
                float x = direction * ((i % 2 == 0 ? -.48f : .55f) + .16f * (i - 4));
                boltPoints[i] = new Vector3(x, (boltPoints.Length - i - 1) * 1.25f, 0);
            }
            boltPoints[boltPoints.Length - 1] = Vector3.zero;
            AnimateLightning();
        }

        void AnimateLightning()
        {
            float t = flashTime / Mathf.Max(.1f, lightningSeconds);
            flashTime += Time.deltaTime;
            if (t >= 1) { Destroy(gameObject); return; }
            int pointCount = Mathf.Clamp(2 + Mathf.FloorToInt(t * 52), 2, boltPoints.Length);
            bolt.positionCount = boltGlow.positionCount = pointCount;
            for (int i = 0; i < pointCount; i++) { bolt.SetPosition(i, boltPoints[i]); boltGlow.SetPosition(i, boltPoints[i]); }
            float flash = t < .34f ? 1f : t < .51f ? .45f : 1f;
            float alpha = (1 - t) * flash;
            bolt.startColor = bolt.endColor = new Color(.94f,.98f,1f,alpha);
            boltGlow.startColor = boltGlow.endColor = new Color(.39f,.66f,1f,alpha * .55f);
            fork.enabled = pointCount >= 5;
            fork.positionCount = 3;
            fork.SetPosition(0, boltPoints[3]);
            fork.SetPosition(1, boltPoints[3] + new Vector3(.9f,-1.1f,0));
            fork.SetPosition(2, boltPoints[3] + new Vector3(.75f,-2.45f,0));
            fork.startColor = fork.endColor = new Color(.69f,.82f,1f,alpha * .82f);
            if (impactSprite != null) { var tint = impactSprite.color; tint.a = alpha; impactSprite.color = tint; }
        }

        LineRenderer Create(string name, float width = .065f)
        {
            var obj=new GameObject(name);obj.transform.SetParent(transform,false);var line=obj.AddComponent<LineRenderer>();
            line.useWorldSpace=false;line.startWidth=line.endWidth=width;line.numCapVertices=2;
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
