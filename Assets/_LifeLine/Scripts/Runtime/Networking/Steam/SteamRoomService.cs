#if LIFELINE_STEAM
using System;
using System.Collections.Generic;
using System.Globalization;
using Steamworks;
using UnityEngine;

namespace Lifeline
{
    /// <summary>Steam handles room discovery and membership. Only the original owner writes match state.</summary>
    [DefaultExecutionOrder(-1100)]
    public sealed class SteamRoomService : OnlineRoomService
    {
        public const uint TestAppId = 480;
        const string GameKey = "lifeline_game";
        const string GameValue = "lifeline-team-coop";
        const string ProtocolKey = "lifeline_protocol";
        const string CodeKey = "lifeline_code";
        const string OwnerKey = "lifeline_owner";
        const string StateKey = "lifeline_state";
        const string RequestKey = "lifeline_role_request";
        const int Unassigned = -1;
        const float RoomPollSeconds = .25f;
        const float RequestTimeoutSeconds = 20f;
        static readonly string Protocol = NetworkCrewCommand.ProtocolVersion.ToString(CultureInfo.InvariantCulture);

        [Serializable]
        sealed class RoomState
        {
            public bool started;
            public bool hostTransportReady;
            public bool resume;
            public bool closed;
            public int hostRole = Unassigned;
            public int guestRole = Unassigned;
            public string guestId = "";
            public string guestName = "";
            public string request = "";
            public bool requestAccepted;

            public RoomState Copy() => (RoomState)MemberwiseClone();
        }

        enum Operation { None, Create, Search, Join }

        bool initialized;
        bool available;
        bool abandoned;
        bool pendingResume;
        bool exiting;
        Operation operation;
        float operationDeadline;
        float nextPoll;
        float roleDeadline;
        CSteamID lobby;
        string localId = "";
        string ownerId = "";
        string pendingCode = "";
        string pendingRoleRequest = "";
        string code = "";
        string status = "";
        string stateJson = "";
        RoomState state;
        IReadOnlyList<OnlineRoomMember> members = Array.Empty<OnlineRoomMember>();
        readonly HashSet<ulong> requestedNames = new HashSet<ulong>();
        CallResult<LobbyCreated_t> created;
        CallResult<LobbyMatchList_t> found;
        CallResult<LobbyEnter_t> entered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterFactory()
        {
            Factory = () =>
            {
                var root = new GameObject("LifeLine Steam Rooms");
                DontDestroyOnLoad(root);
                return root.AddComponent<SteamRoomService>();
            };
        }

        public override bool Available => available;
        public override bool Busy => operation != Operation.None;
        public override bool InRoom => lobby.m_SteamID != 0 && state != null && !state.closed;
        public override bool IsHost => InRoom && localId == ownerId;
        public override bool Started => InRoom && state.started;
        public override bool HostTransportReady => Started && state.hostTransportReady;
        public override bool ResumeSavedGame => InRoom && state.resume;
        public override string RoomCode => code;
        public override string StatusKey => status;
        public override string LocalSteamId => localId;
        public override string HostSteamId => ownerId;
        public override string MatchId => InRoom ? lobby.m_SteamID.ToString(CultureInfo.InvariantCulture) : string.Empty;
        public override IReadOnlyList<OnlineRoomMember> Members => members;
        public override OnlineRoomMember ReservedGuest => Started && !string.IsNullOrEmpty(state.guestId)
            ? new OnlineRoomMember(state.guestId, state.guestName, DecodeRole(state.guestRole), false) : null;

        public override void RefreshAvailability()
        {
            if (exiting) return;
            bool wasAvailable = available;
            string previousStatus = status;
            if (!initialized)
            {
                try
                {
                    initialized = SteamAPI.Init();
                    if (initialized && SteamUtils.GetAppID().m_AppId != TestAppId)
                    {
                        SteamAPI.Shutdown();
                        initialized = false;
                        status = "online.steam.wrongApp";
                        if (status != previousStatus) NotifyChanged();
                        return;
                    }
                    if (initialized)
                    {
                        created = CallResult<LobbyCreated_t>.Create(OnCreated);
                        found = CallResult<LobbyMatchList_t>.Create(OnFound);
                        entered = CallResult<LobbyEnter_t>.Create(OnEntered);
                    }
                }
                catch (Exception exception) when (exception is DllNotFoundException || exception is EntryPointNotFoundException
                    || exception is BadImageFormatException || exception is InvalidOperationException)
                {
                    Debug.LogWarning("LifeLine Steam initialization: " + exception.Message);
                    initialized = false;
                }
            }

            available = initialized && SteamUser.BLoggedOn();
            if (available)
            {
                localId = SteamUser.GetSteamID().m_SteamID.ToString(CultureInfo.InvariantCulture);
                if (!wasAvailable) status = "";
            }
            else if (!InRoom) status = "online.steam.unavailable";
            if (wasAvailable != available || previousStatus != status) NotifyChanged();
        }

        void Update()
        {
            if (!initialized || exiting) return;
            SteamAPI.RunCallbacks();
            if (Busy && !abandoned && Time.unscaledTime >= operationDeadline)
            {
                // Ignoring a create/join callback would leak the just-entered lobby. Let the
                // request finish, then leave the returned lobby without exposing it to the UI.
                abandoned = true;
                SetStatus("online.room.requestTimedOut");
            }
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + RoomPollSeconds;
            bool loggedOn = SteamUser.BLoggedOn();
            if (available && !loggedOn)
            {
                available = false;
                if (Busy) abandoned = true;
                CloseRoom("online.room.connectionFailed");
                return;
            }
            if (!available && loggedOn) RefreshAvailability();
            if (InRoom) RefreshRoom();
        }

        public override void Create(bool resumeSavedGame)
        {
            if (!CanRequestRoom()) return;
            pendingResume = resumeSavedGame;
            pendingCode = GenerateRoomCode();
            BeginOperation(Operation.Create, "online.room.creating");
            SteamAPICall_t call = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, Capacity);
            if (call == SteamAPICall_t.Invalid) EndOperation("online.room.createFailed");
            else created.Set(call);
        }

        public override void Join(string roomCode)
        {
            if (!TryNormalizeRoomCode(roomCode, out string normalized))
            {
                SetStatus("online.room.codeInvalid");
                return;
            }
            if (!CanRequestRoom()) return;
            pendingCode = normalized;
            BeginOperation(Operation.Search, "online.room.searching");
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListStringFilter(GameKey, GameValue, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(ProtocolKey, Protocol, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(CodeKey, pendingCode, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(2);
            SteamAPICall_t call = SteamMatchmaking.RequestLobbyList();
            if (call == SteamAPICall_t.Invalid) EndOperation("online.room.joinFailed");
            else found.Set(call);
        }

        bool CanRequestRoom()
        {
            if (Busy || InRoom || exiting) return false;
            RefreshAvailability();
            return available;
        }

        void BeginOperation(Operation next, string message)
        {
            operation = next;
            abandoned = false;
            operationDeadline = Time.unscaledTime + RequestTimeoutSeconds;
            status = message;
            NotifyChanged();
        }

        void EndOperation(string message)
        {
            operation = Operation.None;
            abandoned = false;
            pendingCode = "";
            status = message;
            NotifyChanged();
        }

        void OnCreated(LobbyCreated_t result, bool ioFailure)
        {
            var createdLobby = new CSteamID(result.m_ulSteamIDLobby);
            if (abandoned || !available)
            {
                if (createdLobby.m_SteamID != 0) SteamMatchmaking.LeaveLobby(createdLobby);
                EndOperation(status);
                return;
            }
            if (ioFailure || result.m_eResult != EResult.k_EResultOK || createdLobby.m_SteamID == 0)
            {
                EndOperation("online.room.createFailed");
                return;
            }
            lobby = createdLobby;
            ownerId = localId;
            code = pendingCode;
            var initial = new RoomState { resume = pendingResume };
            bool published = SteamMatchmaking.SetLobbyData(lobby, GameKey, GameValue)
                && SteamMatchmaking.SetLobbyData(lobby, ProtocolKey, Protocol)
                && SteamMatchmaking.SetLobbyData(lobby, OwnerKey, ownerId)
                && WriteState(initial)
                && SteamMatchmaking.SetLobbyData(lobby, CodeKey, code);
            if (!published)
            {
                CloseRoom("online.room.createFailed");
                return;
            }
            UpdateMembers();
            EndOperation("");
        }

        void OnFound(LobbyMatchList_t result, bool ioFailure)
        {
            if (abandoned || !available) { EndOperation(status); return; }
            if (ioFailure) { EndOperation("online.room.joinFailed"); return; }
            if (result.m_nLobbiesMatching == 0) { EndOperation("online.room.noMatch"); return; }
            if (result.m_nLobbiesMatching > 1) { EndOperation("online.room.ambiguous"); return; }
            BeginOperation(Operation.Join, "online.room.joining");
            SteamAPICall_t call = SteamMatchmaking.JoinLobby(SteamMatchmaking.GetLobbyByIndex(0));
            if (call == SteamAPICall_t.Invalid) EndOperation("online.room.joinFailed");
            else entered.Set(call);
        }

        void OnEntered(LobbyEnter_t result, bool ioFailure)
        {
            var joined = new CSteamID(result.m_ulSteamIDLobby);
            var response = (EChatRoomEnterResponse)result.m_EChatRoomEnterResponse;
            bool success = !ioFailure && response == EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess;
            if (abandoned || !available)
            {
                if (success) SteamMatchmaking.LeaveLobby(joined);
                EndOperation(status);
                return;
            }
            if (!success)
            {
                EndOperation(response == EChatRoomEnterResponse.k_EChatRoomEnterResponseFull
                    ? "online.room.full" : "online.room.joinFailed");
                return;
            }

            lobby = joined;
            ownerId = SteamMatchmaking.GetLobbyData(lobby, OwnerKey);
            code = SteamMatchmaking.GetLobbyData(lobby, CodeKey);
            if (code != pendingCode || SteamMatchmaking.GetLobbyData(lobby, GameKey) != GameValue
                || SteamMatchmaking.GetLobbyData(lobby, ProtocolKey) != Protocol
                || ownerId == localId || !OwnerIsPresent() || !ReadState(out stateJson, out state) || state.closed)
            {
                CloseRoom("online.room.closed");
                return;
            }
            if (state.started && state.guestId != localId)
            {
                CloseRoom("online.room.full");
                return;
            }
            UpdateMembers();
            EndOperation("");
        }

        public override void SelectRole(CrewRole role)
        {
            if (!InRoom || Started || (role != CrewRole.Captain && role != CrewRole.Diver)) return;
            RefreshRoom();
            if (!InRoom || Started) return;
            foreach (OnlineRoomMember member in members)
                if (member.SteamId != localId && member.Role == role)
                {
                    SetStatus("online.room.roleTaken");
                    return;
                }

            if (IsHost)
            {
                RoomState next = state.Copy();
                next.hostRole = (int)role;
                if (!WriteState(next)) { SetStatus("online.room.updateFailed"); return; }
                UpdateMembers();
                status = "";
                NotifyChanged();
            }
            else
            {
                pendingRoleRequest = ((int)role).ToString(CultureInfo.InvariantCulture) + ":" + Guid.NewGuid().ToString("N");
                roleDeadline = Time.unscaledTime + RequestTimeoutSeconds;
                SteamMatchmaking.SetLobbyMemberData(lobby, RequestKey, pendingRoleRequest);
                SetStatus("online.room.roleRequested");
            }
        }

        public override void StartGame()
        {
            if (!IsHost || Started) return;
            RefreshRoom();
            if (!CanStart) { SetStatus("online.room.waitRoles"); return; }
            RoomState next = state.Copy();
            next.started = true;
            OnlineRoomMember guest = members[0].IsHost ? members[1] : members[0];
            next.guestId = guest.SteamId;
            next.guestName = guest.DisplayName;
            next.guestRole = (int)guest.Role.Value;
            if (!WriteState(next)) { SetStatus("online.room.updateFailed"); return; }
            status = "";
            NotifyChanged();
        }

        public override bool PublishHostTransportReady()
        {
            if (!IsHost || !Started) return false;
            RoomState next = state.Copy();
            next.hostTransportReady = true;
            if (!WriteState(next)) return false;
            NotifyChanged();
            return true;
        }

        public override bool TryGetAssignedRole(string steamId, out CrewRole role)
        {
            role = default;
            if (!Started || string.IsNullOrEmpty(steamId) || !OwnerIsPresent()) return false;
            int count = SteamMatchmaking.GetNumLobbyMembers(lobby);
            for (int index = 0; index < count; index++)
            {
                CSteamID id = SteamMatchmaking.GetLobbyMemberByIndex(lobby, index);
                if (id.m_SteamID.ToString(CultureInfo.InvariantCulture) != steamId) continue;
                CrewRole? assigned = GetRole(steamId);
                if (!assigned.HasValue) return false;
                role = assigned.Value;
                return true;
            }
            return false;
        }

        void RefreshRoom()
        {
            if (!OwnerIsPresent()) { CloseRoom("online.room.closed"); return; }
            bool metadataChanged = false;
            if (!IsHost)
            {
                if (!ReadState(out string json, out RoomState remote) || remote.closed)
                {
                    CloseRoom("online.room.closed");
                    return;
                }
                if (remote.started && remote.guestId != localId) { CloseRoom("online.room.full"); return; }
                if (stateJson != json)
                {
                    state = remote;
                    stateJson = json;
                    metadataChanged = true;
                }
                if (pendingRoleRequest.Length > 0)
                {
                    if (state.request == pendingRoleRequest && state.guestId == localId)
                    {
                        status = state.requestAccepted ? "" : "online.room.roleTaken";
                        pendingRoleRequest = "";
                        metadataChanged = true;
                    }
                    else if (Time.unscaledTime >= roleDeadline)
                    {
                        status = "online.room.requestTimedOut";
                        pendingRoleRequest = "";
                        metadataChanged = true;
                    }
                }
            }
            else if (!Started) metadataChanged = ApplyGuestRoleRequest();
            if (UpdateMembers() || metadataChanged) NotifyChanged();
        }

        bool ApplyGuestRoleRequest()
        {
            CSteamID guest = default;
            int count = SteamMatchmaking.GetNumLobbyMembers(lobby);
            for (int index = 0; index < count; index++)
            {
                CSteamID id = SteamMatchmaking.GetLobbyMemberByIndex(lobby, index);
                if (id.m_SteamID.ToString(CultureInfo.InvariantCulture) != ownerId) { guest = id; break; }
            }
            string guestId = guest.m_SteamID == 0 ? "" : guest.m_SteamID.ToString(CultureInfo.InvariantCulture);
            RoomState next = state.Copy();
            bool changed = guestId != state.guestId;
            if (changed)
            {
                next.guestId = guestId;
                next.guestRole = Unassigned;
                next.guestName = "";
                next.request = "";
                next.requestAccepted = false;
            }
            if (guest.m_SteamID != 0)
            {
                string request = SteamMatchmaking.GetLobbyMemberData(lobby, guest, RequestKey);
                if (request != next.request && IsRoleRequest(request))
                {
                    int wanted = request[0] - '0';
                    next.request = request;
                    next.requestAccepted = wanted != next.hostRole;
                    if (next.requestAccepted) next.guestRole = wanted;
                    changed = true;
                }
            }
            return changed && WriteState(next);
        }

        static bool IsRoleRequest(string request) => request != null && request.Length == 34
            && (request[0] == '0' || request[0] == '1') && request[1] == ':'
            && Guid.TryParseExact(request.Substring(2), "N", out _);

        bool OwnerIsPresent() => lobby.m_SteamID != 0 && !string.IsNullOrEmpty(ownerId)
            && SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID.ToString(CultureInfo.InvariantCulture) == ownerId;

        bool ReadState(out string json, out RoomState remote)
        {
            json = SteamMatchmaking.GetLobbyData(lobby, StateKey);
            remote = null;
            if (string.IsNullOrEmpty(json) || json.Length > 2048) return false;
            try { remote = JsonUtility.FromJson<RoomState>(json); }
            catch (ArgumentException) { return false; }
            if (remote == null || !ValidRole(remote.hostRole) || !ValidRole(remote.guestRole)) return false;
            if (remote.hostRole != Unassigned && remote.hostRole == remote.guestRole) return false;
            return !remote.started || (remote.hostRole != Unassigned && remote.guestRole != Unassigned
                && ulong.TryParse(remote.guestId, NumberStyles.None, CultureInfo.InvariantCulture, out ulong guest) && guest != 0
                && remote.guestId != ownerId);
        }

        bool WriteState(RoomState next)
        {
            string json = JsonUtility.ToJson(next);
            if (!SteamMatchmaking.SetLobbyData(lobby, StateKey, json)) return false;
            state = next;
            stateJson = json;
            return true;
        }

        static bool ValidRole(int role) => role == Unassigned || role == (int)CrewRole.Captain || role == (int)CrewRole.Diver;
        static CrewRole? DecodeRole(int role) => role == Unassigned ? (CrewRole?)null : (CrewRole)role;

        CrewRole? GetRole(string id) => id == ownerId ? DecodeRole(state.hostRole)
            : id == state.guestId ? DecodeRole(state.guestRole) : null;

        bool UpdateMembers()
        {
            var next = new List<OnlineRoomMember>(Capacity);
            int count = SteamMatchmaking.GetNumLobbyMembers(lobby);
            for (int index = 0; index < count; index++)
            {
                CSteamID steamId = SteamMatchmaking.GetLobbyMemberByIndex(lobby, index);
                string id = steamId.m_SteamID.ToString(CultureInfo.InvariantCulture);
                if (state.started && id != ownerId && id != state.guestId) continue;
                next.Add(new OnlineRoomMember(id, ReadPersonaName(steamId, id), GetRole(id), id == ownerId));
            }
            next.Sort((first, second) => second.IsHost.CompareTo(first.IsHost));
            bool hasChanges = next.Count != members.Count;
            if (!hasChanges)
                for (int index = 0; index < next.Count; index++)
                    if (next[index].SteamId != members[index].SteamId || next[index].Role != members[index].Role
                        || next[index].DisplayName != members[index].DisplayName) { hasChanges = true; break; }
            if (hasChanges) members = next.AsReadOnly();
            return hasChanges;
        }

        string ReadPersonaName(CSteamID steamId, string id)
        {
            string name = id == localId ? SteamFriends.GetPersonaName() : SteamFriends.GetFriendPersonaName(steamId);
            if (string.IsNullOrWhiteSpace(name) || name == "[unknown]")
            {
                if (id != localId && requestedNames.Add(steamId.m_SteamID)) SteamFriends.RequestUserInformation(steamId, true);
                return id == state.guestId && !string.IsNullOrEmpty(state.guestName) ? state.guestName : id;
            }
            return name.Replace('\r', ' ').Replace('\n', ' ');
        }

        public override void Leave()
        {
            if (Busy)
            {
                abandoned = true;
                SetStatus("");
                return;
            }
            CloseRoom("");
        }

        void CloseRoom(string message)
        {
            if (initialized && lobby.m_SteamID != 0)
            {
                if (localId == ownerId && OwnerIsPresent())
                {
                    SteamMatchmaking.SetLobbyJoinable(lobby, false);
                    if (state != null)
                    {
                        RoomState closed = state.Copy();
                        closed.closed = true;
                        WriteState(closed);
                    }
                }
                SteamMatchmaking.LeaveLobby(lobby);
            }
            lobby = default;
            ownerId = code = pendingRoleRequest = stateJson = "";
            state = null;
            members = Array.Empty<OnlineRoomMember>();
            requestedNames.Clear();
            if (Busy && !abandoned) EndOperation(message);
            else
            {
                status = message;
                if (!exiting) NotifyChanged();
            }
        }

        void SetStatus(string message)
        {
            if (status == message) return;
            status = message;
            NotifyChanged();
        }

        void OnDestroy()
        {
            exiting = true;
            CloseRoom("");
            created?.Dispose();
            found?.Dispose();
            entered?.Dispose();
            if (initialized) SteamAPI.Shutdown();
        }
    }
}
#endif
