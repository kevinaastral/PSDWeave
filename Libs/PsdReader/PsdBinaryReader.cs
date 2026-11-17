// PSD Reader - PSD 大端序二进制读取器
// PSD 文件使用大端序 (Big-Endian)，.NET 默认小端序，所以需要字节翻转

using System;
using System.IO;
using System.Text;

namespace PsdReader
{
    /// <summary>
    /// PSD 专用二进制读取器。
    /// 处理大端序读取、PSD 字符串格式、签名校验等 PSD 文件特有的操作。
    /// Version=1 表示 PSD 格式，Version=2 表示 PSB（大文件）格式。
    /// </summary>
    internal class PsdBinaryReader : IDisposable
    {
        private readonly BinaryReader _reader;
        private readonly Stream _stream;

        /// <summary>
        /// PSD 版本：1 = PSD (最大 30000×30000), 2 = PSB (最大 300000×300000)
        /// PSB 的长度字段用 64 位而非 32 位
        /// </summary>
        public int Version { get; set; } = 1;

        public long Position
        {
            get => _stream.Position;
            set => _stream.Position = value;
        }

        public long Length => _stream.Length;

        public Stream BaseStream => _stream;

        public PsdBinaryReader(Stream stream)
        {
            _stream = stream;
            _reader = new BinaryReader(stream);
        }

        public void Dispose()
        {
            _reader.Close();
        }

        #region 签名校验

        /// <summary>
        /// 读取 4 字节 ASCII 签名
        /// </summary>
        public string ReadSignature() => ReadAsciiString(4);

        /// <summary>
        /// 校验 PSD 文件头的 "8BPS" 签名并读取版本号
        /// </summary>
        public void ValidateDocumentHeader()
        {
            string sig = ReadSignature();
            if (sig != "8BPS")
                throw new PsdParsingException($"无效的 PSD 签名: '{sig}'，应为 '8BPS'");
            Version = ReadInt16BigEndian();
        }

        /// <summary>
        /// 校验图像资源签名 "8BIM"
        /// </summary>
        public bool ValidateResourceSignature()
        {
            string sig = ReadSignature();
            return sig == "8BIM";
        }

        /// <summary>
        /// 校验图层签名 "8BIM" 或 "8B64" (PSB)
        /// 如果签名不匹配，恢复读取位置（peek语义）
        /// </summary>
        public bool ValidateLayerSignature()
        {
            long savedPos = _stream.Position;
            string sig = ReadSignature();
            if (sig == "8BIM" || sig == "8B64")
                return true;
            // 签名不匹配，恢复位置
            _stream.Position = savedPos;
            return false;
        }

        #endregion

        #region 大端序基本类型读取

        public byte ReadByte() => _reader.ReadByte();
        public byte[] ReadBytes(int count) => _reader.ReadBytes(count);
        public char ReadChar() => (char)ReadByte();

        /// <summary>
        /// 读取指定数量的字节到已有缓冲区中（避免分配新数组）
        /// </summary>
        public int ReadBytesInto(byte[] buffer, int count)
        {
            int total = 0;
            while (total < count)
            {
                int read = _stream.Read(buffer, total, count - total);
                if (read == 0) break;
                total += read;
            }
            return total;
        }

        public short ReadInt16BigEndian()
        {
            byte b0 = _reader.ReadByte();
            byte b1 = _reader.ReadByte();
            return (short)((b0 << 8) | b1);
        }

        public ushort ReadUInt16BigEndian()
        {
            byte b0 = _reader.ReadByte();
            byte b1 = _reader.ReadByte();
            return (ushort)((b0 << 8) | b1);
        }

        public int ReadInt32BigEndian()
        {
            byte b0 = _reader.ReadByte();
            byte b1 = _reader.ReadByte();
            byte b2 = _reader.ReadByte();
            byte b3 = _reader.ReadByte();
            return (b0 << 24) | (b1 << 16) | (b2 << 8) | b3;
        }

        public uint ReadUInt32BigEndian()
        {
            byte b0 = _reader.ReadByte();
            byte b1 = _reader.ReadByte();
            byte b2 = _reader.ReadByte();
            byte b3 = _reader.ReadByte();
            return ((uint)b0 << 24) | ((uint)b1 << 16) | ((uint)b2 << 8) | b3;
        }

        public long ReadInt64BigEndian()
        {
            byte b0 = _reader.ReadByte();
            byte b1 = _reader.ReadByte();
            byte b2 = _reader.ReadByte();
            byte b3 = _reader.ReadByte();
            byte b4 = _reader.ReadByte();
            byte b5 = _reader.ReadByte();
            byte b6 = _reader.ReadByte();
            byte b7 = _reader.ReadByte();
            return ((long)b0 << 56) | ((long)b1 << 48) | ((long)b2 << 40) | ((long)b3 << 32)
                 | ((long)b4 << 24) | ((long)b5 << 16) | ((long)b6 << 8) | b7;
        }

        public ulong ReadUInt64BigEndian()
        {
            byte b0 = _reader.ReadByte();
            byte b1 = _reader.ReadByte();
            byte b2 = _reader.ReadByte();
            byte b3 = _reader.ReadByte();
            byte b4 = _reader.ReadByte();
            byte b5 = _reader.ReadByte();
            byte b6 = _reader.ReadByte();
            byte b7 = _reader.ReadByte();
            return ((ulong)b0 << 56) | ((ulong)b1 << 48) | ((ulong)b2 << 40) | ((ulong)b3 << 32)
                 | ((ulong)b4 << 24) | ((ulong)b5 << 16) | ((ulong)b6 << 8) | b7;
        }

        public double ReadDoubleBigEndian()
        {
            byte[] b = _reader.ReadBytes(8);
            Array.Reverse(b);
            return BitConverter.ToDouble(b, 0);
        }

        public bool ReadBoolean() => ReadByte() != 0;

        #endregion

        #region PSD 版本相关

        /// <summary>
        /// 读取版本相关的长度字段：PSD=32位, PSB=64位
        /// </summary>
        public long ReadVersionDependentLength()
        {
            return Version == 1 ? ReadInt32BigEndian() : ReadInt64BigEndian();
        }

        #endregion

        #region PSD 字符串格式

        /// <summary>
        /// 读取 ASCII 字符串
        /// </summary>
        public string ReadAsciiString(int count)
        {
            return Encoding.ASCII.GetString(_reader.ReadBytes(count));
        }

        /// <summary>
        /// 读取 Pascal 风格字符串 (length-prefixed, 带对齐填充)
        /// </summary>
        public string ReadPascalString(int paddingSize)
        {
            byte len = ReadByte();
            if (len == 0)
            {
                // 空字符串，跳过填充字节
                _stream.Position += paddingSize - 1;
                return string.Empty;
            }

            string result = Encoding.Default.GetString(ReadBytes(len));

            // 对齐到 paddingSize 的倍数
            int total = len + 1;
            while (total % paddingSize != 0)
            {
                _stream.Position++;
                total++;
            }
            return result;
        }

        /// <summary>
        /// 读取 Unicode 字符串 (大端序 UTF-16，长度前缀为字符数)
        /// </summary>
        public string ReadUnicodeString()
        {
            int charCount = ReadInt32BigEndian();
            if (charCount == 0)
                return string.Empty;

            byte[] bytes = ReadBytes(charCount * 2);

            // PSD 用大端序 UTF-16，需要逐字翻转字节对
            for (int i = 0; i < charCount; i++)
            {
                int idx = i * 2;
                byte tmp = bytes[idx];
                bytes[idx] = bytes[idx + 1];
                bytes[idx + 1] = tmp;
            }

            // 去掉末尾的空字符
            if (bytes.Length >= 2 && bytes[^1] == 0 && bytes[^2] == 0)
                charCount--;

            return Encoding.Unicode.GetString(bytes, 0, charCount * 2);
        }

        /// <summary>
        /// 读取 4字节长度前缀 + ASCII 字符串 (PSD 的 "LengthPrefixedString")
        /// </summary>
        public string ReadLengthPrefixedString()
        {
            int length = ReadInt32BigEndian();
            if (length <= 0)
                length = 4;
            return ReadAsciiString(length);
        }

        #endregion

        #region PSD 类型读取

        public ColorMode ReadColorMode() => (ColorMode)ReadInt16BigEndian();
        public ChannelType ReadChannelType() => (ChannelType)ReadInt16BigEndian();
        public CompressionType ReadCompressionType() => (CompressionType)ReadInt16BigEndian();
        public LayerFlags ReadLayerFlags() => (LayerFlags)ReadByte();

        public BlendMode ReadBlendMode()
        {
            return PsdUtils.ParseBlendMode(ReadAsciiString(4));
        }

        #endregion

        #region 工具方法

        public void SkipBytes(int count) => _stream.Position += count;

        public int ReadBuffer(byte[] buffer, int offset, int count)
        {
            return _reader.Read(buffer, offset, count);
        }

        public double[] ReadDoubleArray(int count)
        {
            var arr = new double[count];
            for (int i = 0; i < count; i++)
                arr[i] = ReadDoubleBigEndian();
            return arr;
        }

        public void ExpectChar(char expected)
        {
            char c = ReadChar();
            if (c != expected)
                throw new PsdParsingException($"期望字符 '{expected}'，实际为 '{c}'");
        }

        public void ExpectChars(char expected, int count)
        {
            for (int i = 0; i < count; i++)
                ExpectChar(expected);
        }

        #endregion
    }
}
