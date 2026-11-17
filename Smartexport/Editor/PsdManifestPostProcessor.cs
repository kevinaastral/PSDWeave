using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UGF.EditorTools.Psd2UGUI;
using UnityEditor;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI.Smartexport.Editor
{
    public sealed class PsdManifestPostProcessor : AssetPostprocessor
    {
        private static readonly HashSet<string> ProcessingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Regex GeneratedSafeLayerNameRegex =
            new Regex(@"^PsdLayer[-_ ](?<index>\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly string[][] CommonChineseNameTokens =
        {
            new[] { "圆角矩形", "yuanjiaojuxing" },
            new[] { "三角形", "sanjiaoxing" },
            new[] { "矩形", "juxing" },
            new[] { "拷贝", "kaobei" },
            new[] { "背景", "beijing" },
            new[] { "主界面", "zhujiemian" },
            new[] { "图层", "tuceng" },
            new[] { "组", "zu" },
            new[] { "金币", "jinbi" },
            new[] { "加", "jia" },
            new[] { "减", "jian" },
        };
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (importedAssets == null || importedAssets.Length == 0)
                return;

            var settings = SmartexportSettings.LoadOrCreate();

            foreach (var assetPath in importedAssets)
            {
                if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (ProcessingPaths.Contains(assetPath))
                    continue;
                if (!settings.EnableAutoGenerate || !IsParsedPrefab(assetPath))
                    continue;

                var manifestPath = TryFindManifestForPrefab(assetPath);
                if (string.IsNullOrEmpty(manifestPath))
                    continue;

                EditorApplication.delayCall += () => ProcessPrefab(assetPath, manifestPath, settings);
            }
        }

        [MenuItem("Tools/Smartexport/Process Selected Prefab Manifest", false)]
        private static void ProcessSelectedPrefab()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Smartexport", "请先选择一个 Prefab 根节点。", "确定");
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Smartexport", "当前选择不是 Prefab 资产。", "确定");
                return;
            }

            if (!IsParsedPrefab(assetPath))
            {
                Debug.LogWarning("[Smartexport] 请选择 PSD 解析 Prefab。正式 Prefab 可直接导出组件描述。");
                return;
            }

            var manifestPath = TryFindManifestForPrefab(assetPath);
            if (string.IsNullOrEmpty(manifestPath))
            {
                EditorUtility.DisplayDialog("Smartexport", $"未找到与 {assetPath} 对应的 ui_manifest.json。", "确定");
                return;
            }

            ProcessPrefab(assetPath, manifestPath, SmartexportSettings.LoadOrCreate());
        }

        /// <summary>
        /// Synchronous entry point for headless PSD collection pipelines.  The
        /// normal AssetPostprocessor path uses delayCall, which is too late
        /// when a batch parses a PSD and immediately exports several roots.
        /// </summary>
        public static void ProcessPrefabNow(string assetPath, string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("Prefab asset path is required.", nameof(assetPath));
            if (string.IsNullOrWhiteSpace(manifestPath))
                manifestPath = TryFindManifestForPrefab(assetPath);
            if (string.IsNullOrWhiteSpace(manifestPath))
                throw new FileNotFoundException($"未找到与 {assetPath} 对应的 ui_manifest.json。", assetPath);

            ProcessPrefab(assetPath, manifestPath, SmartexportSettings.LoadOrCreate());
        }

        [MenuItem("Tools/Smartexport/Verify Selected Manifest Alignment", false)]
        private static void VerifySelectedManifestAlignment()
        {
            var selected = Selection.activeGameObject;
            var assetPath = selected != null ? AssetDatabase.GetAssetPath(selected) : null;
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Smartexport", "请先选择 PSD2UGUI 解析出的 Prefab。", "确定");
                return;
            }

            var manifestPath = TryFindManifestForPrefab(assetPath);
            var manifest = string.IsNullOrEmpty(manifestPath) ? null : LoadManifest(manifestPath);
            if (manifest == null)
            {
                EditorUtility.DisplayDialog("Smartexport", $"未找到或无法读取 manifest: {assetPath}", "确定");
                return;
            }

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(assetPath);
                var byFlatIndex = manifest.GetRenderableLayers()
                    .Where(l => l != null && l.flatIndex >= 0)
                    .GroupBy(l => l.flatIndex)
                    .ToDictionary(g => g.Key, g => g.First());

                var nodes = root.GetComponentsInChildren<PsdLayerNode>(true);
                var matched = 0;
                var missing = 0;
                foreach (var node in nodes)
                {
                    if (node == null)
                        continue;
                    if (byFlatIndex.ContainsKey(node.BindPsdLayerIndex))
                        matched++;
                    else
                        missing++;
                }

                EditorUtility.DisplayDialog("Smartexport",
                    $"Manifest 对齐检查完成\n\nPrefab 节点: {nodes.Length}\n匹配: {matched}\n未匹配: {missing}\nManifest: {manifestPath}",
                    "确定");
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ProcessPrefab(string assetPath, string manifestPath, SmartexportSettings settings)
        {
            if (ProcessingPaths.Contains(assetPath))
                return;
            if (!IsParsedPrefab(assetPath))
                return;

            ProcessingPaths.Add(assetPath);
            GameObject prefabRoot = null;
            var issues = new List<PsdManifestIssue>();

            try
            {
                var manifest = LoadManifest(manifestPath);
                if (manifest == null)
                {
                    issues.Add(CreateIssue("error", "manifest_parse_failed", "manifest 解析失败", assetPath));
                    WriteIssueReport(assetPath, manifestPath, issues);
                    return;
                }

                var layers = manifest.GetRenderableLayers();
                EnsureParserTags(layers);

                if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath)))
                {
                    Debug.LogWarning($"[Smartexport] Prefab 已不存在，跳过 manifest 后处理: {assetPath}");
                    return;
                }

                prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
                if (prefabRoot == null)
                    return;

                var byFlatIndex = layers
                    .Where(l => l != null && l.flatIndex >= 0)
                    .GroupBy(l => l.flatIndex)
                    .ToDictionary(g => g.Key, g => g.First());
                var byName = BuildLayerNameMap(layers);
                var matched = new HashSet<PsdManifestLayer>();
                var matchedNodes = new HashSet<PsdLayerNode>();

                var nodes = prefabRoot.GetComponentsInChildren<PsdLayerNode>(true);
                foreach (var node in nodes)
                {
                    if (node == null || IsManifestlessStructuralNode(node))
                        continue;

                    var layer = ResolveLayerForNodeByName(node, byName, matched);
                    if (layer == null)
                        continue;

                    matched.Add(layer);
                    matchedNodes.Add(node);
                    ApplyMarker(node.gameObject, layer, settings, issues);
                }

                foreach (var node in nodes)
                {
                    if (node == null || matchedNodes.Contains(node))
                        continue;
                    if (IsManifestlessStructuralNode(node))
                        continue;

                    var layer = ResolveLayerForNodeByFlatIndex(node, byFlatIndex, matched);
                    if (layer == null)
                    {
                        issues.Add(CreateIssue("warning", "layer_not_found",
                            $"manifest 中没有匹配节点 {node.name} / flatIndex={node.BindPsdLayerIndex} 的图层",
                            GetHierarchyPath(node.transform, prefabRoot.transform), node.BindPsdLayerIndex));
                        continue;
                    }

                    matched.Add(layer);
                    matchedNodes.Add(node);
                    ApplyMarker(node.gameObject, layer, settings, issues);
                }

                if (nodes.Length == 0)
                {
                    ApplyMarkersByObjectName(prefabRoot.transform, layers, matched, issues, settings);
                }
                NormalizeUnmatchedParsedNodeTypes(nodes);

                foreach (var layer in layers)
                {
                    if (layer == null)
                        continue;

                    if (layer.issues != null)
                    {
                        foreach (var issueCode in layer.issues)
                        {
                            if (string.IsNullOrEmpty(issueCode))
                                continue;
                            issues.Add(CreateIssue("warning", issueCode,
                                $"{layer.DisplayName} 标记了 {issueCode}",
                                layer.path, layer.flatIndex, layer.LayerId));
                        }
                    }

                    if (layer.flatIndex >= 0 && !matched.Contains(layer))
                    {
                        issues.Add(CreateIssue("warning", "manifest_layer_unmatched",
                            $"Prefab 中未匹配到 manifest 图层 {layer.DisplayName}",
                            layer.path, layer.flatIndex, layer.LayerId));
                    }
                }

                var markers = prefabRoot.GetComponentsInChildren<PsdLayerMarker>(true);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                WriteIssueReport(assetPath, manifestPath, issues);

                if (issues.Count > 0)
                {
                    Debug.LogWarning($"[Smartexport] {assetPath} 处理完成，但有 {issues.Count} 个问题，详见 ui_issues.json。");
                }
                else
                {
                    Debug.Log($"[Smartexport] 已为 {assetPath} 注入 {markers.Length} 个 PsdLayerMarker。");
                }
            }
            catch (Exception e)
            {
                issues.Add(CreateIssue("error", "postprocess_exception", e.ToString(), assetPath));
                WriteIssueReport(assetPath, manifestPath, issues);
                Debug.LogException(e);
            }
            finally
            {
                if (prefabRoot != null)
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                ProcessingPaths.Remove(assetPath);
            }
        }

        private static bool IsParsedPrefab(string assetPath)
        {
            return assetPath.EndsWith("_psd_layers_parsed.prefab", StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyMarker(GameObject gameObject, PsdManifestLayer layer,
            SmartexportSettings settings = null, List<PsdManifestIssue> issues = null)
        {
            var amountStepper = LooksLikeAmountStepper(gameObject, layer);
            var structuralContainer = LooksLikeStructuralContainerGroup(gameObject, layer);
            var plainShape = !layer.ShouldBind && LooksLikePlainShapeMisclassifiedButton(layer);
            var belowConfidenceThreshold = ShouldFilterByConfidence(layer, settings);
            var marker = gameObject.GetComponent<PsdLayerMarker>() ?? gameObject.AddComponent<PsdLayerMarker>();
            var defaultKind = amountStepper || structuralContainer ? "group" : (plainShape ? "image" : layer.Kind);
            var defaultTag = amountStepper || structuralContainer ? string.Empty : (plainShape ? "img" : layer.psd2uguiTag);
            var defaultShouldBind = amountStepper || structuralContainer || plainShape || belowConfidenceThreshold ? false : layer.ShouldBind;
            var defaultSuggestedProp = amountStepper ? "Amount" : (structuralContainer ? "Container" : layer.SuggestedProp);
            var defaultBindingCategory = amountStepper || structuralContainer ? "container" :
                (plainShape || belowConfidenceThreshold ? "visual" : layer.BindingCategory);
            marker.PsdLayerId = layer.LayerId;
            marker.FlatIndex = layer.flatIndex;
            marker.HierarchyPath = layer.path;
            marker.OriginalName = layer.originalName;
            marker.ProposedName = layer.DisplayName;
            marker.LockedName = layer.lockedName;
            if (!marker.ManualBindingOverride)
            {
                marker.Kind = defaultKind;
                marker.Psd2UguiTag = defaultTag;
                marker.ShouldBind = defaultShouldBind;
                marker.SuggestedProp = defaultSuggestedProp;
                marker.BindingCategory = defaultBindingCategory;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(marker.Kind))
                    marker.Kind = defaultKind;
                if (string.IsNullOrWhiteSpace(marker.Psd2UguiTag))
                    marker.Psd2UguiTag = defaultTag;
                if (string.IsNullOrWhiteSpace(marker.SuggestedProp))
                    marker.SuggestedProp = defaultSuggestedProp;
                if (string.IsNullOrWhiteSpace(marker.BindingCategory))
                    marker.BindingCategory = defaultBindingCategory;
            }
            marker.Confidence = amountStepper || structuralContainer || plainShape ? Mathf.Min(layer.confidence, 0.5f) : layer.confidence;
            if (!marker.ManualBindingOverride && belowConfidenceThreshold && issues != null)
            {
                issues.Add(CreateIssue("warning", "bind_filtered_low_confidence",
                    $"{layer.DisplayName} 绑定置信度 {layer.confidence:0.###} 低于阈值 {settings.ConfidenceThreshold:0.###}，已跳过绑定",
                    layer.path, layer.flatIndex, layer.LayerId));
            }
            TryApplyPsdLayerNodeUiType(gameObject, layer);
            EditorUtility.SetDirty(marker);
        }

        private static bool ShouldFilterByConfidence(PsdManifestLayer layer, SmartexportSettings settings)
        {
            if (layer == null || settings == null || !layer.ShouldBind)
                return false;
            if (settings.ConfidenceThreshold <= 0f || layer.confidence <= 0f)
                return false;
            return layer.confidence < settings.ConfidenceThreshold;
        }

        private static Dictionary<string, List<PsdManifestLayer>> BuildLayerNameMap(PsdManifestLayer[] layers)
        {
            var map = new Dictionary<string, List<PsdManifestLayer>>(StringComparer.OrdinalIgnoreCase);
            if (layers == null)
                return map;

            foreach (var layer in layers)
            {
                if (layer == null)
                    continue;

                AddLayerName(map, layer.DisplayName, layer);
                AddLayerName(map, layer.name, layer);
                AddLayerName(map, layer.proposedName, layer);
                AddLayerName(map, layer.originalName, layer);
                AddLayerName(map, layer.path, layer);

                if (!string.IsNullOrEmpty(layer.path))
                    AddLayerName(map, layer.path.Split('/').LastOrDefault(), layer);
            }

            return map;
        }

        private static void AddLayerName(Dictionary<string, List<PsdManifestLayer>> map, string name, PsdManifestLayer layer)
        {
            foreach (var key in GetNormalizedNameKeys(name))
            {
                if (!map.TryGetValue(key, out var list))
                {
                    list = new List<PsdManifestLayer>();
                    map[key] = list;
                }

                if (!list.Contains(layer))
                    list.Add(layer);
            }
        }

        private static PsdManifestLayer ResolveLayerForNodeByName(PsdLayerNode node,
            Dictionary<string, List<PsdManifestLayer>> byName,
            HashSet<PsdManifestLayer> matched)
        {
            var names = new[] { node.SourceLayerName, node.gameObject.name };
            foreach (var name in names)
            {
                foreach (var key in GetNormalizedNameKeys(name))
                {
                    if (!byName.TryGetValue(key, out var candidates))
                        continue;

                    var layer = candidates.FirstOrDefault(candidate => candidate != null && !matched.Contains(candidate));
                    if (layer != null)
                        return layer;
                }
            }

            return null;
        }

        private static bool IsManifestlessStructuralNode(PsdLayerNode node)
        {
            if (node == null)
                return false;
            return node.LayerType == PsdLayerType.LayerGroup
                || node.LayerType == PsdLayerType.SectionDividerLayer
                || node.transform.childCount > 0;
        }

        private static PsdManifestLayer ResolveLayerForNodeByFlatIndex(PsdLayerNode node,
            Dictionary<int, PsdManifestLayer> byFlatIndex,
            HashSet<PsdManifestLayer> matched)
        {
            if (byFlatIndex.TryGetValue(node.BindPsdLayerIndex, out var byIndex) && !matched.Contains(byIndex))
                return byIndex;

            return null;
        }

        private static IEnumerable<string> GetNormalizedNameKeys(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                yield break;

            var raw = name.Trim().Replace("\\", "/");
            var lastPathPart = raw.Split('/').LastOrDefault();
            foreach (var variant in ExpandNameVariants(raw))
                yield return variant;
            if (!string.Equals(raw, lastPathPart, StringComparison.Ordinal))
            {
                foreach (var variant in ExpandNameVariants(lastPathPart))
                    yield return variant;
            }
        }

        private static IEnumerable<string> ExpandNameVariants(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                yield break;

            var variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { name };
            var withoutBindPrefix = name.TrimStart('@');
            variants.Add(withoutBindPrefix);
            variants.Add(Regex.Replace(withoutBindPrefix, @"^ref[:_]+", string.Empty, RegexOptions.IgnoreCase));
            variants.Add(Regex.Replace(withoutBindPrefix, @"\.[a-zA-Z0-9_]+$", string.Empty));
            variants.Add(Regex.Replace(Regex.Replace(withoutBindPrefix, @"^ref[:_]+", string.Empty, RegexOptions.IgnoreCase), @"\.[a-zA-Z0-9_]+$", string.Empty));

            foreach (var variant in variants)
            {
                var normalized = NormalizeNameKey(variant);
                if (!string.IsNullOrEmpty(normalized))
                    yield return normalized;
            }
        }

        private static string NormalizeNameKey(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var value = name.Trim().ToLowerInvariant();
            value = ConvertCommonChineseTokensToPinyin(value);
            value = value.Replace("@ref:", string.Empty).Replace("@", string.Empty);
            value = Regex.Replace(value, @"\.[a-zA-Z0-9_]+$", string.Empty);
            value = Regex.Replace(value, @"[^a-z0-9_\-\u4e00-\u9fff]+", "_");
            value = Regex.Replace(value, @"_+", "_").Trim('_', '-');
            return value;
        }

        private static string ConvertCommonChineseTokensToPinyin(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            foreach (var token in CommonChineseNameTokens)
            {
                value = value.Replace(token[0], token[1]);
            }

            return value;
        }

        private static void TryApplyPsdLayerNodeUiType(GameObject gameObject, PsdManifestLayer layer)
        {
            var node = gameObject != null ? gameObject.GetComponent<PsdLayerNode>() : null;
            if (node == null || layer == null)
                return;
            if (node.ManualUITypeOverride)
                return;

            if (LooksLikeAmountStepper(gameObject, layer))
            {
                if (node.UIType != GUIType.Null)
                {
                    node.SetUIType(GUIType.Null);
                    EditorUtility.SetDirty(node);
                    Debug.Log($"[Smartexport] 数量选择器不再按 Slider 处理: {gameObject.name}");
                }
                return;
            }

            if (LooksLikeStructuralContainerGroup(gameObject, layer))
            {
                if (node.UIType != GUIType.Null)
                {
                    node.SetUIType(GUIType.Null);
                    EditorUtility.SetDirty(node);
                    Debug.Log($"[Smartexport] 结构空组不再生成 Image/Button: {gameObject.name}");
                }
                return;
            }

            var uiType = ResolveGuiType(layer);
            if (uiType == GUIType.Null)
                return;

            if (node.UIType != uiType)
            {
                node.SetUIType(uiType);
                EditorUtility.SetDirty(node);
                Debug.Log($"[Smartexport] 修正 PSD 图层 UIType: {gameObject.name} -> {uiType}");
            }
        }

        private static void ApplyMarkersByObjectName(Transform root, PsdManifestLayer[] layers,
            HashSet<PsdManifestLayer> matched, List<PsdManifestIssue> issues, SmartexportSettings settings)
        {
            if (root == null || layers == null)
                return;

            var byFlatIndex = layers
                .Where(l => l != null && l.flatIndex >= 0)
                .GroupBy(l => l.flatIndex)
                .ToDictionary(g => g.Key, g => g.First());
            var byName = BuildLayerNameMap(layers);
            var transforms = root.GetComponentsInChildren<Transform>(true);

            foreach (var transform in transforms)
            {
                if (transform == null || transform == root)
                    continue;

                var layer = ResolveLayerForGeneratedSafeName(transform.name, byFlatIndex, matched);
                if (layer == null)
                    continue;

                matched.Add(layer);
                ApplyMarker(transform.gameObject, layer, settings, issues);
            }

            foreach (var transform in transforms)
            {
                if (transform == null || transform == root)
                    continue;

                foreach (var key in GetNormalizedNameKeys(transform.name))
                {
                    if (!byName.TryGetValue(key, out var candidates))
                        continue;

                    var layer = candidates.FirstOrDefault(candidate => candidate != null && !matched.Contains(candidate));
                    if (layer == null)
                        continue;

                    matched.Add(layer);
                    ApplyMarker(transform.gameObject, layer, settings, issues);
                    break;
                }
            }

            foreach (var layer in layers)
            {
                if (layer == null || !layer.ShouldBind || matched.Contains(layer))
                    continue;

                issues.Add(CreateIssue("warning", "bind_target_not_found",
                    $"最终 Prefab 中未按名称找到绑定节点 {layer.DisplayName}",
                    layer.path, layer.flatIndex, layer.LayerId));
            }
        }

        private static PsdManifestLayer ResolveLayerForGeneratedSafeName(string objectName,
            Dictionary<int, PsdManifestLayer> byFlatIndex,
            HashSet<PsdManifestLayer> matched)
        {
            if (string.IsNullOrWhiteSpace(objectName) || byFlatIndex == null)
                return null;

            var match = GeneratedSafeLayerNameRegex.Match(objectName.Trim());
            if (!match.Success || !int.TryParse(match.Groups["index"].Value, out var flatIndex))
                return null;

            if (!byFlatIndex.TryGetValue(flatIndex, out var layer) || layer == null)
                return null;

            return matched == null || !matched.Contains(layer) ? layer : null;
        }

        private static void NormalizeUnmatchedParsedNodeTypes(PsdLayerNode[] nodes)
        {
            if (nodes == null || nodes.Length == 0)
                return;

            foreach (var node in nodes)
            {
                if (node == null || node.ManualUITypeOverride)
                    continue;

                if (!LooksLikePlainShapeMisclassifiedButton(node))
                    continue;

                node.SetUIType(GUIType.Image);
                EditorUtility.SetDirty(node);
                Debug.Log($"[Smartexport] 纯形状节点不再按 Button 处理: {node.name}");
            }
        }

        private static bool LooksLikePlainShapeMisclassifiedButton(PsdLayerNode node)
        {
            if (node == null)
                return false;

            var uiTypeName = node.UIType.ToString();
            if (node.UIType != GUIType.Button && !uiTypeName.EndsWith("Button", StringComparison.Ordinal))
                return false;

            var name = (node.name ?? string.Empty).ToLowerInvariant();
            if (name.Contains("common_btn") || name.Contains("button") || name.Contains("confirm") ||
                name.Contains("cancel") || name.Contains("use") || name.Contains("buy") || name.Contains("sell"))
                return false;

            return name.Contains("yuanjiaojuxing") || name.Contains("圆角矩形") ||
                   name.Contains("rectangle") || name.Contains("rect");
        }

        private static void AddNameCandidate(Dictionary<string, Queue<PsdManifestLayer>> map, string name, PsdManifestLayer layer)
        {
            if (string.IsNullOrEmpty(name))
                return;

            AddSingleNameCandidate(map, name, layer);

            var withoutBindPrefix = name.TrimStart('@');
            AddSingleNameCandidate(map, withoutBindPrefix, layer);

            var withoutTag = Regex.Replace(name, @"\.[a-zA-Z0-9_]+$", string.Empty);
            AddSingleNameCandidate(map, withoutTag, layer);
            AddSingleNameCandidate(map, withoutTag.TrimStart('@'), layer);
        }

        private static void AddSingleNameCandidate(Dictionary<string, Queue<PsdManifestLayer>> map, string name,
            PsdManifestLayer layer)
        {
            if (string.IsNullOrEmpty(name))
                return;

            if (!map.TryGetValue(name, out var queue))
            {
                queue = new Queue<PsdManifestLayer>();
                map[name] = queue;
            }

            if (!queue.Contains(layer))
                queue.Enqueue(layer);
        }

        private static PsdLayerManifest LoadManifest(string manifestAssetPath)
        {
            try
            {
                var fullPath = Path.GetFullPath(manifestAssetPath);
                if (!File.Exists(fullPath))
                    return null;
                return JsonUtility.FromJson<PsdLayerManifest>(File.ReadAllText(fullPath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Smartexport] 读取 manifest 失败: {manifestAssetPath}\n{e.Message}");
                return null;
            }
        }

        private static void EnsureParserTags(PsdManifestLayer[] layers)
        {
            var parser = UGUIParser.Instance;
            if (parser == null || layers == null || layers.Length == 0)
                return;

            var serialized = new SerializedObject(parser);
            var rules = serialized.FindProperty("rules");
            if (rules == null || !rules.isArray)
                return;

            var changed = false;
            foreach (var layer in layers)
            {
                if (layer == null || string.IsNullOrEmpty(layer.psd2uguiTag))
                    continue;

                var uiType = ResolveGuiType(layer);
                if (uiType == GUIType.Null)
                    continue;

                changed |= EnsureRuleContainsTag(rules, uiType, layer.psd2uguiTag);
            }

            if (!changed)
                return;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(parser);
            AssetDatabase.SaveAssets();
        }

        private static bool EnsureRuleContainsTag(SerializedProperty rules, GUIType uiType, string tag)
        {
            tag = tag.Trim().TrimStart('.').ToLowerInvariant();
            if (string.IsNullOrEmpty(tag))
                return false;

            for (var i = 0; i < rules.arraySize; i++)
            {
                var rule = rules.GetArrayElementAtIndex(i);
                var uiTypeProperty = rule.FindPropertyRelative("UIType");
                if (uiTypeProperty == null || uiTypeProperty.intValue != (int)uiType)
                    continue;

                var typeMatches = rule.FindPropertyRelative("TypeMatches");
                if (typeMatches == null || !typeMatches.isArray)
                    return false;

                for (var j = 0; j < typeMatches.arraySize; j++)
                {
                    if (string.Equals(typeMatches.GetArrayElementAtIndex(j).stringValue, tag,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                typeMatches.InsertArrayElementAtIndex(typeMatches.arraySize);
                typeMatches.GetArrayElementAtIndex(typeMatches.arraySize - 1).stringValue = tag;
                return true;
            }

            return false;
        }

        private static GUIType ResolveGuiType(string kind, string tag)
        {
            var value = string.IsNullOrEmpty(kind) ? tag : kind;
            switch (value.Trim().TrimStart('.').ToLowerInvariant())
            {
                case "button":
                case "btn":
                case "bt":
                    return GUIType.Button;
                case "text":
                case "txt":
                    return GUIType.Text;
                case "slider":
                case "sld":
                    return GUIType.Slider;
                case "toggle":
                case "tg":
                    return GUIType.Toggle;
                case "input":
                case "ipt":
                    return GUIType.InputField;
                case "dropdown":
                case "dpd":
                    return GUIType.Dropdown;
                case "scrollview":
                case "scroll":
                case "sv":
                    return GUIType.ScrollView;
                case "background":
                case "image":
                case "icon":
                case "img":
                    return GUIType.Image;
                default:
                    return GUIType.Null;
            }
        }

        private static GUIType ResolveGuiType(PsdManifestLayer layer)
        {
            if (layer == null)
                return GUIType.Null;

            if (!layer.ShouldBind && LooksLikePlainShapeMisclassifiedButton(layer))
                return GUIType.Image;

            if (LooksLikeButton(layer))
                return GUIType.Button;

            return ResolveGuiType(layer.Kind, layer.psd2uguiTag);
        }

        private static bool LooksLikePlainShapeMisclassifiedButton(PsdManifestLayer layer)
        {
            if (layer == null)
                return false;

            var manifestType = $"{layer.Kind} {layer.psd2uguiTag} {layer.type}".ToLowerInvariant();
            if (!manifestType.Contains("button") && !manifestType.Contains("btn") && !manifestType.Contains("bt"))
                return false;

            var original = $"{layer.originalName} {layer.path}".ToLowerInvariant();
            if (!original.Contains("圆角矩形") && !original.Contains("矩形") &&
                !original.Contains("rectangle") && !original.Contains("rect"))
                return false;

            var semantic = $"{layer.originalName} {layer.path} {layer.text} {layer.textSample}".ToLowerInvariant();
            if (semantic.Contains("common_btn") || semantic.Contains("按钮") || semantic.Contains("按鈕") ||
                semantic.Contains("确定") || semantic.Contains("确认") || semantic.Contains("確認") ||
                semantic.Contains("取消") || semantic.Contains("使用") || semantic.Contains("购买") ||
                semantic.Contains("購買") || semantic.Contains("领取") || semantic.Contains("領取") ||
                semantic.Contains("button"))
                return false;

            return true;
        }

        private static bool LooksLikeButton(PsdManifestLayer layer)
        {
            var name = $"{layer.name} {layer.originalName} {layer.proposedName} {layer.path}".ToLowerInvariant();
            if (name.Contains(".bt") || name.Contains("@btn") || name.Contains("_btn_") || name.Contains("common_btn") || name.Contains("button"))
                return !name.Contains("itembg") && !name.Contains("item_bg") && !name.Contains("background");

            return name.Contains("按钮") && !name.Contains("按钮底") && !name.Contains("按钮背景");
        }

        private static bool LooksLikeStructuralContainerGroup(GameObject gameObject, PsdManifestLayer layer)
        {
            if (gameObject == null || layer == null || gameObject.transform.childCount == 0)
                return false;

            var node = gameObject.GetComponent<PsdLayerNode>();
            if (node == null || node.LayerType != PsdLayerType.LayerGroup)
                return false;

            var original = $"{layer.originalName} {layer.lockedName}".ToLowerInvariant();
            if (original.Contains(".bt") || original.Contains(".sld") || original.Contains(".tg") ||
                original.Contains(".ipt") || original.Contains(".dpd") || original.Contains(".sv") ||
                original.Contains("@btn") || original.Contains("@ref:"))
                return false;

            var semantic = $"{gameObject.name} {layer.name} {layer.originalName} {layer.proposedName} {layer.path}".ToLowerInvariant();
            if (semantic.Contains("common_btn") || semantic.Contains("_btn_") || semantic.Contains("button") ||
                semantic.Contains("按钮") || semantic.Contains("按鈕") || semantic.Contains("slider") ||
                semantic.Contains("progress") || semantic.Contains("toggle") || semantic.Contains("input") ||
                semantic.Contains("dropdown") || semantic.Contains("scrollview"))
                return false;

            var gameObjectName = gameObject.name.ToLowerInvariant();
            var genericContainerName = Regex.IsMatch(gameObjectName, @"^(zu|group|grp|组|圖層組|图层组)[_\-\d]*(kaobei|copy|拷贝|副本)?[_\-\d]*$");
            var uiType = ResolveGuiType(layer.Kind, layer.psd2uguiTag);
            return genericContainerName || uiType == GUIType.Image || uiType == GUIType.Button;
        }

        private static bool LooksLikeAmountStepper(GameObject gameObject, PsdManifestLayer layer)
        {
            if (!IsSliderLayer(layer))
                return false;

            var text = $"{gameObject?.name} {layer.name} {layer.originalName} {layer.proposedName} {layer.path} {layer.text} {layer.textSample}";
            if (gameObject != null)
            {
                foreach (var marker in gameObject.GetComponentsInChildren<PsdLayerMarker>(true))
                {
                    if (marker == null)
                        continue;
                    text += $" {marker.name} {marker.OriginalName} {marker.ProposedName} {marker.HierarchyPath} {marker.SuggestedProp}";
                }

                foreach (var transform in gameObject.GetComponentsInChildren<Transform>(true))
                {
                    if (transform != null)
                        text += " " + transform.name;
                }
            }

            var normalized = text.ToLowerInvariant();
            var hasMin = Regex.IsMatch(normalized, @"(^|[^a-z0-9])min([^a-z0-9]|$)|最小|下限");
            var hasMax = Regex.IsMatch(normalized, @"(^|[^a-z0-9])max([^a-z0-9]|$)|最大|上限");
            var hasMinus = Regex.IsMatch(normalized, @"(^|[^a-z0-9])(jian|minus|sub|subtract|reduce|decrease)([^a-z0-9]|$)|减|－");
            var hasPlus = Regex.IsMatch(normalized, @"(^|[^a-z0-9])(jia|plus|add|increase)([^a-z0-9]|$)|加|＋|\+");
            var hasCount = Regex.IsMatch(normalized, @"\d+\s*/\s*\d+|数量|使用量|个数|count|amount|num|number");

            return (hasMin && hasMax && (hasMinus || hasPlus)) ||
                   (hasMinus && hasPlus && hasCount);
        }

        private static bool IsSliderLayer(PsdManifestLayer layer)
        {
            if (layer == null)
                return false;
            var value = $"{layer.Kind} {layer.psd2uguiTag} {layer.type} {layer.proposedName} {layer.name}".ToLowerInvariant();
            return value.Contains("slider") || value.Contains("sld") || value.Contains(".sld");
        }

        private static string TryFindManifestForPrefab(string prefabAssetPath)
        {
            var directory = Path.GetDirectoryName(prefabAssetPath)?.Replace("\\", "/");
            if (string.IsNullOrEmpty(directory))
                return null;

            var fileName = Path.GetFileNameWithoutExtension(prefabAssetPath);
            var baseName = fileName;
            const string parsedSuffix = "_psd_layers_parsed";
            if (baseName.EndsWith(parsedSuffix, StringComparison.OrdinalIgnoreCase))
            {
                baseName = baseName.Substring(0, baseName.Length - parsedSuffix.Length);
            }

            var candidateList = new List<string>
            {
                $"{directory}/{baseName}.ui_manifest.json",
                $"{directory}/{baseName}_binding.json"
            };

            const string panelSuffix = "Panel";
            if (baseName.EndsWith(panelSuffix, StringComparison.OrdinalIgnoreCase))
            {
                var withoutPanel = baseName.Substring(0, baseName.Length - panelSuffix.Length);
                if (!string.IsNullOrEmpty(withoutPanel))
                {
                    candidateList.Add($"{directory}/{withoutPanel}.ui_manifest.json");
                    candidateList.Add($"{directory}/{withoutPanel}_binding.json");
                }
            }

            var candidates = candidateList.ToArray();

            foreach (var candidate in candidates)
            {
                if (File.Exists(Path.GetFullPath(candidate)))
                    return candidate;
            }

            var manifestGuid = AssetDatabase.FindAssets($"{baseName} ui_manifest")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path => path.EndsWith($"{baseName}.ui_manifest.json", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(manifestGuid))
                return manifestGuid;

            if (baseName.EndsWith(panelSuffix, StringComparison.OrdinalIgnoreCase))
            {
                var withoutPanel = baseName.Substring(0, baseName.Length - panelSuffix.Length);
                var panelManifestGuid = AssetDatabase.FindAssets($"{withoutPanel} ui_manifest")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(path => path.EndsWith($"{withoutPanel}.ui_manifest.json", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(panelManifestGuid))
                    return panelManifestGuid;
            }

            return AssetDatabase.FindAssets($"{baseName}_binding")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path => path.EndsWith($"{baseName}_binding.json", StringComparison.OrdinalIgnoreCase));
        }

        private static string ToAssetPath(string absolutePath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            absolutePath = Path.GetFullPath(absolutePath);
            if (!absolutePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                return absolutePath.Replace("\\", "/");
            return absolutePath.Substring(projectRoot.Length + 1).Replace("\\", "/");
        }

        private static PsdManifestIssue CreateIssue(string severity, string code, string message, string nodePath,
            int flatIndex = -1, int layerId = 0)
        {
            return new PsdManifestIssue
            {
                severity = severity,
                code = code,
                message = message,
                nodePath = nodePath,
                flatIndex = flatIndex,
                layerId = layerId
            };
        }

        private static void WriteIssueReport(string prefabAssetPath, string manifestPath, List<PsdManifestIssue> issues)
        {
            try
            {
                var directory = Path.GetDirectoryName(prefabAssetPath)?.Replace("\\", "/");
                if (string.IsNullOrEmpty(directory))
                    return;

                var baseName = Path.GetFileNameWithoutExtension(prefabAssetPath);
                if (baseName.EndsWith("_psd_layers_parsed", StringComparison.OrdinalIgnoreCase))
                    baseName = baseName.Substring(0, baseName.Length - "_psd_layers_parsed".Length);

                var report = new PsdManifestIssueReport
                {
                    manifestPath = manifestPath,
                    prefabPath = prefabAssetPath,
                    generatedAt = DateTime.Now.ToString("O"),
                    issues = issues.ToArray()
                };

                var reportAssetPath = $"{directory}/{baseName}.ui_issues.json";
                File.WriteAllText(Path.GetFullPath(reportAssetPath), JsonUtility.ToJson(report, true));
                AssetDatabase.ImportAsset(reportAssetPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Smartexport] 写入 issues 失败: {e.Message}");
            }
        }

        private static string GetHierarchyPath(Transform transform, Transform root)
        {
            if (transform == null)
                return string.Empty;

            var parts = new List<string>();
            var current = transform;
            while (current != null && current != root)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            if (root != null)
                parts.Add(root.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}

