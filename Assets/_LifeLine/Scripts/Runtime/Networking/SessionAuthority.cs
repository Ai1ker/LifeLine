using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    /// <summary>
    /// Host-side command validation seam. Local co-op does not create a network session.
    /// The future transport adapter must authenticate peer IDs before registering them.
    /// </summary>
    public class SessionAuthority : MonoBehaviour
    {
        public GameSession session;
        public GameCameraRig cameraRig;
        public bool onlineView;
        public CrewRole localRole;
        public NetworkPeerIdentity localPeer = new NetworkPeerIdentity { displayName = "本地船长", role = CrewRole.Captain };
        public NetworkPeerIdentity remotePeer = new NetworkPeerIdentity { displayName = "本地潜水员", role = CrewRole.Diver };
        public int snapshotRate = 20;
        public uint simulationTick;
        private readonly Dictionary<string, uint> acceptedSequences = new Dictionary<string, uint>();
        private readonly Dictionary<CrewRole, RemoteCrewInputSource> remoteSources = new Dictionary<CrewRole, RemoteCrewInputSource>();
        public string NameFor(CrewRole role) => localPeer.role == role ? localPeer.displayName : remotePeer.displayName;

        public void SetOnlineView(bool enabled, CrewRole role)
        {
            onlineView = enabled; localRole = role;
            if (cameraRig != null) cameraRig.SetNetworkView(enabled, role);
        }

        public bool ApplyAuthenticatedRemoteCommand(string authenticatedPeerId, NetworkCrewCommand packet)
        {
            if (session == null || !onlineView || packet.version != NetworkCrewCommand.ProtocolVersion ||
                string.IsNullOrEmpty(authenticatedPeerId) || authenticatedPeerId != remotePeer.peerId ||
                packet.senderPeerId != authenticatedPeerId || packet.command.role != remotePeer.role) return false;
            if (acceptedSequences.TryGetValue(authenticatedPeerId, out uint previous) && packet.sequence <= previous) return false;
            acceptedSequences[authenticatedPeerId] = packet.sequence;
            if (!remoteSources.TryGetValue(packet.command.role, out RemoteCrewInputSource source))
            {
                source = new RemoteCrewInputSource { role = packet.command.role };
                remoteSources.Add(packet.command.role, source);
                CrewActor actor = packet.command.role == CrewRole.Captain ? session.captain : session.diver;
                if (actor != null) actor.SetExternalInput(source);
            }
            CrewCommand command = packet.command;
            command.move = Vector2.ClampMagnitude(command.move, 1f);
            source.Push(command);
            return true;
        }
    }
}
