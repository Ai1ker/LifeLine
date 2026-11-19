#if LIFELINE_STEAM
using Mirror.FizzySteam;

namespace Lifeline.Steam
{
    public sealed class FizzyGameplayTransport : FizzySteamworks
    {
        public override void ServerDisconnect(int connectionId)
        {
            base.ServerDisconnect(connectionId);
            // Fizzy 6.0.1's legacy server removes the peer without raising the
            // event Mirror needs to release its connection slot for a rejoin.
            if (!UseNextGenSteamNetworking) OnServerDisconnected?.Invoke(connectionId);
        }
    }
}
#endif
