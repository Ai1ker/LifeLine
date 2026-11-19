using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lifeline
{
    /// <summary>Lifetime of one Steam voyage, including menu-to-game loading and guest rejoining.</summary>
    public sealed class OnlineVoyage : MonoBehaviour
    {
        public static OnlineVoyage Current { get; private set; }
        static string menuNotice;
        public static string ConsumeMenuNotice()
        {
            string notice = menuNotice;
            menuNotice = null;
            return notice ?? string.Empty;
        }

        public static bool InProgress => Current != null && Current.link != null;
        public static bool IsGuest => InProgress && !Current.isHost;
        public static bool WaitingForCrew => InProgress && Current.waiting;
        public CrewRole LocalRole => local.role;
        public bool LocalMenuOpen { get; set; }
        public bool GuestReady => ready;
        public string RoomCode => room?.RoomCode;
        public bool CanResumePlay => !isHost || !waiting;

        OnlineRoomService room;
        IOnlineGameplayTransport link;
        NetworkPeerIdentity local, remote;
        string matchId;
        bool isHost, remoteConnected, ready, waiting, pausedForPeer, ending;
        bool needBaseline;
        float sceneDeadline, nextSend, nextWorld, nextInput;
        uint sequence;
        GameSaveManager save;
        GameSession game;
        GameHUDController hud;
        SessionAuthority authority;
        OnlineWorldFrame queuedBaseline;
        string deferredFailure;
        AsyncOperation sceneLoad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            var root = new GameObject("Online voyage coordinator");
            DontDestroyOnLoad(root);
            Current = root.AddComponent<OnlineVoyage>();
        }

        void Update()
        {
            if (ending) return;
            if (link == null)
            {
                room = OnlineRoomService.Existing;
                if (room != null && room.InRoom && room.Started) Begin(room);
                return;
            }
            if (sceneLoad != null && !sceneLoad.isDone) return;
            if (!string.IsNullOrEmpty(deferredFailure))
            {
                ExitToMenu(deferredFailure, true);
                return;
            }
            if (room == null || !room.InRoom || room.MatchId != matchId)
            {
                ExitToMenu("online.host.closed", isHost);
                return;
            }
            float now = Time.unscaledTime;
            if (!ready && now > sceneDeadline)
            {
                ExitToMenu("online.connection.timeout", false);
                return;
            }
            if (game == null || hud == null) return;
            if (isHost && ready) UpdateHost(now);
            else if (!isHost)
            {
                if (queuedBaseline != null)
                {
                    var baseline = queuedBaseline;
                    queuedBaseline = null;
                    ApplyBaseline(baseline);
                }
                if (ready) UpdateGuest(now);
            }
        }

        void Begin(OnlineRoomService activeRoom)
        {
            if (!activeRoom.TryGetAssignedRole(activeRoom.LocalSteamId, out CrewRole role)) return;
            OnlineRoomMember self = null;
            OnlineRoomMember peer = null;
            foreach (var member in activeRoom.Members)
                if (member.SteamId == activeRoom.LocalSteamId) self = member; else peer = member;
            if (activeRoom.IsHost) peer ??= activeRoom.ReservedGuest;
            if (self == null || peer == null || !peer.Role.HasValue) return;
            link = OnlineGameplayTransport.Create();
            if (link == null)
            {
                ExitToMenu("online.connection.unavailable", false);
                return;
            }
            isHost = activeRoom.IsHost;
            local = Identity(self, role);
            remote = Identity(peer, peer.Role.Value);
            matchId = activeRoom.MatchId;
            ready = remoteConnected = false;
            waiting = true;
            sceneDeadline = Time.unscaledTime + 45;
            sequence = 0;
            link.Configure(local, Admit);
            link.AdmissionReady += OnAdmitted;
            link.PeerLeft += OnPeerLeft;
            link.LinkFailed += OnLinkFailed;
            link.CommandReceived += OnCommand;
            link.WorldStateReceived += OnState;
            link.ActionReceived += OnAction;
            if (isHost) link.Host(); else link.Join(activeRoom.HostSteamId);
            if (!string.IsNullOrEmpty(deferredFailure)) return;
            var menu = FindAnyObjectByType<MainMenuController>();
            sceneLoad = SceneManager.LoadSceneAsync(menu != null ? menu.gameplaySceneName : "Gameplay");
        }

        NetworkPeerIdentity Admit(string steamId)
        {
            if (steamId != remote.steamId || room == null ||
                !room.TryGetAssignedRole(steamId, out CrewRole role) || role != remote.role) return null;
            return remote;
        }

        static NetworkPeerIdentity Identity(OnlineRoomMember member, CrewRole role) => new()
        {
            peerId = member.SteamId, steamId = member.SteamId, displayName = member.DisplayName, role = role
        };

        void OnAdmitted(string peerId)
        {
            if (remote.steamId != peerId) return;
            remoteConnected = true;
            if (!isHost) return;
            authority?.BindRemoteInput();
            needBaseline = true;
        }

        void OnPeerLeft(string peerId)
        {
            if (!isHost || remote.steamId != peerId) return;
            remoteConnected = false;
            authority?.ReleaseRemoteInput();
            if (hud != null) Panel(remote.role)?.Close();
        }

        void OnLinkFailed(string details)
        {
            Debug.LogWarning(details);
            deferredFailure = isHost ? "online.connection.failed" :
                room == null || !room.InRoom ? "online.host.closed" : "online.connection.lost";
        }

        public bool Boot(GameSaveManager manager)
        {
            save = manager;
            game = manager.session;
            hud = manager.hud;
            if (!string.IsNullOrEmpty(deferredFailure) || room == null || !room.InRoom) return false;
            if (game == null || hud == null) { deferredFailure = "online.connection.incompatible"; return false; }
            authority = hud.authority != null ? hud.authority : FindAnyObjectByType<SessionAuthority>();
            if (authority == null) authority = game.gameObject.AddComponent<SessionAuthority>();
            authority.session = game;
            if (authority.cameraRig == null) authority.cameraRig = FindAnyObjectByType<GameCameraRig>();
            hud.authority = authority;
            // Initialize before load so the repair panels and modal state have valid references.
            hud.Initialize();
            if (!isHost)
            {
                game.enabled = false;
                game.ship.enabled = false;
                foreach (var actor in game.Crew) { actor.enabled = false; actor.input?.SetEnabled(false); }
                ConfigureViews();
                return true;
            }
            manager.pathOverride = OnlineSaveSlot.PathFor(local.steamId);
            if (string.IsNullOrEmpty(manager.pathOverride)) { deferredFailure = "online.save.failed"; return false; }
            if (room.ResumeSavedGame)
            {
                if (!manager.TryLoad(game, out string error))
                {
                    Debug.LogWarning(error);
                    deferredFailure = "online.save.failed";
                    return false;
                }
            }
            else game.Initialize();
            ready = true;
            ConfigureViews();
            authority.BindRemoteInput();
            if (!remoteConnected) authority.ReleaseRemoteInput();
            needBaseline = true;
            return true;
        }

        void ConfigureViews()
        {
            authority.localPeer = local;
            authority.remotePeer = remote;
            authority.SetOnlineView(true, local.role);
            var own = Actor(local.role);
            var other = Actor(remote.role);
            own.input?.Initialize(local.role, own.input.actions, true);
            own.input?.SetEnabled(true);
            other.input?.SetEnabled(false);
            if (hud.controls != null) hud.controls.onlineInput = own.input;
            if (hud.settings != null) hud.settings.cameraRig = authority.cameraRig;
            Panel(remote.role)?.SetRemotePresentationHidden();
            if (hud.captainText != null) hud.captainText.supportRichText = false;
            if (hud.diverText != null) hud.diverText.supportRichText = false;
            if (hud.saveButton != null) hud.saveButton.gameObject.SetActive(isHost);
            if (hud.restartButton != null) hud.restartButton.gameObject.SetActive(false);
            if (!isHost && hud.endingRestartButton != null) hud.endingRestartButton.gameObject.SetActive(false);
        }

        void UpdateHost(float now)
        {
            waiting = !remoteConnected;
            if (waiting)
            {
                if (game.phase == SessionPhase.Playing)
                {
                    pausedForPeer = true;
                    game.Pause(true);
                    game.Tell(LocalizationManager.Current != null
                        ? LocalizationManager.Current.Format("online.waiting.peer", RoomCode) : "Waiting for Steam teammate · " + RoomCode);
                }
                return;
            }
            if (pausedForPeer)
            {
                pausedForPeer = false;
                if (!LocalMenuOpen && game.phase == SessionPhase.Paused) game.Pause(false);
            }
            if (needBaseline)
            {
                if (!link.CanQueueReliableState) return;
                needBaseline = false;
                SendFrame(true, true);
                nextWorld = now + .5f;
                nextSend = now + .08f;
            }
            if (now < nextSend) return;
            nextSend = now + .08f;
            bool world = now >= nextWorld && link.CanQueueReliableState;
            if (world) nextWorld = now + .65f;
            SendFrame(false, world);
        }

        void SendFrame(bool baseline, bool includeWorld)
        {
            try
            {
                var frame = game.CaptureOnlineFrame(hud, baseline, includeWorld);
                frame.hostPaused = LocalMenuOpen;
                frame.waitingForCrew = waiting;
                frame.station = Panel(remote.role)?.CaptureOnlineStation();
                link.BroadcastWorldState(OnlineFrameCodec.Encode(frame), !baseline && !includeWorld);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                deferredFailure = "online.connection.failed";
            }
        }

        void OnState(ArraySegment<byte> bytes)
        {
            if (isHost || ending) return;
            try
            {
                var frame = OnlineFrameCodec.Decode(bytes);
                if (frame?.state == null) throw new InvalidOperationException("Empty online state.");
                if (frame.baseline) queuedBaseline = frame;
                else if (ready && queuedBaseline == null)
                {
                    game.ApplyOnlineFrame(frame, hud);
                    waiting = frame.waitingForCrew || frame.hostPaused;
                    Panel(local.role)?.ApplyOnlineStation(frame.station);
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning(error.Message);
                deferredFailure = "online.connection.incompatible";
            }
        }

        void ApplyBaseline(OnlineWorldFrame frame)
        {
            if (!game.TryRestoreSaveData(frame.state, out string error, hud))
            {
                Debug.LogWarning(error);
                deferredFailure = "online.connection.incompatible";
                return;
            }
            game.ResetOnlineReplicaCombat();
            game.FreezeOnlineReplica();
            game.phase = frame.phase;
            waiting = frame.waitingForCrew || frame.hostPaused;
            ConfigureViews();
            Panel(local.role)?.ApplyOnlineStation(frame.station);
            ready = true;
        }

        void UpdateGuest(float now)
        {
            if (now < nextInput) return;
            nextInput = now + .02f;
            var input = Actor(local.role).input;
            var command = input != null ? input.ReadCommand() : default;
            if (command.pause) hud.HandleEscape();
            if (LocalMenuOpen || waiting) command = default;
            command.pause = false;
            command.role = local.role;
            if (!LocalMenuOpen && !waiting && Panel(local.role)?.OnlinePointerHeld == true)
                command.interactHeld = true;
            link.SendCommand(new NetworkCrewCommand
            {
                version = NetworkCrewCommand.ProtocolVersion,
                sequence = ++sequence,
                senderPeerId = local.steamId,
                command = command
            });
        }

        void OnCommand(NetworkCrewCommand packet)
        {
            if (isHost && ready && remoteConnected) authority.ApplyAuthenticatedRemoteCommand(remote.steamId, packet);
        }

        public void SendStationAction(string action)
        {
            if (IsGuest && ready && !LocalMenuOpen) link.SendAction(action);
        }

        void OnAction(string steamId, string action)
        {
            if (!isHost || !ready || !remoteConnected || remote.steamId != steamId || game.phase != SessionPhase.Playing) return;
            var actor = Actor(remote.role);
            var panel = Panel(remote.role);
            if (panel == null || !panel.IsOpen || game.IsIncapacitated(actor) || !actor.onboard) return;
            if (action == "close") panel.Close();
            else panel.InvokeOnlineAction(action);
        }

        public bool ReloadHostSave()
        {
            if (!InProgress || !isHost || save == null || !save.HasValidSave(out _)) return false;
            if (!save.TryLoad(game, out _)) { ExitToMenu("online.save.failed", false); return false; }
            LocalMenuOpen = false;
            ConfigureViews();
            authority.BindRemoteInput();
            if (!remoteConnected) authority.ReleaseRemoteInput();
            needBaseline = true;
            return true;
        }

        public void ExitToMenu(string notice = null, bool saveHost = true)
        {
            if (ending) return;
            ending = true;
            if (saveHost) SaveBeforeExit();
            if (!string.IsNullOrEmpty(notice)) menuNotice = notice;
            else if (!isHost && room != null) menuNotice = "online.room.left";
            link?.Dispose();
            link = null;
            room?.Leave();
            room = null;
            save = null; game = null; hud = null; authority = null;
            ready = waiting = remoteConnected = pausedForPeer = needBaseline = false;
            sceneLoad = null;
            LocalMenuOpen = false;
            deferredFailure = null;
            queuedBaseline = null;
            Time.timeScale = 1;
            if (SceneManager.GetActiveScene().name != "MainMenu") SceneManager.LoadScene("MainMenu");
            else if (!string.IsNullOrEmpty(menuNotice))
            {
                var menu = FindAnyObjectByType<MainMenuController>();
                if (menu != null) menu.OpenOnlineRoom(ConsumeMenuNotice());
            }
            ending = false;
        }

        void SaveBeforeExit()
        {
            if (!isHost || !ready || save == null || game == null || save.loadFailed || !game.IsInitialized) return;
            if (game.phase == SessionPhase.Playing) game.Pause(true);
            if (game.phase == SessionPhase.Paused) save.TrySave(game, SaveReason.Manual, out _);
        }

        void OnApplicationQuit()
        {
            if (link == null) return;
            SaveBeforeExit();
            link.Dispose();
            room?.Leave();
        }

        CrewActor Actor(CrewRole role) => role == CrewRole.Captain ? game.captain : game.diver;
        CrewStationPanel Panel(CrewRole role) => role == CrewRole.Captain ? hud.captainPanel : hud.diverPanel;
    }
}
