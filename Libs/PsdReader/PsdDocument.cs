// PSD Reader - PSD 文档
// PSD 文件的顶层入口点，实现了 IPsdLayer（作为图层树的根节点）

using System;
using System.Collections.Generic;
using System.IO;

namespace PsdReader
{
    /// <summary>
    /// PSD 文档 — 代表一个完整的 Photoshop 文件。
    /// 
    /// 使用方式:
    ///   using (var doc = PsdDocument.Open("file.psd"))
    ///   {
    ///       Console.WriteLine($"尺寸: {doc.Width}x{doc.Height}");
    ///       foreach (var layer in doc.Childs) { ... }
    ///   }
    /// 
    /// 内部使用懒加载机制 — 只在访问属性时才真正读取对应的文件节。
    /// </summary>
    public class PsdDocument : IPsdLayer, IDisposable
    {
        private PsdBinaryReader _reader;
        private FileHeader _fileHeader;
        private bool _headerLoaded;
        private byte[] _colorModeData;
        private bool _colorModeLoaded;
        private PsdLayer[] _layers;
        private bool _layersLoaded;
        private IProperties _imageResources;
        private IProperties _layerResources;
        private List<ILinkedLayer> _linkedLayers;
        private Channel[] _imageDataChannels;

        // 各节在流中的位置（懒加载用）
        private long _colorModePosition;
        private long _imageResourcesPosition;
        private long _layerAndMaskPosition;
        private long _imageDataPosition;

        public event EventHandler Disposed;

        #region 公共属性

        /// <summary>文件头信息</summary>
        public FileHeader FileHeader
        {
            get
            {
                EnsureHeaderLoaded();
                return _fileHeader;
            }
        }

        public int Width => FileHeader.Width;
        public int Height => FileHeader.Height;
        public int Depth => FileHeader.Depth;

        /// <summary>颜色模式数据（索引色调色板，通常为空）</summary>
        public byte[] ColorModeData
        {
            get
            {
                EnsureColorModeLoaded();
                return _colorModeData;
            }
        }

        /// <summary>顶层图层列表</summary>
        public PsdLayer[] Childs
        {
            get
            {
                EnsureLayersLoaded();
                return _layers ?? Array.Empty<PsdLayer>();
            }
        }

        /// <summary>链接的外部 PSD 文件（智能对象）</summary>
        public IEnumerable<ILinkedLayer> LinkedLayers
        {
            get
            {
                EnsureLayersLoaded();
                return _linkedLayers ?? (IEnumerable<ILinkedLayer>)Array.Empty<ILinkedLayer>();
            }
        }

        /// <summary>图层级资源/元数据</summary>
        public IProperties Resources
        {
            get
            {
                EnsureLayersLoaded();
                return _layerResources;
            }
        }

        /// <summary>图像级资源/元数据</summary>
        public IProperties ImageResources
        {
            get
            {
                EnsureImageResourcesLoaded();
                return _imageResources;
            }
        }

        #endregion

        #region IPsdLayer 显式实现（文档作为根节点）

        IPsdLayer IPsdLayer.Parent => null;
        bool IPsdLayer.IsClipping => false;
        PsdDocument IPsdLayer.Document => this;
        ILinkedLayer IPsdLayer.LinkedLayer => null;
        string IPsdLayer.Name => "Document";
        int IPsdLayer.Left => 0;
        int IPsdLayer.Top => 0;
        int IPsdLayer.Right => Width;
        int IPsdLayer.Bottom => Height;
        BlendMode IPsdLayer.BlendMode => BlendMode.Normal;
        IPsdLayer[] IPsdLayer.Childs => Childs;
        IChannel[] IImageSource.Channels => _imageDataChannels;
        float IImageSource.Opacity => 1f;
        bool IImageSource.HasImage => true;
        bool IImageSource.HasMask => FileHeader.NumberOfChannels > 4;

        #endregion

        #region 构造与工厂

        internal PsdDocument() { }

        /// <summary>
        /// 打开 PSD 文件
        /// </summary>
        public static PsdDocument Open(string filename)
        {
            var doc = new PsdDocument();
            var fileInfo = new FileInfo(filename);
            // Parsing is lazy, so the stream can stay open while its prefab is loaded.
            // Allow Photoshop to replace an exported PSD without waiting for Unity to release it.
            var stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 65536);
            doc.Initialize(stream, new Uri(fileInfo.DirectoryName));
            return doc;
        }

        /// <summary>
        /// 从流打开 PSD
        /// </summary>
        public static PsdDocument Open(Stream stream)
        {
            var doc = new PsdDocument();
            doc.Initialize(stream, new Uri(Directory.GetCurrentDirectory()));
            return doc;
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化文档：验证签名 → 记录各节位置（懒加载）
        /// </summary>
        internal void Initialize(Stream stream, Uri baseUri)
        {
            _reader = new PsdBinaryReader(stream);

            // 验证 "8BPS" 签名并读取版本号
            _reader.ValidateDocumentHeader();

            // 读取文件头（立即，因为后续所有节都依赖文件头的信息）
            _fileHeader = SectionParsers.ParseFileHeader(_reader);
            _headerLoaded = true;

            // 跳过保留字节（6字节已在签名校验中处理）

            // 记录颜色模式数据节的位置 → 跳过
            _colorModePosition = _reader.Position;
            int colorModeLen = _reader.ReadInt32BigEndian();
            _reader.SkipBytes(colorModeLen);

            // 记录图像资源节的位置 → 跳过
            _imageResourcesPosition = _reader.Position;
            int imageResourcesLen = _reader.ReadInt32BigEndian();
            _reader.SkipBytes(imageResourcesLen);

            // 记录图层与蒙版节的位置 → 跳过
            _layerAndMaskPosition = _reader.Position;
            long layerAndMaskLen = _reader.ReadVersionDependentLength();
            _reader.SkipBytes((int)layerAndMaskLen);

            // 剩余部分 = 图像数据节
            _imageDataPosition = _reader.Position;
        }

        #endregion

        #region 懒加载

        private void EnsureHeaderLoaded()
        {
            // 文件头在 Initialize 中已读取
        }

        private void EnsureColorModeLoaded()
        {
            if (_colorModeLoaded) return;
            long saved = _reader.Position;
            _reader.Position = _colorModePosition;
            _colorModeData = SectionParsers.ParseColorModeData(_reader);
            _reader.Position = saved;
            _colorModeLoaded = true;
        }

        private void EnsureImageResourcesLoaded()
        {
            if (_imageResources != null) return;
            long saved = _reader.Position;
            _reader.Position = _imageResourcesPosition;
            _imageResources = SectionParsers.ParseImageResources(_reader);
            _reader.Position = saved;
        }

        private void EnsureLayersLoaded()
        {
            if (_layersLoaded) return;
            long saved = _reader.Position;
            _reader.Position = _layerAndMaskPosition;
            var result = SectionParsers.ParseLayerAndMask(_reader, this);
            _layers = result.Layers;
            _linkedLayers = result.LinkedLayers;
            _layerResources = result.Resources;
            _reader.Position = saved;
            _layersLoaded = true;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_reader != null)
            {
                _reader.Dispose();
                _reader = null;
                Disposed?.Invoke(this, EventArgs.Empty);
            }
        }

        #endregion
    }
}
