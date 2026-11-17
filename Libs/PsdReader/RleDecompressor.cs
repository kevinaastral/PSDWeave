// PSD Reader - RLE 解压器
// PSD 使用 Apple PackBits RLE 压缩算法

using System;

namespace PsdReader
{
    /// <summary>
    /// PackBits RLE 解压算法。
    /// 
    /// 编码规则:
    ///   控制字节 n:
    ///     0..127   → 后续 (n+1) 个字节为原始数据（字面复制）
    ///     -1..-127 → 后续 1 个字节重复 (1-n) 次（行程编码）
    ///     -128     → NOP（忽略）
    /// </summary>
    internal static class RleDecompressor
    {
        /// <summary>
        /// 解压 PackBits 编码的数据
        /// </summary>
        /// <param name="src">压缩源数据</param>
        /// <param name="dst">解压目标缓冲区</param>
        /// <param name="packedLength">压缩数据长度</param>
        /// <param name="unpackedLength">期望的解压后长度</param>
        public static void Decompress(byte[] src, byte[] dst, int packedLength, int unpackedLength)
        {
            int srcIdx = 0;
            int dstIdx = 0;
            int srcEnd = packedLength;

            while (dstIdx < unpackedLength && srcIdx < srcEnd)
            {
                // 读取控制字节 (有符号)
                sbyte control = (sbyte)src[srcIdx++];

                if (control == -128)
                {
                    // NOP — 跳过
                    continue;
                }

                if (control >= 0)
                {
                    // 字面值复制: (control + 1) 个字节
                    int count = control + 1;
                    int toCopy = Math.Min(count, Math.Min(unpackedLength - dstIdx, srcEnd - srcIdx));
                    Buffer.BlockCopy(src, srcIdx, dst, dstIdx, toCopy);
                    srcIdx += toCopy;
                    dstIdx += toCopy;
                }
                else
                {
                    // 行程编码: 重复下一个字节 (1 - control) 次
                    int count = 1 - control;
                    byte value = src[srcIdx++];
                    int toFill = Math.Min(count, unpackedLength - dstIdx);
                    // 批量填充代替逐字节赋值
                    if (toFill > 0)
                    {
                        for (int end = dstIdx + toFill; dstIdx < end;)
                            dst[dstIdx++] = value;
                    }
                }
            }

            // 不足部分用 0 填充
            while (dstIdx < unpackedLength)
                dst[dstIdx++] = 0;
        }

        /// <summary>
        /// 解压通道的 RLE 数据（带行长度表）
        /// </summary>
        public static byte[] DecompressChannel(byte[] compressedData, int[] rowPackLengths, int rowByteCount, int height)
        {
            int totalBytes = rowByteCount * height;
            byte[] result = new byte[totalBytes];

            // 预分配行缓冲区，避免每行分配临时数组
            int maxPackedLen = 0;
            for (int i = 0; i < height; i++)
                if (rowPackLengths[i] > maxPackedLen) maxPackedLen = rowPackLengths[i];

            byte[] rowSrc = new byte[maxPackedLen];
            byte[] rowDst = new byte[rowByteCount];

            int srcOffset = 0;
            for (int row = 0; row < height; row++)
            {
                int packedLen = rowPackLengths[row];
                Buffer.BlockCopy(compressedData, srcOffset, rowSrc, 0, packedLen);
                Decompress(rowSrc, rowDst, packedLen, rowByteCount);
                Buffer.BlockCopy(rowDst, 0, result, row * rowByteCount, rowByteCount);
                srcOffset += packedLen;
            }

            return result;
        }
    }
}
