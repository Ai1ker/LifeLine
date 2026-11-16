using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace Lifeline
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RepairWireGraphic : MaskableGraphic
    {
        public bool[] connected;public Color[] colors;
        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();if(connected==null)return;var r=rectTransform.rect;int[] order={2,0,3,1};
            for(int id=0;id<4;id++)if(connected[id])
            {
                int row=Array.IndexOf(order,id);Vector2 a=new Vector2(r.xMin,r.yMax-(id+.4f)*r.height/4),b=new Vector2(r.xMax,r.yMax-(row+.4f)*r.height/4);
                for(int i=0;i<24;i++)
                {
                    float t=i/24f,u=(i+1)/24f;Vector2 p=Bezier(a,b,t),q=Bezier(a,b,u);Vector2 normal=new Vector2(-(q-p).y,(q-p).x).normalized*2;
                    int n=v.currentVertCount;Color c=colors!=null&&colors.Length>id?colors[id]:Color.green;
                    v.AddVert(p-normal,c,Vector2.zero);v.AddVert(p+normal,c,Vector2.zero);v.AddVert(q+normal,c,Vector2.zero);v.AddVert(q-normal,c,Vector2.zero);v.AddTriangle(n,n+1,n+2);v.AddTriangle(n,n+2,n+3);
                }
            }
        }
        static Vector2 Bezier(Vector2 a,Vector2 b,float t){float u=1-t;return u*u*u*a+3*u*u*t*new Vector2(a.x+(b.x-a.x)*.5f,a.y)+3*u*t*t*new Vector2(a.x+(b.x-a.x)*.5f,b.y)+t*t*t*b;}
    }
}
