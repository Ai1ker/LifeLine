using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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
            if (SceneManager.GetActiveScene().name != "MainMenu")
                yield return SceneManager.LoadSceneAsync("MainMenu");
            var roomFactory = OnlineRoomService.Factory;
            var transportFactory = OnlineGameplayTransport.Factory;
            Assert.That(OnlineVoyage.Current != null, Is.True);
            Assert.That(OnlineRoomService.Existing == null, Is.True, "A room from an earlier Unity play session must not still be live.");
            var root = new GameObject("In-process online fixture");
            Object.DontDestroyOnLoad(root);
            var room = root.AddComponent<TestRoom>();
            room.hostId = ((ulong)DateTime.UtcNow.Ticks).ToString();
            string hostSave = OnlineSaveSlot.PathFor(room.hostId);
            var hostLink = new TestLink();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("LifeLineOnlinePlayTestKeyboard");
            var backgroundBehavior = InputSystem.settings.backgroundBehavior;
#if UNITY_EDITOR
            var editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
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
                yield return Until(() => room.HostTransportReady);
                Assert.That(hostLink.IsHost, Is.True);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
                Assert.That(OnlineVoyage.Current.TryGetLoadingProgress(out var loading, out var stage), Is.True);
                Assert.That(loading, Is.GreaterThan(0f));
                Assert.That(stage, Is.EqualTo("online.loading.connect"));
                hostLink.Admit(room.guestId);
                yield return Until(() => SceneManager.GetActiveScene().name == "Gameplay" &&
                    GameSession.Current != null && GameSession.Current.IsInitialized);
                var game = GameSession.Current;
                var save = Object.FindAnyObjectByType<GameSaveManager>();
                Assert.That(save.SavePath, Is.EqualTo(hostSave));
                Assert.That(game.diver.input.UseOnlineBindings, Is.True);
                var move = game.diver.input.RuntimeActions.FindAction("Captain/Move", true);
                Assert.That(move.enabled, Is.True);
                Assert.That(move.bindings[1].path, Is.EqualTo("<Keyboard>/w"));
                move.RemoveAllBindingOverrides();
                var rig = Object.FindAnyObjectByType<GameCameraRig>();
                Assert.That(rig.IsSplitScreen, Is.False);
                Assert.That(rig.diverCamera.enabled && !rig.captainCamera.enabled, Is.True);
                yield return Until(() => hostLink.baseline != null && game.phase == SessionPhase.Playing);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                float hostStartX = game.diver.Position.x;
                Press(keyboard, Key.D);
                yield return Until(() => game.diver.Position.x > hostStartX + .05f);
                Press(keyboard);

                var hud = Object.FindAnyObjectByType<GameHUDController>();
                hud.SetPause(true);
                Assert.That(game.phase, Is.EqualTo(SessionPhase.Playing));
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                float otherX = game.captain.Position.x;
                hostLink.Command(room.guestId, CrewRole.Captain, move: Vector2.right);
                yield return Until(() => game.captain.Position.x > otherX + .05f);
                hud.SetPause(false);
                Assert.That(hud.diverText.transform.parent.gameObject.activeSelf, Is.False);
                game.diver.input.RuntimeActions.FindAction("Captain/Backpack", true).RemoveAllBindingOverrides();
                Press(keyboard, Key.B);
                yield return Until(() => hud.crewOverlay.BackpackOpen);
                Press(keyboard);
                Assert.That(game.MeleeAttack(game.captain), Is.True);
                Assert.That(hostLink.lastAttack, Is.EqualTo((CrewRole.Captain, CrewVisualEvent.Melee)));
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
                yield return null;
                Assert.That(guestLink.State, Is.EqualTo(NetworkConnectionState.Offline));
                room.MarkHostTransportReady();
                yield return Until(() => guestLink.State == NetworkConnectionState.Connecting);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
                guestLink.Admit(room.hostId);
                guestLink.Deliver(state);
                yield return Until(() => SceneManager.GetActiveScene().name == "Gameplay" &&
                    Object.FindAnyObjectByType<GameSaveManager>() != null);
                yield return Until(() => OnlineVoyage.Current.GuestReady);
                game = GameSession.Current;
                Assert.That(game.enabled, Is.False, "Only the host advances the simulation.");
                Assert.That(game.GetResource(ResourceKind.FiberBundle), Is.EqualTo(4));
                Assert.That(game.jobs.Count, Is.EqualTo(1));
                Assert.That(game.captain.input.UseOnlineBindings, Is.True);
                Assert.That(OnlineVoyage.Current.TryGetLoadingProgress(out _, out _), Is.False);
                var sample = game.CaptureOnlineMotion(2);
                float initialX = game.captain.Position.x;
                sample.captain.position.x += 1f;
                guestLink.DeliverMotion(sample);
                game.RenderOnlineMotion(.02f);
                Assert.That(game.captain.Position.x, Is.InRange(initialX + .1f, initialX + .9f));
                var stale = sample;
                stale.sequence = 1;
                stale.captain.position.x += 5f;
                guestLink.DeliverMotion(stale);
                game.RenderOnlineMotion(.03f);
                Assert.That(game.captain.Position.x, Is.EqualTo(sample.captain.position.x).Within(.001f));
                guestLink.DeliverAttack(CrewRole.Captain, CrewVisualEvent.Melee, Vector2.up);
                Assert.That(game.captain.GetComponent<CrewPresentation>().MeleeAnimationPlaying, Is.True);
                Assert.That(game.captain.MeleeFacing, Is.EqualTo(Vector2.up));
                var guestMove = game.captain.input.RuntimeActions.FindAction("Captain/Move", true);
                guestMove.RemoveAllBindingOverrides();
                Press(keyboard, Key.D);
                yield return Until(() => guestLink.lastCommand.move == Vector2.right);
                Assert.That(guestLink.lastCommand.role, Is.EqualTo(CrewRole.Captain));
                Press(keyboard);
                yield return Until(() => guestLink.lastCommand.move == Vector2.zero);
                guestMove.ApplyBindingOverride(4, "<Keyboard>/l");
                Assert.That(game.captain.input.GetBindingLabel("Move", 4), Is.EqualTo("L").IgnoreCase);
                Press(keyboard, Key.L);
                yield return Until(() => guestLink.lastCommand.move == Vector2.right);
                Press(keyboard);
                rig = Object.FindAnyObjectByType<GameCameraRig>();
                Assert.That(rig.captainCamera.enabled && !rig.diverCamera.enabled, Is.True);
                var guestSave = Object.FindAnyObjectByType<GameSaveManager>();
                Assert.That(guestSave.HasValidSave(out _), Is.False);
                Assert.That(Object.FindAnyObjectByType<GameHUDController>().saveButton.gameObject.activeSelf, Is.False);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = backgroundBehavior;
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = editorBehavior;
#endif
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

        static void Press(Keyboard keyboard, params Key[] keys) =>
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

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
            bool occupied, host, started, assigned, hostTransportReady;
            public override bool Available => true;
            public override bool Busy => false;
            public override bool InRoom => occupied;
            public override bool IsHost => host;
            public override bool Started => started;
            public override bool HostTransportReady => hostTransportReady;
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
            public override bool PublishHostTransportReady()
            {
                if (!host || !started) return false;
                MarkHostTransportReady();
                return true;
            }
            public void MarkHostTransportReady() => hostTransportReady = true;
            public override void Leave() { occupied = started = hostTransportReady = false; }
            public override bool TryGetAssignedRole(string steamId, out CrewRole role)
            {
                role = steamId == hostId ? CrewRole.Diver : CrewRole.Captain;
                return occupied && assigned && (steamId == hostId || steamId == guestId);
            }
        }

        sealed class TestLink : IOnlineGameplayTransport
        {
            public ArraySegment<byte>? baseline;
            public CrewCommand lastCommand;
            public (CrewRole, CrewVisualEvent) lastAttack;
            public NetworkConnectionState State { get; private set; }
            public bool IsHost => State == NetworkConnectionState.Hosting;
            public bool CanQueueReliableState => true;
            public float StateDownloadProgress => 0;
            public NetworkPeerIdentity LocalPeer { get; private set; }
            public event Action<string> PeerLeft;
            public event Action<string> AdmissionReady;
            public event Action<ArraySegment<byte>> WorldStateReceived;
            public event Action<string> LinkFailed { add { } remove { } }
            public event Action<string, string> ActionReceived;
            public event Action<OnlineCrewMotion> MotionReceived;
            public event Action<CrewRole, CrewVisualEvent, Vector2> AttackReceived;
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
            uint inputSequence;
            public void Command(string id, CrewRole role, Vector2? move = null) => CommandReceived?.Invoke(new NetworkCrewCommand
            {
                version = NetworkCrewCommand.ProtocolVersion, sequence = ++inputSequence, senderPeerId = id,
                command = new CrewCommand { role = role, interact = !move.HasValue, move = move ?? Vector2.zero }
            });
            public void Action(string id, string command) => ActionReceived?.Invoke(id, command);
            public void Deliver(ArraySegment<byte> data) => WorldStateReceived?.Invoke(data);
            public void DeliverMotion(OnlineCrewMotion motion) => MotionReceived?.Invoke(motion);
            public void DeliverAttack(CrewRole role, CrewVisualEvent kind, Vector2 direction) => AttackReceived?.Invoke(role, kind, direction);
            public void BroadcastMotion(OnlineCrewMotion motion) { }
            public void BroadcastAttack(CrewRole role, CrewVisualEvent kind, Vector2 direction) => lastAttack = (role, kind);
            public void SendAction(string action) { }
            public void SendCommand(NetworkCrewCommand command) => lastCommand = command.command;
            public void BroadcastSnapshot(SessionNetworkSnapshot snapshot) { }
            public void BroadcastWorldState(ArraySegment<byte> data, bool replaceable = false)
            {
                if (!replaceable && OnlineFrameCodec.Decode(data).baseline) baseline = data;
            }
        }
    }
}
