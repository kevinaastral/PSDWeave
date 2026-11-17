// PSD Reader - 懒加载机制
// PSD 文件可以很大，懒加载确保只读取真正需要的部分

using System;

namespace PsdReader
{
    /// <summary>
    /// 懒加载节。
    /// 记录流的当前位置和解析函数，首次访问 .Value 时才真正读取数据。
    /// 读取完成后缓存结果，后续访问直接返回缓存。
    /// </summary>
    internal class LazySection<T>
    {
        private readonly PsdBinaryReader _reader;
        private readonly Func<PsdBinaryReader, T> _parseFunc;
        private readonly long _position;
        private T _value;
        private bool _loaded;

        public LazySection(PsdBinaryReader reader, Func<PsdBinaryReader, T> parseFunc)
        {
            _reader = reader;
            _parseFunc = parseFunc;
            _position = reader.Position; // 记录当前流位置
        }

        /// <summary>
        /// 获取解析后的值。首次访问时执行解析，之后返回缓存。
        /// </summary>
        public T Value
        {
            get
            {
                if (!_loaded)
                {
                    long savedPos = _reader.Position;
                    _reader.Position = _position;         // 回到节的起始位置
                    _value = _parseFunc(_reader);          // 执行解析
                    _reader.Position = savedPos;           // 恢复原位
                    _loaded = true;
                }
                return _value;
            }
        }

        /// <summary>
        /// 跳过此节（移动流位置到节末尾）
        /// </summary>
        public void Skip(PsdBinaryReader reader, long length)
        {
            reader.Position = _position + length;
        }

        public bool IsLoaded => _loaded;
    }

    /// <summary>
    /// 带长度信息的懒加载节
    /// </summary>
    internal class LazySectionWithLength<T>
    {
        private readonly PsdBinaryReader _reader;
        private readonly Func<PsdBinaryReader, long, T> _parseFunc;
        private readonly long _position;
        private readonly long _length;
        private T _value;
        private bool _loaded;

        public LazySectionWithLength(PsdBinaryReader reader, long length, Func<PsdBinaryReader, long, T> parseFunc)
        {
            _reader = reader;
            _parseFunc = parseFunc;
            _position = reader.Position;
            _length = length;
        }

        public T Value
        {
            get
            {
                if (!_loaded)
                {
                    long saved = _reader.Position;
                    _reader.Position = _position;
                    _value = _parseFunc(_reader, _length);
                    _reader.Position = saved;
                    _loaded = true;
                }
                return _value;
            }
        }

        public long Length => _length;
        public long EndPosition => _position + _length;
        public bool IsLoaded => _loaded;
    }
}
