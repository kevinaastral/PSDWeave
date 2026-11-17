#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PsdReader.Tests
{
    public sealed class PsdPrefabComponentizeBatchTests
    {
        private const string ComponentizerTypeName =
            "UGF.EditorTools.Psd2UGUI.Smartexport.Editor.PsdPrefabComponentizeBatch";
        private string tempFolder;
        private string tempPrefix;

        [SetUp]
        public void SetUp()
        {
            var folderName = $"PsdPrefabComponentizeBatchTests_{Guid.NewGuid():N}";
            AssetDatabase.CreateFolder("Assets", folderName);
            tempFolder = $"Assets/{folderName}";
            tempPrefix = $"{tempFolder}/Fixture";
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(tempFolder))
                AssetDatabase.DeleteAsset(tempFolder);
        }

        [Test]
        public void ControlLinksAssignSelectableAndToggleReferences()
        {
            var sourcePath = $"{tempPrefix}_Source.prefab";
            var targetPath = $"{tempPrefix}_Target.prefab";
            var planPath = $"{tempPrefix}_Plan.json";
            CreateControlsPrefab(sourcePath);
            WritePlan(
                planPath,
                sourcePath,
                targetPath,
                "  \"controlLinks\": [\n" +
                "    {\n" +
                "      \"controlPath\": \"TabControl\",\n" +
                "      \"targetGraphicPath\": \".\",\n" +
                "      \"toggleGraphicPath\": \"TabControl/Checkmark\",\n" +
                "      \"toggleGroupPath\": \"TabGroup\",\n" +
                "      \"setToggleIsOn\": true,\n" +
                "      \"toggleIsOn\": true\n" +
                "    },\n" +
                "    {\n" +
                "      \"controlPath\": \"ActionControl\",\n" +
                "      \"targetGraphicPath\": \"ActionControl/Visual\",\n" +
                "      \"required\": true\n" +
                "    }\n" +
                "  ]");

            InvokeComponentizer(planPath);

            var targetRoot = PrefabUtility.LoadPrefabContents(targetPath);
            try
            {
                var tab = targetRoot.transform.Find("TabControl");
                var toggle = tab.GetComponent<Toggle>();
                var tabGraphic = tab.GetComponent<Image>();
                var checkmark = tab.Find("Checkmark").GetComponent<Image>();
                var group = targetRoot.transform.Find("TabGroup").GetComponent<ToggleGroup>();
                var button = targetRoot.transform.Find("ActionControl").GetComponent<Button>();
                var buttonGraphic = targetRoot.transform.Find("ActionControl/Visual").GetComponent<Image>();

                Assert.That(toggle.targetGraphic, Is.SameAs(tabGraphic));
                Assert.That(toggle.graphic, Is.SameAs(checkmark));
                Assert.That(toggle.group, Is.SameAs(group));
                Assert.That(toggle.isOn, Is.True);
                Assert.That(button.targetGraphic, Is.SameAs(buttonGraphic));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(targetRoot);
            }
        }

        [Test]
        public void NodeMakeGraphicTransparentPreservesRaycastConfiguration()
        {
            var sourcePath = $"{tempPrefix}_Source.prefab";
            var targetPath = $"{tempPrefix}_Target.prefab";
            var planPath = $"{tempPrefix}_Plan.json";
            CreateGraphicPrefab(sourcePath);
            WritePlan(
                planPath,
                sourcePath,
                targetPath,
                "  \"strictRequiredNodes\": true,\n" +
                "  \"nodes\": [\n" +
                "    {\n" +
                "      \"path\": \"HitArea\",\n" +
                "      \"makeGraphicTransparent\": true,\n" +
                "      \"required\": true\n" +
                "    }\n" +
                "  ]");

            InvokeComponentizer(planPath);

            var targetRoot = PrefabUtility.LoadPrefabContents(targetPath);
            try
            {
                var graphic = targetRoot.transform.Find("HitArea").GetComponent<Image>();
                Assert.That(graphic.color.r, Is.EqualTo(0.2f).Within(0.001f));
                Assert.That(graphic.color.g, Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(graphic.color.b, Is.EqualTo(0.6f).Within(0.001f));
                Assert.That(graphic.color.a, Is.Zero.Within(0.001f));
                Assert.That(graphic.raycastTarget, Is.False);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(targetRoot);
            }
        }

        [Test]
        public void ReferenceVisualCopiesApplyExternalSpritesAndReplaceTmpWithImage()
        {
            var sourcePath = $"{tempPrefix}_Source.prefab";
            var targetPath = $"{tempPrefix}_Target.prefab";
            var planPath = $"{tempPrefix}_Plan.json";
            var titleSpritePath = CreateSpriteAsset("Title", 8, 4);
            var iconSpritePath = CreateSpriteAsset("Icon", 4, 8);
            CreateSpriteOverridePrefab(sourcePath, titleSpritePath);
            WritePlan(
                planPath,
                sourcePath,
                targetPath,
                "  \"referenceVisualCopies\": [\n" +
                "    {\n" +
                "      \"targetPath\": \"Title\",\n" +
                $"      \"spriteAssetPath\": \"{titleSpritePath}\",\n" +
                "      \"replaceTextWithImage\": true,\n" +
                "      \"setNativeSize\": true,\n" +
                "      \"setRaycastTarget\": true,\n" +
                "      \"raycastTarget\": false\n" +
                "    },\n" +
                "    {\n" +
                "      \"targetPath\": \"Icon\",\n" +
                $"      \"spriteAssetPath\": \"{iconSpritePath}\"\n" +
                "    }\n" +
                "  ]");

            InvokeComponentizer(planPath);

            var targetRoot = PrefabUtility.LoadPrefabContents(targetPath);
            try
            {
                var title = targetRoot.transform.Find("Title");
                var titleRect = (RectTransform)title;
                var titleImage = title.GetComponent<Image>();
                var expectedTitleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(titleSpritePath);
                var iconImage = targetRoot.transform.Find("Icon").GetComponent<Image>();
                var expectedIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconSpritePath);

                Assert.That(title.GetComponent(FindTextMeshProType()), Is.Null);
                Assert.That(titleImage, Is.Not.Null);
                Assert.That(titleImage.sprite, Is.SameAs(expectedTitleSprite));
                Assert.That(titleImage.raycastTarget, Is.False);
                Assert.That(titleRect.anchoredPosition, Is.EqualTo(new Vector2(12f, -7f)));
                Assert.That(titleRect.sizeDelta.x, Is.EqualTo(expectedTitleSprite.rect.width).Within(0.001f));
                Assert.That(titleRect.sizeDelta.y, Is.EqualTo(expectedTitleSprite.rect.height).Within(0.001f));

                Assert.That(iconImage.sprite, Is.SameAs(expectedIconSprite));
                Assert.That(iconImage.color.r, Is.EqualTo(0.2f).Within(0.001f));
                Assert.That(iconImage.type, Is.EqualTo(Image.Type.Filled));
                Assert.That(iconImage.raycastTarget, Is.False);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(targetRoot);
            }
        }

        [Test]
        public void RemovingAnAlreadyMissingNodeIsIdempotent()
        {
            var sourcePath = $"{tempPrefix}_Source.prefab";
            var targetPath = $"{tempPrefix}_Target.prefab";
            var planPath = $"{tempPrefix}_Plan.json";
            CreateGraphicPrefab(sourcePath);
            WritePlan(
                planPath,
                sourcePath,
                targetPath,
                "  \"strictRequiredNodes\": true,\n" +
                "  \"nodes\": [\n" +
                "    {\n" +
                "      \"path\": \"AlreadyRemovedHelper\",\n" +
                "      \"removeNode\": true,\n" +
                "      \"required\": true\n" +
                "    }\n" +
                "  ]");

            Assert.DoesNotThrow(() => InvokeComponentizer(planPath));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(targetPath), Is.Not.Null);
        }

        private static void CreateControlsPrefab(string assetPath)
        {
            var root = new GameObject("SourcePanel", typeof(RectTransform));
            try
            {
                var groupObject = CreateRect("TabGroup", root.transform);
                groupObject.AddComponent<ToggleGroup>();

                var tabObject = CreateRect("TabControl", root.transform);
                var tabGraphic = tabObject.AddComponent<Image>();
                var toggle = tabObject.AddComponent<Toggle>();
                toggle.targetGraphic = null;
                toggle.graphic = null;
                toggle.group = null;
                toggle.SetIsOnWithoutNotify(false);
                tabGraphic.raycastTarget = true;

                var checkmarkObject = CreateRect("Checkmark", tabObject.transform);
                checkmarkObject.AddComponent<Image>();

                var actionObject = CreateRect("ActionControl", root.transform);
                var button = actionObject.AddComponent<Button>();
                button.targetGraphic = null;
                var visualObject = CreateRect("Visual", actionObject.transform);
                visualObject.AddComponent<Image>();

                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateGraphicPrefab(string assetPath)
        {
            var root = new GameObject("SourcePanel", typeof(RectTransform));
            try
            {
                var hitArea = CreateRect("HitArea", root.transform);
                var image = hitArea.AddComponent<Image>();
                image.color = new Color(0.2f, 0.4f, 0.6f, 0.8f);
                image.raycastTarget = false;
                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private string CreateSpriteAsset(string name, int width, int height)
        {
            var assetPath = $"{tempFolder}/{name}.png";
            var texture = new Texture2D(width, height);
            try
            {
                texture.SetPixels(Enumerable.Repeat(Color.white, width * height).ToArray());
                texture.Apply();
                File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
            return assetPath;
        }

        private static void CreateSpriteOverridePrefab(string assetPath, string titleSpritePath)
        {
            var root = new GameObject("SourcePanel", typeof(RectTransform));
            try
            {
                var title = CreateRect("Title", root.transform);
                var titleRect = title.GetComponent<RectTransform>();
                titleRect.anchoredPosition = new Vector2(12f, -7f);
                titleRect.sizeDelta = new Vector2(188f, 109f);
                var titleText = title.AddComponent(FindTextMeshProType());
                titleText.GetType().GetProperty("text")?.SetValue(titleText, "Title");

                var icon = CreateRect("Icon", root.transform);
                var iconImage = icon.AddComponent<Image>();
                var initialSprite = AssetDatabase.LoadAssetAtPath<Sprite>(titleSpritePath);
                iconImage.sprite = initialSprite;
                iconImage.overrideSprite = initialSprite;
                iconImage.color = new Color(0.2f, 0.4f, 0.6f, 0.8f);
                iconImage.type = Image.Type.Filled;
                iconImage.raycastTarget = false;

                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Type FindTextMeshProType()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("TMPro.TextMeshProUGUI", false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, "Could not find TMPro.TextMeshProUGUI.");
            return type;
        }

        private static void WritePlan(
            string planPath,
            string sourcePath,
            string targetPath,
            string additionalProperties)
        {
            var json = "{\n" +
                       $"  \"sourcePrefabPath\": \"{sourcePath}\",\n" +
                       $"  \"targetPrefabPath\": \"{targetPath}\",\n" +
                       "  \"preserveRootRectTransform\": true,\n" +
                       additionalProperties + "\n" +
                       "}";
            File.WriteAllText(planPath, json);
            AssetDatabase.ImportAsset(planPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void InvokeComponentizer(string planPath)
        {
            var componentizerType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(ComponentizerTypeName, false))
                .FirstOrDefault(type => type != null);
            Assert.That(componentizerType, Is.Not.Null, $"Could not find {ComponentizerTypeName}.");

            var runMethod = componentizerType.GetMethod(
                "Run",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string[]) },
                null);
            Assert.That(runMethod, Is.Not.Null, "Could not find PsdPrefabComponentizeBatch.Run(string[]). ");
            runMethod.Invoke(null, new object[] { new[] { planPath } });
        }
    }
}
#endif
