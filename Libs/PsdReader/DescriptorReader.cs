// PSD Reader - Adobe Descriptor 格式解析器
// PSD 中大量元数据使用 Descriptor 格式存储（嵌套键值对，带类型标签）

using System;
using System.Collections;
using System.Collections.Generic;

namespace PsdReader
{
    /// <summary>
    /// Adobe Descriptor 读取器。
    /// 
    /// Descriptor 是 PSD 中的通用嵌套数据格式，类似 JSON:
    ///   - 有类名 + ClassID
    ///   - 包含若干键值对，每对有 key + 4字符类型码 + 类型化值
    /// 
    /// 类型码:
    ///   "TEXT" = 字符串, "long" = 整数, "doub" = 双精度
    ///   "bool" = 布尔, "UntF" = 带单位浮点, "enum" = 枚举
    ///   "Objc" = 嵌套 Descriptor, "VlLs" = 列表
    ///   "tdta" = 原始字节, "ObAr" = 对象数组
    /// </summary>
    internal static class DescriptorReader
    {
        /// <summary>
        /// 读取一个完整的 Descriptor（带版本号前缀）
        /// </summary>
        public static PropertyDictionary ReadDescriptor(PsdBinaryReader reader)
        {
            // 版本号
            int version = reader.ReadInt32BigEndian();
            return ReadDescriptorBody(reader);
        }

        /// <summary>
        /// 读取 Descriptor 主体（不含版本号）
        /// </summary>
        public static PropertyDictionary ReadDescriptorBody(PsdBinaryReader reader)
        {
            var result = new PropertyDictionary();

            // 类名
            string className = reader.ReadUnicodeString();
            result.Add("ClassName", className);

            // ClassID
            string classId = reader.ReadLengthPrefixedString();
            result.Add("ClassID", classId);

            // 键值对数量
            int itemCount = reader.ReadInt32BigEndian();

            for (int i = 0; i < itemCount; i++)
            {
                // 键名
                string key = reader.ReadLengthPrefixedString();

                // 值的类型码 (4字符)
                string typeCode = reader.ReadSignature();

                // 按类型码读取值
                object value = ReadValue(reader, typeCode);

                // 安全添加（避免重复键）— 保留原始 key（含尾部空格，如 "Txt "）
                if (result.Contains(key))
                    key += "_" + i;
                result.Add(key, value);
            }

            return result;
        }

        /// <summary>
        /// 根据 4 字符类型码读取对应类型的值
        /// </summary>
        private static object ReadValue(PsdBinaryReader reader, string typeCode)
        {
            switch (typeCode)
            {
                case "TEXT": // Unicode 字符串
                    return reader.ReadUnicodeString();

                case "long": // 32位整数
                    return reader.ReadInt32BigEndian();

                case "doub": // 64位浮点
                    return reader.ReadDoubleBigEndian();

                case "bool": // 布尔值
                    return reader.ReadBoolean();

                case "UntF": // 带单位的浮点数
                    return ReadUnitFloat(reader);

                case "enum": // 枚举值
                    return ReadEnumerated(reader);

                case "Objc": // 嵌套 Descriptor
                case "GlbO": // 全局对象（同 Descriptor）
                    return ReadDescriptorBody(reader);

                case "VlLs": // 值列表
                    return ReadList(reader);

                case "tdta": // 原始字节数据
                    return ReadRawData(reader);

                case "ObAr": // 对象数组
                    return ReadObjectArray(reader);

                case "type": // 类型引用
                case "GlbC":
                    return ReadClassReference(reader);

                case "alis": // 别名
                    return ReadAlias(reader);

                case "obj ": // 引用
                    return ReadReference(reader);

                case "comp": // 大整数
                    return reader.ReadInt64BigEndian();

                default:
                    // 未知类型，返回类型标识
                    return $"<unknown:{typeCode}>";
            }
        }

        /// <summary>带单位的浮点数: 4字符单位 + 8字节double</summary>
        private static object ReadUnitFloat(PsdBinaryReader reader)
        {
            string unit = reader.ReadSignature();
            double value = reader.ReadDoubleBigEndian();
            return new { Unit = PsdUtils.ParseUnitType(unit), Value = value };
        }

        /// <summary>枚举值: TypeID + EnumValue</summary>
        private static object ReadEnumerated(PsdBinaryReader reader)
        {
            string typeId = reader.ReadLengthPrefixedString();
            string enumValue = reader.ReadLengthPrefixedString();
            return new { Type = typeId, Value = enumValue };
        }

        /// <summary>值列表</summary>
        private static ArrayList ReadList(PsdBinaryReader reader)
        {
            int count = reader.ReadInt32BigEndian();
            var list = new ArrayList(count);
            for (int i = 0; i < count; i++)
            {
                string itemType = reader.ReadSignature();
                list.Add(ReadValue(reader, itemType));
            }
            return list;
        }

        /// <summary>原始字节数据</summary>
        private static byte[] ReadRawData(PsdBinaryReader reader)
        {
            int length = reader.ReadInt32BigEndian();
            return reader.ReadBytes(length);
        }

        /// <summary>对象数组</summary>
        private static ArrayList ReadObjectArray(PsdBinaryReader reader)
        {
            int count = reader.ReadInt32BigEndian();
            string classId = reader.ReadLengthPrefixedString();
            var list = new ArrayList(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(ReadDescriptorBody(reader));
            }
            return list;
        }

        /// <summary>类型引用</summary>
        private static object ReadClassReference(PsdBinaryReader reader)
        {
            string name = reader.ReadUnicodeString();
            string classId = reader.ReadLengthPrefixedString();
            return new { ClassName = name, ClassID = classId };
        }

        /// <summary>别名</summary>
        private static byte[] ReadAlias(PsdBinaryReader reader)
        {
            int length = reader.ReadInt32BigEndian();
            return reader.ReadBytes(length);
        }

        /// <summary>引用</summary>
        private static object ReadReference(PsdBinaryReader reader)
        {
            int count = reader.ReadInt32BigEndian();
            var refs = new ArrayList(count);
            for (int i = 0; i < count; i++)
            {
                string refType = reader.ReadSignature();
                switch (refType)
                {
                    case "prop": // 属性引用
                        refs.Add(new
                        {
                            RefType = "Property",
                            ClassName = reader.ReadUnicodeString(),
                            ClassID = reader.ReadLengthPrefixedString(),
                            KeyID = reader.ReadLengthPrefixedString()
                        });
                        break;
                    case "Clss": // 类引用
                        refs.Add(new
                        {
                            RefType = "Class",
                            ClassName = reader.ReadUnicodeString(),
                            ClassID = reader.ReadLengthPrefixedString()
                        });
                        break;
                    case "Enmr": // 枚举引用
                        refs.Add(new
                        {
                            RefType = "Enum",
                            ClassName = reader.ReadUnicodeString(),
                            ClassID = reader.ReadLengthPrefixedString(),
                            TypeID = reader.ReadLengthPrefixedString(),
                            EnumValue = reader.ReadLengthPrefixedString()
                        });
                        break;
                    case "rele": // 偏移引用
                        refs.Add(new
                        {
                            RefType = "Offset",
                            ClassName = reader.ReadUnicodeString(),
                            ClassID = reader.ReadLengthPrefixedString(),
                            Offset = reader.ReadInt32BigEndian()
                        });
                        break;
                    case "Idnt": // 标识符引用
                        refs.Add(new
                        {
                            RefType = "Identifier",
                            Value = reader.ReadInt32BigEndian()
                        });
                        break;
                    case "indx": // 索引引用
                        refs.Add(new
                        {
                            RefType = "Index",
                            Value = reader.ReadInt32BigEndian()
                        });
                        break;
                    case "name": // 名称引用
                        refs.Add(new
                        {
                            RefType = "Name",
                            ClassName = reader.ReadUnicodeString(),
                            ClassID = reader.ReadLengthPrefixedString(),
                            Name = reader.ReadUnicodeString()
                        });
                        break;
                    default:
                        refs.Add($"<unknown-ref:{refType}>");
                        break;
                }
            }
            return refs;
        }
    }
}
