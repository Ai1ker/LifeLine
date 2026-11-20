using UnityEngine;

namespace Lifeline
{
    /// <summary>The common XY swing sector used aboard, underwater and by the attack effect.</summary>
    internal static class MeleeArc
    {
        public const float HalfAngle = 67.5f;
        static readonly float MinimumDot = Mathf.Cos(HalfAngle * Mathf.Deg2Rad);

        public static Vector2 Direction(Vector2 facing) => facing.sqrMagnitude > .001f ? facing.normalized : Vector2.right;

        public static Vector2 Rotate(Vector2 facing, float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            return new Vector2(facing.x * cos - facing.y * sin, facing.x * sin + facing.y * cos);
        }

        public static bool Intersects(Vector2 offset, Vector2 facing, float reach, float targetRadius = 0f)
        {
            float distance = offset.magnitude;
            if (distance > reach + targetRadius) return false;
            if (distance <= targetRadius || Vector2.Dot(offset, facing) >= distance * MinimumDot) return true;

            // A target outside the angle can still overlap one of the sector's straight edges.
            float side = facing.x * offset.y - facing.y * offset.x >= 0f ? HalfAngle : -HalfAngle;
            var edge = Rotate(facing, side);
            var nearest = edge * Mathf.Clamp(Vector2.Dot(offset, edge), 0f, reach);
            return (offset - nearest).sqrMagnitude <= targetRadius * targetRadius;
        }
    }
}
