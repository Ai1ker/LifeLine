using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Lifeline
{
    [Serializable]
    public sealed class OnlineStationState
    {
        public bool open, repairStarted, fishing, fishingQuality;
        public StationKind kind;
        public int revision, selectedJob, selectedWire, selectedFault, puzzleHash;
        public float fishZone, fishPosition, fishProgress, fishWidth;
        public string liveText;
        public string batchRecipe;
        public int batchQuantity;
    }

    public struct OnlineStationMotion
    {
        public int revision, job;
        public float hostClock, workClock, rate, cueCenter;
        public int note;
        public float feedbackTime;
        public bool good, fishing, fishHooked;
        public float fishClock, fishVelocity, fishZone, fishWidth, fishProgress;
        public int batchQuantity;
    }

    public partial class CrewStationPanel
    {
        readonly Dictionary<string, Button> onlineActions = new();
        int actionRevision, actionIndex;
        int replicaRevision = -1, replicaPuzzleHash;
        int locallyClosedRevision = -1;
        bool remoteHeld;
        float pointerHeldUntil;
        int pointerFault = -1;
        OnlineStationMotion replicaMotion;
        float motionReceivedAt, displayFishZone, displayFishProgress;
        bool hasStationMotion;
        bool ReplicaPanel => OnlineVoyage.IsGuest;
        public bool OnlinePointerHeld => IsOpen && ((heldButton != null && heldButton.held) || Time.unscaledTime < pointerHeldUntil);

        public void RequestClose()
        {
            if (ReplicaPanel && IsOpen)
            {
                locallyClosedRevision = replicaRevision;
                OnlineVoyage.Current.SendStationAction("close");
            }
            Close();
        }

        public void SetRemotePresentationHidden()
        {
            var group = panel.GetComponent<CanvasGroup>();
            if (group == null) group = panel.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        public void BindOnlineAction(Button button, string key, Action action)
        {
            onlineActions[key] = button;
            button.onClick.AddListener(() =>
            {
                if (ReplicaPanel) OnlineVoyage.Current.SendStationAction(replicaRevision + "|" + key);
                else action();
            });
        }

        public void InvokeOnlineAction(string request)
        {
            int separator = request.IndexOf('|');
            if (separator <= 0 || !int.TryParse(request.Substring(0, separator), out int revision) || revision != actionRevision) return;
            if (onlineActions.TryGetValue(request.Substring(separator + 1), out Button button) &&
                button != null && button.gameObject.activeInHierarchy && button.interactable)
                button.onClick.Invoke();
        }

        void HoldOnlineEngineScrub(int fault)
        {
            if (!IsOpen || station != StationKind.Engine || fault < 0 || fault > 3) return;
            if (fault != pointerFault || Time.unscaledTime >= pointerHeldUntil)
                OnlineVoyage.Current.SendStationAction(replicaRevision + "|r:Select fault " + fault);
            pointerFault = fault;
            pointerHeldUntil = Time.unscaledTime + .16f;
        }

        public OnlineStationState CaptureOnlineStation()
        {
            if (!IsOpen) return new OnlineStationState();
            int hash = 17;
            unchecked
            {
                if (station == StationKind.Circuit)
                    foreach (var connected in hud.circuitConnected) hash = hash * 31 + (connected ? 1 : 0);
                if (station == StationKind.Filter && hud.filterPuzzle.masks != null)
                    foreach (int mask in hud.filterPuzzle.masks) hash = hash * 31 + mask;
            }
            return new OnlineStationState
            {
                open = true, repairStarted = repairStarted, kind = station, revision = actionRevision,
                selectedJob = selectedJobId, selectedWire = repairBoard != null ? repairBoard.selectedWire : -1,
                selectedFault = (int)selectedFault, puzzleHash = hash,
                fishing = fishingActive, fishProgress = fishingProgress, fishingQuality = fishingQuality,
                fishZone = fishingZone, fishPosition = fishingFish, fishWidth = fishZoneHalfWidth,
                liveText = station == StationKind.Fishing && liveText != null ? liveText.text : null,
                batchRecipe = batchRecipe != null ? batchRecipe.id : "", batchQuantity = batchQuantity
            };
        }

        public OnlineStationMotion CaptureOnlineStationMotion()
        {
            if (!IsOpen) return default;
            var sample = new OnlineStationMotion { revision = actionRevision, hostClock = session.elapsed,
                batchQuantity = batchQuantity, fishing = fishingActive, fishHooked = fishingTime >= fishWait,
                fishClock = fishingTime, fishVelocity = fishingVelocity, fishZone = fishingZone,
                fishWidth = fishZoneHalfWidth, fishProgress = fishingProgress };
            if (station == StationKind.Workbench || station == StationKind.HullRepair)
            {
                var job = ActiveJob();
                if (job != null)
                {
                    sample.job = job.id; sample.workClock = job.elapsed; sample.rate = session.craftingManager.GetInteractionRate(job);
                    sample.cueCenter = job.nextCue; sample.note = job.noteDirection; sample.feedbackTime = job.feedbackTime;
                    sample.good = !string.IsNullOrEmpty(job.feedback) && job.feedback.StartsWith("+");
                }
            }
            else if (station == StationKind.Winch)
            {
                sample.workClock = session.winchCycleTime;
                sample.rate = session.winchActive || session.winchElectric ? session.WinchInteractionSpeed : 0;
                sample.cueCenter = session.WinchQteCenter;
            }
            else if (station == StationKind.Generator)
            {
                sample.workClock = session.elapsed;
                sample.rate = 1;
                sample.feedbackTime = generatorFeedback;
                sample.good = generatorGood;
            }
            return sample;
        }

        public void ApplyOnlineStationMotion(OnlineStationMotion sample)
        {
            if (!ReplicaPanel || !IsOpen || sample.revision != replicaRevision) return;
            if (!hasStationMotion || !replicaMotion.fishHooked)
            { displayFishZone = sample.fishZone; displayFishProgress = sample.fishProgress; }
            replicaMotion = sample;
            motionReceivedAt = Time.unscaledTime;
            hasStationMotion = true;
            if (batchRecipe != null) batchQuantity = Mathf.Clamp(sample.batchQuantity, 1, CraftingManager.MaximumBatch);
        }

        float OnlineSampleAge => Mathf.Min(.16f, Mathf.Max(0f, Time.unscaledTime - motionReceivedAt));
        bool HasMotion => ReplicaPanel && hasStationMotion && replicaMotion.revision == replicaRevision;

        public void StampOnlineInput(ref CrewCommand command)
        {
            if (!IsOpen || replicaRevision < 1 || (command.noteKeys == 0 && !command.calibrate)) return;
            command.stationRevision = replicaRevision;
            command.stationClock = HasMotion ? replicaMotion.hostClock + OnlineSampleAge : session.elapsed;
        }

        float InputRewind(CrewCommand command)
        {
            if (!OnlineVoyage.InProgress || ReplicaPanel || actor.role == OnlineVoyage.Current.LocalRole ||
                command.stationRevision != actionRevision) return 0f;
            return Mathf.Clamp(session.elapsed - command.stationClock, 0f, .18f);
        }

        public void ApplyOnlineStation(OnlineStationState state)
        {
            if (!ReplicaPanel) return;
            if (state == null || !state.open || OnlineVoyage.Current.LocalMenuOpen)
            {
                if (IsOpen) Close();
                replicaRevision = -1;
                if (state == null || !state.open) locallyClosedRevision = -1;
                return;
            }
            if (state.revision == locallyClosedRevision) return;
            bool opening = !IsOpen || station != state.kind;
            if (opening) Open(state.kind);
            if (!IsOpen) return;
            bool rebuild = !opening && (replicaRevision != state.revision || repairStarted != state.repairStarted);
            if (replicaRevision != state.revision) hasStationMotion = false;
            replicaRevision = state.revision;
            selectedJobId = state.selectedJob;
            selectedFault = state.selectedFault;
            repairStarted = state.repairStarted;
            var nextRecipe = session.craftingManager.FindRecipe(state.batchRecipe);
            if (nextRecipe != batchRecipe) rebuild = true;
            batchRecipe = nextRecipe;
            if (!HasMotion) batchQuantity = Mathf.Clamp(state.batchQuantity, 1, CraftingManager.MaximumBatch);
            if (opening || rebuild) BuildActions();
            if (repairBoard != null && (replicaPuzzleHash != state.puzzleHash || repairBoard.selectedWire != state.selectedWire))
            {
                repairBoard.selectedWire = state.selectedWire;
                repairBoard.Build(station);
            }
            replicaPuzzleHash = state.puzzleHash;
            if (station == StationKind.Fishing)
            {
                if (liveText != null) liveText.text = state.liveText ?? "";
                fishingQuality = state.fishingQuality;
                if (!HasMotion && state.fishing) ShowFishingRange(state.fishZone, state.fishWidth, state.fishPosition);
            }
        }

        void UpdateReplicaStation()
        {
            if (station != StationKind.Fishing) { UpdateLive(); return; }
            if (!HasMotion) return;
            if (!replicaMotion.fishing || !replicaMotion.fishHooked)
            {
                if (rhythmWindow != null) rhythmWindow.gameObject.SetActive(false);
                if (rhythmCursor != null) rhythmCursor.gameObject.SetActive(false);
                return;
            }
            float correction = 1f - Mathf.Exp(-24f * Time.unscaledDeltaTime);
            float age = OnlineSampleAge;
            float acceleration = OnlinePointerHeld || actor.input.IsActionHeld("Interact") ? fishRiseAcceleration : -fishFallAcceleration;
            float velocity = Mathf.Clamp(replicaMotion.fishVelocity + acceleration * age, -fishMaxVelocity, fishMaxVelocity);
            float predicted = Mathf.Clamp(replicaMotion.fishZone + (replicaMotion.fishVelocity + velocity) * .5f * age,
                replicaMotion.fishWidth, 1f - replicaMotion.fishWidth);
            displayFishZone = Mathf.Lerp(displayFishZone, predicted, correction);
            displayFishProgress = Mathf.Lerp(displayFishProgress, replicaMotion.fishProgress, correction);
            ShowFishingRange(displayFishZone, replicaMotion.fishWidth, FishPosition(replicaMotion.fishClock + OnlineSampleAge));
            liveText.text = FishingText(displayFishProgress);
        }
    }
}
