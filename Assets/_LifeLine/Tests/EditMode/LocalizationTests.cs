using System.Collections.Generic;
using Lifeline.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lifeline.Tests
{
    public sealed class LocalizationTests
    {
        private readonly List<Object> created = new List<Object>();
        private T Keep<T>(T item) where T : Object { created.Add(item); return item; }
        [TearDown] public void TearDown()
        {
            foreach (var item in created) if (item != null) Object.DestroyImmediate(item);
            created.Clear();
        }
        private LocalizationTable Table()
        {
            var table = Keep(ScriptableObject.CreateInstance<LocalizationTable>());
            table.entries = new List<LocalizationTable.Entry>
            {
                new LocalizationTable.Entry("hull", "船体 {0}/{1}", "Hull {0}/{1}"),
                new LocalizationTable.Entry("made", "{0}制作了{1}，数量{2}", "{0} crafted {2} × {1}"),
                new LocalizationTable.Entry("captain", "船长", "Captain"),
                new LocalizationTable.Entry("plank", "木板", "Plank"),
                new LocalizationTable.Entry("fiber", "纤维", "Fiber"),
                new LocalizationTable.Entry("bundle", "纤维束", "Fiber bundle"),
                new LocalizationTable.Entry("day", "天", "day"),
                new LocalizationTable.Entry("opaque", "这是完整句。", "This is a complete sentence.") { fragment = false }
            };
            return table;
        }

        [TestCase(SystemLanguage.Chinese, GameLanguage.Chinese)]
        [TestCase(SystemLanguage.ChineseSimplified, GameLanguage.Chinese)]
        [TestCase(SystemLanguage.ChineseTraditional, GameLanguage.Chinese)]
        [TestCase(SystemLanguage.Japanese, GameLanguage.English)]
        [TestCase(SystemLanguage.English, GameLanguage.English)]
        public void SystemLanguageDefaultsToChineseVariantsAndOtherwiseEnglish(SystemLanguage system, GameLanguage expected)
        {
            Assert.That(LocalizationManager.ResolveLanguage(GameLanguage.System, system), Is.EqualTo(expected));
            Assert.That(LocalizationManager.ResolveLanguage(GameLanguage.English, system), Is.EqualTo(GameLanguage.English));
            Assert.That(LocalizationManager.ResolveLanguage(GameLanguage.Chinese, system), Is.EqualTo(GameLanguage.Chinese));
        }

        [Test]
        public void TemplatesReorderValuesTranslateCapturedItemsAndPreserveNumericFormatting()
        {
            var translator = new LocalizationTranslator(Table());
            Assert.That(translator.Translate("船体 87.5/100", GameLanguage.English), Is.EqualTo("Hull 87.5/100"));
            Assert.That(translator.Translate("船长制作了木板，数量3", GameLanguage.English), Is.EqualTo("Captain crafted 3 × Plank"));
            Assert.That(translator.Translate("Captain crafted 3 × Plank", GameLanguage.Chinese), Is.EqualTo("船长制作了木板，数量3"));
            Assert.That(translator.Format("made", GameLanguage.English, "船长", "木板", 3), Is.EqualTo("Captain crafted 3 × Plank"));
            Assert.That(translator.Translate("未知状态 42", GameLanguage.English), Is.EqualTo("未知状态 42"));
        }

        [Test]
        public void FragmentsUseLongestSourceOnceRespectEnglishWordBoundariesAndKeepMarkup()
        {
            var translator = new LocalizationTranslator(Table());
            Assert.That(translator.Translate("<color=red>纤维束 ×2</color> / 纤维", GameLanguage.English), Is.EqualTo("<color=red>Fiber bundle ×2</color> / Fiber"));
            Assert.That(translator.Translate("today day 3", GameLanguage.Chinese), Is.EqualTo("today 天 3"));
            Assert.That(translator.Translate("这是完整句。 更多", GameLanguage.English), Is.EqualTo("这是完整句。 更多"));
            Assert.That(translator.Translate("这是完整句。", GameLanguage.English), Is.EqualTo("This is a complete sentence."));
            Assert.That(translator.FindUntranslatedChinese(new[] { "船体 1/2", "未知状态42", "未知状态42" }), Is.EqualTo(new[] { "未知状态42" }));
        }

        [Test]
        public void DisplayBinderRetainsOriginalSourceAcrossLanguagesAndExternalDynamicWrites()
        {
            var root = Keep(new GameObject("LocalizedDisplay"));
            var manager = root.AddComponent<LocalizationManager>(); manager.table = Table(); manager.languageOverride = GameLanguage.English;
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)); labelObject.transform.SetParent(root.transform);
            var label = labelObject.GetComponent<Text>(); label.text = "船体 40/100";
            var worldObject = new GameObject("WorldLabel", typeof(TextMesh)); worldObject.transform.SetParent(root.transform);
            var world = worldObject.GetComponent<TextMesh>(); world.text = "纤维束 ×2";
            var binder = root.AddComponent<LocalizationTextBinder>(); binder.RebuildTargets(); binder.Refresh(manager);
            Assert.That(label.text, Is.EqualTo("Hull 40/100")); Assert.That(world.text, Is.EqualTo("Fiber bundle ×2"));
            Assert.That(binder.GetSource(label), Is.EqualTo("船体 40/100"));
            label.text = "船体 39/100"; binder.Refresh(manager);
            Assert.That(label.text, Is.EqualTo("Hull 39/100")); Assert.That(binder.GetSource(label), Is.EqualTo("船体 39/100"));
            manager.languageOverride = GameLanguage.Chinese; binder.Refresh(manager);
            Assert.That(label.text, Is.EqualTo("船体 39/100")); Assert.That(world.text, Is.EqualTo("纤维束 ×2"));
            manager.languageOverride = GameLanguage.English; binder.Refresh(manager);
            Assert.That(label.text, Is.EqualTo("Hull 39/100"));
        }

        [Test]
        public void DigitBindingMigrationPreservesGuidsCustomPathsAndOverridesAndIsIdempotent()
        {
            var asset = Keep(CrewInputDefaults.CreateAsset());
            var collect = asset.FindAction("Diver/Collect"); var fire = asset.FindAction("Diver/Fire");
            Assert.That(collect.bindings[0].path, Is.EqualTo("<Keyboard>/1"));
            Assert.That(fire.bindings[0].path, Is.EqualTo("<Keyboard>/2"));
            collect.ChangeBinding(0).WithPath("<Keyboard>/digit1");
            fire.ChangeBinding(0).WithPath("<Keyboard>/j");
            collect.ApplyBindingOverride(0, "<Keyboard>/h"); var id = collect.bindings[0].id;
            Assert.That(InputAssetBuilder.MigrateLegacyDigitBindings(asset, false).Count, Is.EqualTo(1));
            Assert.That(collect.bindings[0].path, Is.EqualTo("<Keyboard>/digit1"));
            InputAssetBuilder.MigrateLegacyDigitBindings(asset, true);
            Assert.That(collect.bindings[0].path, Is.EqualTo("<Keyboard>/1"));
            Assert.That(collect.bindings[0].id, Is.EqualTo(id)); Assert.That(collect.bindings[0].overridePath, Is.EqualTo("<Keyboard>/h"));
            Assert.That(fire.bindings[0].path, Is.EqualTo("<Keyboard>/j"));
            Assert.That(InputAssetBuilder.MigrateLegacyDigitBindings(asset, true), Is.Empty);
        }
    }
}
