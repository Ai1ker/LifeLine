using System.IO;

namespace Lifeline
{
    public static class OnlineSaveSlot
    {
        public static string PathFor(string steamId)
        {
            if (string.IsNullOrEmpty(steamId) || !ulong.TryParse(steamId, out ulong id) || id == 0) return null;
            return Path.Combine(Path.GetDirectoryName(GameSaveStore.DefaultPath), "Online", id.ToString(), "voyage-v1.json");
        }

        public static bool TryRead(string steamId, out GameSaveData data, out string error)
        {
            data = null;
            error = "online.save.missing";
            string path = PathFor(steamId);
            return path != null && File.Exists(path) && GameSaveStore.TryRead(path, out data, out error);
        }
    }
}
