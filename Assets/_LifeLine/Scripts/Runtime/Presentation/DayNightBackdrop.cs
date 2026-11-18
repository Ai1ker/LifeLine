using UnityEngine;
namespace Lifeline
{
    /// <summary>Only the background changes tint. Gameplay art and HUD remain bright placeholders.</summary>
    public sealed class DayNightBackdrop:MonoBehaviour
    {
        public GameSession session;
        public Color nightColor=new Color(.025f,.075f,.14f),duskColor=new Color(.07f,.19f,.27f);
        Renderer target;Material instance;Color dayColor,appliedColor;bool hasAppliedColor;
        void Start()
        {
            target=GetComponent<Renderer>();if(target==null||target.sharedMaterial==null)return;
            dayColor=target.sharedMaterial.color;instance=new Material(target.sharedMaterial);target.sharedMaterial=instance;RefreshNow();
        }
        void LateUpdate()=>RefreshNow();
        public void RefreshNow()
        {
            if(instance==null)return;
            Color desired=session!=null&&session.IsNight?nightColor:session!=null&&session.IsDusk?duskColor:dayColor;
            if(hasAppliedColor&&appliedColor==desired)return;
            instance.color=desired;appliedColor=desired;hasAppliedColor=true;
        }
        void OnDestroy(){if(instance!=null)Destroy(instance);}
    }
}
