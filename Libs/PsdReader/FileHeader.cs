// PSD Reader - 文件头
// PSD 文件头的数据结构定义

namespace PsdReader
{
    /// <summary>
    /// PSD 文件头（26字节）: 签名 + 版本 + 保留 + 通道数 + 高 + 宽 + 位深 + 颜色模式
    /// </summary>
    public struct FileHeader
    {
        /// <summary>位深度（1/8/16/32）</summary>
        public int Depth { get; set; }

        /// <summary>通道数量</summary>
        public int NumberOfChannels { get; set; }

        /// <summary>颜色模式</summary>
        public ColorMode ColorMode { get; set; }

        /// <summary>文档高度（像素）</summary>
        public int Height { get; set; }

        /// <summary>文档宽度（像素）</summary>
        public int Width { get; set; }
    }
}
