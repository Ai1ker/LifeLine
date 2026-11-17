using UnityEngine;
namespace Lifeline
{
    public sealed class UnderwaterHideSpot : MonoBehaviour
    {
        public WorldGenerator world;
        public Transform visualRoot;
        public Material material;
        public float range=1.8f;
        public TextMesh label;
        Material generated;
        void OnDestroy(){if(generated!=null)Destroy(generated);}
        void Start()
        {
            if(visualRoot==null)
            {
                visualRoot=new GameObject("Hide spot visual / replaceable").transform;visualRoot.SetParent(transform,false);
                for(int i=0;i<5;i++)
                {
                    var reed=GameObject.CreatePrimitive(PrimitiveType.Cube);reed.GetComponent<Collider>().enabled=false;reed.transform.SetParent(visualRoot,false);
                    reed.transform.localPosition=new Vector3((i-2)*.25f,-.3f,-.5f);reed.transform.localScale=new Vector3(.16f,.5f+(i%3)*.23f,.2f);
                    if(material==null){material=generated=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.color=new Color(.1f,.65f,.48f);}reed.GetComponent<Renderer>().sharedMaterial=material;
                }
            }
            if(label==null){label=new GameObject("Hide label").AddComponent<TextMesh>();label.transform.SetParent(transform,false);label.transform.localPosition=new Vector3(0,.7f,-1.5f);label.anchor=TextAnchor.MiddleCenter;label.fontSize=40;label.characterSize=.08f;label.font=RuntimeUIFont.GetOrCreateFont();label.GetComponent<Renderer>().sharedMaterial=label.font.material;}
        }
        void Update()
        {
            if(label==null||world==null||world.session==null)return;
            CrewActor focus=null;float distance=4;
            foreach(var actor in world.session.Crew)
                if(actor!=null&&!actor.onboard&&Vector2.Distance(actor.Position,transform.position)<distance){focus=actor;distance=Vector2.Distance(actor.Position,transform.position);}
            label.gameObject.SetActive(focus!=null&&(world.session.IsHidden(focus)||world.GetLabelFocus(focus)==transform));
            label.text=UiLocalization.Text(focus!=null&&world.session.IsHidden(focus)?"藏匿中 · 互动离开":"藏匿点 · 互动躲藏");
        }
    }
}
