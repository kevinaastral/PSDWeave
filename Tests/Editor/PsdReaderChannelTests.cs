using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace PsdReader.Tests
{
    public sealed class PsdReaderChannelTests
    {
        [Test]
        public void SetData_PreservesExact16BitSamples_AndBuildsCompatibilityView()
        {
            var channel = new Channel(ChannelType.Red, 3, 1, 0);

            channel.SetData(new byte[] { 0x00, 0x00, 0x12, 0x34, 0xFF, 0xFF }, 16);

            CollectionAssert.AreEqual(new ushort[] { 0x0000, 0x1234, 0xFFFF }, channel.Data16);
            CollectionAssert.AreEqual(new byte[] { 0x00, 0x12, 0xFF }, channel.Data);
        }

        [Test]
        public void ReadData_DecodesZipChannel()
        {
            byte[] expected = { 1, 2, 3, 4, 250, 255 };
            byte[] packed = WrapZlib(expected);
            var channel = new Channel(ChannelType.Red, 3, 2, packed.Length + 2);

            using (var stream = new MemoryStream(packed))
            using (var reader = new PsdBinaryReader(stream))
                channel.ReadData(reader, 8, CompressionType.Zip, packed.Length);

            CollectionAssert.AreEqual(expected, channel.Data);
        }

        [Test]
        public void ReadData_DecodesEightBitZipPredictionPerRow()
        {
            byte[] predicted = { 10, 10, 5, 1, 1, 2 };
            byte[] packed = WrapZlib(predicted);
            var channel = new Channel(ChannelType.Red, 3, 2, packed.Length + 2);

            using (var stream = new MemoryStream(packed))
            using (var reader = new PsdBinaryReader(stream))
                channel.ReadData(reader, 8, CompressionType.ZipPrediction, packed.Length);

            CollectionAssert.AreEqual(new byte[] { 10, 20, 25, 1, 2, 4 }, channel.Data);
        }

        [Test]
        public void ReadData_DecodesSixteenBitZipPredictionAndBytePlaneShuffle()
        {
            // Predicted form of byte planes [12,56,9A] [34,78,BC].
            byte[] predicted = { 0x12, 0x44, 0x44, 0x9A, 0x44, 0x44 };
            byte[] packed = WrapZlib(predicted);
            var channel = new Channel(ChannelType.Red, 3, 1, packed.Length + 2);

            using (var stream = new MemoryStream(packed))
            using (var reader = new PsdBinaryReader(stream))
                channel.ReadData(reader, 16, CompressionType.ZipPrediction, packed.Length);

            CollectionAssert.AreEqual(new ushort[] { 0x1234, 0x5678, 0x9ABC }, channel.Data16);
        }

        [Test]
        public void SetData_ConvertsThirtyTwoBitFloatSamplesToCompatibilityView()
        {
            var channel = new Channel(ChannelType.Red, 3, 1, 0);

            channel.SetData(new byte[]
            {
                0x00, 0x00, 0x00, 0x00,
                0x3F, 0x00, 0x00, 0x00,
                0x3F, 0x80, 0x00, 0x00,
            }, 32);

            CollectionAssert.AreEqual(new byte[] { 0, 128, 255 }, channel.Data);
            Assert.That(channel.Data16, Is.Null);
        }

        [Test]
        public void RenderedImage_CompositesSixteenBitWithoutEightBitTruncation()
        {
            var image = new RenderedImage(0, 0, 1, 1, 16);
            image.SetPixel(0, 0, (ushort)0x1234, (ushort)0x5678, (ushort)0x9ABC, ushort.MaxValue);

            Assert.That(image.IsHighBitDepth, Is.True);
            CollectionAssert.AreEqual(new ushort[] { 0x1234, 0x5678, 0x9ABC, 0xFFFF }, image.Rgba64);
            Assert.That(image.Rgba32[3], Is.EqualTo(byte.MaxValue));
        }

        [Test]
        public void TextureUtility_EncodesActualSixteenBitRgbaPng()
        {
            Assembly editorAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .First(item => item.GetName().Name == "cn.efunstudio.psd2ugui");
            Type utilityType = editorAssembly.GetType(
                "UGF.EditorTools.Psd2UGUI.PsdTextureAssetUtility", true);
            MethodInfo encodeMethod = utilityType.GetMethod("EncodePng",
                BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(Texture2D) }, null);
            Assert.That(encodeMethod, Is.Not.Null);

            var texture = new Texture2D(1, 1, TextureFormat.RGBA64, false);
            var decoded = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            try
            {
                ushort[] samples = { 0x1234, 0x5678, 0x9ABC, 0xFFFF };
                var raw = new byte[samples.Length * sizeof(ushort)];
                Buffer.BlockCopy(samples, 0, raw, 0, raw.Length);
                texture.LoadRawTextureData(raw);
                texture.Apply();

                byte[] png = (byte[])encodeMethod.Invoke(null, new object[] { texture });
                Assert.That(png, Is.Not.Null);
                Assert.That(png.Length, Is.GreaterThan(28));
                Assert.That(png[24], Is.EqualTo(16), "PNG IHDR bit depth");
                Assert.That(png[25], Is.EqualTo(6), "PNG IHDR RGBA color type");
                Assert.That(ImageConversion.LoadImage(decoded, png, false), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(decoded);
            }
        }

        private static byte[] WrapZlib(byte[] raw)
        {
            using (var payload = new MemoryStream())
            {
                payload.WriteByte(0x78);
                payload.WriteByte(0x9C);
                using (var deflate = new DeflateStream(payload,
                    System.IO.Compression.CompressionLevel.Optimal, true))
                    deflate.Write(raw, 0, raw.Length);

                // Channel validates the zlib header; checksum validation is delegated to the PSD producer.
                payload.Write(new byte[4], 0, 4);
                return payload.ToArray();
            }
        }
    }
}
