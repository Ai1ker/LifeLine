using UnityEngine;
namespace Lifeline
{
    public sealed class SpearTetherView : MonoBehaviour
    {
        public LifelineSpear spear;
        public LineRenderer line;
        public Material material;
        public float width=.025f;
        public Color color=new Color(.9f,.83f,.6f);
        Material generated;
        void LateUpdate()
        {
            if(spear==null)spear=GetComponent<LifelineSpear>();
            if(spear==null||spear.turretShot||spear.owner==null){if(line!=null)line.enabled=false;return;}
            if(line==null){line=new GameObject("Reusable spear tether / replaceable").AddComponent<LineRenderer>();line.transform.SetParent(transform,false);line.useWorldSpace=true;line.positionCount=2;
                if(material==null){generated=new Material(Shader.Find("Sprites/Default"));material=generated;}line.sharedMaterial=material;}
            line.startWidth=line.endWidth=width;line.startColor=line.endColor=color;
            Vector3 a=spear.RecallPoint,b=transform.position;a.z=b.z=-1.6f;line.SetPosition(0,a);line.SetPosition(1,b);
        }
        void OnDestroy(){if(generated!=null)Destroy(generated);}
    }
}
