/*
插件获取:
https://efunstudio.cn
https://shop106471535.taobao.com
*/

#if UNITY_EDITOR
using System;
using UGF.EditorTools.Psd2UGUI.Smartexport;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using cn.efunstudio.psdreader.PsdParser;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    [CustomEditor(typeof(Psd2UIFormConverter))]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    public class Psd2UIFormConverterInspector : UnityEditor.Editor
    {
        Psd2UIFormConverter targetLogic;

        GUIContent parsePsd2NodesBt;
        GUIContent exportUISpritesBt;
        GUIContent generateUIFormBt;
        GUIContent generateMultiScreenUIFormsBt;
        GUIContent aiAutoFixBt;
        GUIContent applyAiResultBt;
        GUILayoutOption btHeight;
        bool metadataDebugFoldout;
        int metadataDebugSelection;
        bool metadataDebugShowJson;
        Vector2 metadataDebugScroll;
        private void OnEnable()
        {
            btHeight = GUILayout.Height(30);
            targetLogic = target as Psd2UIFormConverter;
            parsePsd2NodesBt = new GUIContent("解析psd图层", "把psd图层解析为可编辑节点树");
            exportUISpritesBt = new GUIContent("导出Images", "导出勾选的psd图层为碎图");
            generateUIFormBt = new GUIContent("生成UIForm", "根据解析后的节点树生成UIForm Prefab");
            generateMultiScreenUIFormsBt = new GUIContent("识别合集并分界面导出", "识别单个 PSD 中并列的多个界面分组，预览确认后分别生成 UIForm Prefab");
            aiAutoFixBt = new GUIContent("AI自动识别并改树", "导出当前节点树与预览图，调用所选 AI Provider；结果生成后必须人工确认才会应用");
            applyAiResultBt = new GUIContent("应用最近AI结果", "重新校验并应用当前 PSD 最近一次 AI 结果");
            if (string.IsNullOrWhiteSpace(Psd2UIFormSettings.Instance.UIFormOutputDir))
            {
                Debug.LogWarning($"UIForm输出路径为空!");
            }
        }
        private void OnDisable()
        {
            Psd2UIFormSettings.Save();
        }

        public override void OnInspectorGUI()
        {
            if (!targetLogic.Initiated)
            {
                EditorGUILayout.HelpBox("请打开Prefab,进入Psd2UIForm编辑界面后才能操作!", MessageType.Error);
                if (GUILayout.Button("打开编辑界面"))
                {
                    var assetPath = AssetDatabase.GetAssetPath(target);
                    Psd2UIFormConverter.OpenPsdLayerEditor(assetPath);
                }
                return;
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
                if (GUILayout.Button("查看使用文档"))
                {
                    Application.OpenURL("https://efunstudio.cn");
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("UI图片导出路径:", GUILayout.Width(150));
                    Psd2UIFormSettings.Instance.UIImagesOutputDir = EditorGUILayout.TextField(Psd2UIFormSettings.Instance.UIImagesOutputDir);
                    if (GUILayout.Button("选择路径", GUILayout.Width(80)))
                    {
                        var retPath = EditorUtility.OpenFolderPanel("选择导出路径", Psd2UIFormSettings.Instance.UIImagesOutputDir, null);
                        if (!string.IsNullOrWhiteSpace(retPath))
                        {
                            if (!retPath.StartsWith("Assets/"))
                            {
                                retPath = PathExtensions.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, retPath);
                            }
                            Psd2UIFormSettings.Instance.UIImagesOutputDir = retPath;
                            Psd2UIFormSettings.Save();
                        }
                        GUIUtility.ExitGUI();
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.BeginHorizontal();
                {
                    Psd2UIFormSettings.Instance.UseUIFormOutputDir = EditorGUILayout.ToggleLeft("使用UIForm导出路径:", Psd2UIFormSettings.Instance.UseUIFormOutputDir, GUILayout.Width(150));
                    EditorGUI.BeginDisabledGroup(!Psd2UIFormSettings.Instance.UseUIFormOutputDir);
                    {
                        Psd2UIFormSettings.Instance.UIFormOutputDir = EditorGUILayout.TextField(Psd2UIFormSettings.Instance.UIFormOutputDir);
                        if (GUILayout.Button("选择路径", GUILayout.Width(80)))
                        {
                            var retPath = EditorUtility.OpenFolderPanel("选择导出路径", Psd2UIFormSettings.Instance.UIFormOutputDir, null);
                            if (!string.IsNullOrWhiteSpace(retPath))
                            {
                                if (!retPath.StartsWith("Assets/"))
                                {
                                    retPath = PathExtensions.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, retPath);
                                }
                                Psd2UIFormSettings.Instance.UIFormOutputDir = retPath;
                                Psd2UIFormSettings.Save();
                            }
                            GUIUtility.ExitGUI();
                        }
                        EditorGUI.EndDisabledGroup();
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }


            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button(parsePsd2NodesBt, btHeight))
                {
                    Psd2UIFormConverter.ParsePsd2LayerPrefab(targetLogic.PsdAssetName, targetLogic);
                }
                if (GUILayout.Button(exportUISpritesBt, btHeight))
                {
                    targetLogic.ExportSprites();
                }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button(generateUIFormBt, btHeight))
            {
                targetLogic.GenerateUIForm();
            }
            if (GUILayout.Button(generateMultiScreenUIFormsBt, btHeight))
            {
                PsdMultiScreenExportWindow.Open(targetLogic);
            }
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button(aiAutoFixBt, btHeight))
                {
                    if (!AiAutoFixWorkflow.Start(targetLogic, out var error))
                    {
                        EditorUtility.DisplayDialog("AI自动改树启动失败", error, "确定");
                    }
                }
                if (GUILayout.Button(applyAiResultBt, btHeight))
                {
                    if (!AiAutoFixWorkflow.ApplyCurrentResult(targetLogic, out var error))
                    {
                        EditorUtility.DisplayDialog("应用AI结果失败", error, "确定");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            DrawMetadataDebugPanel();
            base.OnInspectorGUI();
        }

        private void DrawMetadataDebugPanel()
        {
            metadataDebugFoldout = EditorGUILayout.Foldout(metadataDebugFoldout, "生成元数据调试", true);
            if (!metadataDebugFoldout) return;

            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("数据宿主:", targetLogic.GetGeneratedMetadataHostAssetPathForDebug() ?? "<None>");

                var metadataTargets = targetLogic.GetGeneratedMetadataTargetsForDebug();
                if (metadataTargets == null || metadataTargets.Length < 1)
                {
                    metadataDebugShowJson = false;
                    EditorGUILayout.HelpBox("当前未找到生成元数据。先执行一次生成UIForm或增量导出后再查看。", MessageType.Info);
                }
                else
                {
                    metadataDebugSelection = Mathf.Clamp(metadataDebugSelection, 0, metadataTargets.Length - 1);
                    metadataDebugSelection = EditorGUILayout.Popup("目标Prefab", metadataDebugSelection, metadataTargets);

                    var selectedTarget = metadataTargets[metadataDebugSelection];
                    EditorGUILayout.LabelField("路径:", selectedTarget);

                    var json = targetLogic.GetGeneratedMetadataJsonForDebug(selectedTarget);
                    EditorGUILayout.BeginHorizontal();
                    {
                        if (GUILayout.Button(metadataDebugShowJson ? "隐藏内容" : "查看元数据", GUILayout.Width(100)))
                        {
                            metadataDebugShowJson = !metadataDebugShowJson;
                        }
                        if (GUILayout.Button("复制JSON", GUILayout.Width(100)))
                        {
                            EditorGUIUtility.systemCopyBuffer = json;
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    if (metadataDebugShowJson)
                    {
                        metadataDebugScroll = EditorGUILayout.BeginScrollView(metadataDebugScroll, GUILayout.MinHeight(140), GUILayout.MaxHeight(320));
                        EditorGUILayout.TextArea(json ?? string.Empty, GUILayout.ExpandHeight(true));
                        EditorGUILayout.EndScrollView();
                    }
                }
            }
            EditorGUILayout.EndVertical();
        }

        public override bool HasPreviewGUI()
        {
            return targetLogic.BindPsdAsset != null;
        }
        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            GUI.DrawTexture(r, targetLogic.BindPsdAsset.texture, ScaleMode.ScaleToFit);
            //base.OnPreviewGUI(r, background);
        }
    }
    /// <summary>
    /// Psd文件转成UIForm prefab
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(SpriteRenderer))]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    public class Psd2UIFormConverter : MonoBehaviour
    {
        const string RecordLayerOperation = "Change Export Image";
        const string GeneratedContainerTypeKey = "__Container";
        const string GeneratedPrefabReferenceTypeKey = "__PrefabRef";
        public static Psd2UIFormConverter Instance { get; private set; }
        [ReadOnlyField][SerializeField] public string psdAssetChangeTime;//文件修改时间标识
        [Tooltip("UIForm名字")][SerializeField] private string uiFormName;
        [Tooltip("关联的psd文件")][SerializeField] private UnityEngine.Sprite psdAsset;
        [Header("Debug:")][SerializeField] bool drawLayerRectGizmos = true;
        [SerializeField] UnityEngine.Color drawLayerRectGizmosColor = UnityEngine.Color.gray;
        [HideInInspector][SerializeField] private List<GeneratedMetadataSerializedEntry> generatedMetadataEntries = new List<GeneratedMetadataSerializedEntry>();
        private PsdDocument psdInstance;//psd文件解析实例
        private GUIStyle uiTypeLabelStyle;
        private readonly Dictionary<string, PsdLayerNode> layerLookupByName = new Dictionary<string, PsdLayerNode>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> sharedSpriteAssets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SharedSpriteIndexEntry> sharedSpriteIndexByHash = new Dictionary<string, SharedSpriteIndexEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GameObject> sharedPrefabAssets = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> referencedLayerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> sharedPrefabExportInProgress = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool sharedSpriteIndexLoaded;
        private string sharedSpriteIndexPath;

        [Serializable]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
        private sealed class GeneratedMetadataCollection
        {
            public List<GeneratedMetadataEntry> Entries = new List<GeneratedMetadataEntry>();
        }

        [Serializable]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
        private sealed class SharedSpriteIndexCollection
        {
            public int Version = 2;
            public List<SharedSpriteIndexEntry> Entries = new List<SharedSpriteIndexEntry>();
        }

        [Serializable]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
        private sealed class SharedSpriteIndexEntry
        {
            public string Hash;
            public string AssetPath;
            public string PreferredName;
            public string FirstSeenUI;
            public string UpdatedAtUtc;
            public bool IsSprite;
        }

        [Serializable]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
        private sealed class GeneratedMetadataEntry
        {
            public string GlobalObjectId;
            public string Key;
            public string TypeKey;
            public bool IsContainer;
        }

        [Serializable]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
        private sealed class GeneratedMetadataSerializedEntry
        {
            public string PrefabAssetPath;
            [HideInInspector] public string Json;
            public List<GeneratedMetadataEntry> Entries = new List<GeneratedMetadataEntry>();
        }

        private sealed class GeneratedNodeSnapshot
        {
            public GameObject InstanceObject;
            public string Key;
            public string TypeKey;
            public bool IsContainer;
        }
        public bool Initiated => psdInstance != null;
        internal PsdDocument PsdInstance => psdInstance;
        public string PsdAssetName => psdAsset != null ? AssetDatabase.GetAssetPath(psdAsset) : null;
        public UnityEngine.Sprite BindPsdAsset => psdAsset;
        public Vector2Int UIFormCanvasSize => psdInstance != null ? new Vector2Int(psdInstance.Width, psdInstance.Height) : Vector2Int.zero;
        internal static byte[] BuildAnalysisPreviewPng(PsdDocument document, out int canvasLeft, out int canvasTop, out int canvasWidth, out int canvasHeight)
        {
            canvasLeft = 0;
            canvasTop = 0;
            canvasWidth = 0;
            canvasHeight = 0;
            if (document?.Inner == null)
            {
                return null;
            }

            var rendered = PsdReader.PsdRenderer.MergeLayers(
                document.Inner.Childs,
                includeHidden: false,
                applyClippingMasks: true,
                preview: false) ?? PsdReader.PsdRenderer.Render(document.Inner);
            if (rendered == null || rendered.IsEmpty)
            {
                return null;
            }

            canvasLeft = rendered.Left;
            canvasTop = rendered.Top;
            canvasWidth = rendered.Width;
            canvasHeight = rendered.Height;
            var format = rendered.IsHighBitDepth ? TextureFormat.RGBA64 : TextureFormat.RGBA32;
            var texture = new Texture2D(rendered.Width, rendered.Height, format, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                alphaIsTransparency = true
            };
            try
            {
                if (rendered.IsHighBitDepth)
                {
                    var rawData = new byte[rendered.Rgba64.Length * sizeof(ushort)];
                    Buffer.BlockCopy(rendered.Rgba64, 0, rawData, 0, rawData.Length);
                    texture.LoadRawTextureData(rawData);
                }
                else
                {
                    texture.LoadRawTextureData(rendered.Rgba32);
                }
                texture.Apply(false, false);
                return PsdTextureAssetUtility.EncodePng(texture);
            }
            finally
            {
                DestroyImmediate(texture);
            }
        }
        private void OnEnable()
        {
            Instance = this;
            uiTypeLabelStyle = new GUIStyle();
            uiTypeLabelStyle.fontSize = 13;
            uiTypeLabelStyle.fontStyle = UnityEngine.FontStyle.BoldAndItalic;
            UnityEngine.ColorUtility.TryParseHtmlString("#7ED994", out var color);
            uiTypeLabelStyle.normal.textColor = color;

            if (psdInstance == null && !string.IsNullOrWhiteSpace(PsdAssetName))
            {
                RefreshNodesBindLayer();
            }
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
        }

        private void Start()
        {
            RefreshNodesBindLayer();
        }


        private void OnDrawGizmos()
        {
            if (drawLayerRectGizmos)
            {
                var nodes = this.GetComponentsInChildren<PsdLayerNode>();
                PsdLayerNode nodeSelected = null;
                Gizmos.color = drawLayerRectGizmosColor;
                var currentSelect = Selection.activeGameObject;
                foreach (var item in nodes)
                {
                    if (item.gameObject == currentSelect)
                    {
                        nodeSelected = item;
                        continue;
                    }
                    if (item.NeedExportImage())
                    {
                        Gizmos.DrawWireCube(item.LayerRect.position * 0.01f, item.LayerRect.size * 0.01f);
                    }
                }
                if (nodeSelected != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireCube(nodeSelected.LayerRect.position * 0.01f, nodeSelected.LayerRect.size * 0.01f);
                }
            }
        }


        private void OnSceneGUI(SceneView view)
        {
            var e = Event.current;
            if (e != null && e.type == EventType.MouseUp && e.button == 0)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                Plane plane = new Plane(Vector3.forward, Vector3.zero);
                if (!plane.Raycast(ray, out float enter))
                    return;

                Vector3 worldHitPos = ray.GetPoint(enter);
                PsdLayerNode clickNode = FindTopMostPsdLayerNode(worldHitPos, transform);
                if (clickNode != null)
                {
                    Selection.activeGameObject = clickNode.gameObject;
                    EditorGUIUtility.PingObject(clickNode.gameObject);
                    Event.current.Use();
                }
            }
        }
        private static PsdLayerNode FindTopMostPsdLayerNode(Vector3 worldPosition, Transform root)
        {
            if (root == null) return null;

            int childCount = root.childCount;

            // 1. 先从子节点（从上到下）开始，保证上层优先
            for (int i = childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                PsdLayerNode hitInChild = FindTopMostPsdLayerNode(worldPosition, child);
                if (hitInChild != null)
                    return hitInChild;
            }

            // 2. 鍐嶆娴嬭嚜宸?
            PsdLayerNode selfNode = root.GetComponent<PsdLayerNode>();
            if (selfNode != null)
            {
                if (IsHitWorldAABB(worldPosition, selfNode.LayerRect))
                    return selfNode;
            }

            return null;
        }

        private static bool IsHitWorldAABB(Vector3 worldPos, Rect layerRect)
        {
            Vector2 worldCenter = layerRect.position * 0.01f;
            Vector2 worldSize = layerRect.size * 0.01f;

            Vector2 halfSize = worldSize * 0.5f;
            Vector2 min = worldCenter - halfSize;
            Vector2 max = worldCenter + halfSize;

            return worldPos.x >= min.x && worldPos.x <= max.x &&
                   worldPos.y >= min.y && worldPos.y <= max.y;
        }
        private void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            if (Event.current == null) return;
            var node = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (node == null || node == this.gameObject) return;
            if (!node.TryGetComponent<PsdLayerNode>(out var layer)) return;

            Rect tmpRect = selectionRect;
            tmpRect.x = 35;
            tmpRect.width = 10;
            Undo.RecordObject(layer, RecordLayerOperation);
            EditorGUI.BeginChangeCheck();
            {
                layer.markToExport = EditorGUI.Toggle(tmpRect, layer.markToExport);
                if (EditorGUI.EndChangeCheck())
                {
                    if (Selection.gameObjects.Length > 1) SetExportImageTg(Selection.gameObjects, layer.markToExport);
                    EditorUtility.SetDirty(layer);
                }
            }
            tmpRect.width = Mathf.Clamp(selectionRect.xMax * 0.2f, 100, 200);
            tmpRect.x = selectionRect.xMax - tmpRect.width;
            if (EditorGUI.DropdownButton(tmpRect, new GUIContent(layer.UIType.ToString()), FocusType.Passive))
            {
                var dropdownMenu = PopUITypesMenu(layer, selectUIType =>
                {
                    var selectObjs = Selection.gameObjects;
                    if (selectObjs.Length > 1)
                    {
                        foreach (var item in selectObjs)
                        {
                            var psdLayerNode = item?.GetComponent<PsdLayerNode>();
                            if (psdLayerNode != null)
                            {
                                psdLayerNode.SetUIType(selectUIType);
                                EditorUtility.SetDirty(psdLayerNode);
                            }
                        }
                    }
                    else
                    {
                        layer.SetUIType(selectUIType);
                    }
                });

                dropdownMenu.ShowAsContext();
            }
            if (layer.HasReuseReference || layer.HasReusePrefabReference)
            {
                var content = new GUIContent(layer.name);
                var calcSize = GUI.skin.button.CalcSize(content);
                var buttonWidth = Mathf.Min(calcSize.x + 8f, 100f);
                var buttonRect = new Rect(selectionRect.xMax - tmpRect.width - buttonWidth, selectionRect.y, buttonWidth, selectionRect.height);
                if (GUI.Button(buttonRect, content))
                {
                    if (layer.HasReuseReference && layer.TryGetReuseTarget(out PsdLayerNode targetNode))
                    {
                        Selection.activeGameObject = targetNode.gameObject;
                    }
                    else if (layer.HasReuseReference && layer.TryGetReuseTargetAsset(out UnityEngine.Object targetAsset))
                    {
                        Selection.activeObject = targetAsset;
                    }
                    else if (layer.HasReusePrefabReference && layer.TryGetReusePrefabAsset(out var targetPrefab))
                    {
                        Selection.activeObject = targetPrefab;
                    }
                }
            }
        }
        private GenericMenu PopUITypesMenu(PsdLayerNode layer, Action<GUIType> onSelectEnum)
        {
            var names = Enum.GetValues(typeof(GUIType));
            var dropdownMenu = new GenericMenu();
            foreach (GUIType item in names)
            {
                string itemName = UGUIParser.IsMainUIType(item) ? item.ToString() : item.ToString().Replace('_', '/');
                dropdownMenu.AddItem(new GUIContent(itemName), item.Equals(layer.UIType), () => { onSelectEnum(item); });
            }
            return dropdownMenu;
        }

        /// <summary>
        /// 批量勾选导出图片
        /// </summary>
        /// <param name="selects"></param>
        /// <param name="exportImg"></param>
        private void SetExportImageTg(GameObject[] selects, bool exportImg)
        {
            var selectLayerNodes = selects.Where(item => item?.GetComponent<PsdLayerNode>() != null).ToArray();
            foreach (var layer in selectLayerNodes)
            {
                layer.GetComponent<PsdLayerNode>().markToExport = exportImg;
            }
        }

        private void OnDestroy()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
            ReleasePsdFileHandle();
        }

        public void ReleasePsdFileHandle()
        {
            if (psdInstance != null)
            {
                psdInstance.Dispose();
                psdInstance = null;
            }
        }

        private void RefreshNodesBindLayer()
        {
            if (psdInstance == null)
            {
                if (!File.Exists(PsdAssetName))
                {
                    Debug.LogError($"刷新节点绑定图层失败! psd文件不存在:{PsdAssetName}");
                    return;
                }
                try
                {
                    EditorUtility.DisplayProgressBar("文件加载中", string.Format("正在读取psd文件:{0}\n文件过大会影响读取速度,建议通过栅格化图层减小psd大小", PsdAssetName), 0.5f);
                    psdInstance = PsdDocument.Create(PsdAssetName);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    return;
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
            var layers = GetComponentsInChildren<PsdLayerNode>(true);
            foreach (var layer in layers)
            {
                layer.InitPsdLayers(psdInstance);
            }

            var spRender = gameObject.GetComponent<SpriteRenderer>() ?? gameObject.AddComponent<SpriteRenderer>();
            spRender.sprite = this.psdAsset;
        }

        [MenuItem("Assets/Psd2UIForm Editor", priority = 0)]
        static void Psd2UIFormPrefabMenu()
        {
            if (Selection.activeObject == null) return;
            var assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (Path.GetExtension(assetPath).ToLower().CompareTo(".psd") != 0)
            {
                Debug.LogWarning($"选择的文件({assetPath})不是psd格式, 工具只支持psd转换为UIForm");
                return;
            }
            string psdLayerPrefab = GetPsdLayerPrefabPath(assetPath);
            if (!File.Exists(psdLayerPrefab))
            {
                if (ParsePsd2LayerPrefab(assetPath))
                {
                    OpenPsdLayerEditor(psdLayerPrefab);
                }
            }
            else
            {
                OpenPsdLayerEditor(psdLayerPrefab);
            }
        }
        [MenuItem("Assets/Psd2UIForm Editor", true)]
        static bool ValidatePsd2UIFormPrefabMenu()
        {
            if (Selection.activeObject != null)
            {
                var assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(assetPath) && Path.GetExtension(assetPath).ToLower().CompareTo(".psd") == 0)
                {
                    return true;
                }
            }

            return false;
        }
        public bool CheckPsdAssetHasChanged()
        {
            if (psdAsset == null) return false;
            var fileTag = GetAssetChangeTag(PsdAssetName);
            return psdAssetChangeTime.CompareTo(fileTag) != 0;
        }
        public static string GetAssetChangeTag(string fileName)
        {
            return new FileInfo(fileName).LastWriteTimeUtc.ToString("yyyyMMddHHmmss");
        }
        /// <summary>
        /// 打开psd图层信息prefab
        /// </summary>
        /// <param name="psdLayerPrefab"></param>
        public static void OpenPsdLayerEditor(string psdLayerPrefab)
        {
            OpenPrefab(psdLayerPrefab);
        }
        public static void OpenPrefab(string prefabName)
        {
#if UNITY_2021_1_OR_NEWER
            PrefabStageUtility.OpenPrefab(prefabName);
#else
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabName);
            AssetDatabase.OpenAsset(prefab);
#endif
        }
        /// <summary>
        /// 把Psd图层解析成节点prefab
        /// </summary>
        /// <param name="psdPath"></param>
        /// <returns></returns>
        public static bool ParsePsd2LayerPrefab(
            string psdFile,
            Psd2UIFormConverter instanceRoot = null,
            bool openPrefabAfterSave = true)
        {
            if (!File.Exists(psdFile))
            {
                Debug.LogError($"Error: Psd文件不存在:{psdFile}");
                return false;
            }
            var texImporter = AssetImporter.GetAtPath(psdFile) as TextureImporter;
            if (texImporter == null)
            {
                Debug.LogError($"Error: 无法获取PSD导入设置:{psdFile}");
                return false;
            }
            if (texImporter.textureType != TextureImporterType.Sprite || texImporter.spriteImportMode != SpriteImportMode.Single)
            {
                texImporter.textureType = TextureImporterType.Sprite;
                texImporter.spriteImportMode = SpriteImportMode.Single;
                texImporter.mipmapEnabled = false;
                texImporter.alphaIsTransparency = true;
                texImporter.SaveAndReimport();
            }

            var prefabFile = GetPsdLayerPrefabPath(psdFile);
            var rootName = Path.GetFileNameWithoutExtension(prefabFile);

            bool needDestroyInstance = instanceRoot == null;
            if (instanceRoot != null)
            {
                ParsePsdLayer2Root(psdFile, instanceRoot);
                return true;
            }
            else
            {
                Psd2UIFormConverter rootLayer = CreatePsdLayerRoot(rootName);
                if (rootLayer == null)
                {
                    Debug.LogError($"解析PSD失败: 无法创建根节点组件 {nameof(Psd2UIFormConverter)}");
                    return false;
                }

                rootLayer.psdAssetChangeTime = GetAssetChangeTag(psdFile);
                rootLayer.SetPsdAsset(psdFile);
                ParsePsdLayer2Root(psdFile, rootLayer);

                PrefabUtility.SaveAsPrefabAsset(rootLayer.gameObject, prefabFile, out bool savePrefabSuccess);
                if (needDestroyInstance) GameObject.DestroyImmediate(rootLayer.gameObject);
                AssetDatabase.Refresh();
                var currentStagePath = StageUtility.GetCurrentStage()?.assetPath;
                bool isSameStagePrefab = !string.IsNullOrWhiteSpace(currentStagePath)
                    && AssetDatabase.GUIDFromAssetPath(currentStagePath) == AssetDatabase.GUIDFromAssetPath(prefabFile);
                if (savePrefabSuccess && openPrefabAfterSave && !isSameStagePrefab)
                {
                    OpenPrefab(prefabFile);
                }

                return savePrefabSuccess;
            }
        }
        private static void ParsePsdLayer2Root(string psdFile, Psd2UIFormConverter converter)
        {
            EditorUtility.DisplayProgressBar("解析PSD", $"正在解析{psdFile}", 0);
            var totalSw = System.Diagnostics.Stopwatch.StartNew();
            var phaseSw = System.Diagnostics.Stopwatch.StartNew();
            //清空已有节点重新解析
            for (int i = converter.transform.childCount - 1; i >= 0; i--)
            {
                GameObject.DestroyImmediate(converter.transform.GetChild(i).gameObject);
            }
            Debug.Log($"[ParsePsd] 清空节点: {phaseSw.ElapsedMilliseconds}ms");
            phaseSw.Restart();

            try
            {
                var psd = PsdDocument.Create(psdFile);
                Debug.Log($"[ParsePsd] PsdDocument.Create: {phaseSw.ElapsedMilliseconds}ms");
                phaseSw.Restart();
                {
                    int totalCount = psd.CountAllLayers();
                    Debug.Log($"[ParsePsd] CountAllLayers({totalCount}): {phaseSw.ElapsedMilliseconds}ms");
                    phaseSw.Restart();
                    int parsedCount = 0;
                    int nextBindIndex = 0;
                    for (int i = 0; i < psd.Childs.Length; i++)
                    {
                        var rootLayer = psd.Childs[i] as PsdLayer;
                        if (rootLayer == null) continue;
                        ParsePsdLayerRecursive(rootLayer, converter.transform, ref nextBindIndex, ref parsedCount, totalCount);
                    }
                    Debug.Log($"[ParsePsd] ParsePsdLayerRecursive: {phaseSw.ElapsedMilliseconds}ms");
                    phaseSw.Restart();
                }
                // 复用已打开的 PSD 实例，避免重新打开文件（解析通道数据依赖文件流）
                if (converter.psdInstance != null)
                {
                    converter.psdInstance.Dispose();
                }
                converter.psdInstance = psd;
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogException(e);
                return;
            }

            converter.psdAssetChangeTime = GetAssetChangeTag(psdFile);
            converter.RefreshNodesBindLayer();
            Debug.Log($"[ParsePsd] RefreshNodesBindLayer: {phaseSw.ElapsedMilliseconds}ms");
            phaseSw.Restart();
            var childrenNodes = converter.GetComponentsInChildren<PsdLayerNode>(true);
            UIHelperBase.SuppressOnEnable = true;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var item in childrenNodes)
                {
                    item.RefreshUIHelper(false);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                UIHelperBase.SuppressOnEnable = false;
            }
            Debug.Log($"[ParsePsd] RefreshUIHelper: {phaseSw.ElapsedMilliseconds}ms");
            EditorUtility.SetDirty(converter.gameObject);
            Debug.Log($"[ParsePsdLayer2Root] 总耗时: {totalSw.ElapsedMilliseconds}ms");
            EditorUtility.ClearProgressBar();
        }
        private void SetPsdAsset(string psdFile)
        {
            this.psdAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(psdFile);
            if (string.IsNullOrWhiteSpace(Psd2UIFormSettings.Instance.UIImagesOutputDir))
            {
                Psd2UIFormSettings.Instance.UIImagesOutputDir = Path.GetDirectoryName(psdFile);
            }
            if (string.IsNullOrWhiteSpace(this.uiFormName))
            {
                this.uiFormName = this.psdAsset.name;
            }
        }

        /// <summary>
        /// 获取解析好的psd layers文件
        /// </summary>
        /// <param name="psd"></param>
        /// <returns></returns>
        public static string GetPsdLayerPrefabPath(string psd)
        {
            return Path.Combine(Path.GetDirectoryName(psd), Path.GetFileNameWithoutExtension(psd) + "_psd_layers_parsed.prefab");
        }
        private static Psd2UIFormConverter CreatePsdLayerRoot(string rootName)
        {
            var node = new GameObject(rootName, typeof(RectTransform));
            node.gameObject.tag = "EditorOnly";
            node.transform.localPosition = Vector3.zero;
            node.transform.localRotation = Quaternion.identity;
            node.transform.localScale = Vector3.one;
            var layerRoot = node.AddComponent<Psd2UIFormConverter>();
            if (layerRoot == null)
            {
                GameObject.DestroyImmediate(node);
            }
            return layerRoot;
        }
        private static void ParsePsdLayerRecursive(PsdLayer layer, Transform parentNode, ref int nextBindIndex, ref int parsedCount, int totalCount)
        {
            if (layer == null || parentNode == null) return;

            parsedCount++;
            EditorUtility.DisplayProgressBar($"解析PSD({parsedCount}/{Mathf.Max(1, totalCount)})", $"正在解析图层:{layer.GetDisplayName()}", totalCount > 0 ? parsedCount / (float)totalCount : 1f);

            int bindLayerIdx = layer.IsGroup ? nextBindIndex + layer.CountFlatIndexSlots() - 1 : nextBindIndex;
            var newLayerNode = CreatePsdLayerNode(layer, bindLayerIdx);
            newLayerNode.transform.SetParent(parentNode);
            newLayerNode.transform.localPosition = Vector3.zero;

            if (layer.Childs != null && layer.Childs.Length > 0)
            {
                int childBindIndex = nextBindIndex + 1;
                for (int i = 0; i < layer.Childs.Length; i++)
                {
                    var childLayer = layer.Childs[i];
                    if (childLayer == null) continue;
                    ParsePsdLayerRecursive(childLayer, newLayerNode.transform, ref childBindIndex, ref parsedCount, totalCount);
                }
            }

            nextBindIndex += layer.CountFlatIndexSlots();
        }
        private static PsdLayerNode CreatePsdLayerNode(PsdLayer layer, int bindLayerIdx)
        {
            string nodeName = layer.GetDisplayName();
            nodeName = UGUIParser.Instance.GetSafeLayerName(nodeName, bindLayerIdx);
            var node = new GameObject(nodeName, typeof(RectTransform));
            node.gameObject.tag = "EditorOnly";
            node.transform.localPosition = Vector3.zero;
            node.transform.localRotation = Quaternion.identity;
            node.transform.localScale = Vector3.one;
            var layerNode = node.AddComponent<PsdLayerNode>();
            layerNode.BindPsdLayerIndex = bindLayerIdx;
            InitLayerNodeData(layerNode, layer);
            return layerNode;
        }

        /// <summary>
        /// 根据psd图层信息解析并初始化图层UI类型、是否导出等信息
        /// </summary>
        /// <param name="layerNode"></param>
        /// <param name="layer"></param>
        private static void InitLayerNodeData(PsdLayerNode layerNode, PsdLayer layer)
        {
            if (layer == null) return;
            var layerTp = layer.GetLayerType();
            layerNode.BindPsdLayer = layer;
            layerNode.SetSourceLayerName(layer.GetDisplayName());
            layerNode.ClearManualUITypeOverride();
            if (UGUIParser.Instance.TryParse(layerNode, out var initRule))
            {
                layerNode.SetUIType(initRule.UIType, false);
            }
            bool isTextBackedUI = layerTp == PsdLayerType.TextLayer && layerNode.UIType.ToString().EndsWith("Text") && layerNode.UIType != GUIType.FillColor;
            bool isFillColorUI = layerTp == PsdLayerType.FillLayer || layerNode.UIType == GUIType.FillColor;
            layerNode.markToExport = layerTp != PsdLayerType.LayerGroup && !isTextBackedUI && !isFillColorUI;
            layerNode.gameObject.SetActive(layer.IsVisible);
            if (layerNode.HasReuseReference || layerNode.HasReusePrefabReference)
            {
                layerNode.markToExport = false;
            }
        }

        /// <summary>
        /// 导出psd图层为Sprites碎图
        /// </summary>
        /// <param name="psdAssetName"></param>
        internal void ExportSprites()
        {
            ResolveLayerNameLookup();
            var exportLayers = this.GetComponentsInChildren<PsdLayerNode>().Where(node => node.NeedExportImage()).ToArray();
            var exportDir = GetUIFormImagesOutputDir();
            if (!Directory.Exists(exportDir))
            {
                Directory.CreateDirectory(exportDir);
            }

            var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var phaseStopwatch = System.Diagnostics.Stopwatch.StartNew();

            // 阶段1: 批量写出所有 PNG 文件（不触发 AssetDatabase）
            int totalCount = exportLayers.Length;
            for (int exportIdx = 0; exportIdx < totalCount; exportIdx++)
            {
                var layer = exportLayers[exportIdx];
                var assetName = layer.ExportImageAsset(batchMode: true);
                if (assetName == null)
                {
                    Debug.LogWarning($"导出图层[name:{layer.name}, layerIdx:{layer.BindPsdLayerIndex}]图片失败!");
                }
                EditorUtility.DisplayProgressBar($"导出进度({exportIdx + 1}/{totalCount})", $"写出图片:{assetName}", (exportIdx + 1) / (float)totalCount);
            }
            Debug.Log($"[ExportSprites] 阶段1(渲染+写PNG): {phaseStopwatch.ElapsedMilliseconds}ms, 共{totalCount}个图层");
            phaseStopwatch.Restart();

            // 阶段2: 一次性刷新 AssetDatabase（之前是每张图片刷新一次）
            EditorUtility.DisplayProgressBar("导入资产", "刷新 AssetDatabase...", 0f);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[ExportSprites] 阶段2(AssetDatabase.Refresh): {phaseStopwatch.ElapsedMilliseconds}ms");
            phaseStopwatch.Restart();

            // 阶段3: 批量设置纹理导入类型
            var nineSliceAssets = new List<(string path, bool didCrop, Vector4 border)>();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var layer in exportLayers)
                {
                    if (!layer._batchExportInfo.HasValue) continue;
                    var info = layer._batchExportInfo.Value;

                    var texImporter = AssetImporter.GetAtPath(info.assetPath) as TextureImporter;
                    if (texImporter == null) continue;

                    if (info.isSprite)
                    {
                        texImporter.textureType = TextureImporterType.Sprite;
                        texImporter.spriteImportMode = SpriteImportMode.Single;
                        texImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                        texImporter.alphaIsTransparency = true;
                        texImporter.mipmapEnabled = false;
                    }
                    else
                    {
                        texImporter.textureType = TextureImporterType.Default;
                        texImporter.textureShape = TextureImporterShape.Texture2D;
                        texImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                        texImporter.alphaIsTransparency = true;
                        texImporter.mipmapEnabled = false;
                    }

                    PsdTextureAssetUtility.ApplyPrecisionImportSettings(texImporter, info.preferHighBitDepth);

                    // 九宫格：只有裁剪成功（中间有重复像素区域）才设置 border
                    if (info.isSprite && info.didCrop)
                    {
                        texImporter.spriteBorder = info.croppedBorder;
                        Debug.Log($"[NineSlice:Phase3] 已裁剪: {info.assetPath}, border={info.croppedBorder}, origSize={info.imageWidth}x{info.imageHeight}");
                    }
                    else if (info.isSprite)
                    {
                        texImporter.spriteBorder = Vector4.zero;
                    }

                    texImporter.SaveAndReimport();
                    layer._batchExportInfo = null;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // 阶段4: 批量计算未裁剪的九宫格 border
            if (nineSliceAssets.Count > 0)
            {
                // 先触发一次刷新，确保纹理可读后再计算 border
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                AssetDatabase.StartAssetEditing();
                try
                {
                    for (int i = 0; i < nineSliceAssets.Count; i++)
                    {
                        var item = nineSliceAssets[i];
                        EditorUtility.DisplayProgressBar("九宫格计算", $"计算九宫格:{item.path}", (i + 1) / (float)nineSliceAssets.Count);
                        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(item.path);
                        var texImporter = AssetImporter.GetAtPath(item.path) as TextureImporter;
                        if (sprite != null && texImporter != null && texImporter.spriteBorder == Vector4.zero)
                        {
                            texImporter.spriteBorder = UGUIParser.CalculateTexture9SliceBorder(sprite.texture);
                            texImporter.isReadable = false;
                            texImporter.SaveAndReimport();
                        }
                        else if (texImporter != null)
                        {
                            texImporter.isReadable = false;
                            texImporter.SaveAndReimport();
                        }
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
            }

            Debug.Log($"[ExportSprites] 阶4(九宫格): {phaseStopwatch.ElapsedMilliseconds}ms, {nineSliceAssets.Count}个");
            Debug.Log($"[ExportSprites] 总耗时: {totalStopwatch.ElapsedMilliseconds}ms");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorUtility.ClearProgressBar();
        }
        /// <summary>
        /// 根据解析后的节点树生成UIForm Prefab
        /// </summary>
        public void GenerateUIForm(PsdLayerNode exportNode = null, string outputNameOverride = null)
        {
            if (!TryResolveUIFormOutputDirectory(out var outputDir)) return;
            GenerateUIFormToDirectory(exportNode, outputNameOverride, outputDir);
        }

        internal bool TryResolveUIFormOutputDirectory(out string outputDir)
        {
            outputDir = null;
            if (Psd2UIFormSettings.Instance.UseUIFormOutputDir)
            {
                outputDir = Psd2UIFormSettings.Instance.UIFormOutputDir;
                if (string.IsNullOrWhiteSpace(outputDir))
                {
                    Debug.LogError($"生成UIForm失败! UIForm导出路径为空:{outputDir}");
                    return false;
                }
                return true;
            }

            string lastSaveDir = string.IsNullOrWhiteSpace(Psd2UIFormSettings.Instance.LastUIFormOutputDir)
                ? "Assets"
                : Psd2UIFormSettings.Instance.LastUIFormOutputDir;
            string selectDir = EditorUtility.SaveFolderPanel("保存目录", lastSaveDir, null);
            if (string.IsNullOrWhiteSpace(selectDir)) return false;

            if (!selectDir.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(selectDir, "Assets", StringComparison.OrdinalIgnoreCase))
            {
                selectDir = PathExtensions.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, selectDir);
            }
            Psd2UIFormSettings.Instance.LastUIFormOutputDir = selectDir;
            outputDir = selectDir;
            return true;
        }

        internal bool GenerateUIFormToDirectory(
            PsdLayerNode exportNode,
            string outputNameOverride,
            string outputDir)
        {
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                Debug.LogError("生成UIForm失败: 输出目录为空。");
                return false;
            }

            if (exportNode != null
                && exportNode.transform != transform
                && !exportNode.transform.IsChildOf(transform))
            {
                Debug.LogError($"生成UIForm失败: 节点不属于当前 PSD。node={exportNode.name}, psd={name}");
                return false;
            }

            Transform rootNode = exportNode != null ? exportNode.transform : transform;
            var requestedOutputName = GetPanelPrefabOutputName(outputNameOverride);
            Debug.Log($"[PSD2UIForm:GenerateUIForm] source={name}, root={rootNode.name}, uiFormName={uiFormName}, outputOverride={outputNameOverride ?? "<null>"}, finalOutput={requestedOutputName}, imageDir={GetUIFormImagesOutputDir()}");
            return ExportUIPrefab(rootNode, outputDir, requestedOutputName);
        }

        public bool GenerateUIFormForBatch(string outputName, string outputDir)
        {
            if (string.IsNullOrWhiteSpace(outputDir))
                throw new ArgumentException("UIForm output directory is required.", nameof(outputDir));

            return ExportUIPrefab(transform, outputDir, GetPanelPrefabOutputName(outputName), allowOverwrite: true);
        }

        private string GetPanelPrefabOutputName(string outputNameOverride = null)
        {
            var outputName = string.IsNullOrWhiteSpace(outputNameOverride) ? uiFormName : outputNameOverride.Trim();
            if (string.IsNullOrWhiteSpace(outputName))
                return outputName;

            return outputName.EndsWith("Panel", StringComparison.Ordinal) ? outputName : outputName + "Panel";
        }

        private static void NormalizeUIFormRootRect(GameObject root)
        {
            if (root == null)
                return;

            var rect = root.GetComponent<RectTransform>();
            if (rect == null)
                rect = root.AddComponent<RectTransform>();

            rect.anchorMin        = Vector2.zero;
            rect.anchorMax        = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.pivot            = new Vector2(0.5f, 0.5f);
            rect.offsetMax        = Vector2.zero;
            rect.offsetMin        = Vector2.zero;
            rect.sizeDelta        = Vector2.zero;
            rect.localEulerAngles = Vector3.zero;
            rect.localScale       = Vector3.one;
        }

        private static void NormalizeGeneratedContainerRects(GameObject root)
        {
            if (root == null)
                return;

            NormalizeGeneratedContainerRectsRecursive(root.transform, root.transform);
        }

        private static void NormalizeGeneratedContainerRectsRecursive(Transform current, Transform root)
        {
            if (current == null)
                return;

            for (int i = 0; i < current.childCount; i++)
            {
                NormalizeGeneratedContainerRectsRecursive(current.GetChild(i), root);
            }

            if (current == root)
                return;

            var generatedKey = current.GetComponent<PsdGeneratedKey>();
            if (generatedKey == null || !generatedKey.IsContainer || generatedKey.TypeKey != GeneratedContainerTypeKey)
                return;

            var rect = current as RectTransform;
            if (rect == null || rect.childCount < 1)
                return;

            FitRectToDirectChildren(rect);
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

            RestoreDirectChildWorldPositions(childPositions);
        }

        private static bool TryCalculateDirectChildrenBounds(RectTransform rect, out Bounds bounds)
        {
            bounds = default;
            if (rect == null)
                return false;

            var corners = new Vector3[4];
            bool hasBounds = false;
            for (int childIndex = 0; childIndex < rect.childCount; childIndex++)
            {
                var childRect = rect.GetChild(childIndex) as RectTransform;
                if (childRect == null)
                    continue;

                childRect.GetWorldCorners(corners);
                for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    var localCorner = rect.InverseTransformPoint(corners[cornerIndex]);
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
            if (rect == null || rect.childCount < 1)
                return Array.Empty<DirectChildWorldPosition>();

            var result = new DirectChildWorldPosition[rect.childCount];
            for (int i = 0; i < rect.childCount; i++)
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

        private static void RestoreDirectChildWorldPositions(DirectChildWorldPosition[] positions)
        {
            if (positions == null)
                return;

            for (int i = 0; i < positions.Length; i++)
            {
                var item = positions[i];
                if (item.Transform != null)
                    item.Transform.position = item.Position;
            }
        }

        internal void ApplyGroupDerivedStates()
        {
            var nodes = GetComponentsInChildren<PsdLayerNode>(true);
            if (nodes == null) return;
            foreach (var node in nodes)
            {
                if (node == null) continue;
                node.SetGroupGenerationState(false, -1);
                if (node.LayerType != PsdLayerType.LayerGroup) continue;

                if (IsGroupTextSourceType(node.UIType) || IsGroupTextSourceSemanticType(node.UIType))
                {
                    var textSource = FindGroupTextSource(node);
                    if (textSource != null)
                    {
                        node.SetGroupGenerationState(true, textSource.BindPsdLayerIndex);
                    }
                    node.markToExport = false;
                }
                else if (IsGroupImageSourceType(node.UIType) || IsGroupImageSourceSemanticType(node.UIType))
                {
                    node.SetGroupGenerationState(true, -1);
                    node.markToExport = !node.HasReuseReference && !node.HasReusePrefabReference;
                }
                else
                {
                    node.markToExport = false;
                }
                EditorUtility.SetDirty(node);
            }
        }

        internal void RefreshAllLayerNodeHelpers()
        {
            ApplyGroupDerivedStates();
            var nodes = GetComponentsInChildren<PsdLayerNode>(true);
            if (nodes == null) return;
            foreach (var node in nodes)
            {
                node?.RefreshUIHelper(false);
            }
        }

        internal bool NormalizeLocalStructure(bool registerUndo, out string summary)
        {
            if (!PsdStructureNormalizer.Normalize(this, registerUndo, out var report))
            {
                summary = report != null ? report.BuildSummary() : "本地结构归一化失败。";
                return false;
            }

            summary = report != null ? report.BuildSummary() : "本地结构归一化完成。";
            return true;
        }

        private static PsdLayerNode FindGroupTextSource(PsdLayerNode groupNode)
        {
            if (groupNode == null) return null;
            var textNodes = groupNode.GetComponentsInChildren<PsdLayerNode>(true)
                .Where(node => node != null && node != groupNode
                    && node.LayerType == PsdLayerType.TextLayer && node.gameObject.activeSelf)
                .OrderBy(node => node.BindPsdLayerIndex)
                .ToArray();
            if (textNodes.Length == 0) return null;
            return textNodes.FirstOrDefault(node => IsGroupTextSourceType(node.UIType)
                || IsGroupTextSourceSemanticType(node.UIType)) ?? textNodes[0];
        }

        private static bool IsGroupImageSourceType(GUIType uiType)
            => uiType == GUIType.Image || uiType == GUIType.RawImage || uiType == GUIType.Mask;

        private static bool IsGroupTextSourceType(GUIType uiType)
            => uiType == GUIType.Text || uiType == GUIType.TMPText;

        private static bool IsGroupTextSourceSemanticType(GUIType uiType)
        {
            switch (uiType)
            {
                case GUIType.Button_Text:
                case GUIType.Dropdown_Label:
                case GUIType.InputField_Placeholder:
                case GUIType.InputField_Text:
                case GUIType.Toggle_Label:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsGroupImageSourceSemanticType(GUIType uiType)
        {
            switch (uiType)
            {
                case GUIType.Background:
                case GUIType.Button_Highlight:
                case GUIType.Button_Press:
                case GUIType.Button_Select:
                case GUIType.Button_Disable:
                case GUIType.Dropdown_Arrow:
                case GUIType.Toggle_Checkmark:
                case GUIType.Slider_Fill:
                case GUIType.Slider_Handle:
                case GUIType.ScrollView_Viewport:
                case GUIType.ScrollView_HorizontalBarBG:
                case GUIType.ScrollView_HorizontalBar:
                case GUIType.ScrollView_VerticalBarBG:
                case GUIType.ScrollView_VerticalBar:
                    return true;
                default:
                    return false;
            }
        }

        private struct DirectChildWorldPosition
        {
            public Transform Transform;
            public Vector3 Position;
        }

        private static void NormalizeSavedUIFormPrefabRoot(string prefabPath)
        {
            if (string.IsNullOrWhiteSpace(prefabPath) || !File.Exists(prefabPath))
                return;

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                NormalizeUIFormRootRect(root);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public void ExportAsPrefab(PsdLayerNode targetLogic)
        {
            if (targetLogic == null)
            {
                Debug.LogError("导出prefab失败: 目标节点为空");
                return;
            }
            if (UGUIParser.Instance == null)
            {
                Debug.LogError("导出prefab失败: UGUIParser配置未找到");
                return;
            }
            if (string.IsNullOrWhiteSpace(UGUIParser.Instance.SharedPrefabOutput))
            {
                Debug.LogError($"导出prefab失败! 复用prefab导出路径为空:{UGUIParser.Instance.SharedPrefabOutput}");
                return;
            }

            var outputDir = NormalizeAssetDirectory(UGUIParser.Instance.SharedPrefabOutput);
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                Debug.LogError($"导出prefab失败! 复用prefab导出路径无效:{UGUIParser.Instance.SharedPrefabOutput}");
                return;
            }
            if (!Directory.Exists(outputDir))
            {
                try
                {
                    Directory.CreateDirectory(outputDir);
                    AssetDatabase.Refresh();
                }
                catch (Exception err)
                {
                    Debug.LogError($"创建复用prefab导出目录失败:{err.Message}");
                    return;
                }
            }

            var prefabBaseName = LayerNameUtility.Sanitize(targetLogic.name);
            if (string.IsNullOrWhiteSpace(prefabBaseName))
            {
                prefabBaseName = "PsdLayerPrefab";
            }
            var prefabName = Path.Combine(outputDir, $"{prefabBaseName}.prefab").Replace("\\", "/");
            bool prefabExists = File.Exists(prefabName);
            if (prefabExists)
            {
                if (!EditorUtility.DisplayDialog("警告", $"prefab文件已存在, 是否覆盖:{prefabName}", "覆盖生成", "取消生成"))
                {
                    return;
                }
                // 不删除现有prefab,让SaveAsPrefabAssetAndConnect保留GUID
            }

            var rootLayerNode = targetLogic.GetComponent<PsdLayerNode>();
            bool ignoreRootPrefabReference = rootLayerNode != null
                && rootLayerNode.HasReusePrefabReference
                && string.Equals(rootLayerNode.ReusePrefabKey, prefabBaseName, StringComparison.OrdinalIgnoreCase);

            var uiPrefab = ExportLayerTreeAsPrefab(targetLogic, prefabName, prefabBaseName, ignoreRootPrefabReference);
            if (uiPrefab != null)
            {
                Selection.activeGameObject = uiPrefab;
            }
        }
        private bool ExportUIPrefab(
            Transform root,
            string outputDir,
            string outputNameOverride = null,
            bool allowOverwrite = false)
        {
            if (!string.IsNullOrWhiteSpace(outputDir))
            {
                if (!Directory.Exists(outputDir))
                {
                    try
                    {
                        Directory.CreateDirectory(outputDir);
                        AssetDatabase.Refresh();
                    }
                    catch (Exception err)
                    {
                        Debug.LogError($"导出UI prefab失败:{err.Message}");
                        return false;
                    }
                }
            }
            var outputName = string.IsNullOrWhiteSpace(outputNameOverride) ? GetPanelPrefabOutputName() : outputNameOverride.Trim();
            if (string.IsNullOrWhiteSpace(outputName))
            {
                Debug.LogError("导出UI Prefab失败: UI Form Name为空, 请填写UI Form Name.");
                return false;
            }
            var prefabName = Path.Combine(outputDir, $"{outputName}.prefab");
            Debug.Log($"[PSD2UIForm:ExportUIPrefab] root={root.name}, outputDir={outputDir}, outputName={outputName}, prefab={prefabName}");
            var overwriteWholePrefab = root == this.transform && File.Exists(prefabName);
            if (overwriteWholePrefab)
            {
                if (!allowOverwrite
                    && !EditorUtility.DisplayDialog("警告", $"prefab文件已存在, 是否覆盖:{prefabName}", "覆盖生成", "取消生成"))
                {
                    return false;
                }
            }
            var layerNodes = root.GetComponentsInChildren<PsdLayerNode>(true);
            var prefabRefRoots = new List<PsdLayerNode>();
            var prefabRefCandidates = layerNodes.Where(node => node != null && node.HasReusePrefabReference).ToArray();
            var rootLayerNode = root.GetComponent<PsdLayerNode>();
            if (rootLayerNode != null && rootLayerNode.HasReusePrefabReference)
            {
                prefabRefRoots.Add(rootLayerNode);
            }
            else if (prefabRefCandidates.Length > 0)
            {
                foreach (var node in prefabRefCandidates)
                {
                    if (node == null) continue;
                    bool hasPrefabAncestor = false;
                    var parent = node.transform.parent;
                    while (parent != null && parent != root)
                    {
                        var parentNode = parent.GetComponent<PsdLayerNode>();
                        if (parentNode != null && parentNode.HasReusePrefabReference)
                        {
                            hasPrefabAncestor = true;
                            break;
                        }
                        parent = parent.parent;
                    }
                    if (!hasPrefabAncestor)
                    {
                        prefabRefRoots.Add(node);
                    }
                }
            }
            bool IsInsidePrefabRef(Transform transform)
            {
                if (transform == null || prefabRefRoots.Count == 0) return false;
                foreach (var prefabRef in prefabRefRoots)
                {
                    if (prefabRef == null) continue;
                    if (transform == prefabRef.transform || transform.IsChildOf(prefabRef.transform)) return true;
                }
                return false;
            }

            var nodesForBuild = layerNodes.Where(node => node != null && !IsInsidePrefabRef(node.transform)).ToArray();
            ResolveLayerNameLookup();
            referencedLayerKeys.Clear();
            foreach (var node in nodesForBuild)
            {
                if (!node.HasReuseReference || string.IsNullOrEmpty(node.ReuseTargetKey)) continue;
                referencedLayerKeys.Add(node.ReuseTargetKey);
            }
            ExportSharedReferenceSprites(nodesForBuild);

            // A collection screen root may be inactive only because another
            // sibling screen was visible when the PSD was saved. Include that
            // root's activeSelf children while still excluding children that
            // are explicitly hidden inside the screen itself.
            var uiHelpers = GetAvailableUIHelpers(root, includeInactiveRoot: root != this.transform);
            if (uiHelpers != null && uiHelpers.Length > 0 && prefabRefRoots.Count > 0)
            {
                for (int i = uiHelpers.Length - 1; i >= 0; i--)
                {
                    var helper = uiHelpers[i];
                    if (helper == null || helper.LayerNode == null) continue;
                    if (IsInsidePrefabRef(helper.transform))
                    {
                        ArrayUtility.RemoveAt(ref uiHelpers, i);
                    }
                }
            }
            if ((uiHelpers == null || uiHelpers.Length < 1) && prefabRefRoots.Count < 1)
            {
                return false;
            }
            GameObject uiFormRoot;
            var existingUiFormPrefab = overwriteWholePrefab ? null : AssetDatabase.LoadAssetAtPath<GameObject>(prefabName);
            if (existingUiFormPrefab == null)
            {
                if (UGUIParser.Instance.UIFormTemplate == null)
                {
                    Debug.LogError("生成UIForm失败: UIFormTemplate为空");
                    return false;
                }

                uiFormRoot = GameObject.Instantiate(UGUIParser.Instance.UIFormTemplate, Vector3.zero, Quaternion.identity);
                uiFormRoot.transform.localScale = Vector3.one;
                PrefabUtility.SaveAsPrefabAsset(uiFormRoot, prefabName);
            }
            else
            {
                uiFormRoot = GameObject.Instantiate(existingUiFormPrefab);// PrefabUtility.LoadPrefabContents(prefabName);
                RestoreGeneratedKeysFromMetadata(prefabName, uiFormRoot);
            }
            uiFormRoot.name = outputName;
            NormalizeUIFormRootRect(uiFormRoot);
            var activeGeneratedKeys = new HashSet<string>(StringComparer.Ordinal);
            string incrementalScopePathKey = null;
            if (root != this.transform)
            {
                incrementalScopePathKey = GetGameObjectHierarchyPathKey(root.gameObject, this.transform);
                if (!string.IsNullOrEmpty(incrementalScopePathKey))
                {
                    CollectExistingGeneratedKeys(uiFormRoot.transform, activeGeneratedKeys,
                        generatedKey => !IsPathInScope(generatedKey.Key, incrementalScopePathKey));
                }
            }
            Vector3 canvasPosition = Vector3.zero;
            var uiFormRect = uiFormRoot.GetComponent<RectTransform>();
            if (uiFormRect != null)
            {
                canvasPosition = uiFormRect.anchoredPosition;
            }
            int curIdx = 0;
            int totalCount = uiHelpers.Length;
            foreach (var uiHelper in uiHelpers)
            {
                if (uiHelper == null || uiHelper.LayerNode == null) continue;

                EditorUtility.DisplayProgressBar($"生成UIFrom:({curIdx++}/{totalCount})", $"正在生成UI元素:{uiHelper.name}", curIdx /
                (float)totalCount);
                var elementPathKey = GetGameObjectHierarchyPathKey(uiHelper.gameObject, this.transform);
                if (string.IsNullOrEmpty(elementPathKey) && uiHelper.transform == this.transform)
                {
                    elementPathKey = GetRootGeneratedElementKey(uiHelper.LayerNode);
                }
                var goPath = GetGameObjectHierarchyParentPath(uiHelper.gameObject, this.transform, out var goNames);
                var parentNode = GetOrCreateNodeByHierarchyPath(uiFormRoot, goPath, goNames, activeGeneratedKeys);
                var reusableNode = TryGetReusableGeneratedNode(parentNode.transform, uiHelper, elementPathKey);
                var uiElement = uiHelper.CreateUI(reusableNode);
                if (uiElement == null) continue;

                CopyBindingMarker(uiHelper.gameObject, uiElement);
                SetGeneratedNodeKey(uiElement, elementPathKey, uiHelper.LayerNode.UIType.ToString(), false, activeGeneratedKeys);
                uiElement.transform.SetParent(parentNode.transform, true);
                uiElement.transform.SetAsLastSibling(); // 确保 UGUI 层级顺序与 PSD 图层顺序一致
                uiElement.transform.position += canvasPosition;
                uiElement.transform.localScale = Vector3.one;
                RefreshSplitTMPTextEffectsAfterParenting(uiHelper, uiElement);
            }
            if (prefabRefRoots.Count > 0)
            {
                int prefabIdx = 0;
                int prefabTotal = prefabRefRoots.Count;
                foreach (var prefabRefNode in prefabRefRoots)
                {
                    if (prefabRefNode == null || !prefabRefNode.HasReusePrefabReference) continue;

                    EditorUtility.DisplayProgressBar($"生成UIFrom-引用prefab:({prefabIdx++}/{prefabTotal})", $"正在实例化prefab:{prefabRefNode.ReusePrefabDisplayName}", prefabIdx / (float)prefabTotal);
                    if (!TryGetOrCreateSharedPrefabAsset(prefabRefNode, out var prefabAsset) || prefabAsset == null)
                    {
                        Debug.LogWarning($"引用prefab未找到且导出失败: {prefabRefNode.ReusePrefabDisplayName}");
                        continue;
                    }

                    var pathKey = GetGameObjectHierarchyPathKey(prefabRefNode.gameObject, this.transform);
                    var goPath = GetGameObjectHierarchyParentPath(prefabRefNode.gameObject, this.transform, out var goNames);
                    var parentNode = GetOrCreateNodeByHierarchyPath(uiFormRoot, goPath, goNames, activeGeneratedKeys);
                    var prefabInstance = TryGetReusableGeneratedPrefabNode(parentNode.transform, prefabRefNode, pathKey, prefabAsset);
                    if (prefabInstance == null)
                    {
                        prefabInstance = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
                        if (prefabInstance == null)
                        {
                            prefabInstance = GameObject.Instantiate(prefabAsset);
                        }
                    }
                    prefabInstance.name = string.IsNullOrEmpty(prefabRefNode.ReusePrefabKey) ? prefabAsset.name : prefabRefNode.ReusePrefabKey;
                    prefabInstance.transform.localRotation = Quaternion.identity;
                    prefabInstance.transform.localScale = Vector3.one;
                    var prefabRect = prefabInstance.GetComponent<RectTransform>();
                    if (prefabRect != null)
                    {
                        UGUIParser.SetRectTransform(prefabRefNode, prefabRect);
                    }
                    else
                    {
                        Debug.LogWarning($"引用prefab缺少RectTransform: {prefabAsset.name}");
                    }

                    if (prefabRefNode.GetComponent<PsdLayerMarker>() != null)
                        CopyBindingMarker(prefabRefNode.gameObject, prefabInstance);
                    SetGeneratedNodeKey(prefabInstance, pathKey, GeneratedPrefabReferenceTypeKey, false, activeGeneratedKeys);
                    prefabInstance.transform.SetParent(parentNode.transform, true);
                    prefabInstance.transform.SetAsLastSibling(); // 确保 UGUI 层级顺序与 PSD 图层顺序一致
                    prefabInstance.transform.position += canvasPosition;
                    prefabInstance.transform.localScale = Vector3.one;
                }
            }

            CleanupLegacyUiStringKeys(uiFormRoot);
            CleanupStaleGeneratedNodes(uiFormRoot.transform, activeGeneratedKeys);
            NormalizeGeneratedContainerRects(uiFormRoot);
            var generatedNodeSnapshots = CaptureGeneratedNodeSnapshots(uiFormRoot);
            CleanupTransientGeneratedKeys(uiFormRoot);

            var uiPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(uiFormRoot, prefabName, InteractionMode.UserAction);
            if (uiPrefab != null)
            {
                SaveGeneratedKeysMetadata(prefabName, generatedNodeSnapshots, incrementalScopePathKey);
                DestroyImmediate(uiFormRoot);
                NormalizeSavedUIFormPrefabRoot(prefabName);
                Selection.activeGameObject = uiPrefab;
            }

            EditorUtility.ClearProgressBar();
            return true;
        }

        private GameObject GetOrCreateNodeByHierarchyPath(GameObject uiFormRoot, string[] goPath, string[] goNames, HashSet<string> activeGeneratedKeys)
        {
            GameObject result = uiFormRoot;
            if (goPath != null && goNames != null)
            {
                for (int i = 0; i < goPath.Length; i++)
                {
                    var nodeKey = goPath[i];
                    var nodeName = goNames[i];
                    var targetNode = FindGeneratedChildByPath(result.transform, nodeKey, false);
                    bool useExistingUiNode = targetNode != null;
                    if (!useExistingUiNode)
                    {
                        targetNode = FindGeneratedChild(result.transform, nodeKey, GeneratedContainerTypeKey, true);
                    }
                    if (targetNode == null)
                    {
                        targetNode = FindLegacyChildByName(result.transform, nodeName, true);
                    }
                    if (targetNode == null)
                    {
                        targetNode = new GameObject(nodeName, typeof(RectTransform));
                        targetNode.layer = UnityEngine.LayerMask.NameToLayer("UI");
                        targetNode.transform.SetParent(result.transform, false);
                        targetNode.transform.localPosition = Vector3.zero;
                        targetNode.transform.localRotation = Quaternion.identity;
                        targetNode.transform.localScale = Vector3.one;
                    }
                    if (!useExistingUiNode)
                    {
                        SetGeneratedNodeKey(targetNode, nodeKey, GeneratedContainerTypeKey, true, activeGeneratedKeys);
                    }
                    result = targetNode;
                }
            }
            return result;
        }

        private static string BuildGeneratedCompositeKey(string key, string typeKey, bool isContainer)
        {
            return $"{(isContainer ? "C" : "N")}:{typeKey}:{key}";
        }

        private static string GetLegacyGeneratedMetadataPath(string prefabAssetPath)
        {
            if (string.IsNullOrWhiteSpace(prefabAssetPath)) return null;

            var dir = Path.GetDirectoryName(prefabAssetPath);
            var fileName = Path.GetFileNameWithoutExtension(prefabAssetPath);
            if (string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(fileName)) return null;

            return Path.Combine(dir, $"{fileName}.psd2uiform.generated.json").Replace("\\", "/");
        }

        private static string NormalizeMetadataPrefabPath(string prefabAssetPath)
        {
            return string.IsNullOrWhiteSpace(prefabAssetPath) ? null : prefabAssetPath.Replace("\\", "/").Trim();
        }

        private static bool IsSameMetadataPrefabPath(string leftPath, string rightPath)
        {
            var leftNormalized = NormalizeMetadataPrefabPath(leftPath);
            var rightNormalized = NormalizeMetadataPrefabPath(rightPath);
            if (string.IsNullOrWhiteSpace(leftNormalized) || string.IsNullOrWhiteSpace(rightNormalized)) return false;
            return string.Equals(leftNormalized, rightNormalized, StringComparison.OrdinalIgnoreCase);
        }

        private GeneratedMetadataSerializedEntry FindGeneratedMetadataEntry(string targetPrefabAssetPath)
        {
            if (generatedMetadataEntries == null || generatedMetadataEntries.Count < 1) return null;

            for (int i = 0; i < generatedMetadataEntries.Count; i++)
            {
                var entry = generatedMetadataEntries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.PrefabAssetPath)) continue;
                if (IsSameMetadataPrefabPath(entry.PrefabAssetPath, targetPrefabAssetPath))
                {
                    return entry;
                }
            }
            return null;
        }

        private static List<GeneratedMetadataEntry> CloneGeneratedMetadataEntries(IEnumerable<GeneratedMetadataEntry> source)
        {
            var result = new List<GeneratedMetadataEntry>();
            if (source == null) return result;

            foreach (var item in source)
            {
                if (item == null
                    || string.IsNullOrWhiteSpace(item.GlobalObjectId)
                    || string.IsNullOrWhiteSpace(item.Key)
                    || string.IsNullOrWhiteSpace(item.TypeKey))
                {
                    continue;
                }

                result.Add(new GeneratedMetadataEntry
                {
                    GlobalObjectId = item.GlobalObjectId,
                    Key = item.Key,
                    TypeKey = item.TypeKey,
                    IsContainer = item.IsContainer,
                });
            }

            return result;
        }

        private static List<GeneratedMetadataEntry> ParseGeneratedMetadataJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<GeneratedMetadataEntry>();
            }

            try
            {
                var metadata = JsonUtility.FromJson<GeneratedMetadataCollection>(json);
                return CloneGeneratedMetadataEntries(metadata?.Entries);
            }
            catch
            {
                return new List<GeneratedMetadataEntry>();
            }
        }

        private void CleanupGeneratedMetadataEntries()
        {
            if (generatedMetadataEntries == null)
            {
                generatedMetadataEntries = new List<GeneratedMetadataSerializedEntry>();
                return;
            }

            var map = new Dictionary<string, GeneratedMetadataSerializedEntry>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < generatedMetadataEntries.Count; i++)
            {
                var entry = generatedMetadataEntries[i];
                var normalizedPath = NormalizeMetadataPrefabPath(entry?.PrefabAssetPath);
                if (string.IsNullOrWhiteSpace(normalizedPath)) continue;
                var normalizedEntries = CloneGeneratedMetadataEntries(entry.Entries);
                if (normalizedEntries.Count < 1)
                {
                    normalizedEntries = ParseGeneratedMetadataJson(entry.Json);
                }
                if (normalizedEntries.Count < 1) continue;

                map[normalizedPath] = new GeneratedMetadataSerializedEntry
                {
                    PrefabAssetPath = normalizedPath,
                    Json = null,
                    Entries = normalizedEntries,
                };
            }

            generatedMetadataEntries = map.Values.OrderBy(item => item.PrefabAssetPath, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private void MarkGeneratedMetadataDirty()
        {
            EditorUtility.SetDirty(this);
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            if (gameObject != null && gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
            AssetDatabase.SaveAssets();
        }

        internal string GetGeneratedMetadataHostAssetPathForDebug()
        {
            var assetPath = AssetDatabase.GetAssetPath(gameObject);
            return string.IsNullOrWhiteSpace(assetPath) ? "<Scene Object>" : assetPath;
        }

        internal string[] GetGeneratedMetadataTargetsForDebug()
        {
            if (generatedMetadataEntries == null || generatedMetadataEntries.Count < 1)
            {
                return Array.Empty<string>();
            }

            return generatedMetadataEntries
                .Where(item => item != null
                    && !string.IsNullOrWhiteSpace(item.PrefabAssetPath)
                    && item.Entries != null
                    && item.Entries.Count > 0)
                .Select(item => NormalizeMetadataPrefabPath(item.PrefabAssetPath))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        internal string GetGeneratedMetadataJsonForDebug(string targetPrefabAssetPath)
        {
            var entry = FindGeneratedMetadataEntry(targetPrefabAssetPath);
            if (entry == null || entry.Entries == null || entry.Entries.Count < 1) return string.Empty;

            var metadata = new GeneratedMetadataCollection
            {
                Entries = CloneGeneratedMetadataEntries(entry.Entries),
            };
            return JsonUtility.ToJson(metadata, true);
        }

        private List<GeneratedMetadataEntry> LoadGeneratedMetadataEntries(string targetPrefabAssetPath)
        {
            var entry = FindGeneratedMetadataEntry(targetPrefabAssetPath);
            if (entry != null && entry.Entries != null && entry.Entries.Count > 0)
            {
                return CloneGeneratedMetadataEntries(entry.Entries);
            }
            if (entry != null && !string.IsNullOrWhiteSpace(entry.Json))
            {
                var migratedEntries = ParseGeneratedMetadataJson(entry.Json);
                if (migratedEntries.Count > 0)
                {
                    SaveGeneratedMetadataEntries(targetPrefabAssetPath, migratedEntries);
                    return migratedEntries;
                }
            }

            var legacyMetadataPath = GetLegacyGeneratedMetadataPath(targetPrefabAssetPath);
            if (!string.IsNullOrWhiteSpace(legacyMetadataPath) && File.Exists(legacyMetadataPath))
            {
                var legacyJson = File.ReadAllText(legacyMetadataPath);
                if (!string.IsNullOrWhiteSpace(legacyJson))
                {
                    var migratedEntries = ParseGeneratedMetadataJson(legacyJson);
                    if (migratedEntries.Count > 0)
                    {
                        SaveGeneratedMetadataEntries(targetPrefabAssetPath, migratedEntries);
                        return migratedEntries;
                    }
                }
            }

            return null;
        }

        private void SaveGeneratedMetadataEntries(string targetPrefabAssetPath, List<GeneratedMetadataEntry> entries)
        {
            var normalizedPath = NormalizeMetadataPrefabPath(targetPrefabAssetPath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                Debug.LogWarning($"保存生成节点元数据失败: target为空, target={targetPrefabAssetPath}");
                return;
            }

            if (generatedMetadataEntries == null)
            {
                generatedMetadataEntries = new List<GeneratedMetadataSerializedEntry>();
            }

            var entry = FindGeneratedMetadataEntry(normalizedPath);
            var normalizedEntries = CloneGeneratedMetadataEntries(entries);
            if (normalizedEntries.Count < 1)
            {
                if (entry != null)
                {
                    generatedMetadataEntries.Remove(entry);
                }
            }
            else
            {
                if (entry == null)
                {
                    entry = new GeneratedMetadataSerializedEntry();
                    generatedMetadataEntries.Add(entry);
                }

                entry.PrefabAssetPath = normalizedPath;
                entry.Entries = normalizedEntries;
            }

            CleanupGeneratedMetadataEntries();
            MarkGeneratedMetadataDirty();

            var legacyMetadataPath = GetLegacyGeneratedMetadataPath(targetPrefabAssetPath);
            if (!string.IsNullOrWhiteSpace(legacyMetadataPath) && File.Exists(legacyMetadataPath))
            {
                AssetDatabase.DeleteAsset(legacyMetadataPath);
            }
        }

        private List<GeneratedNodeSnapshot> CaptureGeneratedNodeSnapshots(GameObject root)
        {
            var snapshots = new List<GeneratedNodeSnapshot>();
            if (root == null) return snapshots;

            var generatedKeys = root.GetComponentsInChildren<PsdGeneratedKey>(true);
            foreach (var generatedKey in generatedKeys)
            {
                if (generatedKey == null) continue;
                snapshots.Add(new GeneratedNodeSnapshot
                {
                    InstanceObject = generatedKey.gameObject,
                    Key = generatedKey.Key,
                    TypeKey = generatedKey.TypeKey,
                    IsContainer = generatedKey.IsContainer,
                });
            }
            return snapshots;
        }

        private static void CleanupTransientGeneratedKeys(GameObject root)
        {
            if (root == null) return;

            var generatedKeys = root.GetComponentsInChildren<PsdGeneratedKey>(true);
            for (int i = generatedKeys.Length - 1; i >= 0; i--)
            {
                if (generatedKeys[i] != null)
                {
                    DestroyImmediate(generatedKeys[i]);
                }
            }
        }

        private void RestoreGeneratedKeysFromMetadata(string prefabAssetPath, GameObject prefabInstance)
        {
            if (prefabInstance == null) return;

            var metadataEntries = LoadGeneratedMetadataEntries(prefabAssetPath);
            if (metadataEntries == null || metadataEntries.Count < 1) return;

            try
            {
                var sourceToInstance = new Dictionary<string, GameObject>(StringComparer.Ordinal);
                var transforms = prefabInstance.GetComponentsInChildren<Transform>(true);
                foreach (var item in transforms)
                {
                    if (item == null) continue;
                    var sourceGo = PrefabUtility.GetCorrespondingObjectFromSource(item.gameObject) as GameObject;
                    if (sourceGo == null) continue;

                    var globalId = GlobalObjectId.GetGlobalObjectIdSlow(sourceGo).ToString();
                    if (string.IsNullOrEmpty(globalId)) continue;
                    sourceToInstance[globalId] = item.gameObject;
                }

                foreach (var entry in metadataEntries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.GlobalObjectId)) continue;
                    if (!sourceToInstance.TryGetValue(entry.GlobalObjectId, out var instanceGo) || instanceGo == null) continue;

                    SetGeneratedNodeKey(instanceGo, entry.Key, entry.TypeKey, entry.IsContainer);
                }
            }
            catch (Exception err)
            {
                Debug.LogWarning($"恢复生成节点元数据失败: {prefabAssetPath}\n{err.Message}");
            }
        }

        private void SaveGeneratedKeysMetadata(string prefabAssetPath, List<GeneratedNodeSnapshot> snapshots, string incrementalScopePathKey = null)
        {
            try
            {
                if (snapshots == null || snapshots.Count < 1)
                {
                    SaveGeneratedMetadataEntries(prefabAssetPath, null);
                    return;
                }

                var metadataEntries = new List<GeneratedMetadataEntry>();
                foreach (var snapshot in snapshots)
                {
                    if (snapshot == null || snapshot.InstanceObject == null) continue;

                    var sourceGo = PrefabUtility.GetCorrespondingObjectFromSource(snapshot.InstanceObject) as GameObject;
                    if (sourceGo == null) continue;

                    var globalId = GlobalObjectId.GetGlobalObjectIdSlow(sourceGo).ToString();
                    if (string.IsNullOrEmpty(globalId)) continue;

                    metadataEntries.Add(new GeneratedMetadataEntry
                    {
                        GlobalObjectId = globalId,
                        Key = snapshot.Key,
                        TypeKey = snapshot.TypeKey,
                        IsContainer = snapshot.IsContainer,
                    });
                }

                if (metadataEntries.Count < 1)
                {
                    SaveGeneratedMetadataEntries(prefabAssetPath, null);
                    return;
                }

                if (!string.IsNullOrEmpty(incrementalScopePathKey))
                {
                    var oldMetadataEntries = LoadGeneratedMetadataEntries(prefabAssetPath);
                    if (oldMetadataEntries != null && oldMetadataEntries.Count > 0)
                    {
                        var compositeKeys = new HashSet<string>(metadataEntries.Select(entry => BuildGeneratedCompositeKey(entry.Key, entry.TypeKey, entry.IsContainer)), StringComparer.Ordinal);
                        foreach (var entry in oldMetadataEntries)
                        {
                            if (entry == null || IsPathInScope(entry.Key, incrementalScopePathKey)) continue;

                            var compositeKey = BuildGeneratedCompositeKey(entry.Key, entry.TypeKey, entry.IsContainer);
                            if (compositeKeys.Add(compositeKey))
                            {
                                metadataEntries.Add(entry);
                            }
                        }
                    }
                }

                SaveGeneratedMetadataEntries(prefabAssetPath, metadataEntries);
            }
            catch (Exception err)
            {
                Debug.LogWarning($"保存生成节点元数据失败: {prefabAssetPath}\n{err.Message}");
            }
        }

        private static bool IsPathInScope(string pathKey, string scopePathKey)
        {
            if (string.IsNullOrEmpty(pathKey) || string.IsNullOrEmpty(scopePathKey)) return false;

            return string.Equals(pathKey, scopePathKey, StringComparison.Ordinal)
                || pathKey.StartsWith(scopePathKey + "/", StringComparison.Ordinal);
        }

        private void CollectExistingGeneratedKeys(Transform root, HashSet<string> activeGeneratedKeys, Func<PsdGeneratedKey, bool> includePredicate)
        {
            if (root == null || activeGeneratedKeys == null) return;

            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child == null) continue;

                var generatedKey = child.GetComponent<PsdGeneratedKey>();
                if (generatedKey != null && (includePredicate == null || includePredicate(generatedKey)))
                {
                    activeGeneratedKeys.Add(BuildGeneratedCompositeKey(generatedKey.Key, generatedKey.TypeKey, generatedKey.IsContainer));
                }

                CollectExistingGeneratedKeys(child, activeGeneratedKeys, includePredicate);
            }
        }

        private static Type GetPrimaryRootComponentType(GameObject target)
        {
            if (target == null) return null;

            var components = target.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null) continue;
                var type = component.GetType();
                if (type == typeof(Transform)
                    || type == typeof(RectTransform)
                    || type == typeof(CanvasRenderer)
                    || type == typeof(UIStringKey)
                    || type == typeof(PsdGeneratedKey))
                {
                    continue;
                }
                return type;
            }
            return null;
        }

        private static bool IsLikelyMatchingGeneratedRoot(GameObject existing, GUIType uiType)
        {
            var rule = UGUIParser.Instance?.GetRule(uiType);
            if (rule == null || rule.UIPrefab == null || existing == null) return false;

            var expectedType = GetPrimaryRootComponentType(rule.UIPrefab);
            var existingType = GetPrimaryRootComponentType(existing);
            if (expectedType == null || existingType == null)
            {
                return false;
            }
            return expectedType == existingType;
        }

        private GameObject FindGeneratedChild(Transform parent, string pathKey, string typeKey, bool isContainer)
        {
            if (parent == null || string.IsNullOrEmpty(pathKey) || string.IsNullOrEmpty(typeKey)) return null;

            foreach (Transform child in parent)
            {
                if (child == null) continue;
                var key = child.GetComponent<PsdGeneratedKey>();
                if (key == null) continue;
                if (key.IsContainer != isContainer) continue;
                if (!string.Equals(key.TypeKey, typeKey, StringComparison.Ordinal)) continue;
                if (!string.Equals(key.Key, pathKey, StringComparison.Ordinal)) continue;
                return child.gameObject;
            }
            return null;
        }

        private GameObject FindGeneratedChildByPath(Transform parent, string pathKey, bool isContainer)
        {
            if (parent == null || string.IsNullOrEmpty(pathKey)) return null;

            foreach (Transform child in parent)
            {
                if (child == null) continue;
                var key = child.GetComponent<PsdGeneratedKey>();
                if (key == null) continue;
                if (key.IsContainer != isContainer) continue;
                if (!string.Equals(key.Key, pathKey, StringComparison.Ordinal)) continue;
                return child.gameObject;
            }
            return null;
        }

        private GameObject FindLegacyChildByName(Transform parent, string nodeName, bool isContainer, GUIType? expectedUiType = null)
        {
            if (parent == null || string.IsNullOrWhiteSpace(nodeName)) return null;

            GameObject result = null;
            int matchCount = 0;
            foreach (Transform child in parent)
            {
                if (child == null || !string.Equals(child.name, nodeName, StringComparison.Ordinal)) continue;
                if (child.GetComponent<PsdGeneratedKey>() != null) continue;

                if (isContainer)
                {
                    if (GetPrimaryRootComponentType(child.gameObject) != null) continue;
                }
                else if (expectedUiType.HasValue && !IsLikelyMatchingGeneratedRoot(child.gameObject, expectedUiType.Value))
                {
                    continue;
                }

                result = child.gameObject;
                matchCount++;
                if (matchCount > 1)
                {
                    return null;
                }
            }
            return result;
        }

        private static void CopyBindingMarker(GameObject source, GameObject target)
        {
            var marker = source.GetComponent<PsdLayerMarker>();
            var existing = target.GetComponent<PsdLayerMarker>();
            if (existing != null && existing.ManualBindingOverride && !existing.FollowsSource)
                return;

            if (marker == null || (!marker.ShouldBind && !marker.ManualBindingOverride))
            {
                if (existing != null && existing.FollowsSource)
                    DestroyImmediate(existing);
                return;
            }

            var output = existing ?? target.AddComponent<PsdLayerMarker>();
            EditorUtility.CopySerialized(marker, output);
            output.FollowsSource = true;
        }

        private void SetGeneratedNodeKey(GameObject go, string pathKey, string typeKey, bool isContainer, HashSet<string> activeGeneratedKeys = null)
        {
            if (go == null || string.IsNullOrEmpty(pathKey) || string.IsNullOrEmpty(typeKey)) return;

            var generatedKey = go.GetComponent<PsdGeneratedKey>() ?? go.AddComponent<PsdGeneratedKey>();
            generatedKey.Key = pathKey;
            generatedKey.TypeKey = typeKey;
            generatedKey.IsContainer = isContainer;
            activeGeneratedKeys?.Add(BuildGeneratedCompositeKey(pathKey, typeKey, isContainer));
        }

        private static void CleanupLegacyUiStringKeys(GameObject root)
        {
            if (root == null) return;

            var uiStrKeys = root.GetComponentsInChildren<UIStringKey>(true);
            for (int i = uiStrKeys.Length - 1; i >= 0; i--)
            {
                if (uiStrKeys[i] != null)
                {
                    DestroyImmediate(uiStrKeys[i]);
                }
            }
        }

        private void CleanupStaleGeneratedNodes(Transform root, HashSet<string> activeGeneratedKeys)
        {
            if (root == null || activeGeneratedKeys == null) return;

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (child == null) continue;

                var generatedKey = child.GetComponent<PsdGeneratedKey>();
                if (generatedKey != null)
                {
                    var compositeKey = BuildGeneratedCompositeKey(generatedKey.Key, generatedKey.TypeKey, generatedKey.IsContainer);
                    if (!activeGeneratedKeys.Contains(compositeKey))
                    {
                        DestroyImmediate(child.gameObject);
                        continue;
                    }
                }

                CleanupStaleGeneratedNodes(child, activeGeneratedKeys);
            }
        }

        private string GetGameObjectHierarchyPathKey(GameObject go, Transform stopRoot)
        {
            if (go == null) return null;

            var node = go.GetComponent<PsdLayerNode>();
            if (node == null) return null;

            var segments = new List<string>();
            var current = go.transform;
            while (current != null && current != stopRoot)
            {
                var currentNode = current.GetComponent<PsdLayerNode>();
                if (currentNode != null)
                {
                    var segment = GetHierarchySegmentKey(currentNode);
                    if (!string.IsNullOrEmpty(segment))
                    {
                        segments.Insert(0, segment);
                    }
                }
                current = current.parent;
            }
            return segments.Count > 0 ? string.Join("/", segments) : null;
        }

        private static string GetRootGeneratedElementKey(PsdLayerNode node)
        {
            if (node == null) return "Root";

            var key = node.LayerNameLookupKey;
            if (!string.IsNullOrWhiteSpace(key)) return key;

            return string.IsNullOrWhiteSpace(node.name) ? "Root" : node.name;
        }

        private string[] GetGameObjectHierarchyParentPath(GameObject go, Transform stopRoot, out string[] names)
        {
            names = null;
            if (go == null || stopRoot == null) return null;
            if (go.transform == stopRoot) return null;
            if (go.transform.parent == null) return null;
            if (!go.transform.IsChildOf(stopRoot)) return null;
            if (go.transform.parent == stopRoot) return null;

            var parents = new List<PsdLayerNode>();
            var current = go.transform.parent;
            while (current != null && current != stopRoot)
            {
                var currentNode = current.GetComponent<PsdLayerNode>();
                if (currentNode != null)
                {
                    parents.Insert(0, currentNode);
                }
                current = current.parent;
            }
            if (parents.Count < 1) return null;

            names = parents.Select(item => item.gameObject.name).ToArray();
            return parents.Select(item => GetGameObjectHierarchyPathKey(item.gameObject, stopRoot)).ToArray();
        }

        private string GetHierarchySegmentKey(PsdLayerNode node)
        {
            if (node == null) return null;

            var baseKey = node.LayerNameLookupKey;
            if (string.IsNullOrEmpty(baseKey)) return null;

            int duplicateIndex = 0;
            var parent = node.transform.parent;
            if (parent != null)
            {
                for (int i = 0; i < parent.childCount; i++)
                {
                    var sibling = parent.GetChild(i);
                    var siblingNode = sibling != null ? sibling.GetComponent<PsdLayerNode>() : null;
                    if (siblingNode == null) continue;
                    if (ReferenceEquals(siblingNode, node)) break;
                    if (string.Equals(siblingNode.LayerNameLookupKey, baseKey, StringComparison.OrdinalIgnoreCase))
                    {
                        duplicateIndex++;
                    }
                }
            }

            return duplicateIndex > 0 ? $"{baseKey}[{duplicateIndex}]" : baseKey;
        }

        private GameObject TryGetReusableGeneratedNode(Transform parent, UIHelperBase uiHelper, string pathKey)
        {
            if (parent == null || uiHelper == null || uiHelper.LayerNode == null || string.IsNullOrEmpty(pathKey)) return null;

            string typeKey = uiHelper.LayerNode.UIType.ToString();
            var reusableNode = FindGeneratedChild(parent, pathKey, typeKey, false);
            if (reusableNode != null)
            {
                return reusableNode;
            }

            var conflictingNode = FindGeneratedChildByPath(parent, pathKey, false);
            if (conflictingNode != null)
            {
                DestroyImmediate(conflictingNode);
            }

            return FindLegacyChildByName(parent, uiHelper.name, false, uiHelper.LayerNode.UIType);
        }
        private static void RefreshSplitTMPTextEffectsAfterParenting(UIHelperBase uiHelper, GameObject uiElement)
        {
            if (uiHelper == null || uiHelper.LayerNode == null || uiElement == null) return;
            if (!uiHelper.LayerNode.ParseTextLayerInfo(out var textInfo)) return;
            if (!RequiresSplitTMPTextEffects(in textInfo)) return;

            var tmpText = uiElement.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (tmpText == null) return;

            UGUIParser.SetTextStyle(uiHelper.LayerNode, tmpText);
            UGUIParser.SetRectTransform(uiHelper.LayerNode, tmpText);
        }
        private static bool RequiresSplitTMPTextEffects(in TextLayerInfo textInfo)
        {
            if (!textInfo.HasOutline) return false;
            if (textInfo.HasGlow) return true;
            return textInfo.TMPOutlineSize > 0f
                && textInfo.TMPOutlinePosition == TextLayerInfo.TMPOutlineMode.Outside;
        }
        private static bool RequiresContainerRootForSplitTMPText(UIHelperBase rootHelper)
        {
            if (rootHelper == null || rootHelper.LayerNode == null) return false;
            if (!rootHelper.LayerNode.ParseTextLayerInfo(out var textInfo)) return false;
            return RequiresSplitTMPTextEffects(in textInfo);
        }

        private GameObject TryGetReusableGeneratedPrefabNode(Transform parent, PsdLayerNode prefabRefNode, string pathKey, GameObject prefabAsset)
        {
            if (parent == null || prefabRefNode == null || string.IsNullOrEmpty(pathKey)) return null;

            var reusableNode = FindGeneratedChild(parent, pathKey, GeneratedPrefabReferenceTypeKey, false);
            if (reusableNode != null)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(reusableNode);
                if (source == prefabAsset)
                {
                    return reusableNode;
                }
                DestroyImmediate(reusableNode);
            }

            var conflictingNode = FindGeneratedChildByPath(parent, pathKey, false);
            if (conflictingNode != null)
            {
                DestroyImmediate(conflictingNode);
            }

            var legacyName = string.IsNullOrEmpty(prefabRefNode.ReusePrefabKey) ? prefabRefNode.gameObject.name : prefabRefNode.ReusePrefabKey;
            var legacyNode = FindLegacyChildByName(parent, legacyName, false);
            if (legacyNode == null) return null;

            var legacySource = PrefabUtility.GetCorrespondingObjectFromSource(legacyNode);
            return legacySource == prefabAsset ? legacyNode : null;
        }
        private void ResolveLayerNameLookup(PsdLayerNode[] nodes = null)
        {
            layerLookupByName.Clear();
            referencedLayerKeys.Clear();
            if (nodes == null || nodes.Length == 0)
            {
                nodes = this.GetComponentsInChildren<PsdLayerNode>(true);
            }
            foreach (var node in nodes)
            {
                var key = node.LayerNameLookupKey;
                if (string.IsNullOrEmpty(key) || layerLookupByName.ContainsKey(key)) continue;
                layerLookupByName.Add(key, node);
            }
            foreach (var node in nodes)
            {
                if (!node.HasReuseReference || string.IsNullOrEmpty(node.ReuseTargetKey)) continue;
                referencedLayerKeys.Add(node.ReuseTargetKey);
                //if (!layerLookupByName.ContainsKey(node.ReuseTargetKey))
                //{
                //    Debug.LogWarning($"复用图层未找到: {node.name} -> {node.ReuseTargetDisplayName}");
                //}
            }
        }
        private void ExportSharedReferenceSprites(PsdLayerNode[] nodes)
        {
            sharedSpriteAssets.Clear();
            if (nodes == null || nodes.Length == 0) return;
            if (!TryEnsureSharedOutputDirectory(out var sharedDir)) return;
            var referencedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var node in nodes)
            {
                if (!node.HasReuseReference || string.IsNullOrEmpty(node.ReuseTargetKey)) continue;
                referencedKeys.Add(node.ReuseTargetKey);
            }
            foreach (var key in referencedKeys)
            {
                if (!layerLookupByName.TryGetValue(key, out var targetNode))
                {
                    continue;
                }
                var exportedPath = targetNode.ExportImageAsset(true, sharedDir, key, true);
                if (!string.IsNullOrEmpty(exportedPath))
                {
                    RegisterSharedSprite(key, exportedPath);
                }
            }
        }
        private bool TryEnsureSharedOutputDirectory(out string sharedDir)
        {
            sharedDir = null;
            var configuredPath = UGUIParser.Instance?.SharedAssetsOutput;
            if (string.IsNullOrWhiteSpace(configuredPath)) return false;

            sharedDir = NormalizeAssetDirectory(configuredPath);
            if (string.IsNullOrWhiteSpace(sharedDir)) return false;

            if (!Directory.Exists(sharedDir))
            {
                try
                {
                    Directory.CreateDirectory(sharedDir);
                    AssetDatabase.Refresh();
                }
                catch (Exception err)
                {
                    Debug.LogError($"创建共享资源目录失败:{err.Message}");
                    return false;
                }
            }
            return true;
        }
        internal bool TryResolveContentAddressedSharedSprite(byte[] pngBytes, string preferredName, bool isSprite, out string assetPath, out bool reusedExisting)
        {
            assetPath = null;
            reusedExisting = false;
            if (pngBytes == null || pngBytes.Length == 0) return false;
            if (!TryEnsureSharedOutputDirectory(out var sharedDir)) return false;

            EnsureSharedSpriteIndexLoaded(sharedDir);
            var hash = ComputeSha256Hex(pngBytes);
            if (string.IsNullOrEmpty(hash)) return false;

            if (!sharedSpriteIndexByHash.TryGetValue(hash, out var existingEntry))
            {
                return false;
            }

            var existingPath = NormalizeAssetPath(existingEntry.AssetPath);
            if (string.IsNullOrEmpty(existingPath) || !File.Exists(existingPath))
            {
                sharedSpriteIndexByHash.Remove(hash);
                SaveSharedSpriteIndex();
                return false;
            }

            if (IsPathUnderDirectory(existingPath, sharedDir))
            {
                assetPath = existingPath;
                reusedExisting = true;
                if (isSprite) EnsureSpriteAssetReady(assetPath);
                RegisterSharedSprite(hash, assetPath);
                return true;
            }

            var fileName = BuildSharedSpriteFileName(preferredName, hash);
            assetPath = Path.Combine(sharedDir, fileName + ".png").Replace("\\", "/");
            reusedExisting = File.Exists(assetPath);
            existingEntry.AssetPath = assetPath;
            existingEntry.PreferredName = string.IsNullOrWhiteSpace(existingEntry.PreferredName) ? preferredName : existingEntry.PreferredName;
            existingEntry.UpdatedAtUtc = DateTime.UtcNow.ToString("O");
            existingEntry.IsSprite = isSprite;
            sharedSpriteIndexByHash[hash] = existingEntry;
            SaveSharedSpriteIndex();
            if (reusedExisting && isSprite) EnsureSpriteAssetReady(assetPath);
            RegisterSharedSprite(hash, assetPath);
            return true;
        }
        internal void RegisterContentAddressedSharedSprite(byte[] pngBytes, string assetPath, string preferredName, bool isSprite)
        {
            if (pngBytes == null || pngBytes.Length == 0 || string.IsNullOrEmpty(assetPath)) return;
            if (!TryEnsureSharedOutputDirectory(out var sharedDir)) return;

            EnsureSharedSpriteIndexLoaded(sharedDir);
            var hash = ComputeSha256Hex(pngBytes);
            if (string.IsNullOrEmpty(hash)) return;

            var normalizedPath = NormalizeAssetPath(assetPath);
            if (string.IsNullOrEmpty(normalizedPath)) return;

            if (sharedSpriteIndexByHash.TryGetValue(hash, out var existingEntry))
            {
                var existingPath = NormalizeAssetPath(existingEntry.AssetPath);
                if (!string.IsNullOrEmpty(existingPath) && File.Exists(existingPath)) return;
            }

            var entry = new SharedSpriteIndexEntry
            {
                Hash = hash,
                AssetPath = normalizedPath,
                PreferredName = preferredName,
                FirstSeenUI = uiFormName,
                UpdatedAtUtc = DateTime.UtcNow.ToString("O"),
                IsSprite = isSprite
            };
            sharedSpriteIndexByHash[hash] = entry;
            SaveSharedSpriteIndex();
        }
        private void EnsureSharedSpriteIndexLoaded(string sharedDir)
        {
            var indexPath = Path.Combine(sharedDir, "__smartexport_shared_sprites.json").Replace("\\", "/");
            if (sharedSpriteIndexLoaded && string.Equals(sharedSpriteIndexPath, indexPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            sharedSpriteIndexLoaded = true;
            sharedSpriteIndexPath = indexPath;
            sharedSpriteIndexByHash.Clear();

            if (!File.Exists(indexPath))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(indexPath);
                var collection = JsonUtility.FromJson<SharedSpriteIndexCollection>(json);
                if (collection?.Entries == null) return;
                if (collection.Version < 2)
                {
                    // v1 过于激进，会把首次出现的私有散图也归入 Common，直接忽略并由新策略重建索引。
                    return;
                }

                foreach (var entry in collection.Entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.Hash) || string.IsNullOrEmpty(entry.AssetPath)) continue;
                    var normalizedPath = NormalizeAssetPath(entry.AssetPath);
                    if (string.IsNullOrEmpty(normalizedPath) || !File.Exists(normalizedPath)) continue;
                    entry.AssetPath = normalizedPath;
                    sharedSpriteIndexByHash[entry.Hash] = entry;
                }
            }
            catch (Exception err)
            {
                Debug.LogWarning($"读取Common散图索引失败:{indexPath}\n{err.Message}");
            }
        }
        private void SaveSharedSpriteIndex()
        {
            if (string.IsNullOrEmpty(sharedSpriteIndexPath)) return;

            try
            {
                var collection = new SharedSpriteIndexCollection
                {
                    Version = 2,
                    Entries = sharedSpriteIndexByHash.Values
                        .Where(entry => entry != null && !string.IsNullOrEmpty(entry.Hash) && !string.IsNullOrEmpty(entry.AssetPath))
                        .OrderBy(entry => entry.AssetPath, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };
                File.WriteAllText(sharedSpriteIndexPath, JsonUtility.ToJson(collection, true));
            }
            catch (Exception err)
            {
                Debug.LogWarning($"写入Common散图索引失败:{sharedSpriteIndexPath}\n{err.Message}");
            }
        }
        private static string ComputeSha256Hex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var chars = new char[hash.Length * 2];
                for (int i = 0; i < hash.Length; i++)
                {
                    var b = hash[i];
                    chars[i * 2] = GetHexChar(b >> 4);
                    chars[i * 2 + 1] = GetHexChar(b & 0x0F);
                }
                return new string(chars);
            }
        }
        private static char GetHexChar(int value)
        {
            return (char)(value < 10 ? '0' + value : 'a' + (value - 10));
        }
        private static string BuildSharedSpriteFileName(string preferredName, string hash)
        {
            var baseName = string.IsNullOrWhiteSpace(preferredName) ? "common_sprite" : preferredName.Trim();
            baseName = Path.GetFileNameWithoutExtension(baseName);
            baseName = string.IsNullOrWhiteSpace(baseName) ? "common_sprite" : baseName;
            baseName = string.Concat(baseName.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_'));
            baseName = baseName.Trim('_', '-');
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "common_sprite";

            var shortHash = string.IsNullOrEmpty(hash) ? DateTime.UtcNow.Ticks.ToString("x") : hash.Substring(0, Math.Min(12, hash.Length));
            return $"{baseName}_{shortHash}";
        }
        private static string NormalizeAssetPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return null;
            var normalized = assetPath.Replace("\\", "/").Trim();
            if (Path.IsPathRooted(normalized))
            {
                normalized = PathExtensions.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, normalized).Replace("\\", "/");
            }
            return normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "Assets", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : null;
        }
        private static bool IsPathUnderDirectory(string assetPath, string assetDir)
        {
            var path = NormalizeAssetPath(assetPath);
            var dir = NormalizeAssetPath(assetDir);
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(dir)) return false;
            dir = dir.TrimEnd('/');
            return string.Equals(path, dir, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(dir + "/", StringComparison.OrdinalIgnoreCase);
        }
        private static void EnsureSpriteAssetReady(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath)) return;

            if (AssetImporter.GetAtPath(assetPath) == null)
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }

            var texImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (texImporter == null) return;

            bool needSave = false;
            if (texImporter.textureType != TextureImporterType.Sprite)
            {
                texImporter.textureType = TextureImporterType.Sprite;
                needSave = true;
            }
            if (texImporter.spriteImportMode != SpriteImportMode.Single)
            {
                texImporter.spriteImportMode = SpriteImportMode.Single;
                needSave = true;
            }
            if (!texImporter.alphaIsTransparency)
            {
                texImporter.alphaIsTransparency = true;
                needSave = true;
            }
            if (texImporter.mipmapEnabled)
            {
                texImporter.mipmapEnabled = false;
                needSave = true;
            }

            if (needSave)
            {
                texImporter.SaveAndReimport();
            }
        }
        private bool TryEnsureSharedPrefabOutputDirectory(out string sharedDir)
        {
            sharedDir = null;
            var configuredPath = UGUIParser.Instance?.SharedPrefabOutput;
            if (string.IsNullOrWhiteSpace(configuredPath)) return false;

            sharedDir = NormalizeAssetDirectory(configuredPath);
            if (string.IsNullOrWhiteSpace(sharedDir)) return false;

            if (!Directory.Exists(sharedDir))
            {
                try
                {
                    Directory.CreateDirectory(sharedDir);
                    AssetDatabase.Refresh();
                }
                catch (Exception err)
                {
                    Debug.LogError($"创建共享Prefab目录失败:{err.Message}");
                    return false;
                }
            }
            return true;
        }
        private string NormalizeAssetDirectory(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir)) return null;
            var normalized = dir.Replace("\\", "/").Trim();
            if (Path.IsPathRooted(normalized))
            {
                normalized = PathExtensions.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, normalized).Replace("\\", "/");
            }
            if (string.Equals(normalized, "Assets", StringComparison.OrdinalIgnoreCase))
            {
                return "Assets";
            }
            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            return normalized.Replace("\\", "/");
        }
        private bool TryGetOrCreateSharedPrefabAsset(PsdLayerNode requester, out GameObject prefabAsset)
        {
            prefabAsset = null;
            if (requester == null || string.IsNullOrWhiteSpace(requester.ReusePrefabKey)) return false;

            var key = requester.ReusePrefabKey;
            if (sharedPrefabAssets.TryGetValue(key, out var cachedPrefab) && cachedPrefab != null)
            {
                prefabAsset = cachedPrefab;
                return true;
            }

            if (requester.TryGetReusePrefabAsset(out prefabAsset) && prefabAsset != null)
            {
                sharedPrefabAssets[key] = prefabAsset;
                return true;
            }

            if (!TryEnsureSharedPrefabOutputDirectory(out var sharedDir))
            {
                Debug.LogWarning($"SharedPrefabOutput未配置, 无法复用prefab:{requester.name}");
                return false;
            }

            var expectedPrefabPath = Path.Combine(sharedDir, $"{key}.prefab").Replace("\\", "/");
            if (File.Exists(expectedPrefabPath))
            {
                AssetDatabase.ImportAsset(expectedPrefabPath);
                prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(expectedPrefabPath);
                if (prefabAsset != null)
                {
                    sharedPrefabAssets[key] = prefabAsset;
                    return true;
                }

                Debug.LogWarning($"引用prefab文件存在但无法加载: {expectedPrefabPath}");
                return false;
            }

            if (sharedPrefabExportInProgress.Contains(key))
            {
                Debug.LogWarning($"检测到循环引用prefab, 已跳过导出: {key}");
                return false;
            }

            sharedPrefabExportInProgress.Add(key);
            try
            {
                prefabAsset = ExportLayerTreeAsPrefab(requester, expectedPrefabPath, key, ignoreRootPrefabReference: true);
                if (prefabAsset != null)
                {
                    sharedPrefabAssets[key] = prefabAsset;
                    return true;
                }
                return false;
            }
            finally
            {
                sharedPrefabExportInProgress.Remove(key);
            }
        }

        private GameObject ExportLayerTreeAsPrefab(PsdLayerNode targetLogic, string prefabAssetPath, string prefabRootName, bool ignoreRootPrefabReference)
        {
            if (targetLogic == null) return null;
            if (string.IsNullOrWhiteSpace(prefabAssetPath) || string.IsNullOrWhiteSpace(prefabRootName)) return null;

            Transform exportRoot = targetLogic.transform;
            var layerNodes = exportRoot.GetComponentsInChildren<PsdLayerNode>(true);
            var prefabRefRoots = new List<PsdLayerNode>();
            var rootLayerNode = exportRoot.GetComponent<PsdLayerNode>();
            var prefabRefCandidates = layerNodes.Where(node => node != null && node.HasReusePrefabReference).ToArray();
            if (!ignoreRootPrefabReference && rootLayerNode != null && rootLayerNode.HasReusePrefabReference)
            {
                prefabRefRoots.Add(rootLayerNode);
            }
            else if (prefabRefCandidates.Length > 0)
            {
                foreach (var node in prefabRefCandidates)
                {
                    if (node == null) continue;
                    if (ignoreRootPrefabReference && node == rootLayerNode) continue;
                    bool hasPrefabAncestor = false;
                    var parent = node.transform.parent;
                    while (parent != null && parent != exportRoot)
                    {
                        var parentNode = parent.GetComponent<PsdLayerNode>();
                        if (parentNode != null && parentNode.HasReusePrefabReference)
                        {
                            if (!ignoreRootPrefabReference || parentNode != rootLayerNode)
                            {
                                hasPrefabAncestor = true;
                                break;
                            }
                        }
                        parent = parent.parent;
                    }
                    if (!hasPrefabAncestor)
                    {
                        prefabRefRoots.Add(node);
                    }
                }
            }

            bool IsInsidePrefabRef(Transform transform)
            {
                if (transform == null || prefabRefRoots.Count == 0) return false;
                foreach (var prefabRef in prefabRefRoots)
                {
                    if (prefabRef == null) continue;
                    if (transform == prefabRef.transform || transform.IsChildOf(prefabRef.transform)) return true;
                }
                return false;
            }

            var nodesForBuild = layerNodes.Where(node => node != null && !IsInsidePrefabRef(node.transform)).ToArray();
            ResolveLayerNameLookup();
            referencedLayerKeys.Clear();
            foreach (var node in nodesForBuild)
            {
                if (!node.HasReuseReference || string.IsNullOrEmpty(node.ReuseTargetKey)) continue;
                referencedLayerKeys.Add(node.ReuseTargetKey);
            }
            ExportSharedReferenceSprites(nodesForBuild);

            var uiHelpers = GetAvailableUIHelpers(exportRoot);
            if (uiHelpers != null && uiHelpers.Length > 0 && prefabRefRoots.Count > 0)
            {
                for (int i = uiHelpers.Length - 1; i >= 0; i--)
                {
                    var helper = uiHelpers[i];
                    if (helper == null || helper.LayerNode == null) continue;
                    if (IsInsidePrefabRef(helper.transform))
                    {
                        ArrayUtility.RemoveAt(ref uiHelpers, i);
                    }
                }
            }
            if ((uiHelpers == null || uiHelpers.Length < 1) && prefabRefRoots.Count < 1)
            {
                Debug.LogWarning($"导出prefab失败: 未找到可生成的UI节点:{targetLogic.name}");
                return null;
            }

            GameObject prefabRoot = null;
            var existingPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
            var rootHelper = exportRoot.GetComponent<UIHelperBase>();
            bool splitTmpTextRequiresContainerRoot = RequiresContainerRootForSplitTMPText(rootHelper);
            bool useTargetAsPrefabRoot = rootHelper != null
                && rootHelper.LayerNode != null
                && rootHelper.LayerNode.IsMainUIType
                && rootHelper.LayerNode.UIType != GUIType.Null
                && !splitTmpTextRequiresContainerRoot;
            try
            {
                if (existingPrefabAsset != null)
                {
                    prefabRoot = GameObject.Instantiate(existingPrefabAsset);
                    RestoreGeneratedKeysFromMetadata(prefabAssetPath, prefabRoot);
                }

                Vector3 rootWorldOffset = Vector3.zero;
                if (useTargetAsPrefabRoot)
                {
                    if (prefabRoot != null)
                    {
                        if (IsLikelyMatchingGeneratedRoot(prefabRoot, rootHelper.LayerNode.UIType))
                        {
                            prefabRoot = rootHelper.CreateUI(prefabRoot);
                        }
                        else
                        {
                            DestroyImmediate(prefabRoot);
                            prefabRoot = null;
                        }
                    }
                    if (prefabRoot == null)
                    {
                        prefabRoot = rootHelper.CreateUI();
                    }
                    if (prefabRoot == null)
                    {
                        useTargetAsPrefabRoot = false;
                    }
                }
                if (!useTargetAsPrefabRoot)
                {
                    bool canReuseContainerRoot = prefabRoot != null
                        && prefabRoot.GetComponent<RectTransform>() != null
                        && GetPrimaryRootComponentType(prefabRoot) == null;
                    if (!canReuseContainerRoot)
                    {
                        if (prefabRoot != null)
                        {
                            DestroyImmediate(prefabRoot);
                        }
                        prefabRoot = new GameObject(prefabRootName, typeof(RectTransform));
                    }

                    prefabRoot.name = prefabRootName;
                    prefabRoot.layer = UnityEngine.LayerMask.NameToLayer("UI");
                    prefabRoot.transform.localPosition = Vector3.zero;
                    prefabRoot.transform.localRotation = Quaternion.identity;
                    prefabRoot.transform.localScale = Vector3.one;
                    UGUIParser.SetRectTransform(targetLogic, prefabRoot.transform);
                }
                else
                {
                    prefabRoot.name = prefabRootName;
                    prefabRoot.layer = UnityEngine.LayerMask.NameToLayer("UI");
                    rootWorldOffset = prefabRoot.transform.position;
                    prefabRoot.transform.localPosition = Vector3.zero;
                    prefabRoot.transform.localRotation = Quaternion.identity;
                    prefabRoot.transform.localScale = Vector3.one;
                }

                var activeGeneratedKeys = new HashSet<string>(StringComparer.Ordinal);
                int curIdx = 0;
                int totalCount = uiHelpers.Length;
                foreach (var uiHelper in uiHelpers)
                {
                    if (uiHelper == null || uiHelper.LayerNode == null) continue;
                    if (useTargetAsPrefabRoot && uiHelper == rootHelper) continue;

                    EditorUtility.DisplayProgressBar($"生成prefab:({curIdx++}/{totalCount})", $"正在生成UI元素:{uiHelper.name}", curIdx / (float)totalCount);
                    var elementPathKey = GetGameObjectHierarchyPathKey(uiHelper.gameObject, exportRoot);
                    if (string.IsNullOrEmpty(elementPathKey) && uiHelper.transform == exportRoot)
                    {
                        elementPathKey = GetRootGeneratedElementKey(uiHelper.LayerNode);
                    }
                    var goPath = GetGameObjectHierarchyParentPath(uiHelper.gameObject, exportRoot, out var goNames);
                    var parentNode = GetOrCreateNodeByHierarchyPath(prefabRoot, goPath, goNames, activeGeneratedKeys);
                    var reusableNode = TryGetReusableGeneratedNode(parentNode.transform, uiHelper, elementPathKey);
                    var uiElement = uiHelper.CreateUI(reusableNode);
                    if (uiElement == null) continue;

                    CopyBindingMarker(uiHelper.gameObject, uiElement);
                    SetGeneratedNodeKey(uiElement, elementPathKey, uiHelper.LayerNode.UIType.ToString(), false, activeGeneratedKeys);
                    uiElement.transform.SetParent(parentNode.transform, true);
                    uiElement.transform.SetAsLastSibling(); // 确保 UGUI 层级顺序与 PSD 图层顺序一致
                    if (useTargetAsPrefabRoot)
                    {
                        uiElement.transform.position -= rootWorldOffset;
                    }
                    uiElement.transform.localScale = Vector3.one;
                    RefreshSplitTMPTextEffectsAfterParenting(uiHelper, uiElement);
                }

                if (prefabRefRoots.Count > 0)
                {
                    int prefabIdx = 0;
                    int prefabTotal = prefabRefRoots.Count;
                    foreach (var prefabRefNode in prefabRefRoots)
                    {
                        if (prefabRefNode == null || !prefabRefNode.HasReusePrefabReference) continue;

                        EditorUtility.DisplayProgressBar($"生成prefab-引用prefab:({prefabIdx++}/{prefabTotal})", $"正在实例化prefab:{prefabRefNode.ReusePrefabDisplayName}", prefabIdx / (float)prefabTotal);
                        if (!TryGetOrCreateSharedPrefabAsset(prefabRefNode, out var prefabAsset) || prefabAsset == null)
                        {
                            Debug.LogWarning($"引用prefab未找到且导出失败: {prefabRefNode.ReusePrefabDisplayName}");
                            continue;
                        }

                        var pathKey = GetGameObjectHierarchyPathKey(prefabRefNode.gameObject, exportRoot);
                        var goPath = GetGameObjectHierarchyParentPath(prefabRefNode.gameObject, exportRoot, out var goNames);
                        var parentNode = GetOrCreateNodeByHierarchyPath(prefabRoot, goPath, goNames, activeGeneratedKeys);
                        var prefabInstance = TryGetReusableGeneratedPrefabNode(parentNode.transform, prefabRefNode, pathKey, prefabAsset);
                        if (prefabInstance == null)
                        {
                            prefabInstance = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
                            if (prefabInstance == null)
                            {
                                prefabInstance = GameObject.Instantiate(prefabAsset);
                            }
                        }
                        prefabInstance.name = string.IsNullOrEmpty(prefabRefNode.ReusePrefabKey) ? prefabAsset.name : prefabRefNode.ReusePrefabKey;
                        prefabInstance.transform.localRotation = Quaternion.identity;
                        prefabInstance.transform.localScale = Vector3.one;
                        var prefabRect = prefabInstance.GetComponent<RectTransform>();
                        if (prefabRect != null)
                        {
                            UGUIParser.SetRectTransform(prefabRefNode, prefabRect);
                        }
                        else
                        {
                            Debug.LogWarning($"引用prefab缺少RectTransform: {prefabAsset.name}");
                        }

                        if (prefabRefNode.GetComponent<PsdLayerMarker>() != null)
                            CopyBindingMarker(prefabRefNode.gameObject, prefabInstance);
                        SetGeneratedNodeKey(prefabInstance, pathKey, GeneratedPrefabReferenceTypeKey, false, activeGeneratedKeys);
                        prefabInstance.transform.SetParent(parentNode.transform, true);
                        prefabInstance.transform.SetAsLastSibling(); // 确保 UGUI 层级顺序与 PSD 图层顺序一致
                        if (useTargetAsPrefabRoot)
                        {
                            prefabInstance.transform.position -= rootWorldOffset;
                        }
                        prefabInstance.transform.localScale = Vector3.one;
                    }
                }

                CleanupLegacyUiStringKeys(prefabRoot);
                CleanupStaleGeneratedNodes(prefabRoot.transform, activeGeneratedKeys);
                NormalizeGeneratedContainerRects(prefabRoot);
                var generatedNodeSnapshots = CaptureGeneratedNodeSnapshots(prefabRoot);
                CleanupTransientGeneratedKeys(prefabRoot);

                var uiPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(prefabRoot, prefabAssetPath, InteractionMode.UserAction);
                if (uiPrefab != null)
                {
                    SaveGeneratedKeysMetadata(prefabAssetPath, generatedNodeSnapshots);
                }
                return uiPrefab;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (prefabRoot != null)
                {
                    DestroyImmediate(prefabRoot);
                }
            }
        }
        internal string GetOrCreateSharedSprite(PsdLayerNode requester)
        {
            if (requester == null || string.IsNullOrEmpty(requester.ReuseTargetKey))
            {
                return null;
            }

            Debug.Log($"[PSD2UIForm:RefSprite] requester={requester.name}, targetKey={requester.ReuseTargetKey}, display={requester.ReuseTargetDisplayName}, imageDir={GetUIFormImagesOutputDir()}");
            if (sharedSpriteAssets.TryGetValue(requester.ReuseTargetKey, out var cachedPath) && File.Exists(cachedPath))
            {
                EnsureSpriteAssetReady(cachedPath);
                Debug.Log($"[PSD2UIForm:RefSprite] 命中缓存: {requester.ReuseTargetKey} -> {cachedPath}");
                return cachedPath.Replace("\\", "/");
            }

            if (TryGetLocalSpriteAssetPath(requester.ReuseTargetKey, out var localPath))
            {
                RegisterSharedSprite(requester.ReuseTargetKey, localPath);
                Debug.Log($"[PSD2UIForm:RefSprite] 命中当前界面本地资源: {requester.ReuseTargetKey} -> {localPath}");
                return localPath;
            }

            if (!TryEnsureSharedOutputDirectory(out var sharedDir))
            {
                Debug.LogWarning($"SharedAssetsOutput未配置, 无法复用图层:{requester.name}");
                return null;
            }
            var sharedAssetPath = Path.Combine(sharedDir, requester.ReuseTargetKey + ".png").Replace("\\", "/");
            if (File.Exists(sharedAssetPath))
            {
                EnsureSpriteAssetReady(sharedAssetPath);
                RegisterSharedSprite(requester.ReuseTargetKey, sharedAssetPath);
                Debug.Log($"[PSD2UIForm:RefSprite] 命中Common资源: {requester.ReuseTargetKey} -> {sharedAssetPath}");
                return sharedAssetPath;
            }

            ResolveLayerNameLookup();
            if (!layerLookupByName.TryGetValue(requester.ReuseTargetKey, out var targetNode) || targetNode == null)
            {
                Debug.LogWarning($"[PSD2UIForm:RefSprite] 未找到目标节点，尝试从引用节点自身导出: requester={requester.name}, targetKey={requester.ReuseTargetKey}");
                var exportedPath = requester.ExportImageAsset(true, sharedDir, requester.ReuseTargetKey, true, ignoreReference: true);
                if (!string.IsNullOrEmpty(exportedPath))
                {
                    RegisterSharedSprite(requester.ReuseTargetKey, exportedPath);
                    Debug.Log($"[PSD2UIForm:RefSprite] 引用节点自身导出成功: {requester.ReuseTargetKey} -> {exportedPath}");
                    return exportedPath;
                }
                Debug.LogWarning($"引用图片未找到且导出失败: {requester.name} -> {requester.ReuseTargetDisplayName}");
                return null;
            }
            else
            {
                var exportedPath = targetNode.ExportImageAsset(true, sharedDir, requester.ReuseTargetKey, true);
                if (!string.IsNullOrEmpty(exportedPath))
                {
                    RegisterSharedSprite(requester.ReuseTargetKey, exportedPath);
                    Debug.Log($"[PSD2UIForm:RefSprite] 从目标节点导出成功: targetNode={targetNode.name}, key={requester.ReuseTargetKey}, path={exportedPath}");
                    return exportedPath;
                }
            }
            Debug.LogWarning($"[PSD2UIForm:RefSprite] 复用图片解析失败: requester={requester.name}, key={requester.ReuseTargetKey}");
            return null;
        }

        private bool TryGetLocalSpriteAssetPath(string key, out string assetPath)
        {
            assetPath = null;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            var outputDir = NormalizeAssetDirectory(GetUIFormImagesOutputDir());
            if (string.IsNullOrWhiteSpace(outputDir))
                return false;

            var candidate = Path.Combine(outputDir, key + ".png").Replace("\\", "/");
            if (!File.Exists(candidate))
            {
                Debug.Log($"[PSD2UIForm:RefSprite] 当前界面本地资源不存在: key={key}, path={candidate}");
                return false;
            }

            EnsureSpriteAssetReady(candidate);
            assetPath = candidate;
            return true;
        }

        internal bool TryGetSharedSpritePath(PsdLayerNode node, bool forceSpriteType, out string assetPath)
        {
            assetPath = null;
            if (node == null) return false;
            var key = node.LayerNameLookupKey;
            if (string.IsNullOrEmpty(key)) return false;
            if (sharedSpriteAssets.TryGetValue(key, out var cachedPath))
            {
                if (File.Exists(cachedPath))
                {
                    assetPath = cachedPath.Replace("\\", "/");
                    if (forceSpriteType)
                    {
                        EnsureSpriteAssetReady(assetPath);
                    }
                    return true;
                }
                sharedSpriteAssets.Remove(key);
            }
            return false;
        }
        internal bool TryOverrideSharedOutput(PsdLayerNode node, out string exportDir, out string fileName)
        {
            exportDir = null;
            fileName = null;
            ResolveLayerNameLookup();
            if (node == null) return false;
            var key = node.LayerNameLookupKey;
            if (string.IsNullOrEmpty(key) || !referencedLayerKeys.Contains(key)) return false;
            if (!TryEnsureSharedOutputDirectory(out var sharedDir)) return false;
            exportDir = sharedDir;
            fileName = key;
            return true;
        }
        internal void RegisterSharedSprite(string key, string assetPath)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(assetPath)) return;
            sharedSpriteAssets[key] = assetPath.Replace("\\", "/");
        }
        internal PsdLayerNode FindLayerNodeByKey(string key)
        {
            ResolveLayerNameLookup();
            if (string.IsNullOrEmpty(key)) return null;
            layerLookupByName.TryGetValue(key, out var node);
            return node;
        }
        private UIHelperBase[] GetAvailableUIHelpers(Transform root, bool includeInactiveRoot = false)
        {
            var uiHelpers = root.GetComponentsInChildren<UIHelperBase>(includeInactiveRoot);
            uiHelpers = uiHelpers
                .Where(ui => ui != null
                    && (!includeInactiveRoot || IsActiveRelativeToExportRoot(ui.transform, root))
                    && ui.LayerNode != null && ui.LayerNode.IsMainUIType &&
                    !IsStructuralContainerNode(ui.LayerNode) && !IsPlainShapeButtonNode(ui.LayerNode))
                .ToArray();

            // AI may have changed ownership or node types since the helper was first
            // attached. Refresh dependencies before deciding which helpers generate.
            foreach (var uiHelper in uiHelpers)
            {
                uiHelper?.ParseAndAttachUIElements();
            }

            var collapsedRoots = root.GetComponentsInChildren<PsdLayerNode>(true)
                .Where(node => IsValidCollapsedGenerationRoot(node))
                .Select(node => node.transform)
                .ToArray();

            var dependInstIds = new HashSet<int>();
            foreach (var item in uiHelpers)
            {
                var dependencies = item.GetDependencies();
                if (dependencies == null) continue;

                foreach (var depend in dependencies)
                {
                    if (depend == null) continue;

                    int dependId = depend.gameObject.GetInstanceID();
                    dependInstIds.Add(dependId);
                }
            }
            for (int i = uiHelpers.Length - 1; i >= 0; i--)
            {
                var uiHelper = uiHelpers[i];
                if (dependInstIds.Contains(uiHelper.gameObject.GetInstanceID())
                    || IsChildOfCollapsedRoot(uiHelper.transform, collapsedRoots))
                {
                    ArrayUtility.RemoveAt(ref uiHelpers, i);
                }
            }
            return uiHelpers;
        }

        private static bool IsActiveRelativeToExportRoot(Transform current, Transform root)
        {
            if (current == null || root == null) return false;
            for (var cursor = current; cursor != null; cursor = cursor.parent)
            {
                if (cursor == root) return true;
                if (!cursor.gameObject.activeSelf) return false;
            }
            return false;
        }
        private static bool IsValidCollapsedGenerationRoot(PsdLayerNode node)
        {
            if (node == null || !node.CollapseChildrenForGeneration || node.LayerType != PsdLayerType.LayerGroup)
            {
                return false;
            }

            // Null/Panel wrappers cannot render their descendants themselves. A
            // stale collapse flag on such a wrapper would otherwise erase the
            // entire visual subtree from the generated prefab.
            if (IsGroupImageSourceType(node.UIType) || IsGroupImageSourceSemanticType(node.UIType))
            {
                return true;
            }

            return (IsGroupTextSourceType(node.UIType) || IsGroupTextSourceSemanticType(node.UIType))
                && FindGroupTextSource(node) != null;
        }
        private static bool IsChildOfCollapsedRoot(Transform target, Transform[] collapsedRoots)
        {
            if (target == null || collapsedRoots == null || collapsedRoots.Length == 0) return false;
            foreach (var collapsedRoot in collapsedRoots)
            {
                if (collapsedRoot != null && target != collapsedRoot && target.IsChildOf(collapsedRoot))
                {
                    return true;
                }
            }
            return false;
        }
        private static bool IsStructuralContainerNode(PsdLayerNode node)
        {
            if (node == null || node.LayerType != PsdLayerType.LayerGroup || node.transform.childCount == 0)
            {
                return false;
            }

            var uiTypeName = node.UIType.ToString();
            if (node.UIType != GUIType.Image && node.UIType != GUIType.Button && !uiTypeName.EndsWith("Button", StringComparison.Ordinal))
            {
                return false;
            }

            var semantic = (node.name ?? string.Empty).ToLowerInvariant();
            if (semantic.Contains("common_btn") || semantic.Contains("_btn_") || semantic.Contains("button") ||
                semantic.Contains(".bt") || semantic.Contains("按钮") || semantic.Contains("按鈕") ||
                semantic.Contains("slider") || semantic.Contains("progress") || semantic.Contains("toggle") ||
                semantic.Contains("input") || semantic.Contains("dropdown") || semantic.Contains("scrollview"))
            {
                return false;
            }

            var name = (node.name ?? string.Empty).ToLowerInvariant();
            return name.StartsWith("zu_", StringComparison.Ordinal) ||
                   name.StartsWith("group", StringComparison.Ordinal) ||
                   name.StartsWith("grp", StringComparison.Ordinal) ||
                   name.StartsWith("组", StringComparison.Ordinal) ||
                   name.StartsWith("图层组", StringComparison.Ordinal) ||
                   name.StartsWith("圖層組", StringComparison.Ordinal);
        }
        private static bool IsPlainShapeButtonNode(PsdLayerNode node)
        {
            if (node == null)
            {
                return false;
            }

            var uiTypeName = node.UIType.ToString();
            if (node.UIType != GUIType.Button && !uiTypeName.EndsWith("Button", StringComparison.Ordinal))
            {
                return false;
            }

            var name = (node.name ?? string.Empty).ToLowerInvariant();
            if (name.Contains("common_btn") || name.Contains("button") || name.Contains("confirm") ||
                name.Contains("cancel") || name.Contains("use") || name.Contains("buy") || name.Contains("sell"))
            {
                return false;
            }

            return name.Contains("yuanjiaojuxing") || name.Contains("圆角矩形") ||
                   name.Contains("rectangle") || name.Contains("rect");
        }
        /// <summary>
        /// 把图片设置为为Sprite或Texture类型
        /// </summary>
        /// <param name="dir"></param>
        public static void ConvertTexturesType(string[] texAssets, bool isImage = true)
        {
            foreach (var item in texAssets)
            {
                var texImporter = AssetImporter.GetAtPath(item) as TextureImporter;
                if (texImporter == null)
                {
                    Debug.LogError($"TextureImporter为空:{item}");
                    continue;
                }
                if (isImage)
                {
                    texImporter.textureType = TextureImporterType.Sprite;
                    texImporter.spriteImportMode = SpriteImportMode.Single;
                    texImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                    texImporter.alphaIsTransparency = true;
                    texImporter.mipmapEnabled = false;
                }
                else
                {
                    texImporter.textureType = TextureImporterType.Default;
                    texImporter.textureShape = TextureImporterShape.Texture2D;
                    texImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                    texImporter.alphaIsTransparency = true;
                    texImporter.mipmapEnabled = false;
                }

                texImporter.SaveAndReimport();
            }
        }
        internal static void ApplySpriteNineSlice(string spriteAssetPath)
        {
            var texImporter = AssetImporter.GetAtPath(spriteAssetPath) as TextureImporter;
            if (texImporter == null)
            {
                return;
            }
            bool wasReadable = texImporter.isReadable;
            bool needSave = false;

            // 如果不可读，先设置可读（但不立即重导入，等border设完一起提交）
            if (!wasReadable)
            {
                texImporter.isReadable = true;
                texImporter.SaveAndReimport();
                texImporter = AssetImporter.GetAtPath(spriteAssetPath) as TextureImporter;
                if (texImporter == null) return;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spriteAssetPath);
            if (sprite != null && texImporter.spriteBorder == Vector4.zero)
            {
                texImporter.spriteBorder = UGUIParser.CalculateTexture9SliceBorder(sprite.texture);
                needSave = true;
            }

            // 恢复 readable 状态 + 保存 border，合并为一次 SaveAndReimport
            if (!wasReadable)
            {
                texImporter.isReadable = false;
                needSave = true;
            }

            if (needSave)
            {
                texImporter.SaveAndReimport();
            }
        }

        /// <summary>
        /// 直接设置 Sprite 的九宫格 Border（用于裁剪后的图片，border已预计算）
        /// </summary>
        internal static void ApplySpriteNineSliceBorder(string spriteAssetPath, Vector4 border)
        {
            var texImporter = AssetImporter.GetAtPath(spriteAssetPath) as TextureImporter;
            if (texImporter == null) return;
            texImporter.spriteBorder = border;
            texImporter.SaveAndReimport();
        }

        /// <summary>
        /// 压缩图片文件
        /// </summary>
        /// <param name="asset">文件名(相对路径Assets)</param>
        /// <returns></returns>
        public static bool CompressImageFile(string asset)
        {
            var assetPath = asset.StartsWith("Assets/") ? PathExtensions.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, asset) : asset;
            var compressTool = Utility.Assembly.GetType("UGF.EditorTools.CompressTool");
            if (compressTool == null) return false;

            var compressMethod = compressTool.GetMethod("CompressImageOffline", new Type[] { typeof(string), typeof(string) });
            if (compressMethod == null) return false;

            return (bool)compressMethod.Invoke(null, new object[] { assetPath, assetPath });
        }
        /// <summary>
        /// 获取UIForm对应的图片导出目录
        /// </summary>
        /// <returns></returns>
        public string GetUIFormImagesOutputDir()
        {
            return Path.Combine(Psd2UIFormSettings.Instance.UIImagesOutputDir, uiFormName);
        }

    }
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    public static class PathExtensions
    {
        public static string GetRelativePath(string relativeTo, string path)
        {
#if UNITY_2021_1_OR_NEWER
            return Path.GetRelativePath(relativeTo, path);
#else
            relativeTo = Path.GetFullPath(relativeTo.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                                            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            path = Path.GetFullPath(path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
            Uri fromUri = new Uri(Path.GetFullPath(relativeTo));
            Uri toUri = new Uri(Path.GetFullPath(path));

            if (fromUri.Scheme != toUri.Scheme)
            {
                return path;
            }

            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            return relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
#endif
        }
    }
}
#endif





