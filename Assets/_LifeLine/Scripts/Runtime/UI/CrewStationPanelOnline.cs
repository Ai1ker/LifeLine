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
                liveText = station == StationKind.Fishing && liveText != null ? liveText.text : null
            };
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
            replicaRevision = state.revision;
            selectedJobId = state.selectedJob;
            selectedFault = state.selectedFault;
            repairStarted = state.repairStarted;
            if (rebuild) BuildActions();
            if (repairBoard != null && (replicaPuzzleHash != state.puzzleHash || repairBoard.selectedWire != state.selectedWire))
            {
                repairBoard.selectedWire = state.selectedWire;
                repairBoard.Build(station);
            }
            replicaPuzzleHash = state.puzzleHash;
            if (station == StationKind.Fishing)
            {
                if (liveText != null) liveText.text = state.liveText ?? "";
                if (rhythmFill != null) rhythmFill.fillAmount = state.fishProgress;
                if (state.fishing) SetRhythmRange(state.fishZone, state.fishWidth * 2, state.fishPosition);
            }
            else UpdateLive();
        }
    }
}
