// PSD Reader - Unity 集成层
// 提供 PSD → Unity Asset 的导入和场景重建功能
// 注意：此文件依赖 UnityEngine / UnityEditor API

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PsdReader.Unity
{
    #region 枚举

    /// <summary>文件命名约定</summary>
    public enum NamingConvention
    {
        LayerNameOnly,     // 仅图层名
        PathBased          // 包含路径前缀
    }

    /// <summary>分组模式</summary>
    public enum GroupMode
    {
        MergeGroups,       // 合并组图层为一张图
        FlattenAll         // 全部展平为独立图层
    }

    /// <summary>缩放系数</summary>
    public enum ScaleFactor
    {
        Full,      // 1x
        Half,      // 0.5x
        Quarter    // 0.25x
    }

    #endregion

    #region 导入数据结构

    /// <summary>
    /// 单个导入图层的配置数据，含树形结构（Childs）。
    /// 由 PsdImporter 从 PSD 图层树构建。
    /// </summary>
    [Serializable]
    public class ImportLayerData
    {
        public string name;
        public string path;
        public int[] indexId;
        public bool import = true;
        public bool useDefaults = true;
        public SpriteAlignment Alignment;
        public Vector2 Pivot = new Vector2(0.5f, 0.5f);
        public ScaleFactor ScaleFactor;
        public List<ImportLayerData> Childs = new List<ImportLayerData>();

        /// <summary>
        /// 深度优先遍历图层树。
        /// 逆序遍历子节点以匹配 Photoshop 的图层顺序（从底到顶）。
        /// </summary>
        public void Iterate(
            Action<ImportLayerData> layerCallback,
            Func<ImportLayerData, bool> canEnterGroup = null,
            Action<ImportLayerData> enterGroupCallback = null,
            Action<ImportLayerData> exitGroupCallback = null)
        {
            for (int i = Childs.Count - 1; i >= 0; i--)
            {
                var child = Childs[i];
                if (child == null) continue;

                layerCallback?.Invoke(child);

                if (child.Childs.Count > 0)
                {
                    bool enter = canEnterGroup?.Invoke(child) ?? true;
                    if (enter)
                    {
                        enterGroupCallback?.Invoke(child);
                        child.Iterate(layerCallback, canEnterGroup, enterGroupCallback, exitGroupCallback);
                        exitGroupCallback?.Invoke(child);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 用户导入设置 — 控制导入行为的全局参数。
    /// </summary>
    [Serializable]
    public class ImportUserData
    {
        public NamingConvention fileNaming;
        public GroupMode groupMode;
        public string PackingTag = "";
        public string TargetDirectory = "Assets/Sprites";
        public bool AutoImport;
        public SpriteAlignment DefaultAlignment;
        public Vector2 DefaultPivot = new Vector2(0.5f, 0.5f);
        public ScaleFactor ScaleFactor;
        public SpriteAlignment DocAlignment;
        public Vector2 DocPivot = new Vector2(0.5f, 0.5f);
        public ImportLayerData DocRoot;

        /// <summary>通过索引路径定位特定图层数据</summary>
        public ImportLayerData GetLayerData(int[] layerIdx)
        {
            if (DocRoot == null) return null;

            var current = DocRoot;
            for (int i = 0; i < layerIdx.Length; i++)
            {
                int index = layerIdx[i];
                if (index < 0 || index >= current.Childs.Count)
                    return null;
                current = current.Childs[index];
            }
            return current;
        }
    }

    #endregion

    #region 重建数据

    /// <summary>
    /// 重建阶段的运行时数据 — 包含 Sprite 引用、锚点和图层边界。
    /// </summary>
    public struct ReconstructData
    {
        public Dictionary<int[], Sprite> spriteIndex;
        public Dictionary<int[], Vector2> spriteAnchors;
        public Dictionary<int[], Rect> layerBoundsIndex;
        public Vector2 documentSize;
        public Vector2 documentPivot;
        public float documentPPU;

        public ReconstructData(Vector2 docSize, Vector2 docPivot, float ppu)
        {
            spriteIndex = new Dictionary<int[], Sprite>();
            spriteAnchors = new Dictionary<int[], Vector2>();
            layerBoundsIndex = new Dictionary<int[], Rect>();
            documentSize = docSize;
            documentPivot = docPivot;
            documentPPU = ppu;
        }

        public void AddSprite(int[] layerIdx, Sprite sprite, Vector2 anchor)
        {
            spriteIndex[layerIdx] = sprite;
            spriteAnchors[layerIdx] = anchor;
        }
    }

    #endregion

    #region 重建器接口

    /// <summary>
    /// 场景重建器接口 — 将导入数据转化为 Unity 场景中的 GameObject 层级。
    /// </summary>
    public interface IReconstructor
    {
        string DisplayName { get; }
        string HelpMessage { get; }
        bool CanReconstruct(GameObject selection);
        GameObject Reconstruct(ImportLayerData root, ReconstructData data, GameObject selection);
    }

    #endregion

    #region Sprite 重建器

    /// <summary>
    /// SpriteRenderer 重建器 — 将 PSD 图层重建为 SpriteRenderer 为基础的 GameObject 树。
    /// 适用于 2D 游戏场景。
    /// </summary>
    public class SpriteReconstructor : IReconstructor
    {
        public string DisplayName => "Sprite Renderer";
        public string HelpMessage => "";

        public bool CanReconstruct(GameObject selection) => true;

        public GameObject Reconstruct(ImportLayerData root, ReconstructData data, GameObject selection)
        {
            var rootGo = selection ?? new GameObject("PSD_Root");
            var hierarchy = new Stack<Transform>();
            hierarchy.Push(rootGo.transform);

            int sortIdx = CountLeafLayers(root);
            var docRoot = data.documentSize * data.documentPivot;

            root.Iterate(
                layerCallback: layer =>
                {
                    // 叶节点：创建 SpriteRenderer
                    if (layer.Childs.Count > 0 || !layer.import) return;

                    var go = new GameObject(layer.name);
                    var t = go.transform;
                    t.SetParent(hierarchy.Peek());
                    t.SetAsLastSibling();

                    if (data.spriteIndex.TryGetValue(layer.indexId, out var sprite))
                    {
                        var sr = go.AddComponent<SpriteRenderer>();
                        sr.sprite = sprite;
                        sr.sortingOrder = sortIdx;
                        sortIdx--;
                    }

                    var pos = GetLayerPosition(data, layer.indexId) - docRoot;
                    pos /= data.documentPPU;
                    t.position = (Vector3)(Vector2)pos;
                },
                enterGroupCallback: layer =>
                {
                    var groupGo = new GameObject(layer.name);
                    groupGo.transform.SetParent(hierarchy.Peek());
                    hierarchy.Push(groupGo.transform);
                },
                exitGroupCallback: _ => hierarchy.Pop()
            );

            return rootGo;
        }

        private static Vector2 GetLayerPosition(ReconstructData data, int[] layerIdx)
        {
            if (!data.layerBoundsIndex.TryGetValue(layerIdx, out var bounds))
                return Vector2.zero;

            Vector2 anchor = Vector2.zero;
            if (data.spriteAnchors.TryGetValue(layerIdx, out var a))
                anchor = a;

            return new Vector2(
                bounds.xMin + bounds.width * anchor.x,
                bounds.yMin + bounds.height * anchor.y
            );
        }

        private static int CountLeafLayers(ImportLayerData root)
        {
            int count = 0;
            root.Iterate(layer => { if (layer.Childs.Count == 0 && layer.import) count++; });
            return count;
        }
    }

    #endregion

    #region UGUI 重建器

    /// <summary>
    /// UGUI 重建器 — 将 PSD 图层重建为 RectTransform + Image 的 UI 层级。
    /// 适用于 Unity UI (Canvas) 系统。
    /// </summary>
    public class UnityGuiReconstructor : IReconstructor
    {
        public string DisplayName => "Unity GUI (UGUI)";
        public string HelpMessage => "请选择一个带 Canvas 的 GameObject。";

        public bool CanReconstruct(GameObject selection)
        {
            return selection != null && selection.GetComponentInParent<Canvas>() != null;
        }

        public GameObject Reconstruct(ImportLayerData root, ReconstructData data, GameObject selection)
        {
            var hierarchy = new Stack<RectTransform>();
            var rootT = CreateRectObject("PSD_Root");
            rootT.SetParent(selection.transform);
            rootT.localScale = Vector3.one;
            rootT.anchoredPosition = Vector2.zero;
            hierarchy.Push(rootT);

            var docRoot = data.documentSize * data.documentPivot;

            root.Iterate(
                layerCallback: layer =>
                {
                    if (layer.Childs.Count > 0 || !layer.import) return;

                    var rt = CreateRectObject(layer.name);
                    rt.SetParent(hierarchy.Peek());
                    rt.SetAsFirstSibling();

                    if (data.spriteIndex.TryGetValue(layer.indexId, out var sprite))
                    {
                        rt.gameObject.AddComponent<UnityEngine.UI.Image>().sprite = sprite;
                    }

                    if (!data.layerBoundsIndex.TryGetValue(layer.indexId, out var bounds))
                        bounds = Rect.zero;

                    Vector2 anchor = Vector2.zero;
                    if (data.spriteAnchors.TryGetValue(layer.indexId, out var a))
                        anchor = a;

                    var pos = GetLayerPosition(bounds, anchor) - docRoot;
                    rt.position = rootT.TransformPoint(pos.x, pos.y, 0f);
                    rt.pivot = anchor;
                    rt.sizeDelta = new Vector2(bounds.width, bounds.height);
                },
                enterGroupCallback: layer =>
                {
                    var groupRt = CreateRectObject(layer.name);
                    groupRt.SetParent(hierarchy.Peek());
                    groupRt.SetAsFirstSibling();
                    groupRt.anchorMin = Vector2.zero;
                    groupRt.offsetMin = Vector2.zero;
                    hierarchy.Push(groupRt);
                },
                exitGroupCallback: _ => hierarchy.Pop()
            );

            return rootT.gameObject;
        }

        private static RectTransform CreateRectObject(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            return go.GetComponent<RectTransform>();
        }

        private static Vector2 GetLayerPosition(Rect bounds, Vector2 anchor)
        {
            return new Vector2(
                bounds.xMin + bounds.width * anchor.x,
                bounds.yMin + bounds.height * anchor.y
            );
        }
    }

    #endregion

    #region PSD 导入器

    /// <summary>
    /// PSD 导入主逻辑 — 将 PSD 文件解析并导出为 Unity 可用的 Sprite 资源。
    /// </summary>
    public static class PsdImporter
    {
        /// <summary>
        /// 将 PSD 文档的指定图层导出为 PNG 精灵图。
        /// </summary>
        public static Sprite ImportLayer(
            PsdDocument psdDoc,
            ImportUserData settings,
            ImportLayerData layerData,
            TextureImporterSettings texSettings = null)
        {
            // 查找对应的 PSD 图层
            var psdLayer = GetPsdLayerByIndex(psdDoc, layerData.indexId);
            if (psdLayer == null) return null;

            // 渲染图层为像素
            var rendered = PsdRenderer.Render(psdLayer);
            if (rendered == null || rendered.Width == 0 || rendered.Height == 0) return null;

            // 构建输出路径
            string filePath = GetFilePath(psdLayer, settings);
            string dirPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            // 创建 Texture2D 并保存 PNG
            var tex = new Texture2D(rendered.Width, rendered.Height, TextureFormat.RGBA32, false);
            tex.SetPixels32(PixelsToColor32(rendered.Pixels));
            tex.Apply();

            byte[] pngData = tex.EncodeToPNG();
            File.WriteAllBytes(filePath, pngData);
            UnityEngine.Object.DestroyImmediate(tex);

            // 刷新资源并设置导入参数
            AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 100f;

                if (!string.IsNullOrEmpty(settings.PackingTag))
                    importer.spritePackingTag = settings.PackingTag;

                // 应用缩放因子
                if (layerData.ScaleFactor == ScaleFactor.Half)
                    importer.maxTextureSize = Mathf.Max(rendered.Width, rendered.Height) / 2;
                else if (layerData.ScaleFactor == ScaleFactor.Quarter)
                    importer.maxTextureSize = Mathf.Max(rendered.Width, rendered.Height) / 4;

                // 设置锚点
                if (!layerData.useDefaults)
                {
                    var importerSettings = new TextureImporterSettings();
                    importer.ReadTextureSettings(importerSettings);
                    importerSettings.spriteAlignment = (int)layerData.Alignment;
                    if (layerData.Alignment == SpriteAlignment.Custom)
                        importerSettings.spritePivot = layerData.Pivot;
                    importer.SetTextureSettings(importerSettings);
                }

                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(filePath);
        }

        /// <summary>
        /// 构建图层列表（生成 ImportLayerData 树）
        /// </summary>
        public static ImportLayerData BuildImportLayerTree(PsdDocument doc)
        {
            var root = new ImportLayerData
            {
                name = "Root",
                indexId = new int[0],
                Childs = new List<ImportLayerData>()
            };

            BuildLayerTreeRecursive(doc.Childs, root, new List<int>());
            return root;
        }

        private static void BuildLayerTreeRecursive(
            PsdLayer[] layers,
            ImportLayerData parent,
            List<int> currentPath)
        {
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                var path = new List<int>(currentPath) { i };

                var data = new ImportLayerData
                {
                    name = layer.Name,
                    path = string.Join("/", path),
                    indexId = path.ToArray(),
                    import = layer.IsVisible,
                    Childs = new List<ImportLayerData>()
                };

                parent.Childs.Add(data);

                if (layer.Childs != null && layer.Childs.Length > 0)
                {
                    BuildLayerTreeRecursive(layer.Childs, data, path);
                }
            }
        }

        /// <summary>
        /// 准备重建数据 — 导入所有选中图层的 sprite 并收集边界信息
        /// </summary>
        public static ReconstructData GetReconstructData(
            PsdDocument doc,
            ImportUserData settings,
            TextureImporterSettings texSettings = null)
        {
            var data = new ReconstructData(
                new Vector2(doc.Width, doc.Height),
                settings.DocPivot,
                100f
            );

            settings.DocRoot?.Iterate(layer =>
            {
                if (!layer.import) return;

                var psdLayer = GetPsdLayerByIndex(doc, layer.indexId);
                if (psdLayer == null) return;

                // 计算图层边界（Y轴翻转: PSD 原点在左上，Unity 在左下）
                var bounds = new Rect(
                    psdLayer.Left,
                    doc.Height - psdLayer.Bottom,
                    psdLayer.Right - psdLayer.Left,
                    psdLayer.Bottom - psdLayer.Top
                );
                data.layerBoundsIndex[layer.indexId] = bounds;

                // 导入 Sprite
                string filePath = GetFilePath(psdLayer, settings);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(filePath);
                if (sprite == null)
                    sprite = ImportLayer(doc, settings, layer, texSettings);

                if (sprite != null)
                {
                    Vector2 anchor = GetAnchor(filePath, settings, layer);
                    data.AddSprite(layer.indexId, sprite, anchor);
                }
            });

            return data;
        }

        #region 内部辅助

        internal static PsdLayer GetPsdLayerByIndex(PsdDocument doc, int[] indexPath)
        {
            if (indexPath == null || indexPath.Length == 0)
                return null;

            PsdLayer[] layers = doc.Childs;
            PsdLayer current = null;

            for (int i = 0; i < indexPath.Length; i++)
            {
                int idx = indexPath[i];
                if (idx < 0 || idx >= layers.Length)
                    return null;
                current = layers[idx];
                if (i < indexPath.Length - 1)
                    layers = current.Childs;
            }

            return current;
        }

        private static string GetFilePath(PsdLayer layer, ImportUserData settings)
        {
            string dir = settings.TargetDirectory ?? "Assets/Sprites";
            string fileName;

            if (settings.fileNaming == NamingConvention.PathBased)
            {
                // 用父级路径作为前缀
                var parts = new List<string>();
                var current = layer;
                while (current != null)
                {
                    parts.Insert(0, SanitizeFileName(current.Name));
                    current = current.Parent;
                }
                fileName = string.Join("_", parts);
            }
            else
            {
                fileName = SanitizeFileName(layer.Name);
            }

            return Path.Combine(dir, fileName + ".png");
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unnamed";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private static Vector2 GetAnchor(string filePath, ImportUserData settings, ImportLayerData layer)
        {
            var importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
            if (importer != null)
            {
                var texSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(texSettings);
                if (texSettings.spriteAlignment == (int)SpriteAlignment.Custom)
                    return texSettings.spritePivot;
                return AlignmentToPivot((SpriteAlignment)texSettings.spriteAlignment);
            }
            return settings.DefaultPivot;
        }

        internal static Vector2 AlignmentToPivot(SpriteAlignment alignment)
        {
            switch (alignment)
            {
                case SpriteAlignment.TopLeft: return new Vector2(0, 1);
                case SpriteAlignment.TopCenter: return new Vector2(0.5f, 1);
                case SpriteAlignment.TopRight: return new Vector2(1, 1);
                case SpriteAlignment.LeftCenter: return new Vector2(0, 0.5f);
                case SpriteAlignment.Center: return new Vector2(0.5f, 0.5f);
                case SpriteAlignment.RightCenter: return new Vector2(1, 0.5f);
                case SpriteAlignment.BottomLeft: return new Vector2(0, 0);
                case SpriteAlignment.BottomCenter: return new Vector2(0.5f, 0);
                case SpriteAlignment.BottomRight: return new Vector2(1, 0);
                default: return new Vector2(0.5f, 0.5f);
            }
        }

        private static Color32[] PixelsToColor32(byte[] rgba)
        {
            int pixelCount = rgba.Length / 4;
            var colors = new Color32[pixelCount];
            for (int i = 0; i < pixelCount; i++)
            {
                int offset = i * 4;
                colors[i] = new Color32(rgba[offset], rgba[offset + 1], rgba[offset + 2], rgba[offset + 3]);
            }
            return colors;
        }

        #endregion
    }

    #endregion

    #region 编辑器协程

    /// <summary>
    /// 编辑器协程运行器 — 在 Editor 模式下模拟协程，提供进度条显示。
    /// </summary>
    public static class EditorCoroutineRunner
    {
        private static readonly List<CoroutineState> _coroutines = new List<CoroutineState>();
        private static readonly List<CoroutineState> _finished = new List<CoroutineState>();
        private static CoroutineState _uiCoroutine;

        private class CoroutineState
        {
            public IEnumerator Enumerator;
            public string Title;
            public bool HasUI;
            public bool IsCancelable;
            public string Label;
            public float Progress;
        }

        [MenuItem("Window/PSDReader/Force Reset")]
        public static void KillAllCoroutines()
        {
            EditorUtility.ClearProgressBar();
            _uiCoroutine = null;
            _coroutines.Clear();
            _finished.Clear();
        }

        public static void StartCoroutine(IEnumerator coroutine)
        {
            Register(new CoroutineState { Enumerator = coroutine });
        }

        public static void StartCoroutineWithUI(IEnumerator coroutine, string title, bool isCancelable = false)
        {
            if (_uiCoroutine != null)
            {
                Debug.LogError($"已有 UI 协程在运行，无法启动: {title}");
                return;
            }
            _uiCoroutine = new CoroutineState
            {
                Enumerator = coroutine,
                Title = title,
                HasUI = true,
                IsCancelable = isCancelable
            };
            Register(_uiCoroutine);
        }

        public static void UpdateUI(string label, float percent)
        {
            if (_uiCoroutine == null || !_uiCoroutine.HasUI) return;
            _uiCoroutine.Label = label;
            _uiCoroutine.Progress = percent;
        }

        private static void Register(CoroutineState state)
        {
            if (_coroutines.Count == 0)
                EditorApplication.update += Tick;
            _coroutines.Add(state);
        }

        private static void Tick()
        {
            for (int i = 0; i < _coroutines.Count; i++)
            {
                var state = _coroutines[i];
                if (!state.Enumerator.MoveNext())
                {
                    _finished.Add(state);
                    continue;
                }

                // UI 更新
                if (state.HasUI && state == _uiCoroutine)
                {
                    bool cancel = EditorUtility.DisplayCancelableProgressBar(
                        state.Title, state.Label ?? "", state.Progress);
                    if (cancel && state.IsCancelable)
                        _finished.Add(state);
                }
            }

            foreach (var f in _finished)
            {
                _coroutines.Remove(f);
                if (_uiCoroutine == f)
                {
                    _uiCoroutine = null;
                    EditorUtility.ClearProgressBar();
                }
            }
            _finished.Clear();

            if (_coroutines.Count == 0)
                EditorApplication.update -= Tick;
        }
    }

    #endregion
}
#endif
