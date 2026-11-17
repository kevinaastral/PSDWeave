// PSD Reader - PSD 图层
// 单个图层或图层组，包含位置、混合模式、通道数据等

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PsdReader
{
    /// <summary>
    /// PSD 图层 — 图层树中的一个节点。
    /// 可以是普通图层（有像素数据）或组文件夹（包含子图层）。
    /// </summary>
    public class PsdLayer : IPsdLayer, IImageSource
    {
        private static readonly PsdLayer[] EmptyChilds = Array.Empty<PsdLayer>();

        private readonly PsdDocument _document;
        private readonly LayerRecords _records;
        private Channel[] _channelData;
        private bool _channelsLoaded;
        private PsdLayer _parent;
        private PsdLayer[] _childs;
        private ILinkedLayer _linkedLayer;

        // 延迟加载: 仅记录通道数据在文件流中的位置，首次访问 Channels 时才读取并解压
        private PsdBinaryReader _deferredReader;
        private long _deferredStreamPosition;
        private long _deferredDataLength;
        private int _deferredVersion;

        // 位置（组的边界会在构建树后重新计算）
        private int _left, _top, _right, _bottom;

        #region 公共属性

        public string Name => _records.Name;
        public int Left => _left;
        public int Top => _top;
        public int Right => _right;
        public int Bottom => _bottom;
        public int Width => _right - _left;
        public int Height => _bottom - _top;
        public int Depth => _document.FileHeader.Depth;

        public BlendMode BlendMode => _records.BlendMode;
        public float Opacity => _records.Opacity / 255f;
        public bool IsClipping => _records.Clipping;

        /// <summary>图层可见性（注意: PSD 中 Visible 标志位=1 表示隐藏）</summary>
        public bool IsVisible => (_records.Flags & LayerFlags.Visible) != LayerFlags.Visible;

        /// <summary>是否为图层组文件夹</summary>
        public bool IsGroup => SectionType == SectionType.Closed || SectionType == SectionType.Opend;

        public SectionType SectionType => _records.SectionType;

        /// <summary>通道数据（懒加载）</summary>
        public Channel[] Channels
        {
            get
            {
                EnsureChannelsLoaded();
                return _channelData;
            }
        }

        /// <summary>父图层/组</summary>
        public PsdLayer Parent
        {
            get => _parent;
            internal set => _parent = value;
        }

        /// <summary>子图层列表（组才有）</summary>
        public PsdLayer[] Childs
        {
            get => _childs ?? EmptyChilds;
            internal set => _childs = value;
        }

        /// <summary>附加资源/元数据（Contains检查使用快速缓存，索引访问触发完整加载）</summary>
        public IProperties Resources
        {
            get
            {
                var res = _records.Resources;
                if (res == null) return null;
                // 如果已完整加载，直接返回
                if (_records.IsResourcesFullyLoaded) return res;
                // 返回延迟代理
                if (_lazyResources == null)
                    _lazyResources = new LazyResourcesProxy(_records);
                return _lazyResources;
            }
        }
        private LazyResourcesProxy _lazyResources;

        /// <summary>所属文档</summary>
        public PsdDocument Document => _document;

        /// <summary>图层记录元数据</summary>
        public LayerRecords Records => _records;

        /// <summary>链接的外部 PSD（智能对象）</summary>
        public ILinkedLayer LinkedLayer
        {
            get
            {
                if (_records.PlacedID == Guid.Empty)
                    return null;

                if (_linkedLayer == null)
                {
                    _linkedLayer = _document.LinkedLayers
                        .FirstOrDefault(l => l.ID == _records.PlacedID && l.HasDocument);
                }
                return _linkedLayer;
            }
        }

        /// <summary>是否有可渲染的像素数据</summary>
        public bool HasImage
        {
            get
            {
                if (SectionType != SectionType.Normal)
                    return false;
                return Width > 0 && Height > 0;
            }
        }

        public bool HasMask => _records.Mask != null;

        #endregion

        #region IPsdLayer 显式实现

        IPsdLayer IPsdLayer.Parent => _parent ?? (IPsdLayer)_document;
        IChannel[] IImageSource.Channels => Channels;
        IPsdLayer[] IPsdLayer.Childs => Childs;

        #endregion

        #region 构造

        internal PsdLayer(PsdDocument document, LayerRecords records)
        {
            _document = document;
            _records = records;
            _left = records.Left;
            _top = records.Top;
            _right = records.Right;
            _bottom = records.Bottom;
        }

        #endregion

        #region 通道数据加载

        /// <summary>存储已解压的通道数据</summary>
        internal void SetChannelData(Channel[] channels)
        {
            _channelData = channels;
            _channelsLoaded = true;
            _deferredReader = null;
        }

        /// <summary>记录通道数据在文件流中的位置，延迟到首次访问时才读取并解压</summary>
        internal void SetDeferredChannelPosition(PsdBinaryReader reader, long position, long length)
        {
            _deferredReader = reader;
            _deferredStreamPosition = position;
            _deferredDataLength = length;
            _deferredVersion = reader.Version;
            _channelsLoaded = false;
        }

        private void EnsureChannelsLoaded()
        {
            if (_channelsLoaded)
                return;

            if (_deferredReader != null && _deferredDataLength > 0)
            {
                // 从文件流中读取并解压通道数据
                DecompressChannelData();
            }
            else
            {
                _channelData = _records.Channels;
            }
            _channelsLoaded = true;
        }

        /// <summary>从文件流的记录位置读取并解压通道数据（真正的懒加载）</summary>
        private void DecompressChannelData()
        {
            // 从文件中读取原始字节
            long savedPos = _deferredReader.Position;
            _deferredReader.Position = _deferredStreamPosition;
            byte[] rawData = _deferredReader.ReadBytes((int)_deferredDataLength);
            _deferredReader.Position = savedPos;

            var channels = _records.Channels;

            using (var ms = new MemoryStream(rawData))
            using (var channelReader = new PsdBinaryReader(ms))
            {
                channelReader.Version = _deferredVersion;

                foreach (var channel in channels)
                {
                    channel.Width = Math.Max(0, _records.Width);
                    channel.Height = Math.Max(0, _records.Height);

                    if (channel.Type == ChannelType.Mask && _records.Mask != null)
                    {
                        channel.Width = _records.Mask.Width;
                        channel.Height = _records.Mask.Height;
                    }

                    if (channel.Width == 0 || channel.Height == 0)
                    {
                        channel.Data = Array.Empty<byte>();
                        continue;
                    }

                    var compression = channelReader.ReadCompressionType();
                    channel.ReadRleHeader(channelReader, compression);
                    int packedDataLength = checked((int)Math.Max(0L, channel.Size - 2L));
                    channel.ReadData(channelReader, Depth, compression, packedDataLength);
                }
            }

            _channelData = channels;
            _deferredReader = null; // 解压后释放流引用
        }

        #endregion

        #region 组边界计算

        /// <summary>
        /// 递归计算组的边界框（包含所有子图层的范围）
        /// </summary>
        public void ComputeBounds()
        {
            if (SectionType != SectionType.Opend && SectionType != SectionType.Closed)
                return;

            int minLeft = int.MaxValue;
            int minTop = int.MaxValue;
            int maxRight = int.MinValue;
            int maxBottom = int.MinValue;
            bool found = false;

            foreach (var descendant in GetDescendants())
            {
                if (descendant == this) continue;
                if (!descendant.HasImage) continue;

                // 直接使用图层记录中的边界（避免触发延迟资源加载）
                minLeft = Math.Min(descendant.Left, minLeft);
                minTop = Math.Min(descendant.Top, minTop);
                maxRight = Math.Max(descendant.Right, maxRight);
                maxBottom = Math.Max(descendant.Bottom, maxBottom);
                found = true;
            }

            if (found)
            {
                _left = minLeft;
                _top = minTop;
                _right = maxRight;
                _bottom = maxBottom;
            }
        }

        /// <summary>
        /// 递归遍历所有后代图层（深度优先）
        /// </summary>
        public IEnumerable<PsdLayer> GetDescendants()
        {
            yield return this;
            if (_childs != null)
            {
                foreach (var child in _childs)
                {
                    foreach (var desc in child.GetDescendants())
                        yield return desc;
                }
            }
        }

        #endregion

        public override string ToString()
        {
            return $"[{SectionType}] {Name} ({Left},{Top},{Right},{Bottom})";
        }
    }

    /// <summary>
    /// Resources 延迟代理 — Contains 使用快速缓存，索引器访问触发完整加载
    /// </summary>
    internal class LazyResourcesProxy : IProperties
    {
        private readonly LayerRecords _records;

        internal LazyResourcesProxy(LayerRecords records) => _records = records;

        public bool Contains(string property)
        {
            var fast = _records.Resources;
            return fast != null && fast.Contains(property);
        }

        public object this[string property]
        {
            get
            {
                _records.EnsureResourcesFullyLoaded();
                return _records.Resources?[property];
            }
        }

        public int Count
        {
            get
            {
                _records.EnsureResourcesFullyLoaded();
                return _records.Resources?.Count ?? 0;
            }
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            _records.EnsureResourcesFullyLoaded();
            return _records.Resources?.GetEnumerator()
                ?? ((IEnumerable<KeyValuePair<string, object>>)Array.Empty<KeyValuePair<string, object>>()).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
