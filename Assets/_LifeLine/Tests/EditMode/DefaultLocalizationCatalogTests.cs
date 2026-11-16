using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Lifeline.Editor;

namespace Lifeline.Tests
{
    public sealed class DefaultLocalizationCatalogTests
    {
        LocalizationTable table;
        LocalizationTranslator translator;

        [SetUp]
        public void SetUp()
        {
            table = ScriptableObject.CreateInstance<LocalizationTable>();
            table.entries = DefaultLocalizationBuilder.CreateDefaultEntries();
            translator = new LocalizationTranslator(table);
        }

        [TearDown]
        public void TearDown() { if (table != null) UnityEngine.Object.DestroyImmediate(table); }

        [Test]
        public void DefaultCopyHasStableUniqueKeysAndCompleteEnglishIncludingStory()
        {
            var keys = new HashSet<string>();
            foreach (LocalizationTable.Entry entry in table.entries)
            {
                Assert.That(entry.key, Is.Not.Empty);
                Assert.That(keys.Add(entry.key), Is.True, "Duplicate localization key: " + entry.key);
                Assert.That(entry.zh, Is.Not.Empty, entry.key);
                Assert.That(entry.en, Is.Not.Empty, entry.key);
                Assert.That(LocalizationTranslator.ContainsChinese(entry.en), Is.False, entry.key);
                Assert.That(LocalizationTranslator.ContainsChinese(translator.Translate(entry.zh, GameLanguage.English)), Is.False, entry.key);
            }
            Assert.That(translator.Translate("story.radio.antenna", GameLanguage.English), Does.Contain("children"));
            Assert.That(translator.Translate("ending.victory", GameLanguage.English), Does.Contain("survivors"));
            Assert.That(translator.Translate("settings.language.system", GameLanguage.English), Does.Contain("System"));
        }

        [Test]
        public void DynamicHudCraftingRepairBuffDeathAndQuestCopyLeavesNoChineseInEnglish()
        {
            string[] samples =
            {
                "船体 73/100   电力 18%   航线 12%",
                "DAY 3 · 夜晚  145s",
                "潜水员 · 水下  O₂ 24s  背包 8/10  绳长 18.3/54m",
                "船长 · 扶起同伴 2.5/5s · 持续按住互动",
                "木板开始自动制作，离开工作台仍会继续。",
                "已取消铁板，已投入材料不予返还。",
                "校准成功 +1.78m",
                "校准失败 −0.89m",
                "故障 3 清理 75%",
                "故障 2：冷却阀",
                "入口第2行，出口第4行。逐格顺时针旋转后试水。",
                "电源 A ✓",
                "潜水员扶起了船长。",
                "品质鱼已食用：船长潜水员背包容量增加1格，持续120秒。",
                "品质鱼已食用：潜水员潜水员氧气上限增加10%，持续120秒。",
                "潜水服承伤次数耗尽，潜水员昏厥。缓慢拖向船底，仍需船长用绞盘接回；一半背包物资留在原处。",
                "深渊巨影横扫（1/2）。用鱼叉反击或投放诱饵。",
                "手摇发电 +1%",
                "救援航程：向东航行至信号浮标（42/85m）",
                "等待第4日救援频道，先修船备物资；当前第2日",
                "船长接回潜水员，8件物资已卸入共享仓库。",
                "普通作业 35秒；快速修补用时减半。校准成功加进度，离开仍继续，不返还已投入材料。",
                "无线电·放大器 · 65%\n下个音符 [W] · 0.13s  指针进入绿色区域时按键",
                "电力 34% · 燃油 / 手摇双模式\n指针进入绿色区域时发力 / 当前 0.80",
            };
            Assert.That(translator.FindUntranslatedChinese(samples), Is.Empty);
            string ship = translator.Translate(samples[0], GameLanguage.English);
            Assert.That(ship, Does.Contain("73/100").And.Contain("18%").And.Contain("12%"));
            Assert.That(translator.Translate("WOOD", GameLanguage.Chinese), Is.EqualTo("木材"));
            Assert.That(translator.Translate("Move/up", GameLanguage.Chinese), Is.EqualTo("移动/上"));
        }

        [Test]
        public void RuntimeChineseDisplayLiteralsAreCoveredByDefaultCatalog()
        {
            string root = Path.Combine(Application.dataPath, "_LifeLine", "Scripts", "Runtime");
            Assert.That(Directory.Exists(root), Is.True, "Expected imported Runtime source at " + root);
            var sources = new HashSet<string>();
            var literals = new Regex(@"""(?:\\.|[^""\\])*""");
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Localization/")) continue;
                foreach (string line in File.ReadAllLines(file))
                {
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal) || Regex.IsMatch(line, @"\[(?:Header|Tooltip|TextArea)\(")) continue;
                    foreach (Match match in literals.Matches(line))
                    {
                        string source;
                        try { source = Regex.Unescape(match.Value.Substring(1, match.Value.Length - 2)); }
                        catch (ArgumentException) { continue; }
                        if (LocalizationTranslator.ContainsChinese(source)) sources.Add(source);
                    }
                }
            }
            var missing = translator.FindUntranslatedChinese(sources);
            Assert.That(missing, Is.Empty, "Untranslated runtime display sources:\n" + string.Join("\n", missing));
        }
    }
}
