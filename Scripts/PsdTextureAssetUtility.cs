#if UNITY_EDITOR
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    /// <summary>Encodes and imports PSD textures without silently discarding 16-bit channel data.</summary>
    internal static class PsdTextureAssetUtility
    {
        private const string ExportModePrefix = "Psd2UIExportMode:";

        internal static byte[] EncodePng(Texture2D texture)
        {
            if (texture == null) return null;
            if (texture.format != TextureFormat.RGBA64)
                return texture.EncodeToPNG();

            return EncodeRgba64Png(texture.GetRawTextureData<byte>().ToArray(), texture.width, texture.height);
        }

        internal static void ApplyPrecisionImportSettings(TextureImporter importer, bool preferHighBitDepth)
        {
            if (importer == null) return;

            if (preferHighBitDepth)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.crunchedCompression = false;
                ApplyRgba64OverrideIfSupported(importer, "DefaultTexturePlatform", BuildTarget.NoTarget);

                BuildTarget activeTarget = EditorUserBuildSettings.activeBuildTarget;
                if (activeTarget != BuildTarget.NoTarget)
                {
                    string platformName = BuildPipeline.GetBuildTargetName(activeTarget);
                    if (!string.IsNullOrEmpty(platformName))
                        ApplyRgba64OverrideIfSupported(importer, platformName, activeTarget);
                }
            }

            importer.userData = MergeModeTag(importer.userData, preferHighBitDepth);
        }

        internal static bool MatchesExportMode(string assetPath, bool preferHighBitDepth)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null || string.IsNullOrWhiteSpace(importer.userData)) return false;
            string expected = BuildModeTag(preferHighBitDepth);
            string[] lines = importer.userData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
                if (string.Equals(lines[i].Trim(), expected, StringComparison.Ordinal)) return true;
            return false;
        }

        private static void ApplyRgba64OverrideIfSupported(TextureImporter importer, string platformName,
            BuildTarget target)
        {
            bool supported = target == BuildTarget.NoTarget
                ? TextureImporter.IsDefaultPlatformTextureFormatValid(importer.textureType,
                    TextureImporterFormat.RGBA64)
                : TextureImporter.IsPlatformTextureFormatValid(importer.textureType, target,
                    TextureImporterFormat.RGBA64);
            if (!supported) return;

            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platformName);
            settings.name = platformName;
            settings.overridden = true;
            settings.format = TextureImporterFormat.RGBA64;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            settings.allowsAlphaSplitting = false;
            importer.SetPlatformTextureSettings(settings);
        }

        private static string MergeModeTag(string userData, bool highBitDepth)
        {
            string expected = BuildModeTag(highBitDepth);
            if (string.IsNullOrWhiteSpace(userData)) return expected;

            string[] lines = userData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith(ExportModePrefix, StringComparison.Ordinal))
                    return userData.Replace(line, expected);
            }
            return userData + "\n" + expected;
        }

        private static string BuildModeTag(bool highBitDepth)
            => ExportModePrefix + (highBitDepth ? "16" : "8");

        private static byte[] EncodeRgba64Png(byte[] littleEndianRgba64, int width, int height)
        {
            if (littleEndianRgba64 == null || littleEndianRgba64.Length != checked(width * height * 8))
                throw new ArgumentException("RGBA64 data length does not match the texture dimensions.");

            int rowBytes = checked(width * 8);
            var scanlines = new byte[checked((rowBytes + 1) * height)];
            for (int pngRow = 0; pngRow < height; pngRow++)
            {
                int sourceRow = height - 1 - pngRow;
                int sourceOffset = sourceRow * rowBytes;
                int destinationOffset = pngRow * (rowBytes + 1) + 1;
                for (int i = 0; i < rowBytes; i += 2)
                {
                    // PNG stores 16-bit samples in network byte order.
                    scanlines[destinationOffset + i] = littleEndianRgba64[sourceOffset + i + 1];
                    scanlines[destinationOffset + i + 1] = littleEndianRgba64[sourceOffset + i];
                }
            }

            byte[] compressed = CompressZlib(scanlines);
            using (var output = new MemoryStream())
            {
                output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
                var header = new byte[13];
                WriteUInt32BigEndian(header, 0, (uint)width);
                WriteUInt32BigEndian(header, 4, (uint)height);
                header[8] = 16;
                header[9] = 6;
                WriteChunk(output, "IHDR", header);
                WriteChunk(output, "IDAT", compressed);
                WriteChunk(output, "IEND", Array.Empty<byte>());
                return output.ToArray();
            }
        }

        private static byte[] CompressZlib(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                output.WriteByte(0x78);
                output.WriteByte(0x9C);
                using (var deflate = new DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
                    deflate.Write(data, 0, data.Length);

                uint adler = ComputeAdler32(data);
                var checksum = new byte[4];
                WriteUInt32BigEndian(checksum, 0, adler);
                output.Write(checksum, 0, checksum.Length);
                return output.ToArray();
            }
        }

        private static void WriteChunk(Stream output, string type, byte[] data)
        {
            var length = new byte[4];
            WriteUInt32BigEndian(length, 0, (uint)data.Length);
            output.Write(length, 0, length.Length);

            byte[] typeBytes = Encoding.ASCII.GetBytes(type);
            output.Write(typeBytes, 0, typeBytes.Length);
            output.Write(data, 0, data.Length);

            uint crc = ComputeCrc32(typeBytes, data);
            var checksum = new byte[4];
            WriteUInt32BigEndian(checksum, 0, crc);
            output.Write(checksum, 0, checksum.Length);
        }

        private static uint ComputeAdler32(byte[] data)
        {
            const uint modulus = 65521;
            uint a = 1;
            uint b = 0;
            for (int i = 0; i < data.Length; i++)
            {
                a = (a + data[i]) % modulus;
                b = (b + a) % modulus;
            }
            return (b << 16) | a;
        }

        private static uint ComputeCrc32(byte[] type, byte[] data)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < type.Length; i++) crc = UpdateCrc32(crc, type[i]);
            for (int i = 0; i < data.Length; i++) crc = UpdateCrc32(crc, data[i]);
            return crc ^ 0xFFFFFFFFu;
        }

        private static uint UpdateCrc32(uint crc, byte value)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc & 1u) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            return crc;
        }

        private static void WriteUInt32BigEndian(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }
    }
}
#endif
