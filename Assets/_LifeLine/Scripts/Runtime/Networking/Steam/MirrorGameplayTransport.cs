#if LIFELINE_STEAM
using System;
using System.Globalization;
using System.Text;
using Mirror;
using UnityEngine;
using Channel = Mirror.Channels;

namespace Lifeline.Steam
{
    public struct LifeLinePeerHello : NetworkMessage
    {
        public int protocol;
        public ulong steamId;
    }

    public struct LifeLinePeerWelcome : NetworkMessage
    {
        public int protocol;
        public ulong hostSteamId;
        public byte role;
    }

    public struct LifeLinePeerInput : NetworkMessage
    {
        public uint sequence, tick;
        public float horizontal, vertical;
        public ushort buttons;
        public byte notes;
        public int stationRevision;
        public float stationClock;
    }

    public struct LifeLinePeerAction : NetworkMessage
    {
        public string json;
    }

    public struct LifeLineStateChunk : NetworkMessage
    {
        public uint id;
        public int offset, total;
        public bool legacySnapshot;
        public ArraySegment<byte> bytes;
    }

    public struct LifeLineStateAck : NetworkMessage
    {
        public uint id;
        public int received;
    }

    public struct LifeLineCrewMotion : NetworkMessage
    {
        public OnlineCrewMotion motion;
    }

    public struct LifeLineCrewAttack : NetworkMessage
    {
        public byte role, kind;
    }

    public sealed class MirrorGameplayTransport : IOnlineGameplayTransport
    {
        const int MaxActionBytes = 2048;
        const int ChunkSize = 32 * 1024;
        const int FramesInFlight = 4;
        const double ConnectTimeout = 25;
        const double TransferTimeout = 20;

        public int MaximumStateBytes { get; set; } = 8 * 1024 * 1024;
        public NetworkConnectionState State { get; private set; }
        public bool IsHost => State == NetworkConnectionState.Hosting;
        public bool CanQueueReliableState => admittedConnection != null && pendingBaseline == null;
        public float StateDownloadProgress => incoming == null ? 0 : incoming.sent / (float)incoming.bytes.Length;
        public NetworkPeerIdentity LocalPeer { get; private set; }

        public event Action<NetworkPeerIdentity> PeerJoined;
        public event Action<string> PeerLeft;
        public event Action<NetworkCrewCommand> CommandReceived;
        public event Action<SessionNetworkSnapshot> SnapshotReceived;
        public event Action<string> LinkFailed;
        public event Action<string> AdmissionReady;
        public event Action<ArraySegment<byte>> WorldStateReceived;
        public event Action<string, string> ActionReceived;
        public event Action<OnlineCrewMotion> MotionReceived;
        public event Action<CrewRole, CrewVisualEvent> AttackReceived;

        MirrorGameplayManager manager;
        Func<string, NetworkPeerIdentity> admitPeer;
        NetworkConnectionToClient pendingConnection;
        NetworkConnectionToClient rejectedConnection;
        NetworkConnectionToClient admittedConnection;
        NetworkPeerIdentity remotePeer;
        ulong localSteamId, expectedHost;
        double connectionDeadline;
        string scheduledFailure;
        bool stopping, disposed, originalBackgroundMode;
        uint lastInputSequence, frameSequence;
        double lastInputBudgetAt, lastActionBudgetAt;
        double inputBudget, actionBudget;

        sealed class StateFrame
        {
            public byte[] bytes;
            public uint id;
            public int sent, acknowledged;
            public bool legacySnapshot;
            public double deadline;
        }

        StateFrame outgoing, pendingBaseline, pendingUpdate, incoming;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InstallFactory() => OnlineGameplayTransport.Factory = () => new MirrorGameplayTransport();

        public void Configure(NetworkPeerIdentity local, Func<string, NetworkPeerIdentity> admitPeer)
        {
            if (State == NetworkConnectionState.Hosting || State == NetworkConnectionState.Connecting || State == NetworkConnectionState.Connected)
                throw new InvalidOperationException("Stop the current gameplay connection before configuring it.");
            LocalPeer = local ?? throw new ArgumentNullException(nameof(local));
            this.admitPeer = admitPeer ?? throw new ArgumentNullException(nameof(admitPeer));
            if (!TryParseSteamId(local.steamId, out localSteamId))
                throw new ArgumentException("Steam identity is unavailable.", nameof(local));
        }

        public void Host()
        {
            Begin(NetworkConnectionState.Hosting, () =>
            {
                manager.StartServer();
                if (!NetworkServer.active || !manager.transport.ServerActive())
                    throw new InvalidOperationException("Steam could not open the gameplay connection.");
                Debug.Log("LifeLine Steam: gameplay listener ready.");
            });
        }

        public void Join(string steamHostId)
        {
            if (!TryParseSteamId(steamHostId, out ulong hostId) || hostId == localSteamId)
            {
                LinkFailed?.Invoke("The room does not have a valid Steam host.");
                return;
            }

            Begin(NetworkConnectionState.Connecting, () =>
            {
                expectedHost = hostId;
                connectionDeadline = Time.realtimeSinceStartupAsDouble + ConnectTimeout;
                manager.networkAddress = steamHostId;
                manager.StartClient();
            });
        }

        void Begin(NetworkConnectionState state, Action start)
        {
            if (disposed || State == NetworkConnectionState.Hosting || State == NetworkConnectionState.Connecting || State == NetworkConnectionState.Connected)
                return;
            if (LocalPeer == null || admitPeer == null)
            {
                LinkFailed?.Invoke("Choose a crew role before starting the gameplay connection.");
                return;
            }
            try
            {
                originalBackgroundMode = Application.runInBackground;
                EnsureManager();
                State = state;
                start();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("LifeLine Steam transport: " + exception.Message);
                Fail("Steam could not establish the gameplay connection.");
            }
        }

        void EnsureManager()
        {
            if (manager != null) return;
            if (NetworkManager.singleton != null)
                throw new InvalidOperationException("Another Mirror NetworkManager is active.");
            var root = new GameObject("LifeLine Steam gameplay");
            root.SetActive(false);
            var transport = root.AddComponent<FizzyGameplayTransport>();
            // App ID 480 can use direct P2P with relay fallback via Fizzy's SteamNetworking path.
            transport.UseNextGenSteamNetworking = false;
            transport.AllowSteamRelay = true;
            transport.Timeout = (int)ConnectTimeout;
            manager = root.AddComponent<MirrorGameplayManager>();
            manager.Link = this;
            manager.transport = transport;
            manager.autoCreatePlayer = false;
            manager.maxConnections = 1;
            manager.disconnectInactiveConnections = true;
            manager.disconnectInactiveTimeout = 35f;
            root.SetActive(true);
        }

        internal void RegisterServerMessages()
        {
            NetworkServer.RegisterHandler<LifeLinePeerHello>(ReceiveHello, false);
            NetworkServer.RegisterHandler<LifeLinePeerInput>(ReceiveInput);
            NetworkServer.RegisterHandler<LifeLinePeerAction>(ReceiveAction);
            NetworkServer.RegisterHandler<LifeLineStateAck>(ReceiveAcknowledgement);
        }

        internal void RegisterClientMessages()
        {
            NetworkClient.RegisterHandler<LifeLinePeerWelcome>(ReceiveWelcome);
            NetworkClient.RegisterHandler<LifeLineStateChunk>(ReceiveChunk);
            NetworkClient.RegisterHandler<LifeLineCrewMotion>(message =>
            {
                if (State == NetworkConnectionState.Connected) MotionReceived?.Invoke(message.motion);
            });
            NetworkClient.RegisterHandler<LifeLineCrewAttack>(message =>
            {
                var kind = (CrewVisualEvent)message.kind;
                if (State == NetworkConnectionState.Connected && message.role <= (byte)CrewRole.Diver &&
                    (kind == CrewVisualEvent.Melee || kind == CrewVisualEvent.Fire))
                    AttackReceived?.Invoke((CrewRole)message.role, kind);
            });
        }

        public void BroadcastMotion(OnlineCrewMotion motion) =>
            admittedConnection?.Send(new LifeLineCrewMotion { motion = motion }, Channel.Unreliable);

        public void BroadcastAttack(CrewRole role, CrewVisualEvent kind) =>
            admittedConnection?.Send(new LifeLineCrewAttack { role = (byte)role, kind = (byte)kind }, Channel.Reliable);

        internal void AwaitAdmission(NetworkConnectionToClient connection)
        {
            Debug.Log("LifeLine Steam: guest transport connected; checking room membership.");
            connection.isAuthenticated = false;
            pendingConnection = connection;
            connectionDeadline = Time.realtimeSinceStartupAsDouble + ConnectTimeout;
        }

        internal void ClientConnected()
        {
            Debug.Log("LifeLine Steam: host transport connected; sending gameplay admission request.");
            NetworkClient.Send(new LifeLinePeerHello
            {
                protocol = NetworkCrewCommand.ProtocolVersion,
                steamId = localSteamId
            }, Channel.Reliable);
        }

        void ReceiveHello(NetworkConnectionToClient connection, LifeLinePeerHello hello)
        {
            if (!IsHost || connection != pendingConnection || connection == rejectedConnection || admittedConnection != null)
                return;
            string address = manager.transport.ServerGetClientAddress(connection.connectionId);
            if (hello.protocol != NetworkCrewCommand.ProtocolVersion
                || !TryParseSteamId(address, out ulong authenticatedSteamId)
                || authenticatedSteamId == localSteamId || hello.steamId != authenticatedSteamId)
            {
                RejectAdmission(connection, "The gameplay protocol or Steam identity did not match.");
                return;
            }
            NetworkPeerIdentity peer = ResolvePeer(address);
            if (peer == null)
            {
                RejectAdmission(connection, "The Steam user did not match the room's assigned teammate.");
                return;
            }

            connection.isAuthenticated = true;
            pendingConnection = null;
            remotePeer = peer;
            admittedConnection = connection;
            lastInputSequence = 0;
            lastInputBudgetAt = lastActionBudgetAt = Time.realtimeSinceStartupAsDouble;
            inputBudget = 120;
            actionBudget = 30;
            connection.Send(new LifeLinePeerWelcome
            {
                protocol = NetworkCrewCommand.ProtocolVersion,
                hostSteamId = localSteamId,
                role = (byte)peer.role
            }, Channel.Reliable);
            PeerJoined?.Invoke(peer);
            AdmissionReady?.Invoke(address);
        }

        void RejectAdmission(NetworkConnectionToClient connection, string reason)
        {
            Debug.LogWarning("LifeLine Steam: " + reason);
            rejectedConnection = connection;
        }

        void ReceiveWelcome(LifeLinePeerWelcome welcome)
        {
            if (State != NetworkConnectionState.Connecting) return;
            if (welcome.protocol != NetworkCrewCommand.ProtocolVersion || welcome.hostSteamId != expectedHost
                || welcome.role != (byte)LocalPeer.role)
            {
                scheduledFailure = "The host gameplay version or crew role does not match this room.";
                return;
            }
            string hostId = expectedHost.ToString(CultureInfo.InvariantCulture);
            NetworkPeerIdentity peer = ResolvePeer(hostId);
            if (peer == null)
            {
                scheduledFailure = "The Steam host is no longer part of this room.";
                return;
            }
            remotePeer = peer;
            State = NetworkConnectionState.Connected;
            PeerJoined?.Invoke(peer);
            AdmissionReady?.Invoke(hostId);
        }

        NetworkPeerIdentity ResolvePeer(string steamId)
        {
            NetworkPeerIdentity peer = admitPeer?.Invoke(steamId);
            return peer != null && peer.steamId == steamId && !string.IsNullOrEmpty(peer.peerId)
                && Enum.IsDefined(typeof(CrewRole), peer.role) && peer.role != LocalPeer.role ? peer : null;
        }

        public void SendCommand(NetworkCrewCommand command)
        {
            if (State != NetworkConnectionState.Connected || !NetworkClient.isConnected
                || command.version != NetworkCrewCommand.ProtocolVersion)
                return;
            CrewCommand crew = command.command;
            NetworkClient.Send(new LifeLinePeerInput
            {
                sequence = command.sequence,
                tick = command.tick,
                horizontal = crew.move.x,
                vertical = crew.move.y,
                buttons = PackButtons(crew), notes = crew.noteKeys,
                stationRevision = crew.stationRevision, stationClock = crew.stationClock
            }, Channel.Reliable);
        }

        void ReceiveInput(NetworkConnectionToClient connection, LifeLinePeerInput input)
        {
            if (connection != admittedConnection || input.sequence <= lastInputSequence
                || !IsFinite(input.horizontal) || !IsFinite(input.vertical) || !IsFinite(input.stationClock)
                || !UseBudget(ref inputBudget, ref lastInputBudgetAt, 120))
                return;
            lastInputSequence = input.sequence;
            CrewCommand crew = UnpackButtons(input.buttons);
            crew.role = remotePeer.role;
            crew.noteKeys = (byte)(input.notes & 63);
            crew.stationRevision = input.stationRevision;
            crew.stationClock = input.stationClock;
            crew.move = Vector2.ClampMagnitude(new Vector2(Mathf.Clamp(input.horizontal, -1, 1), Mathf.Clamp(input.vertical, -1, 1)), 1);
            CommandReceived?.Invoke(new NetworkCrewCommand
            {
                version = NetworkCrewCommand.ProtocolVersion,
                sequence = input.sequence,
                tick = input.tick,
                senderPeerId = remotePeer.peerId,
                command = crew
            });
        }

        public void SendAction(string action)
        {
            if (State == NetworkConnectionState.Connected && NetworkClient.isConnected && IsBoundedAction(action))
                NetworkClient.Send(new LifeLinePeerAction { json = action }, Channel.Reliable);
        }

        void ReceiveAction(NetworkConnectionToClient connection, LifeLinePeerAction action)
        {
            if (connection == admittedConnection && IsBoundedAction(action.json)
                && UseBudget(ref actionBudget, ref lastActionBudgetAt, 30))
                ActionReceived?.Invoke(remotePeer.steamId, action.json);
        }

        public void BroadcastWorldState(ArraySegment<byte> payload, bool replaceable = false) =>
            QueueState(payload, false, replaceable);

        public void BroadcastSnapshot(SessionNetworkSnapshot snapshot)
        {
            if (snapshot == null) return;
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(snapshot));
            QueueState(new ArraySegment<byte>(bytes), true, true);
        }

        void QueueState(ArraySegment<byte> payload, bool legacySnapshot, bool replaceable)
        {
            if (!IsHost || admittedConnection == null || payload.Count == 0) return;
            if (payload.Count > MaximumStateBytes)
            {
                Debug.LogWarning($"LifeLine online state exceeds the {MaximumStateBytes} byte transport limit.");
                return;
            }
            if (!replaceable && pendingBaseline != null)
            {
                // A reliable baseline must reach the guest before updates based on it.
                // Retry admission and send the newest baseline in the new connection.
                admittedConnection.Disconnect();
                return;
            }
            var frame = new StateFrame { bytes = new byte[payload.Count], legacySnapshot = legacySnapshot };
            Buffer.BlockCopy(payload.Array, payload.Offset, frame.bytes, 0, payload.Count);
            if (outgoing == null)
                outgoing = frame;
            else if (replaceable)
                pendingUpdate = frame;
            else
            {
                pendingBaseline = frame;
                pendingUpdate = null;
            }
        }

        void SendChunks(double now)
        {
            if (outgoing == null || admittedConnection == null) return;
            int size = PacketChunkSize();
            if (size <= 0) { scheduledFailure = "The Steam gameplay packet size is invalid."; return; }
            if (outgoing.deadline == 0)
            {
                outgoing.id = ++frameSequence;
                outgoing.deadline = now + TransferTimeout;
            }
            if (now >= outgoing.deadline)
            {
                admittedConnection.Disconnect();
                return;
            }
            while (outgoing != null && outgoing.sent < outgoing.bytes.Length
                && outgoing.sent - outgoing.acknowledged < size * FramesInFlight)
            {
                int offset = outgoing.sent;
                int length = Math.Min(size, outgoing.bytes.Length - offset);
                outgoing.sent += length;
                admittedConnection.Send(new LifeLineStateChunk
                {
                    id = outgoing.id, offset = offset, total = outgoing.bytes.Length,
                    legacySnapshot = outgoing.legacySnapshot,
                    bytes = new ArraySegment<byte>(outgoing.bytes, offset, length)
                }, Channel.Reliable);
            }
        }

        void ReceiveAcknowledgement(NetworkConnectionToClient connection, LifeLineStateAck ack)
        {
            if (connection != admittedConnection || outgoing == null || ack.id != outgoing.id
                || ack.received <= outgoing.acknowledged || ack.received > outgoing.sent)
                return;
            outgoing.acknowledged = ack.received;
            outgoing.deadline = Time.realtimeSinceStartupAsDouble + TransferTimeout;
            if (outgoing.acknowledged == outgoing.bytes.Length)
            {
                outgoing = pendingBaseline ?? pendingUpdate;
                if (pendingBaseline != null) pendingBaseline = null;
                else pendingUpdate = null;
            }
        }

        void ReceiveChunk(LifeLineStateChunk chunk)
        {
            if (State != NetworkConnectionState.Connected) return;
            if (chunk.total <= 0 || chunk.total > MaximumStateBytes || chunk.offset < 0
                || chunk.bytes.Count <= 0 || chunk.bytes.Count > ChunkSize || chunk.offset > chunk.total - chunk.bytes.Count)
            {
                scheduledFailure = "The host sent an invalid gameplay state.";
                return;
            }
            if (incoming == null)
            {
                if (chunk.offset != 0) { scheduledFailure = "The incoming gameplay state is incomplete."; return; }
                incoming = new StateFrame { id = chunk.id, bytes = new byte[chunk.total], legacySnapshot = chunk.legacySnapshot };
            }
            if (incoming.id != chunk.id || incoming.bytes.Length != chunk.total || incoming.sent != chunk.offset
                || incoming.legacySnapshot != chunk.legacySnapshot)
            {
                scheduledFailure = "The incoming gameplay state is incomplete.";
                return;
            }
            Buffer.BlockCopy(chunk.bytes.Array, chunk.bytes.Offset, incoming.bytes, chunk.offset, chunk.bytes.Count);
            incoming.sent += chunk.bytes.Count;
            incoming.deadline = Time.realtimeSinceStartupAsDouble + TransferTimeout;
            NetworkClient.Send(new LifeLineStateAck { id = chunk.id, received = incoming.sent }, Channel.Reliable);
            if (incoming.sent != incoming.bytes.Length) return;

            StateFrame complete = incoming;
            incoming = null;
            if (complete.legacySnapshot)
            {
                var snapshot = JsonUtility.FromJson<SessionNetworkSnapshot>(Encoding.UTF8.GetString(complete.bytes));
                if (snapshot?.protocolVersion == NetworkCrewCommand.ProtocolVersion) SnapshotReceived?.Invoke(snapshot);
            }
            else WorldStateReceived?.Invoke(new ArraySegment<byte>(complete.bytes));
        }

        int PacketChunkSize() => Math.Min(ChunkSize, manager.transport.GetMaxPacketSize(Channel.Reliable) - 128);

        internal void Poll()
        {
            if (stopping || disposed) return;
            if (scheduledFailure != null)
            {
                string message = scheduledFailure;
                scheduledFailure = null;
                Fail(message);
                return;
            }
            double now = Time.realtimeSinceStartupAsDouble;
            if (State == NetworkConnectionState.Connecting && now >= connectionDeadline)
            {
                Fail(NetworkClient.isConnected ? "Steam connected but gameplay admission timed out." :
                    "The Steam gameplay transport timed out before connecting to the host.");
                return;
            }
            if (IsHost)
            {
                if (rejectedConnection != null)
                {
                    var connection = rejectedConnection;
                    rejectedConnection = null;
                    connection.Disconnect();
                }
                else if (pendingConnection != null && now >= connectionDeadline)
                {
                    Debug.LogWarning("LifeLine Steam: guest gameplay admission timed out.");
                    pendingConnection.Disconnect();
                }
                SendChunks(now);
            }
            if (incoming != null && now >= incoming.deadline) Fail("The host gameplay state did not finish downloading.");
        }

        internal void ServerDisconnected(NetworkConnectionToClient connection)
        {
            if (connection == rejectedConnection) rejectedConnection = null;
            if (connection == pendingConnection) pendingConnection = null;
            if (connection != admittedConnection) return;
            admittedConnection = null;
            outgoing = pendingBaseline = pendingUpdate = null;
            string peerId = remotePeer?.peerId;
            remotePeer = null;
            if (!stopping && !string.IsNullOrEmpty(peerId)) PeerLeft?.Invoke(peerId);
        }

        internal void ClientDisconnected()
        {
            if (!stopping && (State == NetworkConnectionState.Connecting || State == NetworkConnectionState.Connected))
                scheduledFailure ??= "The Steam gameplay connection closed.";
        }

        internal void ClientError(string reason)
        {
            if (stopping) return;
            Debug.LogWarning("LifeLine Steam transport: " + reason);
            scheduledFailure ??= "Steam lost the gameplay connection.";
        }

        void Fail(string message)
        {
            string peerId = remotePeer?.peerId;
            Leave();
            State = NetworkConnectionState.Disconnected;
            if (!string.IsNullOrEmpty(peerId)) PeerLeft?.Invoke(peerId);
            LinkFailed?.Invoke(message);
        }

        public void Leave()
        {
            if (stopping) return;
            stopping = true;
            try
            {
                if (manager != null)
                {
                    if (NetworkServer.active) manager.StopServer();
                    else if (NetworkClient.active) manager.StopClient();
                    // Some transport failures report an error without completing disconnect.
                    if (NetworkClient.active || NetworkClient.connection != null)
                        NetworkClient.Shutdown();
                    Application.runInBackground = originalBackgroundMode;
                }
            }
            finally
            {
                admittedConnection = pendingConnection = rejectedConnection = null;
                remotePeer = null;
                incoming = outgoing = pendingBaseline = pendingUpdate = null;
                scheduledFailure = null;
                State = NetworkConnectionState.Offline;
                stopping = false;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Leave();
            if (manager == null) return;
            var root = manager.gameObject;
            if (NetworkManager.singleton == manager) NetworkManager.ResetStatics();
            if (Transport.active == manager.transport) Transport.active = null;
            manager.Link = null;
            manager = null;
            UnityEngine.Object.Destroy(root);
        }

        internal void ManagerDestroyed()
        {
            manager = null;
            State = NetworkConnectionState.Offline;
        }

        static bool TryParseSteamId(string value, out ulong parsed)
            => ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed) && parsed != 0;

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool IsBoundedAction(string action) => !string.IsNullOrEmpty(action) && Encoding.UTF8.GetByteCount(action) <= MaxActionBytes;

        static bool UseBudget(ref double budget, ref double last, int perSecond)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            budget = Math.Min(perSecond, budget + (now - last) * perSecond);
            last = now;
            if (budget < 1) return false;
            budget--;
            return true;
        }

        static ushort PackButtons(CrewCommand command) => (ushort)(
            (command.jump ? 1 : 0) | (command.interact ? 2 : 0) | (command.collect ? 4 : 0)
            | (command.melee ? 8 : 0) | (command.fire ? 16 : 0) | (command.sprint ? 32 : 0)
            | (command.signal ? 64 : 0) | (command.bait ? 128 : 0) | (command.hide ? 256 : 0)
            | (command.interactHeld ? 512 : 0) | (command.calibrate ? 1024 : 0) | (command.hideSeparate ? 2048 : 0));

        static CrewCommand UnpackButtons(ushort buttons) => new CrewCommand
        {
            jump = (buttons & 1) != 0, interact = (buttons & 2) != 0, collect = (buttons & 4) != 0,
            melee = (buttons & 8) != 0, fire = (buttons & 16) != 0, sprint = (buttons & 32) != 0,
            signal = (buttons & 64) != 0, bait = (buttons & 128) != 0, hide = (buttons & 256) != 0,
            interactHeld = (buttons & 512) != 0, calibrate = (buttons & 1024) != 0, hideSeparate = (buttons & 2048) != 0
        };
    }
}
#endif
