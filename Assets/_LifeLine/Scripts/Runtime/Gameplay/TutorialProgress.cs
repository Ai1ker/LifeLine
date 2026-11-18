using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    [Flags]
    public enum TutorialAction
    {
        None = 0, CraftPlank = 1, ViewControl = 2, DiverDive = 4, Collect = 8,
        LoadCargo = 16, AttachCargo = 32, Hide = 64, CaptainWinchRecovery = 128
    }
    [Serializable]
    public sealed class TutorialSave
    {
        public int observedActions;
        public bool skipBasics;
        public int seenNotices, finishedNotices;
        public int activeNotice = -1;
        public float remainingNoticeSeconds;
    }

    /// <summary>Non-blocking, action-driven onboarding, independent of the story quest and device panels.</summary>
    [DisallowMultipleComponent]
    public sealed class TutorialProgress : MonoBehaviour
    {
        public GameSession session;
        [Min(1)] public float noticeSeconds = 5;
        public TutorialAction observedActions;
        public bool skipBasics;
        public int seenNotices, finishedNotices;
        public int activeNotice = -1;
        public float remainingNoticeSeconds;
        [NonSerialized] public bool remindersVisible = true;
        public event Action Changed;
        bool configured, subscribed;
        bool diverWasUnderwater, captainWinchObserved;
        int observedWorldDay = -1;
        readonly Dictionary<LootPickup, int> crateCounts = new Dictionary<LootPickup, int>();
        static readonly TutorialAction[] sequence = {
            TutorialAction.CraftPlank, TutorialAction.ViewControl, TutorialAction.DiverDive, TutorialAction.Collect,
            TutorialAction.LoadCargo, TutorialAction.AttachCargo, TutorialAction.Hide, TutorialAction.CaptainWinchRecovery
        };
        public int Step
        {
            get { if (skipBasics) return sequence.Length; for (int i = 0; i < sequence.Length; i++) if ((observedActions & sequence[i]) == 0) return i; return sequence.Length; }
        }
        public bool Complete => Step >= sequence.Length;
        public string InstructionSource
        {
            get
            {
                switch (Step)
                {
                    case 0: return "合作入门 1/8 · 船长\n靠近制作台按 [{0}] 互动，尝试制作 1 个木板。离开后仍会自动制作。";
                    case 1: return "合作入门 2/8 · 船长\n靠近设备总控按 [{0}] 查看设备升级。查看升级效果和材料费用，按需改装船只。";
                    case 2: return "合作入门 3/8 · 潜水员\n靠近潜水准备台按 [{0}] 下水。留意氧气、背包和绳长，船长留在船上接应。";
                    case 3: return "合作入门 4/8 · 潜水员\n靠近水下物资，按住 [{1}] 收集。带回的物资可制作部件并逐步建造船上设备。";
                    case 4: return "合作入门 5/8 · 潜水员\n背包满后可到货箱旁按 [{0}] 存入物品；每个货箱可装 3 件。先尝试装入 1 件。";
                    case 5: return "合作入门 6/8 · 潜水员\n货箱装好后按住 [{1}] 挂绳，交由船长绞盘接回。挂载越多，回收越慢。";
                    case 6: return "合作入门 7/8 · 潜水员\n靠近藏匿处按 [{0}] 进入，再按一次离开。藏匿时无法移动，也不会被索敌或攻击；更深水域会有敌对生物。";
                    case 7: return "合作入门 8/8 · 船长\n潜水员准备好后，靠近绞盘按 [{0}]，用圆形 QTE 接回潜水员和挂载货箱。成功加快回收，失误损失进度。";
                    default: return "";
                }
            }
        }
        public string NoticeSource
        {
            get
            {
                switch (activeNotice)
                {
                    case 0: return "海鸥来袭\n海鸥会攻击船体并消耗耐久；靠近后使用近战攻击将它们驱赶。部分海鸥也会打断船员操作。";
                    case 1: return "第一次夜潜\n夜间水下只能看清附近。建造并开启探照灯可恢复视野，但灯光也会扩大敌对水生生物的索敌范围。";
                    case 2: return "雷雨警告\n注意雷击落点预警并及时离开危险区域。被击倒后需要同伴持续互动 4 秒扶起；两人都倒下会失败。";
                    default: return "";
                }
            }
        }
        public void Initialize(GameSession owner)
        {
            if (owner == null) return;
            if (session != owner) { Unsubscribe(); session = owner; }
            if (!subscribed)
            {
                session.StationRequested += OnStation; session.SessionReady += OnSessionReady; subscribed = true;
            }
            if (session.IsInitialized && !configured) ResetForNewGame();
        }
        void Awake() { Initialize(GetComponent<GameSession>()); }
        void OnDestroy() { Unsubscribe(); }
        void Unsubscribe()
        {
            if (session != null && subscribed) { session.StationRequested -= OnStation; session.SessionReady -= OnSessionReady; }
            subscribed = false;
        }
        void OnSessionReady(bool restored)
        {
            if (!restored) ResetForNewGame();
            else if (!configured) Restore(null);
        }
        public void ResetForNewGame()
        {
            observedActions = TutorialAction.None; skipBasics = false; seenNotices = finishedNotices = 0;
            activeNotice = -1; remainingNoticeSeconds = 0; configured = true;
            diverWasUnderwater = captainWinchObserved = false; crateCounts.Clear(); Changed?.Invoke();
        }
        public void Observe(TutorialAction action)
        {
            TutorialAction before = observedActions; observedActions |= action;
            if (before != observedActions) Changed?.Invoke();
        }
        void OnStation(StationKind kind, CrewActor actor)
        {
            if (actor == session.captain && kind == StationKind.TotalControl) Observe(TutorialAction.ViewControl);
        }
        void Update() { Tick(Time.deltaTime); }
        /// <summary>Advances reminders only; the core lesson never advances on a timer.</summary>
        public void Tick(float seconds)
        {
            if (session == null || !session.IsInitialized || session.phase != SessionPhase.Playing) return;
            if (!configured) ResetForNewGame();
            ObserveWorld();
            if (session.StormActive) QueueNotice(2);
            if (session.AnyCrewUnderwater && session.IsNight) QueueNotice(1);
            foreach (var enemy in session.enemies)
                if (enemy != null && (enemy.kind == EnemyKind.Bird || enemy.kind == EnemyKind.ArmoredBird)) { QueueNotice(0); break; }
            if (activeNotice >= 0 && remindersVisible)
            {
                remainingNoticeSeconds = Mathf.Max(0, remainingNoticeSeconds - Mathf.Max(0, seconds));
                if (remainingNoticeSeconds <= 0) DismissNotice();
            }
            StartNextNotice();
        }
        void ObserveWorld()
        {
            if (session.jobs != null) foreach (var job in session.jobs)
            {
                if (job == null || job.owner != CrewRole.Captain || job.recipe == null || job.recipe.output == null) continue;
                foreach (var output in job.recipe.output) if (output != null && output.resource == ResourceKind.Plank && output.amount > 0) Observe(TutorialAction.CraftPlank);
            }
            var diver = session.diver;
            if (diver != null)
            {
                if (!diver.onboard) { diverWasUnderwater = true; Observe(TutorialAction.DiverDive); }
                if (!diver.onboard && diver.backpack.Count > 0) Observe(TutorialAction.Collect);
                if (session.IsHidden(diver)) Observe(TutorialAction.Hide);
                if (diverWasUnderwater && captainWinchObserved && diver.onboard) Observe(TutorialAction.CaptainWinchRecovery);
            }
            if (session.winchActive && session.winchOperatorRole == CrewRole.Captain) captainWinchObserved = true;
            if (session.world == null) return;
            if (observedWorldDay != session.day) { crateCounts.Clear(); observedWorldDay = session.day; }
            foreach (var pickup in session.world.pickups)
            {
                if (pickup == null || !pickup.isCrate || pickup.isLostCargo) continue;
                int before; bool known = crateCounts.TryGetValue(pickup, out before);
                if (known && pickup.contents.Count > before) Observe(TutorialAction.LoadCargo);
                crateCounts[pickup] = pickup.contents.Count;
                if (pickup.attached) Observe(TutorialAction.AttachCargo);
            }
        }
        public void QueueNotice(int index)
        {
            if (index < 0 || index > 2) return;
            int bit = 1 << index; if ((seenNotices & bit) != 0) return;
            seenNotices |= bit; StartNextNotice(); Changed?.Invoke();
        }
        void StartNextNotice()
        {
            if (activeNotice >= 0) return;
            for (int i = 0; i < 3; i++) if ((seenNotices & 1 << i) != 0 && (finishedNotices & 1 << i) == 0)
            { activeNotice = i; remainingNoticeSeconds = Mathf.Max(1, noticeSeconds); Changed?.Invoke(); break; }
        }
        public void DismissNotice()
        {
            if (activeNotice >= 0) finishedNotices |= 1 << activeNotice;
            activeNotice = -1; remainingNoticeSeconds = 0; StartNextNotice(); Changed?.Invoke();
        }
        public TutorialSave Capture() => new TutorialSave { observedActions = (int)observedActions, skipBasics = skipBasics, seenNotices = seenNotices, finishedNotices = finishedNotices, activeNotice = activeNotice, remainingNoticeSeconds = remainingNoticeSeconds };
        public void Restore(TutorialSave state)
        {
            Initialize(GetComponent<GameSession>());
            observedActions = state != null ? (TutorialAction)(state.observedActions & 255) : TutorialAction.None;
            skipBasics = state == null || state.skipBasics;
            seenNotices = state != null ? state.seenNotices & 7 : 0; finishedNotices = state != null ? state.finishedNotices & seenNotices : 0;
            activeNotice = state != null && state.activeNotice >= 0 && state.activeNotice < 3 && (seenNotices & 1 << state.activeNotice) != 0 && (finishedNotices & 1 << state.activeNotice) == 0 ? state.activeNotice : -1;
            remainingNoticeSeconds = activeNotice >= 0 ? Mathf.Clamp(state.remainingNoticeSeconds, .1f, Mathf.Max(1, noticeSeconds)) : 0;
            if (float.IsNaN(remainingNoticeSeconds) || float.IsInfinity(remainingNoticeSeconds)) remainingNoticeSeconds = Mathf.Max(1, noticeSeconds);
            configured = true; diverWasUnderwater = (observedActions & TutorialAction.DiverDive) != 0; captainWinchObserved = false; crateCounts.Clear();
            StartNextNotice(); Changed?.Invoke();
        }
    }
}
