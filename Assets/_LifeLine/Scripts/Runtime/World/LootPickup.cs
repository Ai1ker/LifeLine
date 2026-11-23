using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    /// <summary>Actual inventory container; replacing VisualRoot does not alter collection or cargo.</summary>
    public class LootPickup : MonoBehaviour
    {
        public string saveId;
        public ResourceKind resource;
        [Min(1)] public int amount = 1;
        public bool isCrate, isCabinet;
        public List<ResourceKind> contents = new List<ResourceKind>();
        public bool attached, consumed;
        public bool persistAcrossDays, isLostCargo;
        [System.NonSerialized] public DiverRope lifeline;
        [System.NonSerialized] public float blockedPull;
        public WorldGenerator world;
        public Transform visualRoot;
        public TextMesh label;
        public Renderer[] cargoSlotRenderers;
        public Material[] resourceMaterials;
        [Min(.1f)] public float interactionDistance = 1.65f;
        [Min(.01f)] public float collectSeconds = .72f;
        [Min(.01f)] public float cabinetSeconds = 1.5f;
        [Min(.01f)] public float attachSeconds = 1f;
        [Min(1)] public int capacity = 3;
        public bool generatedVisual;
        [Tooltip("Disable for authored art. Procedural and placeholder loose loot follows resource materials.")]
        public bool tintPlaceholderVisuals = true;
        public int mazeIndex = -1, mazeCell = -1;
        [System.NonSerialized] public RoomFish roomFish;
        public bool IsSwimmingFish => roomFish != null;
        public Vector3 SavePosition => IsSwimmingFish ? roomFish.Home : transform.position;
        public float CollectionProgress { get; private set; }
        CrewActor collector;
        const float CollectInputGrace = .2f;
        float startedAt, lastCollectAt = -100f;

        public bool InRange(CrewActor actor)
        {
            return actor != null && !actor.onboard && !actor.downed && (actor.session==null||!actor.session.IsHidden(actor)&&!actor.session.IsFainted(actor)) && !consumed
                && Vector2.Distance(actor.Position, transform.position) <= interactionDistance;
        }

        public bool TryCollect(CrewActor actor)
        {
            if (!InRange(actor) || attached) return false;
            var filter = GetComponent<FilterSalvage>();
            if (isCrate && contents.Count == 0) return false;
            if (!isCrate && filter == null && actor.backpack.Count >= actor.BagCapacity) return false;
            float now = Time.time;
            if (IsSwimmingFish && collector != null && collector != actor && now - lastCollectAt <= CollectInputGrace) return true;
            if (collector != actor || now - lastCollectAt > CollectInputGrace)
            {
                ResetCollection();
                collector = actor; startedAt = now;
            }
            actor.CollectionTarget = this;
            lastCollectAt = now;
            float duration = filter != null ? filter.salvageSeconds : isCrate ? attachSeconds : isCabinet ? cabinetSeconds : collectSeconds;
            if (actor.session != null) duration /= Mathf.Max(.01f, actor.session.GetCollectSpeedMultiplier(actor));
            CollectionProgress = Mathf.Clamp01((now - startedAt) / Mathf.Max(.01f, duration));
            if (CollectionProgress < 1) return true;
            int backpackBefore = actor.backpack.Count;
            if (filter != null)
            {
                if (!filter.TryAcquire(actor)) return false;
                Consume();
            }
            else if (isCrate)
            {
                attached = true;
                GameAudio.At(actor, AudioCue.RopeAttach);
                blockedPull = 0;
                if (actor.session != null) actor.session.RegisterAttachedCargo(this);
                actor.session?.GetComponent<TutorialProgress>()?.Observe(TutorialAction.AttachCargo);
            }
            else if (isCabinet)
            {
                if (contents.Count > 0 && actor.backpack.Count < actor.BagCapacity)
                    GameAudio.At(actor, AudioCue.CabinetOpen);
                while (contents.Count > 0 && actor.backpack.Count < actor.BagCapacity)
                {
                    actor.backpack.Add(contents[0]); contents.RemoveAt(0);
                }
                if (contents.Count == 0) Consume();
            }
            else
            {
                if (amount > 0) GameAudio.At(actor, resource switch
                {
                    ResourceKind.Wood => AudioCue.PickupWood,
                    ResourceKind.Metal => AudioCue.PickupMetal,
                    ResourceKind.Fiber => AudioCue.PickupFiber,
                    ResourceKind.Fish => AudioCue.PickupFish,
                    _ => AudioCue.PickupGeneric
                });
                while (amount > 0 && actor.backpack.Count < actor.BagCapacity)
                {
                    actor.backpack.Add(resource); amount--;
                }
                if (amount == 0) Consume();
            }
            if (actor.role == CrewRole.Diver && actor.backpack.Count > backpackBefore)
                actor.session?.GetComponent<TutorialProgress>()?.Observe(TutorialAction.Collect);
            ResetCollection();
            RefreshVisual();
            return true;
        }

        public void CancelCollection(CrewActor actor)
        {
            if (collector == actor) ResetCollection();
        }

        internal bool IsCollecting(CrewActor actor) => collector == actor &&
            Time.time - lastCollectAt <= CollectInputGrace && CanShowCollection(actor);

        internal bool CanShowCollection(CrewActor actor) => isActiveAndEnabled && !attached && InRange(actor);

        void ResetCollection()
        {
            if (collector != null && collector.CollectionTarget == this) collector.CollectionTarget = null;
            collector = null;
            CollectionProgress = 0;
            lastCollectAt = -100f;
        }

        void OnDisable() => ResetCollection();

        public bool TryLoad(CrewActor actor)
        {
            if (!isCrate || attached || !InRange(actor) || contents.Count >= capacity || actor.backpack.Count == 0)
                return false;
            int last = actor.backpack.Count - 1;
            contents.Add(actor.backpack[last]); actor.backpack.RemoveAt(last);
            GameAudio.At(actor, AudioCue.CratePack);
            if (actor.role == CrewRole.Diver)
                actor.session?.GetComponent<TutorialProgress>()?.Observe(TutorialAction.LoadCargo);
            RefreshVisual();
            return true;
        }

        public void Deliver(GameSession session)
        {
            if (session == null || consumed || !isCrate || !attached) return;
            GameAudio.At(session, AudioCue.CargoLand, session.ShipPoint(Vector3.zero), true);
            if (contents.Count > 0) GameAudio.Global(session, AudioCue.BankDeposit);
            foreach (ResourceKind item in contents) session.AddResource(item, 1);
            contents.Clear(); attached = false;
            Consume();
        }

        void Consume()
        {
            consumed = true;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (collector != null && Time.time - lastCollectAt > CollectInputGrace) ResetCollection();
            if (label != null && generatedVisual && !isCrate) RefreshLabel();
        }

        public void RefreshVisual()
        {
            if (isCrate && attached && !consumed && world != null && world.session != null)
            {
                var view = GetComponent<DiverLifelineView>();
                if (view == null) view = gameObject.AddComponent<DiverLifelineView>();
                view.cargo = this;
                view.Initialize(world.session, this);
            }
            RefreshLabel();
            if (cargoSlotRenderers == null) return;
            for (int i = 0; i < cargoSlotRenderers.Length; i++)
            {
                var slot = cargoSlotRenderers[i];
                if (slot == null) continue;
                slot.enabled = false;
            }
        }

        public string LostCargoPrompt()
        {
            return string.Format(UiLocalization.Text(attached ? "遗失物资 {0}/{1} · 已挂绳，等待绞盘" : "遗失物资 {0}/{1}"), contents.Count, capacity);
        }
        void RefreshLabel()
        {
            if (label == null) return;
            if (IsSwimmingFish) { label.text = string.Empty; return; }
            var filter = GetComponent<FilterSalvage>();
            label.text = filter != null ? filter.Prompt() : isLostCargo ? LostCargoPrompt() : isCrate ? contents.Count + "/" + capacity
                : isCabinet ? "SALVAGE " + contents.Count : GameNames.Resource(resource);
            label.text = UiLocalization.Text(label.text);
        }
    }
}
