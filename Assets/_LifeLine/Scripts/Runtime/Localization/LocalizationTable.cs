using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    public enum GameLanguage { System, Chinese, English }

    /// <summary>Editable display copy. Gameplay state and save data never depend on translated strings.</summary>
    [CreateAssetMenu(menuName = "LifeLine/Localization Table", fileName = "LocalizationTable")]
    public sealed class LocalizationTable : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Stable optional lookup key. Existing Chinese source text can also be used directly.")]
            public string key;
            [TextArea(1, 6)] public string zh;
            [TextArea(1, 6)] public string en;
            [Tooltip("Also translate this entry inside dynamic sentences. Disable for ambiguous short words. Templates use {0}, {1}, ... instead.")]
            public bool fragment = true;

            public Entry() { }
            public Entry(string key, string zh, string en) { this.key = key; this.zh = zh; this.en = en; }
        }

        public List<Entry> entries = new List<Entry>();
        public int Revision { get; private set; }

        /// <summary>Call after changing entries through code so a live manager rebuilds its lookup.</summary>
        public void NotifyChanged() { Revision++; }
        private void OnValidate() { NotifyChanged(); }
        private void OnEnable() { NotifyChanged(); }
    }
}
