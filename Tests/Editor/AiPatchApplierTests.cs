#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;

namespace PsdReader.Tests
{
    public sealed class AiPatchApplierTests
    {
        private readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = roots.Count - 1; i >= 0; i--)
            {
                if (roots[i] != null)
                {
                    Object.DestroyImmediate(roots[i]);
                }
            }
            roots.Clear();
        }

        [Test]
        public void ProtocolMainTypeValuesRemainCompatible()
        {
            Assert.That((int)GUIType.Panel, Is.EqualTo(17));
            Assert.That((int)GUIType.ToggleGroup, Is.EqualTo(18));
            Assert.That((int)GUIType.Background, Is.EqualTo(101));
        }

        [Test]
        public void InvalidPatchIsRejectedBeforeMutation()
        {
            var converter = CreateConverter();
            var node = CreateNode(converter.transform, "Target", 1, GUIType.Image);
            var patch = BuildSetTypePatch("psd:1", GUIType.Button);
            patch.version = "invalid";

            bool applied = new AiPatchApplier().Apply(converter, patch, out var error);

            Assert.That(applied, Is.False);
            Assert.That(error, Does.Contain("校验失败"));
            Assert.That(node.UIType, Is.EqualTo(GUIType.Image));
        }

        [Test]
        public void ManualTypeLockPreventsAiTypeChange()
        {
            var converter = CreateConverter();
            var node = CreateNode(converter.transform, "Target", 2, GUIType.Image);
            node.ManualUITypeOverride = true;

            bool applied = new AiPatchApplier().Apply(
                converter,
                BuildSetTypePatch("psd:2", GUIType.Button),
                out var error);

            Assert.That(applied, Is.True, error);
            Assert.That(node.UIType, Is.EqualTo(GUIType.Image));
            Assert.That(node.ManualUITypeOverride, Is.True);
        }

        [Test]
        public void ReferenceNodePreventsAiRename()
        {
            var converter = CreateConverter();
            var node = CreateNode(converter.transform, "ref SharedIcon", 3, GUIType.Image);
            var patch = BuildPatch(
                "psd:3",
                GUIType.Image,
                GUIType.Image,
                new AiPatchOperation
                {
                    op = AiPatchOperationNames.RenameNode,
                    targetId = "psd:3",
                    name = "RenamedByAi",
                    confidence = 0.95f,
                    reason = "test reference protection"
                });

            bool applied = new AiPatchApplier().Apply(converter, patch, out var error);

            Assert.That(applied, Is.True, error);
            Assert.That(node.name, Is.EqualTo("ref SharedIcon"));
        }

        [Test]
        public void GeneratedGroupCanBeCreatedWithStableId()
        {
            var converter = CreateConverter();
            var patch = BuildPatch(
                "root",
                GUIType.Null,
                GUIType.Panel,
                new AiPatchOperation
                {
                    op = AiPatchOperationNames.CreateGroup,
                    id = "gen:test-group",
                    parentId = "root",
                    name = "AiGroup",
                    uiType = GUIType.Panel.ToString(),
                    insertIndex = 0,
                    confidence = 0.95f,
                    reason = "test generated group"
                });

            bool applied = new AiPatchApplier().Apply(converter, patch, out var error);

            Assert.That(applied, Is.True, error);
            var group = converter.transform.Find("AiGroup");
            Assert.That(group, Is.Not.Null);
            var node = group.GetComponent<PsdLayerNode>();
            Assert.That(node, Is.Not.Null);
            Assert.That(node.GeneratedNodeId, Is.EqualTo("gen:test-group"));
            Assert.That(node.BindPsdLayerIndex, Is.EqualTo(-1));
            Assert.That(node.UIType, Is.EqualTo(GUIType.Panel));
        }

        [Test]
        public void ProceduralContainerTypesGenerateWithoutParserPrefabs()
        {
            var converter = CreateConverter();
            var panelNode = CreateNode(converter.transform, "Panel", 10, GUIType.Panel);
            panelNode.RefreshUIHelper();
            var panelHelper = panelNode.GetComponent<PanelHelper>();
            Assert.That(panelHelper, Is.Not.Null);
            var panel = panelHelper.CreateUI();
            roots.Add(panel);
            Assert.That(panel.GetComponent<RectTransform>(), Is.Not.Null);

            var toggleGroupNode = CreateNode(converter.transform, "ToggleGroup", 11, GUIType.ToggleGroup);
            toggleGroupNode.RefreshUIHelper();
            var toggleGroupHelper = toggleGroupNode.GetComponent<ToggleGroupHelper>();
            Assert.That(toggleGroupHelper, Is.Not.Null);
            var toggleGroup = toggleGroupHelper.CreateUI();
            roots.Add(toggleGroup);
            Assert.That(toggleGroup.GetComponent<UnityEngine.UI.ToggleGroup>(), Is.Not.Null);
        }

        [Test]
        public void CollapsedCompositeNodeSuppressesDescendantGeneration()
        {
            var converter = CreateConverter();
            var compositeObject = new GameObject("CompositeImage");
            compositeObject.transform.SetParent(converter.transform, false);
            var composite = compositeObject.AddComponent<PsdLayerNode>();
            composite.ConfigureGeneratedGroupNode("CompositeImage", new Rect(0, 0, 100, 100), "gen:composite");
            composite.SetUIType(GUIType.Image);
            composite.SetGroupGenerationState(true, -1);

            var child = CreateNode(composite.transform, "ChildImage", 12, GUIType.Image);
            child.RefreshUIHelper();

            var getHelpers = typeof(Psd2UIFormConverter).GetMethod(
                "GetAvailableUIHelpers",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(getHelpers, Is.Not.Null);
            var helpers = (UIHelperBase[])getHelpers.Invoke(converter, new object[] { converter.transform });

            Assert.That(helpers, Has.Length.EqualTo(1));
            Assert.That(helpers[0].LayerNode, Is.SameAs(composite));
        }

        [Test]
        public void AiTypeChangeClearsStaleCollapseAndPreservesContainerChildren()
        {
            var converter = CreateConverter();
            var wrapperObject = new GameObject("VisualWrapper");
            wrapperObject.transform.SetParent(converter.transform, false);
            var wrapper = wrapperObject.AddComponent<PsdLayerNode>();
            wrapper.ConfigureGeneratedGroupNode("VisualWrapper", new Rect(0, 0, 100, 100), "gen:visual-wrapper");
            wrapper.SetUIType(GUIType.Image);
            wrapper.SetGroupGenerationState(true, -1);

            var child = CreateNode(wrapper.transform, "ChildImage", 13, GUIType.Image);
            child.RefreshUIHelper();

            bool applied = new AiPatchApplier().Apply(
                converter,
                BuildSetTypePatch("gen:visual-wrapper", GUIType.Null),
                out var error);

            Assert.That(applied, Is.True, error);
            Assert.That(wrapper.UIType, Is.EqualTo(GUIType.Null));
            Assert.That(wrapper.CollapseChildrenForGeneration, Is.False);

            var helpers = GetAvailableUIHelpers(converter);
            Assert.That(helpers.Select(helper => helper.LayerNode), Does.Contain(child));
        }

        [Test]
        public void StaleCollapseOnNullContainerDoesNotSuppressDescendantGeneration()
        {
            var converter = CreateConverter();
            var wrapperObject = new GameObject("NullWrapper");
            wrapperObject.transform.SetParent(converter.transform, false);
            var wrapper = wrapperObject.AddComponent<PsdLayerNode>();
            wrapper.ConfigureGeneratedGroupNode("NullWrapper", new Rect(0, 0, 100, 100), "gen:null-wrapper");
            wrapper.SetUIType(GUIType.Null);
            wrapper.SetGroupGenerationState(true, -1);

            var child = CreateNode(wrapper.transform, "ChildImage", 14, GUIType.Image);
            child.RefreshUIHelper();

            var helpers = GetAvailableUIHelpers(converter);

            Assert.That(helpers.Select(helper => helper.LayerNode), Does.Contain(child));
        }

        [Test]
        public void AiApplyRestoresVisualOrderFromSourceLayerIndices()
        {
            var converter = CreateConverter();

            var contentObject = new GameObject("ContentGroup");
            contentObject.transform.SetParent(converter.transform, false);
            var contentGroup = contentObject.AddComponent<PsdLayerNode>();
            contentGroup.ConfigureGeneratedGroupNode("ContentGroup", Rect.zero, "gen:content");
            contentGroup.BindPsdLayerIndex = 71;
            contentGroup.SetUIType(GUIType.Null);
            CreateNode(contentObject.transform, "Content", 11, GUIType.Image);

            var backgroundObject = new GameObject("BackgroundGroup");
            backgroundObject.transform.SetParent(converter.transform, false);
            var backgroundGroup = backgroundObject.AddComponent<PsdLayerNode>();
            backgroundGroup.ConfigureGeneratedGroupNode("BackgroundGroup", Rect.zero, "gen:background");
            backgroundGroup.BindPsdLayerIndex = 10;
            backgroundGroup.SetUIType(GUIType.Null);
            CreateNode(backgroundObject.transform, "Background", 5, GUIType.Image);

            var auditTarget = CreateNode(converter.transform, "AuditTarget", 99, GUIType.Image);
            bool applied = new AiPatchApplier().Apply(
                converter,
                BuildSetTypePatch("psd:99", GUIType.Image),
                out var error);

            Assert.That(applied, Is.True, error);
            Assert.That(backgroundObject.transform.GetSiblingIndex(), Is.LessThan(contentObject.transform.GetSiblingIndex()));
            Assert.That(auditTarget.transform.GetSiblingIndex(), Is.GreaterThan(contentObject.transform.GetSiblingIndex()));
        }

        private static UIHelperBase[] GetAvailableUIHelpers(Psd2UIFormConverter converter)
        {
            var getHelpers = typeof(Psd2UIFormConverter).GetMethod(
                "GetAvailableUIHelpers",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(getHelpers, Is.Not.Null);
            return (UIHelperBase[])getHelpers.Invoke(converter, new object[] { converter.transform });
        }

        private Psd2UIFormConverter CreateConverter()
        {
            var root = new GameObject("ConverterRoot");
            roots.Add(root);
            return root.AddComponent<Psd2UIFormConverter>();
        }

        private static PsdLayerNode CreateNode(Transform parent, string name, int layerIndex, GUIType uiType)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var node = go.AddComponent<PsdLayerNode>();
            node.BindPsdLayerIndex = layerIndex;
            node.UIType = uiType;
            return node;
        }

        private static AiPatchDocument BuildSetTypePatch(string targetId, GUIType predictedType)
        {
            return BuildPatch(
                targetId,
                GUIType.Image,
                predictedType,
                new AiPatchOperation
                {
                    op = AiPatchOperationNames.SetUIType,
                    targetId = targetId,
                    uiType = predictedType.ToString(),
                    confidence = 0.95f,
                    reason = "test type change"
                });
        }

        private static AiPatchDocument BuildPatch(
            string targetId,
            GUIType currentType,
            GUIType predictedType,
            AiPatchOperation operation)
        {
            return new AiPatchDocument
            {
                version = AiProtocolVersions.Patch,
                providerId = "test",
                jobId = "test-job",
                treeHash = "test-tree",
                analysis = new List<AiAuditEntry>
                {
                    new AiAuditEntry
                    {
                        targetId = targetId,
                        ownerId = string.Empty,
                        semanticKind = "owner",
                        currentUIType = currentType.ToString(),
                        predictedUIType = predictedType.ToString(),
                        verdict = "corrected",
                        confidence = 0.95f,
                        reason = "test audit"
                    }
                },
                operations = new List<AiPatchOperation> { operation }
            };
        }
    }
}
#endif
