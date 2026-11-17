#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    internal sealed class PsdMultiScreenCandidate
    {
        public PsdLayerNode Node;
        public string OutputName;
        public string Reason;
        public bool Selected;
    }

    internal static class PsdMultiScreenDetector
    {
        private static readonly string[] ScreenNameHints =
        {
            "panel", "screen", "page", "view", "popup", "dialog", "window",
            "界面", "页面", "弹窗", "窗口"
        };

        internal static List<PsdMultiScreenCandidate> Detect(Psd2UIFormConverter converter)
        {
            var result = new List<PsdMultiScreenCandidate>();
            if (converter == null) return result;

            var sets = new List<CandidateSet>();
            AddSiblingSet(sets, converter.transform, 0);
            foreach (Transform child in converter.transform)
            {
                var childNode = child.GetComponent<PsdLayerNode>();
                if (IsBoundaryGroup(childNode)) AddSiblingSet(sets, child, 1);
            }

            var validSets = sets.Where(set => set.Nodes.Count >= 2).ToList();
            var best = validSets
                .Where(set => set.AutoSelectedCount >= 2)
                .OrderBy(set => set.Depth)
                .ThenByDescending(set => set.Score)
                .FirstOrDefault()
                ?? validSets
                    .OrderBy(set => set.Depth)
                    .ThenByDescending(set => set.Score)
                    .FirstOrDefault();
            if (best == null) return result;

            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < best.Nodes.Count; i++)
            {
                var node = best.Nodes[i];
                bool explicitPanel = node.UIType == GUIType.Panel;
                bool nameHint = HasScreenNameHint(node.name) || HasScreenNameHint(node.SourceLayerName);
                bool comparableLargeGroup = IsComparableLargeGroup(node, best.Nodes);
                string reason = explicitPanel
                    ? "已标记为 Panel"
                    : nameHint
                        ? "名称包含界面特征"
                        : comparableLargeGroup
                            ? "与同级大分组尺寸相近"
                            : "同级分组候选（请人工确认）";
                string outputName = MakeUniqueOutputName(SuggestOutputName(node, i + 1), usedNames);
                result.Add(new PsdMultiScreenCandidate
                {
                    Node = node,
                    OutputName = outputName,
                    Reason = reason,
                    Selected = explicitPanel || nameHint || comparableLargeGroup
                });
            }
            return result;
        }

        internal static string NormalizeOutputName(string rawName, string fallbackName = "Screen")
        {
            var sanitized = LayerNameUtility.Sanitize(string.IsNullOrWhiteSpace(rawName) ? fallbackName : rawName.Trim());
            if (string.IsNullOrWhiteSpace(sanitized)) sanitized = fallbackName;
            return sanitized.EndsWith("Panel", StringComparison.OrdinalIgnoreCase) ? sanitized : sanitized + "Panel";
        }

        private static void AddSiblingSet(List<CandidateSet> sets, Transform parent, int depth)
        {
            var nodes = new List<PsdLayerNode>();
            foreach (Transform child in parent)
            {
                var node = child.GetComponent<PsdLayerNode>();
                if (IsBoundaryGroup(node)) nodes.Add(node);
            }
            if (nodes.Count < 2) return;

            int autoSelectedCount = nodes.Count(node =>
                node.UIType == GUIType.Panel
                || HasScreenNameHint(node.name)
                || HasScreenNameHint(node.SourceLayerName)
                || IsComparableLargeGroup(node, nodes));
            int explicitCount = nodes.Count(node => node.UIType == GUIType.Panel);
            sets.Add(new CandidateSet
            {
                Nodes = nodes,
                Depth = depth,
                AutoSelectedCount = autoSelectedCount,
                Score = autoSelectedCount * 100 + explicitCount * 20 + nodes.Count * 2 - depth
            });
        }

        private static bool IsBoundaryGroup(PsdLayerNode node)
        {
            // In a PSD collection, only one screen is usually visible in the
            // artist's saved snapshot. Hidden sibling groups are still complete
            // screen definitions and must remain export candidates.
            if (node == null || node.HasReusePrefabReference) return false;
            if (node.transform.childCount == 0) return false;
            return node.LayerType == PsdLayerType.LayerGroup
                || node.LayerType == PsdLayerType.SectionDividerLayer
                || node.UIType == GUIType.Panel;
        }

        private static bool IsComparableLargeGroup(PsdLayerNode node, IReadOnlyList<PsdLayerNode> siblings)
        {
            float area = GetArea(node.LayerRect);
            if (area <= 0f || siblings == null || siblings.Count < 2) return false;
            float largestArea = siblings.Max(item => GetArea(item.LayerRect));
            if (largestArea <= 0f || area < largestArea * 0.6f) return false;
            return siblings.Count(item => GetArea(item.LayerRect) >= largestArea * 0.6f) >= 2;
        }

        private static float GetArea(Rect rect)
        {
            return Mathf.Max(0f, rect.width) * Mathf.Max(0f, rect.height);
        }

        private static bool HasScreenNameHint(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return ScreenNameHints.Any(hint => value.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string SuggestOutputName(PsdLayerNode node, int index)
        {
            string rawName = !string.IsNullOrWhiteSpace(node.SourceLayerName) ? node.SourceLayerName : node.name;
            return NormalizeOutputName(rawName, $"Screen{index}");
        }

        private static string MakeUniqueOutputName(string outputName, HashSet<string> usedNames)
        {
            string baseName = NormalizeOutputName(outputName);
            string candidate = baseName;
            int suffix = 2;
            while (!usedNames.Add(candidate))
            {
                string stem = baseName.EndsWith("Panel", StringComparison.OrdinalIgnoreCase)
                    ? baseName.Substring(0, baseName.Length - "Panel".Length)
                    : baseName;
                candidate = $"{stem}{suffix++}Panel";
            }
            return candidate;
        }

        private sealed class CandidateSet
        {
            public List<PsdLayerNode> Nodes;
            public int Depth;
            public int AutoSelectedCount;
            public int Score;
        }
    }

    internal sealed class PsdMultiScreenExportWindow : EditorWindow
    {
        private Psd2UIFormConverter converter;
        private List<PsdMultiScreenCandidate> candidates = new List<PsdMultiScreenCandidate>();
        private Vector2 scrollPosition;

        internal static void Open(Psd2UIFormConverter target)
        {
            if (target == null)
            {
                EditorUtility.DisplayDialog("PSD 多界面导出", "未找到 PSD 转换器。", "确定");
                return;
            }
            var window = GetWindow<PsdMultiScreenExportWindow>(true, "PSD 合集分界面导出", true);
            window.minSize = new Vector2(720f, 360f);
            window.converter = target;
            window.RefreshCandidates();
            window.Show();
        }

        private void OnGUI()
        {
            if (converter == null)
            {
                EditorGUILayout.HelpBox("PSD 转换器已失效，请关闭窗口后重新打开。", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("合集界面识别", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "识别同一父节点下并列的 PSD 分组。Panel、界面特征命名和尺寸相近的大分组会默认勾选；导出前请确认边界和 Prefab 名称。",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.ObjectField("PSD", converter, typeof(Psd2UIFormConverter), true);
            if (GUILayout.Button("重新识别", GUILayout.Width(90f))) RefreshCandidates();
            EditorGUILayout.EndHorizontal();

            if (candidates.Count < 2)
            {
                EditorGUILayout.HelpBox(
                    "没有找到至少两个并列的界面分组。请确保每个界面是独立 PSD 图层组；推荐将界面根标记为 Panel 后重新识别。",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全选", GUILayout.Width(70f))) candidates.ForEach(item => item.Selected = true);
            if (GUILayout.Button("全不选", GUILayout.Width(70f))) candidates.ForEach(item => item.Selected = false);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"候选 {candidates.Count} 个，已选 {candidates.Count(item => item.Selected)} 个", GUILayout.Width(180f));
            EditorGUILayout.EndHorizontal();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            for (int i = 0; i < candidates.Count; i++)
            {
                var item = candidates[i];
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                item.Selected = EditorGUILayout.Toggle(item.Selected, GUILayout.Width(20f));
                EditorGUILayout.ObjectField(item.Node, typeof(PsdLayerNode), true, GUILayout.Width(220f));
                EditorGUILayout.LabelField("Prefab 名", GUILayout.Width(70f));
                item.OutputName = EditorGUILayout.TextField(item.OutputName);
                if (GUILayout.Button("定位", GUILayout.Width(55f)))
                {
                    Selection.activeGameObject = item.Node.gameObject;
                    EditorGUIUtility.PingObject(item.Node.gameObject);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField($"识别依据：{item.Reason}    范围：{item.Node.LayerRect}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(!candidates.Any(item => item.Selected));
            if (GUILayout.Button("导出所选界面", GUILayout.Height(34f))) ExportSelected();
            EditorGUI.EndDisabledGroup();
        }

        private void RefreshCandidates()
        {
            candidates = PsdMultiScreenDetector.Detect(converter);
            Repaint();
        }

        private void ExportSelected()
        {
            var selected = candidates.Where(item => item.Selected).ToList();
            if (selected.Count == 0) return;

            var duplicateNames = selected
                .Select(item => PsdMultiScreenDetector.NormalizeOutputName(item.OutputName))
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicateNames.Length > 0)
            {
                EditorUtility.DisplayDialog("无法导出", $"Prefab 名称重复：{string.Join(", ", duplicateNames)}", "确定");
                return;
            }

            if (!converter.TryResolveUIFormOutputDirectory(out var outputDir)) return;
            string preview = string.Join("\n", selected.Select(item =>
                $"• {item.Node.name} → {PsdMultiScreenDetector.NormalizeOutputName(item.OutputName)}.prefab"));
            if (!EditorUtility.DisplayDialog(
                    "确认分界面导出",
                    $"输出目录：{outputDir}\n\n{preview}\n\n已有同名 Prefab 将按该界面节点增量更新。",
                    "开始导出",
                    "取消"))
            {
                return;
            }

            int successCount = 0;
            try
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    var item = selected[i];
                    string outputName = PsdMultiScreenDetector.NormalizeOutputName(item.OutputName, $"Screen{i + 1}");
                    EditorUtility.DisplayProgressBar("PSD 合集分界面导出", $"正在生成 {outputName}.prefab", i / (float)selected.Count);
                    if (converter.GenerateUIFormToDirectory(item.Node, outputName, outputDir)) successCount++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            EditorUtility.DisplayDialog(
                "分界面导出完成",
                $"成功 {successCount}/{selected.Count}\n输出目录：{outputDir}",
                "确定");
        }
    }
}
#endif
