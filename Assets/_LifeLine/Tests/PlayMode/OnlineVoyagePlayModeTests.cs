using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Lifeline.Tests
{
    public sealed class OnlineVoyagePlayModeTests
    {
        [UnityTest]
        public IEnumerator HostAndGuestUseOnePlayerViewAndRejoinThroughTheAuthoritativeState()
        {
            var roomFactory = OnlineRoomService.Factory;
            var transportFactory = OnlineGameplayTransport.Factory;
            Assert.That(OnlineVoyage.Current, Is.Not.Null);
            Assert.That(OnlineRoomService.Existing, Is.Null);
            var root = new GameObject("In-process online fixture");
            Object.DontDestroyOnLoad(root);
            var room = root.AddComponent<TestRoom>();
            room.hostId = ((ulong)DateTime.UtcNow.Ticks).ToString();
            string hostSave = OnlineSaveSlot.PathFor(room.hostId);
            var hostLink = new TestLink();
            OnlineRoomService.Factory = () => room;
            OnlineGameplayTransport.Factory = () => hostLink;
            _ = OnlineRoomService.Instance;
            try
            {
                room.Create(false);
                Assert.That(room.CanStart, Is.False, "Both roles must be assigned.");
                room.AssignRoles();
                Assert.That(room.CanStart, Is.True);
                room.StartGame();
                yield return Until(() => SceneManager.GetActiveScene().name == "Gameplay" &&
                    GameSession.Current != null && GameSession.Current.IsInitialized);
                var game = GameSession.Current;
                var save = Object.FindAnyObjectByType<GameSaveManager>();
                Assert.That(save.SavePath, Is.EqualTo(hostSave));
                Assert.That(game.diver.input.UseOnlineBindings, Is.True);
                var move = game.diver.input.RuntimeActions.FindAction("Captain/Move", true);
                Assert.That(move.enabled, Is.True);
                Assert.That(move.bindings[1].path, Is.EqualTo("<Keyboard>/w"));
                UnityEngine.InputSystem.InputActionRebindingExtensions.ApplyBindingOverride(move, 1, "<Keyboard>/i");
                Assert.That(game.diver.input.GetBindingLabel("Move", 1), Is.EqualTo("I").IgnoreCase);
                var rig = Object.FindAnyObjectByType<GameCameraRig>();
                Assert.That(rig.IsSplitScreen, Is.False);
                Assert.That(rig.diverCamera.enabled && !rig.captainCamera.enabled, Is.True);
                yield return Until(() => OnlineVoyage.WaitingForCrew && game.phase == SessionPhase.Paused);

                hostLink.Admit(room.guestId);
                yield return Until(() => hostLink.baseline != null && game.phase == SessionPhase.Playing);
                var hud = Object.FindAnyObjectByType<GameHUDController>();
                foreach (var station in game.ship.GetComponentsInChildren<StationAnchor>())
                    if (station.kind == StationKind.Workbench)
                        game.captain.RestorePose(true, station.InteractionPosition, Vector3.zero, Vector2.right);
                hostLink.Command(room.guestId, CrewRole.Captain);
                yield return Until(() => hud.captainPanel.IsOpen);
                var workbench = hud.captainPanel.CaptureOnlineStation();
                Assert.That(workbench.kind, Is.EqualTo(StationKind.Workbench));
                hostLink.Action(room.guestId, workbench.revision + "|a0");
                Assert.That(game.jobs.Count, Is.EqualTo(1), "The authenticated guest starts the job on the host.");
                game.AddResource(ResourceKind.FiberBundle, 4);
                hostLink.Drop(room.guestId);
                yield return Until(() => OnlineVoyage.WaitingForCrew && game.phase == SessionPhase.Paused);
                Assert.That(save.TrySave(game, SaveReason.Manual, out string saveError), Is.True, saveError);
                Assert.That(OnlineSaveSlot.TryRead(room.hostId, out var stored, out _), Is.True);
                Assert.That(stored.state.bank.Find(item => item.resource == ResourceKind.FiberBundle).amount, Is.EqualTo(4));
                hostLink.baseline = null;
                hostLink.Admit(room.guestId);
                yield return Until(() => hostLink.baseline != null && game.phase == SessionPhase.Playing);
                ArraySegment<byte> state = hostLink.baseline.Value;
                OnlineVoyage.Current.ExitToMenu(saveHost: false);
                yield return Until(() => SceneManager.GetActiveScene().name == "MainMenu");

                var guestLink = new TestLink();
                OnlineGameplayTransport.Factory = () => guestLink;
                room.Join("A1BC23");
                yield return Until(() => SceneManager.GetActiveScene().name == "Gameplay" &&
                    Object.FindAnyObjectByType<GameSaveManager>() != null);
                guestLink.Admit(room.hostId);
                guestLink.Deliver(state);
                yield return Until(() => OnlineVoyage.Current.GuestReady);
                game = GameSession.Current;
                Assert.That(game.enabled, Is.False, "Only the host advances the simulation.");
                Assert.That(game.GetResource(ResourceKind.FiberBundle), Is.EqualTo(4));
                Assert.That(game.jobs.Count, Is.EqualTo(1));
                Assert.That(game.captain.input.UseOnlineBindings, Is.True);
                rig = Object.FindAnyObjectByType<GameCameraRig>();
                Assert.That(rig.captainCamera.enabled && !rig.diverCamera.enabled, Is.True);
                var guestSave = Object.FindAnyObjectByType<GameSaveManager>();
                Assert.That(guestSave.HasValidSave(out _), Is.False);
                Assert.That(Object.FindAnyObjectByType<GameHUDController>().saveButton.gameObject.activeSelf, Is.False);
            }
            finally
            {
                if (OnlineVoyage.InProgress) OnlineVoyage.Current.ExitToMenu(saveHost: false);
                Object.Destroy(root);
                OnlineRoomService.Factory = roomFactory;
                OnlineGameplayTransport.Factory = transportFactory;
                foreach (string suffix in new[] { "", ".bak", ".tmp" })
                    if (File.Exists(hostSave + suffix)) File.Delete(hostSave + suffix);
                string slotDirectory = Path.GetDirectoryName(hostSave);
                if (Directory.Exists(slotDirectory)) Directory.Delete(slotDirectory);
            }
        }

        static IEnumerator Until(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 12;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "Timed out waiting for the online scene or authoritative frame.");
        }

        sealed class TestRoom : OnlineRoomService
        {
            public string hostId;
            public string guestId = "10000001";
            bool occupied, host, started, assigned;
            public override bool Available => true;
            public override bool Busy => false;
            public override bool InRoom => occupied;
            public override bool IsHost => host;
            public override bool Started => started;
            public override bool ResumeSavedGame => false;
            public override string RoomCode => "A1BC23";
            public override string StatusKey => "";
            public override string LocalSteamId => host ? hostId : guestId;
            public override string HostSteamId => hostId;
            public override string MatchId => "test-voyage";
            public override IReadOnlyList<OnlineRoomMember> Members => new[]
            {
                new OnlineRoomMember(hostId, "Test host", assigned ? CrewRole.Diver : (CrewRole?)null, true),
                new OnlineRoomMember(guestId, "Test guest", assigned ? CrewRole.Captain : (CrewRole?)null, false)
            };
            public override OnlineRoomMember ReservedGuest => Members[1];
            public override void RefreshAvailability() { }
            public override void Create(bool resumeSavedGame) { occupied = host = true; started = assigned = false; }
            public override void Join(string roomCode) { occupied = assigned = started = true; host = false; }
            public override void SelectRole(CrewRole role) { }
            public void AssignRoles() => assigned = true;
            public override void StartGame() { if (CanStart) started = true; }
            public override void Leave() { occupied = started = false; }
            public override bool TryGetAssignedRole(string steamId, out CrewRole role)
            {
                role = steamId == hostId ? CrewRole.Diver : CrewRole.Captain;
                return occupied && assigned && (steamId == hostId || steamId == guestId);
            }
        }

        sealed class TestLink : IOnlineGameplayTransport
        {
            public ArraySegment<byte>? baseline;
            public NetworkConnectionState State { get; private set; }
            public bool IsHost => State == NetworkConnectionState.Hosting;
            public bool CanQueueReliableState => true;
            public NetworkPeerIdentity LocalPeer { get; private set; }
            public event Action<string> PeerLeft;
            public event Action<string> AdmissionReady;
            public event Action<ArraySegment<byte>> WorldStateReceived;
            public event Action<string> LinkFailed { add { } remove { } }
            public event Action<string, string> ActionReceived;
            public event Action<NetworkPeerIdentity> PeerJoined { add { } remove { } }
            public event Action<NetworkCrewCommand> CommandReceived;
            public event Action<SessionNetworkSnapshot> SnapshotReceived { add { } remove { } }
            public void Configure(NetworkPeerIdentity local, Func<string, NetworkPeerIdentity> admitPeer) => LocalPeer = local;
            public void Host() => State = NetworkConnectionState.Hosting;
            public void Join(string steamHostId) => State = NetworkConnectionState.Connecting;
            public void Leave() => State = NetworkConnectionState.Offline;
            public void Dispose() => Leave();
            public void Admit(string id)
            {
                if (!IsHost) State = NetworkConnectionState.Connected;
                AdmissionReady?.Invoke(id);
            }
            public void Drop(string id) => PeerLeft?.Invoke(id);
            public void Command(string id, CrewRole role) => CommandReceived?.Invoke(new NetworkCrewCommand
            {
                version = NetworkCrewCommand.ProtocolVersion, sequence = 1, senderPeerId = id,
                command = new CrewCommand { role = role, interact = true }
            });
            public void Action(string id, string command) => ActionReceived?.Invoke(id, command);
            public void Deliver(ArraySegment<byte> data) => WorldStateReceived?.Invoke(data);
            public void SendAction(string action) { }
            public void SendCommand(NetworkCrewCommand command) { }
            public void BroadcastSnapshot(SessionNetworkSnapshot snapshot) { }
            public void BroadcastWorldState(ArraySegment<byte> data, bool replaceable = false)
            {
                if (!replaceable && OnlineFrameCodec.Decode(data).baseline) baseline = data;
            }
        }
    }
}
