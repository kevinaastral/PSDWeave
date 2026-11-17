using System;
using System.IO;
using System.IO.Compression;

namespace PsdReader
{
    /// <summary>Decoded data for one PSD color, alpha, or mask channel.</summary>
    public class Channel : IChannel
    {
        /// <summary>8-bit samples, or a rounded 8-bit compatibility view of 16-bit samples.</summary>
        public byte[] Data { get; internal set; }

        /// <summary>Exact 16-bit samples in host byte order; null for other depths.</summary>
        public ushort[] Data16 { get; internal set; }

        public ChannelType Type { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public float Opacity { get; set; } = 1f;
        public long Size { get; set; }
        internal int[] RlePackLengths { get; set; }

        public Channel() { }

        public Channel(ChannelType type, int width, int height, long size)
        {
            Type = type;
            Width = width;
            Height = height;
            Size = size;
        }

        internal void ReadRleHeader(PsdBinaryReader reader, CompressionType compression)
        {
            if (compression != CompressionType.RLE)
                return;

            RlePackLengths = new int[Height];
            for (int i = 0; i < Height; i++)
            {
                RlePackLengths[i] = reader.Version == 1
                    ? reader.ReadInt16BigEndian()
                    : reader.ReadInt32BigEndian();
            }
        }

        internal void ReadData(PsdBinaryReader reader, int bitsPerSample, CompressionType compression,
            int packedDataLength = -1)
        {
            int rowBytes = PsdUtils.CalculateRowByteCount(bitsPerSample, Width);
            int totalBytes = checked(rowBytes * Height);
            byte[] rawData;

            if (compression == CompressionType.Raw)
            {
                rawData = reader.ReadBytes(totalBytes);
            }
            else if (compression == CompressionType.RLE)
            {
                rawData = ReadRleData(reader, rowBytes, totalBytes);
            }
            else if (compression == CompressionType.Zip || compression == CompressionType.ZipPrediction)
            {
                if (packedDataLength < 0)
                    throw new PsdParsingException("ZIP channel data is missing its packed length.");

                rawData = InflateZlib(reader.ReadBytes(packedDataLength), totalBytes);
                if (compression == CompressionType.ZipPrediction)
                    DecodeZipPrediction(rawData, Width, Height, bitsPerSample);
            }
            else
            {
                throw new NotSupportedException($"Unsupported PSD channel compression: {compression}");
            }

            SetData(rawData, bitsPerSample);
        }

        internal void SetData(byte[] rawData, int bitsPerSample)
        {
            if (rawData == null)
                throw new ArgumentNullException(nameof(rawData));

            if (bitsPerSample == 16)
            {
                int sampleCount = checked(Width * Height);
                if (rawData.Length < sampleCount * 2)
                    throw new PsdParsingException("16-bit channel data is shorter than expected.");

                Data16 = new ushort[sampleCount];
                Data = new byte[sampleCount];
                for (int i = 0, source = 0; i < sampleCount; i++, source += 2)
                {
                    ushort value = (ushort)((rawData[source] << 8) | rawData[source + 1]);
                    Data16[i] = value;
                    Data[i] = ToByte(value);
                }
                return;
            }

            if (bitsPerSample == 32)
            {
                int sampleCount = checked(Width * Height);
                if (rawData.Length < sampleCount * 4)
                    throw new PsdParsingException("32-bit channel data is shorter than expected.");

                Data16 = null;
                Data = new byte[sampleCount];
                var floatBytes = new byte[4];
                for (int i = 0, source = 0; i < sampleCount; i++, source += 4)
                {
                    if (BitConverter.IsLittleEndian)
                    {
                        floatBytes[0] = rawData[source + 3];
                        floatBytes[1] = rawData[source + 2];
                        floatBytes[2] = rawData[source + 1];
                        floatBytes[3] = rawData[source];
                    }
                    else
                    {
                        Buffer.BlockCopy(rawData, source, floatBytes, 0, 4);
                    }

                    float value = BitConverter.ToSingle(floatBytes, 0);
                    if (float.IsNaN(value)) value = 0f;
                    value = Math.Max(0f, Math.Min(1f, value));
                    Data[i] = (byte)Math.Round(value * 255f, MidpointRounding.AwayFromZero);
                }
                return;
            }

            Data16 = null;
            Data = rawData;
        }

        private byte[] ReadRleData(PsdBinaryReader reader, int rowBytes, int totalBytes)
        {
            if (RlePackLengths == null || RlePackLengths.Length != Height)
                throw new PsdParsingException("PSD RLE row lengths are missing or invalid.");

            var result = new byte[totalBytes];
            int maxPackedLength = 0;
            for (int i = 0; i < Height; i++)
                maxPackedLength = Math.Max(maxPackedLength, RlePackLengths[i]);

            var packedBuffer = new byte[maxPackedLength];
            var rowBuffer = new byte[rowBytes];
            int destinationOffset = 0;
            for (int row = 0; row < Height; row++)
            {
                int packedLength = RlePackLengths[row];
                reader.ReadBytesInto(packedBuffer, packedLength);
                RleDecompressor.Decompress(packedBuffer, rowBuffer, packedLength, rowBytes);
                Buffer.BlockCopy(rowBuffer, 0, result, destinationOffset, rowBytes);
                destinationOffset += rowBytes;
            }

            return result;
        }

        private static byte[] InflateZlib(byte[] packedData, int expectedLength)
        {
            if (packedData == null || packedData.Length < 6)
                throw new PsdParsingException("Invalid PSD zlib stream.");

            int cmf = packedData[0];
            int flg = packedData[1];
            if ((cmf & 0x0F) != 8 || ((cmf << 8) + flg) % 31 != 0 || (flg & 0x20) != 0)
                throw new PsdParsingException("Unsupported PSD zlib header.");

            var result = new byte[expectedLength];
            try
            {
                // PSD stores a zlib wrapper. Unity's DeflateStream consumes the raw DEFLATE payload.
                using (var compressed = new MemoryStream(packedData, 2, packedData.Length - 6, false))
                using (var inflater = new DeflateStream(compressed, CompressionMode.Decompress, false))
                {
                    int offset = 0;
                    while (offset < result.Length)
                    {
                        int read = inflater.Read(result, offset, result.Length - offset);
                        if (read <= 0) break;
                        offset += read;
                    }

                    if (offset != result.Length)
                        throw new PsdParsingException(
                            $"PSD ZIP channel expanded to {offset} bytes; expected {result.Length}.");
                }
            }
            catch (PsdParsingException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PsdParsingException("Failed to inflate PSD ZIP channel data.", ex);
            }

            return result;
        }

        private static void DecodeZipPrediction(byte[] data, int width, int height, int bitsPerSample)
        {
            int rowBytes = PsdUtils.CalculateRowByteCount(bitsPerSample, width);
            for (int row = 0; row < height; row++)
            {
                int rowStart = row * rowBytes;
                int rowEnd = rowStart + rowBytes;
                for (int i = rowStart + 1; i < rowEnd; i++)
                    data[i] = unchecked((byte)(data[i] + data[i - 1]));

                int bytesPerSample = bitsPerSample / 8;
                if (bytesPerSample != 2 && bytesPerSample != 4)
                    continue;

                // Photoshop predicts byte planes first, then stores multi-byte samples interleaved.
                var interleaved = new byte[rowBytes];
                for (int x = 0; x < width; x++)
                {
                    for (int plane = 0; plane < bytesPerSample; plane++)
                        interleaved[x * bytesPerSample + plane] = data[rowStart + plane * width + x];
                }
                Buffer.BlockCopy(interleaved, 0, data, rowStart, rowBytes);
            }
        }

        private static byte ToByte(ushort value)
        {
            return (byte)((value + 128u) / 257u);
        }
    }
}
