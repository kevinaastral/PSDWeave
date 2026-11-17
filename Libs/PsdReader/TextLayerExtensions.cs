// PSD Reader - 文本/填充图层扩展
// 提供文本属性、描边、阴影等信息的提取

using System;
using System.Collections;
using System.Collections.Generic;

namespace PsdReader
{
    /// <summary>描边位置</summary>
    public enum StrokePosition
    {
        Outside,
        Inside,
        Center
    }

    /// <summary>PSD 文本图层信息</summary>
    public class PsdTextLayerInfo
    {
        public string Text { get; set; } = "";
        public string FontName { get; set; } = "";
        public float FontSize { get; set; }
        public PsdColor Color { get; set; }
        public bool FauxBold { get; set; }
        public bool FauxItalic { get; set; }
        public bool Underline { get; set; }
        public bool Strikethrough { get; set; }
        public float Leading { get; set; }      // 行距
        public float Tracking { get; set; }      // 字间距
        public string Alignment { get; set; } = "Left";
        public bool AutoLeading { get; set; }
        public float BaselineShift { get; set; }

        /// <summary>描边信息列表</summary>
        public List<PsdTextStrokeInfo> Strokes { get; set; } = new List<PsdTextStrokeInfo>();

        /// <summary>阴影信息列表</summary>
        public List<PsdTextShadowInfo> Shadows { get; set; } = new List<PsdTextShadowInfo>();

        /// <summary>颜色叠加信息列表</summary>
        public List<PsdColorOverlayInfo> ColorOverlays { get; set; } = new List<PsdColorOverlayInfo>();

        /// <summary>渐变叠加信息列表</summary>
        public List<PsdGradientOverlayInfo> GradientOverlays { get; set; } = new List<PsdGradientOverlayInfo>();

        /// <summary>外发光信息列表</summary>
        public List<PsdGlowInfo> OuterGlows { get; set; } = new List<PsdGlowInfo>();
    }

    /// <summary>PSD 描边信息</summary>
    public class PsdTextStrokeInfo
    {
        public bool Enabled { get; set; }
        public PsdColor Color { get; set; }
        public float Size { get; set; }
        public float Opacity { get; set; } = 1f;
        public StrokePosition Position { get; set; } = StrokePosition.Outside;
        public string BlendMode { get; set; } = "Normal";
        public bool Overprint { get; set; }
    }

    /// <summary>PSD 阴影信息</summary>
    public class PsdTextShadowInfo
    {
        public bool Enabled { get; set; }
        public PsdColor Color { get; set; }
        public float Opacity { get; set; } = 0.75f;
        public float Angle { get; set; } = 120f;  // 度
        public float Distance { get; set; } = 5f;
        public float Spread { get; set; }
        public float Size { get; set; } = 5f;
        public string BlendMode { get; set; } = "Multiply";
        public bool UseGlobalAngle { get; set; } = true;
    }

    /// <summary>PSD 颜色叠加信息</summary>
    public class PsdColorOverlayInfo
    {
        public bool Enabled { get; set; }
        public PsdColor Color { get; set; }
        public float Opacity { get; set; } = 1f;
        public string BlendMode { get; set; } = "Normal";
    }

    /// <summary>PSD 渐变叠加信息</summary>
    public class PsdGradientOverlayInfo
    {
        public bool Enabled { get; set; }
        public float Opacity { get; set; } = 1f;
        public float Angle { get; set; }
        public string Type { get; set; } = "Lnr ";
        public bool Reverse { get; set; }
        public float Scale { get; set; } = 100f;
        public List<PsdGradientColorStopInfo> ColorStops { get; set; } = new List<PsdGradientColorStopInfo>();
    }

    /// <summary>PSD 渐变颜色停靠点</summary>
    public class PsdGradientColorStopInfo
    {
        public PsdColor Color { get; set; }
        public float Location { get; set; }
        public float Midpoint { get; set; } = 50f;
    }

    /// <summary>PSD 外发光 / 内发光信息</summary>
    public class PsdGlowInfo
    {
        public bool Enabled { get; set; }
        public PsdColor Color { get; set; }
        public float Opacity { get; set; } = 0.75f;
        public float Size { get; set; } = 5f;   // blur radius (pixels)
        public float Spread { get; set; }         // choke/spread 0-100
        public string BlendMode { get; set; } = "Screen";
    }

    /// <summary>
    /// 文本/填充图层的只读扩展方法
    /// </summary>
    public static class TextLayerExtensions
    {
        /// <summary>是否是文本图层</summary>
        public static bool IsTextLayer(this IPsdLayer layer)
        {
            return layer.Resources != null && layer.Resources.Contains("TySh");
        }

        /// <summary>是否是纯色填充图层</summary>
        public static bool IsSolidFillLayer(this IPsdLayer layer)
        {
            return layer.Resources != null && layer.Resources.Contains("SoCo");
        }

        /// <summary>
        /// 尝试提取文本图层信息。
        /// 从 TySh descriptor 中解析字体、大小、颜色等属性。
        /// </summary>
        public static bool TryGetTextLayerInfo(this IPsdLayer layer, out PsdTextLayerInfo info)
        {
            info = null;

            if (!layer.IsTextLayer() || layer.Resources == null)
                return false;

            var tysh = layer.Resources["TySh"] as PropertyDictionary;
            if (tysh == null)
                return false;

            info = new PsdTextLayerInfo();

            // 标准文本 — PSD 文本末尾带 \r，需要去除
            if (tysh.Contains("Txt "))
            {
                string rawText = tysh["Txt "]?.ToString() ?? "";
                info.Text = rawText.TrimEnd('\r', '\n');
            }
            else
            {
                bool hasTxtNoSpace = tysh.Contains("Txt");
                UnityEngine.Debug.LogWarning($"[PSD:TySh] 图层='{layer.Name}' 没有'Txt '键 (有'Txt'={hasTxtNoSpace})");
            }

            // 从 EngineData 或 TextStyleRun 中提取字体属性
            ExtractStyleInfo(tysh, info);

            ExtractLayerEffects(layer, info);

            // 把第一个已启用的颜色叠加直接应用到文字颜色（覆盖 EngineData 里的原始字色）
            foreach (var colorOverlay in info.ColorOverlays)
            {
                if (colorOverlay.Enabled)
                {
                    info.Color = colorOverlay.Color;
                    break;
                }
            }

            return true;
        }

        /// <summary>
        /// 尝试获取纯色填充图层的颜色。
        /// 从 SoCo descriptor 中提取 RGB 值。
        /// </summary>
        public static bool TryGetSolidFillColor(this IPsdLayer layer, out PsdColor color)
        {
            color = default;

            if (!layer.IsSolidFillLayer() || layer.Resources == null)
                return false;

            var soco = layer.Resources["SoCo"] as PropertyDictionary;
            if (soco == null)
                return false;

            // SoCo -> Clr  -> { Rd, Grn, Bl }
            var clr = soco.Contains("Clr ") ? soco["Clr "] as PropertyDictionary : soco;
            if (clr == null)
                return false;

            float r = GetFloat(clr, "Rd  ");
            float g = GetFloat(clr, "Grn ");
            float b = GetFloat(clr, "Bl  ");

            color = new PsdColor(
                (byte)Math.Round(Math.Min(255, Math.Max(0, r))),
                (byte)Math.Round(Math.Min(255, Math.Max(0, g))),
                (byte)Math.Round(Math.Min(255, Math.Max(0, b))),
                255);
            return true;
        }

        #region Private Helpers

        /// <summary>
        /// 从 EngineData (PostScript-like 格式) 解析字体、字号、颜色等样式信息。
        /// TySh descriptor 中 "EngineData" 是 tdta 类型的二进制数据,
        /// 需要解析后通过路径访问: EngineDict.StyleRun.RunArray.0.StyleSheet.StyleSheetData
        /// </summary>
        private static void ExtractStyleInfo(PropertyDictionary tysh, PsdTextLayerInfo info)
        {
            // 获取 EngineData 原始二进制数据 (tdta 类型)
            byte[] engineDataBytes = tysh["EngineData"] as byte[];
            if (engineDataBytes == null || engineDataBytes.Length == 0)
            {
                UnityEngine.Debug.LogWarning($"[PSD:EngineData] 图层='{info.Text}' EngineData为null或空");
                return;
            }

            // 解析 PostScript-like 格式为 PropertyDictionary
            PropertyDictionary engineData;
            try
            {
                engineData = EngineDataParser.Parse(engineDataBytes);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[PSD:EngineData] 解析失败: {ex.Message}");
                return;
            }
            if (engineData == null)
            {
                UnityEngine.Debug.LogWarning("[PSD:EngineData] 解析结果为null");
                return;
            }

            // 获取主样式表数据: EngineDict.StyleRun.RunArray[0].StyleSheet.StyleSheetData
            var styleData = engineData["EngineDict.StyleRun.RunArray.0.StyleSheet.StyleSheetData"] as PropertyDictionary;
            if (styleData == null)
            {
                UnityEngine.Debug.LogWarning("[PSD:EngineData] StyleSheetData为null");
                return;
            }

            // 字体索引 → 通过 ResourceDict.FontSet[index].Name 解析字体名称
            int fontIndex = GetInt(styleData, "Font", -1);
            if (fontIndex >= 0)
            {
                var fontName = engineData[$"ResourceDict.FontSet.{fontIndex}.Name"]?.ToString();
                if (!string.IsNullOrEmpty(fontName))
                    info.FontName = fontName.Trim('(', ')');
            }

            // 字号 × 变换矩阵的Y缩放 (transform[3] = yy)
            float fontSize = GetFloat(styleData, "FontSize", 0f);
            float scaleY = ReadTransformScaleY(tysh);
            info.FontSize = fontSize * scaleY;

            // 颜色: FillColor.Values = [alpha, R, G, B] (0.0~1.0 归一化)
            ReadFillColor(styleData, info);

            // 样式标志
            info.FauxBold = GetBool(styleData, "FauxBold");
            info.FauxItalic = GetBool(styleData, "FauxItalic");
            info.Underline = GetBool(styleData, "Underline");
            info.Strikethrough = GetBool(styleData, "Strikethrough");

            // 间距
            info.Leading = GetFloat(styleData, "Leading", 0f);
            info.Tracking = GetFloat(styleData, "Tracking", 0f);
            info.AutoLeading = GetBool(styleData, "AutoLeading");
            info.BaselineShift = GetFloat(styleData, "BaselineShift", 0f);

            // 对齐: ParagraphRun → Justification (0=Left, 1=Right, 2=Center)
            ReadAlignment(engineData, info);

            // 如果 TySh 中没找到 "Txt " 文本，从 EngineData 中提取作为回退
            if (string.IsNullOrEmpty(info.Text))
            {
                var editorText = engineData["EngineDict.Editor.Text"]?.ToString();
                if (!string.IsNullOrEmpty(editorText))
                {
                    info.Text = editorText.TrimEnd('\r', '\n', '\0');
                    UnityEngine.Debug.Log($"[PSD:EngineData] 从Editor.Text回退提取文字='{info.Text}'");
                }
            }

        }

        /// <summary>从 _Transform (double[6]: xx,xy,yx,yy,tx,ty) 读取 Y 缩放因子</summary>
        private static float ReadTransformScaleY(PropertyDictionary tysh)
        {
            var transform = tysh["_Transform"] as double[];
            if (transform != null && transform.Length > 3)
            {
                double val = transform[3]; // yy
                if (!double.IsNaN(val) && !double.IsInfinity(val) && val != 0)
                    return (float)val;
            }
            return 1f;
        }

        /// <summary>从 FillColor.Values 读取颜色</summary>
        private static void ReadFillColor(PropertyDictionary styleData, PsdTextLayerInfo info)
        {
            var values = styleData["FillColor.Values"] as IList;
            if (values != null && values.Count >= 4)
            {
                info.Color = new PsdColor(
                    ConvertNormalizedToByte(values[1]),  // R
                    ConvertNormalizedToByte(values[2]),  // G
                    ConvertNormalizedToByte(values[3]),  // B
                    ConvertNormalizedToByte(values[0])   // A
                );
            }
            else
            {
                info.Color = new PsdColor(0, 0, 0, 255);
            }
        }

        /// <summary>从 ParagraphRun 读取对齐方式</summary>
        private static void ReadAlignment(PropertyDictionary engineData, PsdTextLayerInfo info)
        {
            var just = engineData["EngineDict.ParagraphRun.RunArray.0.ParagraphSheet.Properties.Justification"];
            if (just != null)
            {
                try
                {
                    int j = Convert.ToInt32(just);
                    info.Alignment = j switch
                    {
                        0 => "Left",
                        1 => "Right",
                        2 => "Center",
                        _ => "Left"
                    };
                }
                catch { }
            }
        }

        /// <summary>归一化值(0.0~1.0或0~255)转字节</summary>
        private static byte ConvertNormalizedToByte(object value)
        {
            if (value == null) return 0;
            try
            {
                double v = Convert.ToDouble(value);
                if (v <= 1.0)
                    return (byte)Math.Round(Math.Max(0, Math.Min(1, v)) * 255.0, MidpointRounding.AwayFromZero);
                return (byte)Math.Round(Math.Max(0, Math.Min(255, v)), MidpointRounding.AwayFromZero);
            }
            catch { return 0; }
        }

        private static PsdColor ReadDescriptorColor(PropertyDictionary colorDict, PsdColor fallback)
        {
            if (colorDict == null) return fallback;

            return new PsdColor(
                ReadDescriptorColorByte(colorDict, "Rd  ", fallback.R),
                ReadDescriptorColorByte(colorDict, "Grn ", fallback.G),
                ReadDescriptorColorByte(colorDict, "Bl  ", fallback.B),
                fallback.A);
        }

        private static byte ReadDescriptorColorByte(PropertyDictionary dict, string key, byte fallback)
        {
            if (dict == null || !dict.Contains(key)) return fallback;

            float value = GetFloat(dict, key, fallback);
            return (byte)Math.Round(Math.Max(0, Math.Min(255, value)), MidpointRounding.AwayFromZero);
        }

        private static void ExtractStroke(PropertyDictionary effects, PsdTextStrokeInfo info)
        {
            // Not used for single info — see 2-param version
        }

        private static void ExtractStroke(PropertyDictionary effects, PsdTextLayerInfo info)
        {
            // lfx2 -> FrFX (frame effect = stroke) 单一描边
            if (effects.Contains("FrFX"))
            {
                var frfx = effects["FrFX"] as PropertyDictionary;
                if (frfx != null)
                    ExtractSingleStroke(frfx, info);
            }

            // frameFXMulti (描边数组) — 某些 PSD 版本用数组形式存储
            if (effects.Contains("frameFXMulti"))
            {
                var list = effects["frameFXMulti"] as System.Collections.ArrayList;
                if (list != null)
                {
                    foreach (var item in list)
                    {
                        var strokeDict = item as PropertyDictionary;
                        if (strokeDict != null)
                            ExtractSingleStroke(strokeDict, info);
                    }
                }
            }
        }

        private static void ExtractSingleStroke(PropertyDictionary frfx, PsdTextLayerInfo info)
        {
            var stroke = new PsdTextStrokeInfo
            {
                Enabled = GetBool(frfx, "enab", true),
                Color = new PsdColor(0, 0, 0, 255),
                Size = GetFloat(frfx, "Sz  ", 1f),
                Opacity = GetFloat(frfx, "Opct", 100f) / 100f
            };

            // 描边颜色
            if (frfx.Contains("Clr "))
            {
                var colorDict = frfx["Clr "] as PropertyDictionary;
                if (colorDict != null)
                {
                    stroke.Color = ReadDescriptorColor(colorDict, stroke.Color);
                }
            }

            // 描边位置 — PSD enum: InsF(Inside), CtrF(Center), OutF(Outside)
            if (frfx.Contains("Styl"))
            {
                string pos = frfx["Styl"]?.ToString() ?? "";
                if (pos.Contains("InsF") || pos.Contains("Insd") || pos.Contains("Inside"))
                    stroke.Position = StrokePosition.Inside;
                else if (pos.Contains("CtrF") || pos.Contains("Cntr") || pos.Contains("Center"))
                    stroke.Position = StrokePosition.Center;
                else
                    stroke.Position = StrokePosition.Outside;
            }

            info.Strokes.Add(stroke);
        }

        private static void ExtractShadow(PropertyDictionary effects, PsdTextLayerInfo info)
        {
            // lfx2 -> DrSh (drop shadow) 单一阴影
            if (effects.Contains("DrSh"))
            {
                var drsh = effects["DrSh"] as PropertyDictionary;
                if (drsh != null)
                    ExtractSingleShadow(drsh, info);
            }

            // dropShadowMulti (阴影数组) — 某些 PSD 版本用数组形式存储
            if (effects.Contains("dropShadowMulti"))
            {
                var list = effects["dropShadowMulti"] as System.Collections.ArrayList;
                if (list != null)
                {
                    foreach (var item in list)
                    {
                        var shadowDict = item as PropertyDictionary;
                        if (shadowDict != null)
                            ExtractSingleShadow(shadowDict, info);
                    }
                }
            }
        }

        private static void ExtractSingleShadow(PropertyDictionary drsh, PsdTextLayerInfo info)
        {
            var shadow = new PsdTextShadowInfo
            {
                Enabled = GetBool(drsh, "enab", true),
                Color = new PsdColor(0, 0, 0, 255),
                Opacity = GetFloat(drsh, "Opct", 75f) / 100f,
                Angle = GetFloat(drsh, "lagl", 120f),
                Distance = GetFloat(drsh, "Dstn", 5f),
                Spread = GetFloat(drsh, "Ckmt", 0f),
                Size = GetFloat(drsh, "blur", 5f),
                UseGlobalAngle = GetBool(drsh, "uglg", true)
            };

            // 阴影颜色
            if (drsh.Contains("Clr "))
            {
                var colorDict = drsh["Clr "] as PropertyDictionary;
                if (colorDict != null)
                {
                    shadow.Color = ReadDescriptorColor(colorDict, shadow.Color);
                }
            }

            info.Shadows.Add(shadow);
        }

        private static float GetFloat(PropertyDictionary dict, string key, float defaultValue = 0f)
        {
            if (!dict.Contains(key))
                return defaultValue;

            var val = dict[key];
            if (val is int intVal) return intVal;
            if (val is long longVal) return longVal;
            if (val is double doubleVal) return (float)doubleVal;
            if (val is float floatVal) return floatVal;

            // UntF（带单位浮点）可能被序列化为匿名对象
            // 尝试反射获取 Value 属性
            var valueProp = val?.GetType().GetProperty("Value");
            if (valueProp != null)
            {
                return Convert.ToSingle(valueProp.GetValue(val));
            }

            if (float.TryParse(val?.ToString(), out float parsed))
                return parsed;
            return defaultValue;
        }

        private static int GetInt(PropertyDictionary dict, string key, int defaultValue = 0)
        {
            if (!dict.Contains(key))
                return defaultValue;
            var val = dict[key];
            if (val is int intVal) return intVal;
            if (val is long longVal) return (int)longVal;
            if (val is double doubleVal) return (int)doubleVal;
            try { return Convert.ToInt32(val); }
            catch { return defaultValue; }
        }

        private static bool GetBool(PropertyDictionary dict, string key, bool defaultValue = false)
        {
            if (!dict.Contains(key))
                return defaultValue;

            var val = dict[key];
            if (val is bool boolVal) return boolVal;
            if (val is int intVal) return intVal != 0;
            return defaultValue;
        }

        #endregion

        // ─── 通用图层效果提取（适用于所有图层类型，包括图片图层） ───

        /// <summary>
        /// 从任意图层的 lfx2/lrFX 资源中提取描边、阴影、颜色叠加效果。
        /// 不要求图层是文本图层。
        /// </summary>
        public static bool TryGetLayerEffects(this IPsdLayer layer, out PsdTextLayerInfo effectsInfo)
        {
            effectsInfo = null;
            if (layer == null || layer.Resources == null)
                return false;

            effectsInfo = new PsdTextLayerInfo();
            ExtractLayerEffects(layer, effectsInfo);

            return effectsInfo.Strokes.Count > 0 || effectsInfo.Shadows.Count > 0
                || effectsInfo.ColorOverlays.Count > 0 || effectsInfo.GradientOverlays.Count > 0
                || effectsInfo.OuterGlows.Count > 0;
        }

        private static void ExtractLayerEffects(IPsdLayer layer, PsdTextLayerInfo info)
        {
            if (layer?.Resources == null || info == null)
                return;

            if (layer.Resources["lfx2"] is PropertyDictionary effects)
            {
                ExtractStroke(effects, info);
                ExtractShadow(effects, info);
                ExtractOuterGlow(effects, info);
                ExtractGradientOverlay(effects, info);

                if (effects["SoFi"] is PropertyDictionary solidFill)
                    ExtractColorOverlay(solidFill, info);

                if (effects["solidFillMulti"] is ArrayList solidFills)
                {
                    foreach (var item in solidFills)
                    {
                        if (item is PropertyDictionary fill)
                            ExtractColorOverlay(fill, info);
                    }
                }
            }

            if (!(layer.Resources["lrFX"] is PsdTextLayerInfo legacyEffects))
                return;

            // lfx2 is authoritative. Legacy effects only fill missing categories.
            if (info.Strokes.Count == 0 && legacyEffects.Strokes != null)
                info.Strokes.AddRange(legacyEffects.Strokes);
            if (info.Shadows.Count == 0 && legacyEffects.Shadows != null)
                info.Shadows.AddRange(legacyEffects.Shadows);
            if (info.OuterGlows.Count == 0 && legacyEffects.OuterGlows != null)
                info.OuterGlows.AddRange(legacyEffects.OuterGlows);
            if (info.ColorOverlays.Count == 0 && legacyEffects.ColorOverlays != null)
                info.ColorOverlays.AddRange(legacyEffects.ColorOverlays);
            if (info.GradientOverlays.Count == 0 && legacyEffects.GradientOverlays != null)
                info.GradientOverlays.AddRange(legacyEffects.GradientOverlays);
        }

        private static void ExtractOuterGlow(PropertyDictionary effects, PsdTextLayerInfo info)
        {
            // OrGl = Outer Glow
            if (effects.Contains("OrGl"))
            {
                var orgl = effects["OrGl"] as PropertyDictionary;
                if (orgl != null) ExtractSingleGlow(orgl, info);
            }
            // outerGlowMulti — 部分 PS 版本用数组
            if (effects.Contains("outerGlowMulti"))
            {
                var list = effects["outerGlowMulti"] as System.Collections.ArrayList;
                if (list != null)
                {
                    foreach (var item in list)
                    {
                        var glowDict = item as PropertyDictionary;
                        if (glowDict != null) ExtractSingleGlow(glowDict, info);
                    }
                }
            }
        }

        private static void ExtractSingleGlow(PropertyDictionary orgl, PsdTextLayerInfo info)
        {
            var glow = new PsdGlowInfo
            {
                Enabled = GetBool(orgl, "enab", true),
                Color   = new PsdColor(255, 255, 200, 255),
                Opacity = GetFloat(orgl, "Opct", 75f) / 100f,
                Size    = GetFloat(orgl, "blur", 5f),
                Spread  = GetFloat(orgl, "Ckmt", 0f),
            };

            // 颜色 (GlwT=SoLd 为纯色，GlwT=GlwG 为渐变，此处只取纯色)
            if (orgl.Contains("Clr "))
            {
                var colorDict = orgl["Clr "] as PropertyDictionary;
                if (colorDict != null)
                {
                    glow.Color = ReadDescriptorColor(colorDict, glow.Color);
                }
            }
            else
            {
                // 默认白色发光
                glow.Color = new PsdColor(255, 255, 200, 255);
            }

            info.OuterGlows.Add(glow);
        }

        private static void ExtractColorOverlay(PropertyDictionary sofi, PsdTextLayerInfo info)
        {
            var overlay = new PsdColorOverlayInfo
            {
                Enabled = GetBool(sofi, "enab", true),
                Color = new PsdColor(0, 0, 0, 255),
                Opacity = GetFloat(sofi, "Opct", 100f) / 100f,
            };

            if (sofi.Contains("Clr "))
            {
                var colorDict = sofi["Clr "] as PropertyDictionary;
                if (colorDict != null)
                {
                    overlay.Color = ReadDescriptorColor(colorDict, overlay.Color);
                }
            }

            info.ColorOverlays.Add(overlay);
        }

        private static void ExtractGradientOverlay(PropertyDictionary effects, PsdTextLayerInfo info)
        {
            if (effects.Contains("GrFl") && effects["GrFl"] is PropertyDictionary gradient)
            {
                ExtractSingleGradientOverlay(gradient, info);
            }

            if (effects.Contains("gradientFillMulti") && effects["gradientFillMulti"] is ArrayList gradients)
            {
                foreach (var item in gradients)
                {
                    if (item is PropertyDictionary gradientItem)
                    {
                        ExtractSingleGradientOverlay(gradientItem, info);
                    }
                }
            }
        }

        private static void ExtractSingleGradientOverlay(PropertyDictionary gradient, PsdTextLayerInfo info)
        {
            var overlay = new PsdGradientOverlayInfo
            {
                Enabled = GetBool(gradient, "enab", true),
                Opacity = GetFloat(gradient, "Opct", 100f) / 100f,
                Angle = GetFloat(gradient, "Angl", 0f),
                Type = GetDescriptorEnum(gradient, "Type", "Lnr "),
                Reverse = GetBool(gradient, "Rvrs", false),
                Scale = GetFloat(gradient, "Scl ", 100f)
            };

            if (gradient["Grad"] is PropertyDictionary definition
                && definition["Clrs"] is ArrayList colorStops)
            {
                var transparencyStops = definition["Trns"] as ArrayList;
                foreach (var item in colorStops)
                {
                    if (!(item is PropertyDictionary colorStop))
                    {
                        continue;
                    }

                    var color = colorStop["Clr "] is PropertyDictionary colorDescriptor
                        ? ReadDescriptorColor(colorDescriptor, new PsdColor(0, 0, 0, 255))
                        : new PsdColor(0, 0, 0, 255);
                    float location = GetFloat(colorStop, "Lctn", 0f);
                    float opacity = GetGradientStopOpacity(transparencyStops, location);
                    color = new PsdColor(color.R, color.G, color.B,
                        (byte)Math.Round(Math.Max(0f, Math.Min(255f, color.A * opacity)), MidpointRounding.AwayFromZero));
                    overlay.ColorStops.Add(new PsdGradientColorStopInfo
                    {
                        Color = color,
                        Location = location,
                        Midpoint = GetFloat(colorStop, "Mdpn", 50f)
                    });
                }
            }

            info.GradientOverlays.Add(overlay);
        }

        private static float GetGradientStopOpacity(ArrayList transparencyStops, float location)
        {
            if (transparencyStops == null || transparencyStops.Count == 0)
            {
                return 1f;
            }

            float closestDistance = float.MaxValue;
            float opacity = 1f;
            foreach (var item in transparencyStops)
            {
                if (!(item is PropertyDictionary stop))
                {
                    continue;
                }

                float distance = Math.Abs(GetFloat(stop, "Lctn", location) - location);
                if (distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = distance;
                opacity = GetFloat(stop, "Opct", 100f) / 100f;
            }

            return Math.Max(0f, Math.Min(1f, opacity));
        }

        private static string GetDescriptorEnum(PropertyDictionary dictionary, string key, string fallback)
        {
            if (dictionary == null || !dictionary.Contains(key))
            {
                return fallback;
            }

            var value = dictionary[key];
            var valueProperty = value?.GetType().GetProperty("Value");
            return valueProperty?.GetValue(value)?.ToString() ?? value?.ToString() ?? fallback;
        }
    }
}
