// PSD Reader - 枚举定义
// 定义 PSD 文件格式中使用的所有枚举类型

using System;

namespace PsdReader
{
    /// <summary>
    /// Photoshop 图层混合模式 (28种)
    /// </summary>
    public enum BlendMode
    {
        PassThrough,
        Normal,
        Dissolve,
        Darken,
        Multiply,
        ColorBurn,
        LinearBurn,
        DarkerColor,
        Lighten,
        Screen,
        ColorDodge,
        LinearDodge,
        LighterColor,
        Overlay,
        SoftLight,
        HardLight,
        VividLight,
        LinearLight,
        PinLight,
        HardMix,
        Difference,
        Exclusion,
        Subtract,
        Divide,
        Hue,
        Saturation,
        Color,
        Luminosity
    }

    /// <summary>
    /// 通道类型: RGBA + 蒙版
    /// </summary>
    public enum ChannelType
    {
        Alpha = -1,
        Red = 0,
        Green = 1,
        Blue = 2,
        Mask = -2
    }

    /// <summary>
    /// PSD 文档颜色模式
    /// </summary>
    public enum ColorMode
    {
        Bitmap = 0,
        GrayScale = 1,
        Indexed = 2,
        RGB = 3,
        CMYK = 4,
        MultiChannel = 7,
        Duotone = 8,
        Lab = 9
    }

    /// <summary>
    /// 像素数据压缩方式
    /// </summary>
    public enum CompressionType
    {
        Raw = 0,
        RLE = 1,
        Zip = 2,
        ZipPrediction = 3
    }

    /// <summary>
    /// 图层分节类型 — 用于构建图层树的分组标记
    /// Normal = 普通图层, Opend/Closed = 组开始, Divider = 组结束
    /// </summary>
    public enum SectionType
    {
        Normal,
        Opend,
        Closed,
        Divider
    }

    /// <summary>
    /// 度量单位类型
    /// </summary>
    public enum UnitType
    {
        Angle,
        Density,
        Distance,
        None,
        Percent,
        Pixels,
        Points,
        Millimeters
    }

    /// <summary>
    /// 图层标志位
    /// </summary>
    [Flags]
    public enum LayerFlags
    {
        None = 0,
        TransparencyProtected = 1,
        Visible = 2,
        Obsolete = 4,
        PhotoshopV5Later = 8,
        PixelIrrelevant = 16
    }
}
