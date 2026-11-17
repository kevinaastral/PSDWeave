#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;

namespace PsdReader.Tests
{
    public sealed class AiRecognitionInputBuilderTests
    {
        private string tempRoot;

        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "PSD2UIFormRecognitionInputTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(tempRoot) && Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }

        [Test]
        public void BuildWritesPowerShell51CompatibleUtf8BomJson()
        {
            var context = new AiJobContext
            {
                RecognitionInputManifestPath = Path.Combine(tempRoot, "recognition-input", "manifest.json"),
                RecognitionNodeShardDirectory = Path.Combine(tempRoot, "recognition-input", "nodes")
            };
            var package = new AiAnalysisPackageDocument
            {
                treeHash = "test-tree-hash",
                document = new AiAnalysisDocumentInfo
                {
                    previewImagePath = "preview.png",
                    annotatedPreviewImagePath = "annotated-preview.png",
                    nodePreviewDirectoryPath = "node-previews",
                    nodeAtlasDirectoryPath = "node-atlas",
                    width = 1080,
                    height = 2400
                }
            };
            package.config = new AiAnalysisConfigInfo();
            package.config.uiTypeRules.Add(new AiAnalysisUiTypeRuleInfo
            {
                uiType = "Null",
                uiTypeDesc = "空节点",
                typeMatches = Array.Empty<string>()
            });
            package.nodes.Add(new AiAnalysisNodeEntry
            {
                id = "psd:1",
                shortId = "n001",
                parentId = "root",
                childIds = Array.Empty<string>(),
                onlyChildId = string.Empty,
                idPath = "n001",
                displayPath = "下拉框",
                name = "下拉框",
                layerName = "下拉框",
                nameTokens = new[] { "下拉框" },
                layerType = "LayerGroup",
                isGroupLayer = true,
                uiType = "Null",
                rect = new RectData { x = 0, y = 0, w = 100, h = 100 },
                previewKind = "composite",
                siblingIndex = 0,
                childCount = 0
            });

            bool success = new AiRecognitionInputBuilder().Build(context, package, tempRoot, out string error);

            Assert.That(success, Is.True, error);
            string[] jsonPaths = Directory.GetFiles(
                Path.Combine(tempRoot, "recognition-input"),
                "*.json",
                SearchOption.AllDirectories);
            Assert.That(jsonPaths.Length, Is.EqualTo(2));
            Assert.That(jsonPaths.Any(path => path.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(jsonPaths.Any(path => path.EndsWith("nodes-000.json", StringComparison.OrdinalIgnoreCase)), Is.True);

            for (int i = 0; i < jsonPaths.Length; i++)
            {
                AssertPowerShell51CompatibleUtf8(jsonPaths[i]);
            }
        }

        private static void AssertPowerShell51CompatibleUtf8(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(bytes.Length, Is.GreaterThanOrEqualTo(3), path);
            Assert.That(bytes[0], Is.EqualTo(0xEF), path);
            Assert.That(bytes[1], Is.EqualTo(0xBB), path);
            Assert.That(bytes[2], Is.EqualTo(0xBF), path);

            string text;
            using (var reader = new StreamReader(path, Encoding.Default, true))
            {
                text = reader.ReadToEnd();
            }

            Assert.That(text.StartsWith("\uFEFF", StringComparison.Ordinal), Is.False, path);
            if (path.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                var manifest = JsonUtility.FromJson<AiRecognitionInputManifestDocument>(text);
                Assert.That(manifest, Is.Not.Null, path);
                Assert.That(manifest.config.uiTypeRules[0].uiTypeDesc, Is.EqualTo("空节点"), path);
            }
            else
            {
                var shard = JsonUtility.FromJson<AiRecognitionNodeShardDocument>(text);
                Assert.That(shard, Is.Not.Null, path);
                Assert.That(shard.nodes[0].layerName, Is.EqualTo("下拉框"), path);
            }
        }
    }
}
#endif
