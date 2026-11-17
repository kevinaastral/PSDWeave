using System;

namespace PsdReader
{
    /// <summary>RGBA render target with lossless 16-bit support and an 8-bit compatibility view.</summary>
    public sealed class RenderedImage
    {
        private readonly byte[] _pixels8;
        private readonly ushort[] _pixels16;
        private byte[] _pixels8Proxy;

        public int Left { get; }
        public int Top { get; }
        public int Width { get; }
        public int Height { get; }
        public int Right => Left + Width;
        public int Bottom => Top + Height;
        public int BitDepth { get; }
        public bool IsHighBitDepth => BitDepth == 16;

        /// <summary>RGBA32 data. A rounded compatibility copy is returned for 16-bit images.</summary>
        public byte[] Pixels
        {
            get
            {
                if (!IsHighBitDepth) return _pixels8;
                if (_pixels8Proxy == null)
                {
                    _pixels8Proxy = new byte[_pixels16.Length];
                    for (int i = 0; i < _pixels16.Length; i++)
                        _pixels8Proxy[i] = ToByte(_pixels16[i]);
                }
                return _pixels8Proxy;
            }
        }

        public byte[] Rgba32 => Pixels;
        public ushort[] Pixels16 => _pixels16;
        public ushort[] Rgba64 => _pixels16;
        internal byte[] RawPixels => _pixels8;
        internal ushort[] RawPixels16 => _pixels16;

        public bool IsEmpty => Width <= 0 || Height <= 0 ||
            (IsHighBitDepth ? _pixels16.Length == 0 : _pixels8.Length == 0);

        public RenderedImage(int left, int top, int width, int height, int bitDepth = 8)
        {
            Left = left;
            Top = top;
            Width = Math.Max(0, width);
            Height = Math.Max(0, height);
            BitDepth = bitDepth == 16 ? 16 : 8;
            int sampleCount = checked(Width * Height * 4);
            _pixels16 = IsHighBitDepth ? new ushort[sampleCount] : Array.Empty<ushort>();
            _pixels8 = IsHighBitDepth ? Array.Empty<byte>() : new byte[sampleCount];
        }

        public int GetPixelOffset(int x, int yTopDown)
        {
            return ((Height - 1 - yTopDown) * Width + x) * 4;
        }

        public void SetPixel(int x, int yTopDown, byte r, byte g, byte b, byte a)
        {
            int offset = GetPixelOffset(x, yTopDown);
            if (IsHighBitDepth)
            {
                _pixels16[offset] = ToUShort(r);
                _pixels16[offset + 1] = ToUShort(g);
                _pixels16[offset + 2] = ToUShort(b);
                _pixels16[offset + 3] = ToUShort(a);
                InvalidateProxy();
                return;
            }

            _pixels8[offset] = r;
            _pixels8[offset + 1] = g;
            _pixels8[offset + 2] = b;
            _pixels8[offset + 3] = a;
        }

        public void SetPixel(int x, int yTopDown, ushort r, ushort g, ushort b, ushort a)
        {
            int offset = GetPixelOffset(x, yTopDown);
            if (!IsHighBitDepth)
            {
                _pixels8[offset] = ToByte(r);
                _pixels8[offset + 1] = ToByte(g);
                _pixels8[offset + 2] = ToByte(b);
                _pixels8[offset + 3] = ToByte(a);
                return;
            }

            _pixels16[offset] = r;
            _pixels16[offset + 1] = g;
            _pixels16[offset + 2] = b;
            _pixels16[offset + 3] = a;
            InvalidateProxy();
        }

        public void CompositePixel(int x, int yTopDown, byte r, byte g, byte b, byte a)
        {
            if (IsHighBitDepth)
            {
                CompositePixel(x, yTopDown, ToUShort(r), ToUShort(g), ToUShort(b), ToUShort(a));
                return;
            }

            Composite8(GetPixelOffset(x, yTopDown), r, g, b, a);
        }

        public void CompositePixel(int x, int yTopDown, ushort r, ushort g, ushort b, ushort a)
        {
            int offset = GetPixelOffset(x, yTopDown);
            if (!IsHighBitDepth)
            {
                Composite8(offset, ToByte(r), ToByte(g), ToByte(b), ToByte(a));
                return;
            }

            Composite16(offset, r, g, b, a);
            InvalidateProxy();
        }

        public byte[] CreateAlphaMask()
        {
            var mask = new byte[Width * Height];
            byte[] pixels = Pixels;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    mask[y * Width + x] = pixels[GetPixelOffset(x, y) + 3];
            return mask;
        }

        public ushort[] CreateAlphaMask16()
        {
            var mask = new ushort[Width * Height];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int offset = GetPixelOffset(x, y) + 3;
                    mask[y * Width + x] = IsHighBitDepth ? _pixels16[offset] : ToUShort(_pixels8[offset]);
                }
            }
            return mask;
        }

        public void MultiplyOpacity(float opacity)
        {
            if (IsEmpty) return;
            opacity = Math.Max(0f, Math.Min(1f, opacity));
            if (opacity >= 0.9999f) return;

            if (IsHighBitDepth)
            {
                for (int i = 3; i < _pixels16.Length; i += 4)
                    _pixels16[i] = MultiplyAlpha(_pixels16[i], opacity);
                InvalidateProxy();
                return;
            }

            for (int i = 3; i < _pixels8.Length; i += 4)
                _pixels8[i] = MultiplyAlpha(_pixels8[i], opacity);
        }

        public void MultiplyPixelAlpha(int x, int yTopDown, byte alphaMask)
        {
            int offset = GetPixelOffset(x, yTopDown) + 3;
            if (IsHighBitDepth)
            {
                _pixels16[offset] = MultiplyAlpha(_pixels16[offset], ToUShort(alphaMask));
                InvalidateProxy();
            }
            else
            {
                _pixels8[offset] = MultiplyAlpha(_pixels8[offset], alphaMask);
            }
        }

        public void MultiplyPixelAlpha(int x, int yTopDown, ushort alphaMask)
        {
            int offset = GetPixelOffset(x, yTopDown) + 3;
            if (IsHighBitDepth)
            {
                _pixels16[offset] = MultiplyAlpha(_pixels16[offset], alphaMask);
                InvalidateProxy();
            }
            else
            {
                _pixels8[offset] = MultiplyAlpha(_pixels8[offset], ToByte(alphaMask));
            }
        }

        internal void InvalidateProxy()
        {
            _pixels8Proxy = null;
        }

        internal static byte MultiplyAlpha(byte alpha, float opacity)
        {
            return (byte)Math.Round(alpha * Math.Max(0f, Math.Min(1f, opacity)),
                MidpointRounding.AwayFromZero);
        }

        internal static ushort MultiplyAlpha(ushort alpha, float opacity)
        {
            return (ushort)Math.Round(alpha * Math.Max(0f, Math.Min(1f, opacity)),
                MidpointRounding.AwayFromZero);
        }

        internal static byte MultiplyAlpha(byte alpha, byte mask)
        {
            return (byte)((alpha * mask + 127) / 255);
        }

        internal static ushort MultiplyAlpha(ushort alpha, ushort mask)
        {
            return (ushort)(((ulong)alpha * mask + 32767UL) / 65535UL);
        }

        internal static byte ToByte(ushort value)
        {
            return (byte)((value + 128u) / 257u);
        }

        internal static ushort ToUShort(byte value)
        {
            return (ushort)(value * 257);
        }

        private void Composite8(int offset, byte srcR, byte srcG, byte srcB, byte srcA)
        {
            if (srcA == 0) return;
            byte dstA = _pixels8[offset + 3];
            if (dstA == 0 || srcA == 255)
            {
                _pixels8[offset] = srcR;
                _pixels8[offset + 1] = srcG;
                _pixels8[offset + 2] = srcB;
                _pixels8[offset + 3] = srcA;
                return;
            }

            int inverseSourceAlpha = 255 - srcA;
            int outAlpha = srcA + (dstA * inverseSourceAlpha + 127) / 255;
            int denominator = outAlpha * 255;
            int half = denominator >> 1;
            _pixels8[offset] = (byte)((srcR * srcA * 255 + _pixels8[offset] * dstA * inverseSourceAlpha + half) / denominator);
            _pixels8[offset + 1] = (byte)((srcG * srcA * 255 + _pixels8[offset + 1] * dstA * inverseSourceAlpha + half) / denominator);
            _pixels8[offset + 2] = (byte)((srcB * srcA * 255 + _pixels8[offset + 2] * dstA * inverseSourceAlpha + half) / denominator);
            _pixels8[offset + 3] = (byte)outAlpha;
        }

        private void Composite16(int offset, ushort srcR, ushort srcG, ushort srcB, ushort srcA)
        {
            if (srcA == 0) return;
            ushort dstA = _pixels16[offset + 3];
            if (dstA == 0 || srcA == ushort.MaxValue)
            {
                _pixels16[offset] = srcR;
                _pixels16[offset + 1] = srcG;
                _pixels16[offset + 2] = srcB;
                _pixels16[offset + 3] = srcA;
                return;
            }

            long inverseSourceAlpha = ushort.MaxValue - srcA;
            long outAlpha = srcA + ((long)dstA * inverseSourceAlpha + 32767L) / ushort.MaxValue;
            long denominator = outAlpha * ushort.MaxValue;
            long half = denominator >> 1;
            _pixels16[offset] = (ushort)(((long)srcR * srcA * ushort.MaxValue + (long)_pixels16[offset] * dstA * inverseSourceAlpha + half) / denominator);
            _pixels16[offset + 1] = (ushort)(((long)srcG * srcA * ushort.MaxValue + (long)_pixels16[offset + 1] * dstA * inverseSourceAlpha + half) / denominator);
            _pixels16[offset + 2] = (ushort)(((long)srcB * srcA * ushort.MaxValue + (long)_pixels16[offset + 2] * dstA * inverseSourceAlpha + half) / denominator);
            _pixels16[offset + 3] = (ushort)outAlpha;
        }
    }
}
