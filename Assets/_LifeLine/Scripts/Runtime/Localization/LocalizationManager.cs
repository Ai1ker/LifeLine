using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Lifeline
{
    [DefaultExecutionOrder(-1000)]
    public sealed class LocalizationManager : MonoBehaviour
    {
        public const string PreferenceKey = "LifeLine.Language.v1";
        public LocalizationTable table;
        public GameLanguage languageOverride = GameLanguage.System;
        public bool loadSavedPreference = true;
        public static LocalizationManager Current { get; private set; }
        public static event Action LanguageChanged;
        public event Action Changed;
        private static GameLanguage preference;
        private static bool preferenceLoaded;
        private LocalizationTable loadedTable;
        private int loadedRevision = -1;
        private LocalizationTranslator translator;

        public static GameLanguage LanguagePreference
        {
            get { EnsurePreference(); return Current != null ? Current.languageOverride : preference; }
        }

        public static GameLanguage ResolveLanguage(GameLanguage selected, SystemLanguage systemLanguage)
        {
            if (selected == GameLanguage.Chinese || selected == GameLanguage.English) return selected;
            return systemLanguage == SystemLanguage.Chinese || systemLanguage == SystemLanguage.ChineseSimplified || systemLanguage == SystemLanguage.ChineseTraditional
                ? GameLanguage.Chinese : GameLanguage.English;
        }
        public static GameLanguage GetCurrentLanguage() => ResolveLanguage(LanguagePreference, Application.systemLanguage);

        public static void SetLanguage(GameLanguage selected)
        {
            if (!Enum.IsDefined(typeof(GameLanguage), selected)) selected = GameLanguage.System;
            preference = selected; preferenceLoaded = true;
            PlayerPrefs.SetInt(PreferenceKey, (int)selected); PlayerPrefs.Save();
            if (Current != null) Current.languageOverride = selected;
            NotifyLanguageChanged();
        }
        private static void EnsurePreference()
        {
            if (preferenceLoaded) return;
            int saved = PlayerPrefs.GetInt(PreferenceKey, (int)GameLanguage.System);
            preference = saved >= 0 && saved <= 2 ? (GameLanguage)saved : GameLanguage.System;
            preferenceLoaded = true;
        }
        private static void NotifyLanguageChanged()
        {
            LanguageChanged?.Invoke();
            if (Current != null) Current.Changed?.Invoke();
        }

        private void Awake()
        {
            EnsurePreference();
            if (loadSavedPreference && PlayerPrefs.HasKey(PreferenceKey)) languageOverride = preference;
            else preference = languageOverride;
            Current = this;
            RefreshTable();
        }
        private void OnDestroy() { if (Current == this) Current = null; }
        private void Update()
        {
            if (Current == this && (loadedTable != table || (table != null && loadedRevision != table.Revision))) RefreshTable();
        }
        private void OnValidate()
        {
            translator = null;
            if (Application.isPlaying && Current == this) NotifyLanguageChanged();
        }

        /// <summary>Call after replacing/editing the table at runtime. Inspector edits are detected by Revision.</summary>
        public void RefreshTable()
        {
            translator = new LocalizationTranslator(table); loadedTable = table; loadedRevision = table != null ? table.Revision : 0;
            if (Current == this) NotifyLanguageChanged();
        }
        private LocalizationTranslator Translator
        {
            get
            {
                if (translator == null || loadedTable != table || (table != null && loadedRevision != table.Revision)) RefreshTable();
                return translator;
            }
        }
        public GameLanguage CurrentLanguage => ResolveLanguage(languageOverride, Application.systemLanguage);
        public string Translate(string source) => Translator.Translate(source, CurrentLanguage);
        public string Format(string key, params object[] args) => Translator.Format(key, CurrentLanguage, args);
        public List<string> FindUntranslatedChinese(IEnumerable<string> sources) => Translator.FindUntranslatedChinese(sources);

        public List<string> FindUntranslatedChinese()
        {
            var sources = new List<string>();
            foreach (var binder in FindObjectsByType<LocalizationTextBinder>(FindObjectsInactive.Include)) sources.AddRange(binder.GetSources());
            foreach (var text in FindObjectsByType<Text>(FindObjectsInactive.Include)) sources.Add(text.text);
            foreach (var text in FindObjectsByType<TextMesh>(FindObjectsInactive.Include)) sources.Add(text.text);
            return FindUntranslatedChinese(sources);
        }
        [ContextMenu("Find Untranslated Chinese")]
        private void LogUntranslatedChinese()
        {
            var missing = FindUntranslatedChinese();
            Debug.Log("Localization coverage: " + missing.Count + " displayed sources still contain untranslated Chinese.\n" + string.Join("\n", missing), this);
        }
    }
}
