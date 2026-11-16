using UnityEngine;
using UnityEngine.UI;
namespace Lifeline
{
    /// <summary>Font-independent geometric ground, power, light and radio symbols. Replace with theme sprites.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RepairSymbolGraphic:MaskableGraphic
    {
        public int symbol;
        VertexHelper mesh;
        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();mesh=v;
            if(symbol==0){Line(.5f,.88f,.5f,.48f);Line(.15f,.48f,.85f,.48f);Line(.26f,.30f,.74f,.30f);Line(.37f,.12f,.63f,.12f);}
            else if(symbol==1){Line(.5f,.15f,.5f,.85f);Line(.15f,.5f,.85f,.5f);}
            else if(symbol==2)
            {
                for(int i=0;i<24;i++){float a=i*Mathf.PI*2/24,b=(i+1)*Mathf.PI*2/24;Line(.5f+Mathf.Sin(a)*.23f,.5f+Mathf.Cos(a)*.23f,.5f+Mathf.Sin(b)*.23f,.5f+Mathf.Cos(b)*.23f);}
                for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Line(.5f+Mathf.Sin(a)*.34f,.5f+Mathf.Cos(a)*.34f,.5f+Mathf.Sin(a)*.46f,.5f+Mathf.Cos(a)*.46f);}
            }
            else for(int i=0;i<22;i++){float a=i/22f,b=(i+1)/22f;Line(.08f+a*.84f,.5f+Mathf.Sin(a*Mathf.PI*4)*.22f,.08f+b*.84f,.5f+Mathf.Sin(b*Mathf.PI*4)*.22f);}
        }
        void Line(float x1,float y1,float x2,float y2)
        {
            var r=rectTransform.rect;Vector2 a=new Vector2(r.xMin+x1*r.width,r.yMin+y1*r.height),b=new Vector2(r.xMin+x2*r.width,r.yMin+y2*r.height);
            Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*Mathf.Max(1,r.height*.04f);
            int i=mesh.currentVertCount;mesh.AddVert(a-n,color,Vector2.zero);mesh.AddVert(a+n,color,Vector2.zero);mesh.AddVert(b+n,color,Vector2.zero);mesh.AddVert(b-n,color,Vector2.zero);mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
        }
    }
}
