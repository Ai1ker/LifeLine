using System;
using UnityEngine;

namespace Lifeline
{
    public enum NetworkConnectionState { Offline, Connecting, Hosting, Connected, Disconnected }

    [Serializable]
    public class NetworkPeerIdentity
    {
        public string peerId;
        public string steamId;
        public string displayName;
        public CrewRole role;
    }

    [Serializable]
    public struct NetworkCrewCommand
    {
        public const int ProtocolVersion = 10;
        public int version;
        public uint sequence;
        public uint tick;
        public string senderPeerId;
        public bool edgeOnly;
        public CrewCommand command;
    }

    [Serializable]
    public struct CrewNetworkState
    {
        public CrewRole role;
        public Vector3 position;
        public bool onboard, downed;
        public float oxygen;
        public ResourceKind[] backpack;
    }

    [Serializable]
    public class SessionNetworkSnapshot
    {
        public int protocolVersion = NetworkCrewCommand.ProtocolVersion;
        public uint tick;
        public int seed, day, radioStage;
        public float elapsed, hull, power;
        public Vector3 shipPosition;
        public CrewNetworkState captain, diver;
        public ResourceAmount[] bank;
        public int[] upgradeLevels;
        // Extend the versioned snapshot with reliable spawn/job/repair events when a
        // Mirror adapter is added. This DTO is a transport boundary, not live online code.
    }

    /// <summary>Implement with a Mirror-compatible Steam transport in the online phase.</summary>
    public interface IGameTransport : IDisposable
    {
        NetworkConnectionState State { get; }
        bool IsHost { get; }
        NetworkPeerIdentity LocalPeer { get; }
        event Action<NetworkPeerIdentity> PeerJoined;
        event Action<string> PeerLeft;
        event Action<NetworkCrewCommand> CommandReceived;
        event Action<SessionNetworkSnapshot> SnapshotReceived;
        void Host();
        void Join(string steamHostId);
        void Leave();
        void SendCommand(NetworkCrewCommand command);
        void BroadcastSnapshot(SessionNetworkSnapshot snapshot);
    }

    public interface ILocalPeerIdentity
    {
        string SteamId { get; }
        string DisplayName { get; }
    }

    public sealed class RemoteCrewInputSource : ICrewInputSource
    {
        public CrewRole role;
        public float maximumInputAge = .3f;
        private CrewCommand continuous, edges;
        private float lastContinuous = float.NegativeInfinity, lastEdge = float.NegativeInfinity;
        private bool enabled = true;
        public void Push(CrewCommand next)
        {
            if (!enabled) return;
            continuous = new CrewCommand
            {
                move = next.move, collect = next.collect, interactHeld = next.interactHeld,
                sprint = next.sprint, hideSeparate = next.hideSeparate
            };
            lastContinuous = Time.unscaledTime;
            PushEdges(next);
        }
        public void PushEdges(CrewCommand next)
        {
            if (!enabled || !HasEdges(next)) return;
            if (Time.unscaledTime - lastEdge > maximumInputAge) edges = default;
            // Either channel can arrive first. Buffer taps without replacing held inputs.
            edges.jump |= next.jump;
            edges.interact |= next.interact;
            edges.melee |= next.melee;
            edges.fire |= next.fire;
            edges.signal |= next.signal;
            edges.bait |= next.bait;
            edges.hide |= next.hide;
            edges.hideSeparate |= next.hideSeparate;
            edges.calibrate |= next.calibrate;
            edges.noteKeys |= next.noteKeys;
            if (next.calibrate || next.noteKeys != 0)
            {
                edges.stationRevision = next.stationRevision;
                edges.stationClock = next.stationClock;
            }
            lastEdge = Time.unscaledTime;
        }
        public static bool HasEdges(CrewCommand command) => command.jump || command.interact || command.melee ||
            command.fire || command.signal || command.bait || command.hide || command.calibrate || command.noteKeys != 0;

        public CrewCommand ReadCommand()
        {
            var next = new CrewCommand { role = role };
            if (!enabled) return next;
            if (Time.unscaledTime - lastContinuous <= maximumInputAge)
            {
                next = continuous;
                next.role = role;
            }
            if (Time.unscaledTime - lastEdge <= maximumInputAge)
            {
                next.jump = edges.jump;
                next.interact = edges.interact;
                next.melee = edges.melee;
                next.fire = edges.fire;
                next.signal = edges.signal;
                next.bait = edges.bait;
                next.hide = edges.hide;
                next.hideSeparate |= edges.hideSeparate;
                next.calibrate = edges.calibrate;
                next.noteKeys = edges.noteKeys;
                next.stationRevision = edges.stationRevision;
                next.stationClock = edges.stationClock;
            }
            edges = default;
            return next;
        }
        public void SetEnabled(bool value)
        {
            enabled = value;
            if (value) return;
            continuous = edges = default;
            lastContinuous = lastEdge = float.NegativeInfinity;
        }
    }
}
