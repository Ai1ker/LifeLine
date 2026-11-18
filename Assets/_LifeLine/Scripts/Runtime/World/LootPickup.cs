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
        public float CollectionProgress { get; private set; }
        CrewActor collector;
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
            if (collector != actor || now - lastCollectAt > .2f)
            {
                collector = actor; startedAt = now; CollectionProgress = 0;
            }
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
                if (actor.session != null) actor.session.RegisterAttachedCargo(this);
                actor.session?.GetComponent<TutorialProgress>()?.Observe(TutorialAction.AttachCargo);
            }
            else if (isCabinet)
            {
                while (contents.Count > 0 && actor.backpack.Count < actor.BagCapacity)
                {
                    actor.backpack.Add(contents[0]); contents.RemoveAt(0);
                }
                if (contents.Count == 0) Consume();
            }
            else
            {
                while (amount > 0 && actor.backpack.Count < actor.BagCapacity)
                {
                    actor.backpack.Add(resource); amount--;
                }
                if (amount == 0) Consume();
            }
            if (actor.role == CrewRole.Diver && actor.backpack.Count > backpackBefore)
                actor.session?.GetComponent<TutorialProgress>()?.Observe(TutorialAction.Collect);
            collector = null; CollectionProgress = 0; lastCollectAt = -100f;
            RefreshVisual();
            return true;
        }

        public bool TryLoad(CrewActor actor)
        {
            if (!isCrate || attached || !InRange(actor) || contents.Count >= capacity || actor.backpack.Count == 0)
                return false;
            int last = actor.backpack.Count - 1;
            contents.Add(actor.backpack[last]); actor.backpack.RemoveAt(last);
            if (actor.role == CrewRole.Diver)
                actor.session?.GetComponent<TutorialProgress>()?.Observe(TutorialAction.LoadCargo);
            RefreshVisual();
            return true;
        }

        public void Deliver(GameSession session)
        {
            if (session == null || consumed || !isCrate || !attached) return;
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
            if (CollectionProgress > 0 && Time.time - lastCollectAt > .2f)
            {
                CollectionProgress = 0; collector = null;
            }
            if (label != null && generatedVisual) RefreshLabel();
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
                slot.gameObject.SetActive(i < contents.Count);
                if (i < contents.Count && resourceMaterials != null && resourceMaterials.Length > 0)
                    slot.sharedMaterial = resourceMaterials[(int)contents[i] % resourceMaterials.Length];
            }
        }

        public string LostCargoPrompt()
        {
            CrewActor actor=null;float distance=float.PositiveInfinity;
            if(world!=null && world.session!=null)foreach(var crew in world.session.Crew)
            {
                if(crew==null||crew.onboard||world.session.IsIncapacitated(crew))continue;
                float next=(crew.Position-transform.position).sqrMagnitude;if(next<distance){actor=crew;distance=next;}
            }
            string collect=actor!=null&&actor.input!=null?actor.input.GetBindingLabel("Collect"):"1";
            string interact=actor!=null&&actor.input!=null?actor.input.GetBindingLabel("Interact"):"Enter";
            return string.Format(UiLocalization.Text(attached ? "遗失物资 {0}/{1} · 已挂绳，等待绞盘" : "遗失物资 {0}/{1}\n按住 [{2}] 挂绳 / [{3}] 存入"),contents.Count,capacity,collect,interact);
        }
        void RefreshLabel()
        {
            if (label == null) return;
            var filter = GetComponent<FilterSalvage>();
            string progress = CollectionProgress > 0 ? " " + Mathf.RoundToInt(CollectionProgress * 100) + "%" : "";
            label.text = filter != null ? filter.Prompt() + progress : isLostCargo ? LostCargoPrompt() + progress : isCrate ? (attached ? "CARGO / ATTACHED" : "CARGO " + contents.Count + "/" + capacity) + progress
                : isCabinet ? "SALVAGE " + contents.Count + progress : GameNames.Resource(resource) + progress;
            label.text = UiLocalization.Text(label.text);
        }
    }
}
