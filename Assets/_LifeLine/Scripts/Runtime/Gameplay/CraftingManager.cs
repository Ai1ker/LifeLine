using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    /// <summary>
    /// Sole owner of the crafting queue and its rules. Recipes are authored ScriptableObjects;
    /// shared inventory, tool unlocks and story outcomes belong to the referenced session.
    /// The session advances Tick exactly once per simulation step; this component has no Update clock.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameSession))]
    public sealed class CraftingManager : MonoBehaviour
    {
        [Header("制作依赖 / recipe ScriptableObject references")]
        public GameSession session;
        [Tooltip("Each entry is an editable RecipeDefinition SO. No recipes or prices are hard-coded here.")]
        public RecipeDefinition[] recipeDefinitions = new RecipeDefinition[0];

        [Header("唯一权威队列 / authoritative runtime jobs")]
        public List<CraftJob> jobs = new List<CraftJob>();
        [Min(1)] public int nextJobId = 1;
        [Tooltip("Short-lived QTE feedback, in real simulation seconds.")]
        public float feedbackSeconds = .8f;

        readonly CrewActor[] interactionActors = new CrewActor[2];
        readonly int[] interactionJobs = new int[2];
        readonly StationKind[] interactionStations = new StationKind[2];

        public void SetInteractionContext(CrewActor actor, int jobId, StationKind station)
        {
            if (actor == null || actor.session != session) return;
            int index = (int)actor.role;
            interactionActors[index] = actor; interactionJobs[index] = jobId; interactionStations[index] = station;
        }
        public void ClearInteractionContext(CrewActor actor)
        {
            if (actor == null) return;
            int index = (int)actor.role;
            if (interactionActors[index] != actor) return;
            interactionActors[index] = null; interactionJobs[index] = 0;
        }
        public float GetInteractionRate(CraftJob job)
        {
            if (job == null || session == null) return 1;
            for (int i = 0; i < interactionActors.Length; i++)
            {
                CrewActor actor = interactionActors[i];
                if (interactionJobs[i] == job.id && actor != null && actor.session == session && actor.onboard &&
                    !actor.downed && actor.controlsCaptured && interactionStations[i] == JobStation(job) &&
                    session.IsStationUnlocked(interactionStations[i])) return session.GetDeviceInteractionEfficiency(actor);
            }
            return 1;
        }

        public int Capacity => session != null && session.GetUpgrade(UpgradeKind.Workbench) >= 3 ? 2 : 1;
        public const int MaximumBatch = 5;
        public const string InsufficientMaterials = "制作材料不足。";
        public const string CraftFailureNotice = "制作失败，请打捞需要的材料";
        public int SlotCapacity(StationKind station) => station == StationKind.Workbench ? Capacity : 1;
        public int SlotCount(StationKind station)
        {
            int count = 0;
            foreach (var job in jobs) if (job != null && JobStation(job) == station) count++;
            return count;
        }
        public bool CanBatch(RecipeDefinition recipe) => recipe != null && recipe.allowBatch && recipe.repairAmount <= 0 &&
            session != null && session.GetUpgrade(UpgradeKind.Workbench) >= 2;

        public void Initialize(GameSession owner)
        {
            session = owner;
            if (recipeDefinitions == null) recipeDefinitions = new RecipeDefinition[0];
            if (jobs == null) jobs = new List<CraftJob>();
            SynchronizeNextJobId();
        }

        public void SetRecipeDefinitions(RecipeDefinition[] definitions)
        {
            recipeDefinitions = definitions ?? new RecipeDefinition[0];
        }

        public void ReplaceJobs(List<CraftJob> replacement)
        {
            jobs = replacement ?? new List<CraftJob>();
            SynchronizeNextJobId();
        }

        public void ResetQueue()
        {
            jobs.Clear(); nextJobId = 1;
            for(int i=0;i<interactionActors.Length;i++){interactionActors[i]=null;interactionJobs[i]=0;}
        }

        public void ClearQueue()
        {
            // Crew-wipe losses are deliberately not cancellation refunds.
            jobs.Clear();
        }

        void SynchronizeNextJobId()
        {
            nextJobId = Mathf.Max(1, nextJobId);
            foreach (CraftJob job in jobs) if (job != null) nextJobId = Mathf.Max(nextJobId, job.id + 1);
        }

        public RecipeDefinition FindRecipe(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId) || recipeDefinitions == null) return null;
            return Array.Find(recipeDefinitions, recipe => recipe != null && recipe.id == recipeId);
        }

        public float GetCraftDuration(RecipeDefinition recipe = null)
        {
            if (session == null || session.tuning == null) return 0;
            GameTuning tuning = session.tuning;
            int tier = recipe != null && recipe.repairAmount > 0 ? 0 : session.GetUpgrade(UpgradeKind.Workbench);
            float seconds = tuning.craftSeconds;
            if (tier > 0 && tuning.craftSecondsByLevel != null && tier < tuning.craftSecondsByLevel.Length) seconds = tuning.craftSecondsByLevel[tier];
            else if (tier >= 2) seconds = tuning.upgradedCraftSeconds;
            return Mathf.Max(.1f, seconds * (recipe != null ? Mathf.Max(.01f, recipe.craftTimeMultiplier) : 1));
        }

        public float GetCraftQteWindow(CraftJob job = null)
        {
            if (session == null || session.tuning == null) return 0;
            bool upgraded = (job == null || JobStation(job) == StationKind.Workbench) && session.GetUpgrade(UpgradeKind.Workbench) >= 1;
            return session.tuning.craftQteHalfWindow * (upgraded ? session.tuning.workbenchWindowMultiplier : 1);
        }

        public bool CanCraft(string recipeId, CrewActor actor, out string reason, int quantity = 1)
        {
            reason = "";
            if (session == null || session.tuning == null || session.phase != SessionPhase.Playing || actor == null || actor.session != session || !actor.onboard || actor.downed) return false;
            RecipeDefinition recipe = FindRecipe(recipeId);
            if (recipe == null) { reason = "未找到对应的制作配方。"; return false; }
            if (quantity < 1 || quantity > MaximumBatch || quantity > 1 && !CanBatch(recipe)) return false;
            StationKind station = recipe.repairAmount > 0 ? StationKind.HullRepair : StationKind.Workbench;
            if (SlotCount(station) >= SlotCapacity(station))
            { reason = station == StationKind.HullRepair ? "当前已有船体维修作业。" : "制作队列已满，T3制作台可同时运行两项工作。"; return false; }
            if (recipe.repairAmount > 0 && !session.repairBenchInstalled) { reason = "请先在制作台建造船体维修台。"; return false; }
            if (recipe.repairAmount > 0 && session.hull >= session.hullMax) { reason = "船体完好，无需消耗维修材料。"; return false; }
            if (!string.IsNullOrEmpty(recipe.toolId) && session.HasEquipment(recipe.toolId))
            { reason = "这件工具或设备已经制作完成。"; return false; }
            if (recipe.requiredToolIds != null) foreach (string tool in recipe.requiredToolIds)
                if (string.IsNullOrEmpty(tool) || !session.HasEquipment(tool)) { reason = "制作需要先获得所需工具。"; return false; }
            if (recipe.consumedToolIds != null) foreach (string tool in recipe.consumedToolIds)
                if (string.IsNullOrEmpty(tool) || !session.HasEquipment(tool)) { reason = "制作需要消耗对应工具。"; return false; }
            foreach (CraftJob queued in jobs)
            {
                if (queued == null || queued.recipe == null) continue;
                if (recipe.repairAmount > 0 && queued.recipe.repairAmount > 0) { reason = "当前已有船体维修作业。"; return false; }
                bool sameTool = !string.IsNullOrEmpty(recipe.toolId) && queued.recipe.toolId == recipe.toolId;
                bool sameRadioStage = recipe.radioStage > 0 && queued.recipe.radioStage == recipe.radioStage;
                if (sameTool || sameRadioStage) { reason = "这项工具或电台部件已在制作队列中。"; return false; }
            }
            if (recipe.radioStage > 0 && (!session.circuitRepaired || session.radioStage + 1 != recipe.radioStage))
            { reason = "求救电台需要修复电路，并按天线、接收器、放大器顺序建造。"; return false; }
            // Aggregate repeated cost entries before admission. Spend repeats the atomic check at commit.
            var required = new Dictionary<ResourceKind, int>();
            if (recipe.cost != null) foreach (ResourceAmount amount in recipe.cost)
            {
                if (amount == null || amount.amount < 0) { reason = "配方成本配置无效。"; return false; }
                if (!required.ContainsKey(amount.resource)) required[amount.resource] = 0;
                required[amount.resource] += amount.amount * quantity;
            }
            foreach (var cost in required) if (session.GetResource(cost.Key) < cost.Value) { reason = InsufficientMaterials; return false; }
            return true;
        }

        public bool TryCraft(string recipeId, CrewActor actor, int quantity = 1)
        {
            string reason;
            if (!CanCraft(recipeId, actor, out reason, quantity)) { NotifyFailure(actor, reason); return false; }
            RecipeDefinition recipe = FindRecipe(recipeId);
            ResourceAmount[] cost = recipe.cost;
            if (quantity > 1 && cost != null) cost = Array.ConvertAll(cost, item => new ResourceAmount(item.resource, item.amount * quantity));
            if (!session.Spend(cost)) { NotifyFailure(actor, InsufficientMaterials); return false; }
            if (recipe.consumedToolIds != null) foreach (string tool in new HashSet<string>(recipe.consumedToolIds)) session.ConsumeEquipment(tool);
            SynchronizeNextJobId();
            jobs.Add(new CraftJob
            {
                id = nextJobId++, recipe = recipe, owner = actor.role, quantity = quantity,
                duration = GetCraftDuration(recipe) * quantity, nextCue = session.tuning.craftCueInterval,
                noteDirection = UnityEngine.Random.Range(0, 6)
            });
            if (actor.role == CrewRole.Captain && recipe.output != null && Array.Exists(recipe.output, output => output != null && output.resource == ResourceKind.Plank && output.amount > 0))
                session.GetComponent<TutorialProgress>()?.Observe(TutorialAction.CraftPlank);
            session.Tell(recipe.displayName + "开始自动制作，离开工作台仍会继续。"); session.Changed(); return true;
        }

        public bool CancelCraft(int jobId)
        {
            CraftJob job = jobs.Find(candidate => candidate != null && candidate.id == jobId);
            if (session == null || session.phase != SessionPhase.Playing || job == null) return false;
            jobs.Remove(job);
            session.Tell("已取消" + (job.recipe != null ? job.recipe.displayName : "作业") + "，已投入材料不予返还。"); session.Changed(); return true;
        }

        void NotifyFailure(CrewActor actor, string reason)
        {
            if (session == null || string.IsNullOrEmpty(reason)) return;
            if (actor != null && reason == InsufficientMaterials)
            {
                actor.craftFailureUntil = Time.unscaledTime + 2.5f;
                return;
            }
            session.Tell(reason, actor);
        }

        public static StationKind JobStation(CraftJob job) => job != null && job.recipe != null && job.recipe.repairAmount > 0 ? StationKind.HullRepair : StationKind.Workbench;
        public bool CraftQteAtStation(int jobId, int direction, StationKind station, float rewind = 0f)
        {
            var job=jobs.Find(candidate=>candidate!=null&&candidate.id==jobId);
            return job!=null && JobStation(job)==station && CraftQte(jobId,direction,rewind);
        }
        public bool CraftQte(int jobId, int direction, float rewind = 0f)
        {
            if (session == null || session.phase != SessionPhase.Playing || session.tuning == null) return false;
            CraftJob job = jobs.Find(candidate => candidate != null && candidate.id == jobId);
            if (job == null || job.completed || job.attempted) return false;
            job.attempted = true;
            bool good = direction == job.noteDirection && Mathf.Abs(job.elapsed - rewind - job.nextCue) <= GetCraftQteWindow(job);
            if (good)
            {
                CrewBuffs buffs = job.owner == CrewRole.Captain ? session.captainBuffs : session.diverBuffs;
                float bonus = session.tuning.craftQteBonus * (buffs.qte > 0 ? 1 + session.tuning.fishQteBonus : 1);
                job.progress = Mathf.Min(job.duration, job.progress + bonus);
                job.hits++; job.feedback = "+" + bonus.ToString("0.#") + "秒";
            }
            else { job.misses++; job.feedback = "节奏错过 · 自动制作继续"; }
            job.feedbackTime = feedbackSeconds;
            ApplyRepairProgress(job);
            if (job.progress >= job.duration) CompleteCraft(job);
            session.Changed(); return good;
        }

        public void Tick(float dt)
        {
            if (session == null || session.phase != SessionPhase.Playing || session.tuning == null || dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            foreach (CraftJob job in jobs.ToArray())
            {
                if (job == null || job.recipe == null) { jobs.Remove(job); continue; }
                float effectiveDt = dt * GetInteractionRate(job);
                job.elapsed += effectiveDt; job.progress = Mathf.Min(job.duration, job.progress + effectiveDt);
                job.feedbackTime = Mathf.Max(0, job.feedbackTime - dt);
                while (job.elapsed > job.nextCue + GetCraftQteWindow(job) + OnlineGrace(job))
                {
                    job.nextCue += Mathf.Max(.1f, session.tuning.craftCueInterval);
                    job.noteDirection = UnityEngine.Random.Range(0, 6); job.attempted = false;
                }
                ApplyRepairProgress(job);
                if (job.progress >= job.duration) CompleteCraft(job);
            }
        }

        void ApplyRepairProgress(CraftJob job)
        {
            if(job.recipe == null || job.recipe.repairAmount<=0)return;
            float earned=job.recipe.repairAmount*job.NormalizedProgress;
            session.hull=Mathf.Min(session.hullMax, session.hull+Mathf.Max(0,earned-job.repairApplied)); job.repairApplied=earned;
        }

        float OnlineGrace(CraftJob job)
        {
            if (!OnlineVoyage.InProgress || OnlineVoyage.IsGuest) return 0f;
            int remote = 1 - (int)OnlineVoyage.Current.LocalRole;
            return interactionJobs[remote] == job.id && interactionActors[remote] != null && interactionActors[remote].controlsCaptured ? .18f : 0f;
        }

        void CompleteCraft(CraftJob job)
        {
            if (job.completed || !jobs.Contains(job)) return;
            job.completed = true; job.progress = job.duration;
            RecipeDefinition recipe = job.recipe;
            // Remove before notifying inventory listeners, so they cannot see or complete a finished job twice.
            jobs.Remove(job);
            if (recipe.output != null) foreach (ResourceAmount output in recipe.output) if (output != null) session.AddResource(output.resource, output.amount * Mathf.Max(1, job.quantity));
            if (!string.IsNullOrEmpty(recipe.toolId)) session.UnlockEquipment(recipe.toolId);
            if (recipe.radioStage > 0) session.radioStage = Mathf.Max(session.radioStage, recipe.radioStage);
            ApplyRepairProgress(job);
            if (recipe.radioStage == 1) session.Tell("无线电1/3：「……有人吗？我们在东边，浮筏上还有孩子……」");
            else if (recipe.radioStage == 2) session.Tell("无线电2/3：「坐标确认，向东。浮筏撑不了多久。」");
            else if (recipe.radioStage == 3) session.Tell("无线电3/3：我们能呼救自己了。但他们的信号在变弱。去接他们！");
            else session.Tell(recipe.displayName + "已完成，产物进入共享仓库。");
            session.Changed();
        }
    }
}
