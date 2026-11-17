// PSD Reader - 链接图层与文档加载器
// 处理智能对象（Smart Object）的外部文件引用和加载

using System;
using System.Collections.Generic;
using System.IO;

namespace PsdReader
{
    /// <summary>
    /// 链接图层信息 —— 对应 PSD 中的智能对象。
    /// 存储外部文件的引用 ID、名称和尺寸。
    /// </summary>
    public class LinkedLayerInfo : ILinkedLayer
    {
        public string UniqueId { get; set; } = "";
        public string FileName { get; set; } = "";
        public string FileType { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>嵌入的文件数据（如果在 PSD 内部）</summary>
        public byte[] EmbeddedData { get; set; }

        /// <summary>外部文件路径（如果是外部链接）</summary>
        public string ExternalPath { get; set; }

        // ─── ILinkedLayer 接口实现 ───

        PsdDocument ILinkedLayer.Document => null;

        Uri ILinkedLayer.AbsoluteUri =>
            !string.IsNullOrEmpty(ExternalPath) ? new Uri(ExternalPath, UriKind.RelativeOrAbsolute) : null;

        bool ILinkedLayer.HasDocument => false;

        Guid ILinkedLayer.ID =>
            Guid.TryParse(UniqueId, out var g) ? g : Guid.Empty;

        string ILinkedLayer.Name => FileName;
    }

    /// <summary>
    /// 文档加载器 — 用于从外部文件路径加载 PSD 文档。
    /// 带缓存机制，避免重复解析同一文件。
    /// </summary>
    public class DocumentLoader : IDisposable
    {
        private readonly Dictionary<string, PsdDocument> _cache = new Dictionary<string, PsdDocument>(StringComparer.OrdinalIgnoreCase);
        private readonly string _basePath;

        public DocumentLoader(string basePath)
        {
            _basePath = basePath ?? "";
        }

        /// <summary>
        /// 根据相对或绝对路径加载文档，自动缓存。
        /// </summary>
        public PsdDocument Load(string path)
        {
            string fullPath = ResolvePath(path);

            if (_cache.TryGetValue(fullPath, out var cached))
                return cached;

            if (!File.Exists(fullPath))
                return null;

            var doc = PsdDocument.Open(fullPath);
            _cache[fullPath] = doc;
            return doc;
        }

        /// <summary>
        /// 解析路径：如果是相对路径，相对于 basePath 解析。
        /// </summary>
        private string ResolvePath(string path)
        {
            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            if (!string.IsNullOrEmpty(_basePath))
                return Path.GetFullPath(Path.Combine(_basePath, path));

            return Path.GetFullPath(path);
        }

        public void Dispose()
        {
            foreach (var doc in _cache.Values)
            {
                doc?.Dispose();
            }
            _cache.Clear();
        }
    }

    /// <summary>
    /// 链接图层解析辅助 — 从 lnkD / SoLd additional layer info 中提取链接信息
    /// </summary>
    internal static class LinkedLayerParser
    {
        /// <summary>
        /// 从 additional layer info 数据中解析链接图层信息。
        /// lnkD key 包含一个或多个链接文件描述。
        /// </summary>
        public static List<LinkedLayerInfo> ParseLinkedLayers(PsdBinaryReader reader, long endPosition)
        {
            var result = new List<LinkedLayerInfo>();

            while (reader.BaseStream.Position < endPosition - 8)
            {
                var info = new LinkedLayerInfo();

                // 每条记录有: Type + Length + Data
                string type = reader.ReadSignature();
                // liFD = 嵌入数据, liFE = 外部引用, liFA = 别名
                
                long dataLength;
                if (type == "liFD" || type == "liFE" || type == "liFA")
                {
                    // PSD 格式: 版本号决定长度字段大小
                    int version = reader.ReadInt32BigEndian();
                    info.UniqueId = reader.ReadLengthPrefixedString();
                    info.FileName = reader.ReadUnicodeString();
                    info.FileType = reader.ReadSignature();

                    // 创建者签名 + 文件大小
                    string creator = reader.ReadSignature();
                    dataLength = reader.ReadInt64BigEndian();

                    // 是否有嵌入数据
                    if (type == "liFD" && dataLength > 0)
                    {
                        info.EmbeddedData = reader.ReadBytes((int)Math.Min(dataLength, int.MaxValue));
                    }
                    else
                    {
                        // 跳过其余记录数据
                        reader.BaseStream.Position = Math.Min(reader.BaseStream.Position + dataLength, endPosition);
                    }

                    result.Add(info);
                }
                else
                {
                    // 未知类型，跳到末尾
                    break;
                }
            }

            return result;
        }
    }
}
