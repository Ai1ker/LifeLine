using UnityEngine;

namespace Lifeline
{
    public partial class WorldGenerator
    {
        readonly LootPickup[] fishTargets = new LootPickup[2];
        int fishTimeFrame = -1;
        float guestFishTime;

        internal float FishSwimTime
        {
            get
            {
                if (session == null) return 0;
                float elapsed = session.elapsed;
                if (!OnlineVoyage.IsGuest) return elapsed;
                if (fishTimeFrame == Time.frameCount) return guestFishTime;
                fishTimeFrame = Time.frameCount;
                if (session.phase != SessionPhase.Playing || Mathf.Abs(elapsed - guestFishTime) > .4f) guestFishTime = elapsed;
                else guestFishTime = Mathf.MoveTowards(guestFishTime + Time.deltaTime, elapsed, Time.deltaTime * .45f);
                return guestFishTime;
            }
        }

        internal void PrepareRoomFish(LootPickup pickup)
        {
            if (pickup.resource != ResourceKind.Fish || pickup.isCrate || pickup.isCabinet ||
                pickup.mazeIndex < 0 || pickup.mazeIndex >= layouts.Count) return;
            var room = layouts[pickup.mazeIndex];
            if (pickup.mazeCell < 0 || pickup.mazeCell >= room.Count) return;
            float halfExtent = Mathf.Max(.15f, (room.cellSize - wallThickness) * .5f - .40f);
            pickup.roomFish = pickup.GetComponent<RoomFish>();
            if (pickup.roomFish == null) pickup.roomFish = pickup.gameObject.AddComponent<RoomFish>();
            pickup.roomFish.Configure(pickup, room.Center(pickup.mazeCell), halfExtent, tuning != null ? tuning.roomFishSwimSpeed : .48f);
            pickup.interactionDistance = tuning != null ? tuning.roomFishCatchDistance : 1.35f;
            pickup.collectSeconds = tuning != null ? tuning.roomFishCatchSeconds : .85f;
        }

        public LootPickup FindCatchableFish(CrewActor actor)
        {
            if (actor == null || actor.backpack.Count >= actor.BagCapacity) return null;
            var current = fishTargets[(int)actor.role];
            return current != null && current.gameObject.activeInHierarchy && current.InRange(actor) && HasFishAccess(actor, current)
                ? current : FindNearest(actor, false, true);
        }

        bool HasFishAccess(CrewActor actor, LootPickup fish) => !Physics.Linecast(actor.Position, fish.transform.position,
            1 << geometryLayer, QueryTriggerInteraction.Ignore);

        public bool TryCatchRoomFish(CrewActor actor, bool held)
        {
            int role = (int)actor.role;
            var current = fishTargets[role];
            var next = held ? FindCatchableFish(actor) : null;
            if (current != null && current != next) current.CancelCollection(actor);
            fishTargets[role] = next;
            return next != null && next.TryCollect(actor);
        }
    }
}
