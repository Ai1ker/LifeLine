namespace Lifeline
{
    public static class SteamRoomCopy
    {
        public static LocalizationTable.Entry[] Entries() => new LocalizationTable.Entry[]
        {
            Entry("online.steam.unavailable", "请先启动 Steam 并登录，再重试。", "Open Steam and sign in, then retry."),
            Entry("online.steam.wrongApp", "此测试版需要 Steam 测试 App ID 480。", "This test build requires Steam test App ID 480."),
            Entry("online.room.creating", "正在创建房间…", "Creating room…"),
            Entry("online.room.searching", "正在查找房间…", "Looking for room…"),
            Entry("online.room.joining", "正在加入房间…", "Joining room…"),
            Entry("online.room.createFailed", "创建房间失败，请重试。", "Could not create room. Try again."),
            Entry("online.room.joinFailed", "加入房间失败，请重试。", "Could not join room. Try again."),
            Entry("online.room.noMatch", "没有找到可加入的房间，请检查房间码。", "No joinable room found. Check the room code."),
            Entry("online.room.ambiguous", "该房间码暂时冲突，请让房主重新创建房间。", "This room code is in use by multiple rooms. Ask the host to create a new room."),
            Entry("online.room.codeInvalid", "请输入由字母和数字组成的6位房间码。", "Enter a six-character room code containing letters and digits."),
            Entry("online.room.closed", "房主已离开，房间已解散。", "The host left and the room closed."),
            Entry("online.room.full", "房间已满或席位保留给原来的队友。", "The room is full or the seat is reserved for the original crewmate."),
            Entry("online.room.roleTaken", "这个职责已被选，请选择另一个。", "This role is taken. Choose the other role."),
            Entry("online.room.roleRequested", "正在确认职责…", "Confirming role…"),
            Entry("online.room.updateFailed", "房间更新失败，请重试。", "Could not update the room. Try again."),
            Entry("online.room.waitRoles", "需要两位玩家各选一个职责才可开局。", "Both players must choose different roles before starting."),
            Entry("online.room.requestTimedOut", "Steam 房间请求超时，请重试。", "Steam room request timed out. Try again."),
            Entry("online.room.connectionFailed", "Steam 连接已中断，请检查连接后重试。", "Steam disconnected. Check your connection and retry."),
        };

        static LocalizationTable.Entry Entry(string key, string zh, string en)
            => new LocalizationTable.Entry(key, zh, en) { fragment = false };
    }
}
