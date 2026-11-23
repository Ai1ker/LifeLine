using UnityEngine;

namespace Lifeline
{
    /// <summary>Displays the finished radio parts without adding stations or collision.</summary>
    [DisallowMultipleComponent]
    public sealed class ShipRadioDisplay : MonoBehaviour
    {
        public SpriteRenderer antenna, receiver, amplifier;

        public void ApplyStage(int stage)
        {
            Show(antenna, stage >= 1);
            Show(receiver, stage >= 2);
            Show(amplifier, stage >= 3);
        }

        static void Show(SpriteRenderer part, bool visible)
        {
            if (part != null && part.gameObject.activeSelf != visible)
                part.gameObject.SetActive(visible);
        }
    }
}
