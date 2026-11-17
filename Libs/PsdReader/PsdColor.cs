// PSD Reader - 颜色结构体

namespace PsdReader
{
    /// <summary>
    /// 32位 RGBA 颜色值
    /// </summary>
    public struct PsdColor
    {
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }
        public byte A { get; }

        public PsdColor(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public PsdColor WithAlpha(byte alpha) => new PsdColor(R, G, B, alpha);

        public override string ToString() => $"RGBA({R}, {G}, {B}, {A})";
    }
}
