#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Lifeline.Editor
{
    public sealed class SteamTestBuildAppId : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64 &&
                report.summary.platform != BuildTarget.StandaloneWindows) return;
            if (System.Type.GetType("Lifeline.SteamRoomService, LifeLine.Steam") == null) return;
            string folder = Path.GetDirectoryName(report.summary.outputPath);
            if (!string.IsNullOrEmpty(folder))
                File.WriteAllText(Path.Combine(folder, "steam_appid.txt"), "480\n");
        }
    }
}
#endif
