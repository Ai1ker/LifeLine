using System;

namespace Lifeline
{
    /// <summary>Game messages over the admitted Steam peer connection.</summary>
    public interface IOnlineGameplayTransport : IGameTransport
    {
        event Action<string> LinkFailed;
        event Action<string> AdmissionReady;
        event Action<ArraySegment<byte>> WorldStateReceived;
        event Action<string, string> ActionReceived;

        bool CanQueueReliableState { get; }
        void Configure(NetworkPeerIdentity local, Func<string, NetworkPeerIdentity> admitPeer);
        /// <summary>Reliable state. Only the pending replaceable update may be superseded.</summary>
        void BroadcastWorldState(ArraySegment<byte> payload, bool replaceable = false);
        void SendAction(string action);
    }

    public static class OnlineGameplayTransport
    {
        // Assigned by the optional Steam assembly before the main menu opens.
        public static Func<IOnlineGameplayTransport> Factory { get; set; }
        public static IOnlineGameplayTransport Create() => Factory?.Invoke();
    }
}
