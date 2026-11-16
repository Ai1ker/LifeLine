using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace Lifeline
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PipeShapeGraphic : MaskableGraphic
    {
        public int mask;
        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();var r=rectTransform.rect;Vector2 c=r.center;float w=Mathf.Min(r.width,r.height)*.10f;
            Add(v,new Rect(c.x-w,c.y-w,w*2,w*2),color);
            if((mask&1)!=0)Add(v,new Rect(c.x-w,c.y,w*2,r.yMax-c.y),color);
            if((mask&2)!=0)Add(v,new Rect(c.x,c.y-w,r.xMax-c.x,w*2),color);
            if((mask&4)!=0)Add(v,new Rect(c.x-w,r.yMin,w*2,c.y-r.yMin),color);
            if((mask&8)!=0)Add(v,new Rect(r.xMin,c.y-w,c.x-r.xMin,w*2),color);
        }
        internal static void Add(VertexHelper v,Rect r,Color color)
        {int n=v.currentVertCount;v.AddVert(new Vector3(r.xMin,r.yMin),color,Vector2.zero);v.AddVert(new Vector3(r.xMax,r.yMin),color,Vector2.zero);v.AddVert(new Vector3(r.xMax,r.yMax),color,Vector2.zero);v.AddVert(new Vector3(r.xMin,r.yMax),color,Vector2.zero);v.AddTriangle(n,n+1,n+2);v.AddTriangle(n,n+2,n+3);}
    }

}
