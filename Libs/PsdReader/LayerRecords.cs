// PSD Reader - 图层记录
// 存储图层的元数据：位置、混合模式、不透明度、蒙版、名称等

using System;
using System.Linq;

namespace PsdReader
{
    /// <summary>
    /// 图层蒙版数据
    /// </summary>
    public class LayerMask
    {
        public int Top { get; set; }
        public int Left { get; set; }
        public int Bottom { get; set; }
        public int Right { get; set; }
        public byte DefaultColor { get; set; }
        public byte Flags { get; set; }
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    /// <summary>
    /// 图层混合范围数据
    /// </summary>
    public class LayerBlendingRanges
    {
        public byte[] Data { get; set; }
    }

    /// <summary>
    /// 图层记录 — 图层的所有元数据
    /// </summary>
    public class LayerRecords
    {
        // 位置
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }
        public int Width => Right - Left;
        public int Height => Bottom - Top;

        // 混合属性
        public BlendMode BlendMode { get; set; }
        public byte Opacity { get; set; }
        public bool Clipping { get; set; }
        public LayerFlags Flags { get; set; }
        public int Filter { get; set; }

        // 通道
        public Channel[] Channels { get; private set; }
        public int ChannelCount
        {
            get => Channels?.Length ?? 0;
            set
            {
                if (value > 56)
                    throw new PsdParsingException($"通道数过多: {value}");
                Channels = new Channel[value];
                for (int i = 0; i < value; i++)
                    Channels[i] = new Channel();
            }
        }

        /// <summary>所有通道数据的总大小</summary>
        public long ChannelSize => Channels?.Sum(c => c.Size) ?? 0;

        // 附加数据
        public SectionType SectionType { get; internal set; }
        public Guid PlacedID { get; internal set; }
        public string Name { get; internal set; }
        public LayerMask Mask { get; internal set; }
        public LayerBlendingRanges BlendingRanges { get; internal set; }

        // 延迟加载资源
        private IProperties _resources;
        private PsdBinaryReader _deferredResReader;
        private long _deferredResPosition;
        private long _deferredResLength;
        private bool _resourcesFullyLoaded;

        public IProperties Resources
        {
            get => _resources;
            internal set => _resources = value;
        }

        internal bool IsResourcesFullyLoaded => _resourcesFullyLoaded || _deferredResReader == null;

        /// <summary>确保资源完整加载（解析描述符值）</summary>
        internal void EnsureResourcesFullyLoaded()
        {
            if (_resourcesFullyLoaded || _deferredResReader == null) return;
            _resourcesFullyLoaded = true;

            long savedPos = _deferredResReader.Position;
            _deferredResReader.Position = _deferredResPosition;

            var resources = _resources as PropertyDictionary ?? new PropertyDictionary();
            LayerParser.ParseLayerAdditionalInfoFull(_deferredResReader, _deferredResLength, this, resources);
            _resources = resources;

            _deferredResReader.Position = savedPos;
            _deferredResReader = null;
        }

        /// <summary>记录附加信息位置，延迟到实际访问时解析</summary>
        internal void SetDeferredResourceInfo(PsdBinaryReader reader, long position, long length)
        {
            _deferredResReader = reader;
            _deferredResPosition = position;
            _deferredResLength = length;
            _resourcesFullyLoaded = false;
        }

        /// <summary>
        /// 设置图层记录的扩展字段（蒙版、混合范围、资源、名称）
        /// </summary>
        public void SetExtraRecords(LayerMask mask, LayerBlendingRanges blending, IProperties resources, string name)
        {
            Mask = mask;
            BlendingRanges = blending;
            _resources = resources;
            Name = name;
        }

        /// <summary>
        /// 设置分节类型和 PlacedID（用于组和智能对象）
        /// </summary>
        public void SetSectionData(SectionType sectionType, Guid? placedId = null)
        {
            SectionType = sectionType;
            if (placedId.HasValue)
                PlacedID = placedId.Value;
        }
    }
}
