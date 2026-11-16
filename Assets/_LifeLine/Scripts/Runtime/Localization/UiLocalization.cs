using UnityEngine;
using UnityEngine.UI;
namespace Lifeline
{
    public static class UiLocalization
    {
        public static string Text(string source) => LocalizationManager.Current != null ? LocalizationManager.Current.Translate(source) : source;
        public static void Refresh(GameObject root)
        {
            if(root==null)return;
            foreach(var text in root.GetComponentsInChildren<Text>(true))text.text=Text(text.text);
            foreach(var binder in Object.FindObjectsByType<LocalizationTextBinder>()) { binder.RebuildTargets(); binder.Refresh(); }
        }
    }
}
