using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lifeline
{
    /// <summary>Menu-only view of the platform room service. The service owns membership and start decisions.</summary>
    public sealed class OnlineRoomPanel : MonoBehaviour
    {
        enum Page { Home, Create, Join }

        [Serializable]
        public sealed class PlayerRow
        {
            public Text nickname;
            public Text role;
            public Text membership;
        }

        public GameObject homePage, createPage, joinPage, roomPage;
        public Button createButton, joinButton, newVoyageButton, resumeVoyageButton;
        public Button confirmJoinButton, retryButton, copyCodeButton, startButton, backButton;
        public Button captainButton, diverButton;
        public InputField codeInput;
        public Text codePlaceholder, saveSummary, roomCode, playerCount, status, roomHint;
        public PlayerRow[] playerRows;

        MainMenuController menu;
        OnlineRoomService service;
        Page page;
        string notice = string.Empty;
        bool hasOnlineSave;
        int savedDay;

        public bool IsOpen => gameObject.activeSelf;

        void Awake()
        {
            createButton.onClick.AddListener(ChooseCreate);
            joinButton.onClick.AddListener(ChooseJoin);
            newVoyageButton.onClick.AddListener(() => Create(false));
            resumeVoyageButton.onClick.AddListener(() => Create(true));
            confirmJoinButton.onClick.AddListener(Join);
            retryButton.onClick.AddListener(Retry);
            copyCodeButton.onClick.AddListener(CopyCode);
            captainButton.onClick.AddListener(() => SelectRole(CrewRole.Captain));
            diverButton.onClick.AddListener(() => SelectRole(CrewRole.Diver));
            startButton.onClick.AddListener(StartVoyage);
            backButton.onClick.AddListener(GoBack);
            codeInput.onValidateInput = ValidateCodeCharacter;
            codeInput.onValueChanged.AddListener(_ => Refresh());
            codeInput.characterLimit = OnlineRoomService.RoomCodeLength;
        }

        void OnEnable()
        {
            LocalizationManager.LanguageChanged += Refresh;
            AttachService();
            Refresh();
        }

        void OnDisable()
        {
            LocalizationManager.LanguageChanged -= Refresh;
            if (service != null) service.Changed -= Refresh;
            service = null;
        }

        public void Open(MainMenuController owner, string noticeKey = null)
        {
            menu = owner;
            page = Page.Home;
            notice = noticeKey ?? string.Empty;
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            service?.RefreshAvailability();
            Refresh();
            UiLocalization.Refresh(gameObject);
            Select(createButton.interactable ? createButton : retryButton);
        }

        void AttachService()
        {
            if (service != null) return;
            service = OnlineRoomService.Instance;
            if (service != null) service.Changed += Refresh;
        }

        void Retry()
        {
            notice = string.Empty;
            AttachService();
            service?.RefreshAvailability();
            Refresh();
        }

        void ChooseCreate()
        {
            if (!CanRequestRoom) return;
            notice = string.Empty;
            hasOnlineSave = OnlineSaveSlot.TryRead(service.LocalSteamId, out GameSaveData saved, out string error);
            if (hasOnlineSave)
            {
                savedDay = saved.state.day;
                page = Page.Create;
                Refresh();
                Select(resumeVoyageButton);
                return;
            }
            if (string.IsNullOrEmpty(error) || error == "online.save.missing" || error == "未找到存档。")
            {
                Create(false);
                return;
            }
            // A damaged slot is distinct from having no save, so starting a new voyage is explicit.
            page = Page.Create;
            notice = "online.menu.saveCannotRead";
            Refresh();
            Select(newVoyageButton);
        }

        void ChooseJoin()
        {
            if (!CanRequestRoom) return;
            notice = string.Empty;
            page = Page.Join;
            Refresh();
            codeInput.Select();
            codeInput.ActivateInputField();
        }

        bool CanRequestRoom => service != null && service.Available && !service.Busy && !service.InRoom
            && !SceneFadeTransition.IsTransitioning;

        void Create(bool resume)
        {
            if (!CanRequestRoom || resume && !hasOnlineSave) return;
            notice = string.Empty;
            service.Create(resume);
            Refresh();
        }

        void Join()
        {
            if (!CanRequestRoom || !OnlineRoomService.TryNormalizeRoomCode(codeInput.text, out string code)) return;
            notice = string.Empty;
            codeInput.DeactivateInputField();
            service.Join(code);
            Refresh();
        }

        void SelectRole(CrewRole role)
        {
            if (service == null || !service.InRoom || service.Started || service.Busy) return;
            notice = string.Empty;
            service.SelectRole(role);
            Refresh();
        }

        void StartVoyage()
        {
            if (service == null || !service.CanStart || SceneFadeTransition.IsTransitioning) return;
            notice = string.Empty;
            service.StartGame();
            Refresh();
        }

        void CopyCode()
        {
            if (service == null || !service.InRoom) return;
            GUIUtility.systemCopyBuffer = service.RoomCode;
            notice = "online.menu.copied";
            Refresh();
        }

        public void GoBack()
        {
            if (service != null && service.Started || SceneFadeTransition.IsTransitioning) return;
            if (service != null && (service.InRoom || service.Busy))
            {
                service.Leave();
                Close();
            }
            else if (page != Page.Home)
            {
                page = Page.Home;
                notice = string.Empty;
                Refresh();
                Select(createButton);
            }
            else Close();
        }

        void Close()
        {
            gameObject.SetActive(false);
            menu?.OnOnlineRoomClosed();
        }

        void Refresh()
        {
            bool inRoom = service != null && service.InRoom;
            bool busy = service != null && service.Busy;
            bool started = service != null && service.Started;
            homePage.SetActive(!inRoom && page == Page.Home);
            createPage.SetActive(!inRoom && page == Page.Create);
            joinPage.SetActive(!inRoom && page == Page.Join);
            roomPage.SetActive(inRoom);
            createButton.interactable = joinButton.interactable = newVoyageButton.interactable = CanRequestRoom;
            resumeVoyageButton.gameObject.SetActive(hasOnlineSave);
            resumeVoyageButton.interactable = CanRequestRoom && hasOnlineSave;
            saveSummary.text = hasOnlineSave ? Format("online.menu.savedDay", savedDay) : string.Empty;
            confirmJoinButton.interactable = CanRequestRoom && OnlineRoomService.TryNormalizeRoomCode(codeInput.text, out _);
            codeInput.interactable = CanRequestRoom;
            codePlaceholder.text = UiLocalization.Text("online.menu.codePlaceholder");
            retryButton.gameObject.SetActive(!inRoom && service != null && !service.Available);
            retryButton.interactable = !busy;
            backButton.interactable = !started && !SceneFadeTransition.IsTransitioning;
            Label(backButton, inRoom ? service.IsHost ? "online.menu.closeRoom" : "online.menu.leave" : "online.menu.back");

            string error = service == null ? "online.menu.noProvider" : service.StatusKey;
            status.text = !string.IsNullOrEmpty(notice) ? notice : error ?? string.Empty;
            int memberCount = inRoom ? service.Members.Count : 0;
            if (isActiveAndEnabled)
            {
                if (memberCount > audibleMemberCount) GameAudio.Local(AudioCue.OnlineJoin);
                if (!string.IsNullOrEmpty(error) && error != audibleStatus && !busy)
                    GameAudio.Local(AudioCue.OnlineFail, .8f, .3f);
            }
            audibleMemberCount = memberCount;
            audibleStatus = error;
            if (inRoom) RefreshRoom(started, busy);
        }

        int audibleMemberCount;
        string audibleStatus;

        void RefreshRoom(bool started, bool busy)
        {
            IReadOnlyList<OnlineRoomMember> players = service.Members;
            roomCode.text = service.RoomCode;
            playerCount.text = Format("online.menu.count", players.Count);
            CrewRole? ownRole = null;
            bool captainTaken = false, diverTaken = false;
            for (int index = 0; index < playerRows.Length; index++)
            {
                var row = playerRows[index];
                if (index >= players.Count)
                {
                    row.nickname.text = UiLocalization.Text("online.menu.waitingForPlayer");
                    row.role.text = row.membership.text = string.Empty;
                    continue;
                }
                var player = players[index];
                bool local = player.SteamId == service.LocalSteamId;
                row.nickname.text = player.DisplayName;
                row.role.text = player.Role.HasValue ? RoleKey(player.Role.Value) : "online.menu.unassigned";
                row.membership.text = local ? "online.menu.you" : player.IsHost ? "online.menu.host" : "online.menu.guest";
                if (local) ownRole = player.Role;
                else if (player.Role == CrewRole.Captain) captainTaken = true;
                else if (player.Role == CrewRole.Diver) diverTaken = true;
            }
            captainButton.interactable = !started && !busy && !captainTaken && ownRole != CrewRole.Captain;
            diverButton.interactable = !started && !busy && !diverTaken && ownRole != CrewRole.Diver;
            Label(captainButton, ownRole == CrewRole.Captain ? "online.menu.captainChosen" : RoleKey(CrewRole.Captain));
            Label(diverButton, ownRole == CrewRole.Diver ? "online.menu.diverChosen" : RoleKey(CrewRole.Diver));
            bool rolesReady = players.Count == OnlineRoomService.Capacity && players[0].Role.HasValue
                && players[1].Role.HasValue && players[0].Role != players[1].Role;
            roomHint.text = started ? "online.menu.starting" : rolesReady ? service.IsHost ? string.Empty : "online.menu.waitHost" : "online.menu.waitRoles";
            copyCodeButton.interactable = !started;
            startButton.gameObject.SetActive(service.IsHost);
            startButton.interactable = !started && !busy && service.CanStart && !SceneFadeTransition.IsTransitioning;
        }

        static char ValidateCodeCharacter(string current, int index, char character)
        {
            char upper = char.ToUpperInvariant(character);
            return upper >= 'A' && upper <= 'Z' || upper >= '0' && upper <= '9' ? upper : '\0';
        }

        static string RoleKey(CrewRole role) => role == CrewRole.Captain ? "role.captain" : "role.diver";
        static string Format(string key, int value) => string.Format(UiLocalization.Text(key), value);
        static void Label(Button button, string key) => button.GetComponentInChildren<Text>(true).text = key;
        static void Select(Button button)
        {
            if (EventSystem.current != null && button.gameObject.activeInHierarchy && button.interactable)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
        }
    }
}
