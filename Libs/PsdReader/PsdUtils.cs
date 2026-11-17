// PSD Reader - 工具方法
// 混合模式解析、单位类型解析、行字节数计算等

using System;
using System.Collections.Generic;

namespace PsdReader
{
    /// <summary>
    /// PSD 工具方法集
    /// </summary>
    public static class PsdUtils
    {
        /// <summary>
        /// 将 PSD 4字符混合模式代码解析为 BlendMode 枚举
        /// </summary>
        private static readonly Dictionary<string, BlendMode> BlendModeMap = new Dictionary<string, BlendMode>
        {
            { "pass", BlendMode.PassThrough },
            { "norm", BlendMode.Normal },
            { "diss", BlendMode.Dissolve },
            { "dark", BlendMode.Darken },
            { "mul ", BlendMode.Multiply },
            { "idiv", BlendMode.ColorBurn },
            { "lbrn", BlendMode.LinearBurn },
            { "dkCl", BlendMode.DarkerColor },
            { "lite", BlendMode.Lighten },
            { "scrn", BlendMode.Screen },
            { "div ", BlendMode.ColorDodge },
            { "lddg", BlendMode.LinearDodge },
            { "lgCl", BlendMode.LighterColor },
            { "over", BlendMode.Overlay },
            { "sLit", BlendMode.SoftLight },
            { "hLit", BlendMode.HardLight },
            { "vLit", BlendMode.VividLight },
            { "lLit", BlendMode.LinearLight },
            { "pLit", BlendMode.PinLight },
            { "hMix", BlendMode.HardMix },
            { "diff", BlendMode.Difference },
            { "smud", BlendMode.Exclusion },
            { "fsub", BlendMode.Subtract },
            { "fdiv", BlendMode.Divide },
            { "hue ", BlendMode.Hue },
            { "sat ", BlendMode.Saturation },
            { "colr", BlendMode.Color },
            { "lum ", BlendMode.Luminosity },
        };

        public static BlendMode ParseBlendMode(string code)
        {
            if (BlendModeMap.TryGetValue(code, out var mode))
                return mode;
            return BlendMode.Normal;
        }

        /// <summary>
        /// 将 PSD 单位类型字符串解析为 UnitType 枚举
        /// </summary>
        private static readonly Dictionary<string, UnitType> UnitTypeMap = new Dictionary<string, UnitType>
        {
            { "#Pxl", UnitType.Pixels },
            { "#Prc", UnitType.Percent },
            { "#Ang", UnitType.Angle },
            { "#Rsl", UnitType.Density },
            { "#Rlt", UnitType.Distance },
            { "#Pnt", UnitType.Points },
            { "#Mlm", UnitType.Millimeters },
            { "NONE", UnitType.None },
        };

        public static UnitType ParseUnitType(string code)
        {
            if (UnitTypeMap.TryGetValue(code, out var unit))
                return unit;
            return UnitType.None;
        }

        /// <summary>
        /// 根据位深和宽度计算一行像素的字节数
        /// </summary>
        public static int CalculateRowByteCount(int bitsPerSample, int width)
        {
            switch (bitsPerSample)
            {
                case 1:
                case 8: return width;
                case 16: return width * 2;
                case 32: return width * 4;
                default: throw new NotSupportedException($"不支持的位深: {bitsPerSample}");
            }
        }
    }
}
