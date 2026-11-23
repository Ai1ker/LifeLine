using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lifeline
{
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class AudioUiFeedback : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, ISubmitHandler
    {
        public AudioCue click = AudioCue.UiClick;
        public AudioCue denied = AudioCue.UiDeny;
        public bool hover = true;
        Button button;

        void Awake() => button = GetComponent<Button>();

        public void OnPointerEnter(PointerEventData data)
        {
            if (hover && button.IsInteractable()) GameAudio.Local(AudioCue.UiHover, .32f, .065f);
        }

        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) Play();
        }

        public void OnSubmit(BaseEventData data) => Play();
        void Play() => GameAudio.Local(button.IsInteractable() ? click : denied, .66f, .035f);
    }
}
