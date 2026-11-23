using UnityEngine;

namespace Lifeline
{
    public partial class GameSession
    {
        const float RemoteCollectionDisplayTimeout = .45f;

        struct CollectionPreview
        {
            public LootPickup target;
            public float fraction, expiresAt;
        }

        readonly CollectionPreview[] replicaCollections = new CollectionPreview[2];

        OnlineCollectionProgress CaptureCollection(CrewActor actor)
        {
            if (phase == SessionPhase.Playing && TryGetHostCollection(actor, out var pickup, out float fraction))
                return new OnlineCollectionProgress { targetId = pickup.saveId, fraction = fraction };
            return default;
        }

        void ApplyCollection(CrewRole role, OnlineCollectionProgress sample)
        {
            int index = (int)role;
            if (phase != SessionPhase.Playing || world == null || string.IsNullOrEmpty(sample.targetId) ||
                float.IsNaN(sample.fraction) || sample.fraction < 0f || sample.fraction >= 1f)
            {
                replicaCollections[index] = default;
                return;
            }
            var target = replicaCollections[index].target;
            if (target == null || target.saveId != sample.targetId)
                target = world.pickups.Find(pickup => pickup != null && pickup.saveId == sample.targetId);
            replicaCollections[index] = new CollectionPreview
            {
                target = target, fraction = sample.fraction, expiresAt = Time.unscaledTime + RemoteCollectionDisplayTimeout
            };
        }

        public bool TryGetCollectionProgress(CrewActor actor, out LootPickup pickup, out float fraction)
        {
            pickup = null;
            fraction = 0;
            if (phase != SessionPhase.Playing || actor == null) return false;
            if (!OnlineVoyage.IsGuest) return TryGetHostCollection(actor, out pickup, out fraction);
            var sample = replicaCollections[(int)actor.role];
            if (Time.unscaledTime >= sample.expiresAt || sample.target == null || !sample.target.CanShowCollection(actor)) return false;
            pickup = sample.target;
            fraction = sample.fraction;
            return true;
        }

        static bool TryGetHostCollection(CrewActor actor, out LootPickup pickup, out float fraction)
        {
            pickup = actor != null ? actor.CollectionTarget : null;
            bool collecting = pickup != null && pickup.IsCollecting(actor);
            fraction = collecting ? pickup.CollectionProgress : 0f;
            return collecting;
        }
    }
}
