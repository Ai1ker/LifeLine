using UnityEngine;

namespace Lifeline
{
    /// <summary>A collectable fish whose seeded swim is contained by its own room.</summary>
    [DisallowMultipleComponent]
    public sealed class RoomFish : MonoBehaviour
    {
        public Vector3 Home { get; private set; }
        public Rect SwimBounds { get; private set; }
        WorldGenerator world;
        LootPickup pickup;
        SpriteRenderer sprite;
        Vector2 center, amplitude, frequency;
        float phase;

        internal void Configure(LootPickup owner, Vector3 roomCenter, float halfExtent, float speed)
        {
            pickup = owner;
            world = owner.world;
            Home = owner.transform.position;
            SwimBounds = new Rect(roomCenter.x - halfExtent, roomCenter.y - halfExtent, halfExtent * 2, halfExtent * 2);
            Vector2 offset = (Vector2)(Home - roomCenter);
            float maximumOffset = halfExtent * .40f;
            offset.x = Mathf.Clamp(offset.x, -maximumOffset, maximumOffset);
            offset.y = Mathf.Clamp(offset.y, -maximumOffset, maximumOffset);
            center = (Vector2)roomCenter + offset;
            amplitude = new Vector2(halfExtent - Mathf.Abs(offset.x), halfExtent - Mathf.Abs(offset.y));
            frequency = new Vector2(speed * .87f / amplitude.x, speed * .49f / amplitude.y);

            uint hash = 2166136261;
            foreach (char character in owner.saveId ?? string.Empty) hash = unchecked((hash ^ character) * 16777619);
            phase = (hash & 65535) * (2 * Mathf.PI / 65536f);
        }

        public Vector3 PositionAt(float seconds)
        {
            float x = center.x + Mathf.Sin(seconds * frequency.x + phase) * amplitude.x;
            float y = center.y + Mathf.Sin(seconds * frequency.y + phase * .63f) * amplitude.y;
            return new Vector3(x, y, Home.z);
        }

        void Update()
        {
            if (world == null) return;
            float time = world.FishSwimTime;
            transform.position = PositionAt(time);
            if (sprite == null && pickup.visualRoot != null) sprite = pickup.visualRoot.GetComponentInChildren<SpriteRenderer>();
            if (sprite != null) sprite.flipX = Mathf.Cos(time * frequency.x + phase) < 0;
        }
    }
}
