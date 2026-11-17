#!/usr/bin/env python3
"""
PSD图层视觉分析辅助工具 v1.1
用感知哈希(pHash)精确匹配视觉相同的图层，并输出基础视觉特征

用法:  python psd_dedup.py <thumbnail_folder>
输入:  文件夹中的 <layer_id>.png 缩略图（由 JSX 脚本导出）
输出:
  <thumbnail_folder>/dedup_result.json
  <thumbnail_folder>/vision_features.json

依赖安装:  pip install Pillow imagehash
"""
import os
import sys
import json
from collections import Counter

try:
    from PIL import Image
    import imagehash
except ImportError:
    print("ERROR: 缺少依赖。请运行: pip install Pillow imagehash")
    sys.exit(1)

# 汉明距离阈值：< 此值视为同一图像
HAMMING_THRESHOLD = 8
ALPHA_THRESHOLD = 8


def _load_image(path):
    return Image.open(path).convert("RGBA")


def _alpha_bbox(img):
    alpha = img.getchannel("A")
    mask = alpha.point(lambda p: 255 if p > ALPHA_THRESHOLD else 0)
    return mask.getbbox()


def _edge_density(img):
    """轻量边缘复杂度估算，避免依赖 OpenCV。"""
    gray = img.convert("L")
    w, h = gray.size
    if w < 3 or h < 3:
        return 0.0

    pixels = gray.load()
    step_x = max(1, w // 96)
    step_y = max(1, h // 96)
    total = 0
    edges = 0
    for y in range(step_y, h - step_y, step_y):
        for x in range(step_x, w - step_x, step_x):
            gx = abs(int(pixels[x + step_x, y]) - int(pixels[x - step_x, y]))
            gy = abs(int(pixels[x, y + step_y]) - int(pixels[x, y - step_y]))
            if gx + gy > 36:
                edges += 1
            total += 1
    return round(edges / float(total or 1), 4)


def _dominant_alpha_ratio(img):
    alpha = img.getchannel("A")
    values = alpha.resize((min(64, img.width), min(64, img.height))).getdata()
    opaque = sum(1 for a in values if a > ALPHA_THRESHOLD)
    return round(opaque / float(len(values) or 1), 4)


def _color_variety(img):
    small = img.resize((min(48, img.width), min(48, img.height))).convert("RGBA")
    colors = Counter()
    for r, g, b, a in small.getdata():
        if a <= ALPHA_THRESHOLD:
            continue
        colors[(r // 16, g // 16, b // 16)] += 1
    return len(colors)


def _analyze_visual_features(path):
    img = _load_image(path)
    bbox = _alpha_bbox(img)
    w, h = img.size
    aspect = round(w / float(h or 1), 4)
    feature = {
        "width": w,
        "height": h,
        "aspect": aspect,
        "alpha_bbox": None,
        "opaque_ratio": _dominant_alpha_ratio(img),
        "edge_density": _edge_density(img),
        "color_variety": _color_variety(img),
        "visual_role_hint": "unknown",
        "nine_slice_hint": "off"
    }

    if bbox:
        left, top, right, bottom = bbox
        bw = right - left
        bh = bottom - top
        feature["alpha_bbox"] = [left, top, bw, bh]
        content_aspect = bw / float(bh or 1)
        content_ratio = (bw * bh) / float((w * h) or 1)
        feature["content_aspect"] = round(content_aspect, 4)
        feature["content_ratio"] = round(content_ratio, 4)

        if bw >= 80 and bh >= 32 and 1.6 <= content_aspect <= 8.0 and content_ratio >= 0.35:
            feature["visual_role_hint"] = "button_or_panel_bar"
        elif max(bw, bh) <= 128 and content_ratio <= 0.75:
            feature["visual_role_hint"] = "icon"
        elif content_ratio >= 0.85 and w >= 300 and h >= 300:
            feature["visual_role_hint"] = "panel_or_background"
        else:
            feature["visual_role_hint"] = "image"

        if bw >= 96 and bh >= 48 and feature["edge_density"] <= 0.22 and feature["color_variety"] <= 96:
            feature["nine_slice_hint"] = "auto"

    return feature


def analyze_thumbnails(folder):
    """分析缩略图文件夹，找出视觉重复的图层并输出视觉特征"""
    if not os.path.isdir(folder):
        print("ERROR: 文件夹不存在: " + folder)
        sys.exit(1)

    meta = {}
    meta_path = os.path.join(folder, "thumb_meta.json")
    if os.path.exists(meta_path):
        try:
            with open(meta_path, "r", encoding="utf-8") as f:
                meta = json.load(f).get("layers", {})
        except Exception as e:
            print("WARN: 无法读取 thumb_meta.json: %s" % str(e))

    # 1. 计算每个图层的 pHash
    layer_hashes = {}
    features = {}
    for f in sorted(os.listdir(folder)):
        if not f.lower().endswith('.png'):
            continue
        layer_id = os.path.splitext(f)[0]
        try:
            image_path = os.path.join(folder, f)
            img = _load_image(image_path)
            h = imagehash.phash(img)
            layer_hashes[layer_id] = h
            features[layer_id] = _analyze_visual_features(image_path)
            if layer_id in meta:
                original = meta[layer_id]
                ow = int(original.get("width") or features[layer_id]["width"])
                oh = int(original.get("height") or features[layer_id]["height"])
                features[layer_id]["original_width"] = ow
                features[layer_id]["original_height"] = oh
                features[layer_id]["original_aspect"] = round(ow / float(oh or 1), 4)
                features[layer_id]["name"] = original.get("name", "")
                if "bounds" in original:
                    features[layer_id]["bounds"] = original["bounds"]
                if features[layer_id]["visual_role_hint"] == "unknown":
                    original_aspect = features[layer_id]["original_aspect"]
                    if ow >= 80 and oh >= 32 and 1.6 <= original_aspect <= 8.0:
                        features[layer_id]["visual_role_hint"] = "button_or_panel_bar"
                    elif max(ow, oh) <= 128:
                        features[layer_id]["visual_role_hint"] = "icon"
        except Exception as e:
            print("WARN: 跳过 %s: %s" % (f, str(e)))

    if not layer_hashes:
        print("INFO: 未找到任何 PNG 缩略图")
        return

    # 2. 按相似度分组（贪心聚类）
    groups = []  # list of [layer_id, ...]
    assigned = set()

    ids = list(layer_hashes.keys())
    for i in range(len(ids)):
        if ids[i] in assigned:
            continue
        group = [ids[i]]
        assigned.add(ids[i])
        h_i = layer_hashes[ids[i]]

        for j in range(i + 1, len(ids)):
            if ids[j] in assigned:
                continue
            h_j = layer_hashes[ids[j]]
            if h_i - h_j < HAMMING_THRESHOLD:
                group.append(ids[j])
                assigned.add(ids[j])

        if len(group) > 1:
            groups.append(group)

    # 3. 生成结果 JSON
    # 格式: { "layer_id": "canonical_layer_id", ... }
    # canonical = 组中第一个图层的 ID
    result = {
        "version": 1,
        "threshold": HAMMING_THRESHOLD,
        "total_layers": len(layer_hashes),
        "duplicate_groups": len(groups),
        "duplicates": {}
    }

    for group in groups:
        canonical = group[0]
        for member in group[1:]:
            result["duplicates"][member] = canonical

    # 4. 写入结果
    output_path = os.path.join(folder, "dedup_result.json")
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(result, f, indent=2, ensure_ascii=False)

    feature_path = os.path.join(folder, "vision_features.json")
    with open(feature_path, 'w', encoding='utf-8') as f:
        json.dump({
            "version": 1,
            "total_layers": len(features),
            "features": features
        }, f, indent=2, ensure_ascii=False)

    print("OK: 分析完成")
    print("  总图层: %d" % result["total_layers"])
    print("  重复组: %d" % result["duplicate_groups"])
    print("  重复图层: %d" % len(result["duplicates"]))
    print("  结果: %s" % output_path)
    print("  特征: %s" % feature_path)
    return output_path


if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("用法: python psd_dedup.py <thumbnail_folder>")
        print("  JSX 脚本会自动将缩略图导出到临时文件夹")
        sys.exit(0)

    analyze_thumbnails(sys.argv[1])
