// PSD Reader - 受限流包装器
// 将一个大的 Stream 限制在指定区间内读取，防止越界

using System;
using System.IO;

namespace PsdReader
{
    /// <summary>
    /// 受限流包装器。
    /// 在底层 Stream 上划定一个 [offset, offset+length) 的窗口，
    /// 所有 Read/Seek 操作都限制在此窗口内。
    /// 用于 PSD 的分节解析：每节有明确的起始和长度。
    /// </summary>
    internal class BoundedStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _offset;
        private readonly long _length;

        public BoundedStream(Stream baseStream, long offset, long length)
        {
            _baseStream = baseStream;
            _offset = offset;
            _length = length;
            _baseStream.Position = offset;
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _length;

        public override long Position
        {
            get => _baseStream.Position - _offset;
            set => _baseStream.Position = _offset + Math.Min(value, _length);
        }

        public long Remaining => _length - Position;

        public override int Read(byte[] buffer, int offset, int count)
        {
            long remaining = _length - Position;
            if (remaining <= 0) return 0;
            int toRead = (int)Math.Min(count, remaining);
            return _baseStream.Read(buffer, offset, toRead);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPos;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    newPos = offset;
                    break;
                case SeekOrigin.Current:
                    newPos = Position + offset;
                    break;
                case SeekOrigin.End:
                    newPos = _length + offset;
                    break;
                default:
                    throw new ArgumentException("无效的 SeekOrigin");
            }
            Position = Math.Max(0, Math.Min(newPos, _length));
            return Position;
        }

        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
