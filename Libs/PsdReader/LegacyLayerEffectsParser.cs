using System;

namespace PsdReader
{
    /// <summary>
    /// Parses the Photoshop 5.x/7.0 lrFX block. Newer lfx2 descriptors remain
    /// authoritative; this data is used as a compatibility fallback.
    /// </summary>
    internal static class LegacyLayerEffectsParser
    {
        private struct ParsedColor
        {
            public bool IsKnown;
            public PsdColor Color;
        }

        internal static PsdTextLayerInfo Parse(PsdBinaryReader reader, long dataLength)
        {
            var result = new PsdTextLayerInfo();
            long blockEnd = Math.Min(reader.Length, reader.Position + Math.Max(0L, dataLength));
            if (!CanRead(reader, blockEnd, 4))
                return result;

            reader.ReadInt16BigEndian();
            int effectCount = reader.ReadUInt16BigEndian();
            bool effectsVisible = true;

            for (int i = 0; i < effectCount && CanRead(reader, blockEnd, 12); i++)
            {
                string signature = reader.ReadSignature();
                string effectKey = reader.ReadSignature();
                int effectSize = reader.ReadInt32BigEndian();
                if (effectSize < 0)
                    break;

                long effectEnd = Math.Min(blockEnd, reader.Position + effectSize);
                try
                {
                    if (signature != "8BIM")
                        continue;

                    switch (effectKey)
                    {
                        case "cmnS":
                            effectsVisible = ParseCommonState(reader, effectEnd, effectsVisible);
                            break;
                        case "dsdw":
                            ParseDropShadow(reader, effectEnd, result);
                            break;
                        case "oglw":
                            ParseOuterGlow(reader, effectEnd, result);
                            break;
                        case "sofi":
                            ParseSolidFill(reader, effectEnd, result);
                            break;
                    }
                }
                finally
                {
                    reader.Position = effectEnd;
                }
            }

            if (!effectsVisible)
            {
                foreach (var shadow in result.Shadows) shadow.Enabled = false;
                foreach (var glow in result.OuterGlows) glow.Enabled = false;
                foreach (var overlay in result.ColorOverlays) overlay.Enabled = false;
            }

            return result;
        }

        private static bool ParseCommonState(PsdBinaryReader reader, long effectEnd, bool fallback)
        {
            if (!CanRead(reader, effectEnd, 5))
                return fallback;

            reader.ReadInt32BigEndian();
            return reader.ReadByte() != 0;
        }

        private static void ParseDropShadow(PsdBinaryReader reader, long effectEnd, PsdTextLayerInfo result)
        {
            if (!CanRead(reader, effectEnd, 41))
                return;

            int version = reader.ReadInt32BigEndian();
            float blur = ReadFixed16_16(reader);
            float intensity = ReadFixed16_16(reader);
            float angle = ReadFixed16_16(reader);
            float distance = ReadFixed16_16(reader);
            ParsedColor displayColor = ReadColor(reader, effectEnd);
            reader.ReadSignature();
            string blendMode = reader.ReadSignature();
            bool enabled = reader.ReadByte() != 0;
            bool useGlobalAngle = reader.ReadByte() != 0;
            float opacity = PercentToUnit(reader.ReadByte());
            ParsedColor nativeColor = version >= 2 && CanRead(reader, effectEnd, 10)
                ? ReadColor(reader, effectEnd)
                : default;

            result.Shadows.Add(new PsdTextShadowInfo
            {
                Enabled = enabled,
                Color = SelectColor(nativeColor, displayColor, new PsdColor(0, 0, 0)),
                Opacity = opacity,
                Angle = angle,
                Distance = Math.Max(0f, distance),
                Spread = ClampPercent(intensity),
                Size = Math.Max(0f, blur),
                BlendMode = blendMode,
                UseGlobalAngle = useGlobalAngle
            });
        }

        private static void ParseOuterGlow(PsdBinaryReader reader, long effectEnd, PsdTextLayerInfo result)
        {
            if (!CanRead(reader, effectEnd, 32))
                return;

            int version = reader.ReadInt32BigEndian();
            float blur = ReadFixed16_16(reader);
            float intensity = ReadFixed16_16(reader);
            ParsedColor displayColor = ReadColor(reader, effectEnd);
            reader.ReadSignature();
            string blendMode = reader.ReadSignature();
            bool enabled = reader.ReadByte() != 0;
            float opacity = PercentToUnit(reader.ReadByte());
            ParsedColor nativeColor = version >= 2 && CanRead(reader, effectEnd, 10)
                ? ReadColor(reader, effectEnd)
                : default;

            result.OuterGlows.Add(new PsdGlowInfo
            {
                Enabled = enabled,
                Color = SelectColor(nativeColor, displayColor, new PsdColor(255, 255, 255)),
                Opacity = opacity,
                Size = Math.Max(0f, blur),
                Spread = ClampPercent(intensity),
                BlendMode = blendMode
            });
        }

        private static void ParseSolidFill(PsdBinaryReader reader, long effectEnd, PsdTextLayerInfo result)
        {
            if (!CanRead(reader, effectEnd, 34))
                return;

            int version = reader.ReadInt32BigEndian();
            reader.ReadSignature();
            string blendMode = reader.ReadSignature();
            ParsedColor displayColor = ReadColor(reader, effectEnd);
            float opacity = PercentToUnit(reader.ReadByte());
            bool enabled = reader.ReadByte() != 0;
            ParsedColor nativeColor = version >= 2 && CanRead(reader, effectEnd, 10)
                ? ReadColor(reader, effectEnd)
                : default;

            result.ColorOverlays.Add(new PsdColorOverlayInfo
            {
                Enabled = enabled,
                Color = SelectColor(nativeColor, displayColor, new PsdColor(0, 0, 0)),
                Opacity = opacity,
                BlendMode = blendMode
            });
        }

        private static ParsedColor ReadColor(PsdBinaryReader reader, long end)
        {
            if (!CanRead(reader, end, 10))
                return default;

            ushort colorSpace = reader.ReadUInt16BigEndian();
            ushort c1 = reader.ReadUInt16BigEndian();
            ushort c2 = reader.ReadUInt16BigEndian();
            ushort c3 = reader.ReadUInt16BigEndian();
            ushort c4 = reader.ReadUInt16BigEndian();

            switch (colorSpace)
            {
                case 0:
                    return Known(ToByte16(c1), ToByte16(c2), ToByte16(c3));
                case 1:
                    return FromHsb(c1 / 65535.0, c2 / 65535.0, c3 / 65535.0);
                case 2:
                    return FromCmyk(c1, c2, c3, c4);
                case 8:
                    byte gray = (byte)Math.Round(Math.Min(1.0, c1 / 10000.0) * 255.0, MidpointRounding.AwayFromZero);
                    return Known(gray, gray, gray);
                default:
                    return default;
            }
        }

        private static ParsedColor FromHsb(double hue, double saturation, double brightness)
        {
            hue -= Math.Floor(hue);
            saturation = Clamp01(saturation);
            brightness = Clamp01(brightness);
            double chroma = brightness * saturation;
            double sector = hue * 6.0;
            double x = chroma * (1.0 - Math.Abs(sector % 2.0 - 1.0));
            double r = 0.0;
            double g = 0.0;
            double b = 0.0;

            if (sector < 1.0) { r = chroma; g = x; }
            else if (sector < 2.0) { r = x; g = chroma; }
            else if (sector < 3.0) { g = chroma; b = x; }
            else if (sector < 4.0) { g = x; b = chroma; }
            else if (sector < 5.0) { r = x; b = chroma; }
            else { r = chroma; b = x; }

            double match = brightness - chroma;
            return Known(ToByteUnit(r + match), ToByteUnit(g + match), ToByteUnit(b + match));
        }

        private static ParsedColor FromCmyk(ushort c, ushort m, ushort y, ushort k)
        {
            double cyan = c / 65535.0;
            double magenta = m / 65535.0;
            double yellow = y / 65535.0;
            double black = k / 65535.0;
            return Known(
                ToByteUnit((1.0 - cyan) * (1.0 - black)),
                ToByteUnit((1.0 - magenta) * (1.0 - black)),
                ToByteUnit((1.0 - yellow) * (1.0 - black)));
        }

        private static ParsedColor Known(byte r, byte g, byte b)
        {
            return new ParsedColor { IsKnown = true, Color = new PsdColor(r, g, b) };
        }

        private static PsdColor SelectColor(ParsedColor preferred, ParsedColor fallback, PsdColor defaultColor)
        {
            if (preferred.IsKnown) return preferred.Color;
            return fallback.IsKnown ? fallback.Color : defaultColor;
        }

        private static byte ToByte16(ushort value)
        {
            return (byte)Math.Round(value / 257.0, MidpointRounding.AwayFromZero);
        }

        private static byte ToByteUnit(double value)
        {
            return (byte)Math.Round(Clamp01(value) * 255.0, MidpointRounding.AwayFromZero);
        }

        private static float PercentToUnit(int value)
        {
            return ClampPercent(value) / 100f;
        }

        private static float ClampPercent(float value)
        {
            return Math.Max(0f, Math.Min(100f, value));
        }

        private static float ReadFixed16_16(PsdBinaryReader reader)
        {
            return reader.ReadInt32BigEndian() / 65536f;
        }

        private static double Clamp01(double value)
        {
            return Math.Max(0.0, Math.Min(1.0, value));
        }

        private static bool CanRead(PsdBinaryReader reader, long end, int byteCount)
        {
            return byteCount >= 0 && reader.Position <= end - byteCount;
        }
    }
}
