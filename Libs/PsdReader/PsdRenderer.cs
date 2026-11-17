using System;
using System.Collections.Generic;
using System.Linq;

namespace PsdReader
{
    /// <summary>Composites PSD layers into RGBA32 or RGBA64 render targets.</summary>
    public static class PsdRenderer
    {
        public static RenderedImage Render(this PsdDocument document, bool includeHidden = false,
            bool applyClippingMasks = true)
        {
            if (document == null) return null;
            int bitDepth = document.Depth == 16 ? 16 : 8;
            var canvas = new RenderedImage(0, 0, document.Width, document.Height, bitDepth);
            ComposeLayers(canvas, document.Childs, includeHidden, applyClippingMasks, 1f, bitDepth);
            return canvas;
        }

        public static RenderedImage RenderPreview(this PsdDocument document, bool includeHidden = false,
            bool applyClippingMasks = true)
        {
            if (document == null) return null;
            float scale = ResolvePreviewScale(document.Width, document.Height);
            var canvas = new RenderedImage(0, 0, ScaleDimension(document.Width, scale),
                ScaleDimension(document.Height, scale), 8);
            ComposeLayers(canvas, document.Childs, includeHidden, applyClippingMasks, scale, 8);
            return canvas;
        }

        public static RenderedImage Render(this PsdLayer layer, bool includeHidden = false,
            bool applyClippingMasks = true)
        {
            if (layer == null) return null;
            int bitDepth = layer.Depth == 16 ? 16 : 8;
            return RenderInternal(layer, includeHidden, applyClippingMasks, 1f, bitDepth);
        }

        public static RenderedImage RenderPreview(this PsdLayer layer, bool includeHidden = false,
            bool applyClippingMasks = true)
        {
            if (layer == null) return null;
            float scale = ResolvePreviewScale(layer.Width, layer.Height);
            return RenderInternal(layer, includeHidden, applyClippingMasks, scale, 8);
        }

        public static RenderedImage MergeLayers(IEnumerable<PsdLayer> layers, bool includeHidden = false,
            bool applyClippingMasks = true, bool preview = false)
        {
            if (layers == null) return null;
            PsdLayer[] source = layers.Where(layer => layer != null).ToArray();
            if (source.Length == 0) return null;

            int left = source.Min(layer => layer.Left);
            int top = source.Min(layer => layer.Top);
            int right = source.Max(layer => layer.Right);
            int bottom = source.Max(layer => layer.Bottom);
            float scale = preview ? ResolvePreviewScale(right - left, bottom - top) : 1f;
            int bitDepth = preview ? 8 : (source.Any(layer => layer.Depth == 16) ? 16 : 8);
            var canvas = new RenderedImage(ScaleCoordinate(left, scale), ScaleCoordinate(top, scale),
                ScaleDimension(right - left, scale), ScaleDimension(bottom - top, scale), bitDepth);
            ComposeLayers(canvas, source, includeHidden, applyClippingMasks, scale, bitDepth);
            return canvas;
        }

        public static byte MultiplyAlpha(byte alpha, float opacity)
            => RenderedImage.MultiplyAlpha(alpha, opacity);

        public static ushort MultiplyAlpha(ushort alpha, float opacity)
            => RenderedImage.MultiplyAlpha(alpha, opacity);

        public static byte MultiplyAlpha(byte alpha, byte mask)
            => RenderedImage.MultiplyAlpha(alpha, mask);

        public static ushort MultiplyAlpha(ushort alpha, ushort mask)
            => RenderedImage.MultiplyAlpha(alpha, mask);

        private static RenderedImage RenderInternal(PsdLayer layer, bool includeHidden, bool applyClipping,
            float scale, int bitDepth)
        {
            return layer.IsGroup
                ? RenderGroup(layer, includeHidden, applyClipping, scale, bitDepth)
                : RenderLeaf(layer, scale, bitDepth);
        }

        private static RenderedImage RenderGroup(PsdLayer group, bool includeHidden, bool applyClipping,
            float scale, int bitDepth)
        {
            var canvas = new RenderedImage(ScaleCoordinate(group.Left, scale), ScaleCoordinate(group.Top, scale),
                ScaleDimension(group.Width, scale), ScaleDimension(group.Height, scale), bitDepth);
            ComposeLayers(canvas, group.Childs, includeHidden, applyClipping, scale, bitDepth);
            canvas.MultiplyOpacity(group.Opacity);
            return canvas;
        }

        private static RenderedImage RenderLeaf(PsdLayer layer, float scale, int bitDepth)
        {
            if (layer == null || !layer.HasImage || layer.Width <= 0 || layer.Height <= 0)
                return null;

            Channel red = null, green = null, blue = null, alpha = null, mask = null;
            Channel[] channels = layer.Channels;
            for (int i = 0; i < channels.Length; i++)
            {
                switch (channels[i].Type)
                {
                    case ChannelType.Red: red = channels[i]; break;
                    case ChannelType.Green: green = channels[i]; break;
                    case ChannelType.Blue: blue = channels[i]; break;
                    case ChannelType.Alpha: alpha = channels[i]; break;
                    case ChannelType.Mask: mask = channels[i]; break;
                }
            }

            if (red?.Data == null || green?.Data == null || blue?.Data == null)
                return null;

            int targetWidth = ScaleDimension(layer.Width, scale);
            int targetHeight = ScaleDimension(layer.Height, scale);
            var image = new RenderedImage(ScaleCoordinate(layer.Left, scale), ScaleCoordinate(layer.Top, scale),
                targetWidth, targetHeight, bitDepth);
            LayerMask layerMask = layer.Records?.Mask;
            bool hasAlpha = alpha?.Data != null;
            bool hasMask = mask?.Data != null && layerMask != null;

            for (int row = 0; row < targetHeight; row++)
            {
                int sourceRow = ScaleSampleCoordinate(row, scale, layer.Height);
                for (int column = 0; column < targetWidth; column++)
                {
                    int sourceColumn = ScaleSampleCoordinate(column, scale, layer.Width);
                    int sampleIndex = sourceRow * layer.Width + sourceColumn;

                    if (bitDepth == 16)
                    {
                        ushort a = hasAlpha ? Read16(alpha, sampleIndex) : ushort.MaxValue;
                        a = RenderedImage.MultiplyAlpha(a, layer.Opacity);
                        if (hasMask)
                            a = ApplyLayerMask16(a, mask, layerMask, layer.Left + sourceColumn,
                                layer.Top + sourceRow);
                        image.SetPixel(column, row, Read16(red, sampleIndex), Read16(green, sampleIndex),
                            Read16(blue, sampleIndex), a);
                    }
                    else
                    {
                        byte a = hasAlpha ? Read8(alpha, sampleIndex) : byte.MaxValue;
                        a = RenderedImage.MultiplyAlpha(a, layer.Opacity);
                        if (hasMask)
                            a = ApplyLayerMask8(a, mask, layerMask, layer.Left + sourceColumn,
                                layer.Top + sourceRow);
                        image.SetPixel(column, row, Read8(red, sampleIndex), Read8(green, sampleIndex),
                            Read8(blue, sampleIndex), a);
                    }
                }
            }

            return image;
        }

        private static void ComposeLayers(RenderedImage canvas, PsdLayer[] layers, bool includeHidden,
            bool applyClipping, float scale, int bitDepth)
        {
            if (canvas == null || canvas.IsEmpty || layers == null || layers.Length == 0)
                return;

            byte[] clippingMask8 = null;
            ushort[] clippingMask16 = null;
            for (int i = 0; i < layers.Length; i++)
            {
                PsdLayer layer = layers[i];
                if (layer == null || (!includeHidden && !layer.IsVisible))
                    continue;

                RenderedImage rendered = RenderInternal(layer, includeHidden, applyClipping, scale, bitDepth);
                if (layer.IsClipping)
                {
                    if (applyClipping && rendered != null)
                    {
                        if (bitDepth == 16 && clippingMask16 != null)
                            ApplyClippingMask(rendered, clippingMask16, canvas);
                        else if (bitDepth != 16 && clippingMask8 != null)
                            ApplyClippingMask(rendered, clippingMask8, canvas);
                    }
                }
                else
                {
                    if (bitDepth == 16)
                    {
                        clippingMask16 = rendered == null || rendered.IsEmpty
                            ? null : BuildPlacedAlphaMask16(rendered, canvas);
                    }
                    else
                    {
                        clippingMask8 = rendered == null || rendered.IsEmpty
                            ? null : BuildPlacedAlphaMask8(rendered, canvas);
                    }
                }

                CompositeImage(canvas, rendered);
            }
        }

        private static void CompositeImage(RenderedImage destination, RenderedImage source)
        {
            if (destination == null || source == null || destination.IsEmpty || source.IsEmpty)
                return;

            int offsetX = source.Left - destination.Left;
            int offsetY = source.Top - destination.Top;
            int columnStart = Math.Max(0, -offsetX);
            int columnEnd = Math.Min(source.Width, destination.Width - offsetX);
            if (columnStart >= columnEnd) return;

            for (int row = 0; row < source.Height; row++)
            {
                int destinationRow = offsetY + row;
                if (destinationRow < 0 || destinationRow >= destination.Height) continue;
                for (int column = columnStart; column < columnEnd; column++)
                {
                    int sourceOffset = source.GetPixelOffset(column, row);
                    int destinationColumn = offsetX + column;
                    if (destination.IsHighBitDepth)
                    {
                        destination.CompositePixel(destinationColumn, destinationRow,
                            ReadRendered16(source, sourceOffset), ReadRendered16(source, sourceOffset + 1),
                            ReadRendered16(source, sourceOffset + 2), ReadRendered16(source, sourceOffset + 3));
                    }
                    else
                    {
                        destination.CompositePixel(destinationColumn, destinationRow,
                            ReadRendered8(source, sourceOffset), ReadRendered8(source, sourceOffset + 1),
                            ReadRendered8(source, sourceOffset + 2), ReadRendered8(source, sourceOffset + 3));
                    }
                }
            }
        }

        private static byte[] BuildPlacedAlphaMask8(RenderedImage source, RenderedImage canvas)
        {
            var mask = new byte[canvas.Width * canvas.Height];
            PlaceAlpha(source, canvas, (sourceOffset, destinationIndex) =>
                mask[destinationIndex] = ReadRendered8(source, sourceOffset + 3));
            return mask;
        }

        private static ushort[] BuildPlacedAlphaMask16(RenderedImage source, RenderedImage canvas)
        {
            var mask = new ushort[canvas.Width * canvas.Height];
            PlaceAlpha(source, canvas, (sourceOffset, destinationIndex) =>
                mask[destinationIndex] = ReadRendered16(source, sourceOffset + 3));
            return mask;
        }

        private static void PlaceAlpha(RenderedImage source, RenderedImage canvas, Action<int, int> place)
        {
            int offsetX = source.Left - canvas.Left;
            int offsetY = source.Top - canvas.Top;
            for (int row = 0; row < source.Height; row++)
            {
                int destinationRow = offsetY + row;
                if (destinationRow < 0 || destinationRow >= canvas.Height) continue;
                for (int column = 0; column < source.Width; column++)
                {
                    int destinationColumn = offsetX + column;
                    if (destinationColumn < 0 || destinationColumn >= canvas.Width) continue;
                    place(source.GetPixelOffset(column, row), destinationRow * canvas.Width + destinationColumn);
                }
            }
        }

        private static void ApplyClippingMask(RenderedImage source, byte[] mask, RenderedImage canvas)
        {
            ApplyClippingMask(source, canvas, (sourceX, sourceY, maskIndex) =>
                source.MultiplyPixelAlpha(sourceX, sourceY, mask[maskIndex]));
        }

        private static void ApplyClippingMask(RenderedImage source, ushort[] mask, RenderedImage canvas)
        {
            ApplyClippingMask(source, canvas, (sourceX, sourceY, maskIndex) =>
                source.MultiplyPixelAlpha(sourceX, sourceY, mask[maskIndex]));
        }

        private static void ApplyClippingMask(RenderedImage source, RenderedImage canvas,
            Action<int, int, int> apply)
        {
            int offsetX = source.Left - canvas.Left;
            int offsetY = source.Top - canvas.Top;
            for (int row = 0; row < source.Height; row++)
            {
                int maskRow = offsetY + row;
                for (int column = 0; column < source.Width; column++)
                {
                    int maskColumn = offsetX + column;
                    if (maskColumn < 0 || maskColumn >= canvas.Width || maskRow < 0 || maskRow >= canvas.Height)
                    {
                        source.MultiplyPixelAlpha(column, row, (byte)0);
                        continue;
                    }
                    apply(column, row, maskRow * canvas.Width + maskColumn);
                }
            }
        }

        private static byte ApplyLayerMask8(byte alpha, Channel mask, LayerMask bounds, int x, int y)
        {
            if (x < bounds.Left || x >= bounds.Right || y < bounds.Top || y >= bounds.Bottom)
                return RenderedImage.MultiplyAlpha(alpha, bounds.DefaultColor);
            int index = (y - bounds.Top) * bounds.Width + (x - bounds.Left);
            return index >= 0 && index < mask.Data.Length
                ? RenderedImage.MultiplyAlpha(alpha, Read8(mask, index)) : alpha;
        }

        private static ushort ApplyLayerMask16(ushort alpha, Channel mask, LayerMask bounds, int x, int y)
        {
            if (x < bounds.Left || x >= bounds.Right || y < bounds.Top || y >= bounds.Bottom)
                return RenderedImage.MultiplyAlpha(alpha, RenderedImage.ToUShort(bounds.DefaultColor));
            int index = (y - bounds.Top) * bounds.Width + (x - bounds.Left);
            return index >= 0 && index < mask.Data.Length
                ? RenderedImage.MultiplyAlpha(alpha, Read16(mask, index)) : alpha;
        }

        private static byte Read8(Channel channel, int index)
        {
            if (channel?.Data == null || index < 0 || index >= channel.Data.Length) return 0;
            return channel.Data[index];
        }

        private static ushort Read16(Channel channel, int index)
        {
            if (channel?.Data16 != null && index >= 0 && index < channel.Data16.Length)
                return channel.Data16[index];
            return RenderedImage.ToUShort(Read8(channel, index));
        }

        private static byte ReadRendered8(RenderedImage image, int index)
        {
            return image.IsHighBitDepth
                ? RenderedImage.ToByte(image.RawPixels16[index]) : image.RawPixels[index];
        }

        private static ushort ReadRendered16(RenderedImage image, int index)
        {
            return image.IsHighBitDepth
                ? image.RawPixels16[index] : RenderedImage.ToUShort(image.RawPixels[index]);
        }

        private static float ResolvePreviewScale(int width, int height)
        {
            int maxDimension = Math.Max(Math.Max(0, width), Math.Max(0, height));
            return maxDimension > 0 ? Math.Min(0.5f, 1024f / maxDimension) : 0.5f;
        }

        private static int ScaleDimension(int value, float scale)
        {
            if (value <= 0) return 0;
            return scale >= 0.9999f ? value : Math.Max(1, Round(value * scale));
        }

        private static int ScaleCoordinate(int value, float scale)
        {
            return scale >= 0.9999f ? value : Round(value * scale);
        }

        private static int ScaleSampleCoordinate(int value, float scale, int sourceLimit)
        {
            if (sourceLimit <= 0) return 0;
            int result = scale >= 0.9999f ? value : (int)Math.Floor(value / scale);
            return Math.Max(0, Math.Min(sourceLimit - 1, result));
        }

        private static int Round(float value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }
    }
}
