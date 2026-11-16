using UnityEngine;
using UnityEngine.UI;
namespace Lifeline
{
    public sealed class StatusBarValuesView : MonoBehaviour
    {
        public GameSession session;
        public Text hull, power, oxygen;
        public Image background;
        void LateUpdate()
        {
            if(session==null||session.tuning==null)return;
            var font=RuntimeUIFont.GetOrCreateFont();
            foreach(var text in new[]{hull,power,oxygen})if(text!=null&&text.font!=font){text.font=font;text.material=font.material;}
            bool visible=session.phase==SessionPhase.Playing;
            if(background!=null)background.enabled=visible;
            if(hull!=null){hull.enabled=visible;hull.text=UiLocalization.Text("船体耐久")+" "+session.hull.ToString("0")+" / "+session.hullMax.ToString("0");}
            if(power!=null){power.enabled=visible;power.text=UiLocalization.Text("电力")+" "+session.power.ToString("0")+" / "+session.tuning.maxPower.ToString("0")+"%";}
            if(oxygen!=null){oxygen.enabled=visible;oxygen.text=UiLocalization.Text("潜水员氧气")+" "+(session.diver!=null?session.diver.oxygen:0).ToString("0")+" / "+session.GetOxygenMax().ToString("0")+"s";}
        }
    }
}
