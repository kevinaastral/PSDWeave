/*
图层效果烘焙器
将 PSD 图层效果(描边、阴影、颜色叠加)渲染为单独的效果纹理。
PS 的效果渲染与 Unity 大相径庭，用 Unity 组件模拟效果不佳，
因此在导出阶段独立渲染效果图，与基础图层分开导出。
*/

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using cn.efunstudio.psdreader.PsdParser;

namespace UGF.EditorTools.Psd2UGUI
{
    internal static class LayerEffectsBaker
    {
        /// <summary>
        /// 为图层生成独立的效果纹理（仅包含描边和阴影，不含原始图像）。
        /// 支持多个描边和多个阴影叠加。
        /// expandLeft/expandTop 记录效果纹理左侧和顶部相对于原始图层的扩展量（PSD坐标）。
        /// 颜色叠加直接修改 baseTex（如果存在），返回 null。
        /// </summary>
        public static Texture2D GenerateEffectsTexture(Texture2D baseTex, PsdLayer layer,
            out int expandLeft, out int expandTop, out int totalWidth, out int totalHeight)
        {
            expandLeft = 0;
            expandTop = 0;
            totalWidth = baseTex != null ? baseTex.width : 0;
            totalHeight = baseTex != null ? baseTex.height : 0;
            if (baseTex == null || layer == null) return null;

            if (!layer.TryGetAllLayerEffects(out var strokes, out var shadows, out var colorOverlays, out var glows))
                return null;

            bool hasStroke = strokes.Count > 0;
            bool hasShadow = shadows.Count > 0;
            bool hasOverlay = colorOverlays.Count > 0;
            bool hasGlow   = glows.Count > 0;

            if (!hasStroke && !hasShadow && !hasOverlay && !hasGlow)
                return null;

            // 颜色叠加无需独立纹理，后续单独处理
            if (!hasStroke && !hasShadow && !hasGlow)
                return null;

            int baseW = baseTex.width;
            int baseH = baseTex.height;

            // ── 1. 计算所有效果的最大扩展量 ──
            int maxStrokePad = 0;
            foreach (var stroke in strokes)
            {
                int pad = 0;
                switch (stroke.Position)
                {
                    case PsdTextStrokePosition.Outside:
                        pad = Mathf.CeilToInt(stroke.Size);
                        break;
                    case PsdTextStrokePosition.Center:
                        pad = Mathf.CeilToInt(stroke.Size * 0.5f);
                        break;
                    default:
                        pad = 0;
                        break;
                }
                maxStrokePad = Mathf.Max(maxStrokePad, pad);
            }

            // ── 2. 计算所有阴影偏移和模糊的最大扩展量 ──
            int maxShadowPadL = 0, maxShadowPadR = 0, maxShadowPadT = 0, maxShadowPadB = 0;
            foreach (var shadow in shadows)
            {
                float angleRad = shadow.Angle * Mathf.Deg2Rad;
                int dx = Mathf.RoundToInt(-Mathf.Cos(angleRad) * shadow.Distance);
                int dy = Mathf.RoundToInt(-Mathf.Sin(angleRad) * shadow.Distance);
                int blurPad = Mathf.CeilToInt(shadow.Blur);
                maxShadowPadL = Mathf.Max(maxShadowPadL, Mathf.Max(0, -dx) + blurPad);
                maxShadowPadR = Mathf.Max(maxShadowPadR, Mathf.Max(0, dx) + blurPad);
                maxShadowPadT = Mathf.Max(maxShadowPadT, Mathf.Max(0, dy) + blurPad);
                maxShadowPadB = Mathf.Max(maxShadowPadB, Mathf.Max(0, -dy) + blurPad);
            }

            // ── 3. 计算外发光扩展量（取所有发光孤径最大值） ──
            int maxGlowPad = 0;
            foreach (var glow in glows)
            {
                int glowPad = Mathf.CeilToInt(glow.Size + glow.Spread * glow.Size * 0.01f);
                maxGlowPad = Mathf.Max(maxGlowPad, glowPad);
            }

            // ── 3. 计算四个方向的扩展量 ──
            int padL = Mathf.Max(maxStrokePad, maxShadowPadL);
            int padR = Mathf.Max(maxStrokePad, maxShadowPadR);
            int padT = Mathf.Max(maxStrokePad, maxShadowPadT);
            int padB = Mathf.Max(maxStrokePad, maxShadowPadB);

            if (padL == 0 && padR == 0 && padT == 0 && padB == 0)
                return null;

            int newW = baseW + padL + padR;
            int newH = baseH + padT + padB;
            expandLeft = padL;
            expandTop = padT;
            totalWidth = newW;
            totalHeight = newH;

            Debug.Log($"[EffectsBaker] 图层='{layer.Name}', base={baseW}x{baseH}, pad=L{padL}/R{padR}/T{padT}/B{padB}, fx={newW}x{newH}, strokes={strokes.Count}, shadows={shadows.Count}");

            // ── 4. 读取原始像素，建立扩展坐标系中的 alpha ──
            Color32[] basePixels = baseTex.GetPixels32();
            byte[] alpha = new byte[newW * newH];
            for (int y = 0; y < baseH; y++)
                for (int x = 0; x < baseW; x++)
                    alpha[(y + padB) * newW + (x + padL)] = basePixels[y * baseW + x].a;

            Color32[] result = new Color32[newW * newH];

            // ── 5. 渲染所有阴影（最底层，按倒序叠加使第一个在最上面）──
            for (int si = shadows.Count - 1; si >= 0; si--)
            {
                var shadow = shadows[si];
                float angleRad = shadow.Angle * Mathf.Deg2Rad;
                int dx = Mathf.RoundToInt(-Mathf.Cos(angleRad) * shadow.Distance);
                int dy = Mathf.RoundToInt(-Mathf.Sin(angleRad) * shadow.Distance);

                byte[] shadowAlpha = OffsetAlpha(alpha, newW, newH, dx, dy);
                int blurRadius = Mathf.Max(1, Mathf.RoundToInt(shadow.Blur * 0.35f));
                for (int pass = 0; pass < 3; pass++)
                    BoxBlur(shadowAlpha, newW, newH, blurRadius);

                byte sr = shadow.Color.R, sg = shadow.Color.G, sb = shadow.Color.B;
                float opacity = shadow.Opacity;
                for (int i = 0; i < result.Length; i++)
                {
                    byte sa = (byte)Mathf.RoundToInt(shadowAlpha[i] * opacity);
                    if (sa > 0)
                        AlphaComposite(ref result[i], new Color32(sr, sg, sb, sa));
                }
            }

            // ── 6. 渲染所有描边（按倒序叠加使第一个在最上面）──
            for (int si = strokes.Count - 1; si >= 0; si--)
            {
                var stroke = strokes[si];
                int strokePad = 0;
                switch (stroke.Position)
                {
                    case PsdTextStrokePosition.Outside:
                        strokePad = Mathf.CeilToInt(stroke.Size);
                        break;
                    case PsdTextStrokePosition.Center:
                        strokePad = Mathf.CeilToInt(stroke.Size * 0.5f);
                        break;
                    default:
                        strokePad = 0;
                        break;
                }

                byte cr = stroke.Color.R, cg = stroke.Color.G, cb = stroke.Color.B;
                float sOpacity = stroke.Opacity;

                if (strokePad > 0)
                {
                    byte[] dilated = DilateAlpha(alpha, newW, newH, strokePad);
                    for (int i = 0; i < result.Length; i++)
                    {
                        if (dilated[i] > 0 && alpha[i] == 0)
                        {
                            byte sa = (byte)Mathf.RoundToInt(dilated[i] * sOpacity);
                            AlphaComposite(ref result[i], new Color32(cr, cg, cb, sa));
                        }
                    }
                }
                else if (stroke.Position == PsdTextStrokePosition.Inside)
                {
                    int insideWidth = Mathf.CeilToInt(stroke.Size);
                    byte[] eroded = ErodeAlpha(alpha, newW, newH, insideWidth);
                    for (int i = 0; i < result.Length; i++)
                    {
                        if (alpha[i] > 0 && eroded[i] == 0)
                        {
                            byte sa = (byte)Mathf.RoundToInt(alpha[i] * sOpacity);
                            AlphaComposite(ref result[i], new Color32(cr, cg, cb, sa));
                        }
                    }
                }
            }

            // ──注意：不复制原始图像到效果纹理——效果纹理仅包含描边和阴影 ──

            // ── 7. 裁剪空白边缘 ──
            CropTransparentEdges(ref result, ref newW, ref newH, ref expandLeft, ref expandTop, padL, padR, padT, padB);
            totalWidth = newW;
            totalHeight = newH;

            var tex = new Texture2D(newW, newH, TextureFormat.RGBA32, false);
            tex.alphaIsTransparency = true;
            tex.SetPixels32(result);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 将原始图片与所有效果（描边、阴影、颜色叠加）烘焙为一张完整纹理。
        /// 返回的纹理尺寸 ≥ 原始纹理（如有外描边/阴影会更大）。
        /// expandLeft/expandTop 记录烘焙纹理左侧和顶部相对于原始图层的扩展量。
        /// </summary>
        public static Texture2D GenerateBakedTexture(Texture2D baseTex, PsdLayer layer,
            out int expandLeft, out int expandTop)
        {
            expandLeft = 0;
            expandTop = 0;
            if (baseTex == null || layer == null) return null;

            if (!layer.TryGetAllLayerEffects(out var strokes, out var shadows, out var colorOverlays, out var glows))
                return null;

            bool hasStroke  = strokes.Count > 0;
            bool hasShadow  = shadows.Count > 0;
            bool hasOverlay = colorOverlays.Count > 0;
            bool hasGlow    = glows.Count > 0;

            if (!hasStroke && !hasShadow && !hasOverlay && !hasGlow)
                return null;

            int baseW = baseTex.width;
            int baseH = baseTex.height;

            // ── 1. 计算扩展量（与 GenerateEffectsTexture 相同逻辑）──
            int maxStrokePad = 0;
            foreach (var stroke in strokes)
            {
                int pad = 0;
                switch (stroke.Position)
                {
                    case PsdTextStrokePosition.Outside:
                        pad = Mathf.CeilToInt(stroke.Size);
                        break;
                    case PsdTextStrokePosition.Center:
                        pad = Mathf.CeilToInt(stroke.Size * 0.5f);
                        break;
                }
                maxStrokePad = Mathf.Max(maxStrokePad, pad);
            }

            int maxShadowPadL = 0, maxShadowPadR = 0, maxShadowPadT = 0, maxShadowPadB = 0;
            foreach (var shadow in shadows)
            {
                float angleRad = shadow.Angle * Mathf.Deg2Rad;
                int dx = Mathf.RoundToInt(-Mathf.Cos(angleRad) * shadow.Distance);
                int dy = Mathf.RoundToInt(-Mathf.Sin(angleRad) * shadow.Distance);
                int blurPad = Mathf.CeilToInt(shadow.Blur);
                maxShadowPadL = Mathf.Max(maxShadowPadL, Mathf.Max(0, -dx) + blurPad);
                maxShadowPadR = Mathf.Max(maxShadowPadR, Mathf.Max(0, dx) + blurPad);
                maxShadowPadT = Mathf.Max(maxShadowPadT, Mathf.Max(0, dy) + blurPad);
                maxShadowPadB = Mathf.Max(maxShadowPadB, Mathf.Max(0, -dy) + blurPad);
            }

            int maxGlowPad = 0;
            foreach (var glow in glows)
            {
                int glowPad = Mathf.CeilToInt(glow.Size + glow.Spread * glow.Size * 0.01f);
                maxGlowPad = Mathf.Max(maxGlowPad, glowPad);
            }

            int padL = Mathf.Max(maxStrokePad, maxShadowPadL, maxGlowPad);
            int padR = Mathf.Max(maxStrokePad, maxShadowPadR, maxGlowPad);
            int padT = Mathf.Max(maxStrokePad, maxShadowPadT, maxGlowPad);
            int padB = Mathf.Max(maxStrokePad, maxShadowPadB, maxGlowPad);

            int newW = baseW + padL + padR;
            int newH = baseH + padT + padB;
            expandLeft = padL;
            expandTop = padT;

            // ── 2. 建立扩展坐标系 ──
            Color32[] basePixels = baseTex.GetPixels32();
            byte[] alpha = new byte[newW * newH];
            Color32[] baseExpanded = new Color32[newW * newH];
            for (int y = 0; y < baseH; y++)
            {
                for (int x = 0; x < baseW; x++)
                {
                    int dstIdx = (y + padB) * newW + (x + padL);
                    alpha[dstIdx] = basePixels[y * baseW + x].a;
                    baseExpanded[dstIdx] = basePixels[y * baseW + x];
                }
            }

            Color32[] result = new Color32[newW * newH];

            // ── 3. 渲染所有阴影（最底层）──
            for (int si = shadows.Count - 1; si >= 0; si--)
            {
                var shadow = shadows[si];
                float angleRad = shadow.Angle * Mathf.Deg2Rad;
                int dx = Mathf.RoundToInt(-Mathf.Cos(angleRad) * shadow.Distance);
                int dy = Mathf.RoundToInt(-Mathf.Sin(angleRad) * shadow.Distance);
                Debug.Log($"[EffectsBaker:Baked] 图层='{layer.Name}' 渲染Shadow[{si}]: Color=({shadow.Color.R},{shadow.Color.G},{shadow.Color.B}), dx={dx}, dy={dy}, blur={shadow.Blur}, opacity={shadow.Opacity}");

                byte[] shadowAlpha = OffsetAlpha(alpha, newW, newH, dx, dy);
                int blurRadius = Mathf.Max(1, Mathf.RoundToInt(shadow.Blur * 0.35f));
                for (int pass = 0; pass < 3; pass++)
                    BoxBlur(shadowAlpha, newW, newH, blurRadius);

                byte sr = shadow.Color.R, sg = shadow.Color.G, sb2 = shadow.Color.B;
                float opacity = shadow.Opacity;
                for (int i = 0; i < result.Length; i++)
                {
                    byte sa = (byte)Mathf.RoundToInt(shadowAlpha[i] * opacity);
                    if (sa > 0)
                        AlphaComposite(ref result[i], new Color32(sr, sg, sb2, sa));
                }
            }

            // ── 3b. 渲染外发光 ──
            for (int gi = glows.Count - 1; gi >= 0; gi--)
            {
                var glow = glows[gi];
                int spreadPx = Mathf.RoundToInt(glow.Spread * glow.Size * 0.01f);
                byte[] glowAlpha = spreadPx > 0 ? DilateAlpha(alpha, newW, newH, spreadPx) : (byte[])alpha.Clone();
                int blurR = Mathf.Max(1, Mathf.RoundToInt(glow.Size * 0.35f));
                for (int pass = 0; pass < 3; pass++)
                    BoxBlur(glowAlpha, newW, newH, blurR);

                byte gr = glow.Color.R, gg = glow.Color.G, gb = glow.Color.B;
                float gOpacity = glow.Opacity;
                for (int i = 0; i < result.Length; i++)
                {
                    if (alpha[i] == 0 && glowAlpha[i] > 0)
                    {
                        byte ga = (byte)Mathf.RoundToInt(glowAlpha[i] * gOpacity);
                        if (ga > 0)
                            AlphaComposite(ref result[i], new Color32(gr, gg, gb, ga));
                    }
                }
            }

            // ── 4. 渲染所有描边 ──
            for (int si = strokes.Count - 1; si >= 0; si--)
            {
                var stroke = strokes[si];
                int strokePad = 0;
                switch (stroke.Position)
                {
                    case PsdTextStrokePosition.Outside:
                        strokePad = Mathf.CeilToInt(stroke.Size);
                        break;
                    case PsdTextStrokePosition.Center:
                        strokePad = Mathf.CeilToInt(stroke.Size * 0.5f);
                        break;
                }
                Debug.Log($"[EffectsBaker:Baked] 图层='{layer.Name}' 渲染Stroke[{si}]: Size={stroke.Size}, Color=({stroke.Color.R},{stroke.Color.G},{stroke.Color.B}), Pos={stroke.Position}, Pad={strokePad}, Opacity={stroke.Opacity}");

                byte cr = stroke.Color.R, cg = stroke.Color.G, cb = stroke.Color.B;
                float sOpacity = stroke.Opacity;

                if (strokePad > 0)
                {
                    byte[] dilated = DilateAlpha(alpha, newW, newH, strokePad);
                    for (int i = 0; i < result.Length; i++)
                    {
                        if (dilated[i] > 0 && alpha[i] == 0)
                        {
                            byte sa = (byte)Mathf.RoundToInt(dilated[i] * sOpacity);
                            AlphaComposite(ref result[i], new Color32(cr, cg, cb, sa));
                        }
                    }
                }
                else if (stroke.Position == PsdTextStrokePosition.Inside)
                {
                    int insideWidth = Mathf.CeilToInt(stroke.Size);
                    byte[] eroded = ErodeAlpha(alpha, newW, newH, insideWidth);
                    for (int i = 0; i < result.Length; i++)
                    {
                        if (alpha[i] > 0 && eroded[i] == 0)
                        {
                            byte sa = (byte)Mathf.RoundToInt(alpha[i] * sOpacity);
                            AlphaComposite(ref result[i], new Color32(cr, cg, cb, sa));
                        }
                    }
                }
            }

            // ── 5. 颜色叠加应用到基础像素 ──
            if (hasOverlay)
            {
                foreach (var overlay in colorOverlays)
                {
                    byte cr2 = overlay.Color.R, cg2 = overlay.Color.G, cb2 = overlay.Color.B;
                    float op = overlay.Opacity;
                    for (int i = 0; i < baseExpanded.Length; i++)
                    {
                        if (baseExpanded[i].a > 0)
                        {
                            baseExpanded[i].r = (byte)Mathf.RoundToInt(Mathf.Lerp(baseExpanded[i].r, cr2, op));
                            baseExpanded[i].g = (byte)Mathf.RoundToInt(Mathf.Lerp(baseExpanded[i].g, cg2, op));
                            baseExpanded[i].b = (byte)Mathf.RoundToInt(Mathf.Lerp(baseExpanded[i].b, cb2, op));
                        }
                    }
                }
            }

            // ── 6. 将原始图像合成到效果之上（SrcOver）──
            for (int i = 0; i < result.Length; i++)
            {
                AlphaComposite(ref result[i], baseExpanded[i]);
            }

            // ── 7. 裁剪空白边缘 ──
            CropTransparentEdges(ref result, ref newW, ref newH, ref expandLeft, ref expandTop, padL, padR, padT, padB);

            Debug.Log($"[EffectsBaker:Baked] 图层='{layer.Name}', base={baseW}x{baseH}, baked={newW}x{newH}, expand=L{expandLeft}/T{expandTop}");

            var tex = new Texture2D(newW, newH, TextureFormat.RGBA32, false);
            tex.alphaIsTransparency = true;
            tex.SetPixels32(result);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 检查图层是否有颜色叠加效果，有则直接修改纹理像素（支持多个叠加）。
        /// </summary>
        public static void TryApplyColorOverlay(Texture2D tex, PsdLayer layer)
        {
            if (tex == null || layer == null) return;
            if (!layer.TryGetAllLayerEffects(out _, out _, out var colorOverlays, out _)) return;
            foreach (var overlay in colorOverlays)
            {
                ApplyColorOverlayInPlace(tex, overlay);
            }
        }

        // ═══════════════ 像素操作 ═══════════════

        private static void AlphaComposite(ref Color32 dst, Color32 src)
        {
            if (src.a == 0) return;
            if (dst.a == 0 || src.a == 255)
            {
                dst = src;
                return;
            }

            // Porter-Duff SrcOver
            int sa = src.a;
            int da = dst.a;
            int outA = sa + da * (255 - sa) / 255;
            if (outA == 0) return;

            dst.r = (byte)((src.r * sa + dst.r * da * (255 - sa) / 255) / outA);
            dst.g = (byte)((src.g * sa + dst.g * da * (255 - sa) / 255) / outA);
            dst.b = (byte)((src.b * sa + dst.b * da * (255 - sa) / 255) / outA);
            dst.a = (byte)outA;
        }

        private static void ApplyColorOverlayInPlace(Texture2D tex, PsdColorOverlayInfo overlay)
        {
            var px = tex.GetPixels32();
            byte cr = overlay.Color.R, cg = overlay.Color.G, cb = overlay.Color.B;
            float op = overlay.Opacity;
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a > 0)
                {
                    px[i].r = (byte)Mathf.RoundToInt(Mathf.Lerp(px[i].r, cr, op));
                    px[i].g = (byte)Mathf.RoundToInt(Mathf.Lerp(px[i].g, cg, op));
                    px[i].b = (byte)Mathf.RoundToInt(Mathf.Lerp(px[i].b, cb, op));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
        }

        // ═══════════════ Alpha 偏移 ═══════════════

        private static byte[] OffsetAlpha(byte[] alpha, int w, int h, int dx, int dy)
        {
            byte[] result = new byte[w * h];
            for (int y = 0; y < h; y++)
            {
                int srcY = y - dy;
                if (srcY < 0 || srcY >= h) continue;
                for (int x = 0; x < w; x++)
                {
                    int srcX = x - dx;
                    if (srcX < 0 || srcX >= w) continue;
                    result[y * w + x] = alpha[srcY * w + srcX];
                }
            }
            return result;
        }

        // ═══════════════ Alpha 膨胀 (描边) ═══════════════

        /// <summary>
        /// 迭代 3×3 膨胀，执行 iterations 次，近似圆形结构元素。
        /// </summary>
        private static byte[] DilateAlpha(byte[] alpha, int w, int h, int iterations)
        {
            byte[] src = new byte[alpha.Length];
            byte[] dst = new byte[alpha.Length];
            Buffer.BlockCopy(alpha, 0, src, 0, alpha.Length);

            for (int iter = 0; iter < iterations; iter++)
            {
                for (int y = 0; y < h; y++)
                {
                    int yOff = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        byte maxVal = src[yOff + x];
                        if (maxVal == 255) { dst[yOff + x] = 255; continue; }

                        // 检查 3×3 邻域
                        int yMin = y > 0 ? y - 1 : 0;
                        int yMax = y < h - 1 ? y + 1 : h - 1;
                        int xMin = x > 0 ? x - 1 : 0;
                        int xMax = x < w - 1 ? x + 1 : w - 1;

                        for (int ny = yMin; ny <= yMax; ny++)
                        {
                            int nOff = ny * w;
                            for (int nx = xMin; nx <= xMax; nx++)
                            {
                                if (src[nOff + nx] > maxVal)
                                    maxVal = src[nOff + nx];
                            }
                        }
                        dst[yOff + x] = maxVal;
                    }
                }

                // 交换 buffer
                var tmp = src;
                src = dst;
                dst = tmp;
            }
            return src;
        }

        /// <summary>
        /// 迭代 3×3 腐蚀，执行 iterations 次（用于 Inside 描边）。
        /// </summary>
        private static byte[] ErodeAlpha(byte[] alpha, int w, int h, int iterations)
        {
            byte[] src = new byte[alpha.Length];
            byte[] dst = new byte[alpha.Length];
            Buffer.BlockCopy(alpha, 0, src, 0, alpha.Length);

            for (int iter = 0; iter < iterations; iter++)
            {
                for (int y = 0; y < h; y++)
                {
                    int yOff = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        byte minVal = src[yOff + x];
                        if (minVal == 0) { dst[yOff + x] = 0; continue; }

                        int yMin = y > 0 ? y - 1 : 0;
                        int yMax = y < h - 1 ? y + 1 : h - 1;
                        int xMin = x > 0 ? x - 1 : 0;
                        int xMax = x < w - 1 ? x + 1 : w - 1;

                        for (int ny = yMin; ny <= yMax; ny++)
                        {
                            int nOff = ny * w;
                            for (int nx = xMin; nx <= xMax; nx++)
                            {
                                if (src[nOff + nx] < minVal)
                                    minVal = src[nOff + nx];
                            }
                        }
                        dst[yOff + x] = minVal;
                    }
                }

                var tmp = src;
                src = dst;
                dst = tmp;
            }
            return src;
        }

        // ═══════════════ Box Blur (可分离) ═══════════════

        private static void BoxBlur(byte[] data, int w, int h, int radius)
        {
            byte[] temp = new byte[data.Length];

            // 水平 pass
            for (int y = 0; y < h; y++)
            {
                int yOff = y * w;
                int runSum = 0;
                int count = 0;

                // 初始化窗口
                for (int x = 0; x <= radius && x < w; x++)
                {
                    runSum += data[yOff + x];
                    count++;
                }
                temp[yOff] = (byte)(runSum / count);

                for (int x = 1; x < w; x++)
                {
                    int addX = x + radius;
                    int subX = x - radius - 1;
                    if (addX < w) { runSum += data[yOff + addX]; count++; }
                    if (subX >= 0) { runSum -= data[yOff + subX]; count--; }
                    temp[yOff + x] = (byte)(runSum / count);
                }
            }

            // 垂直 pass
            for (int x = 0; x < w; x++)
            {
                int runSum = 0;
                int count = 0;

                for (int y = 0; y <= radius && y < h; y++)
                {
                    runSum += temp[y * w + x];
                    count++;
                }
                data[x] = (byte)(runSum / count);

                for (int y = 1; y < h; y++)
                {
                    int addY = y + radius;
                    int subY = y - radius - 1;
                    if (addY < h) { runSum += temp[addY * w + x]; count++; }
                    if (subY >= 0) { runSum -= temp[subY * w + x]; count--; }
                    data[y * w + x] = (byte)(runSum / count);
                }
            }
        }

        // ═══════════════ 裁剪空白边缘 ═══════════════

        /// <summary>
        /// 在效果渲染后，扫描实际有效像素边界，裁剪多余的透明像素。
        /// 同时修正 expandLeft/expandTop 以反映实际扩展量。
        /// </summary>
        private static void CropTransparentEdges(
            ref Color32[] pixels, ref int w, ref int h,
            ref int expandLeft, ref int expandTop,
            int origPadL, int origPadR, int origPadT, int origPadB)
        {
            // 扫描有效像素边界
            int minX = w, maxX = -1, minY = h, maxY = -1;
            for (int y = 0; y < h; y++)
            {
                int yOff = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (pixels[yOff + x].a > 0)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (maxX < 0) return; // 完全透明，不裁剪

            int cropW = maxX - minX + 1;
            int cropH = maxY - minY + 1;

            // 如果几乎没有裁剪空间，跳过
            if (cropW >= w - 2 && cropH >= h - 2) return;

            Color32[] cropped = new Color32[cropW * cropH];
            for (int y = 0; y < cropH; y++)
            {
                int srcOff = (y + minY) * w + minX;
                int dstOff = y * cropW;
                Array.Copy(pixels, srcOff, cropped, dstOff, cropW);
            }

            // 修正扩展量: 实际左侧扩展 = 原始padL - 被裁掉的左侧空白
            expandLeft = Mathf.Max(0, origPadL - minX);
            // 修正扩展量: 实际顶部扩展 = 原始padT - 被裁掉的顶部空白
            // Unity纹理 y=0=底, padT=高y方向, 被裁顶部 = (h - 1 - maxY)
            expandTop = Mathf.Max(0, origPadT - (h - 1 - maxY));

            pixels = cropped;
            w = cropW;
            h = cropH;
        }
    }
}
#endif
