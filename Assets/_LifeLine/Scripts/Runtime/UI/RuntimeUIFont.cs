using UnityEngine;
using UnityEngine.UI;

namespace Lifeline
{
    /// <summary>A font asset can replace the OS fallback without changing gameplay or UI scripts.</summary>
    public class RuntimeUIFont : MonoBehaviour
    {
        public Font fontAsset;
        public string[] fallbackOSFonts = { "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "Arial" };
        public static Font Current { get; private set; }
        public static Font GetOrCreateFont()
        {
            if (Current == null) Current = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 20);
            if (Current == null) Current = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return Current;
        }
        private void OnEnable()
        {
            if (fontAsset != null) Current = fontAsset;
            if (Current == null) Current = Font.CreateDynamicFontFromOSFont(fallbackOSFonts, 20);
            if (Current == null) Current = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (Text text in GetComponentsInChildren<Text>(true)) text.font = Current;
        }
    }
}
