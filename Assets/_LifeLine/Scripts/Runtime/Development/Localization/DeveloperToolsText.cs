#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Lifeline.Development
{
    // Kept with the removable tools; no entries or references are added to game assets.
    public static class DeveloperToolsText
    {
        public static string Get(string english)
        {
            if (LocalizationManager.GetCurrentLanguage() != GameLanguage.Chinese) return english;
            switch (english)
            {
                case "Developer test tools / F8": return "开发测试工具 / F8";
                case "Start a local game first.": return "请先进入本地游戏。";
                case "Commands use a separate developer save slot.": return "测试命令使用独立开发存档，不覆盖玩家存档。";
                case "Next day with cave": return "下一天（必出洞穴）";
                case "Next day with City seawater filter": return "下一天（城区必出海水过滤器）";
                case "Seawater filter already acquired": return "海水过滤器已获取（不再刷新）";
                case "Infinite oxygen / both crew": return "无限氧气（船长与潜水员）";
                case "Skip active interaction QTE": return "跳过当前设备QTE（保留材料消耗）";
                case "Complete current interaction": return "立即完成当前设备交互";
                case "Daylight": return "白天";
                case "Thunderstorm": return "雷雨夜";
                case "Move both crew into cabin": return "两名船员移入船舱";
                case "Add materials / fuel": return "添加材料和燃油";
                case "Restore hull / power / oxygen / crew": return "恢复耐久、电力、氧气和船员";
                case "Create 2 attached cargo crates": return "生成两个已挂绳货箱";
                case "Summon abyss shadow": return "召唤深渊之影（切至夜晚）";
                case "Speed x1": return "正常速度";
                case "Speed x4": return "4倍速度";
                case "Delete Scripts/Runtime/Development to remove all tools.": return "删除 Scripts/Runtime/Development 即可移除全部测试工具。";
                default: return english;
            }
        }
    }
}
#endif
