/*
插件获取:
https://efunstudio.cn
https://shop106471535.taobao.com
*/

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using cn.efunstudio.psdreader;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    public enum GUIType
    {
        Null = 0,
        Image,
        RawImage,
        Text,
        Button,
        Dropdown,
        InputField,
        Toggle,
        Slider,
        ScrollView,
        Mask,
        FillColor, //纯色填充
        TMPText,
        TMPButton,
        TMPDropdown,
        TMPInputField,
        TMPToggle,
        Panel = 17,
        ToggleGroup,

        //UI的子类型, 以101开始。 0-100预留给UI类型, 新类型从尾部追加
        Background = 101, //通用背景

        //Button的子类型
        Button_Highlight,
        Button_Press,
        Button_Select,
        Button_Disable,
        Button_Text,

        //Dropdown/TMPDropdown的子类型
        Dropdown_Label,
        Dropdown_Arrow,

        //InputField/TMPInputField的子类型
        InputField_Placeholder,
        InputField_Text,

        //Toggle的子类型
        Toggle_Checkmark,
        Toggle_Label,

        //Slider的子类型
        Slider_Fill,
        Slider_Handle,

        //ScrollView的子类型
        ScrollView_Viewport, //列表可视区域的遮罩图
        ScrollView_HorizontalBarBG, //水平滑动栏背景
        ScrollView_HorizontalBar,//水平滑块
        ScrollView_VerticalBarBG, //垂直滑动栏背景
        ScrollView_VerticalBar, //垂直滑动块
    }
    [Serializable]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    public class UGUIParseRule
    {
        public GUIType UIType;
        public string UITypeDesc;
        public string[] TypeMatches; //类型匹配标识
        public GameObject UIPrefab; //UI模板
        public string UIHelper; //UIHelper类型全名
        public string Comment;//注释
    }
    [CustomEditor(typeof(UGUIParser))]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    public class UGUIParserEditor : Editor
    {
        private SerializedProperty readmeProperty;
        SerializedProperty defaultTextType;
        SerializedProperty defaultImageType;
        SerializedProperty forceUseTMP;
        SerializedProperty convertZh2En;
        SerializedProperty sharedAssetsOutput;
        SerializedProperty sharedPrefabOutput;
        SerializedProperty aiProviderConfig;
        private string[] textTypesDisplay;
        private int[] textTypes;
        private string[] imageTypesDisplay;
        private int[] imageTypes;
        string orderId;
        private void OnEnable()
        {
            readmeProperty = serializedObject.FindProperty("readmeDoc");
            defaultTextType = serializedObject.FindProperty("defaultTextType");
            defaultImageType = serializedObject.FindProperty("defaultImageType");
            forceUseTMP = serializedObject.FindProperty("forceUseTMP");
            convertZh2En = serializedObject.FindProperty("convertZh2En");
            sharedAssetsOutput = serializedObject.FindProperty("sharedAssetsOutput");
            sharedPrefabOutput = serializedObject.FindProperty("sharedPrefabOutput");
            aiProviderConfig = serializedObject.FindProperty("aiProviderConfig");
            var textEnums = new GUIType[] { GUIType.Text, GUIType.TMPText };
            textTypes = new int[textEnums.Length];
            textTypesDisplay = new string[textEnums.Length];
            for (int i = 0; i < textEnums.Length; i++)
            {
                var textEnum = textEnums[i];
                textTypes[i] = (int)textEnum;
                textTypesDisplay[i] = textEnum.ToString();
            }
            orderId = PsdReaderLicense.GetOrderId();
            var imageEnums = new GUIType[] { GUIType.Image, GUIType.RawImage };
            imageTypes = new int[imageEnums.Length];
            imageTypesDisplay = new string[imageEnums.Length];
            for (int i = 0; i < imageEnums.Length; i++)
            {
                var imageEnum = imageEnums[i];
                imageTypes[i] = (int)imageEnum;
                imageTypesDisplay[i] = imageEnum.ToString();
            }
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            if (GUILayout.Button("使用教程"))
            {
                Application.OpenURL("https://efunstudio.cn");
            }
            if (GUILayout.Button("导出使用文档"))
            {
                (target as UGUIParser).ExportReadmeDoc();
            }
            if (GUILayout.Button("导出Rules标签"))
            {
                (target as UGUIParser).ExportLayerTagMenuConfig();
            }

            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("导出配置json"))
                {
                    ExportConfigJson(serializedObject);
                }
                if (GUILayout.Button("从json导入配置"))
                {
                    ImportConfigFromJson(serializedObject);
                    GUIUtility.ExitGUI();
                }
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("新手引导"))
            {
                Psd2UIFormOnboardingWindow.ShowWindow();
            }
            EditorGUILayout.LabelField("使用说明:");
            readmeProperty.stringValue = EditorGUILayout.TextArea(readmeProperty.stringValue, GUILayout.Height(100));
            if (PsdReaderLicense.RequiresLicense)
            {
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUI.BeginChangeCheck();
                    {
                        orderId = EditorGUILayout.PasswordField("订单号:", orderId);
                    }
                    if (GUILayout.Button("确认", GUILayout.Width(60)))
                    {
                        PsdReaderLicense.SetOrderId(orderId);
                    }
                    if (GUILayout.Button("清除", GUILayout.Width(60)))
                    {
                        orderId = string.Empty;
                        PsdReaderLicense.ClearOrderId();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                if (GUILayout.Button("获取单号以取消水印"))
                {
                    Application.OpenURL("https://shop106471535.taobao.com");
                }
            }
            EditorGUILayout.BeginHorizontal();
            {
                forceUseTMP.boolValue = EditorGUILayout.ToggleLeft("强制优先使用TMP", forceUseTMP.boolValue);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.BeginHorizontal();
            {
                defaultTextType.enumValueIndex = EditorGUILayout.IntPopup("默认文本类型:", defaultTextType.enumValueIndex, textTypesDisplay, textTypes);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.BeginHorizontal();
            {
                defaultImageType.enumValueIndex = EditorGUILayout.IntPopup("默认图片类型:", defaultImageType.enumValueIndex, imageTypesDisplay, imageTypes);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.BeginHorizontal();
            {
                convertZh2En.boolValue = EditorGUILayout.ToggleLeft("中文转拼音", convertZh2En.boolValue);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.BeginHorizontal();
            {
                sharedAssetsOutput.stringValue = EditorGUILayout.TextField("复用图片导出路径", sharedAssetsOutput.stringValue);
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    string path = EditorUtility.OpenFolderPanel("选择导出路径", Application.dataPath, "");

                    if (!string.IsNullOrEmpty(path))
                    {
                        sharedAssetsOutput.stringValue = PathExtensions.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, path); ;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.BeginHorizontal();
            {
                sharedPrefabOutput.stringValue = EditorGUILayout.TextField("复用prefab导出路径", sharedPrefabOutput.stringValue);
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    string path = EditorUtility.OpenFolderPanel("选择导出路径", Application.dataPath, "");

                    if (!string.IsNullOrEmpty(path))
                    {
                        sharedPrefabOutput.stringValue = PathExtensions.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, path); ;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            if (aiProviderConfig != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(aiProviderConfig, new GUIContent("AI 自动改树"), true);
            }
            serializedObject.ApplyModifiedProperties();
            base.OnInspectorGUI();
        }

        private void ImportConfigFromJson(SerializedObject serializedObject)
        {
            string path = EditorUtility.OpenFilePanel("Select Config Json", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string json = File.ReadAllText(path);
                var snapshot = JsonUtility.FromJson<UGUIParserSnapshot>(json);
                if (snapshot == null)
                {
                    Debug.LogError("Failed to parse config json");
                    return;
                }

                // Restore simple fields
                serializedObject.FindProperty("defaultTextType").enumValueIndex = (int)snapshot.defaultTextType;
                serializedObject.FindProperty("defaultImageType").enumValueIndex = (int)snapshot.defaultImageType;
                serializedObject.FindProperty("forceUseTMP").boolValue = snapshot.forceUseTMP;
                serializedObject.FindProperty("readmeDoc").stringValue = snapshot.readmeDoc;
                serializedObject.FindProperty("convertZh2En").boolValue = snapshot.convertZh2En;
                serializedObject.FindProperty("sharedAssetsOutput").stringValue = snapshot.sharedAssetsOutput;
                serializedObject.FindProperty("sharedPrefabOutput").stringValue = snapshot.sharedPrefabOutput;

                // Restore UIForm Template
                if (!string.IsNullOrEmpty(snapshot.uiFormTemplateGuid))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(snapshot.uiFormTemplateGuid);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        var template = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                        serializedObject.FindProperty("uiFormTemplate").objectReferenceValue = template;
                    }
                }

                // Restore Rules
                if (snapshot.rules != null)
                {
                    var rulesProp = serializedObject.FindProperty("rules");
                    rulesProp.ClearArray();
                    for (int i = 0; i < snapshot.rules.Length; i++)
                    {
                        var ruleSnapshot = snapshot.rules[i];
                        rulesProp.InsertArrayElementAtIndex(i);
                        var ruleProp = rulesProp.GetArrayElementAtIndex(i);

                        ruleProp.FindPropertyRelative("UIType").enumValueIndex = (int)ruleSnapshot.UIType;
                        ruleProp.FindPropertyRelative("UITypeDesc").stringValue = ruleSnapshot.UITypeDesc;
                        ruleProp.FindPropertyRelative("UIHelper").stringValue = ruleSnapshot.UIHelper;
                        ruleProp.FindPropertyRelative("Comment").stringValue = ruleSnapshot.Comment;

                        // TypeMatches
                        var matchesProp = ruleProp.FindPropertyRelative("TypeMatches");
                        matchesProp.ClearArray();
                        if (ruleSnapshot.TypeMatches != null)
                        {
                            for (int j = 0; j < ruleSnapshot.TypeMatches.Length; j++)
                            {
                                matchesProp.InsertArrayElementAtIndex(j);
                                matchesProp.GetArrayElementAtIndex(j).stringValue = ruleSnapshot.TypeMatches[j];
                            }
                        }

                        // Restore UIPrefab
                        if (!string.IsNullOrEmpty(ruleSnapshot.UIPrefabGuid))
                        {
                            string prefabPath = AssetDatabase.GUIDToAssetPath(ruleSnapshot.UIPrefabGuid);
                            if (!string.IsNullOrEmpty(prefabPath))
                            {
                                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                                ruleProp.FindPropertyRelative("UIPrefab").objectReferenceValue = prefab;
                            }
                        }
                    }
                }

                serializedObject.ApplyModifiedProperties();
                Debug.Log($"Config imported from {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Import config failed: {e.Message}");
            }
        }

        private void ExportConfigJson(SerializedObject serializedObject)
        {
            string path = EditorUtility.SaveFilePanel("Save Config Json", Application.dataPath, "Psd2UIFormConfig", "json");
            if (string.IsNullOrEmpty(path)) return;

            var snapshot = new UGUIParserSnapshot();
            var target = serializedObject.targetObject as UGUIParser;
            if (target == null) return;

            // Use SerializedObject to access private fields via reflection or just use public properties if available.
            // But since we are inside the class (or editor class), let's use the serialized properties to be safe or access fields if we are inside the class.
            // Wait, UGUIParserEditor is an Editor class, it cannot access private fields of UGUIParser directly unless they are internal or via SerializedProperty.
            // However, we are writing code inside UGUIParserEditor which is nested or separate?
            // The user provided code shows UGUIParserEditor : Editor.
            // So we should use serializedObject to read values to ensure we get what's in inspector.

            snapshot.defaultTextType = (GUIType)serializedObject.FindProperty("defaultTextType").enumValueIndex;
            snapshot.defaultImageType = (GUIType)serializedObject.FindProperty("defaultImageType").enumValueIndex;
            snapshot.forceUseTMP = serializedObject.FindProperty("forceUseTMP").boolValue;
            snapshot.readmeDoc = serializedObject.FindProperty("readmeDoc").stringValue;
            snapshot.convertZh2En = serializedObject.FindProperty("convertZh2En").boolValue;
            snapshot.sharedAssetsOutput = serializedObject.FindProperty("sharedAssetsOutput").stringValue;
            snapshot.sharedPrefabOutput = serializedObject.FindProperty("sharedPrefabOutput").stringValue;

            // UIForm Template
            var template = serializedObject.FindProperty("uiFormTemplate").objectReferenceValue;
            if (template != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(template);
                snapshot.uiFormTemplateGuid = AssetDatabase.AssetPathToGUID(assetPath);
            }

            // Rules
            var rulesProp = serializedObject.FindProperty("rules");
            if (rulesProp != null && rulesProp.isArray)
            {
                var rulesList = new List<UGUIParseRuleSnapshot>();
                for (int i = 0; i < rulesProp.arraySize; i++)
                {
                    var ruleProp = rulesProp.GetArrayElementAtIndex(i);
                    var ruleSnapshot = new UGUIParseRuleSnapshot();
                    ruleSnapshot.UIType = (GUIType)ruleProp.FindPropertyRelative("UIType").enumValueIndex;
                    ruleSnapshot.UITypeDesc = ruleProp.FindPropertyRelative("UITypeDesc").stringValue;
                    ruleSnapshot.UIHelper = ruleProp.FindPropertyRelative("UIHelper").stringValue;
                    ruleSnapshot.Comment = ruleProp.FindPropertyRelative("Comment").stringValue;

                    // TypeMatches
                    var matchesProp = ruleProp.FindPropertyRelative("TypeMatches");
                    if (matchesProp != null && matchesProp.isArray)
                    {
                        var matches = new List<string>();
                        for (int j = 0; j < matchesProp.arraySize; j++)
                        {
                            matches.Add(matchesProp.GetArrayElementAtIndex(j).stringValue);
                        }
                        ruleSnapshot.TypeMatches = matches.ToArray();
                    }

                    // UIPrefab
                    var prefab = ruleProp.FindPropertyRelative("UIPrefab").objectReferenceValue;
                    if (prefab != null)
                    {
                        string assetPath = AssetDatabase.GetAssetPath(prefab);
                        ruleSnapshot.UIPrefabGuid = AssetDatabase.AssetPathToGUID(assetPath);
                    }

                    rulesList.Add(ruleSnapshot);
                }
                snapshot.rules = rulesList.ToArray();
            }

            try
            {
                string json = JsonUtility.ToJson(snapshot, true);
                File.WriteAllText(path, json);
                Debug.Log($"Config exported to {path}");
                EditorUtility.RevealInFinder(path);
            }
            catch (Exception e)
            {
                Debug.LogError($"Export config failed: {e.Message}");
            }
        }

        [Serializable]
        [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
        private class UGUIParserSnapshot
        {
            public GUIType defaultTextType;
            public GUIType defaultImageType;
            public bool forceUseTMP;
            public string uiFormTemplateGuid;
            public UGUIParseRuleSnapshot[] rules;
            public string readmeDoc;
            public bool convertZh2En;
            public string sharedAssetsOutput;
            public string sharedPrefabOutput;
        }

        [Serializable]
        [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
        private class UGUIParseRuleSnapshot
        {
            public GUIType UIType;
            public string UITypeDesc;
            public string[] TypeMatches;
            public string UIPrefabGuid;
            public string UIHelper;
            public string Comment;
        }
    }
    [CanEditMultipleObjects]
    [CreateAssetMenu(fileName = "Psd2UIFormConfig", menuName = "ScriptableObject/Psd2UIForm Config【Psd2UIForm工具配置】")]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    public class UGUIParser : ScriptableObject
    {
        public const char UITYPE_SPLIT_CHAR = '.';
        public const int UITYPE_MAX = 100;
        private const string LayerTagMenuScriptRelativePath = "PSScript/PSD2UGUI-LayerTagMenu.jsx";
        public const string REF_TAG = "ref ";
        public const string REF_PREFAB_TAG = "refp ";
        [HideInInspector][SerializeField] GUIType defaultTextType = GUIType.Text;
        [HideInInspector][SerializeField] GUIType defaultImageType = GUIType.Image;
        [SerializeField, HideInInspector] bool forceUseTMP = false;
        [SerializeField] GameObject uiFormTemplate;
        [SerializeField] UGUIParseRule[] rules;
        [HideInInspector][SerializeField] string readmeDoc = "使用说明";
        [HideInInspector][SerializeField] bool convertZh2En = true;
        [HideInInspector][SerializeField] string sharedAssetsOutput = "Assets/SharedUIAssets";
        [HideInInspector][SerializeField] string sharedPrefabOutput = "Assets/SharedPrefab";
        [HideInInspector][SerializeField] AiProviderConfig aiProviderConfig = new AiProviderConfig();
        /// <summary>
        /// 复用资源的导出路径
        /// </summary>
        public string SharedAssetsOutput => sharedAssetsOutput;
        public string SharedPrefabOutput => sharedPrefabOutput;
        internal AiProviderConfig AiProviderConfig => aiProviderConfig ?? (aiProviderConfig = new AiProviderConfig());
        public GUIType DefaultText => ResolvePreferredUIType(defaultTextType);
        public GUIType DefaultImage => defaultImageType;
        public GameObject UIFormTemplate => uiFormTemplate;
        public bool ConvertZh2En => convertZh2En;
        public bool ForceUseTMP => forceUseTMP;
        private static UGUIParser mInstance = null;
        private const string TmpEffectMaterialNameToken = "__PSD2UI_TMPFX__";
        private const string TmpEffectMaterialAliasPrefix = "PSD2UI_TMPFX_SOURCE_NAME=";
        private const string TmpSplitGlowLayerSuffix = "__PSD2UI_TMP_GLOW";
        private const string TmpSplitStrokeLayerSuffix = "__PSD2UI_TMP_STROKE";
        private const string TmpSplitShadowLayerSuffix = "__PSD2UI_TMP_SHADOW";
        private static readonly Dictionary<string, Material> tmpEffectMaterialCache = new Dictionary<string, Material>();
        private static readonly Dictionary<int, Material> tmpEffectMaterialBaseLookup = new Dictionary<int, Material>();
        private static readonly int TmpFaceDilatePropertyId = Shader.PropertyToID("_FaceDilate");
        private const float TmpOutlineThicknessScale = 1f;
        private const float TmpOutsideStrokeDilateScale = 1.0f;
        private const float TmpOutsideStrokeOutlineScale = 0.2f;
        private const float TmpShadowBlurToDilateScale = 0.5f;
        private const float TmpSongtiNormalWeightCompensation = 0.45f;
        public static UGUIParser Instance
        {
            get
            {
                if (mInstance == null)
                {
                    var guid = AssetDatabase.FindAssets("t:UGUIParser").FirstOrDefault();
                    mInstance = AssetDatabase.LoadAssetAtPath<UGUIParser>(AssetDatabase.GUIDToAssetPath(guid));
                }
                return mInstance;
            }
        }
        public static bool IsMainUIType(GUIType tp)
        {
            return tp != GUIType.Null && (int)tp <= UITYPE_MAX;
        }
        internal static bool IsSemanticUIType(GUIType uiType)
        {
            return (int)uiType > UITYPE_MAX;
        }
        internal static bool IsTransparentContainerType(GUIType uiType)
        {
            return uiType == GUIType.Null || uiType == GUIType.Panel;
        }
        internal static bool IsCompositeControlType(GUIType uiType)
        {
            switch (uiType)
            {
                case GUIType.Button:
                case GUIType.Dropdown:
                case GUIType.InputField:
                case GUIType.Toggle:
                case GUIType.Slider:
                case GUIType.ScrollView:
                case GUIType.TMPButton:
                case GUIType.TMPDropdown:
                case GUIType.TMPInputField:
                case GUIType.TMPToggle:
                case GUIType.Panel:
                case GUIType.ToggleGroup:
                    return true;
                default:
                    return false;
            }
        }
        internal static bool IsStandaloneGraphicMainType(GUIType uiType)
        {
            switch (uiType)
            {
                case GUIType.Image:
                case GUIType.RawImage:
                case GUIType.Text:
                case GUIType.TMPText:
                case GUIType.Mask:
                case GUIType.FillColor:
                    return true;
                default:
                    return false;
            }
        }
        internal static bool HasExplicitMainChildOwnerRule(GUIType uiType)
        {
            return uiType == GUIType.ScrollView || uiType == GUIType.Toggle || uiType == GUIType.TMPToggle;
        }
        internal static bool RequiresOwnerResolution(GUIType uiType)
        {
            return IsSemanticUIType(uiType) || HasExplicitMainChildOwnerRule(uiType);
        }
        internal static bool CanOwnSemanticRole(GUIType ownerType, GUIType roleType)
        {
            switch (roleType)
            {
                case GUIType.Background:
                    return IsMainUIType(ownerType);
                case GUIType.Button_Highlight:
                case GUIType.Button_Press:
                case GUIType.Button_Select:
                case GUIType.Button_Disable:
                case GUIType.Button_Text:
                    return ownerType == GUIType.Button || ownerType == GUIType.TMPButton;
                case GUIType.Dropdown_Label:
                case GUIType.Dropdown_Arrow:
                    return ownerType == GUIType.Dropdown || ownerType == GUIType.TMPDropdown;
                case GUIType.InputField_Placeholder:
                case GUIType.InputField_Text:
                    return ownerType == GUIType.InputField || ownerType == GUIType.TMPInputField;
                case GUIType.Toggle_Checkmark:
                case GUIType.Toggle_Label:
                    return ownerType == GUIType.Toggle || ownerType == GUIType.TMPToggle;
                case GUIType.Slider_Fill:
                case GUIType.Slider_Handle:
                    return ownerType == GUIType.Slider;
                case GUIType.ScrollView_Viewport:
                case GUIType.ScrollView_HorizontalBarBG:
                case GUIType.ScrollView_HorizontalBar:
                case GUIType.ScrollView_VerticalBarBG:
                case GUIType.ScrollView_VerticalBar:
                    return ownerType == GUIType.ScrollView;
                default:
                    return false;
            }
        }
        internal static bool CanOwnMainChild(GUIType ownerType, GUIType childType)
        {
            switch (ownerType)
            {
                case GUIType.Dropdown:
                case GUIType.TMPDropdown:
                    return childType == GUIType.ScrollView || childType == GUIType.Toggle || childType == GUIType.TMPToggle;
                case GUIType.ToggleGroup:
                    return childType == GUIType.Toggle || childType == GUIType.TMPToggle;
                default:
                    return false;
            }
        }
        public GUIType ResolvePreferredUIType(GUIType uiType)
        {
            if (!forceUseTMP) return uiType;

            switch (uiType)
            {
                case GUIType.Text:
                    return GUIType.TMPText;
                case GUIType.Button:
                    return GUIType.TMPButton;
                case GUIType.Dropdown:
                    return GUIType.TMPDropdown;
                case GUIType.InputField:
                    return GUIType.TMPInputField;
                case GUIType.Toggle:
                    return GUIType.TMPToggle;
                default:
                    return uiType;
            }
        }
        private UGUIParseRule ResolvePreferredRule(UGUIParseRule rule)
        {
            if (rule == null) return null;

            var preferredUIType = ResolvePreferredUIType(rule.UIType);
            if (preferredUIType == rule.UIType)
            {
                return rule;
            }

            return GetRule(preferredUIType) ?? rule;
        }
        public Type GetHelperType(GUIType uiType)
        {
            if (uiType == GUIType.Null) return null;

            // Panel/ToggleGroup are generated procedurally.  They intentionally do not
            // depend on a prefab-backed parser rule, so older project configurations can
            // consume AI tree results without rewriting the existing rule asset.
            if (uiType == GUIType.Panel) return typeof(PanelHelper);
            if (uiType == GUIType.ToggleGroup) return typeof(ToggleGroupHelper);

            var rule = GetRule(uiType);
            if (rule == null || string.IsNullOrWhiteSpace(rule.UIHelper)) return null;

            return Type.GetType(rule.UIHelper);
        }
        public UGUIParseRule GetRule(GUIType uiType)
        {
            foreach (var rule in rules)
            {
                if (rule.UIType == uiType) return rule;
            }
            return null;
        }
        internal UGUIParseRule[] GetRules()
        {
            return rules ?? Array.Empty<UGUIParseRule>();
        }
        public string GetSafeLayerName(string rawName, int fallbackIndex = -1)
        {
            var workingName = string.IsNullOrWhiteSpace(rawName) ? string.Empty : rawName.Trim();
            var refDetectSource = workingName;
            bool isRefName = refDetectSource.StartsWith(REF_TAG, StringComparison.OrdinalIgnoreCase);
            bool isRefPrefabName = refDetectSource.StartsWith(REF_PREFAB_TAG, StringComparison.OrdinalIgnoreCase);
            if (convertZh2En && !string.IsNullOrEmpty(workingName))
            {
                workingName = LayerNameUtility.ConvertChineseToLetters(workingName);
                refDetectSource = workingName;
            }
            if (isRefName || isRefPrefabName)
            {
                var refTag = isRefPrefabName ? REF_PREFAB_TAG : REF_TAG;
                var refTargetRaw = refDetectSource.Length > refTag.Length ? refDetectSource.Substring(refTag.Length).Trim() : string.Empty;
                refTargetRaw = StripLayerTypeFlags(refTargetRaw);
                var sanitizedTarget = LayerNameUtility.Sanitize(refTargetRaw);
                workingName = $"{refTag}{(string.IsNullOrEmpty(sanitizedTarget) ? refTargetRaw : sanitizedTarget)}".TrimEnd();
            }
            else
            {
                workingName = StripLayerTypeFlags(workingName);
                workingName = LayerNameUtility.Sanitize(workingName);
            }
            if (string.IsNullOrEmpty(workingName))
            {
                workingName = fallbackIndex >= 0 ? $"PsdLayer-{fallbackIndex}" : "PsdLayer";
            }
            return workingName;
        }
        /// <summary>
        /// 根据图层命名解析UI类型
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="comType"></param>
        /// <returns></returns>
        public bool TryParse(PsdLayerNode layer, out UGUIParseRule result)
        {
            result = null;
            var layerName = layer.BindPsdLayer.GetDisplayName();
            if (HasUITypeFlag(layerName, out var tpFlag))
            {
                var tpTag = tpFlag.Substring(1);
                foreach (var rule in rules)
                {
                    foreach (var item in rule.TypeMatches)
                    {
                        if (tpTag.CompareTo(item.ToLower()) == 0)
                        {
                            result = ResolvePreferredRule(rule);
                            return true;
                        }
                    }
                }
            }

            switch (layer.LayerType)
            {
                case PsdLayerType.TextLayer:
                    result = ResolvePreferredRule(rules.First(itm => itm.UIType == defaultTextType));
                    break;
                case PsdLayerType.FillLayer:
                    result = rules.First(itm => itm.UIType == GUIType.FillColor);
                    break;
                case PsdLayerType.LayerGroup:
                    result = rules.First(itm => itm.UIType == GUIType.Null);
                    break;
                default:
                    result = ResolvePreferredRule(rules.First(itm => itm.UIType == defaultImageType));
                    break;
            }
            return result != null;
        }
        public static bool HasUITypeFlag(string layerName, out string tpFlag)
        {
            tpFlag = null;
            if (string.IsNullOrWhiteSpace(layerName) || layerName.EndsWith(UGUIParser.UITYPE_SPLIT_CHAR.ToString())) return false;
            int startIdx = -1;
            for (int i = layerName.Length - 1; i >= 0; i--)
            {
                if (layerName[i] == UGUIParser.UITYPE_SPLIT_CHAR)
                {
                    startIdx = i;
                    break;
                }
            }
            if (startIdx <= 0) return false;

            tpFlag = layerName.Substring(startIdx);
            return true;
        }
        /// <summary>
        /// 根据图层大小和位置设置UI节点大小和位置
        /// </summary>
        /// <param name="layerNode"></param>
        /// <param name="uiNode"></param>
        /// <param name="pos">是否设置位置</param>
        private string StripLayerTypeFlags(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return string.Empty;

            var workingName = layerName.Trim();
            while (HasUITypeFlag(workingName, out var tpFlag))
            {
                if (string.IsNullOrEmpty(tpFlag) || tpFlag.Length <= 1)
                {
                    break;
                }

                var typeTag = tpFlag.Substring(1);
                if (!IsKnownLayerTypeTag(typeTag))
                {
                    break;
                }

                workingName = workingName.Substring(0, workingName.Length - tpFlag.Length).TrimEnd();
            }

            return workingName;
        }

        private bool IsKnownLayerTypeTag(string typeTag)
        {
            if (string.IsNullOrWhiteSpace(typeTag) || rules == null)
            {
                return false;
            }

            for (int i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                if (rule?.TypeMatches == null)
                {
                    continue;
                }

                for (int j = 0; j < rule.TypeMatches.Length; j++)
                {
                    if (string.Equals(rule.TypeMatches[j], typeTag, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static void SetRectTransform(PsdLayerNode layerNode, UnityEngine.Component uiNode, bool pos = true, bool width = true, bool height = true, int extSize = 0)
        {
            if (uiNode == null || layerNode == null) return;

            var rect = layerNode.LayerRect;
            var rectTransform = uiNode.GetComponent<RectTransform>();

            // 设置尺寸
            if (width)
            {
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rect.size.x + extSize);
            }
            if (height)
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rect.size.y + extSize);

            if (pos)
            {
                // 获取实际设置后的尺寸
                Vector2 actualSize = rectTransform.rect.size;

                // 计算 pivot 偏移（从中心点到 pivot 点的偏移）
                Vector2 pivotOffset = (rectTransform.pivot - Vector2.one * 0.5f) * actualSize;

                // 假设 rect.position 是图层中心点的世界坐标
                // pivot 点的世界坐标 = 中心点世界坐标 + pivot 偏移
                Vector3 pivotWorldPos = new Vector3(
                    rect.position.x + pivotOffset.x,
                    rect.position.y + pivotOffset.y,
                    rectTransform.position.z
                );

                // 直接设置世界坐标
                rectTransform.position = pivotWorldPos;
            }

            var tmpText = uiNode as TextMeshProUGUI ?? uiNode.GetComponent<TextMeshProUGUI>();
            var effectBinder = tmpText != null ? tmpText.GetComponent<PsdTmpTextEffectBinder>() : null;
            if (effectBinder != null)
            {
                effectBinder.SyncNow();
            }
            else
            {
                SyncSplitTMPGlowLayerRect(tmpText);
            }
        }

        /// <summary>
        /// 把LayerNode图片保存到本地并返回
        /// </summary>
        /// <param name="layerNode"></param>
        /// <returns></returns>
        public static Texture2D LayerNode2Texture(PsdLayerNode layerNode)
        {
            if (layerNode != null)
            {
                var spAssetName = layerNode.ExportImageAsset(false);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(spAssetName);
                return texture;
            }
            return null;
        }
        /// <summary>
        /// 把LayerNode图片保存到本地并返回
        /// </summary>
        /// <param name="layerNode"></param>
        /// <param name="auto9Slice">若没有设置Sprite的九宫,是否自动计算并设置九宫</param>
        /// <returns></returns>
        public static Sprite LayerNode2Sprite(PsdLayerNode layerNode, bool auto9Slice = false)
        {
            if (layerNode != null)
            {
                var spAssetName = layerNode.ExportImageAsset(true);
                EnsureSpriteAssetImported(spAssetName);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spAssetName);
                if (sprite != null)
                {
                    return sprite;
                }
                if (!string.IsNullOrEmpty(spAssetName))
                {
                    Debug.LogWarning($"[PSD2UIForm:LayerNode2Sprite] Sprite资源尚未就绪或导入失败: layer={layerNode.name}, uiType={layerNode.UIType}, asset={spAssetName}, exists={File.Exists(spAssetName)}, importer={(AssetImporter.GetAtPath(spAssetName) != null)}");
                }
            }
            return null;
        }

        private static void EnsureSpriteAssetImported(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
                return;

            if (AssetImporter.GetAtPath(assetPath) == null)
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }

            var texImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (texImporter == null)
                return;

            var changed = false;
            if (texImporter.textureType != TextureImporterType.Sprite)
            {
                texImporter.textureType = TextureImporterType.Sprite;
                changed = true;
            }
            if (texImporter.spriteImportMode != SpriteImportMode.Single)
            {
                texImporter.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }
            if (texImporter.alphaSource != TextureImporterAlphaSource.FromInput)
            {
                texImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                changed = true;
            }
            if (!texImporter.alphaIsTransparency)
            {
                texImporter.alphaIsTransparency = true;
                changed = true;
            }
            if (texImporter.mipmapEnabled)
            {
                texImporter.mipmapEnabled = false;
                changed = true;
            }

            if (changed)
            {
                texImporter.SaveAndReimport();
            }
            else if (AssetDatabase.LoadAssetAtPath<Sprite>(assetPath) == null)
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }
        }

        /// <summary>
        /// 尝试加载图层的效果图 Sprite（描边/阴影独立导出的 _effect.png）。
        /// 同时向 imgCom 的父级添加一个效果底图 Image（位于原图后面）。
        /// </summary>
        internal static void TryPlaceEffectsImage(PsdLayerNode layerNode, UnityEngine.UI.Graphic mainGraphic)
        {
            if (layerNode == null || mainGraphic == null) return;
            string fxPath = layerNode.EffectsAssetPath;
            if (string.IsNullOrEmpty(fxPath)) return;

            // 确保效果图已导入
            if (AssetImporter.GetAtPath(fxPath) == null)
            {
                AssetDatabase.ImportAsset(fxPath, ImportAssetOptions.ForceSynchronousImport);
                Psd2UIFormConverter.ConvertTexturesType(new[] { fxPath }, true);
            }
            var fxSprite = AssetDatabase.LoadAssetAtPath<Sprite>(fxPath);
            if (fxSprite == null) return;

            // 创建效果 Image GameObject（在主图之前=视觉上在后面）
            var parent = mainGraphic.transform.parent;
            var fxGo = new GameObject(layerNode.name + "_effect");
            fxGo.transform.SetParent(parent, false);
            fxGo.transform.SetSiblingIndex(mainGraphic.transform.GetSiblingIndex());

            var fxImg = fxGo.AddComponent<UnityEngine.UI.Image>();
            fxImg.sprite = fxSprite;
            fxImg.type = UnityEngine.UI.Image.Type.Simple;
            fxImg.raycastTarget = false;

            // 计算效果图的 RectTransform
            // 基础图层 Rect 与效果图的偏移关系:
            //   效果图 PSD坐标 left = 原图left - expandLeft
            //   效果图 PSD坐标 top  = 原图top  - expandTop
            var converter = Psd2UIFormConverter.Instance;
            if (converter != null && converter.Initiated)
            {
                var canvasSize = converter.UIFormCanvasSize;
                int fxLeft = layerNode.BindPsdLayer.Left - layerNode.EffectsExpandLeft;
                int fxTop = layerNode.BindPsdLayer.Top - layerNode.EffectsExpandTop;
                int fxRight = fxLeft + layerNode.EffectsWidth;
                int fxBottom = fxTop + layerNode.EffectsHeight;
                var fxRect = PsdParserExtensions.PsdRect2UnityRect(fxLeft, fxTop, fxRight, fxBottom, canvasSize);

                var rt = fxGo.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(fxRect.x, fxRect.y);
                rt.sizeDelta = new Vector2(fxRect.width, fxRect.height);
            }
        }

        #region 计算贴图9宫Border

        // ═══════════════════════════════════════════════════════════════
        // 九宫格自动检测与裁剪 — 列/行轮廓匹配算法
        //
        // 核心思路:
        //   1. 找到图片的非透明内容边界
        //   2. 对每对相邻列(行)，比较它们在交叉轴方向的完整像素轮廓
        //   3. 轮廓匹配的连续列(行) = 可拉伸区间（最长匹配段）
        //   4. 拉伸区间需占内容维度 ≥15% 且长度 ≥4px
        //
        // 优势:
        //   - 天然排除圆角/胶囊边缘（alpha 轮廓不同 → 不匹配）
        //   - 天然排除复杂花纹/图案（各列花纹不同 → 不匹配）
        //   - 支持渐变（相邻列颜色差极小 → 仍算匹配）
        //   - 不需要额外的均匀性验证或轮廓一致性验证
        //   - 算法简洁，逻辑清晰，鲁棒性高
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 判断一张纹理是否适合做九宫格切分
        /// </summary>
        public static bool IsNineSliceCandidate(Texture2D texture)
        {
            if (texture == null) return false;
            int w = texture.width, h = texture.height;

            // 太小的图不值得切。
            if (w < 2 || h < 2) return false;

            // 至少有一个维度 > 40 才考虑裁剪
            if (w <= 40 && h <= 40) return false;

            var pixels = texture.GetPixels32();
            var contentRect = FindContentBounds(pixels, w, h, 1);
            var contentW = Mathf.Max(1, (int)contentRect.xMax - (int)contentRect.xMin + 1);
            var contentH = Mathf.Max(1, (int)contentRect.yMax - (int)contentRect.yMin + 1);
            if (IsThinDecorativeStrip(contentW, contentH))
            {
                Debug.Log($"[NineSlice:Candidate] 跳过细长装饰线/虚线 size={w}x{h}, content={contentW}x{contentH}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 自动计算贴图的 9宫 Border。
        /// 返回 Vector4.zero 表示不适合做九宫格。
        /// </summary>
        public static Vector4 CalculateTexture9SliceBorder(Texture2D texture, byte alphaThreshold = 1, int variationTolerance = 3)
        {
            var result = CalculateTexture9SliceBorderInternal(texture, alphaThreshold, variationTolerance);
            return result.border;
        }

        private struct NineSliceBorderResult
        {
            public Vector4 border;
            public bool hAxisValid;
            public bool vAxisValid;
        }

        private static NineSliceBorderResult CalculateTexture9SliceBorderInternal(Texture2D texture, byte alphaThreshold = 1, int variationTolerance = 3)
        {
            var fail = new NineSliceBorderResult { border = Vector4.zero, hAxisValid = false, vAxisValid = false };
            if (texture == null) return fail;

            int width = texture.width;
            int height = texture.height;
            Color32[] pixels = texture.GetPixels32();

            // 1. 找到非透明内容边界
            Rect contentRect = FindContentBounds(pixels, width, height, alphaThreshold);
            int cL = (int)contentRect.xMin;
            int cR = (int)contentRect.xMax;
            int cB = (int)contentRect.yMin;
            int cT = (int)contentRect.yMax;

            int contentW = cR - cL + 1;
            int contentH = cT - cB + 1;

            if (IsThinDecorativeStrip(contentW, contentH))
            {
                Debug.Log($"[NineSlice:Final] 跳过细长装饰线/虚线 size={width}x{height}, content={contentW}x{contentH}");
                return fail;
            }

            if (TryCalculateStrip9SliceBorder(pixels, width, height, cL, cR, cB, cT,
                contentW, contentH, alphaThreshold, variationTolerance, out var stripResult))
            {
                return stripResult;
            }

            if (contentW < 8 || contentH < 8) return fail;

            // 2. 纹理复杂度预检 — 照片/复杂图案直接跳过
            if (IsComplexTexture(pixels, width, height, cL, cR, cB, cT, variationTolerance))
                return fail;

            // 3. 列/行轮廓匹配法 — 比较相邻列(行)的完整交叉轴轮廓
            //    相邻列轮廓匹配的连续区域 = 可拉伸区间
            //    天然排除圆角(alpha轮廓不同)、复杂花纹(各列图案不同)
            RangeInt hStretch = FindStretchByProfile(pixels, width, height, cL, cR, cB, cT, true, variationTolerance, alphaThreshold);
            RangeInt vStretch = FindStretchByProfile(pixels, width, height, cL, cR, cB, cT, false, variationTolerance, alphaThreshold);

            // 4. 验证
            bool hValid = hStretch.length >= 4 && (float)hStretch.length / contentW >= 0.15f;
            bool vValid = vStretch.length >= 4 && (float)vStretch.length / contentH >= 0.15f;

            Debug.Log($"[NineSlice:Profile] size={width}x{height}, content=({cL},{cB})-({cR},{cT})[{contentW}x{contentH}], " +
                $"hStretch=(start={hStretch.start},len={hStretch.length},ratio={((float)hStretch.length / contentW):P0},valid={hValid}), " +
                $"vStretch=(start={vStretch.start},len={vStretch.length},ratio={((float)vStretch.length / contentH):P0},valid={vValid})");

            // 4b. 回退：边框/框架图形专用检测（轮廓匹配法，适合矮宽/窄高圆角矩形）
            //     要求两侧各留 ≥4px 边框（端点漂移修剪已排除圆角区域）
            const int minFrameBorder = 4;
            if (!hValid)
            {
                var hFrame = DetectBorderFrameStretch(pixels, width, height, cL, cR, cB, cT, true, variationTolerance, alphaThreshold);
                int hBorderL = hFrame.start - cL;
                int hBorderR = cR - (hFrame.start + hFrame.length - 1);
                if (hFrame.length >= 4 && (float)hFrame.length / contentW >= 0.15f && hBorderL >= minFrameBorder && hBorderR >= minFrameBorder)
                {
                    // 端点相似度检查：拉伸区首列与尾列相差过大说明是渐变列，不可拉伸
                    int endTol = variationTolerance * 4;
                    bool hEndpointsOk = CheckColSimilarity(pixels, width, hFrame.start, hFrame.start + hFrame.length - 1, cB, cT, endTol);
                    if (hEndpointsOk) { hStretch = hFrame; hValid = true; }
                    else Debug.Log($"[NineSlice:BorderFrame] axis=H 端点差异过大(渐变列), 跳过 len={hFrame.length}");
                }
            }
            if (!vValid)
            {
                var vFrame = DetectBorderFrameStretch(pixels, width, height, cL, cR, cB, cT, false, variationTolerance, alphaThreshold);
                int vBorderB = vFrame.start - cB;
                int vBorderT = cT - (vFrame.start + vFrame.length - 1);
                if (vFrame.length >= 4 && (float)vFrame.length / contentH >= 0.15f && vBorderB >= minFrameBorder && vBorderT >= minFrameBorder)
                {
                    // 端点相似度检查：拉伸区首行与尾行相差过大说明是渐变行，不可拉伸
                    int endTol = variationTolerance * 4;
                    bool vEndpointsOk = CheckRowSimilarity(pixels, width, vFrame.start, vFrame.start + vFrame.length - 1, cL, cR, endTol);
                    if (vEndpointsOk) { vStretch = vFrame; vValid = true; }
                    else Debug.Log($"[NineSlice:BorderFrame] axis=V 端点差异过大(渐变行), 跳过 len={vFrame.length}");
                }
            }

            Debug.Log($"[NineSlice:Final] hStretch=(start={hStretch.start},len={hStretch.length},valid={hValid}), " +
                $"vStretch=(start={vStretch.start},len={vStretch.length},valid={vValid})");

            if (!hValid && !vValid) return fail;

            // 如果某个方向无效，给它一个安全的中心区间（用于 sprite border 设置，不用于裁剪）
            if (!hValid) hStretch = new RangeInt(cL + contentW / 3, Mathf.Max(1, contentW / 3));
            if (!vValid) vStretch = new RangeInt(cB + contentH / 3, Mathf.Max(1, contentH / 3));

            // 5. 胶囊/长条形安全约束
            //    水平胶囊(宽>>高)：左右border覆盖圆头，垂直border覆盖整个高度(不允许垂直拉伸)
            //    垂直胶囊(高>>宽)：上下border覆盖圆头，水平border覆盖整个宽度(不允许水平拉伸)
            //    核心原因：Unity 9-slice 的4个角不拉伸、上下边只水平拉伸、左右边只垂直拉伸、中心双向拉伸。
            //    如果胶囊圆头在左右border区域，而垂直有拉伸区，那中间段变高但圆头不变高→变形。
            //    解决：让短轴border覆盖几乎全部高度(留3px stretch zone通过验证，均匀像素拉伸无害)。
            bool isCapsule = false;
            {
                int minDim = Mathf.Min(contentW, contentH);
                int maxDim = Mathf.Max(contentW, contentH);
                if (minDim <= 64 && maxDim >= minDim * 2)
                {
                    isCapsule = true;
                    int capsuleRadius = (minDim + 1) / 2 + 1;

                    if (contentW > contentH)
                    {
                        // ── 水平胶囊 ──
                        // a) 水平方向：约束 hStretch 排除圆头
                        int newStart = Mathf.Max(hStretch.start, cL + capsuleRadius);
                        int newEnd = Mathf.Min(hStretch.start + hStretch.length - 1, cR - capsuleRadius);
                        if (newEnd > newStart)
                            hStretch = new RangeInt(newStart, newEnd - newStart + 1);

                        // b) 垂直方向：锁定border覆盖几乎全高度，只留中心3px stretch zone
                        //    这3px在胶囊中间段是完全均匀的一行像素，拉伸无任何变形
                        int vCenter = (cB + cT) / 2;
                        vStretch = new RangeInt(vCenter - 1, 3);
                        vValid = false; // 阻止垂直裁剪

                        Debug.Log($"[NineSlice:Capsule] 水平胶囊! minDim={minDim}, maxDim={maxDim}, capsuleR={capsuleRadius}, " +
                            $"hStretch=[{hStretch.start},{hStretch.start + hStretch.length - 1}], vStretch=锁定[{vCenter - 1},{vCenter + 1}]");
                    }
                    else
                    {
                        // ── 垂直胶囊 ──
                        int newStart = Mathf.Max(vStretch.start, cB + capsuleRadius);
                        int newEnd = Mathf.Min(vStretch.start + vStretch.length - 1, cT - capsuleRadius);
                        if (newEnd > newStart)
                            vStretch = new RangeInt(newStart, newEnd - newStart + 1);

                        int hCenter = (cL + cR) / 2;
                        hStretch = new RangeInt(hCenter - 1, 3);
                        hValid = false;

                        Debug.Log($"[NineSlice:Capsule] 垂直胶囊! minDim={minDim}, maxDim={maxDim}, capsuleR={capsuleRadius}, " +
                            $"vStretch=[{vStretch.start},{vStretch.start + vStretch.length - 1}], hStretch=锁定[{hCenter - 1},{hCenter + 1}]");
                    }
                }
            }

            // 5b. 计算 Border（相对于图片边缘，与 Unity spriteBorder 一致）
            int leftBorder = Mathf.Max(1, hStretch.start);
            int rightBorder = Mathf.Max(1, (width - 1) - (hStretch.start + hStretch.length - 1));
            int bottomBorder = Mathf.Max(1, vStretch.start);
            int topBorder = Mathf.Max(1, (height - 1) - (vStretch.start + vStretch.length - 1));

            // 5c. 圆角安全约束：确保四个角的 border 都能覆盖圆角区域
            //     九宫格的角块 = leftBorder × topBorder（左上）等，圆角是二维的，
            //     如果圆角半径为 R，需要 border 在两个方向都 >= R 来覆盖角块。
            //     利用四个角实际的 alpha 内缩量来检测圆角半径，并适当扩大 border。
            if (!isCapsule)
            {
                int cornerLT = MeasureCornerRadius(pixels, width, height, cL, cT, true, true, alphaThreshold);   // 左上
                int cornerRT = MeasureCornerRadius(pixels, width, height, cR, cT, false, true, alphaThreshold);   // 右上
                int cornerLB = MeasureCornerRadius(pixels, width, height, cL, cB, true, false, alphaThreshold);   // 左下
                int cornerRB = MeasureCornerRadius(pixels, width, height, cR, cB, false, false, alphaThreshold);  // 右下

                // 左上角：left 和 top 都要覆盖
                if (cornerLT > 0) { leftBorder = Mathf.Max(leftBorder, cornerLT + 1); topBorder = Mathf.Max(topBorder, cornerLT + 1); }
                // 右上角
                if (cornerRT > 0) { rightBorder = Mathf.Max(rightBorder, cornerRT + 1); topBorder = Mathf.Max(topBorder, cornerRT + 1); }
                // 左下角
                if (cornerLB > 0) { leftBorder = Mathf.Max(leftBorder, cornerLB + 1); bottomBorder = Mathf.Max(bottomBorder, cornerLB + 1); }
                // 右下角
                if (cornerRB > 0) { rightBorder = Mathf.Max(rightBorder, cornerRB + 1); bottomBorder = Mathf.Max(bottomBorder, cornerRB + 1); }

                // 同步更新 stretch range（border 变大后拉伸区可能缩小）
                int newHStart = Mathf.Max(hStretch.start, leftBorder);
                int newHEnd = Mathf.Min(hStretch.start + hStretch.length - 1, width - 1 - rightBorder);
                if (newHEnd > newHStart)
                    hStretch = new RangeInt(newHStart, newHEnd - newHStart + 1);
                else
                    return fail; // border 占满了

                int newVStart = Mathf.Max(vStretch.start, bottomBorder);
                int newVEnd = Mathf.Min(vStretch.start + vStretch.length - 1, height - 1 - topBorder);
                if (newVEnd > newVStart)
                    vStretch = new RangeInt(newVStart, newVEnd - newVStart + 1);
                else
                    return fail;
            }

            // 6. 验证边框合理性：拉伸轴的border不能占满（短轴可以占满——胶囊就是这样）
            if (isCapsule)
            {
                // 胶囊：只验证长轴
                if (contentW > contentH)
                {
                    if (leftBorder + rightBorder >= width - 2) return fail;
                }
                else
                {
                    if (bottomBorder + topBorder >= height - 2) return fail;
                }
            }
            else
            {
                if (leftBorder + rightBorder >= width - 2) return fail;
                if (bottomBorder + topBorder >= height - 2) return fail;
            }

            return new NineSliceBorderResult
            {
                border = new Vector4(leftBorder, bottomBorder, rightBorder, topBorder),
                hAxisValid = hValid,
                vAxisValid = vValid
            };
        }

        private static bool IsThinDecorativeStrip(int contentW, int contentH)
        {
            var minDim = Mathf.Min(contentW, contentH);
            var maxDim = Mathf.Max(contentW, contentH);
            return minDim <= 12 && maxDim >= 64 && maxDim >= minDim * 6;
        }

        private static bool TryCalculateStrip9SliceBorder(
            Color32[] pixels, int width, int height,
            int cL, int cR, int cB, int cT,
            int contentW, int contentH,
            byte alphaThreshold, int variationTolerance,
            out NineSliceBorderResult result)
        {
            result = new NineSliceBorderResult { border = Vector4.zero, hAxisValid = false, vAxisValid = false };
            if (pixels == null || contentW <= 0 || contentH <= 0)
                return false;

            bool horizontalStrip = contentH <= 12 && contentW >= Mathf.Max(16, contentH * 4);
            bool verticalStrip = contentW <= 12 && contentH >= Mathf.Max(16, contentW * 4);
            if (!horizontalStrip && !verticalStrip)
                return false;

            if (horizontalStrip)
            {
                var hStretch = FindStretchByProfile(pixels, width, height, cL, cR, cB, cT, true, variationTolerance, alphaThreshold);
                bool hValid = hStretch.length >= 4 && (float)hStretch.length / contentW >= 0.15f;
                if (!hValid)
                    return false;

                int leftBorder = Mathf.Max(0, hStretch.start);
                int rightBorder = Mathf.Max(0, (width - 1) - (hStretch.start + hStretch.length - 1));
                if (leftBorder == 0 && rightBorder == 0 && width >= 4)
                {
                    leftBorder = 1;
                    rightBorder = 1;
                }
                if (leftBorder + rightBorder >= width - 1)
                    return false;

                int bottomBorder = Mathf.Max(0, cB);
                int topBorder = Mathf.Max(0, (height - 1) - cT);
                result = new NineSliceBorderResult
                {
                    border = new Vector4(leftBorder, bottomBorder, rightBorder, topBorder),
                    hAxisValid = true,
                    vAxisValid = false
                };
                Debug.Log($"[NineSlice:Strip] 水平线/横条 size={width}x{height}, border={result.border}, hStretch=({hStretch.start},{hStretch.length})");
                return true;
            }

            var vStretch = FindStretchByProfile(pixels, width, height, cL, cR, cB, cT, false, variationTolerance, alphaThreshold);
            bool vValid = vStretch.length >= 4 && (float)vStretch.length / contentH >= 0.15f;
            if (!vValid)
                return false;

            int bottom = Mathf.Max(0, vStretch.start);
            int top = Mathf.Max(0, (height - 1) - (vStretch.start + vStretch.length - 1));
            if (bottom == 0 && top == 0 && height >= 4)
            {
                bottom = 1;
                top = 1;
            }
            if (bottom + top >= height - 1)
                return false;

            int left = Mathf.Max(0, cL);
            int right = Mathf.Max(0, (width - 1) - cR);
            result = new NineSliceBorderResult
            {
                border = new Vector4(left, bottom, right, top),
                hAxisValid = false,
                vAxisValid = true
            };
            Debug.Log($"[NineSlice:Strip] 垂直线/竖条 size={width}x{height}, border={result.border}, vStretch=({vStretch.start},{vStretch.length})");
            return true;
        }

        /// <summary>
        /// 纹理复杂度检测 — 照片和复杂图案包含大量不同颜色，不适合九宫格
        /// 通过采样几条行/列，统计量化后的颜色种类数来判断
        /// </summary>
        private static bool IsComplexTexture(Color32[] pixels, int width, int height,
            int cL, int cR, int cB, int cT, int tolerance)
        {
            int contentW = cR - cL + 1;
            int contentH = cT - cB + 1;

            // 采样 5 条水平线，统计量化颜色数
            int totalUniqueColors = 0;
            int sampleCount = 0;
            int quantize = Mathf.Max(tolerance * 2, 8); // 量化步长

            for (int i = 0; i < 5; i++)
            {
                int y = cB + contentH * (i + 1) / 6;
                if (y < 0 || y >= height) continue;

                var colorSet = new HashSet<int>();
                for (int x = cL; x <= cR; x++)
                {
                    int idx = y * width + x;
                    if (idx >= pixels.Length) break;
                    var c = pixels[idx];
                    // 量化颜色以容忍微小差异
                    int key = ((c.r / quantize) << 16) | ((c.g / quantize) << 8) | (c.b / quantize) | ((c.a / quantize) << 24);
                    colorSet.Add(key);
                }
                totalUniqueColors += colorSet.Count;
                sampleCount++;
            }

            if (sampleCount == 0) return false;
            float avgColors = (float)totalUniqueColors / sampleCount;

            // 如果平均每条线有超过内容宽度30%的不同颜色，认为过于复杂
            return avgColors > contentW * 0.3f;
        }

        /// <summary>
        /// 列/行轮廓匹配法：比较相邻列(行)的完整交叉轴像素轮廓来寻找可拉伸区间。
        ///
        /// 核心思路：对于可九宫的图（按钮、面板、进度条底框等），中间区域的每一列/行
        /// 在交叉轴方向上的像素轮廓完全相同（或极接近），而边框区域（圆角、装饰、渐变边缘）
        /// 的各列/行轮廓各不相同。因此只需比较相邻列/行的完整轮廓，找到最长的一段
        /// 连续"轮廓相同"的列/行，即为可拉伸区间。
        ///
        /// 优势：
        /// - 天然排除圆角/胶囊边缘 — 圆角列的 alpha 轮廓与直线段列不同
        /// - 天然排除复杂图案 — 各列花纹不同导致轮廓不匹配
        /// - 支持渐变 — 相邻列颜色差异极小，在容差内仍算匹配
        /// - 不需要额外的均匀性验证或轮廓一致性验证（算法本身已包含）
        /// </summary>
        private static RangeInt FindStretchByProfile(
            Color32[] pixels, int width, int height,
            int cL, int cR, int cB, int cT,
            bool isHorizontal, int tolerance, byte alphaThreshold = 1)
        {
            int contentMain = isHorizontal ? (cR - cL + 1) : (cT - cB + 1);
            int mainStart = isHorizontal ? cL : cB;
            int crossStart = isHorizontal ? cB : cL;
            int crossEnd = isHorizontal ? cT : cR;
            int crossLen = crossEnd - crossStart + 1;

            if (contentMain < 8 || crossLen < 1) return new RangeInt(mainStart, 0);

            // 交叉轴采样点数：尽可能覆盖全轴（上限64，防止大图太慢）
            int numSamples = Mathf.Max(1, Mathf.Min(64, crossLen));

            // 逐对比较相邻列/行的轮廓是否匹配
            bool[] matchesNext = new bool[contentMain - 1];
            for (int i = 0; i < contentMain - 1; i++)
            {
                int pos1 = mainStart + i;
                int pos2 = mainStart + i + 1;
                matchesNext[i] = ProfilesMatch(pixels, width, height,
                    pos1, pos2, crossStart, crossLen, numSamples, isHorizontal, tolerance);
            }

            // 找最长连续匹配段 = 可拉伸区间
            int bestStart = 0, bestLen = 1;
            int curStart = 0, curLen = 1;
            for (int i = 0; i < matchesNext.Length; i++)
            {
                if (matchesNext[i])
                {
                    curLen++;
                }
                else
                {
                    if (curLen > bestLen)
                    {
                        bestLen = curLen;
                        bestStart = curStart;
                    }
                    else if (curLen == bestLen && curLen > 1)
                    {
                        // 同长度优先选靠中心的
                        int center = contentMain / 2;
                        if (Mathf.Abs(curStart + curLen / 2 - center) < Mathf.Abs(bestStart + bestLen / 2 - center))
                            bestStart = curStart;
                    }
                    curStart = i + 1;
                    curLen = 1;
                }
            }
            if (curLen > bestLen)
            {
                bestLen = curLen;
                bestStart = curStart;
            }
            else if (curLen == bestLen && curLen > 1)
            {
                int center = contentMain / 2;
                if (Mathf.Abs(curStart + curLen / 2 - center) < Mathf.Abs(bestStart + bestLen / 2 - center))
                    bestStart = curStart;
            }

            // ── Phase 2: Alpha 边界精炼 ──
            // 排除因相邻列渐进漂移而误包含的圆角区域
            RefineStretchByAlphaEnvelope(pixels, width, height,
                mainStart, crossStart, crossEnd,
                isHorizontal, alphaThreshold,
                ref bestStart, ref bestLen);

            return new RangeInt(mainStart + bestStart, bestLen);
        }

        /// <summary>
        /// Alpha 边界精炼：相邻列匹配可能因渐进 alpha 漂移而将圆角区域误包含在拉伸区。
        /// 以中心列为参考，检查拉伸区边缘各列的不透明范围是否与中心一致。
        /// 如果边缘列的不透明范围内缩超过 maxIndent，则从拉伸区排除（通过二分查找确定边界）。
        /// </summary>
        private static void RefineStretchByAlphaEnvelope(
            Color32[] pixels, int width, int height,
            int mainStart, int crossStart, int crossEnd,
            bool isHorizontal, byte alphaThreshold,
            ref int bestStart, ref int bestLen)
        {
            if (bestLen <= 8) return;

            int centerIdx = bestStart + bestLen / 2;
            int centerPos = mainStart + centerIdx;

            int cf, cl;
            FindOpaqueRange(pixels, width, height, centerPos, crossStart, crossEnd,
                isHorizontal, alphaThreshold, out cf, out cl);
            if (cf < 0 || cl < 0 || cl <= cf) return;

            // 自适应容差：短轴圆角缩进与轴长成比例，避免矮宽/窄高图被过度精炼
            int crossLenR = crossEnd - crossStart + 1;
            int maxIndent = Mathf.Clamp(crossLenR / 8, 2, 6);

            // 快速检查：如果拉伸区两端都通过，无需精炼（纯色/渐变背景走此路径）
            int edgeLeftPos = mainStart + bestStart;
            int edgeRightPos = mainStart + bestStart + bestLen - 1;
            int lf, ll, rf, rl;
            FindOpaqueRange(pixels, width, height, edgeLeftPos, crossStart, crossEnd,
                isHorizontal, alphaThreshold, out lf, out ll);
            FindOpaqueRange(pixels, width, height, edgeRightPos, crossStart, crossEnd,
                isHorizontal, alphaThreshold, out rf, out rl);

            bool leftOk = lf >= 0 && (lf - cf) <= maxIndent && (cl - ll) <= maxIndent;
            bool rightOk = rf >= 0 && (rf - cf) <= maxIndent && (cl - rl) <= maxIndent;

            if (leftOk && rightOk) return;

            int refinedLeft = bestStart;
            int refinedRight = bestStart + bestLen - 1;

            // 二分查找左边界：最左侧通过 alpha 检查的列
            if (!leftOk)
            {
                int lo = bestStart, hi = centerIdx;
                while (lo < hi)
                {
                    int mid = (lo + hi) / 2;
                    int f, l;
                    FindOpaqueRange(pixels, width, height, mainStart + mid, crossStart, crossEnd,
                        isHorizontal, alphaThreshold, out f, out l);
                    if (f >= 0 && (f - cf) <= maxIndent && (cl - l) <= maxIndent)
                        hi = mid;
                    else
                        lo = mid + 1;
                }
                refinedLeft = lo;
            }

            // 二分查找右边界：最右侧通过 alpha 检查的列
            if (!rightOk)
            {
                int lo = centerIdx, hi = bestStart + bestLen - 1;
                while (lo < hi)
                {
                    int mid = (lo + hi + 1) / 2;
                    int f, l;
                    FindOpaqueRange(pixels, width, height, mainStart + mid, crossStart, crossEnd,
                        isHorizontal, alphaThreshold, out f, out l);
                    if (f >= 0 && (f - cf) <= maxIndent && (cl - l) <= maxIndent)
                        lo = mid;
                    else
                        hi = mid - 1;
                }
                refinedRight = lo;
            }

            bestStart = refinedLeft;
            bestLen = refinedRight - refinedLeft + 1;
        }

        /// <summary>
        /// 边框/框架图形专用检测算法。
        /// 适用于矮宽/窄高的圆角矩形边框、进度条、渐变边框等形状。
        ///
        /// 原理：通过比较每列(行)的 alpha 轮廓(首个不透明像素、末个不透明像素、不透明数量)
        /// 来判断可拉伸区间。边框图形的特征是中间大段列(行)具有相同的不透明轮廓，
        /// 仅圆角处变化。这种粗粒度比较比逐像素轮廓匹配对抗锯齿更鲁棒。
        ///
        /// 还会验证拉伸区内沿主轴的像素一致性，以区分:
        /// - 纯色/垂直渐变边框 → 水平可拉伸
        /// - 纯色/水平渐变边框 → 垂直可拉伸
        /// - 沿主轴渐变 → 不可拉伸 (拉伸会撕裂渐变)
        /// </summary>
        private static RangeInt DetectBorderFrameStretch(
            Color32[] pixels, int width, int height,
            int cL, int cR, int cB, int cT,
            bool isHorizontal, int tolerance, byte alphaThreshold)
        {
            int contentMain = isHorizontal ? (cR - cL + 1) : (cT - cB + 1);
            int mainStart = isHorizontal ? cL : cB;
            int crossStart = isHorizontal ? cB : cL;
            int crossEnd = isHorizontal ? cT : cR;
            int crossLen = crossEnd - crossStart + 1;

            if (contentMain < 8 || crossLen < 4) return new RangeInt(mainStart, 0);

            // 1. 计算每列(行)的 alpha 轮廓: (首个不透明, 末个不透明, 不透明计数)
            int[] silFirst = new int[contentMain];
            int[] silLast = new int[contentMain];
            int[] silCount = new int[contentMain];

            for (int i = 0; i < contentMain; i++)
            {
                int pos = mainStart + i;
                silFirst[i] = -1;
                silLast[i] = -1;
                silCount[i] = 0;

                for (int c = crossStart; c <= crossEnd; c++)
                {
                    int idx;
                    if (isHorizontal)
                    {
                        if (pos >= width || c >= height) continue;
                        idx = c * width + pos;
                    }
                    else
                    {
                        if (pos >= height || c >= width) continue;
                        idx = pos * width + c;
                    }
                    if (idx >= 0 && idx < pixels.Length && pixels[idx].a >= alphaThreshold)
                    {
                        if (silFirst[i] < 0) silFirst[i] = c;
                        silLast[i] = c;
                        silCount[i]++;
                    }
                }
            }

            // 2. 比较相邻列轮廓 (容许自适应容差，适配圆角抗锯齿)
            int silTolerance = Mathf.Clamp(crossLen / 8, 2, 6);
            bool[] matches = new bool[contentMain - 1];

            for (int i = 0; i < contentMain - 1; i++)
            {
                if (silFirst[i] < 0 && silFirst[i + 1] < 0)
                {
                    matches[i] = true; // 均空 = 匹配
                    continue;
                }
                if (silFirst[i] < 0 || silFirst[i + 1] < 0)
                {
                    matches[i] = false; // 一空一有 = 不匹配
                    continue;
                }
                matches[i] = Mathf.Abs(silFirst[i] - silFirst[i + 1]) <= silTolerance &&
                              Mathf.Abs(silLast[i] - silLast[i + 1]) <= silTolerance &&
                              Mathf.Abs(silCount[i] - silCount[i + 1]) <= silTolerance;
            }

            // 3. 找最长连续匹配段
            int bestStart = 0, bestLen = 1;
            int curStart = 0, curLen = 1;
            for (int i = 0; i < matches.Length; i++)
            {
                if (matches[i])
                {
                    curLen++;
                }
                else
                {
                    if (curLen > bestLen) { bestLen = curLen; bestStart = curStart; }
                    curStart = i + 1;
                    curLen = 1;
                }
            }
            if (curLen > bestLen) { bestLen = curLen; bestStart = curStart; }

            if (bestLen < 4) return new RangeInt(mainStart, 0);

            // 4. 端点漂移修剪 — 防止圆角渐变轮廓逐列累积导致全宽误匹配
            //    每列只与相邻列差1-2px（在tolerance内），但首尾累计差几十px
            //    修剪方法：以中心列为参考，从两端裁去与中心不匹配的列
            if (bestLen >= 8)
            {
                int refIdx = bestStart + bestLen / 2;
                // 从左侧裁剪
                while (bestLen > 4 && bestStart < refIdx)
                {
                    int i = bestStart;
                    bool matchRef = (silFirst[i] < 0 && silFirst[refIdx] < 0) ||
                        (silFirst[i] >= 0 && silFirst[refIdx] >= 0 &&
                         Mathf.Abs(silFirst[i] - silFirst[refIdx]) <= silTolerance &&
                         Mathf.Abs(silLast[i] - silLast[refIdx]) <= silTolerance &&
                         Mathf.Abs(silCount[i] - silCount[refIdx]) <= silTolerance);
                    if (matchRef) break;
                    bestStart++;
                    bestLen--;
                }
                // 从右侧裁剪
                while (bestLen > 4 && (bestStart + bestLen - 1) > refIdx)
                {
                    int i = bestStart + bestLen - 1;
                    bool matchRef = (silFirst[i] < 0 && silFirst[refIdx] < 0) ||
                        (silFirst[i] >= 0 && silFirst[refIdx] >= 0 &&
                         Mathf.Abs(silFirst[i] - silFirst[refIdx]) <= silTolerance &&
                         Mathf.Abs(silLast[i] - silLast[refIdx]) <= silTolerance &&
                         Mathf.Abs(silCount[i] - silCount[refIdx]) <= silTolerance);
                    if (matchRef) break;
                    bestLen--;
                }
            }

            if (bestLen < 4) return new RangeInt(mainStart, 0);

            // 5. 验证拉伸区内沿主轴的像素一致性（防止沿主轴渐变被错误标记为可拉伸）
            if (!VerifyStretchZoneUniformity(pixels, width, height,
                mainStart + bestStart, bestLen, crossStart, crossEnd, isHorizontal, tolerance))
            {
                Debug.Log($"[NineSlice:BorderFrame] axis={(isHorizontal ? 'H' : 'V')} 轮廓匹配但像素一致性验证失败, start={mainStart + bestStart}, len={bestLen}");
                return new RangeInt(mainStart, 0);
            }

            Debug.Log($"[NineSlice:BorderFrame] axis={(isHorizontal ? 'H' : 'V')} 命中! start={mainStart + bestStart}, len={bestLen}, silTol={silTolerance}");
            return new RangeInt(mainStart + bestStart, bestLen);
        }

        /// <summary>
        /// 验证拉伸区域内沿主轴的像素一致性。
        /// 沿交叉轴取数条采样线，在每条线上检查拉伸区首、中、尾像素是否相似。
        /// 这确保沿主轴无显著渐变（沿主轴渐变会被拉伸撕裂）。
        /// 同时允许沿交叉轴的渐变（不影响拉伸质量）。
        /// </summary>
        private static bool VerifyStretchZoneUniformity(
            Color32[] pixels, int width, int height,
            int stretchStart, int stretchLen,
            int crossStart, int crossEnd,
            bool isHorizontal, int tolerance)
        {
            if (stretchLen < 4) return true;
            int crossLen = crossEnd - crossStart + 1;
            int numLines = Mathf.Min(8, crossLen);
            if (numLines < 1) return true;

            // 拉伸区首尾允许较宽松的颜色差异（边框图形可能有细微渐变）
            int edgeTolerance = Mathf.Max(tolerance * 4, 16);
            int passCount = 0;

            for (int li = 0; li < numLines; li++)
            {
                int crossPos = crossStart + crossLen * (li + 1) / (numLines + 1);

                int posFirst = stretchStart;
                int posLast = stretchStart + stretchLen - 1;
                int posMid = stretchStart + stretchLen / 2;
                // 额外取 1/4 和 3/4 位置
                int posQ1 = stretchStart + stretchLen / 4;
                int posQ3 = stretchStart + stretchLen * 3 / 4;

                Color32 pxFirst, pxLast, pxMid, pxQ1, pxQ3;

                if (isHorizontal)
                {
                    if (posFirst >= width || posLast >= width || crossPos >= height) { passCount++; continue; }
                    pxFirst = pixels[crossPos * width + posFirst];
                    pxLast  = pixels[crossPos * width + posLast];
                    pxMid   = pixels[crossPos * width + posMid];
                    pxQ1    = pixels[crossPos * width + posQ1];
                    pxQ3    = pixels[crossPos * width + posQ3];
                }
                else
                {
                    if (posFirst >= height || posLast >= height || crossPos >= width) { passCount++; continue; }
                    pxFirst = pixels[posFirst * width + crossPos];
                    pxLast  = pixels[posLast  * width + crossPos];
                    pxMid   = pixels[posMid   * width + crossPos];
                    pxQ1    = pixels[posQ1    * width + crossPos];
                    pxQ3    = pixels[posQ3    * width + crossPos];
                }

                // 五点互查：首尾中及 1/4、3/4 位置应相似
                bool ok = IsSimilarColor(pxFirst, pxLast, edgeTolerance) &&
                          IsSimilarColor(pxFirst, pxMid, edgeTolerance) &&
                          IsSimilarColor(pxQ1, pxQ3, edgeTolerance);
                if (ok) passCount++;
            }

            // 至少 60% 的采样线通过
            return passCount >= Mathf.Max(1, Mathf.RoundToInt(numLines * 0.6f));
        }

        /// <summary>
        /// 比较两个相邻列(行)的完整交叉轴轮廓是否匹配。
        /// 沿交叉轴均匀采样 numSamples 个点，逐点比较两列/行对应位置的像素颜色。
        /// 允许 ≤5% 的采样点不匹配（容忍抗锯齿/压缩噪声）。
        /// </summary>
        private static bool ProfilesMatch(
            Color32[] pixels, int width, int height,
            int pos1, int pos2,
            int crossStart, int crossLen, int numSamples,
            bool isHorizontal, int tolerance)
        {
            int mismatchCount = 0;
            int maxMismatch = Mathf.Max(2, numSamples / 10); // 允许 10% 误差（小图抗锯齿容忍）

            for (int s = 0; s < numSamples; s++)
            {
                // 均匀分布采样点（避免首尾聚集）
                int crossPos = crossStart + (crossLen * s + crossLen / 2) / numSamples;
                Color32 c1, c2;
                if (isHorizontal)
                {
                    if (pos1 < 0 || pos1 >= width || pos2 >= width || crossPos < 0 || crossPos >= height) continue;
                    c1 = pixels[crossPos * width + pos1];
                    c2 = pixels[crossPos * width + pos2];
                }
                else
                {
                    if (pos1 < 0 || pos1 >= height || pos2 >= height || crossPos < 0 || crossPos >= width) continue;
                    c1 = pixels[pos1 * width + crossPos];
                    c2 = pixels[pos2 * width + crossPos];
                }

                if (!IsSimilarColor(c1, c2, tolerance))
                {
                    mismatchCount++;
                    if (mismatchCount > maxMismatch) return false; // 快速失败
                }
            }
            return true;
        }

        private static Rect FindContentBounds(Color32[] pixels, int width, int height, byte alphaThreshold)
        {
            int minX = width, minY = height, maxX = -1, maxY = -1;
            bool found = false;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a >= alphaThreshold)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                        found = true;
                    }
                }
            }

            if (!found) return new Rect(0, 0, width, height);
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>
        /// 测量图片某个角的圆角半径（像素）。
        /// 从角点出发沿对角线方向扫描，找到第一个不透明像素，该距离即为圆角半径估计。
        /// </summary>
        private static int MeasureCornerRadius(Color32[] pixels, int width, int height,
            int cornerX, int cornerY, bool isLeft, bool isTop, byte alphaThreshold)
        {
            // 沿角的两条边扫描，取较大值
            int maxR = Mathf.Min(width, height) / 2;
            int radiusH = 0, radiusV = 0;

            // 沿水平边（固定 y=cornerY，从角点向内扫描）
            for (int d = 0; d < maxR; d++)
            {
                int x = isLeft ? cornerX + d : cornerX - d;
                int y = cornerY;
                if (x < 0 || x >= width || y < 0 || y >= height) break;
                if (pixels[y * width + x].a >= alphaThreshold)
                {
                    radiusH = d;
                    break;
                }
                radiusH = d + 1;
            }

            // 沿垂直边（固定 x=cornerX，从角点向内扫描）
            for (int d = 0; d < maxR; d++)
            {
                int x = cornerX;
                int y = isTop ? cornerY - d : cornerY + d;
                if (x < 0 || x >= width || y < 0 || y >= height) break;
                if (pixels[y * width + x].a >= alphaThreshold)
                {
                    radiusV = d;
                    break;
                }
                radiusV = d + 1;
            }

            return Mathf.Max(radiusH, radiusV);
        }

        private static bool IsSimilarColor(Color32 c1, Color32 c2, int tolerance)
        {
            if (Mathf.Abs(c1.a - c2.a) > tolerance) return false;
            return Mathf.Abs(c1.r - c2.r) <= tolerance &&
                   Mathf.Abs(c1.g - c2.g) <= tolerance &&
                   Mathf.Abs(c1.b - c2.b) <= tolerance;
        }

        /// <summary>
        /// 沿交叉轴查找某列(行)的不透明像素起止位置。
        /// 用于 alpha 边界精炼：圆角区域的列/行比中心列/行的不透明范围短（内缩）。
        /// </summary>
        private static void FindOpaqueRange(
            Color32[] pixels, int width, int height,
            int mainPos, int crossStart, int crossEnd,
            bool isHorizontal, byte alphaThreshold,
            out int firstOpaque, out int lastOpaque)
        {
            firstOpaque = -1;
            lastOpaque = -1;
            for (int c = crossStart; c <= crossEnd; c++)
            {
                int idx;
                if (isHorizontal)
                {
                    if (mainPos < 0 || mainPos >= width || c < 0 || c >= height) continue;
                    idx = c * width + mainPos;
                }
                else
                {
                    if (mainPos < 0 || mainPos >= height || c < 0 || c >= width) continue;
                    idx = mainPos * width + c;
                }
                if (idx >= 0 && idx < pixels.Length && pixels[idx].a >= alphaThreshold)
                {
                    if (firstOpaque < 0) firstOpaque = c;
                    lastOpaque = c;
                }
            }
        }

        /// <summary>
        /// 检查两列像素是否相似（用于兼容旧调用）
        /// </summary>
        private static bool CheckColumnSimilarity(Color32[] pixels, int width, int x1, int x2, int yStart, int yEnd, int tolerance)
        {
            for (int y = yStart; y <= yEnd; y++)
            {
                int idx1 = Mathf.Min(y * width + x1, pixels.Length - 1);
                int idx2 = Mathf.Min(y * width + x2, pixels.Length - 1);
                if (!IsSimilarColor(pixels[idx1], pixels[idx2], tolerance)) return false;
            }
            return true;
        }

        /// <summary>
        /// 检查两行像素是否相似（用于兼容旧调用）
        /// </summary>
        private static bool CheckRowSimilarity(Color32[] pixels, int width, int y1, int y2, int xStart, int xEnd, int tolerance)
        {
            for (int x = xStart; x <= xEnd; x++)
            {
                int idx1 = Mathf.Min(y1 * width + x, pixels.Length - 1);
                int idx2 = Mathf.Min(y2 * width + x, pixels.Length - 1);
                if (!IsSimilarColor(pixels[idx1], pixels[idx2], tolerance)) return false;
            }
            return true;
        }

        /// <summary>列（垂直条）相似度：比较 x=x1 列与 x=x2 列在 [yStart,yEnd] 行范围内各像素是否相似。</summary>
        private static bool CheckColSimilarity(Color32[] pixels, int width, int x1, int x2, int yStart, int yEnd, int tolerance)
        {
            for (int y = yStart; y <= yEnd; y++)
            {
                int idx1 = Mathf.Min(y * width + x1, pixels.Length - 1);
                int idx2 = Mathf.Min(y * width + x2, pixels.Length - 1);
                if (!IsSimilarColor(pixels[idx1], pixels[idx2], tolerance)) return false;
            }
            return true;
        }
        #endregion

        #region 九宫格裁剪导出
        /// <summary>
        /// 对九宫格纹理进行裁剪：移除拉伸区域中的冗余像素，只保留边框+拉伸带。
        /// 返回裁剪后的 Texture2D 和调整后的 spriteBorder。
        /// 如果纹理不适合裁剪（太小、无有效拉伸区、复杂图案），返回 null。
        /// </summary>
        public static Texture2D CropNineSliceTexture(Texture2D source, out Vector4 adjustedBorder, byte alphaThreshold = 1, int variationTolerance = 3, int keepPixels = 2)
        {
            adjustedBorder = Vector4.zero;
            if (source == null) return null;

            int w = source.width;
            int h = source.height;

            // 先用内部方法做完整验证（含复杂度检测），同时获取各轴有效性
            var result = CalculateTexture9SliceBorderInternal(source, alphaThreshold, variationTolerance);
            var border = result.border;
            if (border == Vector4.zero)
            {
                Debug.Log($"[NineSlice:Crop] 跳过: 不适合九宫格 size={w}x{h}");
                return null;
            }

            Color32[] pixels = source.GetPixels32();
            Rect contentRect = FindContentBounds(pixels, w, h, alphaThreshold);
            int cLeft = (int)contentRect.xMin;
            int cRight = (int)contentRect.xMax;
            int cBottom = (int)contentRect.yMin;
            int cTop = (int)contentRect.yMax;

            // 从 border 反推拉伸区域（border 是相对于图片边缘的）
            int hStretchStart = (int)border.x;
            int hStretchEnd = (w - 1) - (int)border.z;
            int vStretchStart = (int)border.y;
            int vStretchEnd = (h - 1) - (int)border.w;

            int hStretchLen = hStretchEnd - hStretchStart + 1;
            int vStretchLen = vStretchEnd - vStretchStart + 1;

            Debug.Log($"[NineSlice:Crop] size={w}x{h}, content=({cLeft},{cBottom})-({cRight},{cTop}), hStretch=(start={hStretchStart},len={hStretchLen}), vStretch=(start={vStretchStart},len={vStretchLen})");

            // 可裁剪的像素数
            int hCropCount = Mathf.Max(0, hStretchLen - keepPixels);
            int vCropCount = Mathf.Max(0, vStretchLen - keepPixels);

            // 只裁剪真正通过验证的轴；无效轴的 border 来自假安全区间，不能裁
            if (!result.hAxisValid) hCropCount = 0;
            if (!result.vAxisValid) vCropCount = 0;

            // 原始尺寸太小的轴不裁剪
            const int minDimForCrop = 20;
            if (w < minDimForCrop) hCropCount = 0;
            if (h < minDimForCrop) vCropCount = 0;

            // 拉伸区占比≥15%的轴才值得裁剪（与检测阈值一致）
            float hRatio = (float)hStretchLen / w;
            float vRatio = (float)vStretchLen / h;
            if (hRatio < 0.15f) hCropCount = 0;
            if (vRatio < 0.15f) vCropCount = 0;

            if (hCropCount <= 0 && vCropCount <= 0)
            {
                Debug.Log($"[NineSlice:Crop] 跳过: 裁剪量不足 hRatio={hRatio:P0}, vRatio={vRatio:P0}");
                return null;
            }

            // 保留拉伸区像素数下限
            const int minResultDim = 16;   // 裁剪后每轴最少像素
            const int minKeepPixels = 2;   // 拉伸保留区最少像素
            int hKeepMin = Mathf.Max(minKeepPixels, Mathf.Max((int)border.x, (int)border.z));
            int vKeepMin = Mathf.Max(minKeepPixels, Mathf.Max((int)border.y, (int)border.w));

            // 裁剪量上限：保留 left+right border + 少量拉伸像素即可
            // 九宫格拉伸区的像素是重复的，裁到只剩几像素不影响渲染
            int maxHCrop = Mathf.Max(0, w - (int)border.x - (int)border.z - minKeepPixels);
            int maxVCrop = Mathf.Max(0, h - (int)border.y - (int)border.w - minKeepPixels);

            if (hCropCount > 0)
            {
                hCropCount = Mathf.Min(hCropCount, hStretchLen - hKeepMin);
                hCropCount = Mathf.Min(hCropCount, maxHCrop);
                hCropCount = Mathf.Max(0, hCropCount);
                if (w - hCropCount < minResultDim) hCropCount = Mathf.Max(0, w - minResultDim);
            }
            if (vCropCount > 0)
            {
                vCropCount = Mathf.Min(vCropCount, vStretchLen - vKeepMin);
                vCropCount = Mathf.Min(vCropCount, maxVCrop);
                vCropCount = Mathf.Max(0, vCropCount);
                if (h - vCropCount < minResultDim) vCropCount = Mathf.Max(0, h - minResultDim);
            }

            if (hCropCount <= 0 && vCropCount <= 0)
            {
                Debug.Log($"[NineSlice:Crop] 跳过: 约束后裁剪量为零");
                return null;
            }

            int newW = w - hCropCount;
            int newH = h - vCropCount;
            if (newW < 4 || newH < 4) return null;

            int actualHKeep = hStretchLen - hCropCount;
            int actualVKeep = vStretchLen - vCropCount;

            // 从拉伸区中心保留像素（渐变纹理中心比边缘更具代表性）
            int hSkipLeft = hCropCount / 2;
            int hSkipRight = hCropCount - hSkipLeft;
            int hSkipLStart = hStretchStart;                          // 左跳区起
            int hSkipLEnd   = hStretchStart + hSkipLeft;              // 左跳区止
            int hKeepEnd    = hSkipLEnd + actualHKeep;                // 保留区止
            int hSkipREnd   = hStretchStart + hStretchLen;            // 右跳区止 = 拉伸区末尾

            int vSkipBottom = vCropCount / 2;
            int vSkipTop    = vCropCount - vSkipBottom;
            int vSkipBStart = vStretchStart;
            int vSkipBEnd   = vStretchStart + vSkipBottom;
            int vKeepEnd    = vSkipBEnd + actualVKeep;
            int vSkipTEnd   = vStretchStart + vStretchLen;

            Color32[] newPixels = new Color32[newW * newH];

            int dstY = 0;
            for (int srcY = 0; srcY < h; srcY++)
            {
                // 跳过垂直拉伸区的上下两段
                if (vCropCount > 0 && srcY >= vSkipBStart && srcY < vSkipBEnd) continue;
                if (vCropCount > 0 && srcY >= vKeepEnd && srcY < vSkipTEnd) continue;
                int dstX = 0;
                for (int srcX = 0; srcX < w; srcX++)
                {
                    // 跳过水平拉伸区的左右两段
                    if (hCropCount > 0 && srcX >= hSkipLStart && srcX < hSkipLEnd) continue;
                    if (hCropCount > 0 && srcX >= hKeepEnd && srcX < hSkipREnd) continue;
                    newPixels[dstY * newW + dstX] = pixels[srcY * w + srcX];
                    dstX++;
                }
                dstY++;
            }

            var cropped = new Texture2D(newW, newH, TextureFormat.RGBA32, false);
            cropped.alphaIsTransparency = true;
            cropped.SetPixels32(newPixels);
            cropped.Apply();

            // 裁剪后 border 重算 — 基于裁剪后的图片尺寸
            // 裁剪仅移除拉伸区中间的冗余像素，左/右/上/下边框区域完全保留
            int newLeftBorder = hStretchStart;
            int newRightBorder = newW - (hStretchStart + actualHKeep);
            int newBottomBorder = vStretchStart;
            int newTopBorder = newH - (vStretchStart + actualVKeep);

            // 如果某个方向没有裁剪，border 使用原值
            if (hCropCount <= 0) { newLeftBorder = (int)border.x; newRightBorder = (int)border.z; }
            if (vCropCount <= 0) { newBottomBorder = (int)border.y; newTopBorder = (int)border.w; }

            // 安全钳位：线条类资源允许短轴 border 为 0；只要求两边之和小于尺寸。
            ClampSpriteBorderPair(ref newLeftBorder, ref newRightBorder, newW);
            ClampSpriteBorderPair(ref newBottomBorder, ref newTopBorder, newH);

            adjustedBorder = new Vector4(newLeftBorder, newBottomBorder, newRightBorder, newTopBorder);

            Debug.Log($"[NineSlice:Crop] 裁剪成功! {w}x{h} -> {newW}x{newH}, " +
                $"原始border=({border.x},{border.y},{border.z},{border.w}), " +
                $"裁剪后border=({adjustedBorder.x},{adjustedBorder.y},{adjustedBorder.z},{adjustedBorder.w}), " +
                $"拉伸保留=H{actualHKeep}/V{actualVKeep}");
            return cropped;
        }

        private static void ClampSpriteBorderPair(ref int leading, ref int trailing, int size)
        {
            if (size <= 1)
            {
                leading = 0;
                trailing = 0;
                return;
            }

            leading = Mathf.Clamp(leading, 0, size - 1);
            trailing = Mathf.Clamp(trailing, 0, size - leading - 1);
        }
        #endregion
        /// <summary>
        /// 把PS的字体样式同步设置到UGUI Text
        /// </summary>
        /// <param name="txtLayer"></param>
        /// <param name="text"></param>
        internal static TextLayerInfo SetTextStyle(PsdLayerNode txtLayer, UnityEngine.UI.Text text)
        {
            if (text == null) return default;
            text.gameObject.SetActive(txtLayer != null);
            if (txtLayer != null && txtLayer.ParseTextLayerInfo(out var textInfo))
            {
                bool isMultiLine = IsLikelyMultiLineText(txtLayer, in textInfo);
                var tFont = FindFontAsset(textInfo.FontName);
                if (tFont == null && !string.IsNullOrWhiteSpace(textInfo.FontName))
                {
                    // 项目中未找到对应字体，尝试使用系统字体回退
                    tFont = Font.CreateDynamicFontFromOSFont(textInfo.FontName, textInfo.FontSize);
                    Debug.LogWarning($"[PSD2UIForm] 字体 '{textInfo.FontName}' 未在项目中找到，使用系统字体回退");
                }
                if (tFont != null) text.font = tFont;
                text.text = textInfo.Text;
                text.fontSize = textInfo.FontSize;
                text.fontStyle = textInfo.FontStyle;
                text.color = textInfo.Color;
                text.resizeTextForBestFit = false;
                text.lineSpacing = ConvertPsdLeadingToUGUILineSpacing(text, in textInfo);
                text.horizontalOverflow = isMultiLine ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                ApplyUGUITextEffects(text, in textInfo);
                return textInfo;
            }
            else if (txtLayer != null)
            {
                Debug.LogWarning($"[PSD2UIForm] 文本图层 '{txtLayer.name}' ParseTextLayerInfo 失败, BindPsdLayer={(txtLayer.BindPsdLayer != null ? "OK" : "null")}, LayerType={txtLayer.LayerType}");
            }
            return default;
        }
        /// <summary>
        /// 把PS的字体样式同步设置到TextMeshProUGUI
        /// </summary>
        /// <param name="txtLayer"></param>
        /// <param name="text"></param>
        public static void SetTextStyle(PsdLayerNode txtLayer, TextMeshProUGUI text)
        {
            if (text == null) return;
            text.gameObject.SetActive(txtLayer != null);
            if (txtLayer != null && txtLayer.ParseTextLayerInfo(out var textInfo))
            {
                bool isMultiLine = IsLikelyMultiLineText(txtLayer, in textInfo);
                var tFont = FindTMPFontAsset(textInfo.FontName);
                if (tFont == null && !string.IsNullOrWhiteSpace(textInfo.FontName))
                {
                    // 项目中未找到TMP字体，尝试从系统字体创建
                    var systemFont = Font.CreateDynamicFontFromOSFont(textInfo.FontName, textInfo.FontSize);
                    if (systemFont != null)
                    {
                        tFont = FindTMPFontAsset(systemFont.name) ?? CreateTMPFontAsset(systemFont);
                    }
                    if (tFont == null) tFont = text.font;
                    Debug.LogWarning($"[PSD2UIForm] TMP字体 '{textInfo.FontName}' 未在项目中找到，使用{(tFont != null ? tFont.name : "默认")}回退");
                }
                else
                {
                    tFont ??= text.font;
                }
                if (tFont != null)
                {
                    EnsureTMPFontHasCharacters(tFont, textInfo.Text);
                    text.font = tFont;
                    if (tFont.material != null)
                    {
                        text.fontSharedMaterial = tFont.material;
                    }
                }
                text.text = textInfo.Text ?? string.Empty;
                text.fontSize = textInfo.FontSize;
                text.fontStyle = textInfo.TMPFontStyle;
                text.color = textInfo.Color;
                ApplyTMPVertexGradient(text, in textInfo);
                text.characterSpacing = textInfo.CharacterSpacing;
                text.lineSpacing = ConvertPsdLeadingToTMPLineSpacing(text, in textInfo);
                text.enableAutoSizing = false;
                text.enableWordWrapping = isMultiLine;
                text.overflowMode = TextOverflowModes.Overflow;
                text.verticalAlignment = VerticalAlignmentOptions.Geometry;
                text.margin = Vector4.zero;

                bool hasSplitGlowLayer = false;
                bool hasSplitStrokeLayer = false;
                TextMeshProUGUI splitGlowText = null;
                TextMeshProUGUI splitShadowText = null;
                var splitStrokeTexts = new List<TextMeshProUGUI>();
                var outsideStrokes = GetTMPOutsideStrokes(in textInfo);
                bool shouldSplitGlowLayer = textInfo.HasOutline && textInfo.HasGlow;
                bool shouldSplitStrokeLayer = outsideStrokes.Count > 0;

                if (shouldSplitGlowLayer)
                {
                    var glowText = GetOrCreateSplitTMPGlowLayer(text);
                    if (glowText != null)
                    {
                        CopyTMPTextState(text, glowText);
                        var glowTextInfo = CreateTMPGlowOnlyTextInfo(textInfo);
                        ApplyTMPTextEffects(glowText, in glowTextInfo, true);
                        SyncSplitTMPGlowLayerRect(text);
                        glowText.ForceMeshUpdate();
                        splitGlowText = glowText;
                        hasSplitGlowLayer = true;
                    }
                }
                else
                {
                    RemoveSplitTMPGlowLayer(text);
                }

                if (shouldSplitStrokeLayer)
                {
                    RemoveSplitTMPStrokeLayer(text);
                    bool useShadowCompanion = textInfo.HasShadow && ShouldUseTMPShadowCompanion(text, in textInfo);
                    for (int i = 0; i < outsideStrokes.Count; i++)
                    {
                        var strokeText = GetOrCreateSplitTMPStrokeLayer(text, i, outsideStrokes.Count);
                        if (strokeText == null)
                        {
                            continue;
                        }

                        CopyTMPTextState(text, strokeText);
                        ClearTMPVertexGradient(strokeText);
                        var strokeTextInfo = CreateTMPOutsideStrokeLayerTextInfo(
                            textInfo,
                            outsideStrokes[i],
                            i == 0 && !useShadowCompanion);
                        strokeText.color = strokeTextInfo.Color;
                        ApplyTMPTextEffects(strokeText, in strokeTextInfo, false, true);
                        strokeText.ForceMeshUpdate();
                        splitStrokeTexts.Add(strokeText);
                        hasSplitStrokeLayer = true;
                    }

                    if (useShadowCompanion && splitStrokeTexts.Count > 0)
                    {
                        var shadowText = GetOrCreateSplitTMPShadowLayer(text);
                        if (shadowText != null)
                        {
                            CopyTMPTextState(text, shadowText);
                            ClearTMPVertexGradient(shadowText);
                            var shadowTextInfo = CreateTMPOutsideShadowLayerTextInfo(textInfo, outsideStrokes[0]);
                            shadowText.color = shadowTextInfo.Color;
                            ApplyTMPTextEffects(shadowText, in shadowTextInfo, false, true, true);
                            shadowText.rectTransform.anchoredPosition += textInfo.ShadowOffset;
                            shadowText.ForceMeshUpdate();
                            splitShadowText = shadowText;
                        }
                    }
                    else
                    {
                        RemoveSplitTMPShadowLayer(text);
                    }
                }
                else
                {
                    RemoveSplitTMPStrokeLayer(text);
                    RemoveSplitTMPShadowLayer(text);
                }

                var mainTextInfo = hasSplitStrokeLayer
                    ? CreateTMPOutsideStrokeMainTextInfo(textInfo)
                    : (hasSplitGlowLayer ? CreateTMPMainTextInfo(textInfo) : textInfo);
                ApplyTMPTextEffects(text, in mainTextInfo);
                ConfigureSplitTMPTextEffectBinder(text, splitGlowText, splitStrokeTexts, splitShadowText, textInfo.ShadowOffset);

                // 诊断: ForceMeshUpdate 之前读取材质属性
                if (mainTextInfo.HasGlow)
                {
                    var preMat = text.fontSharedMaterial;
                    float preGO  = (preMat != null && preMat.HasProperty(Shader.PropertyToID("_GlowOuter")))   ? preMat.GetFloat(Shader.PropertyToID("_GlowOuter")) : -1f;
                    float preScB = (preMat != null && preMat.HasProperty(ShaderUtilities.ID_ScaleRatio_B))     ? preMat.GetFloat(ShaderUtilities.ID_ScaleRatio_B) : -1f;
                    bool  preKw  = preMat != null && preMat.IsKeywordEnabled("GLOW_ON");
                    Debug.Log($"[TMP:BeforeFMU] mat={preMat?.name} | _GlowOuter={preGO:F4} | _ScaleRatioB={preScB:F4} | GLOW_ON={preKw}");
                }

                text.ForceMeshUpdate();

                // 运行时诊断：确认材质和_ScaleRatioB实际值，并对比 FMU 前后 _GlowOuter 是否被重置
                if (mainTextInfo.HasGlow)
                {
                    var diagMat = text.fontSharedMaterial;
                    float scaleB = (diagMat != null && diagMat.HasProperty(ShaderUtilities.ID_ScaleRatio_B))
                        ? diagMat.GetFloat(ShaderUtilities.ID_ScaleRatio_B) : -1f;
                    float glowOuterPost = (diagMat != null && diagMat.HasProperty(Shader.PropertyToID("_GlowOuter")))
                        ? diagMat.GetFloat(Shader.PropertyToID("_GlowOuter")) : -1f;
                    bool glowKeyword = diagMat != null && diagMat.IsKeywordEnabled("GLOW_ON");
                    // 取 BeforeFMU 日志中记录的值（已在前面打印），对比
                    var bfProp = diagMat != null && diagMat.HasProperty(Shader.PropertyToID("_GlowOuter"));
                    string fmuWarn = "";
                    if (bfProp)
                    {
                        // 注：BeforeFMU _GlowOuter 已打印在上方；若 post≈0.05 说明 FMU 覆盖了我们设的值
                        if (glowOuterPost < 0.1f)
                            fmuWarn = " ⚠ FMU 可能重置了 _GlowOuter";
                    }
                    Debug.Log($"[TMP:GlowRuntime] shader={diagMat?.shader?.name} | GLOW_ON={glowKeyword} | " +
                              $"_ScaleRatioB={scaleB:F4} | _GlowOuter={glowOuterPost:F4}{fmuWarn} | " +
                              $"mat={diagMat?.name} (base={text.font?.material?.name})");
                }
            }
            else if (txtLayer != null)
            {
                Debug.LogWarning($"[PSD2UIForm] TMP文本图层 '{txtLayer.name}' ParseTextLayerInfo 失败, BindPsdLayer={(txtLayer.BindPsdLayer != null ? "OK" : "null")}, LayerType={txtLayer.LayerType}");
            }
        }
        private static float ConvertPsdLeadingToUGUILineSpacing(UnityEngine.UI.Text text, in TextLayerInfo textInfo)
        {
            if (textInfo.IsAutoLineSpacing || textInfo.LineSpacing <= 0f)
            {
                return 1f;
            }

            float baseLineHeight = Mathf.Max(1f, textInfo.FontSize);
            var font = text != null ? text.font : null;
            if (font != null && font.fontSize > 0 && font.lineHeight > 0)
            {
                baseLineHeight = font.lineHeight * (textInfo.FontSize / (float)font.fontSize);
            }

            float spacingFactor = textInfo.LineSpacing / Mathf.Max(1f, baseLineHeight);
            if (float.IsNaN(spacingFactor) || float.IsInfinity(spacingFactor))
            {
                return 1f;
            }

            return Mathf.Max(0.01f, spacingFactor);
        }
        private static bool IsLikelyMultiLineText(PsdLayerNode layerNode, in TextLayerInfo textInfo)
        {
            if (layerNode == null) return false;
            if (!string.IsNullOrEmpty(textInfo.Text) && (textInfo.Text.IndexOf('\n') >= 0 || textInfo.Text.IndexOf('\r') >= 0))
            {
                return true;
            }

            float singleLineHeight = Mathf.Max(textInfo.FontSize * 1.35f, 1f);
            return layerNode.LayerRect.height > singleLineHeight;
        }
        private static float ConvertPsdLeadingToTMPLineSpacing(TextMeshProUGUI text, in TextLayerInfo textInfo)
        {
            if (textInfo.IsAutoLineSpacing || textInfo.LineSpacing <= 0f)
            {
                return 0f;
            }

            float fontSize = Mathf.Max(1f, textInfo.FontSize);
            float baseLineHeight = fontSize;
            var font = text != null ? text.font : null;
            if (font != null)
            {
                var faceInfo = font.faceInfo;
                if (faceInfo.pointSize > 0f && faceInfo.lineHeight > 0f)
                {
                    float scale = fontSize / faceInfo.pointSize;
                    baseLineHeight = faceInfo.lineHeight * scale;
                }
            }

            float additionalSpacing = textInfo.LineSpacing - baseLineHeight;
            float spacing = additionalSpacing * 100f / fontSize;
            if (float.IsNaN(spacing) || float.IsInfinity(spacing))
            {
                return 0f;
            }

            // Avoid tiny negative jitter caused by metric mismatch when PS uses auto leading.
            if (spacing < 0f && spacing > -0.25f)
            {
                return 0f;
            }

            return spacing;
        }
        private static void RemoveLegacyTextEffects(UnityEngine.UI.Graphic textCom)
        {
            if (textCom == null) return;
            var effects = textCom.GetComponents<Shadow>();
            for (int i = 0; i < effects.Length; i++)
            {
                var fx = effects[i];
                if (fx != null)
                {
                    GameObject.DestroyImmediate(fx);
                }
            }
        }
        private static void ApplyUGUITextEffects(UnityEngine.UI.Text text, in TextLayerInfo textInfo)
        {
            if (text == null) return;
            RemoveLegacyTextEffects(text);

            if (textInfo.HasOutline)
            {
                var outline = text.gameObject.AddComponent<Outline>();
                outline.enabled = true;
                float distance = textInfo.OutlineSize;
                outline.effectColor = textInfo.OutlineColor;
                outline.effectDistance = new Vector2(distance, distance);
                outline.useGraphicAlpha = true;
            }

            if (textInfo.HasShadow)
            {
                var shadow = text.gameObject.AddComponent<Shadow>();
                shadow.enabled = true;
                shadow.effectColor = textInfo.ShadowColor;
                shadow.effectDistance = textInfo.ShadowOffset;
                shadow.useGraphicAlpha = true;
            }
        }

        private static bool ShouldSplitTMPOutsideStroke(in TextLayerInfo textInfo)
        {
            return textInfo.HasOutline
                && textInfo.TMPOutlineSize > 0f
                && textInfo.TMPOutlinePosition == TextLayerInfo.TMPOutlineMode.Outside;
        }
        private static List<TMPTextStrokeEffect> GetTMPOutsideStrokes(in TextLayerInfo textInfo)
        {
            var strokes = new List<TMPTextStrokeEffect>();
            if (textInfo.Strokes != null)
            {
                foreach (var stroke in textInfo.Strokes)
                {
                    if (stroke.Size > 0f && stroke.Position == TextLayerInfo.TMPOutlineMode.Outside)
                    {
                        strokes.Add(stroke);
                    }
                }
            }

            if (strokes.Count == 0 && ShouldSplitTMPOutsideStroke(in textInfo))
            {
                strokes.Add(new TMPTextStrokeEffect
                {
                    Color = textInfo.OutlineColor,
                    Size = textInfo.TMPOutlineSize,
                    Position = textInfo.TMPOutlinePosition
                });
            }

            return strokes.OrderByDescending(item => item.Size).ToList();
        }
        private static bool ShouldUseTMPShadowCompanion(TextMeshProUGUI text, in TextLayerInfo textInfo)
        {
            var baseMaterial = ResolveTMPBaseMaterial(text);
            float gradientScale = GetTMPMaterialGradientScale(baseMaterial);
            float effectiveSpread = textInfo.ShadowSoftness * Mathf.Clamp01(textInfo.ShadowSpread * 0.01f);
            float demand = Mathf.Max(Mathf.Abs(textInfo.ShadowOffset.x), Mathf.Abs(textInfo.ShadowOffset.y))
                + textInfo.ShadowSoftness + effectiveSpread;
            return demand > gradientScale * 0.5f;
        }
        private static void ApplyTMPVertexGradient(TextMeshProUGUI text, in TextLayerInfo textInfo)
        {
            if (text == null)
            {
                return;
            }

            if (!textInfo.HasGradient || textInfo.GradientMode == TMPTextGradientMode.None)
            {
                ClearTMPVertexGradient(text);
                text.color = textInfo.Color;
                return;
            }

            var gradient = textInfo.GradientMode == TMPTextGradientMode.Vertical
                ? new VertexGradient(textInfo.GradientTopColor, textInfo.GradientTopColor, textInfo.GradientBottomColor, textInfo.GradientBottomColor)
                : new VertexGradient(textInfo.GradientTopColor, textInfo.GradientBottomColor, textInfo.GradientTopColor, textInfo.GradientBottomColor);
            text.color = new Color(1f, 1f, 1f, textInfo.Color.a);
            text.colorGradient = gradient;
            text.enableVertexGradient = true;

            SetOptionalTMPGradientOptions(text, textInfo.GradientFirstColorPercent,
                textInfo.GradientMode == TMPTextGradientMode.Vertical ? ColorMode.VerticalGradient : ColorMode.HorizontalGradient);
        }
        private static void ClearTMPVertexGradient(TextMeshProUGUI text)
        {
            if (text == null)
            {
                return;
            }

            text.enableVertexGradient = false;
            text.colorGradient = new VertexGradient(Color.white);
            SetOptionalTMPGradientOptions(text, 50f, ColorMode.Single);
        }
        private static void SetOptionalTMPGradientOptions(TextMeshProUGUI text, float firstColorPercent, ColorMode mode)
        {
            // These options are available in the extended TMP package. Stock TMP uses the vertex colors above.
            var serialized = new SerializedObject(text);
            var directColors = serialized.FindProperty("m_colorGradientUsesDirectColors");
            if (directColors != null)
                directColors.boolValue = true;
            var midpoint = serialized.FindProperty("m_colorGradientFirstColorPercent");
            if (midpoint != null)
                midpoint.floatValue = Mathf.Clamp(firstColorPercent, 0f, 100f);
            var colorMode = serialized.FindProperty("m_colorMode");
            if (colorMode != null)
                colorMode.enumValueIndex = (int)mode;
            if (serialized.ApplyModifiedPropertiesWithoutUndo())
                text.SetVerticesDirty();
        }

        private static TextLayerInfo CreateTMPMainTextInfo(TextLayerInfo textInfo)
        {
            textInfo.HasGlow = false;
            textInfo.GlowColor = Color.clear;
            textInfo.GlowOpacity = 0f;
            textInfo.GlowSize = 0f;
            textInfo.GlowSpread = 0f;
            return textInfo;
        }
        private static TextLayerInfo CreateTMPOutsideStrokeMainTextInfo(TextLayerInfo textInfo)
        {
            textInfo.HasOutline = false;
            textInfo.OutlineColor = Color.clear;
            textInfo.OutlineSize = 0f;
            textInfo.TMPOutlineSize = 0f;
            textInfo.TMPOutlinePosition = TextLayerInfo.TMPOutlineMode.Center;
            textInfo.HasGlow = false;
            textInfo.GlowColor = Color.clear;
            textInfo.GlowOpacity = 0f;
            textInfo.GlowSize = 0f;
            textInfo.GlowSpread = 0f;
            textInfo.HasShadow = false;
            textInfo.ShadowColor = Color.clear;
            textInfo.ShadowOffset = Vector2.zero;
            textInfo.ShadowSpread = 0f;
            textInfo.ShadowSoftness = 0f;
            return textInfo;
        }
        private static TextLayerInfo CreateTMPOutsideStrokeLayerTextInfo(TextLayerInfo textInfo, TMPTextStrokeEffect stroke, bool includeShadow)
        {
            textInfo.Color = stroke.Color;
            textInfo.HasOutline = true;
            textInfo.OutlineColor = stroke.Color;
            textInfo.OutlineSize = 0f;
            textInfo.TMPOutlineSize = stroke.Size;
            textInfo.TMPOutlinePosition = stroke.Position;
            textInfo.HasGlow = false;
            textInfo.GlowColor = Color.clear;
            textInfo.GlowOpacity = 0f;
            textInfo.GlowSize = 0f;
            textInfo.GlowSpread = 0f;
            if (!includeShadow)
            {
                textInfo.HasShadow = false;
                textInfo.ShadowColor = Color.clear;
                textInfo.ShadowOffset = Vector2.zero;
                textInfo.ShadowSpread = 0f;
                textInfo.ShadowSoftness = 0f;
            }
            return textInfo;
        }
        private static TextLayerInfo CreateTMPOutsideShadowLayerTextInfo(TextLayerInfo textInfo, TMPTextStrokeEffect outerStroke)
        {
            float spreadPixels = textInfo.ShadowSoftness * Mathf.Clamp01(textInfo.ShadowSpread * 0.01f);
            textInfo.Color = textInfo.ShadowColor;
            textInfo.HasOutline = true;
            textInfo.OutlineColor = textInfo.ShadowColor;
            textInfo.OutlineSize = 0f;
            textInfo.TMPOutlineSize = Mathf.Max(outerStroke.Size, outerStroke.Size + spreadPixels);
            textInfo.TMPOutlinePosition = TextLayerInfo.TMPOutlineMode.Outside;
            textInfo.HasShadow = false;
            textInfo.HasGlow = false;
            textInfo.GlowColor = Color.clear;
            textInfo.GlowOpacity = 0f;
            textInfo.GlowSize = 0f;
            textInfo.GlowSpread = 0f;
            return textInfo;
        }
        private static TextLayerInfo CreateTMPGlowOnlyTextInfo(TextLayerInfo textInfo)
        {
            textInfo.HasOutline = false;
            textInfo.OutlineColor = Color.clear;
            textInfo.OutlineSize = 0f;
            textInfo.TMPOutlineSize = 0f;
            textInfo.TMPOutlinePosition = TextLayerInfo.TMPOutlineMode.Center;
            textInfo.HasShadow = false;
            textInfo.ShadowColor = Color.clear;
            textInfo.ShadowOffset = Vector2.zero;
            textInfo.ShadowSpread = 0f;
            textInfo.ShadowSoftness = 0f;
            return textInfo;
        }
        private static TextMeshProUGUI GetOrCreateSplitTMPGlowLayer(TextMeshProUGUI mainText)
        {
            if (mainText == null || mainText.transform.parent == null) return null;
            if (IsSplitTMPGlowLayer(mainText)) return null;

            RemoveSplitTMPGlowLayer(mainText);

            var glowGo = new GameObject(GetSplitTMPGlowLayerName(mainText), typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            glowGo.layer = mainText.gameObject.layer;
            glowGo.transform.SetParent(mainText.transform.parent, false);
            glowGo.transform.SetSiblingIndex(mainText.transform.GetSiblingIndex());

            var glowText = glowGo.GetComponent<TextMeshProUGUI>();
            glowText.raycastTarget = false;
            CopyRectTransform(mainText.GetComponent<RectTransform>(), glowGo.GetComponent<RectTransform>());
            return glowText;
        }
        private static void RemoveSplitTMPGlowLayer(TextMeshProUGUI mainText)
        {
            if (mainText == null || mainText.transform.parent == null || IsSplitTMPGlowLayer(mainText)) return;

            string glowName = GetSplitTMPGlowLayerName(mainText);
            var parent = mainText.transform.parent;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child != null && child.name == glowName)
                {
                    GameObject.DestroyImmediate(child.gameObject);
                }
            }
        }
        private static TextMeshProUGUI GetOrCreateSplitTMPStrokeLayer(TextMeshProUGUI mainText, int layerIndex, int layerCount)
        {
            if (mainText == null || mainText.transform.parent == null) return null;
            if (IsSplitTMPGlowLayer(mainText) || IsSplitTMPStrokeLayer(mainText)) return null;

            string strokeName = GetSplitTMPStrokeLayerName(mainText, layerIndex, layerCount);
            var parent = mainText.transform.parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child != null && child.name == strokeName)
                {
                    return child.GetComponent<TextMeshProUGUI>();
                }
            }

            var strokeGo = new GameObject(strokeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            strokeGo.layer = mainText.gameObject.layer;
            strokeGo.transform.SetParent(parent, false);
            MoveSiblingBefore(strokeGo.transform, mainText.transform);

            var strokeText = strokeGo.GetComponent<TextMeshProUGUI>();
            strokeText.raycastTarget = false;
            CopyRectTransform(mainText.GetComponent<RectTransform>(), strokeGo.GetComponent<RectTransform>());
            return strokeText;
        }
        private static void RemoveSplitTMPStrokeLayer(TextMeshProUGUI mainText)
        {
            if (mainText == null || mainText.transform.parent == null || IsSplitTMPStrokeLayer(mainText)) return;

            string strokeName = GetSplitTMPStrokeLayerName(mainText);
            var parent = mainText.transform.parent;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child != null && (child.name == strokeName || child.name.StartsWith(strokeName + "_", StringComparison.Ordinal)))
                {
                    GameObject.DestroyImmediate(child.gameObject);
                }
            }
        }
        private static TextMeshProUGUI GetOrCreateSplitTMPShadowLayer(TextMeshProUGUI mainText)
        {
            if (mainText == null || mainText.transform.parent == null || IsSplitTMPShadowLayer(mainText)) return null;

            RemoveSplitTMPShadowLayer(mainText);
            var shadowGo = new GameObject(GetSplitTMPShadowLayerName(mainText), typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            shadowGo.layer = mainText.gameObject.layer;
            shadowGo.transform.SetParent(mainText.transform.parent, false);
            MoveSiblingBefore(shadowGo.transform, mainText.transform);

            var shadowText = shadowGo.GetComponent<TextMeshProUGUI>();
            shadowText.raycastTarget = false;
            CopyRectTransform(mainText.GetComponent<RectTransform>(), shadowGo.GetComponent<RectTransform>());
            return shadowText;
        }
        private static void RemoveSplitTMPShadowLayer(TextMeshProUGUI mainText)
        {
            if (mainText == null || mainText.transform.parent == null || IsSplitTMPShadowLayer(mainText)) return;

            string shadowName = GetSplitTMPShadowLayerName(mainText);
            var parent = mainText.transform.parent;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child != null && child.name == shadowName)
                {
                    GameObject.DestroyImmediate(child.gameObject);
                }
            }
        }
        private static void SyncSplitTMPGlowLayerRect(TextMeshProUGUI mainText)
        {
            if (mainText == null || mainText.transform.parent == null || IsSplitTMPGlowLayer(mainText)) return;

            var parent = mainText.transform.parent;
            string glowName = GetSplitTMPGlowLayerName(mainText);
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child == null || child.name != glowName) continue;

                CopyRectTransform(mainText.GetComponent<RectTransform>(), child.GetComponent<RectTransform>());
                if (child.GetSiblingIndex() > mainText.transform.GetSiblingIndex())
                {
                    child.SetSiblingIndex(mainText.transform.GetSiblingIndex());
                }
                return;
            }
        }
        private static void ConfigureSplitTMPTextEffectBinder(TextMeshProUGUI mainText, TextMeshProUGUI glowText, List<TextMeshProUGUI> strokeTexts, TextMeshProUGUI shadowText, Vector2 shadowOffset)
        {
            if (mainText == null) return;

            var binder = mainText.GetComponent<PsdTmpTextEffectBinder>();
            if (glowText == null && (strokeTexts == null || strokeTexts.Count == 0) && shadowText == null)
            {
                if (binder != null)
                    GameObject.DestroyImmediate(binder);
                return;
            }

            if (binder == null)
                binder = mainText.gameObject.AddComponent<PsdTmpTextEffectBinder>();
            binder.Configure(mainText, glowText, strokeTexts, shadowText, shadowOffset);
        }
        private static void CopyTMPTextState(TextMeshProUGUI source, TextMeshProUGUI target)
        {
            if (source == null || target == null) return;

            target.font = source.font;
            target.fontSharedMaterial = source.fontSharedMaterial;
            target.text = source.text;
            target.fontSize = source.fontSize;
            target.fontStyle = source.fontStyle;
            target.fontWeight = source.fontWeight;
            target.color = source.color;
            target.characterSpacing = source.characterSpacing;
            target.lineSpacing = source.lineSpacing;
            target.enableAutoSizing = source.enableAutoSizing;
            target.enableWordWrapping = source.enableWordWrapping;
            target.extraPadding = source.extraPadding;
            target.overflowMode = source.overflowMode;
            target.alignment = source.alignment;
            target.verticalAlignment = source.verticalAlignment;
            target.horizontalAlignment = source.horizontalAlignment;
            target.margin = source.margin;
            target.richText = source.richText;
            target.raycastTarget = false;
        }
        private static void CopyRectTransform(RectTransform source, RectTransform target)
        {
            if (source == null || target == null) return;

            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.sizeDelta = source.sizeDelta;
            target.anchoredPosition3D = source.anchoredPosition3D;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }
        private static bool IsSplitTMPGlowLayer(TextMeshProUGUI text)
        {
            return text != null && text.gameObject.name.EndsWith(TmpSplitGlowLayerSuffix, StringComparison.Ordinal);
        }
        private static bool IsSplitTMPStrokeLayer(TextMeshProUGUI text)
        {
            return text != null && text.gameObject.name.IndexOf(TmpSplitStrokeLayerSuffix, StringComparison.Ordinal) >= 0;
        }
        private static bool IsSplitTMPShadowLayer(TextMeshProUGUI text)
        {
            return text != null && text.gameObject.name.EndsWith(TmpSplitShadowLayerSuffix, StringComparison.Ordinal);
        }
        private static string GetSplitTMPGlowLayerName(TextMeshProUGUI text)
        {
            return text != null ? text.gameObject.name + TmpSplitGlowLayerSuffix : TmpSplitGlowLayerSuffix;
        }
        private static string GetSplitTMPStrokeLayerName(TextMeshProUGUI text)
        {
            return text != null ? text.gameObject.name + TmpSplitStrokeLayerSuffix : TmpSplitStrokeLayerSuffix;
        }
        private static string GetSplitTMPStrokeLayerName(TextMeshProUGUI text, int layerIndex, int layerCount)
        {
            string baseName = GetSplitTMPStrokeLayerName(text);
            return layerCount <= 1 ? baseName : $"{baseName}_{layerIndex:00}";
        }
        private static string GetSplitTMPShadowLayerName(TextMeshProUGUI text)
        {
            return text != null ? text.gameObject.name + TmpSplitShadowLayerSuffix : TmpSplitShadowLayerSuffix;
        }
        private static void MoveSiblingBefore(Transform child, Transform target)
        {
            if (child == null || target == null || child.parent != target.parent) return;

            int targetIndex = target.GetSiblingIndex();
            if (child.GetSiblingIndex() < targetIndex)
            {
                targetIndex--;
            }
            child.SetSiblingIndex(Mathf.Max(0, targetIndex));
        }
        private static void ApplyTMPTextEffects(TextMeshProUGUI text, in TextLayerInfo textInfo, bool isSplitGlowLayer = false, bool isSplitStrokeLayer = false, bool isSplitShadowLayer = false)
        {
            if (text == null) return;
            RemoveLegacyTextEffects(text);

            var baseMaterial = ResolveTMPBaseMaterial(text);
            if (baseMaterial == null)
            {
                return;
            }

            if (!textInfo.HasOutline && !textInfo.HasShadow && !textInfo.HasGlow)
            {
                if (text.fontSharedMaterial != baseMaterial)
                {
                    text.fontSharedMaterial = baseMaterial;
                    text.UpdateMeshPadding();
                    text.SetMaterialDirty();
                }
                return;
            }

            var effectMaterial = GetOrCreateTMPEffectMaterial(text.font, baseMaterial, text.fontSize, in textInfo, isSplitGlowLayer, isSplitStrokeLayer, isSplitShadowLayer);
            if (effectMaterial != null && text.fontSharedMaterial != effectMaterial)
            {
                text.fontSharedMaterial = effectMaterial;
                text.UpdateMeshPadding();
                text.havePropertiesChanged = true;
                text.SetMaterialDirty();
            }
        }
        private static Material ResolveTMPBaseMaterial(TextMeshProUGUI text)
        {
            var current = text.fontSharedMaterial;
            var fontMaterial = text.font != null ? text.font.material : null;
            if (current != null && tmpEffectMaterialBaseLookup.TryGetValue(current.GetInstanceID(), out var baseMaterial) && baseMaterial != null)
            {
                if (fontMaterial == null || HasSameTMPAtlas(baseMaterial, fontMaterial))
                {
                    return baseMaterial;
                }
            }
            if (current != null && IsPSD2UITMPEffectMaterial(current) && fontMaterial != null && HasSameTMPAtlas(current, fontMaterial))
            {
                return fontMaterial;
            }
            if (current != null && (fontMaterial == null || HasSameTMPAtlas(current, fontMaterial)))
            {
                return current;
            }
            return fontMaterial;
        }
        private static bool IsPSD2UITMPEffectMaterial(Material material)
        {
            return material != null && material.name.IndexOf(TmpEffectMaterialNameToken, StringComparison.Ordinal) >= 0;
        }
        private static bool HasSameTMPAtlas(Material a, Material b)
        {
            if (a == null || b == null) return false;
            var texA = a.GetTexture(ShaderUtilities.ID_MainTex);
            var texB = b.GetTexture(ShaderUtilities.ID_MainTex);
            if (texA == null || texB == null) return false;
            return texA.GetInstanceID() == texB.GetInstanceID();
        }
        private static Material GetOrCreateTMPEffectMaterial(TMP_FontAsset fontAsset, Material baseMaterial, float fontSize, in TextLayerInfo textInfo, bool isSplitGlowLayer = false, bool isSplitStrokeLayer = false, bool isSplitShadowLayer = false)
        {
            if (baseMaterial == null) return null;
            fontSize = Mathf.Max(1f, fontSize);
            ShaderUtilities.GetShaderPropertyIDs();

            float gradientScale = GetTMPMaterialGradientScale(baseMaterial);
            float normalizedOutlineSize = textInfo.HasOutline ? Mathf.Clamp01(textInfo.TMPOutlineSize / gradientScale) : 0f;
            // 描边像素宽 → FaceDilate 的换算链：shader 里 dilate 乘 ScaleRatioA*0.5，而图集 SDF
            // 的 alpha 0→1 实测跨约 2*gradientScale 像素（padding=9 时 ≈20px），两者相乘后
            // 除数取 gradientScale 即可 1:1 还原像素宽（0.5 * 0.9 * 2 ≈ 0.9，偏差在 AA 内）。
            // 另外 SDF 参数在图集空间（采样字号 faceInfo.pointSize）：文本字号小于采样字号时，
            // 同样的 dilate 渲染得更细，需按 pointSize/fontSize 把 PSD 像素宽度换算到图集空间。
            float atlasPointSize = fontAsset != null && fontAsset.faceInfo.pointSize > 0 ? fontAsset.faceInfo.pointSize : fontSize;
            float splitStrokeSize = textInfo.HasOutline ? Mathf.Clamp01(textInfo.TMPOutlineSize * (atlasPointSize / fontSize) / gradientScale) : 0f;
            float outlineWidth = isSplitStrokeLayer ? Mathf.Clamp01(splitStrokeSize * TmpOutsideStrokeOutlineScale) : CalculateTMPOutlineWidth(normalizedOutlineSize, textInfo.TMPOutlinePosition);
            float baseFaceDilate = GetTMPMaterialFloat(baseMaterial, TmpFaceDilatePropertyId, 0f);
            float faceDilate = isSplitStrokeLayer ? Mathf.Clamp(baseFaceDilate + splitStrokeSize * TmpOutsideStrokeDilateScale, -1f, 1f) : CalculateTMPFaceDilate(normalizedOutlineSize, textInfo.TMPOutlinePosition);
            float normalWeight = CalculateTMPNormalWeight(fontAsset, baseMaterial, in textInfo);
            float outlineSoftness = isSplitShadowLayer ? Mathf.Clamp01(textInfo.ShadowSoftness / gradientScale) : 0f;
            // PSD 大软投影（如 距离6px/大小16px）远超 SDF 预算（padding=gradientScale）时，
            // Underlay 参数会被 clamp 且再被 ScaleRatioC 压缩，渲成贴字暗圈——比没有更糟：
            // 既盖住外描边伴生层，又把字缘压暗（视觉上像"色差"）。预算内的小投影照常转换；
            // 超预算直接跳过并告警，需要时用伴生阴影层或加大字体图集 padding 还原。
            float shadowDemandPx = Mathf.Max(Mathf.Abs(textInfo.ShadowOffset.x), Mathf.Abs(textInfo.ShadowOffset.y)) + textInfo.ShadowSoftness + textInfo.ShadowSpread;
            bool hasShadow = textInfo.HasShadow && shadowDemandPx <= gradientScale * 0.5f;
            if (textInfo.HasShadow && !hasShadow)
            {
                Debug.LogWarning($"[PSD2UI] DropShadow 超出字体图集 SDF 预算，已跳过 Underlay 转换（offset={textInfo.ShadowOffset}, blur={textInfo.ShadowSoftness:F1}px, spread={textInfo.ShadowSpread:F1}px, 预算≈{gradientScale * 0.5f:F1}px）。如需还原请用伴生阴影层或加大图集 padding。");
            }
            float shadowOffsetX = hasShadow ? Mathf.Clamp(textInfo.ShadowOffset.x / gradientScale, -1f, 1f) : 0f;
            float shadowOffsetY = hasShadow ? Mathf.Clamp(textInfo.ShadowOffset.y / gradientScale, -1f, 1f) : 0f;
            float shadowSoftness = hasShadow ? Mathf.Clamp01(textInfo.ShadowSoftness / gradientScale) : 0f;
            float shadowDilate = hasShadow ? Mathf.Clamp((textInfo.ShadowSpread + textInfo.ShadowSoftness * TmpShadowBlurToDilateScale) / gradientScale, -1f, 1f) : 0f;
            Color32 outlineColor = isSplitStrokeLayer ? textInfo.Color : textInfo.OutlineColor;
            Color32 shadowColor = textInfo.ShadowColor;

            // 外发光 TMP 映射
            // TMP Glow 的强度峰值位于 _GlowOffset 对应的 SDF 位置。旧逻辑将 _GlowOffset 固定为 0，
            // 峰值正好压在字形边缘，会把 Glow 画成一圈假描边，并在 shader 中覆盖真正的 Outline。
            // 这里把峰值推到字形/描边外侧，_GlowInner 保持 0，Glow 只承担外侧晕光。
            float glowOuter = textInfo.HasGlow ? Mathf.Clamp(textInfo.GlowSize / gradientScale * 2f, 0f, 10f) : 0f;
            float glowOffset = textInfo.HasGlow
                ? (isSplitGlowLayer ? 0f : CalculateTMPGlowOffset(glowOuter, outlineWidth, textInfo.HasOutline))
                : 0f;
            float glowInner = 0f;
            // Shader 最终 alpha = saturate(_GlowColor.a * glow * 2)，用 opacity*0.5 保证渐变完整，不会在边缘立即饱和。
            Color glowColorLinear = textInfo.HasGlow ? new Color(textInfo.GlowColor.r, textInfo.GlowColor.g, textInfo.GlowColor.b, textInfo.GlowOpacity * 0.5f) : Color.clear;

            int ow = Mathf.RoundToInt(outlineWidth * 10000f);
            int fd = Mathf.RoundToInt(faceDilate * 10000f);
            int nw = Mathf.RoundToInt(normalWeight * 10000f);
            int sx = Mathf.RoundToInt(shadowOffsetX * 10000f);
            int sy = Mathf.RoundToInt(shadowOffsetY * 10000f);
            int od = Mathf.RoundToInt(outlineSoftness * 10000f);
            int sd = Mathf.RoundToInt(shadowDilate * 10000f);
            int ss = Mathf.RoundToInt(shadowSoftness * 10000f);
            int go = Mathf.RoundToInt(glowOuter  * 10000f);
            int gOf = Mathf.RoundToInt(glowOffset * 10000f);
            Color32 gc = (Color32)glowColorLinear;
            string effectSignature =
                $"o:{(textInfo.HasOutline ? 1 : 0)}|op:{(int)textInfo.TMPOutlinePosition}|ow:{ow}|fd:{fd}|nw:{nw}|os:{od}|oc:{outlineColor.r},{outlineColor.g},{outlineColor.b},{outlineColor.a}" +
                $"|s:{(hasShadow ? 1 : 0)}|so:{sx},{sy}|sd:{sd}|ss:{ss}|sc:{shadowColor.r},{shadowColor.g},{shadowColor.b},{shadowColor.a}" +
                $"|g:{(textInfo.HasGlow ? 1 : 0)}|go:{go}|gof:{gOf}|gc:{gc.r},{gc.g},{gc.b},{gc.a}";
            string key = $"{(fontAsset != null ? fontAsset.GetInstanceID() : 0)}|{effectSignature}";

            if (textInfo.HasGlow)
                Debug.Log($"[TMP:MatCalc] GlowSize={textInfo.GlowSize:F1}px | gradientScale={gradientScale:F4} | glowOuter={glowOuter:F4} | glowOffset={glowOffset:F4} | glowColorA={glowColorLinear.a:F4}");

            if (tmpEffectMaterialCache.TryGetValue(key, out var cachedMaterial) && cachedMaterial != null)
            {
                // 始终重新 Apply：旧磁盘材质可能在发光功能实装之前保存，属性值已过时（如 _GlowOuter=0.05）
                ApplyTMPEffectMaterialProperties(cachedMaterial, outlineWidth, faceDilate, normalWeight, outlineSoftness, outlineColor, textInfo.HasOutline, shadowOffsetX, shadowOffsetY, shadowDilate, shadowSoftness, shadowColor, hasShadow, glowOffset, glowInner, glowOuter, glowColorLinear, textInfo.HasGlow);
                EditorUtility.SetDirty(cachedMaterial);
                if (textInfo.HasGlow)
                {
                    float cachedGO = cachedMaterial.HasProperty(Shader.PropertyToID("_GlowOuter")) ? cachedMaterial.GetFloat(Shader.PropertyToID("_GlowOuter")) : -1f;
                    Debug.Log($"[TMP:MatPath] CACHE HIT (re-applied) | mat={cachedMaterial.name} | _GlowOuter after apply={cachedGO:F4}");
                }
                return cachedMaterial;
            }

            string materialName = BuildTMPEffectMaterialName(fontAsset, baseMaterial, effectSignature);
            var materialAsset = LoadPersistentTMPEffectMaterial(fontAsset, materialName);
            if (materialAsset != null)
            {
                if (textInfo.HasGlow)
                {
                    float preGO = materialAsset.HasProperty(Shader.PropertyToID("_GlowOuter")) ? materialAsset.GetFloat(Shader.PropertyToID("_GlowOuter")) : -1f;
                    Debug.Log($"[TMP:MatPath] LOAD from asset | mat={materialAsset.name} | _GlowOuter BEFORE apply={preGO:F4}");
                }
                ApplyTMPEffectMaterialProperties(materialAsset, outlineWidth, faceDilate, normalWeight, outlineSoftness, outlineColor, textInfo.HasOutline, shadowOffsetX, shadowOffsetY, shadowDilate, shadowSoftness, shadowColor, hasShadow, glowOffset, glowInner, glowOuter, glowColorLinear, textInfo.HasGlow);
                if (textInfo.HasGlow)
                {
                    float postGO = materialAsset.HasProperty(Shader.PropertyToID("_GlowOuter")) ? materialAsset.GetFloat(Shader.PropertyToID("_GlowOuter")) : -1f;
                    Debug.Log($"[TMP:MatPath] LOAD from asset | _GlowOuter AFTER apply={postGO:F4}");
                }
                EditorUtility.SetDirty(materialAsset);
                tmpEffectMaterialCache[key] = materialAsset;
                tmpEffectMaterialBaseLookup[materialAsset.GetInstanceID()] = baseMaterial;
                LogTMPEffects(in textInfo, glowOuter, glowOffset, gradientScale, glowColorLinear);
                return materialAsset;
            }

            var mat = CreateTMPMaterialCopy(baseMaterial, true);
            if (mat == null)
            {
                return null;
            }
            if (textInfo.HasGlow)
            {
                int glowOuterCheckId = Shader.PropertyToID("_GlowOuter");
                Debug.Log($"[TMP:MatPath] NEW material copy | shader={mat.shader?.name} | hasGlowOuterProp={mat.HasProperty(glowOuterCheckId)}");
            }
            mat.name = materialName;
            ApplyTMPEffectMaterialProperties(mat, outlineWidth, faceDilate, normalWeight, outlineSoftness, outlineColor, textInfo.HasOutline, shadowOffsetX, shadowOffsetY, shadowDilate, shadowSoftness, shadowColor, hasShadow, glowOffset, glowInner, glowOuter, glowColorLinear, textInfo.HasGlow);
            LogTMPEffects(in textInfo, glowOuter, glowOffset, gradientScale, glowColorLinear);

            if (fontAsset != null)
            {
                PersistTMPEffectMaterial(fontAsset, mat);
            }

            tmpEffectMaterialCache[key] = mat;
            tmpEffectMaterialBaseLookup[mat.GetInstanceID()] = baseMaterial;
            return mat;
        }

        private static void LogTMPEffects(in TextLayerInfo textInfo, float glowOuter, float glowOffset, float gradientScale, Color glowColorLinear)
        {
            if (!textInfo.HasGlow && !textInfo.HasOutline && !textInfo.HasShadow) return;
            var sb = new System.Text.StringBuilder("[TMP:Effects]");
            if (textInfo.HasGlow)
                sb.Append($" Glow(psSize={textInfo.GlowSize:F1}px, psOpacity={textInfo.GlowOpacity:P0}, psColor={textInfo.GlowColor}" +
                           $" → tmpOuter={glowOuter:F3}, tmpOffset={glowOffset:F3}, tmpColorA={glowColorLinear.a:F3}, gradScale={gradientScale:F1})");
            if (textInfo.HasOutline)
                sb.Append($" Outline(size={textInfo.TMPOutlineSize:F1}px, color={textInfo.OutlineColor})");
            if (textInfo.HasShadow)
                sb.Append($" Shadow(offset={textInfo.ShadowOffset}, blur={textInfo.ShadowSoftness:F1}px, color={textInfo.ShadowColor})");
            Debug.Log(sb.ToString());
        }
        private static float GetTMPMaterialGradientScale(Material mat)
        {
            if (mat == null || !mat.HasProperty(ShaderUtilities.ID_GradientScale))
            {
                return 10f;
            }

            return Mathf.Max(1f, mat.GetFloat(ShaderUtilities.ID_GradientScale));
        }
        private static float CalculateTMPOutlineWidth(float normalizedOutlineSize, TextLayerInfo.TMPOutlineMode outlineMode)
        {
            if (normalizedOutlineSize <= 0f)
            {
                return 0f;
            }

            // Photoshop stores stroke size as total thickness. TMP's SDF outline straddles
            // the face edge, so use half the PSD size and shift the face for inside/outside.
            return Mathf.Clamp01(normalizedOutlineSize * 0.5f * TmpOutlineThicknessScale);
        }
        private static float CalculateTMPFaceDilate(float normalizedOutlineSize, TextLayerInfo.TMPOutlineMode outlineMode)
        {
            if (normalizedOutlineSize <= 0f)
            {
                return 0f;
            }

            float halfStroke = Mathf.Clamp01(normalizedOutlineSize * 0.5f);
            switch (outlineMode)
            {
                case TextLayerInfo.TMPOutlineMode.Inside:
                    return -halfStroke;
                case TextLayerInfo.TMPOutlineMode.Outside:
                    return halfStroke;
                default:
                    return 0f;
            }
        }
        private static float CalculateTMPGlowOffset(float glowOuter, float outlineWidth, bool hasOutline)
        {
            if (glowOuter <= 0f)
            {
                return 0f;
            }

            float offset = glowOuter * 0.25f;
            if (hasOutline)
            {
                offset = Mathf.Max(offset, outlineWidth + 0.05f);
            }

            return Mathf.Clamp(offset, 0f, 1f);
        }
        private static float CalculateTMPNormalWeight(TMP_FontAsset fontAsset, Material baseMaterial, in TextLayerInfo textInfo)
        {
            float baseNormalWeight = GetTMPMaterialFloat(baseMaterial, ShaderUtilities.ID_WeightNormal, 0f);
            bool isBold = (textInfo.TMPFontStyle & FontStyles.Bold) != 0;
            if (isBold || (!textInfo.HasOutline && !textInfo.HasGlow))
            {
                return baseNormalWeight;
            }

            if (!IsSongtiRelatedFontName(textInfo.FontName) && !IsSongtiTMPFontAsset(fontAsset))
            {
                return baseNormalWeight;
            }

            // Photoshop rasterizes Songti-style text heavier than TMP's neutral SDF threshold.
            // Compensate only on generated effect materials so regular UI text keeps its base font.
            return Mathf.Max(baseNormalWeight, TmpSongtiNormalWeightCompensation);
        }
        private static float GetTMPMaterialFloat(Material mat, int propertyId, float fallback)
        {
            return mat != null && mat.HasProperty(propertyId) ? mat.GetFloat(propertyId) : fallback;
        }
        private static bool IsSongtiTMPFontAsset(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null) return false;

            if (IsSongtiRelatedFontName(fontAsset.name)) return true;
            if (IsSongtiRelatedFontName(fontAsset.faceInfo.familyName)) return true;
            return fontAsset.sourceFontFile != null && IsSongtiRelatedFontName(fontAsset.sourceFontFile.name);
        }
        private static void ApplyTMPEffectMaterialProperties(Material mat, float outlineWidth, float faceDilate, float normalWeight, float outlineSoftness, Color32 outlineColor, bool hasOutline, float shadowOffsetX, float shadowOffsetY, float shadowDilate, float shadowSoftness, Color32 shadowColor, bool hasShadow, float glowOffset = 0f, float glowInner = 0f, float glowOuter = 0f, Color glowColor = default, bool hasGlow = false)
        {
            if (mat == null) return;

            if (mat.HasProperty(ShaderUtilities.ID_FaceColor))
            {
                mat.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
            }
            if (mat.HasProperty(TmpFaceDilatePropertyId))
            {
                mat.SetFloat(TmpFaceDilatePropertyId, hasOutline ? faceDilate : 0f);
            }
            if (mat.HasProperty(ShaderUtilities.ID_WeightNormal))
            {
                mat.SetFloat(ShaderUtilities.ID_WeightNormal, normalWeight);
            }
            if (mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
            {
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
            }
            if (mat.HasProperty(ShaderUtilities.ID_OutlineSoftness))
            {
                mat.SetFloat(ShaderUtilities.ID_OutlineSoftness, outlineSoftness);
            }
            if (mat.HasProperty(ShaderUtilities.ID_OutlineColor))
            {
                mat.SetColor(ShaderUtilities.ID_OutlineColor, hasOutline ? outlineColor : Color.clear);
            }
            if (hasOutline)
            {
                mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            }
            else
            {
                mat.DisableKeyword(ShaderUtilities.Keyword_Outline);
            }

            if (mat.HasProperty(ShaderUtilities.ID_UnderlayColor))
            {
                mat.SetColor(ShaderUtilities.ID_UnderlayColor, hasShadow ? shadowColor : Color.clear);
            }
            if (mat.HasProperty(ShaderUtilities.ID_UnderlayOffsetX))
            {
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, shadowOffsetX);
            }
            if (mat.HasProperty(ShaderUtilities.ID_UnderlayOffsetY))
            {
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, shadowOffsetY);
            }
            if (mat.HasProperty(ShaderUtilities.ID_UnderlaySoftness))
            {
                mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, shadowSoftness);
            }
            if (mat.HasProperty(ShaderUtilities.ID_UnderlayDilate))
            {
                mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, shadowDilate);
            }
            if (hasShadow)
            {
                mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                // 确保 _ScaleRatioC 为 1（TMP 在运行时更新此值，但预设材质可能为 0）
                if (mat.HasProperty(ShaderUtilities.ID_ScaleRatio_C))
                {
                    float scaleC = mat.GetFloat(ShaderUtilities.ID_ScaleRatio_C);
                    if (scaleC < 0.01f)
                        mat.SetFloat(ShaderUtilities.ID_ScaleRatio_C, 1f);
                }
            }
            else
            {
                mat.DisableKeyword(ShaderUtilities.Keyword_Underlay);
            }

            // 外发光 (GLOW_ON keyword + _GlowColor/_GlowOffset/_GlowInner/_GlowOuter/_GlowPower)
            // 关键：_ScaleRatioB 需 > 0，否则 sqrt(min(1,t))=sqrt(0)=0 → glow 完全不可见。
            // TMP 可能运行时覆盖此值；若材质里默认为 0，需显式设为 1。
            int scaleBId = Shader.PropertyToID("_ScaleRatioB");
            if (hasGlow && mat.HasProperty(scaleBId))
            {
                float existingScaleB = mat.GetFloat(scaleBId);
                if (existingScaleB < 0.01f)
                    mat.SetFloat(scaleBId, 1f);
            }
            int glowColorId   = Shader.PropertyToID("_GlowColor");
            int glowOffsetId  = Shader.PropertyToID("_GlowOffset");
            int glowInnerPropId = Shader.PropertyToID("_GlowInner");
            int glowOuterPropId = Shader.PropertyToID("_GlowOuter");
            int glowPowerId   = Shader.PropertyToID("_GlowPower");
            if (mat.HasProperty(glowColorId))
                mat.SetColor(glowColorId, hasGlow ? glowColor : Color.clear);
            if (mat.HasProperty(glowOffsetId))
                mat.SetFloat(glowOffsetId, hasGlow ? glowOffset : 0f);
            if (mat.HasProperty(glowInnerPropId))
                mat.SetFloat(glowInnerPropId, hasGlow ? glowInner : 0f);
            if (mat.HasProperty(glowOuterPropId))
                mat.SetFloat(glowOuterPropId, hasGlow ? glowOuter : 0f);
            if (mat.HasProperty(glowPowerId))
                mat.SetFloat(glowPowerId, 0.75f);  // 0.75 = TMP 默认值，轻微凸型曲线，视觉上比纯线性(1.0)更有辉光感
            if (hasGlow)
                mat.EnableKeyword("GLOW_ON");
            else
                mat.DisableKeyword("GLOW_ON");

            if (hasGlow)
            {
                float appliedGO   = mat.HasProperty(Shader.PropertyToID("_GlowOuter"))  ? mat.GetFloat(Shader.PropertyToID("_GlowOuter"))  : float.NaN;
                float appliedScaleB = mat.HasProperty(Shader.PropertyToID("_ScaleRatioB")) ? mat.GetFloat(Shader.PropertyToID("_ScaleRatioB")) : float.NaN;
                bool  keywordOn   = mat.IsKeywordEnabled("GLOW_ON");
                bool  hasProp     = mat.HasProperty(Shader.PropertyToID("_GlowOuter"));
                Debug.Log($"[TMP:ApplyProp] mat={mat.name} | hasGlowOuterProp={hasProp} | _GlowOuter(want={glowOuter:F4})→read={appliedGO:F4} | _ScaleRatioB={appliedScaleB:F4} | GLOW_ON={keywordOn}");
            }
        }
        private static string BuildTMPEffectMaterialName(TMP_FontAsset fontAsset, Material baseMaterial, string effectSignature)
        {
            uint hash = unchecked((uint)Animator.StringToHash(effectSignature));
            string ownerName = fontAsset != null ? fontAsset.name : baseMaterial.name;
            return $"{ownerName}{TmpEffectMaterialNameToken}{hash:X8}";
        }
        private static Material LoadPersistentTMPEffectMaterial(TMP_FontAsset fontAsset, string materialName)
        {
            if (fontAsset == null || string.IsNullOrWhiteSpace(materialName)) return null;

            string assetPath = AssetDatabase.GetAssetPath(fontAsset);
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string materialPath = GetSiblingMaterialAssetPath(assetPath, materialName, false);
            if (string.IsNullOrWhiteSpace(materialPath)) return null;

            var directMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (directMaterial != null)
            {
                return directMaterial;
            }

            string folderPath = Path.GetDirectoryName(materialPath)?.Replace("\\", "/");
            if (string.IsNullOrWhiteSpace(folderPath)) return null;

            var aliasedPaths = AssetDatabase.FindAssets("t:Material", new[] { folderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => string.Equals(
                    Path.GetDirectoryName(path)?.Replace("\\", "/"),
                    folderPath,
                    StringComparison.OrdinalIgnoreCase))
                .Where(path => HasTMPEffectMaterialAlias(AssetImporter.GetAtPath(path)?.userData, materialName))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            if (aliasedPaths.Length > 1)
            {
                Debug.LogError($"[PSD2UI] TMP 材质别名重复：{materialName} -> {string.Join(", ", aliasedPaths)}");
                return null;
            }

            return aliasedPaths.Length == 1
                ? AssetDatabase.LoadAssetAtPath<Material>(aliasedPaths[0])
                : null;
        }
        private static void PersistTMPEffectMaterial(TMP_FontAsset fontAsset, Material material)
        {
            if (fontAsset == null || material == null) return;

            string sourceMaterialName = material.name;

            string assetPath = AssetDatabase.GetAssetPath(fontAsset);
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string materialPath = GetSiblingMaterialAssetPath(assetPath, material.name, false);
            if (string.IsNullOrWhiteSpace(materialPath))
            {
                return;
            }

            var existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (existingMaterial == null)
            {
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (existingMaterial != material)
            {
                EditorUtility.CopySerialized(material, existingMaterial);
                UnityEngine.Object.DestroyImmediate(material);
                material = existingMaterial;
            }

            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.ImportAsset(materialPath, ImportAssetOptions.ForceSynchronousImport);
            AddTMPEffectMaterialAlias(materialPath, sourceMaterialName);
            AssetDatabase.SaveAssets();
        }
        private static bool HasTMPEffectMaterialAlias(string userData, string materialName)
        {
            if (string.IsNullOrWhiteSpace(userData) || string.IsNullOrWhiteSpace(materialName)) return false;

            string expected = TmpEffectMaterialAliasPrefix + materialName;
            return userData
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(line => string.Equals(line.Trim(), expected, StringComparison.Ordinal));
        }
        private static void AddTMPEffectMaterialAlias(string materialPath, string sourceMaterialName)
        {
            if (string.IsNullOrWhiteSpace(materialPath) || string.IsNullOrWhiteSpace(sourceMaterialName)) return;

            var importer = AssetImporter.GetAtPath(materialPath);
            if (importer == null || HasTMPEffectMaterialAlias(importer.userData, sourceMaterialName)) return;

            var lines = string.IsNullOrWhiteSpace(importer.userData)
                ? new List<string>()
                : importer.userData
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToList();
            lines.Add(TmpEffectMaterialAliasPrefix + sourceMaterialName);
            importer.userData = string.Join("\n", lines);
            AssetDatabase.WriteImportSettingsIfDirty(materialPath);
        }
        private static string GetSiblingMaterialAssetPath(string ownerAssetPath, string materialName, bool generateUnique)
        {
            if (string.IsNullOrWhiteSpace(ownerAssetPath) || string.IsNullOrWhiteSpace(materialName))
            {
                return null;
            }

            string folderPath = Path.GetDirectoryName(ownerAssetPath)?.Replace("\\", "/");
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return null;
            }

            string targetPath = $"{folderPath}/{materialName}.mat";
            return generateUnique ? AssetDatabase.GenerateUniqueAssetPath(targetPath) : targetPath;
        }
        private static Material CreateTMPMaterialCopy(Material baseMaterial, bool preferDistanceFieldShader)
        {
            if (baseMaterial == null) return null;

            ShaderUtilities.GetShaderPropertyIDs();
            Material material;
            var distanceFieldShader = preferDistanceFieldShader ? Shader.Find("TextMeshPro/Distance Field") : null;
            if (distanceFieldShader != null && baseMaterial.shader != distanceFieldShader)
            {
                material = new Material(distanceFieldShader);
                material.CopyPropertiesFromMaterial(baseMaterial);
            }
            else
            {
                material = new Material(baseMaterial);
            }

            var mainTexture = baseMaterial.GetTexture(ShaderUtilities.ID_MainTex);
            if (mainTexture != null && material.HasProperty(ShaderUtilities.ID_MainTex))
            {
                material.SetTexture(ShaderUtilities.ID_MainTex, mainTexture);
            }

            return material;
        }

        /// <summary>
        /// Warning:Unity导入字库字体FamilyName的特殊字符会被替换为空格,导致按原本的字体名找不到字体
        /// 将字体名特殊字符替换为空格以解决找不到字体的问题
        /// </summary>
        /// <param name="fontName"></param>
        /// <returns></returns>
        public const string CanonicalSongtiFontName = "STSongti-SC-Regular";
        public const string CanonicalSongtiBoldFontName = "STSongti-SC-Black";

        public static string GetFixedFontName(string fontName)
        {
            if (string.IsNullOrWhiteSpace(fontName)) return string.Empty;
            string fixedFontName = Regex.Replace(fontName, "[^A-Za-z0-9]+", " ");
            return fixedFontName;
        }
        public static string NormalizeFontNameForImport(string fontName)
        {
            if (string.IsNullOrWhiteSpace(fontName)) return string.Empty;
            string trimmed = fontName.Trim();
            if (!IsSongtiRelatedFontName(trimmed)) return trimmed;
            return IsBoldFontVariantName(trimmed) ? CanonicalSongtiBoldFontName : CanonicalSongtiFontName;
        }
        public static bool IsBoldFontVariantName(string fontName)
        {
            if (string.IsNullOrWhiteSpace(fontName)) return false;

            string key = Regex.Replace(fontName.Trim('[', ']'), @"[^A-Za-z0-9\u4e00-\u9fff]+", string.Empty)
                .ToLowerInvariant();

            return key.Contains("black")
                || key.Contains("bold")
                || key.Contains("heavy")
                || key.Contains("semibold")
                || key.Contains("demibold")
                || key.Contains("粗体")
                || key.Contains("粗體");
        }
        private static bool IsSongtiRelatedFontName(string fontName)
        {
            if (string.IsNullOrWhiteSpace(fontName)) return false;

            string key = Regex.Replace(fontName.Trim('[', ']'), @"[^A-Za-z0-9\u4e00-\u9fff]+", string.Empty)
                .ToLowerInvariant();

            return key.Contains("songti")
                || key.Contains("stsong")
                || key.Contains("simsun")
                || key.Contains("nsimsun")
                || key.Contains("宋体")
                || key.Contains("宋體")
                || key.Contains("华文宋")
                || key.Contains("華文宋")
                || key.Contains("经典宋")
                || key.Contains("經典宋");
        }
        /// <summary>
        /// 根据字体名查找TMP_FontAsset
        /// </summary>
        /// <param name="fontName"></param>
        /// <returns></returns>
        public static TMP_FontAsset FindTMPFontAsset(string fontName)
        {
            fontName = NormalizeFontNameForImport(fontName);
            if (string.IsNullOrWhiteSpace(fontName)) return null;
            string fixedFontName = GetFixedFontName(fontName);
            var sourceFont = FindFontAsset(fontName);
            string sourceFontGuid = null;
            if (sourceFont != null)
            {
                var sourceFontPath = AssetDatabase.GetAssetPath(sourceFont);
                if (!string.IsNullOrWhiteSpace(sourceFontPath))
                {
                    sourceFontGuid = AssetDatabase.AssetPathToGUID(sourceFontPath);
                }
            }

            TMP_FontAsset familyMatch = null;
            var fontGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            foreach (var guid in fontGuids)
            {
                var fontPath = AssetDatabase.GUIDToAssetPath(guid);
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
                if (font == null)
                {
                    continue;
                }

                if (sourceFont != null && IsTMPFontFromSource(font, sourceFont, sourceFontGuid))
                {
                    return font;
                }

                if (IsTMPFontNameMatch(font, fontName, fixedFontName))
                {
                    familyMatch ??= font;
                }
            }

            if (familyMatch != null)
            {
                return familyMatch;
            }

            return sourceFont != null ? CreateTMPFontAsset(sourceFont) : null;
        }
        private static bool IsTMPFontNameMatch(TMP_FontAsset font, string fontName, string fixedFontName)
        {
            if (font == null) return false;
            return IsFontNameMatch(font.faceInfo.familyName, fontName, fixedFontName)
                || IsFontNameMatch(font.name, fontName, fixedFontName)
                || IsFontNameMatch(font.sourceFontFile != null ? font.sourceFontFile.name : null, fontName, fixedFontName);
        }
        private static bool IsFontNameMatch(string candidate, string fontName, string fixedFontName)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return false;
            if (string.Equals(NormalizeFontNameForImport(candidate), NormalizeFontNameForImport(fontName), StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(candidate, fontName, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(candidate, fixedFontName, StringComparison.OrdinalIgnoreCase)) return true;
            return string.Equals(GetFixedFontName(candidate), fixedFontName, StringComparison.OrdinalIgnoreCase);
        }
        private static bool IsTMPFontFromSource(TMP_FontAsset font, UnityEngine.Font sourceFont, string sourceFontGuid)
        {
            if (font == null || sourceFont == null) return false;
            if (font.sourceFontFile == sourceFont) return true;

            if (!string.IsNullOrWhiteSpace(sourceFontGuid))
            {
                if (string.Equals(font.creationSettings.sourceFontFileGUID, sourceFontGuid, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (font.sourceFontFile != null)
                {
                    var existingSourcePath = AssetDatabase.GetAssetPath(font.sourceFontFile);
                    if (!string.IsNullOrWhiteSpace(existingSourcePath) &&
                        string.Equals(AssetDatabase.AssetPathToGUID(existingSourcePath), sourceFontGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        private static TMP_FontAsset CreateTMPFontAsset(UnityEngine.Font sourceFont)
        {
            if (sourceFont == null) return null;
            if (TMP_Settings.instance == null)
            {
                Debug.LogWarning("Unable to create TMP font asset because TMP Essential Resources are missing.");
                return null;
            }

            ShaderUtilities.GetShaderPropertyIDs();
            string sourceFontPath = AssetDatabase.GetAssetPath(sourceFont);
            if (string.IsNullOrWhiteSpace(sourceFontPath))
            {
                return null;
            }

            var fontImporter = AssetImporter.GetAtPath(sourceFontPath) as TrueTypeFontImporter;
            if (fontImporter != null && !fontImporter.includeFontData)
            {
                fontImporter.includeFontData = true;
                fontImporter.SaveAndReimport();
                sourceFont = AssetDatabase.LoadAssetAtPath<UnityEngine.Font>(sourceFontPath);
                if (sourceFont == null)
                {
                    return null;
                }
            }

            string sourceFontGuid = AssetDatabase.AssetPathToGUID(sourceFontPath);
            var existingFontAsset = FindExistingTMPFontBySource(sourceFont, sourceFontGuid);
            if (existingFontAsset != null)
            {
                return existingFontAsset;
            }

            string folderPath = Path.GetDirectoryName(sourceFontPath)?.Replace("\\", "/");
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return null;
            }

            string sourceFontName = NormalizeFontNameForImport(sourceFont.name);
            string assetBaseName = IsSongtiRelatedFontName(sourceFont.name) || IsSongtiRelatedFontName(Path.GetFileNameWithoutExtension(sourceFontPath))
                ? CanonicalSongtiFontName
                : Path.GetFileNameWithoutExtension(sourceFontPath);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{assetBaseName} SDF.asset");
            var fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
            if (fontAsset == null)
            {
                return null;
            }

            fontAsset.name = Path.GetFileNameWithoutExtension(assetPath);
            var atlasTexture = fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 ? fontAsset.atlasTextures[0] : null;
            if (atlasTexture != null)
            {
                atlasTexture.name = $"{assetBaseName} Atlas";
            }

            var originalMaterial = fontAsset.material;
            var fontMaterial = CreateTMPMaterialCopy(originalMaterial, true);
            if (fontMaterial != null)
            {
                fontMaterial.name = $"{assetBaseName} Atlas Material";
                if (atlasTexture != null && fontMaterial.HasProperty(ShaderUtilities.ID_MainTex))
                {
                    fontMaterial.SetTexture(ShaderUtilities.ID_MainTex, atlasTexture);
                }
                if (fontMaterial.HasProperty(ShaderUtilities.ID_TextureWidth))
                {
                    fontMaterial.SetFloat(ShaderUtilities.ID_TextureWidth, fontAsset.atlasWidth);
                }
                if (fontMaterial.HasProperty(ShaderUtilities.ID_TextureHeight))
                {
                    fontMaterial.SetFloat(ShaderUtilities.ID_TextureHeight, fontAsset.atlasHeight);
                }
                if (fontMaterial.HasProperty(ShaderUtilities.ID_GradientScale))
                {
                    fontMaterial.SetFloat(ShaderUtilities.ID_GradientScale, fontAsset.atlasPadding + 1f);
                }
                if (fontMaterial.HasProperty(ShaderUtilities.ID_WeightNormal))
                {
                    fontMaterial.SetFloat(ShaderUtilities.ID_WeightNormal, fontAsset.normalStyle);
                }
                if (fontMaterial.HasProperty(ShaderUtilities.ID_WeightBold))
                {
                    fontMaterial.SetFloat(ShaderUtilities.ID_WeightBold, fontAsset.boldStyle);
                }
                fontAsset.material = fontMaterial;
            }

            AssetDatabase.CreateAsset(fontAsset, assetPath);
            if (atlasTexture != null)
            {
                AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
            }
            if (fontAsset.material != null)
            {
                string fontMaterialPath = GetSiblingMaterialAssetPath(assetPath, fontAsset.material.name, true);
                AssetDatabase.CreateAsset(fontAsset.material, fontMaterialPath);
            }
            if (originalMaterial != null && originalMaterial != fontAsset.material)
            {
                UnityEngine.Object.DestroyImmediate(originalMaterial);
            }

            var creationSettings = fontAsset.creationSettings;
            creationSettings.sourceFontFileName = sourceFontName;
            creationSettings.sourceFontFileGUID = sourceFontGuid;
            creationSettings.pointSizeSamplingMode = 0;
            creationSettings.pointSize = Mathf.RoundToInt(fontAsset.faceInfo.pointSize);
            creationSettings.padding = fontAsset.atlasPadding;
            creationSettings.packingMode = 0;
            creationSettings.atlasWidth = fontAsset.atlasWidth;
            creationSettings.atlasHeight = fontAsset.atlasHeight;
            creationSettings.characterSetSelectionMode = 7;
            creationSettings.characterSequence = string.Empty;
            creationSettings.referencedFontAssetGUID = string.Empty;
            creationSettings.referencedTextAssetGUID = string.Empty;
            creationSettings.fontStyle = 0;
            creationSettings.fontStyleModifier = 0f;
            creationSettings.renderMode = (int)fontAsset.atlasRenderMode;
            creationSettings.includeFontFeatures = false;
            fontAsset.creationSettings = creationSettings;

            EditorUtility.SetDirty(fontAsset);
            if (atlasTexture != null)
            {
                EditorUtility.SetDirty(atlasTexture);
            }
            if (fontAsset.material != null)
            {
                EditorUtility.SetDirty(fontAsset.material);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) ?? fontAsset;
        }
        private static TMP_FontAsset FindExistingTMPFontBySource(UnityEngine.Font sourceFont, string sourceFontGuid)
        {
            if (sourceFont == null) return null;

            var fontGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            foreach (var guid in fontGuids)
            {
                var fontPath = AssetDatabase.GUIDToAssetPath(guid);
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
                if (IsTMPFontFromSource(font, sourceFont, sourceFontGuid))
                {
                    return font;
                }
            }

            return null;
        }
        private static void EnsureTMPFontHasCharacters(TMP_FontAsset fontAsset, string text)
        {
            if (fontAsset == null || string.IsNullOrEmpty(text)) return;
            if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic) return;
            if (fontAsset.HasCharacters(text)) return;

            fontAsset.TryAddCharacters(text, out _);
            EditorUtility.SetDirty(fontAsset);
            if (fontAsset.material != null)
            {
                EditorUtility.SetDirty(fontAsset.material);
            }
            if (fontAsset.atlasTextures != null)
            {
                for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
                {
                    if (fontAsset.atlasTextures[i] != null)
                    {
                        EditorUtility.SetDirty(fontAsset.atlasTextures[i]);
                    }
                }
            }
            AssetDatabase.SaveAssets();
        }
        /// <summary>
        /// 根据字体名查找Font Asset
        /// </summary>
        /// <param name="fontName"></param>
        /// <returns></returns>
        public static UnityEngine.Font FindFontAsset(string fontName)
        {
            fontName = NormalizeFontNameForImport(fontName);
            if (string.IsNullOrWhiteSpace(fontName)) return null;
            var fontNameLow = fontName.ToLower();
            string fixedFontName = GetFixedFontName(fontNameLow);
            UnityEngine.Font fileNameMatch = null;
            var fontGuids = AssetDatabase.FindAssets("t:font");
            foreach (var guid in fontGuids)
            {
                var fontPath = AssetDatabase.GUIDToAssetPath(guid);
                var font = AssetImporter.GetAtPath(fontPath) as TrueTypeFontImporter;
                var ttfName = font != null ? font.fontTTFName : "";
                var fileName = Path.GetFileNameWithoutExtension(fontPath);

                // 优先匹配 TTF 内部字体名
                if (!string.IsNullOrEmpty(ttfName) && IsFontNameMatch(ttfName, fontName, fixedFontName))
                {
                    return AssetDatabase.LoadAssetAtPath<UnityEngine.Font>(fontPath);
                }

                // 回退匹配文件名（如 TencentSansW7.ttf → TencentSansW7）
                if (fileNameMatch == null && IsFontNameMatch(fileName, fontName, fixedFontName))
                {
                    fileNameMatch = AssetDatabase.LoadAssetAtPath<UnityEngine.Font>(fontPath);
                }
            }
            return fileNameMatch;
        }

        internal static UnityEngine.Color LayerNode2Color(PsdLayerNode fillColor, Color defaultColor)
        {
            if (fillColor != null && fillColor.TryGetLayerColor(out var layerColor))
            {
                return layerColor;
            }
            return defaultColor;
        }
        /// <summary>
        /// 导出UI设计师使用规则文档
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        internal void ExportReadmeDoc()
        {
            var exportDir = EditorUtility.SaveFolderPanel("选择文档导出路径", Application.dataPath, null);
            if (string.IsNullOrWhiteSpace(exportDir) || !Directory.Exists(exportDir))
            {
                return;
            }

            var docFile = Path.Combine(exportDir, "Psd2UGUI设计师使用文档.doc");
            var strBuilder = new StringBuilder();
            strBuilder.AppendLine("使用说明:");
            strBuilder.AppendLine(this.readmeDoc);
            strBuilder.AppendLine(Environment.NewLine + Environment.NewLine);
            strBuilder.AppendLine("UI类型标识: 图层/组命名以'.类型'结尾");
            strBuilder.AppendLine("UI类型标识列表:");

            foreach (var rule in rules)
            {
                if (rule.UIType == GUIType.Null) continue;

                strBuilder.AppendLine($"{rule.UIType}: {rule.Comment}");
                strBuilder.Append("类型标识: ");
                foreach (var tag in rule.TypeMatches)
                {
                    strBuilder.Append($".{tag}, ");
                }
                strBuilder.AppendLine();
                strBuilder.AppendLine();
            }

            try
            {
                File.WriteAllText(docFile, strBuilder.ToString(), Encoding.UTF8);
                EditorUtility.RevealInFinder(docFile);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

        }

        public void ExportLayerTagMenuConfig()
        {
            if (rules == null || rules.Length == 0)
            {
                return;
            }

            var layerTagMenuScriptAssetPath = Psd2UIFormPluginPathUtility.GetPluginAssetPath(LayerTagMenuScriptRelativePath);
            var layerTagMenuScriptAbsolutePath = Psd2UIFormPluginPathUtility.AssetPathToAbsolutePath(layerTagMenuScriptAssetPath);
            if (string.IsNullOrWhiteSpace(layerTagMenuScriptAssetPath) || !File.Exists(layerTagMenuScriptAbsolutePath))
            {
                EditorUtility.DisplayDialog("导出Rules失败", $"PS脚本文件不存在:\n{layerTagMenuScriptAssetPath}", "OK");
                return;
            }

            var configLines = new List<string>();
            foreach (var rule in rules)
            {
                if (rule == null || rule.UIType == GUIType.Null) continue;
                if (rule.TypeMatches == null || rule.TypeMatches.Length == 0) continue;
                var match = rule.TypeMatches[0];
                if (string.IsNullOrWhiteSpace(match)) continue;

                match = match.Trim();
                if (!match.StartsWith("."))
                {
                    match = "." + match;
                }
                configLines.Add($"    \"{rule.UIType}\\n{rule.UITypeDesc}\": \"{match}\"");
            }

            var builder = new StringBuilder();
            builder.AppendLine("var CONFIG = {");
            for (int i = 0; i < configLines.Count; i++)
            {
                builder.Append(configLines[i]);
                if (i < configLines.Count - 1) builder.Append(',');
                builder.AppendLine();
            }
            builder.Append("};");

            var scriptContent = File.ReadAllText(layerTagMenuScriptAbsolutePath);
            var configPattern = new Regex(@"var\s+CONFIG\s*=\s*\{[\s\S]*?\};", RegexOptions.Multiline);
            if (!configPattern.IsMatch(scriptContent))
            {
                EditorUtility.DisplayDialog("导出Rules失败", $"PS脚本中未找到CONFIG字段:\n{layerTagMenuScriptAssetPath}", "OK");
                return;
            }

            var newContent = configPattern.Replace(scriptContent, builder.ToString(), 1);
            if (layerTagMenuScriptAssetPath.StartsWith("Packages/", StringComparison.Ordinal))
            {
                string outputPath = EditorUtility.SaveFilePanel(
                    "导出 Photoshop 图层标签脚本",
                    Psd2UIFormPluginPathUtility.GetProjectRoot(),
                    Path.GetFileNameWithoutExtension(layerTagMenuScriptAssetPath),
                    "jsx");
                if (!string.IsNullOrEmpty(outputPath))
                {
                    File.WriteAllText(outputPath, newContent, Encoding.UTF8);
                    EditorUtility.RevealInFinder(outputPath);
                }
                return;
            }

            File.WriteAllText(layerTagMenuScriptAbsolutePath, newContent, Encoding.UTF8);
            AssetDatabase.ImportAsset(layerTagMenuScriptAssetPath);
            EditorUtility.DisplayDialog("导出Rules成功", layerTagMenuScriptAssetPath, "OK");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(layerTagMenuScriptAssetPath);
        }
    }
    internal struct TextLayerInfo
    {
        internal enum TMPOutlineMode
        {
            Center = 0,
            Inside = 1,
            Outside = 2
        }
        public string Text;
        public int FontSize;
        public bool IsAutoLineSpacing;
        public float LineSpacing;
        public float CharacterSpacing;
        public Color Color;
        public FontStyle FontStyle;
        public TMPro.FontStyles TMPFontStyle;
        public string FontName;
        public bool HasOutline;
        public Color OutlineColor;
        public float OutlineSize;
        public float TMPOutlineSize;
        public TMPOutlineMode TMPOutlinePosition;
        public List<TMPTextStrokeEffect> Strokes;
        public bool HasShadow;
        public Color ShadowColor;
        public Vector2 ShadowOffset;
        public float ShadowSpread;
        public float ShadowSoftness;
        public bool HasGradient;
        public TMPTextGradientMode GradientMode;
        public Color GradientTopColor;
        public Color GradientBottomColor;
        public float GradientFirstColorPercent;
        public bool HasGlow;
        public Color GlowColor;
        public float GlowOpacity;
        public float GlowSize;
        public float GlowSpread;
    }
    internal enum TMPTextGradientMode
    {
        None = 0,
        Horizontal = 1,
        Vertical = 2
    }
    internal struct TMPTextStrokeEffect
    {
        public Color Color;
        public float Size;
        public TextLayerInfo.TMPOutlineMode Position;
    }
    internal static class LayerNameUtility
    {
        private const int MaxNameLength = 64;
        private static readonly Regex MultiUnderscoreRegex = new Regex("_+", RegexOptions.Compiled);
        private static readonly HashSet<string> ReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON","PRN","AUX","NUL",
            "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
            "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"
        };
        private static readonly char[] InvalidFileChars = Path.GetInvalidFileNameChars();
        private static readonly Encoding Gb2312Encoding;
        private static readonly int[] PinyinCodes = new int[]
        {
            -20319,-20317,-20304,-20295,-20292,-20283,-20265,-20257,-20242,-20230,
            -20051,-20036,-20032,-20026,-20002,-19990,-19986,-19982,-19976,-19805,
            -19784,-19775,-19774,-19763,-19756,-19751,-19746,-19741,-19739,-19728,
            -19725,-19715,-19540,-19531,-19525,-19515,-19500,-19484,-19479,-19467,
            -19289,-19288,-19281,-19275,-19270,-19263,-19261,-19249,-19243,-19242,
            -19238,-19235,-19227,-19224,-19218,-19212,-19038,-19023,-19018,-19006,
            -19003,-18996,-18977,-18961,-18952,-18783,-18774,-18773,-18763,-18756,
            -18741,-18735,-18731,-18722,-18710,-18697,-18696,-18526,-18518,-18501,
            -18490,-18478,-18463,-18448,-18447,-18446,-18239,-18237,-18231,-18220,
            -18211,-18201,-18184,-18183,-18181,-18012,-17997,-17988,-17970,-17964,
            -17961,-17950,-17947,-17931,-17928,-17922,-17759,-17752,-17733,-17730,
            -17721,-17703,-17701,-17697,-17692,-17683,-17676,-17496,-17487,-17482,
            -17468,-17454,-17433,-17427,-17417,-17202,-17185,-16983,-16970,-16942,
            -16915,-16733,-16708,-16706,-16689,-16664,-16657,-16647,-16474,-16470,
            -16465,-16459,-16452,-16448,-16433,-16429,-16427,-16423,-16419,-16412,
            -16407,-16403,-16401,-16393,-16220,-16216,-16212,-16205,-16202,-16187,
            -16180,-16171,-16169,-16158,-16155,-15959,-15958,-15944,-15933,-15920,
            -15915,-15903,-15889,-15878,-15707,-15701,-15681,-15667,-15661,-15659,
            -15652,-15640,-15631,-15625,-15454,-15448,-15436,-15435,-15419,-15416,
            -15408,-15394,-15385,-15377,-15375,-15369,-15363,-15362,-15183,-15180,
            -15165,-15158,-15153,-15150,-15149,-15144,-15143,-15141,-15140,-15139,
            -15128,-15121,-15119,-15117,-15110,-15109,-14941,-14937,-14933,-14930,
            -14929,-14928,-14926,-14922,-14921,-14914,-14908,-14902,-14894,-14889,
            -14882,-14873,-14871,-14857,-14678,-14674,-14670,-14668,-14663,-14654,
            -14645,-14630,-14594,-14429,-14407,-14399,-14384,-14379,-14368,-14355,
            -14353,-14345,-14170,-14159,-14151,-14149,-14145,-14140,-14137,-14135,
            -14125,-14123,-14122,-14112,-14109,-14099,-14097,-14094,-14092,-14090,
            -14087,-14083,-13917,-13914,-13910,-13907,-13906,-13905,-13896,-13894,
            -13878,-13870,-13859,-13847,-13831,-13658,-13611,-13601,-13406,-13404,
            -13400,-13398,-13395,-13391,-13387,-13383,-13367,-13359,-13356,-13343,
            -13340,-13329,-13326,-13318,-13147,-13138,-13120,-13107,-13096,-13095,
            -13091,-13076,-13068,-13063,-13060,-12888,-12875,-12871,-12860,-12858,
            -12852,-12849,-12838,-12831,-12829,-12812,-12802,-12607,-12597,-12594,
            -12585,-12556,-12359,-12346,-12320,-12300,-12120,-12099,-12089,-12074,
            -12067,-12058,-12039,-11867,-11861,-11847,-11831,-11798,-11781,-11604,
            -11589,-11536,-11358,-11340,-11339,-11324,-11303,-11097,-11077,-11067,
            -11055,-11052,-11045,-11041,-11038,-11024,-11020,-11019,-11018,-11014,
            -10838,-10832,-10815,-10800,-10790,-10780,-10764,-10587,-10544,-10533,
            -10519,-10331,-10329,-10328,-10322,-10315,-10309,-10307,-10296,-10281,
            -10274,-10270,-10262,-10260,-10256,-10254
        };
        private static readonly string[] PinyinValues = new string[]
        {
            "a","ai","an","ang","ao","ba","bai","ban","bang","bao","bei","ben","beng","bi","bian","biao",
            "bie","bin","bing","bo","bu","ca","cai","can","cang","cao","ce","ceng","cha","chai","chan",
            "chang","chao","che","chen","cheng","chi","chong","chou","chu","chuai","chuan","chuang",
            "chui","chun","chuo","ci","cong","cou","cu","cuan","cui","cun","cuo","da","dai","dan",
            "dang","dao","de","deng","di","dian","diao","die","ding","diu","dong","dou","du","duan",
            "dui","dun","duo","e","en","er","fa","fan","fang","fei","fen","feng","fo","fou","fu","ga",
            "gai","gan","gang","gao","ge","gei","gen","geng","gong","gou","gu","gua","guai","guan",
            "guang","gui","gun","guo","ha","hai","han","hang","hao","he","hei","hen","heng","hong",
            "hou","hu","hua","huai","huan","huang","hui","hun","huo","ji","jia","jian","jiang","jiao",
            "jie","jin","jing","jiong","jiu","ju","juan","jue","jun","ka","kai","kan","kang","kao",
            "ke","ken","keng","kong","kou","ku","kua","kuai","kuan","kuang","kui","kun","kuo","la",
            "lai","lan","lang","lao","le","lei","leng","li","lia","lian","liang","liao","lie","lin",
            "ling","liu","long","lou","lu","lv","luan","lue","lun","luo","ma","mai","man","mang","mao",
            "me","mei","men","meng","mi","mian","miao","mie","min","ming","miu","mo","mou","mu","na",
            "nai","nan","nang","nao","ne","nei","nen","neng","ni","nian","niang","niao","nie","nin",
            "ning","niu","nong","nu","nv","nuan","nue","nuo","o","ou","pa","pai","pan","pang","pao",
            "pei","pen","peng","pi","pian","piao","pie","pin","ping","po","pu","qi","qia","qian",
            "qiang","qiao","qie","qin","qing","qiong","qiu","qu","quan","que","qun","ran","rang",
            "rao","re","ren","reng","ri","rong","rou","ru","ruan","rui","run","ruo","sa","sai","san",
            "sang","sao","se","sen","seng","sha","shai","shan","shang","shao","she","shen","sheng",
            "shi","shou","shu","shua","shuai","shuan","shuang","shui","shun","shuo","si","song",
            "sou","su","suan","sui","sun","suo","ta","tai","tan","tang","tao","te","teng","ti","tian",
            "tiao","tie","ting","tong","tou","tu","tuan","tui","tun","tuo","wa","wai","wan","wang",
            "wei","wen","weng","wo","wu","xi","xia","xian","xiang","xiao","xie","xin","xing","xiong",
            "xiu","xu","xuan","xue","xun","ya","yan","yang","yao","ye","yi","yin","ying","yo","yong",
            "you","yu","yuan","yue","yun","za","zai","zan","zang","zao","ze","zei","zen","zeng","zha",
            "zhai","zhan","zhang","zhao","zhe","zhen","zheng","zhi","zhong","zhou","zhu","zhua",
            "zhuai","zhuan","zhuang","zhui","zhun","zhuo","zi","zong","zou","zu","zuan","zui","zun","zuo"
        };

        static LayerNameUtility()
        {
            try
            {
                Gb2312Encoding = Encoding.GetEncoding("GB2312");
            }
            catch
            {
                try
                {
                    Gb2312Encoding = Encoding.GetEncoding(936);
                }
                catch
                {
                    Gb2312Encoding = Encoding.UTF8;
                }
            }
        }

        public static string ConvertChineseToLetters(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var builder = new StringBuilder(input.Length * 2);
            foreach (var ch in input)
            {
                if (IsChinese(ch))
                {
                    builder.Append(ConvertChineseChar(ch));
                }
                else
                {
                    builder.Append(ch);
                }
            }
            return builder.ToString();
        }

        public static string Sanitize(string input)
        {
            return SanitizeInternal(input, stripRefTags: true);
        }

        public static string SanitizePreserveRefTags(string input)
        {
            return SanitizeInternal(input, stripRefTags: false);
        }

        private static string SanitizeInternal(string input, bool stripRefTags)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var working = input.Trim();
            if (stripRefTags)
            {
                if (!string.IsNullOrEmpty(UGUIParser.REF_PREFAB_TAG) && working.StartsWith(UGUIParser.REF_PREFAB_TAG, StringComparison.OrdinalIgnoreCase))
                {
                    working = working.Substring(UGUIParser.REF_PREFAB_TAG.Length).TrimStart();
                }
                else if (!string.IsNullOrEmpty(UGUIParser.REF_TAG) && working.StartsWith(UGUIParser.REF_TAG, StringComparison.OrdinalIgnoreCase))
                {
                    working = working.Substring(UGUIParser.REF_TAG.Length).TrimStart();
                }
            }

            var builder = new StringBuilder(working.Length);
            foreach (var ch in working)
            {
                if (IsAllowedChar(ch))
                {
                    builder.Append(ch);
                }
                else if (char.IsWhiteSpace(ch) || char.IsControl(ch) || Array.IndexOf(InvalidFileChars, ch) >= 0)
                {
                    builder.Append('_');
                }
                else
                {
                    builder.Append('_');
                }
            }
            var sanitized = MultiUnderscoreRegex.Replace(builder.ToString(), "_");
            sanitized = sanitized.Trim('_', '.');
            if (sanitized.Length > MaxNameLength)
            {
                sanitized = sanitized.Substring(0, MaxNameLength);
            }
            if (ReservedNames.Contains(sanitized))
            {
                sanitized = "_" + sanitized;
            }
            return sanitized;
        }

        private static string ConvertChineseChar(char chineseChar)
        {
            if (Gb2312Encoding == null)
            {
                return ((int)chineseChar).ToString("x4");
            }
            var bytes = Gb2312Encoding.GetBytes(new[] { chineseChar });
            if (bytes.Length != 2)
            {
                return chineseChar.ToString();
            }
            var code = bytes[0] << 8 | bytes[1];
            code -= 65536;
            if (code > 0 && code < 160)
            {
                return chineseChar.ToString();
            }
            for (int i = PinyinCodes.Length - 1; i >= 0; i--)
            {
                if (code >= PinyinCodes[i])
                {
                    return PinyinValues[i];
                }
            }
            return "u" + ((int)chineseChar).ToString("x4");
        }

        private static bool IsChinese(char ch)
        {
            return ch >= 0x4e00 && ch <= 0x9fff;
        }

        private static bool IsAllowedChar(char ch)
        {
            return char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '.';
        }
    }

}
#endif
