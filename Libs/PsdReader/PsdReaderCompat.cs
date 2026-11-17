// PsdReader 兼容适配器
// 将 PsdReader 命名空间桥接到 cn.efunstudio.psdreader.PsdParser 命名空间，
// 使现有 PSD2UIForm 插件代码无需修改即可使用重写后的 PSD 解析器
//
// 命名空间映射:
//   cn.efunstudio.psdreader.PsdParser  →  PsdDocument, PsdLayer, PsdColor,
//                                         PsdTextLayerInfo, PsdTextStrokeInfo,
//                                         PsdTextShadowInfo, PsdTextStrokePosition,
//                                         CompatRenderedImage
//   cn.efunstudio.psdreader            →  PsdReaderLicense (stub)

using System;

// ═══════════════════════════════════════════════════════════════════
// cn.efunstudio.psdreader.PsdParser — 主命名空间
// PSD2UIForm 的 Psd2UIFormConverter / PsdLayerNode / PsdParserExtensions 导入此命名空间
// ═══════════════════════════════════════════════════════════════════
namespace cn.efunstudio.psdreader.PsdParser
{
    // ─── PsdColor (值类型，兼容 PsdReader.PsdColor) ───

    /// <summary>32 位 RGBA 颜色</summary>
    public struct PsdColor
    {
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }
        public byte A { get; }

        public PsdColor(byte r, byte g, byte b, byte a = 255)
        {
            R = r; G = g; B = b; A = a;
        }

        internal static PsdColor FromInner(PsdReader.PsdColor c)
            => new PsdColor(c.R, c.G, c.B, c.A);
    }

    // ─── PsdTextStrokePosition ───

    public enum PsdTextStrokePosition
    {
        Outside = 0,
        Inside  = 1,
        Center  = 2
    }

    // ─── PsdTextStrokeInfo ───

    public class PsdTextStrokeInfo
    {
        public bool Enabled { get; set; }
        public PsdColor Color { get; set; }
        public float Size { get; set; }
        public float Opacity { get; set; } = 1f;
        public PsdTextStrokePosition Position { get; set; } = PsdTextStrokePosition.Outside;
    }

    // ─── PsdTextShadowInfo ───

    public class PsdTextShadowInfo
    {
        public bool Enabled { get; set; }
        public PsdColor Color { get; set; }
        public float Opacity { get; set; } = 0.75f;
        public float Angle { get; set; } = 120f;
        public float Distance { get; set; } = 5f;
        public float Spread { get; set; }
        public float Blur { get; set; } = 5f;
    }

    // ─── PsdColorOverlayInfo ───

    public class PsdColorOverlayInfo
    {
        public bool Enabled { get; set; }
        public PsdColor Color { get; set; }
        public float Opacity { get; set; } = 1f;
    }

    // ─── PsdGlowInfo ───

    public class PsdGlowInfo
    {
        public bool Enabled { get; set; }
        public PsdColor Color { get; set; }
        public float Opacity { get; set; } = 0.75f;
        public float Size { get; set; } = 5f;
        public float Spread { get; set; }
    }

    public class PsdGradientColorStopInfo
    {
        public PsdColor Color { get; set; }
        public float Location { get; set; }
        public float Midpoint { get; set; } = 50f;
    }

    public class PsdGradientOverlayInfo
    {
        public bool Enabled { get; set; }
        public float Opacity { get; set; } = 1f;
        public float Angle { get; set; }
        public string Type { get; set; } = "Lnr ";
        public bool Reverse { get; set; }
        public float Scale { get; set; } = 100f;
        public System.Collections.Generic.List<PsdGradientColorStopInfo> ColorStops { get; set; } = new System.Collections.Generic.List<PsdGradientColorStopInfo>();
    }

    // ─── PsdTextLayerInfo ───

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
        public float Leading { get; set; }
        public float Tracking { get; set; }
        public bool AutoLeading { get; set; }
        public PsdTextStrokeInfo Stroke { get; set; }
        public PsdTextShadowInfo Shadow { get; set; }
        public PsdGlowInfo OuterGlow { get; set; }
        public System.Collections.Generic.List<PsdTextStrokeInfo> Strokes { get; set; } = new System.Collections.Generic.List<PsdTextStrokeInfo>();
        public System.Collections.Generic.List<PsdTextShadowInfo> Shadows { get; set; } = new System.Collections.Generic.List<PsdTextShadowInfo>();
        public System.Collections.Generic.List<PsdGradientOverlayInfo> GradientOverlays { get; set; } = new System.Collections.Generic.List<PsdGradientOverlayInfo>();
    }

    // ─── CompatRenderedImage (映射 Pixels → Rgba32) ───

    /// <summary>渲染结果包装，提供 PSD2UIForm 期望的 Rgba32 属性</summary>
    public class CompatRenderedImage
    {
        private readonly PsdReader.RenderedImage _inner;

        internal CompatRenderedImage(PsdReader.RenderedImage inner)
        {
            _inner = inner;
        }

        public int Width  => _inner?.Width  ?? 0;
        public int Height => _inner?.Height ?? 0;
        public int BitDepth => _inner?.BitDepth ?? 8;
        public bool IsHighBitDepth => _inner != null && _inner.IsHighBitDepth;
        public bool IsEmpty => _inner == null || _inner.IsEmpty;

        /// <summary>RGBA32 像素数据 — PSD2UIForm 使用此属性名</summary>
        public byte[] Rgba32 => _inner?.Pixels;
        public ushort[] Rgba64 => _inner?.Pixels16;
    }

    // ─── PsdDocument 适配器 ───

    public class PsdDocument : IDisposable
    {
        private readonly PsdReader.PsdDocument _inner;
        private PsdLayer[] _cachedChilds;

        private PsdDocument(PsdReader.PsdDocument inner) { _inner = inner; }

        /// <summary>原 API: PsdDocument.Create(path)</summary>
        public static PsdDocument Create(string path)
            => new PsdDocument(PsdReader.PsdDocument.Open(path));

        public int Width  => _inner.Width;
        public int Height => _inner.Height;
        public int Depth  => _inner.Depth;
        public PsdReader.PsdDocument Inner => _inner;

        public PsdLayer[] Childs
        {
            get
            {
                if (_cachedChilds != null) return _cachedChilds;
                var src = _inner.Childs;
                if (src == null || src.Length == 0) { _cachedChilds = Array.Empty<PsdLayer>(); return _cachedChilds; }
                var arr = new PsdLayer[src.Length];
                for (int i = 0; i < src.Length; i++)
                    arr[i] = PsdLayer.Wrap(src[i], this);
                _cachedChilds = arr;
                return _cachedChilds;
            }
        }

        public void Dispose() => _inner?.Dispose();
    }

    // ─── PsdLayer 适配器 ───

    public class PsdLayer
    {
        internal readonly PsdReader.PsdLayer _inner;
        public PsdReader.PsdLayer Inner => _inner;
        internal readonly PsdDocument _doc;
        private PsdLayer[] _cachedChilds;
        private CompatRenderedImage _cachedRender;

        private PsdLayer(PsdReader.PsdLayer inner, PsdDocument doc)
        {
            _inner = inner;
            _doc   = doc;
        }

        internal static PsdLayer Wrap(PsdReader.PsdLayer inner, PsdDocument doc)
            => inner == null ? null : new PsdLayer(inner, doc);

        public string Name      => _inner.Name;
        public int    Left      => _inner.Left;
        public int    Top       => _inner.Top;
        public int    Right     => _inner.Right;
        public int    Bottom    => _inner.Bottom;
        public bool   IsGroup   => _inner.IsGroup;
        public bool   IsVisible => _inner.IsVisible;
        public bool IsPreviewPassthroughGroup() => false;
        public PsdDocument Document => _doc;

        public PsdLayer[] Childs
        {
            get
            {
                if (_cachedChilds != null) return _cachedChilds;
                var src = _inner.Childs;
                if (src == null || src.Length == 0) { _cachedChilds = Array.Empty<PsdLayer>(); return _cachedChilds; }
                var arr = new PsdLayer[src.Length];
                for (int i = 0; i < src.Length; i++)
                    arr[i] = Wrap(src[i], _doc);
                _cachedChilds = arr;
                return _cachedChilds;
            }
        }

        // ─── 渲染 ───

        /// <summary>渲染图层为 RGBA32 图像（结果缓存，避免重复渲染）</summary>
        public CompatRenderedImage Render()
        {
            if (_cachedRender != null) return _cachedRender;
            var rendered = PsdReader.PsdRenderer.Render(_inner);
            _cachedRender = new CompatRenderedImage(rendered);
            return _cachedRender;
        }

        // ─── 文本/填充图层检测 ───

        public bool IsTextLayer()
            => PsdReader.TextLayerExtensions.IsTextLayer(_inner);

        public bool IsSolidFillLayer()
            => PsdReader.TextLayerExtensions.IsSolidFillLayer(_inner);

        public bool TryGetTextLayerInfo(out PsdTextLayerInfo info)
        {
            info = null;
            try
            {
                if (!PsdReader.TextLayerExtensions.TryGetTextLayerInfo(_inner, out var src))
                {
                    // 诊断：查看 Resources 状态
                    bool hasTySh = _inner.Resources != null && _inner.Resources.Contains("TextEngineData");
                    object tyshVal = hasTySh ? _inner.Resources["TextEngineData"] : null;
                    UnityEngine.Debug.LogWarning($"[PSD2UIForm:TryGetTextLayerInfo] {Name}: TryGetTextLayerInfo failed, hasTySh={hasTySh}, tyshType={tyshVal?.GetType()?.Name ?? "null"}");
                    return false;
                }

                info = new PsdTextLayerInfo
                {
                    Text          = src.Text,
                    FontName      = src.FontName,
                    FontSize      = src.FontSize,
                    Color         = PsdColor.FromInner(src.Color),
                FauxBold      = src.FauxBold,
                FauxItalic    = src.FauxItalic,
                Underline     = src.Underline,
                Strikethrough = src.Strikethrough,
                Leading       = src.Leading,
                Tracking      = src.Tracking,
                AutoLeading   = src.AutoLeading,
            };

            // 描边 — 保留全部，同时继续填写旧字段以兼容旧调用方。
            if (src.Strokes != null)
            {
                foreach (var s in src.Strokes)
                {
                    var stroke = new PsdTextStrokeInfo
                    {
                        Enabled  = s.Enabled,
                        Color    = PsdColor.FromInner(s.Color),
                        Size     = s.Size,
                        Opacity  = s.Opacity,
                        Position = (PsdTextStrokePosition)(int)s.Position
                    };
                    info.Strokes.Add(stroke);
                    if (info.Stroke == null)
                    {
                        info.Stroke = stroke;
                    }
                }
            }

            // 阴影 — 保留全部，同时继续填写旧字段以兼容旧调用方。
            if (src.Shadows != null)
            {
                foreach (var sh in src.Shadows)
                {
                    var shadow = new PsdTextShadowInfo
                    {
                        Enabled  = sh.Enabled,
                        Color    = PsdColor.FromInner(sh.Color),
                        Opacity  = sh.Opacity,
                        Angle    = sh.Angle,
                        Distance = sh.Distance,
                        Spread   = sh.Spread,
                        Blur     = sh.Size
                    };
                    info.Shadows.Add(shadow);
                    if (info.Shadow == null)
                    {
                        info.Shadow = shadow;
                    }
                }
            }

            if (src.GradientOverlays != null)
            {
                foreach (var item in src.GradientOverlays)
                {
                    var gradient = new PsdGradientOverlayInfo
                    {
                        Enabled = item.Enabled,
                        Opacity = item.Opacity,
                        Angle = item.Angle,
                        Type = item.Type,
                        Reverse = item.Reverse,
                        Scale = item.Scale
                    };
                    if (item.ColorStops != null)
                    {
                        foreach (var stop in item.ColorStops)
                        {
                            gradient.ColorStops.Add(new PsdGradientColorStopInfo
                            {
                                Color = PsdColor.FromInner(stop.Color),
                                Location = stop.Location,
                                Midpoint = stop.Midpoint
                            });
                        }
                    }
                    info.GradientOverlays.Add(gradient);
                }
            }

            // 外发光 — 取第一个启用的
            if (src.OuterGlows != null && src.OuterGlows.Count > 0)
            {
                foreach (var og in src.OuterGlows)
                {
                    if (og.Enabled)
                    {
                        info.OuterGlow = new PsdGlowInfo
                        {
                            Enabled = true,
                            Color   = PsdColor.FromInner(og.Color),
                            Opacity = og.Opacity,
                            Size    = og.Size,
                            Spread  = og.Spread
                        };
                        break;
                    }
                }
            }

            return true;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[PSD2UIForm:TryGetTextLayerInfo] {Name}: 异常 {ex.Message}");
                return false;
            }
        }

        public bool TryGetSolidFillColor(out PsdColor color)
        {
            color = default;
            if (!PsdReader.TextLayerExtensions.TryGetSolidFillColor(_inner, out var innerColor))
                return false;
            color = PsdColor.FromInner(innerColor);
            return true;
        }

        /// <summary>
        /// 从任意图层（包括图片图层）的 lfx2 中提取描边、阴影、颜色叠加效果。
        /// </summary>
        public bool TryGetLayerEffects(out PsdTextStrokeInfo stroke, out PsdTextShadowInfo shadow, out PsdColorOverlayInfo colorOverlay)
        {
            stroke = null;
            shadow = null;
            colorOverlay = null;
            try
            {
                if (!PsdReader.TextLayerExtensions.TryGetLayerEffects(_inner, out var src))
                    return false;

                // 描边 — 取第一个 Enabled
                if (src.Strokes != null)
                {
                    foreach (var s in src.Strokes)
                    {
                        if (s.Enabled)
                        {
                            stroke = new PsdTextStrokeInfo
                            {
                                Enabled  = s.Enabled,
                                Color    = PsdColor.FromInner(s.Color),
                                Size     = s.Size,
                                Opacity  = s.Opacity,
                                Position = (PsdTextStrokePosition)(int)s.Position
                            };
                            break;
                        }
                    }
                }

                // 阴影 — 取第一个 Enabled
                if (src.Shadows != null)
                {
                    foreach (var sh in src.Shadows)
                    {
                        if (sh.Enabled)
                        {
                            shadow = new PsdTextShadowInfo
                            {
                                Enabled  = sh.Enabled,
                                Color    = PsdColor.FromInner(sh.Color),
                                Opacity  = sh.Opacity,
                                Angle    = sh.Angle,
                                Distance = sh.Distance,
                                Spread   = sh.Spread,
                                Blur     = sh.Size
                            };
                            break;
                        }
                    }
                }

                // 颜色叠加 — 取第一个 Enabled
                if (src.ColorOverlays != null)
                {
                    foreach (var co in src.ColorOverlays)
                    {
                        if (co.Enabled)
                        {
                            colorOverlay = new PsdColorOverlayInfo
                            {
                                Enabled = co.Enabled,
                                Color   = PsdColor.FromInner(co.Color),
                                Opacity = co.Opacity
                            };
                            break;
                        }
                    }
                }

                return stroke != null || shadow != null || colorOverlay != null;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[PSD2UIForm:TryGetLayerEffects] {Name}: 异常 {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从任意图层的 lfx2 中提取所有启用的描边、阴影、颜色叠加效果。
        /// 与 TryGetLayerEffects 不同的是返回所有而非仅第一个。
        /// </summary>
        public bool TryGetAllLayerEffects(
            out System.Collections.Generic.List<PsdTextStrokeInfo> strokes,
            out System.Collections.Generic.List<PsdTextShadowInfo> shadows,
            out System.Collections.Generic.List<PsdColorOverlayInfo> colorOverlays,
            out System.Collections.Generic.List<PsdGlowInfo> glows)
        {
            strokes = new System.Collections.Generic.List<PsdTextStrokeInfo>();
            shadows = new System.Collections.Generic.List<PsdTextShadowInfo>();
            colorOverlays = new System.Collections.Generic.List<PsdColorOverlayInfo>();
            glows = new System.Collections.Generic.List<PsdGlowInfo>();
            try
            {
                if (!PsdReader.TextLayerExtensions.TryGetLayerEffects(_inner, out var src))
                    return false;

                if (src.Strokes != null)
                {
                    foreach (var s in src.Strokes)
                    {
                        if (s.Enabled)
                        {
                            strokes.Add(new PsdTextStrokeInfo
                            {
                                Enabled  = true,
                                Color    = PsdColor.FromInner(s.Color),
                                Size     = s.Size,
                                Opacity  = s.Opacity,
                                Position = (PsdTextStrokePosition)(int)s.Position
                            });
                        }
                    }
                }

                if (src.Shadows != null)
                {
                    foreach (var sh in src.Shadows)
                    {
                        if (sh.Enabled)
                        {
                            shadows.Add(new PsdTextShadowInfo
                            {
                                Enabled  = true,
                                Color    = PsdColor.FromInner(sh.Color),
                                Opacity  = sh.Opacity,
                                Angle    = sh.Angle,
                                Distance = sh.Distance,
                                Spread   = sh.Spread,
                                Blur     = sh.Size
                            });
                        }
                    }
                }

                if (src.ColorOverlays != null)
                {
                    foreach (var co in src.ColorOverlays)
                    {
                        if (co.Enabled)
                        {
                            colorOverlays.Add(new PsdColorOverlayInfo
                            {
                                Enabled = true,
                                Color   = PsdColor.FromInner(co.Color),
                                Opacity = co.Opacity
                            });
                        }
                    }
                }

                if (src.OuterGlows != null)
                {
                    foreach (var og in src.OuterGlows)
                    {
                        if (og.Enabled)
                        {
                            glows.Add(new PsdGlowInfo
                            {
                                Enabled = true,
                                Color   = PsdColor.FromInner(og.Color),
                                Opacity = og.Opacity,
                                Size    = og.Size,
                                Spread  = og.Spread
                            });
                        }
                    }
                }

                var hasAny = strokes.Count > 0 || shadows.Count > 0 || colorOverlays.Count > 0 || glows.Count > 0;
                return hasAny;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[PSD2UIForm:TryGetAllLayerEffects] {Name}: 异常 {ex.Message}");
                return false;
            }
        }

        public string GetDisplayName()
            => Name ?? string.Empty;
    }
}

// ═══════════════════════════════════════════════════════════════════
// cn.efunstudio.psdreader — UGUIParser.cs 导入此命名空间
// ═══════════════════════════════════════════════════════════════════
namespace cn.efunstudio.psdreader
{
    /// <summary>
    /// 许可证检查存根 — 原 DLL 的 DRM 已移除，始终返回无需许可。
    /// </summary>
    public static class PsdReaderLicense
    {
        public static bool RequiresLicense => false;
        public static string GetOrderId() => string.Empty;
        public static void SetOrderId(string orderId) { }
        public static void ClearOrderId() { }
    }
}
