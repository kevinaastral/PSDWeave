// PSD Reader - 图层解析与层级构建
// 从二进制流中读取图层记录，构建图层树

using System;
using System.Collections.Generic;
using System.Linq;

namespace PsdReader
{
    /// <summary>
    /// 图层解析器 — 读取扁平的图层记录数组，构建嵌套的图层树。
    /// 
    /// PSD 中图层是扁平存储的，图层组通过 SectionType 标记：
    ///   [Layer "Bg"] [Layer "Btn"] [Opend "Group1"] [Layer "Icon"] [Layer "Label"] [Divider]
    ///   
    /// 解析后构建为树:
    ///   Document
    ///     ├─ Group1/
    ///     │   ├─ Icon
    ///     │   └─ Label
    ///     ├─ Btn
    ///     └─ Bg
    /// </summary>
    internal static class LayerParser
    {
        /// <summary>
        /// 解析图层节: 读取图层记录 → 读取通道数据 → 构建层级树 → 计算组边界
        /// </summary>
        public static PsdLayer[] ParseLayers(PsdBinaryReader reader, PsdDocument document)
        {
            // 图层数量（可能为负数，绝对值为实际数量）
            int layerCount = Math.Abs(reader.ReadInt16BigEndian());

            if (layerCount == 0)
                return Array.Empty<PsdLayer>();

            // 第一遍: 读取所有图层的元数据记录
            var layers = new PsdLayer[layerCount];
            for (int i = 0; i < layerCount; i++)
            {
                var records = ParseLayerRecord(reader);
                layers[i] = new PsdLayer(document, records);
            }

            // 第二遍: 读取所有图层的通道像素数据
            for (int i = 0; i < layerCount; i++)
            {
                ReadChannelData(reader, layers[i]);
            }

            // 第三步: 从扁平数组构建嵌套的图层树
            layers = BuildHierarchy(layers);

            // 第四步: 自底向上计算组的边界
            foreach (var layer in layers.SelectMany(l => l.GetDescendants()).Reverse())
            {
                layer.ComputeBounds();
            }

            return layers;
        }

        /// <summary>
        /// 解析单条图层记录
        /// </summary>
        private static LayerRecords ParseLayerRecord(PsdBinaryReader reader)
        {
            var rec = new LayerRecords
            {
                Top = reader.ReadInt32BigEndian(),
                Left = reader.ReadInt32BigEndian(),
                Bottom = reader.ReadInt32BigEndian(),
                Right = reader.ReadInt32BigEndian()
            };

            // 通道信息
            int channelCount = reader.ReadInt16BigEndian();
            rec.ChannelCount = channelCount;
            for (int c = 0; c < channelCount; c++)
            {
                rec.Channels[c].Type = reader.ReadChannelType();
                rec.Channels[c].Size = reader.ReadVersionDependentLength();
            }

            // 混合模式签名 "8BIM"
            reader.ReadSignature();

            // 混合模式
            rec.BlendMode = reader.ReadBlendMode();

            // 不透明度 (0~255)
            rec.Opacity = reader.ReadByte();

            // 裁剪: 0=基底, 1=非基底
            rec.Clipping = reader.ReadByte() != 0;

            // 标志位
            rec.Flags = reader.ReadLayerFlags();

            // 填充字节
            reader.ReadByte();

            // 额外数据长度
            int extraDataLength = reader.ReadInt32BigEndian();
            long extraDataEnd = reader.Position + extraDataLength;

            // 图层蒙版
            var mask = ParseLayerMask(reader);

            // 混合范围
            var blending = ParseBlendingRanges(reader);

            // Pascal 字符串名称 (4字节对齐)
            string name = reader.ReadPascalString(4);

            // 快速解析附加信息（仅 lsct/luni，用于构建层级树）
            var resources = new PropertyDictionary();
            long additionalInfoPos = reader.Position;
            long additionalInfoLen = extraDataEnd - reader.Position;
            ParseLayerAdditionalInfoFast(reader, additionalInfoLen, rec, resources);

            // luni（Unicode图层名）优先于 Pascal string，因为 Pascal string 可能编码错误
            string finalName = name;
            if (resources.Contains("LayerUnicodeName"))
            {
                string luniName = resources["LayerUnicodeName"] as string;
                if (!string.IsNullOrEmpty(luniName))
                    finalName = luniName;
            }
            rec.SetExtraRecords(mask, blending, resources, finalName);
            // 记录位置，完整资源延迟到首次访问 Resources 时解析
            rec.SetDeferredResourceInfo(reader, additionalInfoPos, additionalInfoLen);

            reader.Position = extraDataEnd;
            return rec;
        }

        /// <summary>
        /// 解析图层蒙版数据
        /// </summary>
        private static LayerMask ParseLayerMask(PsdBinaryReader reader)
        {
            int maskLength = reader.ReadInt32BigEndian();
            if (maskLength == 0)
                return null;

            long endPos = reader.Position + maskLength;

            var mask = new LayerMask
            {
                Top = reader.ReadInt32BigEndian(),
                Left = reader.ReadInt32BigEndian(),
                Bottom = reader.ReadInt32BigEndian(),
                Right = reader.ReadInt32BigEndian(),
                DefaultColor = reader.ReadByte(),
                Flags = reader.ReadByte()
            };

            reader.Position = endPos;
            return mask;
        }

        /// <summary>
        /// 解析混合范围数据
        /// </summary>
        private static LayerBlendingRanges ParseBlendingRanges(PsdBinaryReader reader)
        {
            int length = reader.ReadInt32BigEndian();
            if (length == 0)
                return null;

            return new LayerBlendingRanges { Data = reader.ReadBytes(length) };
        }

        /// <summary>
        /// 快速解析图层附加信息 — 仅提取构建层级树所需的 lsct/lsdk 和 luni，跳过所有描述符
        /// </summary>
        private static void ParseLayerAdditionalInfoFast(PsdBinaryReader reader, long remainLen,
            LayerRecords rec, PropertyDictionary resources)
        {
            long endPos = reader.Position + remainLen;
            bool foundLuni = false;

            while (reader.Position + 12 <= endPos)
            {
                long sigPos = reader.Position;
                if (!reader.ValidateLayerSignature())
                {
                    UnityEngine.Debug.Log($"[PSD:FastParse] 图层='{rec.Name}' 签名验证失败 @pos={sigPos}, endPos={endPos}, remain={endPos-sigPos}");
                    break;
                }

                string key = reader.ReadSignature();
                int rawDataLength = reader.ReadInt32BigEndian();
                int dataLength = (rawDataLength + 1) & ~1; // 2字节对齐（PSD规范: even-padded）
                long dataEnd = reader.Position + dataLength;

                switch (key)
                {
                    case "lsct": // 分节类型（组开始/结束/普通）
                    case "lsdk":
                        int sectionType = reader.ReadInt32BigEndian();
                        rec.SetSectionData((SectionType)sectionType);

                        if (dataLength >= 12)
                        {
                            reader.ReadSignature(); // "8BIM"
                            rec.BlendMode = reader.ReadBlendMode();
                        }
                        break;

                    case "luni": // Unicode 图层名
                        foundLuni = true;
                        string unicodeName = reader.ReadUnicodeString();
                        UnityEngine.Debug.Log($"[PSD:luni] 图层='{rec.Name}' -> unicodeName='{unicodeName}' rawLen={rawDataLength}");
                        if (!string.IsNullOrEmpty(unicodeName))
                        {
                            rec.Name = unicodeName;
                            resources.Add("LayerUnicodeName", unicodeName);
                        }
                        break;

                    case "TySh": // 文字图层标记（仅记录存在，不解析描述符）
                        resources.Add("TySh", true);
                        break;

                    case "SoCo": // 纯色填充标记
                        resources.Add("SoCo", true);
                        break;

                    case "SoLd": // 智能对象标记
                    case "PlLd":
                        resources.Add("PlacedLayerData", true);
                        break;

                    case "lfx2": // 图层效果标记（实际描述符在 Full 阶段解析）
                    case "lfxs":
                    case "lmfx": // Photoshop 新版图层效果描述符，结构与 lfx2 相同
                        resources.Add("lfx2", true);
                        break;

                    case "lrFX":
                        resources.Add("lrFX", true);
                        break;

                    // 所有其他 key 跳过，延迟到 EnsureResourcesLoaded
                }

                reader.Position = dataEnd;
            }

            if (!foundLuni)
                UnityEngine.Debug.LogWarning($"[PSD:FastParse] 图层='{rec.Name}' 未找到luni, remainLen={remainLen}, endPos={endPos}");
        }

        /// <summary>
        /// 完整解析图层附加信息 — 延迟调用，解析描述符（TySh、SoLd、lfx2、SoCo 等）
        /// </summary>
        internal static void ParseLayerAdditionalInfoFull(PsdBinaryReader reader, long remainLen,
            LayerRecords rec, PropertyDictionary resources)
        {
            long endPos = reader.Position + remainLen;

            while (reader.Position + 12 <= endPos)
            {
                if (!reader.ValidateLayerSignature())
                    break;

                string key = reader.ReadSignature();
                int dataLength = reader.ReadInt32BigEndian();
                dataLength = (dataLength + 1) & ~1; // 2字节对齐（PSD规范: even-padded）
                long dataEnd = reader.Position + dataLength;

                switch (key)
                {
                    case "lsct": // 已在快速解析中处理
                    case "lsdk":
                    case "luni":
                        break;

                    case "SoLd": // 智能对象放置数据
                    case "PlLd":
                        try
                        {
                            var desc = DescriptorReader.ReadDescriptor(reader);
                            resources.Add("PlacedLayerData", desc); // 覆盖快速解析的布尔标记

                            if (desc.Contains("Idnt"))
                            {
                                string idStr = desc["Idnt"] as string;
                                if (Guid.TryParse(idStr, out Guid placedId))
                                    rec.SetSectionData(rec.SectionType, placedId);
                            }
                        }
                        catch { }
                        break;

                    case "TySh": // 文字工具数据
                        try
                        {
                            // TySh 格式: Version(Int16) + Transform(6 doubles) + TextDescVersion(Int16) + Descriptor(Int32 ver + body)
                            reader.ReadInt16BigEndian();   // TySh version (=1)
                            var transform = reader.ReadDoubleArray(6);     // 变换矩阵 (xx, xy, yx, yy, tx, ty)
                            reader.ReadInt16BigEndian();   // 文本描述符版本
                            var textDesc = DescriptorReader.ReadDescriptor(reader);
                            textDesc.Add("_Transform", transform);
                            resources.Add("TySh", textDesc); // 覆盖快速解析的布尔标记
                            if (!resources.Contains("TextEngineData"))
                                resources.Add("TextEngineData", textDesc);
                        }
                        catch (System.Exception ex)
                        {
                            UnityEngine.Debug.LogWarning($"[PSD:TySh] 解析失败: {ex.Message}");
                        }
                        break;

                    case "lfx2": // 图层效果
                    case "lfxs":
                    case "lmfx": // Photoshop 新版图层效果描述符，结构与 lfx2 相同
                        try
                        {
                            reader.ReadInt32BigEndian(); // 跳过 Object effects version (= 0)
                            var fxDesc = DescriptorReader.ReadDescriptor(reader);
                            resources.Add("lfx2", fxDesc); // 覆盖快速解析的布尔标记
                        }
                        catch (System.Exception ex)
                        {
                            UnityEngine.Debug.LogWarning($"[PSD:lfx2] 图层效果解析失败: {ex.Message}");
                        }
                        break;

                    case "lrFX": // Photoshop 5.x/7.0 旧版图层效果
                        try
                        {
                            var legacyEffects = LegacyLayerEffectsParser.Parse(reader, dataLength);
                            resources.Add("lrFX", legacyEffects);
                        }
                        catch (System.Exception ex)
                        {
                            UnityEngine.Debug.LogWarning($"[PSD:lrFX] 旧版图层效果解析失败: {ex.Message}");
                        }
                        break;

                    case "SoCo": // 纯色填充
                        try
                        {
                            var fillDesc = DescriptorReader.ReadDescriptor(reader);
                            resources.Add("SoCo", fillDesc); // 覆盖快速解析的布尔标记
                            if (!resources.Contains("SolidColorFillData"))
                                resources.Add("SolidColorFillData", fillDesc);
                        }
                        catch { }
                        break;

                    // 跳过未知键，不再读取原始字节
                }

                reader.Position = dataEnd;
            }
        }

        /// <summary>
        /// 记录单个图层的通道数据在流中的位置，跳过不读取（真正的懒加载）。
        /// 数据仅在首次访问 Channels 属性时才从文件读取并解压。
        /// </summary>
        private static void ReadChannelData(PsdBinaryReader reader, PsdLayer layer)
        {
            var records = layer.Records;
            long totalSize = records.ChannelSize;

            // 仅记录位置并跳过，与原版 LazyLoadSection 行为一致
            layer.SetDeferredChannelPosition(reader, reader.Position, totalSize);
            reader.SkipBytes((int)totalSize);
        }

        /// <summary>
        /// 从扁平的图层数组构建嵌套的图层树。
        /// 
        /// 算法: 反向遍历图层列表（PSD 从底到顶存储），
        /// 用栈跟踪当前所在的组。遇到 Opend/Closed 进入组，
        /// 遇到 Divider 退出组。
        /// </summary>
        public static PsdLayer[] BuildHierarchy(PsdLayer[] flatLayers)
        {
            var stack = new Stack<PsdLayer>();     // 父组栈
            var rootLayers = new List<PsdLayer>(); // 根层级图层
            var childMap = new Dictionary<PsdLayer, List<PsdLayer>>();

            PsdLayer currentParent = null;

            // PSD 图层从底到顶存储，所以反向遍历
            foreach (var layer in flatLayers.Reverse())
            {
                if (layer.SectionType == SectionType.Divider)
                {
                    // 组结束标记 → 弹出回到上一层
                    currentParent = stack.Count > 0 ? stack.Pop() : null;
                    continue;
                }

                if (currentParent != null)
                {
                    // 在某个组内
                    if (!childMap.ContainsKey(currentParent))
                        childMap[currentParent] = new List<PsdLayer>();
                    childMap[currentParent].Add(layer);
                    layer.Parent = currentParent;
                }
                else
                {
                    // 在根层级
                    rootLayers.Add(layer);
                }

                // 如果当前图层是组的开始标记 → 进入子层级
                if (layer.SectionType == SectionType.Opend || layer.SectionType == SectionType.Closed)
                {
                    stack.Push(currentParent);
                    currentParent = layer;
                }
            }

            // 将收集的子图层分配给对应的组（反转顺序恢复原始排列）
            foreach (var pair in childMap)
            {
                pair.Value.Reverse();
                pair.Key.Childs = pair.Value.ToArray();
            }

            rootLayers.Reverse();
            return rootLayers.ToArray();
        }
    }
}
