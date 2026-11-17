/*
插件获取:
https://efunstudio.cn
https://shop106471535.taobao.com
*/

#if UNITY_EDITOR
namespace UGF.EditorTools.Psd2UGUI
{
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    public enum PsdLayerType
    {
        Unknown = 0,
        Layer,
        BlackWhiteAdjustmentLayer,
        BrightnessContrastLayer,
        CmykChannelMixerLayer,
        ColorBalanceAdjustmentLayer,
        CurvesLayer,
        ExposureLayer,
        HueSaturationLayer,
        InvertAdjustmentLayer,
        LevelsLayer,
        PhotoFilterLayer,
        PosterizeLayer,
        RgbChannelMixerLayer,
        SelectiveColorLayer,
        ThresholdLayer,
        VibranceLayer,
        FillLayer,
        LayerGroup,
        SectionDividerLayer,
        ShapeLayer,
        SmartObjectLayer,
        TextLayer
    }
}
#endif