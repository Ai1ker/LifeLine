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
        uint latestMovementSequence, latestActionSequence;
        private readonly Dictionary<CrewRole, RemoteCrewInputSource> remoteSources = new Dictionary<CrewRole, RemoteCrewInputSource>();
        public string NameFor(CrewRole role) => localPeer.role == role ? localPeer.displayName : remotePeer.displayName;

        public void SetOnlineView(bool enabled, CrewRole role)
        {
            onlineView = enabled; localRole = role;
            if (cameraRig != null) cameraRig.SetNetworkView(enabled, role);
            ApplyCrewRenderOrder(session, enabled, role);
        }

        public static void ApplyCrewRenderOrder(GameSession game, bool online, CrewRole localRole)
        {
            bool diverInFront = online && localRole == CrewRole.Diver;
            game?.captain?.GetComponent<CrewPresentation>()?.SetFront(!diverInFront);
            game?.diver?.GetComponent<CrewPresentation>()?.SetFront(diverInFront);
        }

        public void BindRemoteInput()
        {
            latestMovementSequence = latestActionSequence = 0;
            var source = new RemoteCrewInputSource { role = remotePeer.role };
            remoteSources[remotePeer.role] = source;
            ActorFor(remotePeer.role)?.SetExternalInput(source);
        }

        public void ReleaseRemoteInput()
        {
            if (remoteSources.TryGetValue(remotePeer.role, out var source)) source.SetEnabled(false);
            latestMovementSequence = latestActionSequence = 0;
        }

        CrewActor ActorFor(CrewRole role) => role == CrewRole.Captain ? session.captain : session.diver;

        public bool ApplyAuthenticatedRemoteCommand(string authenticatedPeerId, NetworkCrewCommand packet)
        {
            if (session == null || !onlineView || packet.version != NetworkCrewCommand.ProtocolVersion ||
                string.IsNullOrEmpty(authenticatedPeerId) || authenticatedPeerId != remotePeer.peerId ||
                packet.senderPeerId != authenticatedPeerId || packet.command.role != remotePeer.role) return false;
            if (!float.IsFinite(packet.command.move.x) || !float.IsFinite(packet.command.move.y)) return false;
            ref uint latest = ref (packet.edgeOnly ? ref latestActionSequence : ref latestMovementSequence);
            if (packet.sequence <= latest) return false;
            latest = packet.sequence;
            if (!remoteSources.TryGetValue(packet.command.role, out RemoteCrewInputSource source))
            {
                source = new RemoteCrewInputSource { role = packet.command.role };
                remoteSources.Add(packet.command.role, source);
                CrewActor actor = packet.command.role == CrewRole.Captain ? session.captain : session.diver;
                if (actor != null) actor.SetExternalInput(source);
            }
            CrewCommand command = packet.command;
            command.pause = false;
            command.move = Vector2.ClampMagnitude(command.move, 1f);
            if (packet.edgeOnly) source.PushEdges(command);
            else source.Push(command);
            return true;
        }
    }
}
