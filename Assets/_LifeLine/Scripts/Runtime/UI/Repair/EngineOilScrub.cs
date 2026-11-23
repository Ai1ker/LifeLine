using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace Lifeline
{
    public sealed class EngineOilScrub : MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IDragHandler
    {
        public RepairBoardView view;public Image art;public int fault;bool held;
        public void OnPointerDown(PointerEventData e){if(view==null)return;held=true;view.panel.selectedFault=fault;}
        public void OnPointerUp(PointerEventData e){held=false;}
        public void OnDrag(PointerEventData e){}
        void Update(){if(held&&view!=null)view.Scrub(fault,Time.deltaTime);}
        void OnDisable(){held=false;}
    }

}
