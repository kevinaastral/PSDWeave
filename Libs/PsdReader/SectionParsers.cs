// PSD Reader - PSD 五大节解析器
// 将 PSD 文件的 5 个主要节（文件头、颜色模式、图像资源、图层蒙版、图像数据）统一管理

using System;
using System.Collections.Generic;
using System.IO;

namespace PsdReader
{
    /// <summary>
    /// 图层与蒙版节的解析结果
    /// </summary>
    public class LayerAndMaskResult
    {
        public PsdLayer[] Layers { get; set; }
        public List<ILinkedLayer> LinkedLayers { get; set; }
        public IProperties Resources { get; set; }
    }

    /// <summary>
    /// PSD 节解析器 — 负责解析 PSD 文件的各个主要节。
    /// 
    /// PSD 文件结构:
    ///   [文件头 26字节] → [颜色模式] → [图像资源] → [图层与蒙版] → [图像数据]
    ///   每节前面有一个 32 位（PSD）或 64 位（PSB）的长度字段。
    /// </summary>
    internal static class SectionParsers
    {
        /// <summary>
        /// 解析文件头: 6 字节保留 + 通道数(2) + 高(4) + 宽(4) + 位深(2) + 颜色模式(2)
        /// 注意: 签名"8BPS"和版本号已在 ValidateDocumentHeader 中读取。
        /// </summary>
        public static FileHeader ParseFileHeader(PsdBinaryReader reader)
        {
            // 跳过 6 字节保留区
            reader.SkipBytes(6);

            var header = new FileHeader
            {
                NumberOfChannels = reader.ReadInt16BigEndian(),
                Height = reader.ReadInt32BigEndian(),
                Width = reader.ReadInt32BigEndian(),
                Depth = reader.ReadInt16BigEndian(),
                ColorMode = reader.ReadColorMode()
            };

            return header;
        }

        /// <summary>
        /// 解析颜色模式数据节。
        /// 对于索引色模式包含 768 字节调色板 (256 × RGB)。
        /// 对于其他模式通常为空。
        /// </summary>
        public static byte[] ParseColorModeData(PsdBinaryReader reader)
        {
            int length = reader.ReadInt32BigEndian();
            if (length > 0)
                return reader.ReadBytes(length);
            return Array.Empty<byte>();
        }

        /// <summary>
        /// 解析图像资源节。
        /// 包含文档级元数据：分辨率、ICC 配置、打印标记等。
        /// 每条资源格式: "8BIM" + ResourceID(2) + PascalName + DataLength(4) + Data
        /// </summary>
        public static IProperties ParseImageResources(PsdBinaryReader reader)
        {
            int sectionLength = reader.ReadInt32BigEndian();
            long endPos = reader.Position + sectionLength;

            var props = new PropertyDictionary();

            while (reader.Position < endPos)
            {
                // 校验签名
                if (!reader.ValidateResourceSignature())
                    break;

                // 资源 ID
                int resourceId = reader.ReadInt16BigEndian();

                // Pascal 字符串名称 (偶数对齐)
                string name = reader.ReadPascalString(2);

                // 数据长度 (偶数对齐)
                int dataLength = reader.ReadInt32BigEndian();
                int alignedLength = (dataLength + 1) & ~1;

                long dataStart = reader.Position;

                // 读取已知的资源类型
                switch (resourceId)
                {
                    case 0x0408: // 参考线
                        props.Add("Guides", ReadGuidesResource(reader, dataLength));
                        break;
                    case 0x03ED: // 分辨率
                        props.Add("Resolution", ReadResolutionResource(reader));
                        break;
                    case 0x0419: // 全局光照角度
                        props.Add("GlobalAngle", reader.ReadInt32BigEndian());
                        break;
                    case 0x0421: // 版本信息
                        props.Add("VersionInfo", true);
                        break;
                    default:
                        // 未知资源：保存原始数据
                        props.Add($"Resource_{resourceId:X4}", reader.ReadBytes(dataLength));
                        break;
                }

                // 确保跳到下一个资源
                reader.Position = dataStart + alignedLength;
            }

            return props;
        }

        /// <summary>
        /// 解析图层与蒙版节 — PSD 最重要的部分。
        /// 包含图层记录、通道数据、附加图层信息（文字、效果、链接层等）。
        /// </summary>
        public static LayerAndMaskResult ParseLayerAndMask(PsdBinaryReader reader, PsdDocument document)
        {
            long sectionLength = reader.ReadVersionDependentLength();
            long endPos = reader.Position + sectionLength;

            var result = new LayerAndMaskResult
            {
                Layers = Array.Empty<PsdLayer>(),
                LinkedLayers = new List<ILinkedLayer>(),
                Resources = new PropertyDictionary()
            };

            if (sectionLength <= 0)
                return result;

            // 子节1: 图层信息
            long layerInfoLength = reader.ReadVersionDependentLength();
            long layerInfoEnd = reader.Position + layerInfoLength;

            if (layerInfoLength > 0)
            {
                result.Layers = LayerParser.ParseLayers(reader, document);
            }
            reader.Position = layerInfoEnd;

            // 子节2: 全局蒙版信息
            int globalMaskLen = reader.ReadInt32BigEndian();
            reader.SkipBytes(globalMaskLen);

            // 子节3: 附加图层信息
            if (reader.Position < endPos)
            {
                var resources = new PropertyDictionary();
                ParseAdditionalLayerInfo(reader, endPos - reader.Position, resources, result.LinkedLayers);
                result.Resources = resources;
            }

            reader.Position = endPos;
            return result;
        }

        /// <summary>
        /// 解析附加图层信息 — 遍历 "8BIM" / "8B64" 标记的子记录
        /// </summary>
        private static void ParseAdditionalLayerInfo(PsdBinaryReader reader, long remainLen,
            PropertyDictionary resources, List<ILinkedLayer> linkedLayers)
        {
            long endPos = reader.Position + remainLen;

            while (reader.Position + 12 < endPos) // 至少需要 4(sig) + 4(key) + 4(len)
            {
                if (!reader.ValidateLayerSignature())
                    break;

                string key = reader.ReadSignature();

                // PSB 中某些键使用 64 位长度
                long dataLength;
                bool isPsbLongKey = reader.Version == 2 && IsPsbLongKey(key);
                if (isPsbLongKey)
                    dataLength = reader.ReadInt64BigEndian();
                else
                    dataLength = reader.ReadInt32BigEndian();

                // 对齐到 4 字节
                dataLength = (dataLength + 3) & ~3L;
                long dataEnd = reader.Position + dataLength;

                // 按 key 路由到对应处理器
                try
                {
                    ParseResourceByKey(reader, key, dataLength, resources, linkedLayers);
                }
                catch
                {
                    // 忽略解析失败的资源
                }

                reader.Position = dataEnd;
            }
        }

        /// <summary>
        /// 按 4 字符键路由到对应的资源解析器
        /// </summary>
        private static void ParseResourceByKey(PsdBinaryReader reader, string key, long dataLength,
            PropertyDictionary resources, List<ILinkedLayer> linkedLayers)
        {
            switch (key)
            {
                case "lsct": // 分节标记（组开始/结束）
                case "lsdk":
                    // 在 LayerRecordParser 中已处理
                    break;

                case "TySh": // 文字工具数据
                    var textData = DescriptorReader.ReadDescriptor(reader);
                    resources.Add("TextEngineData", textData);
                    break;

                case "luni": // Unicode 图层名
                    resources.Add("LayerUnicodeName", reader.ReadUnicodeString());
                    break;

                case "lnsr": // 图层名称源
                    resources.Add("LayerNameSource", reader.ReadSignature());
                    break;

                case "lyid": // 图层 ID
                    resources.Add("LayerID", reader.ReadInt32BigEndian());
                    break;

                case "lfx2": // 图层效果（描述符）
                case "lfxs":
                    reader.ReadInt32BigEndian(); // 跳过 Object effects version (= 0)
                    var effectData = DescriptorReader.ReadDescriptor(reader);
                    resources.Add("LayerEffects", effectData);
                    break;

                case "SoCo": // 纯色填充
                    var colorData = DescriptorReader.ReadDescriptor(reader);
                    resources.Add("SolidColorFillData", colorData);
                    break;

                case "lnkD": // 链接层数据
                case "lnk2":
                case "lnk3":
                    ParseLinkedLayerData(reader, dataLength, linkedLayers);
                    break;

                case "SoLd": // 智能对象放置数据
                case "PlLd":
                    var placedData = DescriptorReader.ReadDescriptor(reader);
                    resources.Add("PlacedLayerData", placedData);
                    break;

                case "vmsk": // 矢量蒙版
                case "vsms":
                    resources.Add("VectorMask", reader.ReadBytes((int)dataLength));
                    break;

                case "iOpa": // 图层不透明度
                    resources.Add("FillOpacity", reader.ReadByte());
                    break;

                case "shmd": // 元数据集
                    ParseMetadataItems(reader, dataLength, resources);
                    break;

                default:
                    // 跳过未知键，不读取原始字节
                    break;
            }
        }

        /// <summary>
        /// 解析链接层数据
        /// </summary>
        private static void ParseLinkedLayerData(PsdBinaryReader reader, long dataLength, List<ILinkedLayer> linkedLayers)
        {
            long endPos = reader.Position + dataLength;

            while (reader.Position < endPos - 8)
            {
                try
                {
                    // 链接层类型
                    string linkType = reader.ReadSignature();

                    // 版本
                    int version = reader.ReadInt32BigEndian();

                    // UniqueID (GUID)
                    string idStr = reader.ReadUnicodeString();
                    Guid id = Guid.Empty;
                    if (!string.IsNullOrEmpty(idStr))
                        Guid.TryParse(idStr, out id);

                    // 文件名
                    string name = reader.ReadUnicodeString();

                    // 文件类型
                    string fileType = reader.ReadSignature();

                    // 文件创建者
                    string creator = reader.ReadSignature();

                    // 数据大小
                    long fileSize = reader.ReadInt64BigEndian();

                    // 目前只记录基本信息
                    linkedLayers.Add(new LinkedLayerInfo { FileName = name, UniqueId = idStr, FileType = fileType });

                    break; // 简化处理
                }
                catch
                {
                    break;
                }
            }

            reader.Position = endPos;
        }

        /// <summary>
        /// 解析元数据集
        /// </summary>
        private static void ParseMetadataItems(PsdBinaryReader reader, long dataLength, PropertyDictionary resources)
        {
            long endPos = reader.Position + dataLength;
            int count = reader.ReadInt32BigEndian();

            for (int i = 0; i < count && reader.Position < endPos; i++)
            {
                reader.ReadSignature(); // "8BIM"
                string key = reader.ReadSignature();
                byte copyOnSheetDup = reader.ReadByte();
                reader.SkipBytes(3); // padding
                int len = reader.ReadInt32BigEndian();
                long itemEnd = reader.Position + len;

                // 跳过原始数据，不读入内存

                reader.Position = itemEnd;
            }
        }

        /// <summary>
        /// PSB 中使用 64 位长度字段的键
        /// </summary>
        private static readonly HashSet<string> PsbLongKeys = new HashSet<string>
        {
            "LMsk", "Lr16", "Lr32", "Layr", "Mt16", "Mt32", "Mtrn",
            "Alph", "FMsk", "lnk2", "FEid", "FXid", "PxSD"
        };

        private static bool IsPsbLongKey(string key) => PsbLongKeys.Contains(key);

        /// <summary>参考线资源解析</summary>
        private static object ReadGuidesResource(PsdBinaryReader reader, int length)
        {
            int version = reader.ReadInt32BigEndian();
            int futureVersion = reader.ReadInt32BigEndian();
            int count = reader.ReadInt32BigEndian();

            var guides = new List<(int position, bool isHorizontal)>();
            for (int i = 0; i < count; i++)
            {
                int pos = reader.ReadInt32BigEndian();
                bool horizontal = reader.ReadByte() == 0;
                guides.Add((pos, horizontal));
            }
            return guides;
        }

        /// <summary>分辨率资源解析</summary>
        private static object ReadResolutionResource(PsdBinaryReader reader)
        {
            return new
            {
                HRes = reader.ReadInt16BigEndian() + reader.ReadInt16BigEndian() / 65536.0,
                HResUnit = reader.ReadInt16BigEndian(),
                WidthUnit = reader.ReadInt16BigEndian(),
                VRes = reader.ReadInt16BigEndian() + reader.ReadInt16BigEndian() / 65536.0,
                VResUnit = reader.ReadInt16BigEndian(),
                HeightUnit = reader.ReadInt16BigEndian()
            };
        }
    }
}
