using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lifeline
{
    /// <summary>Lifetime of one Steam voyage, including menu-to-game loading and guest rejoining.</summary>
    [DefaultExecutionOrder(-140)]
    public sealed class OnlineVoyage : MonoBehaviour
    {
        const float StartupTimeout = 45f;
        const float InputInterval = 1f / 60f;
        const float MotionInterval = 1f / 60f;

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
        public double? RoundTripTimeMilliseconds => remoteConnected ? link?.RoundTripTimeMilliseconds : null;

        OnlineRoomService room;
        IOnlineGameplayTransport link;
        NetworkPeerIdentity local, remote;
        string matchId;
        bool isHost, remoteConnected, ready, waiting, ending;
        bool needBaseline;
        float sceneDeadline, nextSend, nextWorld, nextInput, nextMotion;
        uint sequence, motionSequence;
        int lastWorldSignature;
        GameSaveManager save;
        GameSession game;
        GameHUDController hud;
        SessionAuthority authority;
        OnlineWorldFrame queuedBaseline;
        string deferredFailure;
        AsyncOperation sceneLoad;
        string pendingRoom;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            var root = new GameObject("Online voyage coordinator");
            DontDestroyOnLoad(root);
            Current = root.AddComponent<OnlineVoyage>();
            root.AddComponent<OnlineLoadingView>().voyage = Current;
        }

        public bool TryGetLoadingProgress(out float progress, out string stage)
        {
            progress = 0f;
            stage = "";
            if (ending || ready || room == null || !room.Started) return false;
            if (sceneLoad == null)
            {
                bool transportReady = room.HostTransportReady;
                progress = transportReady ? .30f : .12f;
                stage = transportReady ? "online.loading.connect" : "online.loading.prepare";
            }
            else if (!sceneLoad.isDone)
            {
                progress = Mathf.Lerp(.45f, .75f, Mathf.Clamp01(sceneLoad.progress / .9f));
                stage = "online.loading.scene";
            }
            else
            {
                progress = queuedBaseline != null ? .97f : Mathf.Lerp(.75f, .95f, link?.StateDownloadProgress ?? 0f);
                stage = isHost ? "online.loading.scene" : "online.loading.state";
            }
            return true;
        }

        void Update()
        {
            if (ending) return;
            if (link == null)
            {
                room = OnlineRoomService.Existing;
                if (room == null || !room.Started) { pendingRoom = null; return; }
                if (room.IsHost || room.HostTransportReady) Begin(room);
                else WaitForHostTransport();
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
                Debug.LogWarning(game == null ? "LifeLine online: timed out waiting for gameplay admission." :
                    "LifeLine online: timed out waiting for the initial gameplay state.");
                ExitToMenu("online.connection.timeout", false);
                return;
            }
            if (sceneLoad == null && remoteConnected)
            {
                LoadGameplay();
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

        void WaitForHostTransport()
        {
            if (pendingRoom != room.MatchId)
            {
                pendingRoom = room.MatchId;
                sceneDeadline = Time.unscaledTime + StartupTimeout;
            }
            if (Time.unscaledTime < sceneDeadline) return;
            Debug.LogWarning("LifeLine online: the host did not publish a ready gameplay transport.");
            ExitToMenu("online.connection.timeout", false);
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
            sceneDeadline = Time.unscaledTime + StartupTimeout;
            sequence = motionSequence = 0;
            nextMotion = nextInput = 0;
            link.Configure(local, Admit);
            link.AdmissionReady += OnAdmitted;
            link.PeerLeft += OnPeerLeft;
            link.LinkFailed += OnLinkFailed;
            link.CommandReceived += OnCommand;
            link.WorldStateReceived += OnState;
            link.ActionReceived += OnAction;
            link.MotionReceived += OnMotion;
            link.AttackReceived += OnAttack;
            if (!isHost)
            {
                link.Join(activeRoom.HostSteamId);
                return;
            }
            link.Host();
            if (link.IsHost && !activeRoom.PublishHostTransportReady())
                OnLinkFailed("LifeLine online: could not publish gameplay transport readiness to Steam.");
        }

        void LoadGameplay()
        {
            var menu = FindAnyObjectByType<MainMenuController>();
            sceneDeadline = Time.unscaledTime + StartupTimeout;
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
            Debug.Log("LifeLine online: Steam teammate admitted to gameplay.");
            if (!isHost) return;
            authority?.BindRemoteInput();
            needBaseline = true;
        }

        void OnPeerLeft(string peerId)
        {
            if (!isHost || remote.steamId != peerId) return;
            remoteConnected = false;
            waiting = true;
            authority?.ReleaseRemoteInput();
            if (ready && game != null) game.InterruptOperation(Actor(remote.role));
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
            game.captain.PresentationEvent += ForwardAttack;
            game.diver.PresentationEvent += ForwardAttack;
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
            hud.crewOverlay?.Configure(true);
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
            // Steam lobby departure can be visible before the legacy P2P disconnect arrives.
            if (remoteConnected && room.Members.Count < OnlineRoomService.Capacity &&
                !room.TryGetAssignedRole(remote.steamId, out _))
                link.DisconnectGuest();
            waiting = !remoteConnected;
            if (waiting) return;
            if (needBaseline)
            {
                if (!link.CanQueueReliableState) return;
                needBaseline = false;
                SendFrame(true, true);
                lastWorldSignature = game.OnlineWorldSignature();
                nextWorld = now + .5f;
                nextSend = now + .08f;
            }
            if (UpdateDue(now, ref nextMotion, MotionInterval))
            {
                var motion = game.CaptureOnlineMotion(++motionSequence);
                if (Panel(remote.role) is CrewStationPanel station) motion.station = station.CaptureOnlineStationMotion();
                link.BroadcastMotion(motion);
            }
            if (now < nextSend) return;
            nextSend = now + .08f;
            bool changedWorld = false;
            if (now >= nextWorld && link.CanQueueReliableState)
            {
                nextWorld = now + .65f;
                int signature = game.OnlineWorldSignature();
                changedWorld = signature != lastWorldSignature;
                lastWorldSignature = signature;
            }
            SendFrame(false, changedWorld);
        }

        void SendFrame(bool baseline, bool includeWorld)
        {
            try
            {
                var frame = game.CaptureOnlineFrame(hud, baseline, includeWorld);
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
                    waiting = frame.waitingForCrew;
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
            game.StatusAudienceRole = frame.statusAudienceRole;
            waiting = frame.waitingForCrew;
            ConfigureViews();
            Panel(local.role)?.ApplyOnlineStation(frame.station);
            ready = true;
        }

        void ForwardAttack(CrewActor actor, CrewVisualEvent kind, float strength)
        {
            if (remoteConnected && (kind == CrewVisualEvent.Melee || kind == CrewVisualEvent.Fire))
                link?.BroadcastAttack(actor.role, kind, kind == CrewVisualEvent.Melee ? actor.MeleeFacing : actor.SpeargunFacing);
        }

        void OnAttack(CrewRole role, CrewVisualEvent kind, Vector2 direction)
        {
            if (!isHost && ready && queuedBaseline == null) Actor(role)?.EmitPresentationEvent(kind, direction: direction);
        }

        void OnMotion(OnlineCrewMotion motion)
        {
            if (!isHost && ready && queuedBaseline == null && game.BufferOnlineMotion(motion))
                Panel(local.role)?.ApplyOnlineStationMotion(motion.station);
        }

        void LateUpdate()
        {
            if (!isHost && ready && game != null) game.RenderOnlineMotion(Time.unscaledDeltaTime);
        }

        void UpdateGuest(float now)
        {
            if (!UpdateDue(now, ref nextInput, InputInterval)) return;
            var input = Actor(local.role).input;
            var command = input != null ? input.ReadCommand() : default;
            if (command.pause) hud.HandleEscape();
            if (command.backpack && !LocalMenuOpen && !waiting) hud.crewOverlay?.ToggleBackpack();
            if (LocalMenuOpen || waiting) command = default;
            command.pause = false;
            command.role = local.role;
            if (!LocalMenuOpen && !waiting && Panel(local.role)?.OnlinePointerHeld == true)
                command.interactHeld = true;
            if (!LocalMenuOpen && !waiting) Panel(local.role)?.StampOnlineInput(ref command);
            link.SendCommand(new NetworkCrewCommand
            {
                version = NetworkCrewCommand.ProtocolVersion,
                sequence = ++sequence,
                senderPeerId = local.steamId,
                command = command
            });
        }

        static bool UpdateDue(float now, ref float next, float interval)
        {
            // Preserve the cadence across uneven frames without sending a burst after a stall.
            if (now + .0001f < next) return false;
            next = Mathf.Max(next + interval, now);
            return true;
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
            if (isHost && game != null)
            {
                if (game.captain != null) game.captain.PresentationEvent -= ForwardAttack;
                if (game.diver != null) game.diver.PresentationEvent -= ForwardAttack;
            }
            room = null;
            save = null; game = null; hud = null; authority = null;
            ready = waiting = remoteConnected = needBaseline = false;
            sceneLoad = null;
            pendingRoom = null;
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
