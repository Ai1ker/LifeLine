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
        public const int ProtocolVersion = 1;
        public int version;
        public uint sequence;
        public uint tick;
        public string senderPeerId;
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
        private CrewCommand command;
        private float lastReceived;
        private bool enabled = true;
        public void Push(CrewCommand next)
        {
            command = next; command.role = role; lastReceived = Time.unscaledTime;
        }
        public CrewCommand ReadCommand()
        {
            if (!enabled || Time.unscaledTime - lastReceived > maximumInputAge) return new CrewCommand { role = role };
            CrewCommand next = command;
            command.jump = command.interact = command.melee = command.fire = command.signal = command.bait = command.hide = command.pause = false;
            return next;
        }
        public void SetEnabled(bool value) { enabled = value; }
    }
}
