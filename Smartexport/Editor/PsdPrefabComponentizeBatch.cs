#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UGF.EditorTools.Psd2UGUI.Smartexport;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI.Smartexport.Editor
{
    public static class PsdPrefabComponentizeBatch
    {
        public static void RunFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            Run(SplitArg(GetArg(args, "-planPaths")));
        }

        public static void Run(string[] planPaths)
        {
            if (planPaths == null || planPaths.Length == 0)
                throw new ArgumentException("Missing componentization plan paths.");

            foreach (var planPath in planPaths)
                ApplyPlan(planPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[PsdPrefabComponentizeBatch] Completed.");
        }

        private static void ApplyPlan(string planPath)
        {
            if (string.IsNullOrWhiteSpace(planPath))
                throw new ArgumentException("Componentization plan path is empty.");
            if (!File.Exists(planPath))
                throw new FileNotFoundException($"Componentization plan not found: {planPath}");

            var json = File.ReadAllText(planPath);
            var plan = JsonUtility.FromJson<ComponentizationPlan>(json);
            if (plan == null)
                throw new InvalidOperationException($"Invalid componentization plan: {planPath}");
            if (string.IsNullOrWhiteSpace(plan.sourcePrefabPath))
                throw new ArgumentException($"Missing sourcePrefabPath in plan: {planPath}");
            if (string.IsNullOrWhiteSpace(plan.targetPrefabPath))
                throw new ArgumentException($"Missing targetPrefabPath in plan: {planPath}");

            var sourcePath = NormalizeAssetPath(plan.sourcePrefabPath);
            var targetPath = NormalizeAssetPath(plan.targetPrefabPath);
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException($"Source prefab not found: {sourcePath}");

            var sourceRoot = PrefabUtility.LoadPrefabContents(sourcePath);
            GameObject root = null;
            try
            {
                root = CreateWorkingRoot(sourceRoot, plan.sourceRootIndexPath);
                if (!string.IsNullOrWhiteSpace(plan.rootName))
                    root.name = plan.rootName;

                SetupRoot(root, plan);
                RemoveRootComponents(root, plan.removeRootComponentTypeNames);

                var resolvedNodes = (plan.nodes ?? Array.Empty<NodePlan>())
                    .Select(node => new ResolvedNodePlan(node, ResolveNode(root.transform, node)))
                    .ToArray();
                var referenceResolution = new Vector2(
                    plan.referenceWidth > 0f ? plan.referenceWidth : 1080f,
                    plan.referenceHeight > 0f ? plan.referenceHeight : 2400f);
                var resolvedContainers = ResolveContainerPlans(root.transform, plan);
                var containerLookup = CreateContainers(root.transform, resolvedContainers, plan.strictRequiredNodes);
                ReparentContainerMembers(resolvedContainers, plan.strictRequiredNodes);

                foreach (var resolvedNode in resolvedNodes)
                    ApplyNodePlan(resolvedNode, plan.strictRequiredNodes, referenceResolution);

                ApplyReferenceCopies(root.transform, plan, containerLookup, referenceResolution);
                ApplyReferenceVisualCopies(root.transform, plan);
                ApplyControlLinks(root.transform, plan);

                ValidateBindComponents(root, plan.strictRequiredNodes);

                var targetDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(targetDir))
                    Directory.CreateDirectory(targetDir);

                PrefabUtility.SaveAsPrefabAsset(root, targetPath, out var success);
                if (!success)
                    throw new InvalidOperationException($"Save componentized prefab failed: {targetPath}");

                Debug.Log($"[PsdPrefabComponentizeBatch] Saved: {targetPath}");
            }
            finally
            {
                if (root != null && root != sourceRoot)
                    UnityEngine.Object.DestroyImmediate(root);
                PrefabUtility.UnloadPrefabContents(sourceRoot);
            }
        }

        private static GameObject CreateWorkingRoot(GameObject sourceRoot, int[] sourceRootIndexPath)
        {
            if (sourceRoot == null)
                throw new ArgumentNullException(nameof(sourceRoot));
            if (sourceRootIndexPath == null || sourceRootIndexPath.Length == 0)
                return sourceRoot;

            var sourceTransform = ResolveIndexPath(sourceRoot.transform, sourceRootIndexPath);
            if (sourceTransform == null)
            {
                throw new InvalidOperationException(
                    $"Source root not found in '{sourceRoot.name}': " +
                    $"sourceRootIndexPath=[{string.Join(",", sourceRootIndexPath)}]");
            }

            var clone = UnityEngine.Object.Instantiate(sourceTransform.gameObject);
            clone.name = sourceTransform.name;
            return clone;
        }

        private static void SetupRoot(GameObject root, ComponentizationPlan plan)
        {
            var rect = root.GetComponent<RectTransform>();
            if (rect == null)
                rect = root.AddComponent<RectTransform>();

            if (!plan.preserveRootRectTransform)
                StretchToParent(rect);
            rect.localScale = Vector3.one;
            rect.localEulerAngles = Vector3.zero;

            if (root.GetComponent<CanvasGroup>() == null)
                root.AddComponent<CanvasGroup>();
            if (root.GetComponent<Canvas>() == null)
                root.AddComponent<Canvas>();
            if (root.GetComponent<GraphicRaycaster>() == null)
                root.AddComponent<GraphicRaycaster>();
        }

        private static void ApplyNodePlan(
            ResolvedNodePlan resolvedNode,
            bool strictRequiredNodes,
            Vector2 referenceResolution)
        {
            var node = resolvedNode.Node;
            var transform = resolvedNode.Transform;
            if (transform == null)
            {
                // Removing an already-absent helper is a successful no-op. This keeps plans
                // replayable after the first componentization pass has deleted temporary layers.
                if (node.removeNode)
                    return;

                HandlePlanProblem(
                    $"Node not found: {DescribeNode(node)}",
                    strictRequiredNodes || node.required);
                return;
            }

            var go = transform.gameObject;
            if (node.removeNode)
            {
                if (transform.parent == null)
                {
                    throw new InvalidOperationException(
                        $"Node '{GetHierarchyPath(transform)}' cannot be removed because it is the prefab root.");
                }

                UnityEngine.Object.DestroyImmediate(go, true);
                return;
            }

            var suffix = NormalizeSuffix(node.bindSuffix);
            if (!string.IsNullOrWhiteSpace(node.newName))
                go.name = WithSuffix(node.newName, suffix);
            else if (!string.IsNullOrEmpty(suffix))
                go.name = WithSuffix(go.name, suffix);

            if (node.removeButton)
            {
                var button = go.GetComponent<Button>();
                if (button != null)
                    UnityEngine.Object.DestroyImmediate(button, true);
            }

            if (transform is RectTransform rect)
            {
                if (node.fitRectToChildren)
                    FitRectToDirectChildren(rect);

                if (node.stretchToParent && !string.IsNullOrWhiteSpace(node.anchorPreset))
                {
                    HandlePlanProblem(
                        $"Node '{go.name}' sets both stretchToParent and anchorPreset; stretchToParent wins.",
                        strictRequiredNodes || node.required);
                }
                else if (!string.IsNullOrWhiteSpace(node.anchorPreset))
                {
                    ApplyAnchorPresetPreserveVisual(rect, node.anchorPreset, referenceResolution);
                }

                if (node.stretchToParent)
                    StretchToParent(rect);

                ApplyRectTransformOverrides(
                    rect,
                    node.setAnchoredPosition,
                    node.anchoredPosition,
                    node.setSizeDelta,
                    node.sizeDelta,
                    node.setAnchorMinMax,
                    node.anchorMin,
                    node.anchorMax,
                    node.setPivot,
                    node.pivot);
            }
            else if (HasRectTransformOverrides(
                         node.setAnchoredPosition,
                         node.setSizeDelta,
                         node.setAnchorMinMax,
                         node.setPivot))
            {
                HandlePlanProblem(
                    $"Node '{go.name}' requested RectTransform overrides but is not a RectTransform.",
                    strictRequiredNodes || node.required);
            }

            if (node.setActive)
                go.SetActive(node.activeSelf);

            EnsureBindComponent(go, suffix, node.addTransparentGraphic);

            if (node.makeGraphicTransparent)
                MakeGraphicsTransparent(go);

            if (node.setRaycastTarget)
            {
                var graphic = go.GetComponent<Graphic>();
                if (graphic == null)
                {
                    HandlePlanProblem(
                        $"Node '{go.name}' requested raycastTarget={node.raycastTarget} but has no Graphic component.",
                        strictRequiredNodes || node.required);
                }
                else
                {
                    graphic.raycastTarget = node.raycastTarget;
                }
            }

            if (node.shouldBind || !string.IsNullOrEmpty(suffix))
                ApplyBindingMetadata(go, suffix, node.describe);

            if (!string.IsNullOrEmpty(suffix))
                ValidateBindComponent(go, suffix, strictRequiredNodes || node.required, DescribeNode(node));
        }

        private static ResolvedContainerPlan[] ResolveContainerPlans(Transform root, ComponentizationPlan plan)
        {
            return (plan.containers ?? Array.Empty<ContainerPlan>())
                .Select(container => new ResolvedContainerPlan(
                    container,
                    (container.members ?? Array.Empty<NodeSelector>())
                    .Select(member => new ResolvedNodeSelector(member, ResolveNode(root, member)))
                    .ToArray()))
                .ToArray();
        }

        private static Dictionary<string, Transform> CreateContainers(
            Transform root,
            ResolvedContainerPlan[] resolvedContainers,
            bool strictRequiredNodes)
        {
            var lookup = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            foreach (var resolved in resolvedContainers)
            {
                var container = resolved.Plan;
                if (string.IsNullOrWhiteSpace(container.name))
                    throw new ArgumentException("Container name is required.");

                var suffix = NormalizeSuffix(container.bindSuffix);
                var finalName = WithSuffix(container.name.Trim(), suffix);
                if (lookup.ContainsKey(finalName) || lookup.ContainsKey(StripSuffix(finalName)))
                    throw new InvalidOperationException($"Duplicate container name: {finalName}");

                var go = new GameObject(finalName, typeof(RectTransform));
                go.layer = root.gameObject.layer;
                var rect = (RectTransform)go.transform;
                rect.SetParent(root, false);
                rect.localScale = Vector3.one;
                rect.localEulerAngles = Vector3.zero;

                if (container.stretchToParent)
                    StretchToParent(rect);

                ApplyRectTransformOverrides(
                    rect,
                    container.setAnchoredPosition,
                    container.anchoredPosition,
                    container.setSizeDelta,
                    container.sizeDelta,
                    container.setAnchorMinMax,
                    container.anchorMin,
                    container.anchorMax,
                    container.setPivot,
                    container.pivot);

                if (container.siblingIndex >= 0)
                    rect.SetSiblingIndex(Mathf.Clamp(container.siblingIndex, 0, Math.Max(0, root.childCount - 1)));

                go.SetActive(container.activeSelf);
                if (!string.IsNullOrEmpty(suffix))
                {
                    EnsureBindComponent(
                        go,
                        suffix,
                        string.Equals(suffix, "@Btn", StringComparison.Ordinal));
                    ApplyBindingMetadata(go, suffix, $"container:{container.name}");
                    ValidateBindComponent(go, suffix, strictRequiredNodes, $"container:{container.name}");
                }

                resolved.Transform = rect;
                RegisterContainerLookup(lookup, container.name, rect);
                RegisterContainerLookup(lookup, finalName, rect);
                RegisterContainerLookup(lookup, StripSuffix(finalName), rect);
            }

            return lookup;
        }

        private static void RegisterContainerLookup(Dictionary<string, Transform> lookup, string key, Transform value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            if (lookup.TryGetValue(key, out var existing) && existing != value)
                throw new InvalidOperationException($"Container lookup name is ambiguous: {key}");
            lookup[key] = value;
        }

        private static void ReparentContainerMembers(
            ResolvedContainerPlan[] resolvedContainers,
            bool strictRequiredNodes)
        {
            var assigned = new HashSet<Transform>();
            foreach (var resolved in resolvedContainers)
            {
                if (resolved.Transform == null)
                    continue;

                foreach (var member in resolved.Members)
                {
                    if (member.Transform == null)
                    {
                        HandlePlanProblem(
                            $"Container '{resolved.Plan.name}' member not found: {DescribeSelector(member.Selector)}",
                            strictRequiredNodes);
                        continue;
                    }

                    if (member.Transform == resolved.Transform || resolved.Transform.IsChildOf(member.Transform))
                    {
                        HandlePlanProblem(
                            $"Container '{resolved.Plan.name}' cannot adopt itself or one of its ancestors: {member.Transform.name}",
                            true);
                        continue;
                    }

                    if (!assigned.Add(member.Transform))
                    {
                        HandlePlanProblem(
                            $"Node '{member.Transform.name}' is assigned to more than one container.",
                            strictRequiredNodes);
                    }

                    member.Transform.SetParent(resolved.Transform, true);
                }
            }
        }

        private static void ApplyReferenceCopies(
            Transform root,
            ComponentizationPlan plan,
            Dictionary<string, Transform> containerLookup,
            Vector2 referenceResolution)
        {
            var copies = plan.referenceCopies ?? Array.Empty<ReferenceCopyPlan>();
            if (copies.Length == 0)
                return;
            if (string.IsNullOrWhiteSpace(plan.referencePrefabPath))
                throw new ArgumentException("referencePrefabPath is required when referenceCopies are present.");

            var referencePath = NormalizeAssetPath(plan.referencePrefabPath);
            if (!File.Exists(referencePath))
                throw new FileNotFoundException($"Reference prefab not found: {referencePath}");

            var referenceRoot = PrefabUtility.LoadPrefabContents(referencePath);
            try
            {
                for (var copyIndex = 0; copyIndex < copies.Length; copyIndex++)
                {
                    var copy = copies[copyIndex];
                    if (copy == null)
                        throw new InvalidOperationException($"referenceCopies[{copyIndex}] is null.");

                    var sourceDescription = DescribeReferenceSource(copy);
                    if (sourceDescription == null)
                    {
                        throw new ArgumentException(
                            $"referenceCopies[{copyIndex}] must set sourceIndexPath or sourcePath.");
                    }

                    var source = ResolveReferenceCopySource(referenceRoot.transform, copy);
                    if (source == null)
                    {
                        throw new InvalidOperationException(
                            $"Reference source not found in '{referencePath}': {sourceDescription}");
                    }

                    var target = ResolveReferenceCopyTarget(root, copy, containerLookup);
                    if (target == null)
                    {
                        throw new InvalidOperationException(
                            $"Reference target not found: container='{copy.targetContainer}', name='{copy.targetName}'");
                    }

                    var suffix = NormalizeSuffix(copy.bindSuffix);
                    var baseName = string.IsNullOrWhiteSpace(copy.newName) ? source.name : copy.newName.Trim();
                    var finalName = WithSuffix(baseName, suffix);
                    if (copy.removeExistingTargetChildren)
                    {
                        var existingChildren = Enumerable.Range(0, target.childCount)
                            .Select(target.GetChild)
                            .Where(child => string.Equals(child.name, finalName, StringComparison.Ordinal))
                            .ToArray();
                        foreach (var existingChild in existingChildren)
                            UnityEngine.Object.DestroyImmediate(existingChild.gameObject);
                    }

                    var clone = UnityEngine.Object.Instantiate(source.gameObject, target, false);
                    SetLayerRecursively(clone, root.gameObject.layer);
                    if (!copy.preserveGeneratedBindingComponents)
                        StripGeneratedBindingComponents(clone);
                    if (copy.removeChildren)
                    {
                        for (var childIndex = clone.transform.childCount - 1; childIndex >= 0; childIndex--)
                            UnityEngine.Object.DestroyImmediate(clone.transform.GetChild(childIndex).gameObject);
                    }
                    if (copy.makeGraphicTransparent)
                    {
                        foreach (var graphic in clone.GetComponents<Graphic>())
                        {
                            var color = graphic.color;
                            color.a = 0f;
                            graphic.color = color;
                            graphic.raycastTarget = true;
                        }
                    }

                    clone.name = finalName;

                    if (clone.transform is RectTransform rect)
                    {
                        if (copy.stretchToParent && !string.IsNullOrWhiteSpace(copy.anchorPreset))
                        {
                            HandlePlanProblem(
                                $"Reference copy '{sourceDescription}' sets both stretchToParent and anchorPreset; stretchToParent wins.",
                                plan.strictRequiredNodes);
                        }
                        else if (!string.IsNullOrWhiteSpace(copy.anchorPreset))
                        {
                            ApplyAnchorPresetPreserveVisual(rect, copy.anchorPreset, referenceResolution);
                        }

                        if (copy.stretchToParent)
                            StretchToParent(rect);

                        ApplyRectTransformOverrides(
                            rect,
                            copy.setAnchoredPosition,
                            copy.anchoredPosition,
                            copy.setSizeDelta,
                            copy.sizeDelta,
                            copy.setAnchorMinMax,
                            copy.anchorMin,
                            copy.anchorMax,
                            copy.setPivot,
                            copy.pivot);
                    }
                    else if (HasRectTransformOverrides(
                                 copy.setAnchoredPosition,
                                 copy.setSizeDelta,
                                 copy.setAnchorMinMax,
                                 copy.setPivot))
                    {
                        HandlePlanProblem(
                            $"Reference copy '{sourceDescription}' requested RectTransform overrides " +
                            "but the copied object is not a RectTransform.",
                            plan.strictRequiredNodes);
                    }
                    if (copy.setActive)
                        clone.SetActive(copy.activeSelf);
                    if (copy.siblingIndex >= 0)
                    {
                        clone.transform.SetSiblingIndex(
                            Mathf.Clamp(copy.siblingIndex, 0, Math.Max(0, target.childCount - 1)));
                    }

                    if (!string.IsNullOrEmpty(suffix))
                    {
                        EnsureBindComponent(clone, suffix, copy.addTransparentGraphic);
                        ApplyBindingMetadata(clone, suffix, $"reference:{sourceDescription}");
                        ValidateBindComponent(
                            clone,
                            suffix,
                            plan.strictRequiredNodes,
                            $"reference:{sourceDescription}");
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(referenceRoot);
            }
        }

        private static void ApplyReferenceVisualCopies(Transform root, ComponentizationPlan plan)
        {
            var copies = plan.referenceVisualCopies ?? Array.Empty<ReferenceVisualCopyPlan>();
            if (copies.Length == 0)
                return;

            var needsReferencePrefab = copies.Any(RequiresReferenceVisualSource);
            var referencePath = string.Empty;
            GameObject referenceRoot = null;
            if (needsReferencePrefab)
            {
                if (string.IsNullOrWhiteSpace(plan.referencePrefabPath))
                    throw new ArgumentException(
                        "referencePrefabPath is required when referenceVisualCopies copy from a reference prefab.");

                referencePath = NormalizeAssetPath(plan.referencePrefabPath);
                if (!File.Exists(referencePath))
                    throw new FileNotFoundException($"Reference prefab not found: {referencePath}");

                referenceRoot = PrefabUtility.LoadPrefabContents(referencePath);
            }

            try
            {
                for (var copyIndex = 0; copyIndex < copies.Length; copyIndex++)
                {
                    var copy = copies[copyIndex];
                    if (copy == null)
                        throw new InvalidOperationException($"referenceVisualCopies[{copyIndex}] is null.");
                    if (string.IsNullOrWhiteSpace(copy.targetPath))
                        throw new ArgumentException($"referenceVisualCopies[{copyIndex}] must set targetPath.");

                    var target = ResolveTransformPath(root, copy.targetPath);
                    if (target == null)
                    {
                        throw new InvalidOperationException(
                            $"Reference visual target not found: targetPath='{copy.targetPath}'");
                    }

                    if (RequiresReferenceVisualSource(copy))
                    {
                        var source = ResolveReferenceVisualSource(referenceRoot.transform, copy);
                        if (source == null)
                        {
                            throw new InvalidOperationException(
                                $"Reference visual source not found in '{referencePath}': {DescribeReferenceVisualSource(copy)}");
                        }

                        if (copy.copyRectTransform)
                            CopyRectTransformVisual(source as RectTransform, target as RectTransform, copyIndex);
                        if (copy.copyImage)
                            CopyImageVisual(source.GetComponent<Image>(), target.GetComponent<Image>(), copyIndex);
                        if (copy.copyRawImage)
                            CopyRawImageVisual(source.GetComponent<RawImage>(), target.GetComponent<RawImage>(), copyIndex);
                        if (copy.copyText)
                            CopyTextVisual(source.GetComponent<TMPro.TextMeshProUGUI>(), target.GetComponent<TMPro.TextMeshProUGUI>(), copyIndex);
                        if (copy.copyActiveSelf)
                            target.gameObject.SetActive(source.gameObject.activeSelf);
                    }

                    ApplyExternalSpriteVisual(target, copy, copyIndex);

                    if (copy.setNativeSize)
                    {
                        var image = target.GetComponent<Image>();
                        if (image == null)
                        {
                            throw new InvalidOperationException(
                                $"referenceVisualCopies[{copyIndex}] requested setNativeSize but target " +
                                $"'{GetHierarchyPath(target)}' has no Image component.");
                        }

                        image.SetNativeSize();
                    }

                    if (copy.setRaycastTarget)
                    {
                        var graphic = target.GetComponent<Graphic>();
                        if (graphic == null)
                        {
                            throw new InvalidOperationException(
                                $"referenceVisualCopies[{copyIndex}] requested raycastTarget={copy.raycastTarget} " +
                                $"but target '{GetHierarchyPath(target)}' has no Graphic component.");
                        }

                        graphic.raycastTarget = copy.raycastTarget;
                    }

                    if (copy.setActive)
                        target.gameObject.SetActive(copy.activeSelf);
                }
            }
            finally
            {
                if (referenceRoot != null)
                    PrefabUtility.UnloadPrefabContents(referenceRoot);
            }
        }

        private static bool RequiresReferenceVisualSource(ReferenceVisualCopyPlan copy)
        {
            return copy.copyRectTransform
                   || copy.copyImage
                   || copy.copyRawImage
                   || copy.copyText
                   || copy.copyActiveSelf;
        }

        private static void ApplyExternalSpriteVisual(
            Transform target,
            ReferenceVisualCopyPlan copy,
            int copyIndex)
        {
            if (string.IsNullOrWhiteSpace(copy.spriteAssetPath))
            {
                if (copy.replaceTextWithImage)
                {
                    throw new ArgumentException(
                        $"referenceVisualCopies[{copyIndex}] replaceTextWithImage requires spriteAssetPath.");
                }

                return;
            }

            var spritePath = NormalizeAssetPath(copy.spriteAssetPath);
            if (!spritePath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"referenceVisualCopies[{copyIndex}] spriteAssetPath must be under Assets/: '{copy.spriteAssetPath}'.");
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
            {
                throw new InvalidOperationException(
                    $"referenceVisualCopies[{copyIndex}] Sprite asset not found: '{spritePath}'.");
            }

            var go = target.gameObject;
            var image = go.GetComponent<Image>();
            if (copy.replaceTextWithImage)
            {
                var text = go.GetComponent<TMPro.TextMeshProUGUI>();
                if (image == null && text == null)
                {
                    throw new InvalidOperationException(
                        $"referenceVisualCopies[{copyIndex}] target '{GetHierarchyPath(target)}' " +
                        "has neither TextMeshProUGUI nor Image for replaceTextWithImage.");
                }

                if (text != null)
                    UnityEngine.Object.DestroyImmediate(text, true);

                if (image == null)
                {
                    image = go.AddComponent<Image>();
                    image.color = Color.white;
                    image.material = null;
                    image.type = Image.Type.Simple;
                    image.preserveAspect = false;
                    image.raycastTarget = false;
                }
            }
            else if (image == null)
            {
                throw new InvalidOperationException(
                    $"referenceVisualCopies[{copyIndex}] target '{GetHierarchyPath(target)}' " +
                    "has no Image component for spriteAssetPath.");
            }

            image.sprite = sprite;
            image.overrideSprite = null;
        }

        private static void ApplyControlLinks(Transform root, ComponentizationPlan plan)
        {
            var links = plan.controlLinks ?? Array.Empty<ControlLinkPlan>();
            var toggleInitialStates = new List<KeyValuePair<Toggle, bool>>();
            for (var linkIndex = 0; linkIndex < links.Length; linkIndex++)
            {
                var link = links[linkIndex];
                if (link == null)
                    throw new InvalidOperationException($"controlLinks[{linkIndex}] is null.");

                var control = ResolveControlLinkPath(root, root, link.controlPath);
                if (control == null)
                {
                    HandlePlanProblem(
                        $"controlLinks[{linkIndex}] control not found: controlPath='{link.controlPath}'",
                        link.required);
                    continue;
                }

                if (link.targetGraphicPath != null)
                {
                    var selectable = control.GetComponent<Selectable>();
                    if (selectable == null)
                    {
                        HandlePlanProblem(
                            $"controlLinks[{linkIndex}] control '{GetHierarchyPath(control)}' has no Selectable component.",
                            link.required);
                    }
                    else if (TryResolveControlLinkComponent(
                                 root,
                                 control,
                                 link.targetGraphicPath,
                                 linkIndex,
                                 nameof(link.targetGraphicPath),
                                 link.required,
                                 out Graphic targetGraphic))
                    {
                        selectable.targetGraphic = targetGraphic;
                    }
                }

                var hasToggleSettings = link.toggleGraphicPath != null
                                        || link.toggleGroupPath != null
                                        || link.setToggleIsOn;
                if (!hasToggleSettings)
                    continue;

                var toggle = control.GetComponent<Toggle>();
                if (toggle == null)
                {
                    HandlePlanProblem(
                        $"controlLinks[{linkIndex}] control '{GetHierarchyPath(control)}' has no Toggle component.",
                        link.required);
                    continue;
                }

                if (link.toggleGraphicPath != null
                    && TryResolveControlLinkComponent(
                        root,
                        control,
                        link.toggleGraphicPath,
                        linkIndex,
                        nameof(link.toggleGraphicPath),
                        link.required,
                        out Graphic toggleGraphic))
                {
                    toggle.graphic = toggleGraphic;
                }

                if (link.toggleGroupPath != null
                    && TryResolveControlLinkComponent(
                        root,
                        control,
                        link.toggleGroupPath,
                        linkIndex,
                        nameof(link.toggleGroupPath),
                        link.required,
                        out ToggleGroup toggleGroup))
                {
                    toggle.group = toggleGroup;
                }

                if (link.setToggleIsOn)
                {
                    toggleInitialStates.Add(
                        new KeyValuePair<Toggle, bool>(toggle, link.toggleIsOn));
                }
            }

            // Assign every ToggleGroup first. Turning planned-off toggles off before the planned-on
            // toggle avoids source-prefab state and ToggleGroup.allowSwitchOff deciding the result.
            foreach (var state in toggleInitialStates.Where(item => !item.Value))
                state.Key.SetIsOnWithoutNotify(false);
            foreach (var state in toggleInitialStates.Where(item => item.Value))
                state.Key.SetIsOnWithoutNotify(true);
        }

        private static Transform ResolveControlLinkPath(Transform root, Transform control, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || string.Equals(path.Trim(), ".", StringComparison.Ordinal))
                return control;
            return ResolveTransformPath(root, path);
        }

        private static bool TryResolveControlLinkComponent<T>(
            Transform root,
            Transform control,
            string path,
            int linkIndex,
            string fieldName,
            bool required,
            out T component) where T : Component
        {
            component = null;
            var target = ResolveControlLinkPath(root, control, path);
            if (target == null)
            {
                HandlePlanProblem(
                    $"controlLinks[{linkIndex}] {fieldName} target not found: path='{path}'",
                    required);
                return false;
            }

            component = target.GetComponent<T>();
            if (component != null)
                return true;

            HandlePlanProblem(
                $"controlLinks[{linkIndex}] {fieldName} target '{GetHierarchyPath(target)}' " +
                $"has no {typeof(T).Name} component.",
                required);
            return false;
        }

        private static void MakeGraphicsTransparent(GameObject go)
        {
            foreach (var graphic in go.GetComponents<Graphic>())
            {
                var color = graphic.color;
                color.a = 0f;
                graphic.color = color;
            }
        }

        private static Transform ResolveReferenceVisualSource(Transform referenceRoot, ReferenceVisualCopyPlan copy)
        {
            if (copy.sourceIndexPath != null && copy.sourceIndexPath.Length > 0)
                return ResolveIndexPath(referenceRoot, copy.sourceIndexPath);
            return ResolveTransformPath(referenceRoot, copy.sourcePath);
        }

        private static string DescribeReferenceVisualSource(ReferenceVisualCopyPlan copy)
        {
            if (copy.sourceIndexPath != null && copy.sourceIndexPath.Length > 0)
                return $"sourceIndexPath=[{string.Join(",", copy.sourceIndexPath)}]";
            return $"sourcePath='{copy.sourcePath}'";
        }

        private static void CopyRectTransformVisual(
            RectTransform source,
            RectTransform target,
            int copyIndex)
        {
            if (source == null || target == null)
            {
                throw new InvalidOperationException(
                    $"referenceVisualCopies[{copyIndex}] requested RectTransform copy but source/target is missing it.");
            }

            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.pivot = source.pivot;
            target.localScale = source.localScale;
            target.localEulerAngles = source.localEulerAngles;
        }

        private static void CopyImageVisual(Image source, Image target, int copyIndex)
        {
            if (source == null || target == null)
            {
                throw new InvalidOperationException(
                    $"referenceVisualCopies[{copyIndex}] requested Image copy but source/target is missing it.");
            }

            target.sprite = source.sprite;
            target.overrideSprite = source.overrideSprite;
            target.color = source.color;
            target.material = source.material;
            target.type = source.type;
            target.preserveAspect = source.preserveAspect;
            target.fillCenter = source.fillCenter;
            target.fillMethod = source.fillMethod;
            target.fillAmount = source.fillAmount;
            target.fillClockwise = source.fillClockwise;
            target.fillOrigin = source.fillOrigin;
            target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            target.useSpriteMesh = source.useSpriteMesh;
        }

        private static void CopyRawImageVisual(RawImage source, RawImage target, int copyIndex)
        {
            if (source == null || target == null)
            {
                throw new InvalidOperationException(
                    $"referenceVisualCopies[{copyIndex}] requested RawImage copy but source/target is missing it.");
            }

            target.texture = source.texture;
            target.uvRect = source.uvRect;
            target.color = source.color;
            target.material = source.material;
        }

        private static void CopyTextVisual(
            TMPro.TextMeshProUGUI source,
            TMPro.TextMeshProUGUI target,
            int copyIndex)
        {
            if (source == null || target == null)
            {
                throw new InvalidOperationException(
                    $"referenceVisualCopies[{copyIndex}] requested TMP copy but source/target is missing it.");
            }

            target.text = source.text;
            target.font = source.font;
            target.fontSharedMaterial = source.fontSharedMaterial;
            target.fontStyle = source.fontStyle;
            target.fontSize = source.fontSize;
            target.enableAutoSizing = source.enableAutoSizing;
            target.fontSizeMin = source.fontSizeMin;
            target.fontSizeMax = source.fontSizeMax;
            target.color = source.color;
            target.alignment = source.alignment;
            target.enableWordWrapping = source.enableWordWrapping;
            target.overflowMode = source.overflowMode;
            target.characterSpacing = source.characterSpacing;
            target.wordSpacing = source.wordSpacing;
            target.lineSpacing = source.lineSpacing;
            target.paragraphSpacing = source.paragraphSpacing;
            target.richText = source.richText;
        }

        private static Transform ResolveReferenceCopyTarget(
            Transform root,
            ReferenceCopyPlan copy,
            Dictionary<string, Transform> containerLookup)
        {
            if (!string.IsNullOrWhiteSpace(copy.targetContainer)
                && containerLookup.TryGetValue(copy.targetContainer.Trim(), out var container))
            {
                return container;
            }

            if (string.IsNullOrWhiteSpace(copy.targetName))
                return null;

            return ResolveTransformPath(root, copy.targetName) ?? FindByName(root, copy.targetName.Trim(), 0);
        }

        private static Transform ResolveReferenceCopySource(Transform referenceRoot, ReferenceCopyPlan copy)
        {
            if (copy.sourceIndexPath != null && copy.sourceIndexPath.Length > 0)
                return ResolveIndexPath(referenceRoot, copy.sourceIndexPath);

            return ResolveTransformPath(referenceRoot, copy.sourcePath);
        }

        private static string DescribeReferenceSource(ReferenceCopyPlan copy)
        {
            if (copy.sourceIndexPath != null && copy.sourceIndexPath.Length > 0)
                return $"sourceIndexPath=[{string.Join(",", copy.sourceIndexPath)}]";
            if (!string.IsNullOrWhiteSpace(copy.sourcePath))
                return $"sourcePath='{copy.sourcePath}'";
            return null;
        }

        private static Transform ResolveTransformPath(Transform root, string path)
        {
            if (root == null || string.IsNullOrWhiteSpace(path))
                return null;

            var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrEmpty(item))
                .ToArray();
            if (parts.Length == 0)
                return null;

            var current = root;
            var startIndex = string.Equals(parts[0], root.name, StringComparison.Ordinal) ? 1 : 0;
            for (var i = startIndex; i < parts.Length; i++)
            {
                var part = parts[i];
                current = Enumerable.Range(0, current.childCount)
                    .Select(current.GetChild)
                    .FirstOrDefault(child => child.name == part);
                if (current == null)
                    return null;
            }

            return current;
        }

        private static void StripGeneratedBindingComponents(GameObject root)
        {
            DestroyComponentsInChildren<PsdLayerMarker>(root);
        }

        private static void RemoveRootComponents(GameObject root, string[] componentTypeNames)
        {
            var requestedTypeNames = (componentTypeNames ?? Array.Empty<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (requestedTypeNames.Length == 0)
                return;

            var rootComponents = root.GetComponents<Component>()
                .Where(component => component != null)
                .ToArray();
            var componentsToRemove = new List<Component>();
            foreach (var requestedTypeName in requestedTypeNames)
            {
                var matches = rootComponents
                    .Where(component =>
                    {
                        var type = component.GetType();
                        return string.Equals(type.FullName, requestedTypeName, StringComparison.Ordinal)
                               || string.Equals(type.Name, requestedTypeName, StringComparison.Ordinal);
                    })
                    .ToArray();
                if (matches.Length == 0)
                {
                    Debug.Log($"[PsdPrefabComponentizeBatch] Root component '{requestedTypeName}' " +
                              $"is already absent on '{root.name}', skip removal.");
                    continue;
                }

                if (matches.Any(component => component is Transform))
                {
                    throw new InvalidOperationException(
                        $"Root component type '{requestedTypeName}' on '{root.name}' cannot be removed because " +
                        "Transform and RectTransform are protected.");
                }

                foreach (var match in matches)
                {
                    if (!componentsToRemove.Contains(match))
                        componentsToRemove.Add(match);
                }
            }

            foreach (var component in componentsToRemove)
                UnityEngine.Object.DestroyImmediate(component, true);
        }

        private static void DestroyComponentsInChildren<T>(GameObject root) where T : Component
        {
            if (root == null)
                return;

            var components = root.GetComponentsInChildren<T>(true);
            for (var i = components.Length - 1; i >= 0; i--)
            {
                if (components[i] != null)
                    UnityEngine.Object.DestroyImmediate(components[i], true);
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
                return;

            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                transform.gameObject.layer = layer;
        }

        private static Transform ResolveNode(Transform root, NodePlan node)
        {
            if (node.indexPath != null && node.indexPath.Length > 0)
                return ResolveIndexPath(root, node.indexPath);

            if (!string.IsNullOrWhiteSpace(node.path))
                return ResolveTransformPath(root, node.path);

            if (!string.IsNullOrWhiteSpace(node.text))
                return FindByText(root, node.text, node.occurrence);

            if (!string.IsNullOrWhiteSpace(node.sourceName))
                return FindByName(root, node.sourceName, node.occurrence);

            return null;
        }

        private static Transform ResolveNode(Transform root, NodeSelector node)
        {
            if (node.indexPath != null && node.indexPath.Length > 0)
                return ResolveIndexPath(root, node.indexPath);

            if (!string.IsNullOrWhiteSpace(node.path))
                return ResolveTransformPath(root, node.path);

            if (!string.IsNullOrWhiteSpace(node.text))
                return FindByText(root, node.text, node.occurrence);

            if (!string.IsNullOrWhiteSpace(node.sourceName))
                return FindByName(root, node.sourceName, node.occurrence);

            return null;
        }

        private static Transform ResolveIndexPath(Transform root, int[] indexPath)
        {
            var current = root;
            foreach (var index in indexPath)
            {
                if (index < 0 || index >= current.childCount)
                    return null;
                current = current.GetChild(index);
            }

            return current;
        }

        private static Transform FindByName(Transform root, string sourceName, int occurrence)
        {
            var index = 0;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform == root || transform.name != sourceName)
                    continue;

                if (index == Math.Max(0, occurrence))
                    return transform;
                index++;
            }

            return null;
        }

        private static Transform FindByText(Transform root, string text, int occurrence)
        {
            var index = 0;
            foreach (var tmp in root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
            {
                if (IsPsd2UISplitTMPTextLayer(tmp.transform))
                    continue;

                if (tmp.text != text)
                    continue;

                if (index == Math.Max(0, occurrence))
                    return tmp.transform;
                index++;
            }

            return null;
        }

        private static bool IsPsd2UISplitTMPTextLayer(Transform transform)
        {
            if (transform == null)
                return false;

            var name = transform.name;
            return name.EndsWith("__PSD2UI_TMP_STROKE", StringComparison.Ordinal)
                || name.EndsWith("__PSD2UI_TMP_GLOW", StringComparison.Ordinal);
        }

        private static void EnsureBindComponent(GameObject go, string suffix, bool addTransparentGraphic)
        {
            if (string.IsNullOrEmpty(suffix))
                return;

            switch (suffix)
            {
                case "@Btn":
                {
                    var graphic = go.GetComponent<Graphic>();
                    if (graphic == null && addTransparentGraphic)
                    {
                        var image = go.AddComponent<Image>();
                        image.color = new Color(1f, 1f, 1f, 0f);
                        image.raycastTarget = true;
                        graphic = image;
                    }

                    if (graphic != null)
                        graphic.raycastTarget = true;

                    var button = go.GetComponent<Button>();
                    if (button == null)
                        button = go.AddComponent<Button>();
                    if (button.targetGraphic == null)
                        button.targetGraphic = graphic;
                    break;
                }
                case "@Image":
                    if (go.GetComponent<Image>() == null)
                        Debug.LogWarning($"[PsdPrefabComponentizeBatch] {go.name} is bound as Image but has no Image component.");
                    break;
                case "@RawImage":
                    if (go.GetComponent<RawImage>() == null)
                        Debug.LogWarning($"[PsdPrefabComponentizeBatch] {go.name} is bound as RawImage but has no RawImage component.");
                    break;
                case "@Txt":
                    if (go.GetComponent<TMPro.TextMeshProUGUI>() == null)
                        Debug.LogWarning($"[PsdPrefabComponentizeBatch] {go.name} is bound as Text but has no TextMeshProUGUI component.");
                    break;
                case "@Slider":
                    if (go.GetComponent<Slider>() == null)
                        go.AddComponent<Slider>();
                    break;
                case "@Toggle":
                {
                    var graphic = go.GetComponent<Graphic>();
                    if (graphic != null)
                        graphic.raycastTarget = true;

                    var toggle = go.GetComponent<Toggle>();
                    if (toggle == null)
                        toggle = go.AddComponent<Toggle>();
                    if (toggle.targetGraphic == null)
                        toggle.targetGraphic = graphic;
                    break;
                }
            }
        }

        private static void ApplyBindingMetadata(GameObject go, string suffix, string description)
        {
            var marker = go.GetComponent<PsdLayerMarker>();
            if (marker == null)
                marker = go.AddComponent<PsdLayerMarker>();

            marker.ShouldBind = true;
            marker.ManualBindingOverride = true;
            marker.OriginalName = string.IsNullOrWhiteSpace(marker.OriginalName) ? description : marker.OriginalName;
            marker.ProposedName = StripSuffix(go.name);
            marker.SuggestedProp = StripSuffix(go.name);
            marker.Kind = BindSuffixToKind(suffix);
            marker.Psd2UguiTag = BindSuffixToPsd2UguiTag(suffix);
            marker.Confidence = 1f;
        }

        private static void ValidateBindComponents(GameObject root, bool strict)
        {
            if (root == null)
                return;

            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (!TryGetBindSuffix(transform.name, out var suffix))
                    continue;
                ValidateBindComponent(transform.gameObject, suffix, strict, GetHierarchyPath(transform));
            }
        }

        private static void ValidateBindComponent(GameObject go, string suffix, bool strict, string context)
        {
            suffix = NormalizeSuffix(suffix);
            if (ComponentBindingRules.FindTarget(go, suffix) == null && !string.Equals(suffix, "@Obj", StringComparison.OrdinalIgnoreCase))
                HandlePlanProblem($"Bind suffix '{suffix}' has no matching component at {context}.", strict);
        }

        private static bool TryGetBindSuffix(string name, out string suffix)
        {
            return ComponentBindingRules.TryGetSuffix(name, out suffix);
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return "<null>";

            var names = new Stack<string>();
            for (var current = transform; current != null; current = current.parent)
                names.Push(current.name);
            return string.Join("/", names);
        }

        private static bool HasRectTransformOverrides(
            bool setAnchoredPosition,
            bool setSizeDelta,
            bool setAnchorMinMax,
            bool setPivot)
        {
            return setAnchoredPosition || setSizeDelta || setAnchorMinMax || setPivot;
        }

        private static void ApplyRectTransformOverrides(
            RectTransform rect,
            bool setAnchoredPosition,
            Vector2 anchoredPosition,
            bool setSizeDelta,
            Vector2 sizeDelta,
            bool setAnchorMinMax,
            Vector2 anchorMin,
            Vector2 anchorMax,
            bool setPivot,
            Vector2 pivot)
        {
            // Apply explicit values last so a plan can intentionally override anchorPreset/stretchToParent.
            if (setAnchorMinMax)
            {
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
            }

            if (setPivot)
                rect.pivot = pivot;
            if (setSizeDelta)
                rect.sizeDelta = sizeDelta;
            if (setAnchoredPosition)
                rect.anchoredPosition = anchoredPosition;
        }

        private static void ApplyAnchorPresetPreserveVisual(
            RectTransform rect,
            string preset,
            Vector2 referenceResolution)
        {
            if (!TryGetAnchorPreset(preset, out var anchor))
            {
                throw new ArgumentException(
                    $"Unknown anchorPreset '{preset}' on '{GetHierarchyPath(rect)}'. " +
                    "Supported values: Top, TopLeft, TopRight, Bottom, BottomLeft, BottomRight, Center.");
            }

            if (rect.anchorMin != rect.anchorMax)
            {
                throw new InvalidOperationException(
                    $"anchorPreset requires fixed source anchors on '{GetHierarchyPath(rect)}', " +
                    $"but found {rect.anchorMin}-{rect.anchorMax}.");
            }

            var parentRect = rect.parent as RectTransform;
            var parentSize = parentRect != null ? parentRect.rect.size : Vector2.zero;
            if (parentSize.x <= Mathf.Epsilon || parentSize.y <= Mathf.Epsilon)
                parentSize = referenceResolution;

            var parentPivot = parentRect != null ? parentRect.pivot : new Vector2(0.5f, 0.5f);
            var size = rect.rect.size;
            var oldAnchor = rect.anchorMin;
            var oldAnchorReference = Vector2.Scale(oldAnchor - parentPivot, parentSize);
            var oldPivotPosition = oldAnchorReference + rect.anchoredPosition;
            var bottomLeft = oldPivotPosition - Vector2.Scale(rect.pivot, size);
            var newPivotPosition = bottomLeft + Vector2.Scale(anchor, size);
            var newAnchorReference = Vector2.Scale(anchor - parentPivot, parentSize);

            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = newPivotPosition - newAnchorReference;
        }

        private static bool TryGetAnchorPreset(string preset, out Vector2 anchor)
        {
            switch (preset?.Trim().ToLowerInvariant())
            {
                case "top": anchor = new Vector2(0.5f, 1f); return true;
                case "topleft": anchor = new Vector2(0f, 1f); return true;
                case "topright": anchor = new Vector2(1f, 1f); return true;
                case "bottom": anchor = new Vector2(0.5f, 0f); return true;
                case "bottomleft": anchor = new Vector2(0f, 0f); return true;
                case "bottomright": anchor = new Vector2(1f, 0f); return true;
                case "center": anchor = new Vector2(0.5f, 0.5f); return true;
                default:
                    anchor = default;
                    return false;
            }
        }

        private static void FitRectToDirectChildren(RectTransform rect)
        {
            if (rect == null || !TryCalculateDirectChildrenBounds(rect, out var bounds))
                return;

            var size = bounds.size;
            if (size.x <= 0f || size.y <= 0f)
                return;

            var pivotLocalPosition = new Vector3(
                bounds.min.x + size.x * rect.pivot.x,
                bounds.min.y + size.y * rect.pivot.y,
                0f);
            var targetWorldPosition = rect.TransformPoint(pivotLocalPosition);
            var childPositions = CaptureDirectChildWorldPositions(rect);

            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            rect.position = targetWorldPosition;

            foreach (var item in childPositions)
            {
                if (item.Transform != null)
                    item.Transform.position = item.Position;
            }
        }

        private static bool TryCalculateDirectChildrenBounds(RectTransform rect, out Bounds bounds)
        {
            bounds = default;
            var corners = new Vector3[4];
            var hasBounds = false;
            for (var childIndex = 0; childIndex < rect.childCount; childIndex++)
            {
                if (!(rect.GetChild(childIndex) is RectTransform childRect))
                    continue;

                childRect.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var localCorner = rect.InverseTransformPoint(corner);
                    if (!hasBounds)
                    {
                        bounds = new Bounds(localCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(localCorner);
                    }
                }
            }

            return hasBounds;
        }

        private static DirectChildWorldPosition[] CaptureDirectChildWorldPositions(RectTransform rect)
        {
            var result = new DirectChildWorldPosition[rect.childCount];
            for (var i = 0; i < rect.childCount; i++)
            {
                var child = rect.GetChild(i);
                result[i] = new DirectChildWorldPosition
                {
                    Transform = child,
                    Position = child.position,
                };
            }

            return result;
        }

        private static void HandlePlanProblem(string message, bool strict)
        {
            if (strict)
                throw new InvalidOperationException($"[PsdPrefabComponentizeBatch] {message}");
            Debug.LogWarning($"[PsdPrefabComponentizeBatch] {message}");
        }

        private static string DescribeNode(NodePlan node)
        {
            if (!string.IsNullOrWhiteSpace(node.describe))
                return node.describe;
            return DescribeSelector(node.indexPath, node.path, node.sourceName, node.text, node.occurrence);
        }

        private static string DescribeSelector(NodeSelector selector)
        {
            return DescribeSelector(
                selector.indexPath,
                selector.path,
                selector.sourceName,
                selector.text,
                selector.occurrence);
        }

        private static string DescribeSelector(
            int[] indexPath,
            string path,
            string sourceName,
            string text,
            int occurrence)
        {
            if (indexPath != null && indexPath.Length > 0)
                return $"indexPath=[{string.Join(",", indexPath)}]";
            if (!string.IsNullOrWhiteSpace(path))
                return $"path='{path}'";
            if (!string.IsNullOrWhiteSpace(sourceName))
                return $"sourceName='{sourceName}', occurrence={occurrence}";
            if (!string.IsNullOrWhiteSpace(text))
                return $"text='{text}', occurrence={occurrence}";
            return "<empty selector>";
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static string WithSuffix(string name, string suffix)
        {
            if (string.IsNullOrEmpty(suffix) || name.EndsWith(suffix, StringComparison.Ordinal))
                return name;
            return StripSuffix(name) + suffix;
        }

        private static string StripSuffix(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var index = name.LastIndexOf('@');
            return index >= 0 ? name.Substring(0, index) : name;
        }

        private static string NormalizeSuffix(string suffix)
        {
            if (string.IsNullOrWhiteSpace(suffix))
                return string.Empty;

            suffix = suffix.Trim();
            return suffix.StartsWith("@", StringComparison.Ordinal) ? suffix : "@" + suffix;
        }

        private static string BindSuffixToKind(string suffix)
        {
            switch (suffix)
            {
                case "@Btn": return "button";
                case "@Txt": return "text";
                case "@Image": return "image";
                case "@RawImage": return "rawImage";
                case "@Slider": return "slider";
                case "@Toggle": return "toggle";
                case "@ScrollRect": return "scroll";
                default: return "object";
            }
        }

        private static string BindSuffixToPsd2UguiTag(string suffix)
        {
            switch (suffix)
            {
                case "@Btn": return "bt";
                case "@Txt": return "txt";
                case "@Image": return "img";
                case "@RawImage": return "rimg";
                case "@Slider": return "sld";
                case "@Toggle": return "tg";
                case "@ScrollRect": return "sv";
                case "@Rect": return "vpt";
                case "@Obj": return "obj";
                default: return null;
            }
        }

        private static string GetArg(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }

            return null;
        }

        private static string[] SplitArg(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Array.Empty<string>();

            return value
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
        }

        private static string NormalizeAssetPath(string path)
        {
            return path?.Replace('\\', '/');
        }

        [Serializable]
        private sealed class ComponentizationPlan
        {
            public string sourcePrefabPath;
            public string targetPrefabPath;
            public string referencePrefabPath;
            public string rootName;
            public float referenceWidth = 1080f;
            public float referenceHeight = 2400f;
            public bool strictRequiredNodes;
            public bool preserveRootRectTransform;
            public int[] sourceRootIndexPath;
            public string[] removeRootComponentTypeNames;
            public ContainerPlan[] containers;
            public ReferenceCopyPlan[] referenceCopies;
            public ReferenceVisualCopyPlan[] referenceVisualCopies;
            public ControlLinkPlan[] controlLinks;
            public NodePlan[] nodes;
        }

        [Serializable]
        private sealed class ContainerPlan
        {
            public string name;
            public string bindSuffix;
            public bool stretchToParent;
            public bool setAnchoredPosition;
            public Vector2 anchoredPosition;
            public bool setSizeDelta;
            public Vector2 sizeDelta;
            public bool setAnchorMinMax;
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public bool setPivot;
            public Vector2 pivot;
            public bool activeSelf = true;
            public int siblingIndex = -1;
            public NodeSelector[] members;
        }

        [Serializable]
        private sealed class ReferenceCopyPlan
        {
            public int[] sourceIndexPath;
            public string sourcePath;
            public string targetContainer;
            public string targetName;
            public string newName;
            public string bindSuffix;
            public bool stretchToParent;
            public string anchorPreset;
            public bool setAnchoredPosition;
            public Vector2 anchoredPosition;
            public bool setSizeDelta;
            public Vector2 sizeDelta;
            public bool setAnchorMinMax;
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public bool setPivot;
            public Vector2 pivot;
            public bool setActive;
            public bool activeSelf;
            public int siblingIndex = -1;
            public bool preserveGeneratedBindingComponents;
            public bool removeExistingTargetChildren;
            public bool removeChildren;
            public bool makeGraphicTransparent;
            public bool addTransparentGraphic;
        }

        [Serializable]
        private sealed class ReferenceVisualCopyPlan
        {
            public int[] sourceIndexPath;
            public string sourcePath;
            public string targetPath;
            public bool copyRectTransform;
            public bool copyImage;
            public bool copyRawImage;
            public bool copyText;
            public bool copyActiveSelf;
            public string spriteAssetPath;
            public bool replaceTextWithImage;
            public bool setNativeSize;
            public bool setRaycastTarget;
            public bool raycastTarget;
            public bool setActive;
            public bool activeSelf;
        }

        [Serializable]
        private sealed class ControlLinkPlan
        {
            public string controlPath;
            public string targetGraphicPath;
            public string toggleGraphicPath;
            public string toggleGroupPath;
            public bool setToggleIsOn;
            public bool toggleIsOn;
            public bool required = true;
        }

        [Serializable]
        private sealed class NodeSelector
        {
            public int[] indexPath;
            public string path;
            public string sourceName;
            public string text;
            public int occurrence;
        }

        [Serializable]
        private sealed class NodePlan
        {
            public string describe;
            public int[] indexPath;
            public string path;
            public string sourceName;
            public string text;
            public int occurrence;
            public string newName;
            public string bindSuffix;
            public bool shouldBind;
            public bool removeNode;
            public bool addTransparentGraphic;
            public bool makeGraphicTransparent;
            public bool stretchToParent;
            public bool required;
            public bool fitRectToChildren;
            public bool setActive;
            public bool activeSelf;
            public bool removeButton;
            public bool setRaycastTarget;
            public bool raycastTarget;
            public string anchorPreset;
            public bool setAnchoredPosition;
            public Vector2 anchoredPosition;
            public bool setSizeDelta;
            public Vector2 sizeDelta;
            public bool setAnchorMinMax;
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public bool setPivot;
            public Vector2 pivot;
        }

        private readonly struct ResolvedNodePlan
        {
            public readonly NodePlan Node;
            public readonly Transform Transform;

            public ResolvedNodePlan(NodePlan node, Transform transform)
            {
                Node = node;
                Transform = transform;
            }
        }

        private sealed class ResolvedContainerPlan
        {
            public readonly ContainerPlan Plan;
            public readonly ResolvedNodeSelector[] Members;
            public Transform Transform;

            public ResolvedContainerPlan(ContainerPlan plan, ResolvedNodeSelector[] members)
            {
                Plan = plan;
                Members = members;
            }
        }

        private readonly struct ResolvedNodeSelector
        {
            public readonly NodeSelector Selector;
            public readonly Transform Transform;

            public ResolvedNodeSelector(NodeSelector selector, Transform transform)
            {
                Selector = selector;
                Transform = transform;
            }
        }

        private struct DirectChildWorldPosition
        {
            public Transform Transform;
            public Vector3 Position;
        }
    }

}
#endif
