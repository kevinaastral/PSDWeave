using System;
using UGF.EditorTools.Psd2UGUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI.Smartexport.Editor
{
    [InitializeOnLoad]
    internal static class SmartexportBindingHierarchyGUI
    {
        private const float BindToggleX = 35f;
        private const float BindToggleWidth = 10f;

        private static readonly BindingTypeOption[] TypeOptions =
        {
            new BindingTypeOption("TMPButton", "button", "bt", "@Btn"),
            new BindingTypeOption("TMPText", "text", "txt", "@Txt"),
            new BindingTypeOption("Image", "image", "img", "@Image"),
            new BindingTypeOption("RawImage", "image", "rimg", "@RawImage"),
            new BindingTypeOption("Slider", "slider", "sld", "@Slider"),
            new BindingTypeOption("Toggle", "toggle", "tg", "@Toggle"),
            new BindingTypeOption("InputField", "input", "ipt", "@Input"),
            new BindingTypeOption("Dropdown", "dropdown", "dpd", "@Dropdown"),
            new BindingTypeOption("ScrollView", "scrollview", "sv", "@ScrollRect"),
            new BindingTypeOption("LoopVScroll", "scrollview", "vscroll", "@LoopVScroll"),
            new BindingTypeOption("LoopHScroll", "scrollview", "hscroll", "@LoopHScroll"),
            new BindingTypeOption("LoopList", "scrollview", "looplist", "@LoopList"),
            new BindingTypeOption("LoopGrid", "scrollview", "loopgrid", "@LoopGrid"),
            new BindingTypeOption("GameObject", "gameobject", "obj", "@Obj"),
            new BindingTypeOption("Transform", "transform", "tran", "@Tran"),
            new BindingTypeOption("RectTransform", "recttransform", "rect", "@Rect"),
            new BindingTypeOption("Canvas", "canvas", "canvas", "@Canvas"),
            new BindingTypeOption("CanvasGroup", "canvasgroup", "cg", "@CanvasGroup"),
            new BindingTypeOption("Animator", "animator", "animator", "@Animator"),
            new BindingTypeOption("Animation", "animation", "animation", "@Animation"),
            new BindingTypeOption("Spine", "spine", "spine", "@Spine"),
        };

        static SmartexportBindingHierarchyGUI()
        {
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
        }

        private static void OnHierarchyGUI(int instanceId, Rect selectionRect)
        {
            if (Event.current == null)
                return;

            var gameObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (!TryResolveBindingTarget(gameObject, out var marker, out var prefabRoot))
                return;

            if (gameObject.GetComponent<PsdLayerNode>() != null)
                return;

            DrawBindToggle(gameObject, marker, prefabRoot, selectionRect);
            DrawTypeDropdown(gameObject, marker, prefabRoot, selectionRect);
        }

        private static void DrawBindToggle(GameObject gameObject, PsdLayerMarker marker, GameObject prefabRoot, Rect selectionRect)
        {
            var rect = selectionRect;
            rect.x = BindToggleX;
            rect.width = BindToggleWidth;

            bool currentValue = IsVirtualTarget(gameObject, prefabRoot)
                ? HasLegacySuffix(gameObject.name)
                : marker != null && marker.ShouldBind;
            EditorGUI.BeginChangeCheck();
            bool shouldBind = EditorGUI.Toggle(rect, currentValue);
            if (!EditorGUI.EndChangeCheck())
                return;

            ApplyToSelectedTargets(
                gameObject,
                "Edit Smartexport Binding Flag",
                current => ApplyBindingFlag(current, shouldBind));
        }

        private static void DrawTypeDropdown(GameObject gameObject, PsdLayerMarker marker, GameObject prefabRoot, Rect selectionRect)
        {
            var rect = selectionRect;
            rect.width = Mathf.Clamp(selectionRect.xMax * 0.2f, 100f, 200f);
            rect.x = selectionRect.xMax - rect.width;

            var optionIndex = FindTypeIndex(gameObject, marker, prefabRoot);
            var label = TypeOptions[Mathf.Clamp(optionIndex, 0, TypeOptions.Length - 1)].Label;

            if (!EditorGUI.DropdownButton(rect, new GUIContent(label), FocusType.Passive))
                return;

            var menu = new GenericMenu();
            for (int i = 0; i < TypeOptions.Length; i++)
            {
                var option = TypeOptions[i];
                menu.AddItem(
                    new GUIContent(option.Label),
                    i == optionIndex,
                    () => ApplyToSelectedTargets(
                        gameObject,
                        "Edit Smartexport Binding Type",
                        current => ApplyBindingType(current, option)));
            }

            menu.ShowAsContext();
        }

        private static void ApplyToSelectedTargets(GameObject fallbackGameObject, string undoName, Action<GameObject> apply)
        {
            var selectedTargets = Selection.gameObjects != null && Selection.gameObjects.Length > 1
                ? Array.FindAll(Selection.gameObjects, item =>
                    TryResolveBindingTarget(item, out _, out _) && item.GetComponent<PsdLayerNode>() == null)
                : Array.Empty<GameObject>();

            var targets = selectedTargets.Length > 0 ? selectedTargets : new[] { fallbackGameObject };

            foreach (var target in targets)
            {
                if (target == null)
                    continue;

                apply(target);
            }

            EditorApplication.RepaintHierarchyWindow();
        }

        private static bool TryResolveBindingTarget(GameObject gameObject, out PsdLayerMarker marker, out GameObject prefabRoot)
        {
            marker = null;
            prefabRoot = null;
            if (gameObject == null)
                return false;

            marker = gameObject.GetComponent<PsdLayerMarker>();
            prefabRoot = FindPrefabRoot(gameObject);
            return marker != null || IsVirtualTarget(gameObject, prefabRoot);
        }

        private static bool IsVirtualTarget(GameObject gameObject, GameObject prefabRoot)
        {
            return gameObject != null && prefabRoot != null && gameObject != prefabRoot
                && gameObject.GetComponent<PsdLayerMarker>() == null;
        }

        private static GameObject FindPrefabRoot(GameObject gameObject)
        {
            if (gameObject == null)
                return null;

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot != null &&
                gameObject.transform.IsChildOf(stage.prefabContentsRoot.transform))
            {
                return stage.prefabContentsRoot;
            }

            return PrefabUtility.GetNearestPrefabInstanceRoot(gameObject);
        }

        private static void ApplyBindingFlag(GameObject gameObject, bool shouldBind)
        {
            var prefabRoot = FindPrefabRoot(gameObject);
            if (IsVirtualTarget(gameObject, prefabRoot))
            {
                var option = TypeOptions[FindTypeIndex(gameObject, gameObject.GetComponent<PsdLayerMarker>(), prefabRoot)];
                SetLegacySuffix(gameObject, shouldBind ? option : (BindingTypeOption?)null);
                return;
            }

            var marker = gameObject.GetComponent<PsdLayerMarker>();
            if (marker == null)
                return;

            Undo.RecordObject(marker, "Edit Smartexport Binding Flag");
            marker.FollowsSource = false;
            marker.ManualBindingOverride = true;
            marker.ShouldBind = shouldBind;
            EditorUtility.SetDirty(marker);
            PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
            MarkPrefabStageDirty(gameObject);
        }

        private static void ApplyBindingType(GameObject gameObject, BindingTypeOption option)
        {
            var prefabRoot = FindPrefabRoot(gameObject);
            if (IsVirtualTarget(gameObject, prefabRoot))
            {
                SetLegacySuffix(gameObject, option);
                return;
            }

            var marker = gameObject.GetComponent<PsdLayerMarker>();
            if (marker == null)
                return;

            Undo.RecordObject(marker, "Edit Smartexport Binding Type");
            marker.FollowsSource = false;
            marker.ManualBindingOverride = true;
            ApplyTypeOption(marker, option);
            EditorUtility.SetDirty(marker);
            PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
            MarkPrefabStageDirty(gameObject);
        }

        private static void SetLegacySuffix(GameObject gameObject, BindingTypeOption? option)
        {
            if (gameObject == null)
                return;

            Undo.RecordObject(gameObject, "Edit Smartexport Binding Name");
            var baseName = StripLegacySuffix(gameObject.name);
            gameObject.name = option.HasValue ? baseName + option.Value.LegacySuffix : baseName;
            EditorUtility.SetDirty(gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(gameObject);
            MarkPrefabStageDirty(gameObject);
        }

        private static void MarkPrefabStageDirty(GameObject gameObject)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot != null &&
                gameObject != null && gameObject.transform.IsChildOf(stage.prefabContentsRoot.transform))
            {
                EditorSceneManager.MarkSceneDirty(stage.scene);
            }
        }

        private static int FindTypeIndex(GameObject gameObject, PsdLayerMarker marker, GameObject prefabRoot)
        {
            if (IsVirtualTarget(gameObject, prefabRoot))
            {
                if (TryGetLegacySuffix(gameObject.name, out var suffix))
                {
                    for (int i = 0; i < TypeOptions.Length; i++)
                    {
                        if (string.Equals(suffix, TypeOptions[i].LegacySuffix, StringComparison.OrdinalIgnoreCase))
                            return i;
                    }
                }

                return FindTypeIndexByComponents(gameObject);
            }

            if (marker == null)
                return FindTypeIndexByComponents(gameObject);

            var tag = marker.Psd2UguiTag?.Trim().TrimStart('.');
            for (int i = 0; i < TypeOptions.Length; i++)
            {
                if (string.Equals(tag, TypeOptions[i].Tag, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(marker.Kind, TypeOptions[i].Kind, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return FindTypeIndexByComponents(gameObject);
        }

        private static int FindTypeIndexByComponents(GameObject gameObject)
        {
            if (gameObject == null)
                return 0;

            if (gameObject.GetComponent<UnityEngine.UI.Button>() != null)
                return FindTypeIndexBySuffix("@Btn");
            if (gameObject.GetComponent<UnityEngine.UI.Toggle>() != null)
                return FindTypeIndexBySuffix("@Toggle");
            if (gameObject.GetComponent<UnityEngine.UI.Slider>() != null)
                return FindTypeIndexBySuffix("@Slider");
            if (gameObject.GetComponent<UnityEngine.UI.ScrollRect>() != null)
                return FindTypeIndexBySuffix("@ScrollRect");
            if (gameObject.GetComponent<TMPro.TMP_InputField>() != null || gameObject.GetComponent<UnityEngine.UI.InputField>() != null)
                return FindTypeIndexBySuffix("@Input");
            if (gameObject.GetComponent<TMPro.TMP_Dropdown>() != null || gameObject.GetComponent<UnityEngine.UI.Dropdown>() != null)
                return FindTypeIndexBySuffix("@Dropdown");
            if (gameObject.GetComponent<TMPro.TextMeshProUGUI>() != null || gameObject.GetComponent<UnityEngine.UI.Text>() != null)
                return FindTypeIndexBySuffix("@Txt");
            if (gameObject.GetComponent<UnityEngine.UI.RawImage>() != null)
                return FindTypeIndexBySuffix("@RawImage");
            if (gameObject.GetComponent<UnityEngine.UI.Image>() != null)
                return FindTypeIndexBySuffix("@Image");
            if (gameObject.GetComponent<Canvas>() != null)
                return FindTypeIndexBySuffix("@Canvas");
            if (gameObject.GetComponent<CanvasGroup>() != null)
                return FindTypeIndexBySuffix("@CanvasGroup");

            return FindTypeIndexBySuffix("@Obj");
        }

        private static int FindTypeIndexBySuffix(string suffix)
        {
            for (int i = 0; i < TypeOptions.Length; i++)
            {
                if (string.Equals(suffix, TypeOptions[i].LegacySuffix, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return 0;
        }

        private static void ApplyTypeOption(PsdLayerMarker marker, BindingTypeOption option)
        {
            marker.Kind = option.Kind;
            marker.Psd2UguiTag = "." + option.Tag;
        }

        private static bool HasLegacySuffix(string name)
        {
            return TryGetLegacySuffix(name, out _);
        }

        private static bool TryGetLegacySuffix(string name, out string suffix)
        {
            suffix = null;
            if (string.IsNullOrWhiteSpace(name))
                return false;

            for (int i = 0; i < TypeOptions.Length; i++)
            {
                var candidate = TypeOptions[i].LegacySuffix;
                if (name.EndsWith(candidate, StringComparison.OrdinalIgnoreCase))
                {
                    suffix = candidate;
                    return true;
                }
            }

            return false;
        }

        private static string StripLegacySuffix(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Node";

            return TryGetLegacySuffix(name, out var suffix)
                ? name.Substring(0, name.Length - suffix.Length)
                : name;
        }

        private readonly struct BindingTypeOption
        {
            public readonly string Label;
            public readonly string Kind;
            public readonly string Tag;
            public readonly string LegacySuffix;

            public BindingTypeOption(string label, string kind, string tag, string legacySuffix)
            {
                Label = label;
                Kind = kind;
                Tag = tag;
                LegacySuffix = legacySuffix;
            }
        }
    }
}
