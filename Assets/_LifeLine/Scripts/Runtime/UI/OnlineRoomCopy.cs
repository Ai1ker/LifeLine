namespace Lifeline
{
    public static class OnlineRoomCopy
    {
        public static LocalizationTable.Entry[] Entries() => new[]
        {
            Entry("online.menu.entry", "联机模式", "Online Co-op"),
            Entry("online.menu.title", "Steam 双人联机", "Steam Co-op"),
            Entry("online.menu.create", "创建房间", "Create Room"),
            Entry("online.menu.join", "加入房间", "Join Room"),
            Entry("online.menu.codePrompt", "输入 6 位房间码", "Enter a 6-character room code"),
            Entry("online.menu.codePlaceholder", "6 位字母和数字", "6 letters and digits"),
            Entry("online.menu.codeLabel", "房间码", "Room Code"),
            Entry("online.menu.copyCode", "复制房间码", "Copy Code"),
            Entry("online.menu.copied", "房间码已复制。", "Room code copied."),
            Entry("online.menu.newGame", "创建新航程", "Start a New Voyage"),
            Entry("online.menu.resumeGame", "读取联机存档并创建房间", "Host From Online Save"),
            Entry("online.menu.savedDay", "联机存档 · 第 {0} 天", "Online Save · Day {0}"),
            Entry("online.menu.noProvider", "此版本的 Steam 联机组件不可用。", "Steam co-op is unavailable in this build."),
            Entry("online.menu.retry", "重试 Steam 连接", "Retry Steam Connection"),
            Entry("online.menu.count", "玩家 {0} / 2", "Players {0} / 2"),
            Entry("online.menu.roles", "选择职责", "Choose Your Role"),
            Entry("online.menu.unassigned", "未选职责", "Role Not Chosen"),
            Entry("online.menu.waitingForPlayer", "等待玩家加入", "Waiting for a Player"),
            Entry("online.menu.host", "房主", "Host"),
            Entry("online.menu.guest", "来宾", "Guest"),
            Entry("online.menu.you", "你", "You"),
            Entry("online.menu.captainChosen", "船长（已选）", "Captain (Selected)"),
            Entry("online.menu.diverChosen", "潜水员（已选）", "Diver (Selected)"),
            Entry("online.menu.start", "开始游戏", "Start Game"),
            Entry("online.menu.waitRoles", "两名玩家各选一个职责后即可开始。", "Assign different roles to both players to begin."),
            Entry("online.menu.waitHost", "等待房主开始游戏。", "Waiting for the host to start."),
            Entry("online.menu.starting", "正在进入航程…", "Entering the voyage…"),
            Entry("online.menu.leave", "离开房间", "Leave Room"),
            Entry("online.menu.closeRoom", "解散房间", "Disband Room"),
            Entry("online.menu.back", "返回", "Back"),
            Entry("online.menu.saveCannotRead", "联机存档无法读取。", "The online save could not be read.")
        };

        static LocalizationTable.Entry Entry(string key, string chinese, string english)
            => new LocalizationTable.Entry(key, chinese, english) { fragment = false };
    }
}
