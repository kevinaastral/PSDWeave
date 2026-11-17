/*
插件获取:
https://efunstudio.cn
https://shop106471535.taobao.com
*/

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.IO;
using System.Linq;
using cn.efunstudio.psdreader.PsdParser;
using Object = UnityEngine.Object;

namespace UGF.EditorTools.Psd2UGUI
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(PsdLayerNode))]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    public class PsdLayerNodeInspector : Editor
    {
        private const float CopyButtonWidth = 52f;
        //private static readonly GUIStyle ReadOnlyTextAreaStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
        PsdLayerNode targetLogic;
        private void OnEnable()
        {
            targetLogic = target as PsdLayerNode;
            targetLogic.RefreshLayerTexture();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            var newUIType = (GUIType)EditorGUILayout.EnumPopup("UI Type", targetLogic.UIType);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(targets, "Change UI Type");

                foreach (var item in targets)
                {
                    if (item == null) continue;
                    var layerNode = item as PsdLayerNode;
                    if (layerNode == null) continue;
                    layerNode.SetUIType(newUIType, true, true);
                    EditorUtility.SetDirty(layerNode);
                }
            }
            if (targetLogic.ManualUITypeOverride)
            {
                EditorGUILayout.HelpBox("当前 UI Type 已被人工锁定，manifest 后处理不会再自动覆盖。", MessageType.Info);
                if (GUILayout.Button("解除人工类型锁定"))
                {
                    Undo.RecordObjects(targets, "Clear Manual UI Type Override");
                    foreach (var item in targets)
                    {
                        var layerNode = item as PsdLayerNode;
                        if (layerNode == null) continue;
                        layerNode.ClearManualUITypeOverride();
                        EditorUtility.SetDirty(layerNode);
                    }
                }
            }
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("自动压缩图片:", GUILayout.Width(150));
                    Psd2UIFormSettings.Instance.CompressImage = EditorGUILayout.Toggle(Psd2UIFormSettings.Instance.CompressImage);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("裁剪超出画布图层:", GUILayout.Width(150));
                    Psd2UIFormSettings.Instance.EnableCanvasClipping = EditorGUILayout.Toggle(Psd2UIFormSettings.Instance.EnableCanvasClipping);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("九宫格自动裁剪:", GUILayout.Width(150));
                    Psd2UIFormSettings.Instance.EnableNineSliceCrop = EditorGUILayout.Toggle(Psd2UIFormSettings.Instance.EnableNineSliceCrop);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("单独导出效果图:", GUILayout.Width(150));
                    Psd2UIFormSettings.Instance.ExportSeparateEffects = EditorGUILayout.Toggle(Psd2UIFormSettings.Instance.ExportSeparateEffects);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("导出图片"))
                {
                    foreach (var item in targets)
                    {
                        if (item == null) continue;

                        (item as PsdLayerNode)?.ExportImageAsset();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            if (targetLogic.IsMainUIType && GUILayout.Button("增量导出至界面"))
            {
                Psd2UIFormConverter.Instance.GenerateUIForm(targetLogic);
            }
            if (GUILayout.Button("导出prefab"))
            {
                Psd2UIFormConverter.Instance.ExportAsPrefab(targetLogic);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Layer Data", EditorStyles.boldLabel);
            var rect = targetLogic.LayerRect;
            DrawVector2FieldReadOnly("Position", rect.position);
            DrawVector2FieldReadOnly("Size", rect.size);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Copy", GUILayout.Width(CopyButtonWidth)))
            {
                CopyRectTransformClipboard(rect.position, rect.size);
            }
            EditorGUILayout.EndHorizontal();

            if (targetLogic.ParseTextLayerInfo(out var textInfo))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Text Data", EditorStyles.boldLabel);
                DrawReadOnlyField("Text Content", textInfo.Text);
                DrawReadOnlyField("Font Name", textInfo.FontName);
                DrawReadOnlyField("Font Size", textInfo.FontSize.ToString());
                DrawReadOnlyField("Font Style", textInfo.FontStyle.ToString());
                DrawColorFieldWithCopy("Color", textInfo.Color);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawVector2FieldReadOnly(string label, Vector2 value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Vector2Field(label, value);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private void CopyRectTransformClipboard(Vector2 positionValue, Vector2 sizeValue)
        {
            var temp = new GameObject("RectTransformClipboard", typeof(RectTransform));
            temp.hideFlags = HideFlags.HideAndDontSave;
            var rectTransform = temp.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = positionValue;
            rectTransform.sizeDelta = sizeValue;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
            ComponentUtility.CopyComponent(rectTransform);
            DestroyImmediate(temp);
        }


        private void DrawReadOnlyField(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(label, value);
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("Copy", GUILayout.Width(CopyButtonWidth)))
            {
                GUIUtility.systemCopyBuffer = value;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawColorFieldWithCopy(string label, Color color)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ColorField(label, color);
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("RGB", GUILayout.Width(CopyButtonWidth)))
            {
                GUIUtility.systemCopyBuffer = ColorUtility.ToHtmlStringRGB(color);
            }
            if (GUILayout.Button("A", GUILayout.Width(CopyButtonWidth)))
            {
                GUIUtility.systemCopyBuffer = color.a.ToString("0.###", CultureInfo.InvariantCulture);
            }
            EditorGUILayout.EndHorizontal();
        }


        public override bool HasPreviewGUI()
        {
            var layerNode = (target as PsdLayerNode);
            return layerNode != null && layerNode.PreviewTexture != null;
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            var layerNode = (target as PsdLayerNode);
            GUI.DrawTexture(r, layerNode.PreviewTexture, ScaleMode.ScaleToFit);
            //base.OnPreviewGUI(r, background);
        }
        public override string GetInfoString()
        {
            var layerNode = (target as PsdLayerNode);
            return layerNode.LayerInfo;
        }
    }
    [CanEditMultipleObjects]
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    public class PsdLayerNode : MonoBehaviour
    {
        /// <summary>批量导出时缓存的资产处理信息</summary>
        internal struct BatchExportInfo
        {
            public string assetPath;
            public bool isSprite;
            public bool didCrop;
            public Vector4 croppedBorder;
            public int imageWidth;
            public int imageHeight;
            public bool preferHighBitDepth;
        }

        /// <summary>批量导出时缓存的信息（ExportSprites 统一处理后清除）</summary>
        internal BatchExportInfo? _batchExportInfo;

        [ReadOnlyField] public int BindPsdLayerIndex = -1;
        [ReadOnlyField][SerializeField] PsdLayerType mLayerType = PsdLayerType.Unknown;
        [ReadOnlyField][SerializeField] string sourceLayerName;
        [SerializeField] public bool markToExport;
        [HideInInspector][SerializeField] string generatedNodeId;
        [HideInInspector] public GUIType UIType;
        [HideInInspector] public bool ManualUITypeOverride;
        [HideInInspector][SerializeField] bool collapseChildrenForGeneration;
        [HideInInspector][SerializeField] int textSourceBindPsdLayerIndex = -1;
        public Texture2D PreviewTexture { get; private set; }
        public string LayerInfo { get; private set; }
        public Rect LayerRect { get; private set; }
        public PsdLayerType LayerType { get => mLayerType; }
        public string SourceLayerName => string.IsNullOrWhiteSpace(sourceLayerName)
            ? BindPsdLayer?.GetDisplayName() ?? gameObject?.name ?? string.Empty
            : sourceLayerName;
        internal string GeneratedNodeId => generatedNodeId;
        internal bool CollapseChildrenForGeneration => collapseChildrenForGeneration;
        public bool IsMainUIType => UGUIParser.IsMainUIType(UIType);
        public bool HasReuseReference => !string.IsNullOrEmpty(ReuseTargetKey);
        public bool HasReusePrefabReference => !string.IsNullOrEmpty(ReusePrefabKey);
        public string ReuseTargetKey => TryGetReuseTargetName(out var targetName) ? NormalizeLayerName(targetName) : null;
        public string ReuseTargetDisplayName => TryGetReuseTargetName(out var targetName) ? targetName : null;
        public string ReusePrefabKey => TryGetReusePrefabName(out var targetName) ? NormalizePrefabName(targetName) : null;
        public string ReusePrefabDisplayName => TryGetReusePrefabName(out var targetName) ? targetName : null;
        public string LayerNameLookupKey => NormalizeLayerName(gameObject?.name, preserveRefTags: true);
        private WeakReference<PsdLayerNode> reuseTargetRef = null;
        private string previewCacheKey;
        /// <summary>
        /// 绑定的psd图层
        /// </summary>
        private PsdLayer mBindPsdLayer;
        private PsdLayer mTextSourcePsdLayer;

        public PsdLayer BindPsdLayer
        {
            get => mBindPsdLayer;
            set
            {
                if (!ReferenceEquals(mBindPsdLayer, value))
                {
                    ReleasePreviewTexture();
                }
                mBindPsdLayer = value;
                if (mBindPsdLayer == null) return;
                mLayerType = mBindPsdLayer.GetLayerType();
                //if (IsTextLayer(out var txtLayer) && !txtLayer.TextBoundBox.IsEmpty)
                //{
                //    var txtRect = txtLayer.TextBoundBox;
                //    //txtRect.Width *= (float)txtLayer.TransformMatrix[3];
                //    //txtRect.Height *= (float)txtLayer.TransformMatrix[3];
                //    LayerRect = PsdParserExtensions.PsdRect2UnityRect(txtRect.Left, txtRect.Top, txtRect.Right, txtRect.Bottom, new Vector2Int(BindPsdLayer.Container.Width, BindPsdLayer.Container.Height));
                //}
                //else
                //{
                //    LayerRect = mBindPsdLayer.GetLayerRect();
                //}
                LayerRect = mBindPsdLayer.GetLayerRect();
                LayerInfo = $"{LayerRect}";
            }
        }
        internal static string NormalizeLayerName(string rawName, bool preserveRefTags = false)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return null;
            rawName = LayerNameUtility.ConvertChineseToLetters(rawName.Trim());
            var sanitized = preserveRefTags
                ? LayerNameUtility.SanitizePreserveRefTags(rawName)
                : LayerNameUtility.Sanitize(rawName);
            sanitized = StripKnownLayerTypeSuffix(sanitized);
            sanitized = sanitized.Replace(".", "_");
            return sanitized.ToLowerInvariant();
        }

        private static string StripKnownLayerTypeSuffix(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;

            var dotIndex = name.LastIndexOf('.');
            if (dotIndex < 0 || dotIndex == name.Length - 1)
                return name;

            var suffix = name.Substring(dotIndex + 1);
            if (Enum.TryParse(typeof(GUIType), suffix, true, out _)
                || string.Equals(suffix, "img", StringComparison.OrdinalIgnoreCase)
                || string.Equals(suffix, "bt", StringComparison.OrdinalIgnoreCase)
                || string.Equals(suffix, "txt", StringComparison.OrdinalIgnoreCase))
            {
                return name.Substring(0, dotIndex);
            }

            return name;
        }
        internal static string NormalizePrefabName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return null;
            return LayerNameUtility.Sanitize(rawName.Trim());
        }
        private bool TryGetReuseTargetName(out string targetName)
        {
            targetName = null;
            var workingName = gameObject?.name;
            if (string.IsNullOrWhiteSpace(workingName)) return false;

            workingName = workingName.Trim();
            if (workingName.StartsWith(UGUIParser.REF_TAG, StringComparison.OrdinalIgnoreCase))
            {
                targetName = workingName.Substring(UGUIParser.REF_TAG.Length).Trim();
            }
            else if (workingName.StartsWith("@ref:", StringComparison.OrdinalIgnoreCase))
            {
                targetName = workingName.Substring("@ref:".Length).Trim();
            }
            else if (workingName.StartsWith("ref_", StringComparison.OrdinalIgnoreCase) ||
                     workingName.StartsWith("ref.", StringComparison.OrdinalIgnoreCase))
            {
                targetName = workingName.Substring("ref_".Length).Trim();
            }
            else
            {
                return false;
            }

            return !string.IsNullOrEmpty(targetName);
        }
        private bool TryGetReusePrefabName(out string targetName)
        {
            targetName = null;
            var workingName = gameObject?.name;
            if (string.IsNullOrWhiteSpace(workingName)) return false;

            workingName = workingName.Trim();
            if (!workingName.StartsWith(UGUIParser.REF_PREFAB_TAG, StringComparison.OrdinalIgnoreCase)) return false;

            targetName = workingName.Substring(UGUIParser.REF_PREFAB_TAG.Length).Trim();
            return !string.IsNullOrEmpty(targetName);
        }
        private void OnDestroy()
        {
            ReleasePreviewTexture();
        }
        public void SetUIType(GUIType uiType, bool triggerParseFunc = true, bool manualOverride = false)
        {
            var parser = UGUIParser.Instance;
            this.UIType = parser != null ? parser.ResolvePreferredUIType(uiType) : uiType;
            if (manualOverride)
            {
                ManualUITypeOverride = true;
            }
            RemoveUIHelper();

            if (triggerParseFunc)
            {
                RefreshUIHelper(true);
            }
        }
        public void ClearManualUITypeOverride()
        {
            ManualUITypeOverride = false;
            EditorUtility.SetDirty(this);
        }
        internal void SetSourceLayerName(string layerName)
        {
            sourceLayerName = layerName ?? string.Empty;
        }
        internal void ConfigureGeneratedGroupNode(string layerName, Rect layerRect, string nodeId = null)
        {
            BindPsdLayerIndex = -1;
            markToExport = false;
            ManualUITypeOverride = false;
            collapseChildrenForGeneration = false;
            textSourceBindPsdLayerIndex = -1;
            mLayerType = PsdLayerType.LayerGroup;
            sourceLayerName = layerName ?? string.Empty;
            generatedNodeId = nodeId ?? string.Empty;
            UpdateLayerRectData(layerRect);
        }
        internal void SetGeneratedNodeId(string nodeId)
        {
            generatedNodeId = nodeId ?? string.Empty;
        }
        internal void UpdateLayerRectData(Rect layerRect)
        {
            LayerRect = layerRect;
            LayerInfo = layerRect.ToString();
        }
        internal void SetGroupGenerationState(bool collapseChildren, int textSourceIndex)
        {
            collapseChildrenForGeneration = collapseChildren;
            textSourceBindPsdLayerIndex = textSourceIndex;
            mTextSourcePsdLayer = null;
            if (textSourceBindPsdLayerIndex >= 0)
            {
                mTextSourcePsdLayer = BindPsdLayer?.Document?.GetLayerByFlatIndex(textSourceBindPsdLayerIndex);
            }
        }
        internal PsdLayerNode FindNearestCompatibleOwner()
        {
            if (!UGUIParser.IsSemanticUIType(UIType))
            {
                return FindNearestCompatibleMainOwner();
            }

            var current = transform.parent;
            while (current != null)
            {
                var owner = current.GetComponent<PsdLayerNode>();
                if (owner == null)
                {
                    current = current.parent;
                    continue;
                }
                if (UGUIParser.CanOwnSemanticRole(owner.UIType, UIType))
                {
                    return owner;
                }
                if (!UGUIParser.IsTransparentContainerType(owner.UIType))
                {
                    return null;
                }
                current = current.parent;
            }
            return null;
        }
        private PsdLayerNode FindNearestCompatibleMainOwner()
        {
            var current = transform.parent;
            while (current != null)
            {
                var owner = current.GetComponent<PsdLayerNode>();
                if (owner == null)
                {
                    current = current.parent;
                    continue;
                }
                if (UGUIParser.CanOwnMainChild(owner.UIType, UIType))
                {
                    return owner;
                }
                if (!UGUIParser.IsTransparentContainerType(owner.UIType))
                {
                    return null;
                }
                current = current.parent;
            }
            return null;
        }
        public void RefreshUIHelper(bool refreshParent = false)
        {
            var parser = UGUIParser.Instance;
            if (parser != null)
            {
                UIType = parser.ResolvePreferredUIType(UIType);
            }
            if (UIType == GUIType.Null) return;

            var uiHelperTp = UGUIParser.Instance.GetHelperType(UIType);
            if (uiHelperTp != null)
            {
                var helper = (gameObject.GetComponent(uiHelperTp) ?? gameObject.AddComponent(uiHelperTp)) as UIHelperBase;
                helper.ParseAndAttachUIElements();
            }
            if (refreshParent)
            {
                var currentNode = transform.parent;
                while (currentNode != null)
                {
                    var parentHelper = currentNode.GetComponent<UIHelperBase>();
                    if (parentHelper != null)
                    {
                        parentHelper.ParseAndAttachUIElements();
                        break;
                    }
                    currentNode = currentNode.parent;
                }

            }
            EditorUtility.SetDirty(this);
        }
        private void RemoveUIHelper()
        {
            var uiHelpers = this.GetComponents<UIHelperBase>();
            if (uiHelpers != null)
            {
                foreach (var uiHelper in uiHelpers)
                {
                    DestroyImmediate(uiHelper);
                }
            }
            EditorUtility.SetDirty(this);
        }
        /// <summary>
        /// 是否需要导出此图层
        /// </summary>
        /// <returns></returns>
        public bool NeedExportImage()
        {
            if (UIType == GUIType.FillColor || LayerType == PsdLayerType.FillLayer)
            {
                return gameObject.activeSelf && ShouldExportFillLayerAsImage();
            }

            return gameObject.activeSelf && markToExport;
        }

        internal bool ShouldExportFillLayerAsImage()
        {
            if (UIType != GUIType.FillColor && LayerType != PsdLayerType.FillLayer)
            {
                return false;
            }

            if (!RefreshLayerTexture(false) || PreviewTexture == null)
            {
                return false;
            }

            var pixels = PreviewTexture.GetPixels32();
            if (pixels == null || pixels.Length == 0)
            {
                return false;
            }

            var reference = pixels[0];
            const int colorTolerance = 2;
            const int alphaTolerance = 2;
            for (int i = 1; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                if (Mathf.Abs(pixel.a - reference.a) > alphaTolerance
                    || Mathf.Abs(pixel.r - reference.r) > colorTolerance
                    || Mathf.Abs(pixel.g - reference.g) > colorTolerance
                    || Mathf.Abs(pixel.b - reference.b) > colorTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetSanitizedExportName(string rawName, bool convertFileNameToLower, string fallbackName)
        {
            var sanitized = LayerNameUtility.Sanitize(rawName);
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                sanitized = LayerNameUtility.Sanitize(fallbackName);
            }
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                sanitized = "PsdLayer";
            }
            if (convertFileNameToLower)
            {
                sanitized = sanitized.ToLowerInvariant();
            }
            return sanitized;
        }

        private static string GetLayerExportBaseName(PsdLayerNode node, bool convertFileNameToLower)
        {
            if (node == null) return "PsdLayer";
            var rawName = string.IsNullOrWhiteSpace(node.name) ? node.UIType.ToString() : node.name;
            return GetSanitizedExportName(rawName, convertFileNameToLower, node.UIType.ToString());
        }

        private string ResolveDuplicateExportName(Psd2UIFormConverter converter, string desiredName, bool convertFileNameToLower)
        {
            if (converter == null || string.IsNullOrWhiteSpace(desiredName)) return desiredName;

            var allNodes = converter.GetComponentsInChildren<PsdLayerNode>(true);
            if (allNodes == null || allNodes.Length == 0) return desiredName;

            var exportNodes = allNodes
                .Where(node => node != null && (node == this || node.NeedExportImage()))
                .ToArray();
            if (exportNodes.Length <= 1) return desiredName;

            var desiredNames = exportNodes
                .Select(node => new { Node = node, Name = GetLayerExportBaseName(node, convertFileNameToLower) })
                .ToArray();

            var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in desiredNames)
            {
                reservedNames.Add(entry.Name);
            }

            var duplicates = desiredNames
                .Where(entry => string.Equals(entry.Name, desiredName, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Node)
                .OrderBy(node => node.BindPsdLayerIndex)
                .ThenBy(node => node.GetInstanceID())
                .ToArray();

            if (duplicates.Length <= 1) return desiredName;

            var position = Array.IndexOf(duplicates, this);
            if (position <= 0) return desiredName;

            int suffixNumber = 1;
            for (int duplicateIndex = 1; duplicateIndex <= position; duplicateIndex++)
            {
                while (reservedNames.Contains($"{desiredName}_{suffixNumber}"))
                {
                    suffixNumber++;
                }
                if (duplicateIndex == position)
                {
                    return $"{desiredName}_{suffixNumber}";
                }
                suffixNumber++;
            }

            return desiredName;
        }
        /// <summary>
        /// 导出图片（批量模式下只写文件，不触发 AssetDatabase 操作）
        /// </summary>
        /// <param name="forceSpriteType">强制贴图类型为Sprite</param>
        /// <returns></returns>
        public string ExportImageAsset(bool forceSpriteType = false, string overrideExportDir = null, string overrideFileName = null, bool convertFileNameToLower = true, bool auto9Slice = false, bool ignoreReference = false, bool batchMode = false)
        {
            var converter = Psd2UIFormConverter.Instance;
            bool preferHighBitDepth = BindPsdLayer?.Document?.Depth == 16;
            if (converter != null && converter.TryGetSharedSpritePath(this, forceSpriteType, out var sharedPath))
            {
                return sharedPath;
            }
            bool useSharedOverride = false;
            string sharedOverrideKey = null;
            string exportDir = overrideExportDir;
            string fileName = overrideFileName;
            if (converter != null && string.IsNullOrEmpty(exportDir) && !HasReuseReference)
            {
                if (converter.TryOverrideSharedOutput(this, out var sharedDir, out var sharedFile))
                {
                    exportDir = sharedDir;
                    fileName = sharedFile;
                    useSharedOverride = true;
                    sharedOverrideKey = sharedFile;
                    forceSpriteType = true;
                }
            }
            if (!ignoreReference && HasReuseReference && converter != null)
            {
                return converter.GetOrCreateSharedSprite(this);
            }
            string assetName = null;
            if ((UIType == GUIType.FillColor || LayerType == PsdLayerType.FillLayer)
                && !ShouldExportFillLayerAsImage())
            {
                return null;
            }
            // 计算最终文件路径（提前计算以支持文件存在性检查）
            string earlyExportDir = string.IsNullOrWhiteSpace(exportDir) ? Psd2UIFormConverter.Instance?.GetUIFormImagesOutputDir() : exportDir;
            if (!string.IsNullOrWhiteSpace(earlyExportDir))
            {
                var earlyImgName = string.IsNullOrWhiteSpace(fileName)
                    ? ResolveDuplicateExportName(converter, GetLayerExportBaseName(this, convertFileNameToLower), convertFileNameToLower)
                    : GetSanitizedExportName(Path.GetFileNameWithoutExtension(fileName?.Trim()), convertFileNameToLower, GetLayerExportBaseName(this, convertFileNameToLower));
                var earlyImgFileName = Path.Combine(earlyExportDir, earlyImgName + ".png").Replace("\\", "/");
                // 非批量模式下，如果文件已经存在且已导入为资产，直接返回路径
                if (!batchMode && File.Exists(earlyImgFileName)
                    && PsdTextureAssetUtility.MatchesExportMode(earlyImgFileName, preferHighBitDepth))
                {
                    if (useSharedOverride && converter != null)
                    {
                        converter.RegisterSharedSprite(string.IsNullOrEmpty(sharedOverrideKey) ? LayerNameLookupKey : sharedOverrideKey, earlyImgFileName);
                    }
                    return earlyImgFileName;
                }
            }
            // ── 导出前重置 LayerRect，确保与当前设置一致 ──
            // GetLayerRect() 根据 EnableCanvasClipping 决定是否裁剪到画布
            // ConvertPsdLayer2Texture2D() 裁剪纹理时也会同步更新 LayerRect
            // 必须在 RefreshLayerTexture 之前重置，避免上次导出的残留值影响
            if (BindPsdLayer != null)
            {
                LayerRect = mBindPsdLayer.GetLayerRect();
            }
            if (this.RefreshLayerTexture(true))
            {
                Debug.Log($"[ImageExport] ════════ 开始导出图层='{name}' ════════\n" +
                    $"  LayerType={mLayerType}, UIType={UIType}, Rect={LayerRect}\n" +
                    $"  PreviewTexture={PreviewTexture.width}x{PreviewTexture.height}\n" +
                    $"  BindPsdLayer={(BindPsdLayer != null ? $"'{BindPsdLayer.Name}' IsGroup={BindPsdLayer.IsGroup}" : "null")}\n" +
                    $"  forceSpriteType={forceSpriteType}, batchMode={batchMode}, auto9Slice={auto9Slice}");
                var bytes = PsdTextureAssetUtility.EncodePng(PreviewTexture);
                exportDir = string.IsNullOrWhiteSpace(exportDir) ? Psd2UIFormConverter.Instance.GetUIFormImagesOutputDir() : exportDir;
                if (!Directory.Exists(exportDir))
                {
                    try
                    {
                        Directory.CreateDirectory(exportDir);
                        AssetDatabase.Refresh();
                    }
                    catch (System.Exception)
                    {
                        return null;
                    }
                }
                var imgName = string.IsNullOrWhiteSpace(fileName)
                    ? ResolveDuplicateExportName(converter, GetLayerExportBaseName(this, convertFileNameToLower), convertFileNameToLower)
                    : GetSanitizedExportName(Path.GetFileNameWithoutExtension(fileName?.Trim()), convertFileNameToLower, GetLayerExportBaseName(this, convertFileNameToLower));
                var imgFileName = Path.Combine(exportDir, imgName + ".png").Replace("\\", "/");
                // 九宫格裁剪：任一边大于512的图片尝试裁剪拉伸区域的冗余像素
                Vector4 croppedBorder = Vector4.zero;
                bool didCrop = false;

                // ── 烘焙效果到原始图像中（描边/阴影/颜色叠加 → 图片变大一圈）──
                Texture2D bakedTex = null;
                int bakedExpandL = 0, bakedExpandT = 0;
                if (!preferHighBitDepth && BindPsdLayer != null
                    && mLayerType != PsdLayerType.TextLayer && mLayerType != PsdLayerType.FillLayer)
                {
                    Debug.Log($"[ImageExport] 尝试烘焙效果: 图层='{name}'");
                    bakedTex = LayerEffectsBaker.GenerateBakedTexture(PreviewTexture, BindPsdLayer, out bakedExpandL, out bakedExpandT);
                    if (bakedTex != null)
                    {
                        Debug.Log($"[ImageExport:Baked] 图层='{name}', 原始={PreviewTexture.width}x{PreviewTexture.height}, 烘焙={bakedTex.width}x{bakedTex.height}, expand=L{bakedExpandL}/T{bakedExpandT}");
                        bytes = PsdTextureAssetUtility.EncodePng(bakedTex);
                        // 记录烘焙扩展信息，供后续定位使用
                        BakedExpandLeft = bakedExpandL;
                        BakedExpandTop = bakedExpandT;

                        // 更新 LayerRect：烘焙后纹理变大=效果扩展，Rect 需要同步扩大
                        // LayerRect 的 x/y 是中心点坐标，扩展后中心偏移 = (右扩-左扩)/2, (上扩-下扩)/2
                        int expandR = bakedTex.width - PreviewTexture.width - bakedExpandL;
                        int expandB = bakedTex.height - PreviewTexture.height - bakedExpandT;
                        var oldRect = LayerRect;
                        LayerRect = new Rect(
                            oldRect.x + (expandR - bakedExpandL) * 0.5f,
                            oldRect.y + (bakedExpandT - expandB) * 0.5f,
                            bakedTex.width,
                            bakedTex.height);
                        Debug.Log($"[ImageExport:Baked] Rect更新: {oldRect} -> {LayerRect}");
                    }
                    else
                    {
                        Debug.Log($"[ImageExport:Baked] 图层='{name}' 无效果需要烘焙(返回null)");
                    }
                }

                // 九宫格裁剪（对烘焙后或原始纹理）
                var texForNineSlice = bakedTex ?? PreviewTexture;
                bool isComplexFillLayerImage = (UIType == GUIType.FillColor || LayerType == PsdLayerType.FillLayer)
                    && ShouldExportFillLayerAsImage();
                bool nineSliceEnabled = Psd2UIFormSettings.Instance.EnableNineSliceCrop
                    && !isComplexFillLayerImage && !preferHighBitDepth;
                bool nineSliceSizeOk = UGUIParser.IsNineSliceCandidate(texForNineSlice);
                bool nineSliceSemanticOk = !IsNineSliceExcludedByResourceName(name)
                    && !IsNineSliceExcludedByResourceName(imgName)
                    && !IsNineSliceExcludedByResourceName(BindPsdLayer?.Name);
                bool nineSliceCandidate = nineSliceEnabled && nineSliceSizeOk && nineSliceSemanticOk;
                Debug.Log($"[NineSlice] 图层={name}, size={texForNineSlice.width}x{texForNineSlice.height}, batchMode={batchMode}, enabled={nineSliceEnabled}, sizeOk={nineSliceSizeOk}, semanticOk={nineSliceSemanticOk}, candidate={nineSliceCandidate}, path={imgFileName}");
                if (nineSliceCandidate)
                {
                    var croppedTex = UGUIParser.CropNineSliceTexture(texForNineSlice, out croppedBorder);
                    Debug.Log($"[NineSlice] 图层={name}, CropResult={(croppedTex != null ? $"{croppedTex.width}x{croppedTex.height}" : "null")}, border={croppedBorder}");
                    if (croppedTex != null)
                    {
                        bytes = PsdTextureAssetUtility.EncodePng(croppedTex);
                        didCrop = true;
                        Object.DestroyImmediate(croppedTex);
                    }
                }

                if (bakedTex != null)
                    Object.DestroyImmediate(bakedTex);

                bool isImage = this.UIType != GUIType.RawImage
                    && (this.UIType != GUIType.FillColor || ShouldExportFillLayerAsImage());
                bool allowContentAddressedSharing = converter != null
                    && !useSharedOverride
                    && !HasReuseReference
                    && string.IsNullOrEmpty(overrideExportDir)
                    && string.IsNullOrEmpty(overrideFileName);
                bool reusedSharedAsset = false;

                if (allowContentAddressedSharing && converter.TryResolveContentAddressedSharedSprite(bytes, imgName, isImage || forceSpriteType, out var sharedAssetPath, out reusedSharedAsset))
                {
                    imgFileName = sharedAssetPath;
                    if (!reusedSharedAsset)
                    {
                        File.WriteAllBytes(imgFileName, bytes);
                        Debug.Log($"[ImageExport:Shared] 写入Common资源: {imgFileName}, hash复用名={imgName}");
                    }
                    else
                    {
                        Debug.Log($"[ImageExport:Shared] 复用Common资源: {imgFileName}, layer={name}");
                    }
                }
                else
                {
                    File.WriteAllBytes(imgFileName, bytes);
                    Debug.Log($"[ImageExport] 已写入文件: {imgFileName}, 字节数={bytes.Length}");
                    if (allowContentAddressedSharing)
                    {
                        converter.RegisterContentAddressedSharedSprite(bytes, imgFileName, imgName, isImage || forceSpriteType);
                    }
                }
                if (converter != null)
                {
                    converter.RegisterSharedSprite(LayerNameLookupKey, imgFileName);
                }

                // 导出独立效果图（描边/阴影），同时尝试九宫格裁剪
                if (Psd2UIFormSettings.Instance.ExportSeparateEffects)
                {
                    var fxAssetPath = ExportEffectsAsset(imgFileName, out var fxExL, out var fxExT, out var fxW, out var fxH);
                    if (fxAssetPath != null)
                    {
                        EffectsAssetPath = fxAssetPath;
                        EffectsExpandLeft = fxExL;
                        EffectsExpandTop = fxExT;
                        EffectsWidth = fxW;
                        EffectsHeight = fxH;
                        Debug.Log($"[ImageExport:Effects] 效果图导出成功: {fxAssetPath}, size={fxW}x{fxH}, expand=L{fxExL}/T{fxExT}");
                    }
                    else
                    {
                        Debug.Log($"[ImageExport:Effects] 无独立效果图: 图层='{name}'");
                    }
                }

                if (!reusedSharedAsset && Psd2UIFormSettings.Instance.CompressImage && !preferHighBitDepth)
                {
                    bool compressResult = Psd2UIFormConverter.CompressImageFile(imgFileName);
                    if (compressResult)
                    {
                        Debug.Log($"Compress image success:{imgFileName}");
                    }
                    else
                    {
                        Debug.LogWarning($"Compress image failed:{imgFileName}");
                    }
                }

                assetName = imgFileName;
                if (batchMode)
                {
                    // 批量模式：记录待处理的资产信息，不触发 AssetDatabase 操作
                    _batchExportInfo = new BatchExportInfo
                    {
                        assetPath = imgFileName,
                        isSprite = isImage || forceSpriteType,
                        didCrop = didCrop,
                        croppedBorder = croppedBorder,
                        imageWidth = PreviewTexture.width,
                        imageHeight = PreviewTexture.height,
                        preferHighBitDepth = preferHighBitDepth
                    };
                }
                else
                {
                    // 单文件模式: 立即处理
                    AssetDatabase.ImportAsset(imgFileName, ImportAssetOptions.ForceSynchronousImport);
                    Psd2UIFormConverter.ConvertTexturesType(new string[] { imgFileName }, isImage || forceSpriteType);
                    var precisionImporter = AssetImporter.GetAtPath(imgFileName) as TextureImporter;
                    if (precisionImporter != null)
                    {
                        PsdTextureAssetUtility.ApplyPrecisionImportSettings(precisionImporter, preferHighBitDepth);
                        precisionImporter.SaveAndReimport();
                    }
                    // 单文件模式下：只有确实裁剪成功（中间有重复像素）才设置九宫格 border
                    if (didCrop && (isImage || forceSpriteType))
                    {
                        var texImporter = AssetImporter.GetAtPath(imgFileName) as TextureImporter;
                        if (texImporter != null)
                        {
                            texImporter.spriteBorder = croppedBorder;
                            texImporter.SaveAndReimport();
                            Debug.Log($"[NineSlice] 单文件模式设置border: {imgFileName}, border={croppedBorder}");
                        }
                    }
                    else if (isImage || forceSpriteType)
                    {
                        var texImporter = AssetImporter.GetAtPath(imgFileName) as TextureImporter;
                        if (texImporter != null && texImporter.spriteBorder != Vector4.zero)
                        {
                            texImporter.spriteBorder = Vector4.zero;
                            texImporter.SaveAndReimport();
                            Debug.Log($"[NineSlice] 单文件模式清理旧border: {imgFileName}");
                        }
                    }
                }
                if (useSharedOverride && converter != null)
                {
                    converter.RegisterSharedSprite(string.IsNullOrEmpty(sharedOverrideKey) ? LayerNameLookupKey : sharedOverrideKey, imgFileName);
                }
                Debug.Log($"[ImageExport] ════════ 完成导出图层='{name}' ════════\n" +
                    $"  最终路径={imgFileName}\n" +
                    $"  hasBaked={BakedExpandLeft != 0 || BakedExpandTop != 0}, didCrop={didCrop}, croppedBorder={croppedBorder}\n" +
                    $"  hasEffects={EffectsAssetPath != null}, effectsPath={EffectsAssetPath ?? "无"}");
            }

            return assetName;
        }

        private static bool IsNineSliceExcludedByResourceName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return false;

            var normalized = rawName.Trim().ToLowerInvariant();
            normalized = normalized.Replace("@ref:", string.Empty).Replace("@", string.Empty);
            normalized = normalized.Replace(".png", string.Empty).Replace(".img", string.Empty);

            if (normalized.Contains("图标") || normalized.Contains("圖標"))
                return true;

            var tokens = normalized.Split(new[] { '_', '-', '.', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return tokens.Any(token => token == "icon" || token == "ico");
        }

        /// <summary>
        /// 导出图层效果图（描边/阴影），返回效果图 asset 路径。
        /// 效果图也会尝试九宫格裁剪。
        /// expandLeft / expandTop 返回效果图在PSD坐标系中相对于原图层左上角的偏移。
        /// </summary>
        public string ExportEffectsAsset(string baseAssetPath, out int expandLeft, out int expandTop,
            out int fxWidth, out int fxHeight)
        {
            expandLeft = 0;
            expandTop = 0;
            fxWidth = 0;
            fxHeight = 0;
            if (string.IsNullOrEmpty(baseAssetPath)) return null;
            if (BindPsdLayer == null || PreviewTexture == null) return null;
            // 效果导出仅针对图片图层（非文本、非填充）；组图层也允许（PS可对组加效果）
            if (mLayerType == PsdLayerType.TextLayer || mLayerType == PsdLayerType.FillLayer) return null;

            Debug.Log($"[EffectsExport] 尝试导出效果: 图层='{name}', type={mLayerType}, base={baseAssetPath}");

            try
            {
                var fxTex = LayerEffectsBaker.GenerateEffectsTexture(
                    PreviewTexture, BindPsdLayer,
                    out expandLeft, out expandTop, out fxWidth, out fxHeight);
                if (fxTex == null) return null;

                string dir = Path.GetDirectoryName(baseAssetPath).Replace("\\", "/");
                string baseName = Path.GetFileNameWithoutExtension(baseAssetPath);
                string fxPath = Path.Combine(dir, baseName + "_effect.png").Replace("\\", "/");

                // 尝试九宫格裁剪效果图
                byte[] bytes;
                Vector4 fxBorder = Vector4.zero;
                bool fxDidCrop = false;
                if (Psd2UIFormSettings.Instance.EnableNineSliceCrop && UGUIParser.IsNineSliceCandidate(fxTex))
                {
                    var croppedFx = UGUIParser.CropNineSliceTexture(fxTex, out fxBorder);
                    if (croppedFx != null)
                    {
                        Debug.Log($"[NineSlice:Effect] 效果图裁剪成功: {fxTex.width}x{fxTex.height} -> {croppedFx.width}x{croppedFx.height}, border={fxBorder}");
                        bytes = croppedFx.EncodeToPNG();
                        fxDidCrop = true;
                        fxWidth = croppedFx.width;
                        fxHeight = croppedFx.height;
                        DestroyImmediate(croppedFx);
                    }
                    else
                    {
                        bytes = fxTex.EncodeToPNG();
                    }
                }
                else
                {
                    bytes = fxTex.EncodeToPNG();
                }

                DestroyImmediate(fxTex);
                File.WriteAllBytes(fxPath, bytes);

                // 设置效果图为 Sprite 并应用九宫格 border
                if (fxDidCrop)
                {
                    AssetDatabase.ImportAsset(fxPath, ImportAssetOptions.ForceSynchronousImport);
                    Psd2UIFormConverter.ConvertTexturesType(new string[] { fxPath }, true);
                    var fxImporter = AssetImporter.GetAtPath(fxPath) as TextureImporter;
                    if (fxImporter != null)
                    {
                        fxImporter.spriteBorder = fxBorder;
                        fxImporter.SaveAndReimport();
                        Debug.Log($"[NineSlice:Effect] 设置效果图border: {fxPath}, border={fxBorder}");
                    }
                }

                Debug.Log($"[EffectsBaker] 导出效果图: {fxPath} ({fxWidth}x{fxHeight}), expand=L{expandLeft}/T{expandTop}");
                return fxPath;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[EffectsBaker] 导出效果图失败 图层={name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>缓存的效果图信息</summary>
        internal string EffectsAssetPath { get; set; }
        internal int EffectsExpandLeft { get; set; }
        internal int EffectsExpandTop { get; set; }
        internal int EffectsWidth { get; set; }
        internal int EffectsHeight { get; set; }

        /// <summary>烘焙纹理（原始+效果）的扩展偏移</summary>
        internal int BakedExpandLeft { get; set; }
        internal int BakedExpandTop { get; set; }

        public bool RefreshLayerTexture(bool forceRefresh = false)
        {
            var currentCacheKey = ResolvePreviewCacheKey();
            if (string.IsNullOrEmpty(currentCacheKey))
            {
                ReleasePreviewTexture();
                return false;
            }

            var cacheKeyMatches = PreviewTexture != null
                && !string.IsNullOrEmpty(previewCacheKey)
                && string.Equals(previewCacheKey, currentCacheKey, StringComparison.Ordinal);
            if (!forceRefresh && cacheKeyMatches)
            {
                return true;
            }

            if (BindPsdLayer == null) return false;

            if (PreviewTexture != null)
            {
                ReleasePreviewTexture();
            }

            PreviewTexture = PsdLayerPreviewCache.Acquire(currentCacheKey, ConvertPsdLayer2Texture2D);
            if (PreviewTexture != null)
            {
                previewCacheKey = currentCacheKey;
            }
            return PreviewTexture != null;
        }

        private string ResolvePreviewCacheKey()
        {
            if (BindPsdLayer == null)
            {
                return string.Empty;
            }

            var converter = Psd2UIFormConverter.Instance;
            var assetPath = converter != null && converter.BindPsdAsset != null
                ? AssetDatabase.GetAssetPath(converter.BindPsdAsset)
                : string.Empty;
            var sourceKey = string.IsNullOrWhiteSpace(assetPath)
                ? $"memory:{BindPsdLayer.GetHashCode()}"
                : assetPath.Replace('\\', '/').ToLowerInvariant();

            return string.Join(
                "|",
                "PsdLayerPreview",
                sourceKey,
                ResolveAssetChangeTag(assetPath),
                BindPsdLayerIndex.ToString(CultureInfo.InvariantCulture),
                BindPsdLayer.Left.ToString(CultureInfo.InvariantCulture),
                BindPsdLayer.Top.ToString(CultureInfo.InvariantCulture),
                BindPsdLayer.Right.ToString(CultureInfo.InvariantCulture),
                BindPsdLayer.Bottom.ToString(CultureInfo.InvariantCulture),
                Psd2UIFormSettings.Instance.EnableCanvasClipping ? "clip" : "full");
        }

        private static string ResolveAssetChangeTag(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || !File.Exists(assetPath))
            {
                return string.Empty;
            }

            var fileInfo = new FileInfo(assetPath);
            return $"{fileInfo.Length}:{fileInfo.LastWriteTimeUtc.Ticks}";
        }

        private void ReleasePreviewTexture()
        {
            if (!string.IsNullOrWhiteSpace(previewCacheKey))
            {
                var wasManaged = PsdLayerPreviewCache.Release(previewCacheKey);
                if (!wasManaged && PreviewTexture != null)
                {
                    DestroyImmediate(PreviewTexture);
                }
            }
            else if (PreviewTexture != null)
            {
                DestroyImmediate(PreviewTexture);
            }

            PreviewTexture = null;
            previewCacheKey = string.Empty;
        }

        /// <summary>
        /// 把psd图层转成Texture2D
        /// </summary>
        /// <param name="psdLayer"></param>
        /// <returns>Texture2D</returns>
        public Texture2D ConvertPsdLayer2Texture2D()
        {
            if (BindPsdLayer == null) return null;

            var rendered = BindPsdLayer.Render();
            if (rendered == null || rendered.IsEmpty)
            {
                return null;
            }

            var textureFormat = rendered.IsHighBitDepth ? TextureFormat.RGBA64 : TextureFormat.RGBA32;
            var texture = new Texture2D(rendered.Width, rendered.Height, textureFormat, false);
            texture.alphaIsTransparency = true;
            if (rendered.IsHighBitDepth)
            {
                var rawData = new byte[rendered.Rgba64.Length * sizeof(ushort)];
                Buffer.BlockCopy(rendered.Rgba64, 0, rawData, 0, rawData.Length);
                texture.LoadRawTextureData(rawData);
            }
            else
            {
                // Rgba32 的 byte 布局与 RGBA32 纹理格式一致，Y 轴已翻转匹配 Unity。
                texture.LoadRawTextureData(rendered.Rgba32);
            }
            texture.Apply();

            // 裁剪超出PSD画布范围的部分（开关控制）
            var converter = Psd2UIFormConverter.Instance;
            if (Psd2UIFormSettings.Instance.EnableCanvasClipping && converter != null && converter.Initiated)
            {
                var canvasSize = converter.UIFormCanvasSize;
                int layerLeft = BindPsdLayer.Left;
                int layerTop = BindPsdLayer.Top;
                int layerRight = layerLeft + rendered.Width;
                int layerBottom = layerTop + rendered.Height;

                // 检查是否超出画布
                if (layerLeft < 0 || layerTop < 0 || layerRight > canvasSize.x || layerBottom > canvasSize.y)
                {
                    int visLeft = Mathf.Max(layerLeft, 0);
                    int visTop = Mathf.Max(layerTop, 0);
                    int visRight = Mathf.Min(layerRight, canvasSize.x);
                    int visBottom = Mathf.Min(layerBottom, canvasSize.y);
                    int visW = visRight - visLeft;
                    int visH = visBottom - visTop;

                    if (visW <= 0 || visH <= 0)
                    {
                        DestroyImmediate(texture);
                        return null;
                    }

                    // 在图层局部坐标中的偏移
                    int localX = visLeft - layerLeft;
                    int localTopY = visTop - layerTop;
                    // 转换为Unity纹理坐标 (y=0在底部)
                    int unityY = rendered.Height - localTopY - visH;

                    var visPixels = texture.GetPixels(localX, unityY, visW, visH);
                    var clipped = new Texture2D(visW, visH, textureFormat, false);
                    clipped.alphaIsTransparency = true;
                    clipped.SetPixels(visPixels);
                    clipped.Apply();
                    DestroyImmediate(texture);

                    // 更新 LayerRect 以反映裁剪后的实际尺寸和位置
                    LayerRect = PsdParserExtensions.PsdRect2UnityRect(visLeft, visTop, visRight, visBottom, canvasSize);
                    Debug.Log($"[Canvas裁剪] 图层={name}, 原始={layerRight - layerLeft}x{layerBottom - layerTop}@({layerLeft},{layerTop}), 裁剪后={visW}x{visH}, 新Rect={LayerRect}");
                    return clipped;
                }
            }

            return texture;
        }

        internal bool TryGetLayerColor(out Color color)
        {
            color = default;
            if (BindPsdLayer == null)
            {
                return false;
            }

            if (BindPsdLayer.TryGetSolidFillColor(out var solidFillColor))
            {
                color = ConvertPsdColor(solidFillColor);
                return true;
            }

            // 如果已有预览纹理，从纹理取色，避免重复调用 Render()
            if (PreviewTexture != null)
            {
                int cx = PreviewTexture.width / 2;
                int cy = PreviewTexture.height / 2;
                var px = PreviewTexture.GetPixel(cx, cy);
                if (px.a > 0f) { color = px; return true; }
                // 中心透明时扫描找一个非透明像素
                var pixels = PreviewTexture.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].a > 0) { color = pixels[i]; return true; }
                }
                return false;
            }

            var rendered = BindPsdLayer.Render();
            if (rendered == null || rendered.IsEmpty || rendered.Rgba32 == null || rendered.Rgba32.Length < 4)
            {
                return false;
            }

            int targetX = rendered.Width / 2;
            int targetY = rendered.Height / 2;
            int targetOffset = (((rendered.Height - 1 - targetY) * rendered.Width) + targetX) * 4;
            if (targetOffset + 3 >= rendered.Rgba32.Length || rendered.Rgba32[targetOffset + 3] == 0)
            {
                targetOffset = -1;
                for (int dataIndex = 0; dataIndex < rendered.Rgba32.Length; dataIndex += 4)
                {
                    if (rendered.Rgba32[dataIndex + 3] > 0)
                    {
                        targetOffset = dataIndex;
                        break;
                    }
                }
                if (targetOffset < 0)
                {
                    return false;
                }
            }

            color = new Color32(rendered.Rgba32[targetOffset], rendered.Rgba32[targetOffset + 1], rendered.Rgba32[targetOffset + 2], rendered.Rgba32[targetOffset + 3]);
            return true;
        }

        /// <summary>
        /// 从第一层子节点按类型查找LayerNode
        /// </summary>
        /// <param name="uiTp"></param>
        /// <returns></returns>
        public PsdLayerNode FindSubLayerNode(GUIType uiTp)
        {
            if (uiTp == GUIType.Null) return null;
            return FindSubLayerNodeRecursive(transform, uiTp);
        }
        /// <summary>
        /// 依次查找给定多个类型,返回最先找到的类型
        /// </summary>
        /// <param name="uiTps"></param>
        /// <returns></returns>
        public PsdLayerNode FindSubLayerNode(params GUIType[] uiTps)
        {
            foreach (var tp in uiTps)
            {
                var result = FindSubLayerNode(tp);
                if (result != null) return result;
            }
            return null;
        }
        internal bool TryGetReuseTargetAsset(out UnityEngine.Object targetObj)
        {
            targetObj = null;
            if (!HasReuseReference) return false;
            string targetPath = Path.Combine(UGUIParser.Instance.SharedAssetsOutput, $"{ReuseTargetKey}.png");
            targetObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetPath);
            return targetObj != null;
        }
        internal bool TryGetReusePrefabAsset(out GameObject targetPrefab)
        {
            targetPrefab = null;
            if (!HasReusePrefabReference) return false;
            var sharedDir = UGUIParser.Instance?.SharedPrefabOutput;
            if (string.IsNullOrWhiteSpace(sharedDir)) return false;

            sharedDir = sharedDir.Replace("\\", "/");
            var targetPath = Path.Combine(sharedDir, $"{ReusePrefabKey}.prefab").Replace("\\", "/");
            targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
            if (targetPrefab != null) return true;

            var searchKey = ReusePrefabKey;
            if (string.IsNullOrWhiteSpace(searchKey)) return false;

            var guids = AssetDatabase.FindAssets($"{searchKey} t:prefab", new string[] { sharedDir });
            if (guids == null || guids.Length == 0) return false;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path)) continue;
                var fileName = Path.GetFileNameWithoutExtension(path);
                if (!string.Equals(fileName, searchKey, StringComparison.OrdinalIgnoreCase)) continue;

                targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (targetPrefab != null) return true;
            }

            // fallback: try first match
            var firstPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            if (!string.IsNullOrWhiteSpace(firstPath))
            {
                targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(firstPath);
            }
            return targetPrefab != null;
        }
        internal bool TryGetReuseTarget(out PsdLayerNode target)
        {
            target = null;
            if (!HasReuseReference) return false;
            if (reuseTargetRef != null && reuseTargetRef.TryGetTarget(out target) && target != null)
            {
                return true;
            }
            var converter = Psd2UIFormConverter.Instance;
            if (converter == null) return false;
            target = converter.FindLayerNodeByKey(ReuseTargetKey);
            if (target != null)
            {
                reuseTargetRef = new WeakReference<PsdLayerNode>(target);
                return true;
            }
            return false;
        }
        public PsdLayerNode FindLayerNodeInChildren(GUIType uiTp)
        {
            var layers = GetComponentsInChildren<PsdLayerNode>(true);
            if (layers != null && layers.Length > 0)
            {
                return layers.FirstOrDefault(layer => layer.UIType == uiTp);
            }
            return null;
        }

        private PsdLayerNode FindSubLayerNodeRecursive(Transform current, GUIType uiTp)
        {
            if (current == null) return null;
            for (int i = 0; i < current.childCount; i++)
            {
                var child = current.GetChild(i);
                var childNode = child.GetComponent<PsdLayerNode>();
                if (childNode != null)
                {
                    if (childNode.UIType == uiTp)
                    {
                        return childNode;
                    }
                    if (childNode != this && childNode.IsMainUIType && childNode.UIType != GUIType.Null)
                    {
                        continue;
                    }
                }
                var result = FindSubLayerNodeRecursive(child, uiTp);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>
        /// 判断该图层是否为文本图层
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        public bool IsTextLayer(out PsdTextLayerInfo layer)
        {
            layer = null;
            var textSource = mTextSourcePsdLayer ?? BindPsdLayer;
            if (textSource == null)
            {
                Debug.LogWarning($"[PSD2UIForm:IsTextLayer] {name}: BindPsdLayer is null");
                return false;
            }

            bool result = textSource.TryGetTextLayerInfo(out layer);
            if (!result && LayerType == PsdLayerType.TextLayer)
            {
                Debug.LogWarning($"[PSD2UIForm:IsTextLayer] {name}: LayerType=TextLayer 但 TryGetTextLayerInfo 失败");
            }
            return result;
        }
        internal void InitPsdLayers(PsdDocument psdInstance)
        {
            mTextSourcePsdLayer = null;
            if (psdInstance == null)
            {
                BindPsdLayer = null;
                return;
            }
            if (BindPsdLayerIndex >= 0)
            {
                BindPsdLayer = psdInstance.GetLayerByFlatIndex(BindPsdLayerIndex);
            }
            if (textSourceBindPsdLayerIndex >= 0)
            {
                mTextSourcePsdLayer = psdInstance.GetLayerByFlatIndex(textSourceBindPsdLayerIndex);
            }
        }
        internal bool ParseTextLayerInfo(out TextLayerInfo textInfo)
        {
            textInfo = default;
            if (IsTextLayer(out var txtLayer))
            {
                textInfo = new TextLayerInfo()
                {
                    Text = null,
                    FontSize = 0,
                    IsAutoLineSpacing = true,
                    CharacterSpacing = 0f,
                    LineSpacing = 0f,
                    Color = Color.white,
                    FontStyle = FontStyle.Normal,
                    TMPFontStyle = TMPro.FontStyles.Normal,
                    FontName = null,
                    HasOutline = false,
                    OutlineColor = Color.clear,
                    OutlineSize = 0f,
                    TMPOutlineSize = 0f,
                    TMPOutlinePosition = TextLayerInfo.TMPOutlineMode.Center,
                    Strokes = new List<TMPTextStrokeEffect>(),
                    HasShadow = false,
                    ShadowColor = Color.clear,
                    ShadowOffset = Vector2.zero,
                    ShadowSpread = 0f,
                    ShadowSoftness = 0f,
                    HasGradient = false,
                    GradientMode = TMPTextGradientMode.None,
                    GradientTopColor = Color.white,
                    GradientBottomColor = Color.white,
                    GradientFirstColorPercent = 50f,
                    HasGlow = false,
                    GlowColor = Color.clear,
                    GlowOpacity = 0f,
                    GlowSize = 0f,
                    GlowSpread = 0f
                };
                textInfo.Text = txtLayer.Text;
                textInfo.FontSize = Mathf.Max(1, Mathf.FloorToInt(txtLayer.FontSize));
                textInfo.IsAutoLineSpacing = txtLayer.AutoLeading || txtLayer.Leading <= 0f;
                textInfo.Color = ConvertPsdColor(txtLayer.Color);
                bool hasBoldFontVariant = UGUIParser.IsBoldFontVariantName(txtLayer.FontName);
                bool isBold = txtLayer.FauxBold || hasBoldFontVariant;
                bool useTMPFauxBold = txtLayer.FauxBold;
                if (isBold && txtLayer.FauxItalic)
                {
                    textInfo.FontStyle = UnityEngine.FontStyle.BoldAndItalic;
                }
                else if (isBold)
                {
                    textInfo.FontStyle = UnityEngine.FontStyle.Bold;

                }
                else if (txtLayer.FauxItalic)
                {
                    textInfo.FontStyle = UnityEngine.FontStyle.Italic;
                }
                else
                {
                    textInfo.FontStyle = UnityEngine.FontStyle.Normal;
                }

                if (txtLayer.FauxItalic)
                {
                    textInfo.TMPFontStyle |= TMPro.FontStyles.Italic;
                }
                if (useTMPFauxBold)
                {
                    textInfo.TMPFontStyle |= TMPro.FontStyles.Bold;
                }
                if (txtLayer.Underline)
                {
                    textInfo.TMPFontStyle |= TMPro.FontStyles.Underline;
                }
                if (txtLayer.Strikethrough)
                {
                    textInfo.TMPFontStyle |= TMPro.FontStyles.Strikethrough;
                }
                textInfo.FontName = UGUIParser.NormalizeFontNameForImport(txtLayer.FontName);
                textInfo.CharacterSpacing = txtLayer.Tracking * 0.1f;
                // Keep PSD Leading as-is; each text component uses different spacing units.
                textInfo.LineSpacing = txtLayer.Leading;
                ParseTextLayerEffects(txtLayer, ref textInfo);
                return true;
            }
            return false;
        }

        private static void ParseTextLayerEffects(PsdTextLayerInfo txtLayer, ref TextLayerInfo textInfo)
        {
            if (txtLayer == null) return;

            if (txtLayer.Shadows != null && txtLayer.Shadows.Count > 0)
            {
                foreach (var shadow in txtLayer.Shadows)
                {
                    if (shadow != null && shadow.Enabled)
                    {
                        ApplyTextShadow(shadow, ref textInfo);
                        break;
                    }
                }
            }
            else if (txtLayer.Shadow != null && txtLayer.Shadow.Enabled)
            {
                ApplyTextShadow(txtLayer.Shadow, ref textInfo);
            }

            if (txtLayer.Strokes != null && txtLayer.Strokes.Count > 0)
            {
                foreach (var stroke in txtLayer.Strokes)
                {
                    if (stroke != null && stroke.Enabled)
                    {
                        ApplyTextStroke(stroke, ref textInfo);
                    }
                }
            }
            else if (txtLayer.Stroke != null && txtLayer.Stroke.Enabled)
            {
                ApplyTextStroke(txtLayer.Stroke, ref textInfo);
            }

            if (txtLayer.GradientOverlays != null)
            {
                foreach (var gradient in txtLayer.GradientOverlays)
                {
                    if (gradient != null && gradient.Enabled && TryApplyLinearGradient(gradient, ref textInfo))
                    {
                        break;
                    }
                }
            }

            if (txtLayer.OuterGlow != null && txtLayer.OuterGlow.Enabled)
            {
                textInfo.HasGlow = true;
                textInfo.GlowColor = ConvertPsdColor(txtLayer.OuterGlow.Color);
                textInfo.GlowOpacity = txtLayer.OuterGlow.Opacity;
                textInfo.GlowSize = txtLayer.OuterGlow.Size;
                textInfo.GlowSpread = txtLayer.OuterGlow.Spread;
            }
        }

        private static void ApplyTextShadow(PsdTextShadowInfo shadow, ref TextLayerInfo textInfo)
        {
            textInfo.HasShadow = true;
            textInfo.ShadowColor = ConvertPsdColor(shadow.Color, shadow.Opacity);
            textInfo.ShadowOffset = Quaternion.Euler(0, 0, shadow.Angle) * (Vector2.left * shadow.Distance);
            textInfo.ShadowSpread = Mathf.Max(0f, shadow.Spread);
            textInfo.ShadowSoftness = shadow.Blur;
        }

        private static void ApplyTextStroke(PsdTextStrokeInfo stroke, ref TextLayerInfo textInfo)
        {
            var effect = new TMPTextStrokeEffect
            {
                Color = ConvertPsdColor(stroke.Color, stroke.Opacity),
                Size = Mathf.Max(0f, stroke.Size),
                Position = ConvertTMPOutlineMode(stroke.Position)
            };
            textInfo.Strokes ??= new List<TMPTextStrokeEffect>();
            textInfo.Strokes.Add(effect);

            // UGUI 文本仍只支持一层描边，保留首层作为旧路径回退。
            if (textInfo.HasOutline)
            {
                return;
            }

            textInfo.HasOutline = true;
            textInfo.OutlineSize = CalculateUnityOutlineSize(stroke);
            textInfo.TMPOutlineSize = effect.Size;
            textInfo.TMPOutlinePosition = effect.Position;
            textInfo.OutlineColor = effect.Color;
        }

        private static bool TryApplyLinearGradient(PsdGradientOverlayInfo gradient, ref TextLayerInfo textInfo)
        {
            if (gradient.ColorStops == null || gradient.ColorStops.Count < 2
                || !string.Equals(gradient.Type, "Lnr ", StringComparison.Ordinal)
                   && !string.Equals(gradient.Type, "Lnr", StringComparison.Ordinal))
            {
                return false;
            }

            var stops = gradient.ColorStops
                .OrderBy(item => item.Location)
                .ToList();
            var start = ConvertPsdColor(stops[0].Color, gradient.Opacity);
            var end = ConvertPsdColor(stops[stops.Count - 1].Color, gradient.Opacity);
            if (gradient.Reverse)
            {
                var temporary = start;
                start = end;
                end = temporary;
            }

            float angle = Mathf.Repeat(gradient.Angle, 360f) * Mathf.Deg2Rad;
            float horizontal = Mathf.Cos(angle);
            float vertical = Mathf.Sin(angle);
            textInfo.HasGradient = true;
            textInfo.GradientFirstColorPercent = Mathf.Clamp(stops[0].Midpoint, 0f, 100f);

            if (Mathf.Abs(vertical) >= Mathf.Abs(horizontal))
            {
                textInfo.GradientMode = TMPTextGradientMode.Vertical;
                bool startsAtTop = vertical < 0f;
                textInfo.GradientTopColor = startsAtTop ? start : end;
                textInfo.GradientBottomColor = startsAtTop ? end : start;
            }
            else
            {
                textInfo.GradientMode = TMPTextGradientMode.Horizontal;
                bool startsAtLeft = horizontal > 0f;
                textInfo.GradientTopColor = startsAtLeft ? start : end;
                textInfo.GradientBottomColor = startsAtLeft ? end : start;
            }

            return true;
        }
        private static TextLayerInfo.TMPOutlineMode ConvertTMPOutlineMode(PsdTextStrokePosition position)
        {
            switch (position)
            {
                case PsdTextStrokePosition.Inside:
                    return TextLayerInfo.TMPOutlineMode.Inside;
                case PsdTextStrokePosition.Center:
                    return TextLayerInfo.TMPOutlineMode.Center;
                default:
                    return TextLayerInfo.TMPOutlineMode.Outside;
            }
        }
        private static float CalculateUnityOutlineSize(PsdTextStrokeInfo stroke)
        {
            if (stroke == null) return 0f;

            var rawSize = Mathf.Max(0f, stroke.Size);
            float effectSize;
            switch (stroke.Position)
            {
                case PsdTextStrokePosition.Inside:
                    effectSize = rawSize * 0.5f;
                    break;
                case PsdTextStrokePosition.Center:
                    effectSize = rawSize * 0.75f;
                    break;
                default: // Outside
                    effectSize = rawSize;
                    break;
            }
            // PSD 描边渲染比 UGUI Outline 更粗实，乘以补偿系数
            return Mathf.Ceil(effectSize * 1.5f);
        }

        private static Color ConvertPsdColor(PsdColor color, float opacityMultiplier = 1f)
        {
            float alpha = (color.A / 255f) * Mathf.Clamp01(opacityMultiplier);
            return new Color(color.R / 255f, color.G / 255f, color.B / 255f, alpha);
        }
    }
}

#endif
