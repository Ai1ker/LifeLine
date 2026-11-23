using UnityEngine;

namespace Lifeline
{
    public sealed class PixelPlantSway : MonoBehaviour
    {
        public float phase, degrees = 4f, rate = 1.35f;
        Vector3 basePosition;
        float footOffset;

        public void Initialize(float phaseOffset)
        {
            phase = phaseOffset;
            basePosition = transform.localPosition;
            var sprite = GetComponent<SpriteRenderer>();
            if (sprite != null) footOffset = sprite.bounds.extents.y;
        }

        void LateUpdate()
        {
            var rotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * rate + phase) * degrees);
            transform.localRotation = rotation;
            Vector3 fromFoot = Vector3.up * footOffset;
            transform.localPosition = basePosition - fromFoot + rotation * fromFoot;
        }
    }
}
