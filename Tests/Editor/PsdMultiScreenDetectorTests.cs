#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;
using UnityEngine.TestTools;

namespace PsdReader.Tests
{
    public sealed class PsdMultiScreenDetectorTests
    {
        private readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = roots.Count - 1; i >= 0; i--)
            {
                if (roots[i] != null) Object.DestroyImmediate(roots[i]);
            }
            roots.Clear();
        }

        [Test]
        public void DetectsSiblingPanelRootsAsSeparateScreens()
        {
            var converter = CreateConverter();
            CreatePanel(converter.transform, "Settings");
            CreatePanel(converter.transform, "Mail");

            var candidates = PsdMultiScreenDetector.Detect(converter);

            Assert.That(candidates, Has.Count.EqualTo(2));
            Assert.That(candidates.TrueForAll(item => item.Selected), Is.True);
            Assert.That(candidates[0].OutputName, Does.EndWith("Panel"));
            Assert.That(candidates[1].OutputName, Does.EndWith("Panel"));
        }

        [Test]
        public void PrefersShallowScreenSetOverNestedPanels()
        {
            var converter = CreateConverter();
            var settings = CreatePanel(converter.transform, "Settings");
            CreatePanel(settings.transform, "HeaderPanel");
            CreatePanel(settings.transform, "ContentPanel");
            CreatePanel(settings.transform, "FooterPanel");
            CreatePanel(converter.transform, "Mail");

            var candidates = PsdMultiScreenDetector.Detect(converter);

            Assert.That(candidates, Has.Count.EqualTo(2));
            Assert.That(candidates.Exists(item => item.Node.name == "Settings"), Is.True);
            Assert.That(candidates.Exists(item => item.Node.name == "Mail"), Is.True);
            Assert.That(candidates.Exists(item => item.Node.name == "HeaderPanel"), Is.False);
        }

        [Test]
        public void DetectsScreensInsideSingleCollectionWrapper()
        {
            var converter = CreateConverter();
            var collection = CreatePanel(converter.transform, "UICollection");
            CreatePanel(collection.transform, "Login");
            CreatePanel(collection.transform, "Home");

            var candidates = PsdMultiScreenDetector.Detect(converter);

            Assert.That(candidates, Has.Count.EqualTo(2));
            Assert.That(candidates.Exists(item => item.Node.name == "Login"), Is.True);
            Assert.That(candidates.Exists(item => item.Node.name == "Home"), Is.True);
        }

        [Test]
        public void DetectsHiddenSiblingScreenRoots()
        {
            var converter = CreateConverter();
            var visible = CreatePanel(converter.transform, "MailEmpty");
            var hidden = CreatePanel(converter.transform, "MailPopulated");
            visible.gameObject.SetActive(true);
            hidden.gameObject.SetActive(false);

            var candidates = PsdMultiScreenDetector.Detect(converter);

            Assert.That(candidates, Has.Count.EqualTo(2));
            Assert.That(candidates.Exists(item => item.Node == hidden), Is.True);
            Assert.That(candidates.TrueForAll(item => item.Selected), Is.True);
        }

        [TestCase("Settings", "SettingsPanel")]
        [TestCase("SettingsPanel", "SettingsPanel")]
        [TestCase("", "ScreenPanel")]
        public void NormalizesIndependentPrefabNames(string input, string expected)
        {
            Assert.That(PsdMultiScreenDetector.NormalizeOutputName(input), Is.EqualTo(expected));
        }

        [Test]
        public void ConverterKeepsBatchSafeMultiScreenApiContract()
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            var converterType = typeof(Psd2UIFormConverter);

            Assert.That(converterType.GetMethod(
                "ParsePsd2LayerPrefab",
                flags,
                null,
                new[] { typeof(string), typeof(Psd2UIFormConverter), typeof(bool) },
                null), Is.Not.Null);
            Assert.That(converterType.GetMethod(
                "TryResolveUIFormOutputDirectory",
                flags,
                null,
                new[] { typeof(string).MakeByRefType() },
                null), Is.Not.Null);
            Assert.That(converterType.GetMethod(
                "GenerateUIFormToDirectory",
                flags,
                null,
                new[] { typeof(PsdLayerNode), typeof(string), typeof(string) },
                null), Is.Not.Null);
        }

        [Test]
        public void ExplicitDirectoryExportRejectsNodeFromAnotherPsd()
        {
            var converter = CreateConverter();
            var otherConverter = CreateConverter();
            var foreignNode = CreatePanel(otherConverter.transform, "ForeignScreen");
            LogAssert.Expect(
                LogType.Error,
                "生成UIForm失败: 节点不属于当前 PSD。node=ForeignScreen, psd=ConverterRoot");

            bool exported = converter.GenerateUIFormToDirectory(foreignNode, "ForeignPanel", "Assets");

            Assert.That(exported, Is.False);
        }

        private Psd2UIFormConverter CreateConverter()
        {
            var root = new GameObject("ConverterRoot");
            roots.Add(root);
            return root.AddComponent<Psd2UIFormConverter>();
        }

        private static PsdLayerNode CreatePanel(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var node = go.AddComponent<PsdLayerNode>();
            node.UIType = GUIType.Panel;
            var content = new GameObject("Content");
            content.transform.SetParent(go.transform, false);
            content.AddComponent<PsdLayerNode>().UIType = GUIType.Image;
            return node;
        }
    }
}
#endif
