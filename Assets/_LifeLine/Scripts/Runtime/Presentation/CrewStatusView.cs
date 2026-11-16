using UnityEngine;
namespace Lifeline
{
    public sealed class CrewStatusView : MonoBehaviour
    {
        public CrewActor actor;
        public Transform dotsRoot;
        public Material dotMaterial;
        public float spacing=.23f, height=1.05f;
        Material generated;
        Transform[] dots=new Transform[2];
        void OnDestroy(){if(generated!=null)Destroy(generated);}
        void LateUpdate()
        {
            if(actor==null)actor=GetComponent<CrewActor>();
            if(actor==null||actor.session==null)return;
            int count=actor.role==CrewRole.Diver&&!actor.onboard&&!actor.downed&&!actor.session.diverFainted?Mathf.Max(0,actor.session.GetMaxDiverBites()-actor.session.diverBites):0;
            if(dotsRoot==null){dotsRoot=new GameObject("Remaining hit dots / replaceable").transform;dotsRoot.SetParent(transform,false);}
            for(int i=0;i<2;i++)
            {
                if(dots[i]==null)
                {
                    var obj=GameObject.CreatePrimitive(PrimitiveType.Sphere);obj.name="Hit dot";obj.GetComponent<Collider>().enabled=false;
                    dots[i]=obj.transform;dots[i].SetParent(dotsRoot,false);dots[i].localScale=Vector3.one*.13f;
                    if(dotMaterial==null){dotMaterial=generated=new Material(Shader.Find("Universal Render Pipeline/Unlit"));dotMaterial.color=new Color(.2f,1,.42f);}
                    obj.GetComponent<Renderer>().sharedMaterial=dotMaterial;
                }
                dots[i].gameObject.SetActive(i<count);dots[i].localPosition=new Vector3((i-(count-1)*.5f)*spacing,height,-1.5f);
            }
        }
    }
}
