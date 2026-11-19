#if LIFELINE_STEAM
using Mirror;
using UnityEngine;

namespace Lifeline.Steam
{
    /// <summary>Runs Mirror's transport while game scenes and actors remain owned by LifeLine.</summary>
    public sealed class MirrorGameplayManager : NetworkManager
    {
        internal MirrorGameplayTransport Link { get; set; }

        public override void Update()
        {
            base.Update();
            Link?.Poll();
        }

        public override void OnStartServer() => Link?.RegisterServerMessages();
        public override void OnStartClient() => Link?.RegisterClientMessages();
        public override void OnServerConnect(NetworkConnectionToClient connection) => Link?.AwaitAdmission(connection);
        public override void OnServerDisconnect(NetworkConnectionToClient connection) => Link?.ServerDisconnected(connection);
        public override void OnClientConnect() => Link?.ClientConnected();
        public override void OnClientDisconnect() => Link?.ClientDisconnected();
        public override void OnClientError(TransportError error, string reason) => Link?.ClientError(reason);

        public override void OnServerAddPlayer(NetworkConnectionToClient connection) => connection.Disconnect();
        public override void OnServerReady(NetworkConnectionToClient connection) { }
        public override void OnClientSceneChanged() { }

        public override void OnDestroy()
        {
            Link?.ManagerDestroyed();
            base.OnDestroy();
        }
    }
}
#endif
