using UnityEngine;
using UnityEngine.EventSystems;

namespace Lifeline
{
    public class HoldActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool held;
        public void OnPointerDown(PointerEventData eventData) { held = true; }
        public void OnPointerUp(PointerEventData eventData) { held = false; }
        public void OnPointerExit(PointerEventData eventData) { held = false; }
        private void OnDisable() { held = false; }
    }
}
