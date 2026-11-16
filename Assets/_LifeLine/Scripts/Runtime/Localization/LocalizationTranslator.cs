using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Lifeline
{
    /// <summary>Exact keys, complete sentence templates, then one longest-first fragment pass.</summary>
    public sealed class LocalizationTranslator
    {
        private sealed class Fragment
        {
            public string source, target;
            public bool startBoundary, endBoundary;
        }
        private sealed class Template
        {
            public Regex pattern;
            public string target;
            public int literalLength;
        }
        private sealed class LanguageLookup
        {
            public readonly Dictionary<string, string> exact = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly Dictionary<char, List<Fragment>> fragments = new Dictionary<char, List<Fragment>>();
            public readonly List<Template> templates = new List<Template>();
            public readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        private static readonly Regex Placeholder = new Regex(@"\{(\d+)(?:[^{}]*)\}", RegexOptions.CultureInvariant);
        private static readonly Regex Chinese = new Regex(@"[\u3400-\u4dbf\u4e00-\u9fff\uf900-\ufaff]", RegexOptions.CultureInvariant);
        private readonly LanguageLookup chinese = new LanguageLookup(), english = new LanguageLookup();

        public LocalizationTranslator(LocalizationTable table)
        {
            if (table == null || table.entries == null) return;
            foreach (var entry in table.entries)
            {
                if (entry == null) continue;
                AddEntry(chinese, entry, entry.zh, entry.en, true);
                AddEntry(english, entry, entry.en, entry.zh, false);
            }
            Sort(chinese); Sort(english);
        }

        private static void AddEntry(LanguageLookup lookup, LocalizationTable.Entry entry, string target, string source, bool fromEnglish)
        {
            if (string.IsNullOrEmpty(target)) return;
            AddExact(lookup, entry.key, target);
            AddExact(lookup, entry.zh, target);
            AddExact(lookup, entry.en, target);
            if (string.IsNullOrEmpty(source) || source == target) return;
            var matches = Placeholder.Matches(source);
            if (matches.Count > 0)
            {
                var expression = new StringBuilder(@"\A");
                var groups = new HashSet<string>();
                int cursor = 0, literalLength = 0;
                foreach (Match match in matches)
                {
                    string literal = source.Substring(cursor, match.Index - cursor);
                    expression.Append(Regex.Escape(literal)); literalLength += literal.Trim().Length;
                    string group = "p" + match.Groups[1].Value;
                    expression.Append(groups.Add(group) ? "(?<" + group + ">.*?)" : @"\k<" + group + ">");
                    cursor = match.Index + match.Length;
                }
                string tail = source.Substring(cursor);
                expression.Append(Regex.Escape(tail)).Append(@"\z"); literalLength += tail.Trim().Length;
                // A bare {0} would claim every string and prevent useful fragment translation.
                if (literalLength > 0) lookup.templates.Add(new Template
                {
                    pattern = new Regex(expression.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(30)),
                    target = target, literalLength = literalLength
                });
                return;
            }
            if (!entry.fragment) return;
            if (!lookup.fragments.TryGetValue(source[0], out var list)) lookup.fragments[source[0]] = list = new List<Fragment>();
            foreach (var item in list) if (item.source == source) return;
            list.Add(new Fragment
            {
                source = source, target = target,
                startBoundary = fromEnglish && IsWord(source[0]),
                endBoundary = fromEnglish && IsWord(source[source.Length - 1])
            });
        }

        private static void AddExact(LanguageLookup lookup, string source, string target)
        {
            if (!string.IsNullOrEmpty(source) && !lookup.exact.ContainsKey(source)) lookup.exact.Add(source, target);
        }
        private static void Sort(LanguageLookup lookup)
        {
            lookup.templates.Sort((a, b) => b.literalLength.CompareTo(a.literalLength));
            foreach (var list in lookup.fragments.Values) list.Sort((a, b) => b.source.Length.CompareTo(a.source.Length));
        }
        private static bool IsWord(char value) => char.IsLetterOrDigit(value) || value == '_';

        public string Translate(string source, GameLanguage language)
        {
            if (string.IsNullOrEmpty(source)) return source ?? string.Empty;
            var lookup = language == GameLanguage.Chinese ? chinese : english;
            if (lookup.exact.TryGetValue(source, out string exact)) return exact;
            if (lookup.cache.TryGetValue(source, out string cached)) return cached;
            string translated = null;
            foreach (var template in lookup.templates)
            {
                Match match;
                try { match = template.pattern.Match(source); }
                catch (RegexMatchTimeoutException) { continue; }
                if (!match.Success) continue;
                translated = Placeholder.Replace(template.target, placeholder =>
                {
                    var group = match.Groups["p" + placeholder.Groups[1].Value];
                    return group.Success ? TranslateFragments(group.Value, lookup) : placeholder.Value;
                });
                break;
            }
            if (translated == null) translated = TranslateFragments(source, lookup);
            if (lookup.cache.Count >= 2048) lookup.cache.Clear();
            lookup.cache[source] = translated;
            return translated;
        }

        private static string TranslateFragments(string source, LanguageLookup lookup)
        {
            var output = new StringBuilder(source.Length);
            for (int i = 0; i < source.Length;)
            {
                // Do not translate UGUI/TextMesh rich-text markup or its attributes.
                if (source[i] == '<')
                {
                    int tagEnd = source.IndexOf('>', i + 1);
                    if (tagEnd >= 0) { output.Append(source, i, tagEnd - i + 1); i = tagEnd + 1; continue; }
                }
                Fragment found = null;
                if (lookup.fragments.TryGetValue(source[i], out var fragments))
                    foreach (var fragment in fragments)
                    {
                        int end = i + fragment.source.Length;
                        if (end > source.Length || string.CompareOrdinal(source, i, fragment.source, 0, fragment.source.Length) != 0) continue;
                        if (fragment.startBoundary && i > 0 && IsWord(source[i - 1])) continue;
                        if (fragment.endBoundary && end < source.Length && IsWord(source[end])) continue;
                        found = fragment; break;
                    }
                if (found == null) output.Append(source[i++]);
                else { output.Append(found.target); i += found.source.Length; }
            }
            return output.ToString();
        }

        public string Format(string key, GameLanguage language, params object[] args)
        {
            string format = Translate(key, language);
            if (args == null || args.Length == 0) return format;
            var localized = new object[args.Length];
            for (int i = 0; i < args.Length; i++) localized[i] = args[i] is string text ? Translate(text, language) : args[i];
            try { return string.Format(language == GameLanguage.Chinese ? CultureInfo.GetCultureInfo("zh-CN") : CultureInfo.GetCultureInfo("en-US"), format, localized); }
            catch (FormatException) { return format; } // An edited invalid entry remains visible and can be diagnosed.
        }

        public static bool ContainsChinese(string text) => !string.IsNullOrEmpty(text) && Chinese.IsMatch(text);

        /// <summary>Returns original display sources whose English result still contains Chinese, without duplicates.</summary>
        public List<string> FindUntranslatedChinese(IEnumerable<string> sources)
        {
            var result = new List<string>(); var seen = new HashSet<string>(StringComparer.Ordinal);
            if (sources == null) return result;
            foreach (string source in sources)
                if (!string.IsNullOrEmpty(source) && seen.Add(source) && ContainsChinese(Translate(source, GameLanguage.English))) result.Add(source);
            return result;
        }
    }
}
