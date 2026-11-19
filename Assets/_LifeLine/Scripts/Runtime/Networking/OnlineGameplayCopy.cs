namespace Lifeline
{
    public static class OnlineGameplayCopy
    {
        public static LocalizationTable.Entry[] Entries() => new[]
        {
            new LocalizationTable.Entry("online.connection.unavailable", "此版本缺少 Steam 联机传输组件。", "The Steam transport is unavailable in this build."),
            new LocalizationTable.Entry("online.connection.failed", "无法建立联机连接，请重试。", "Could not establish the online connection. Try again."),
            new LocalizationTable.Entry("online.connection.lost", "连接已断开。可用原房间码重新加入。", "Connection lost. Join again with the same room code."),
            new LocalizationTable.Entry("online.connection.timeout", "等待联机连接超时，请重新加入。", "The online connection timed out. Join again."),
            new LocalizationTable.Entry("online.connection.incompatible", "联机状态无法载入，请确认双方使用相同版本。", "Could not load the online state. Check that both players use the same build."),
            new LocalizationTable.Entry("online.host.closed", "房主已退出，房间已解散。", "The host left and the room closed."),
            new LocalizationTable.Entry("online.room.left", "已离开航程，可用原房间码重新加入。", "You left the voyage. Use the same room code to rejoin."),
            new LocalizationTable.Entry("online.loading.prepare", "正在准备 Steam 联机", "Preparing Steam multiplayer"),
            new LocalizationTable.Entry("online.loading.connect", "正在等待 Steam 队友连接", "Waiting for your Steam teammate"),
            new LocalizationTable.Entry("online.loading.scene", "正在加载航程场景", "Loading the gameplay scene"),
            new LocalizationTable.Entry("online.loading.state", "正在同步房主航程状态", "Syncing the host's voyage"),
            new LocalizationTable.Entry("online.save.missing", "没有联机存档。", "There is no online save."),
            new LocalizationTable.Entry("online.save.failed", "联机存档无法载入。", "Could not load the online save."),
            new LocalizationTable.Entry("online.waiting.peer", "等待队友重新连接 · 房间码 {0}", "Waiting for your teammate · Room code {0}"),
            new LocalizationTable.Entry("online.waiting.host", "房主已暂停航程。", "The host paused the voyage."),
            new LocalizationTable.Entry("online.camera", "联机镜头：全屏跟随本地角色", "Online camera: Full-screen local follow")
        };
    }
}
