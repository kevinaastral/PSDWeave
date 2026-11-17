using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI.Smartexport.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(PsdLayerMarker))]
    public sealed class PsdLayerMarkerInspector : UnityEditor.Editor
    {
        private static readonly BindingTypeOption[] TypeOptions =
        {
            new BindingTypeOption("Button", "button", "bt"),
            new BindingTypeOption("Text", "text", "txt"),
            new BindingTypeOption("Image", "image", "img"),
            new BindingTypeOption("RawImage", "image", "rimg"),
            new BindingTypeOption("Slider", "slider", "sld"),
            new BindingTypeOption("Toggle", "toggle", "tg"),
            new BindingTypeOption("Input", "input", "ipt"),
            new BindingTypeOption("Dropdown", "dropdown", "dpd"),
            new BindingTypeOption("ScrollRect", "scrollview", "sv"),
            new BindingTypeOption("GameObject", "gameobject", "obj"),
            new BindingTypeOption("Transform", "transform", "tran"),
            new BindingTypeOption("RectTransform", "recttransform", "rect"),
            new BindingTypeOption("Canvas", "canvas", "canvas"),
            new BindingTypeOption("CanvasGroup", "canvasgroup", "cg"),
            new BindingTypeOption("Spine", "spine", "spine"),
        };

        private PsdLayerMarker marker;

        private void OnEnable()
        {
            marker = target as PsdLayerMarker;
        }

        public override void OnInspectorGUI()
        {
            if (marker == null)
                return;

            DrawBindingEditor();
            EditorGUILayout.Space();
            DrawActions();
            EditorGUILayout.Space();
            DrawManifestInfo();
        }

        private void DrawBindingEditor()
        {
            EditorGUILayout.LabelField("Smartexport Binding", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            EditorGUI.BeginChangeCheck();
            bool shouldBind = EditorGUILayout.ToggleLeft("生成绑定字段", marker.ShouldBind);
            int typeIndex = EditorGUILayout.Popup("绑定类型", FindTypeIndex(marker), GetTypeLabels());
            string suggestedProp = EditorGUILayout.TextField("字段语义名", marker.SuggestedProp ?? string.Empty);
            string bindingCategory = EditorGUILayout.TextField("分类", marker.BindingCategory ?? string.Empty);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(targets, "Edit Smartexport Binding");
                foreach (var item in targets)
                {
                    var current = item as PsdLayerMarker;
                    if (current == null)
                        continue;

                    current.ShouldBind = shouldBind;
                    current.FollowsSource = false;
                    current.ManualBindingOverride = true;
                    current.SuggestedProp = suggestedProp;
                    current.BindingCategory = bindingCategory;
                    ApplyTypeOption(current, TypeOptions[Mathf.Clamp(typeIndex, 0, TypeOptions.Length - 1)]);
                    MarkMarkerDirty(current);
                }
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("使用节点名"))
            {
                SetSuggestedProp(marker.gameObject.name);
            }
            if (GUILayout.Button("使用PS建议名"))
            {
                SetSuggestedProp(!string.IsNullOrEmpty(marker.ProposedName) ? marker.ProposedName : marker.OriginalName);
            }
            if (GUILayout.Button(marker.ShouldBind ? "取消绑定" : "设为绑定"))
            {
                SetShouldBind(!marker.ShouldBind);
            }
            EditorGUILayout.EndHorizontal();

            if (marker.ManualBindingOverride)
            {
                EditorGUILayout.HelpBox("当前绑定设置已被人工覆盖，manifest 后处理不会再覆盖绑定开关或类型。", MessageType.Info);
                if (GUILayout.Button("解除人工绑定覆盖"))
                {
                    Undo.RecordObjects(targets, "Clear Smartexport Manual Binding Override");
                    foreach (var item in targets)
                    {
                        var current = item as PsdLayerMarker;
                        if (current == null)
                            continue;

                        current.ManualBindingOverride = false;
                        MarkMarkerDirty(current);
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            if (GUILayout.Button("保存当前 Prefab 绑定设置"))
            {
                SaveCurrentPrefab(GetRoot(marker));
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawManifestInfo()
        {
            EditorGUILayout.LabelField("PSD Layer Data", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            DrawReadOnly("Layer Id", marker.PsdLayerId.ToString());
            DrawReadOnly("Flat Index", marker.FlatIndex.ToString());
            DrawReadOnly("Kind", marker.Kind);
            DrawReadOnly("PSD2UGUI Tag", marker.Psd2UguiTag);
            DrawReadOnly("Confidence", marker.Confidence.ToString("0.##"));
            DrawReadOnly("Original Name", marker.OriginalName);
            DrawReadOnly("Proposed Name", marker.ProposedName);
            DrawReadOnly("Hierarchy Path", marker.HierarchyPath);
            EditorGUILayout.EndVertical();
        }

        private static void DrawReadOnly(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(label, value ?? string.Empty);
            }
            if (GUILayout.Button("Copy", GUILayout.Width(52)))
            {
                EditorGUIUtility.systemCopyBuffer = value ?? string.Empty;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void SetSuggestedProp(string value)
        {
            Undo.RecordObjects(targets, "Set Smartexport Suggested Prop");
            foreach (var item in targets)
            {
                var current = item as PsdLayerMarker;
                if (current == null)
                    continue;
                current.FollowsSource = false;
                current.ManualBindingOverride = true;
                current.SuggestedProp = value ?? string.Empty;
                MarkMarkerDirty(current);
            }
        }

        private void SetShouldBind(bool value)
        {
            Undo.RecordObjects(targets, "Set Smartexport ShouldBind");
            foreach (var item in targets)
            {
                var current = item as PsdLayerMarker;
                if (current == null)
                    continue;
                current.FollowsSource = false;
                current.ManualBindingOverride = true;
                current.ShouldBind = value;
                MarkMarkerDirty(current);
            }
        }

        private static string[] GetTypeLabels()
        {
            var labels = new string[TypeOptions.Length];
            for (int i = 0; i < TypeOptions.Length; i++)
                labels[i] = TypeOptions[i].Label;
            return labels;
        }

        private static int FindTypeIndex(PsdLayerMarker marker)
        {
            if (marker == null)
                return 0;

            for (int i = 0; i < TypeOptions.Length; i++)
            {
                if (string.Equals(marker.Psd2UguiTag?.TrimStart('.'), TypeOptions[i].Tag, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(marker.Kind, TypeOptions[i].Kind, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return 0;
        }

        private static void ApplyTypeOption(PsdLayerMarker marker, BindingTypeOption option)
        {
            marker.Kind = option.Kind;
            marker.Psd2UguiTag = "." + option.Tag;
        }

        private static void MarkMarkerDirty(PsdLayerMarker marker)
        {
            if (marker == null)
                return;

            EditorUtility.SetDirty(marker);
            PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
            MarkPrefabStageDirty(marker);
        }

        private static void MarkPrefabStageDirty(PsdLayerMarker marker)
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.prefabContentsRoot != null &&
                marker != null && marker.transform.IsChildOf(prefabStage.prefabContentsRoot.transform))
            {
                EditorSceneManager.MarkSceneDirty(prefabStage.scene);
            }
        }

        private static GameObject GetRoot(PsdLayerMarker marker)
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.prefabContentsRoot != null &&
                marker != null && marker.transform.IsChildOf(prefabStage.prefabContentsRoot.transform))
            {
                return prefabStage.prefabContentsRoot;
            }

            return marker != null ? marker.transform.root.gameObject : null;
        }

        private static void SaveCurrentPrefab(GameObject root)
        {
            if (root == null)
                return;

            EditorUtility.SetDirty(root);

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.prefabContentsRoot == root)
            {
                EditorSceneManager.MarkSceneDirty(prefabStage.scene);
                PrefabUtility.SaveAsPrefabAsset(root, prefabStage.assetPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Smartexport] 已保存 Prefab 绑定设置: {root.name}");
                return;
            }

            if (PrefabUtility.IsPartOfPrefabAsset(root))
            {
                PrefabUtility.SavePrefabAsset(root);
            }
            else
            {
                EditorSceneManager.MarkSceneDirty(root.scene);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Smartexport] 已保存 Prefab 绑定设置: {root.name}");
        }

        private readonly struct BindingTypeOption
        {
            public readonly string Label;
            public readonly string Kind;
            public readonly string Tag;

            public BindingTypeOption(string label, string kind, string tag)
            {
                Label = label;
                Kind = kind;
                Tag = tag;
            }
        }
    }
}
