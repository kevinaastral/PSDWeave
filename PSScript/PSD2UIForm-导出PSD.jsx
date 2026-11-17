// ============================================
// PSD智能导出脚本 v3.4
// 功能：智能命名 + 复用标记 + 优化导出
// 作者：sunsvip
// 更新日期：2026-07-12
// ============================================

// ========== 配置选项 ==========
var CONFIG = {
    // 调试模式：true = 只处理不导出不还原
    DEBUG_MODE: false,

    // 是否跳过隐藏图层
    SKIP_HIDDEN_LAYERS: false,
    // 导出文件名后缀
    EXPORT_SUFFIX: "",

    // ── 智能命名 ──
    // true = 只重命名 PS 默认名（"图层1"等），保留用户自定义名
    RENAME_ONLY_GENERIC: true,
    // true = 尺寸相同的图片标记 @ref:xxx，让插件复用同一 Sprite
    MARK_DUPLICATES_REF: true,

    // ── 分类阈值 ──
    // 面积占画布比例 > 此值 → bg_ (背景)
    BG_AREA_RATIO: 0.50,
    // max(w,h) < 此值px → icon_ (小图标)
    ICON_MAX_PX: 96,
    // 按钮组宽度 < 画布宽度的此比例
    BTN_MAX_WIDTH_RATIO: 0.50,
    // 滑块轨道最小长宽比
    SLIDER_ASPECT_MIN: 3.0,

    // ── 图片渲染 ──
    // ps_visual = Photoshop 烘焙图片特效/蒙版（默认）
    // unity_editable = 保留常规图片特效给 Unity 兼容处理
    IMAGE_RENDER_MODE: "ps_visual"
};
var LAYER_ANALYSIS_CACHE = {};
// =============================

function stripExportSuffix(baseName) {
    baseName = String(baseName || "");
    if (CONFIG.EXPORT_SUFFIX) {
        var escapedSuffix = CONFIG.EXPORT_SUFFIX.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
        baseName = baseName.replace(new RegExp(escapedSuffix + "$", "i"), "");
    }
    return baseName.replace(/_UGUI$/i, "");
}

var CANONICAL_SONGTI_FONT_NAME = "STSongti-SC-Regular";

function normalizeFontKey(value) {
    return String(value || "")
        .replace(/^\[/, "")
        .replace(/\]$/, "")
        .replace(/[^A-Za-z0-9\u4e00-\u9fff]+/g, "")
        .toLowerCase();
}

function isSongtiRelatedFontName(fontName) {
    var key = normalizeFontKey(fontName);
    if (!key) return false;
    return key.indexOf("songti") >= 0 ||
        key.indexOf("stsong") >= 0 ||
        key.indexOf("simsun") >= 0 ||
        key.indexOf("nsimsun") >= 0 ||
        key.indexOf("\u5b8b\u4f53") >= 0 ||
        key.indexOf("\u5b8b\u9ad4") >= 0 ||
        key.indexOf("\u534e\u6587\u5b8b") >= 0 ||
        key.indexOf("\u83ef\u6587\u5b8b") >= 0 ||
        key.indexOf("\u7ecf\u5178\u5b8b") >= 0 ||
        key.indexOf("\u7d93\u5178\u5b8b") >= 0;
}

function normalizeSongtiTextFonts(layers) {
    var stats = { changed: 0, skipped: 0, errors: [] };

    function walk(container) {
        for (var i = 0; i < container.length; i++) {
            var layer = container[i];
            try {
                if (layer.typename === "LayerSet") {
                    walk(layer.layers);
                    continue;
                }

                if (layer.typename !== "ArtLayer") continue;
                var isText = false;
                try { isText = layer.kind === LayerKind.TEXT; } catch (kindError) {}
                if (!isText) continue;

                var currentFont = "";
                try { currentFont = layer.textItem.font || ""; } catch (fontError) {}
                if (!isSongtiRelatedFontName(currentFont)) {
                    stats.skipped++;
                    continue;
                }

                if (String(currentFont).toLowerCase() === CANONICAL_SONGTI_FONT_NAME.toLowerCase()) {
                    stats.skipped++;
                    continue;
                }

                try { layer.allLocked = false; } catch (lockError) {}
                app.activeDocument.activeLayer = layer;
                layer.textItem.font = CANONICAL_SONGTI_FONT_NAME;
                stats.changed++;
            } catch (e) {
                stats.errors.push(layer.name + ": " + e.message);
            }
        }
    }

    walk(layers);
    return stats;
}

// 检查是否有打开的文档
if (app.documents.length === 0) {
    alert("请先打开一个PSD文件！");
} else {
    showMainMenu();
}

// ╔══════════════════════════════════════════╗
// ║           工 具 函 数                    ║
// ╚══════════════════════════════════════════╝

/**
 * 判断是否为 Photoshop 自动生成的通用图层名
 */
function isGenericName(name) {
    if (!name) return true;
    // 去掉"副本/拷贝/copy"后缀，再检查
    var n = name.replace(/\s*(副本|拷贝)\s*\d*$/g, '')
               .replace(/\s*copy\s*\d*$/gi, '')
               .replace(/^\s+|\s+$/g, '');
    // 中英文 PS 默认名
    if (/^(Layer|图层|Rectangle|矩形|Shape|形状|Ellipse|椭圆|Line|线条|Path|路径|Group|组|Background|背景|Polygon|多边形)\s*\d*$/i.test(n)) {
        return true;
    }
    // 纯数字
    if (/^\d+$/.test(n)) return true;
    return false;
}

/**
 * 判断图层名是否已经带有插件标签 (.img .bt .txt .sld 等)
 */
function hasPluginTag(name) {
    if (!name) return false;
    return /\.(img|rimg|txt|tmptxt|msk|col|bg|bt|tmpbt|sld|sv|tg|tmptg|ipt|tmpipt|dpd|tmpdpd)\s*$/i.test(name);
}

/**
 * 清理文本为合法命名片段
 */
function sanitizeText(text, maxLen) {
    if (!text) return "";
    maxLen = maxLen || 10;
    var s = text.replace(/[\r\n]+/g, '').replace(/^\s+|\s+$/g, '');
    s = s.replace(/[\\\/\:\*\?\"\<\>\|\.\,\;\!\@\#\$\%\^\&\(\)\[\]\{\}\s]+/g, '_');
    s = s.replace(/^_+|_+$/g, '').replace(/_+/g, '_');
    if (s.length > maxLen) s = s.substring(0, maxLen);
    return s.toLowerCase();
}

/**
 * 获取图层 bounds 信息
 */
function getLayerBounds(layer) {
    try {
        var b = layer.bounds;
        var l = b[0].as('px'), t = b[1].as('px');
        var r = b[2].as('px'), bt = b[3].as('px');
        return { left: l, top: t, right: r, bottom: bt, width: r - l, height: bt - t };
    } catch(e) {
        return null;
    }
}

/**
 * 简单字符串哈希 → 4位 base36 短码
 */
function shortHash(str) {
    var hash = 0;
    for (var i = 0; i < str.length; i++) {
        hash = ((hash << 5) - hash) + str.charCodeAt(i);
        hash = hash & hash;
    }
    var h = Math.abs(hash).toString(36);
    while (h.length < 4) h = "0" + h;
    return h.substring(0, 4);
}

// ╔══════════════════════════════════════════╗
// ║         智 能 命 名 引 擎                ║
// ╚══════════════════════════════════════════╝

/**
 * 分析组的子图层构成
 */
function analyzeGroupChildren(group) {
    var result = {
        hasImage: false,
        hasText: false,
        textContent: "",
        imageCount: 0,
        textCount: 0
    };
    for (var i = 0; i < group.layers.length; i++) {
        var child = group.layers[i];
        if (!child.visible) continue;
        try {
            if (child.typename === "ArtLayer") {
                try {
                    if (child.kind === LayerKind.TEXT) {
                        result.hasText = true;
                        result.textCount++;
                        if (!result.textContent) {
                            result.textContent = child.textItem.contents;
                        }
                    } else {
                        result.hasImage = true;
                        result.imageCount++;
                    }
                } catch(e2) {
                    result.hasImage = true;
                    result.imageCount++;
                }
            } else if (child.typename === "LayerSet") {
                result.hasImage = true;
                result.imageCount++;
            }
        } catch(e) {}
    }
    return result;
}

/**
 * 判断组是否符合滑块结构
 * 特征：一个长条（轨道）+ 一个/多个小块（滑块/填充）
 */
function isSliderPattern(group) {
    var children = [];
    for (var i = 0; i < group.layers.length; i++) {
        if (!group.layers[i].visible) continue;
        var b = getLayerBounds(group.layers[i]);
        if (b && b.width > 0 && b.height > 0) {
            children.push(b);
        }
    }
    if (children.length < 2) return false;

    // 找最大面积子图层作为轨道候选
    var maxArea = -1, trackIdx = -1;
    for (var i = 0; i < children.length; i++) {
        var area = children[i].width * children[i].height;
        if (area > maxArea) { maxArea = area; trackIdx = i; }
    }
    var track = children[trackIdx];
    var trackAspect = Math.max(track.width, track.height) / Math.max(1, Math.min(track.width, track.height));

    // 轨道需要足够长条
    if (trackAspect < (CONFIG.SLIDER_ASPECT_MIN || 3.0)) return false;

    // 其他子图层有至少一个比轨道面积小很多（handle / fill）
    for (var i = 0; i < children.length; i++) {
        if (i === trackIdx) continue;
        if (children[i].width * children[i].height < maxArea * 0.3) {
            return true;
        }
    }
    return false;
}

/**
 * 分类单个图层
 * @returns "txt" | "btn" | "slider" | "bg" | "icon" | "img" | "group"
 */
function classifyLayer(layer, canvasW, canvasH) {
    // 文本
    if (layer.typename === "ArtLayer") {
        try {
            if (layer.kind === LayerKind.TEXT) return "txt";
        } catch(e) {}
    }

    var bounds = getLayerBounds(layer);
    if (!bounds || bounds.width <= 0 || bounds.height <= 0) return "group";
    var w = bounds.width, h = bounds.height;

    // 组分析
    if (layer.typename === "LayerSet") {
        var analysis = analyzeGroupChildren(layer);

        // 按钮：有图且有文字，尺寸不太大
        if (analysis.hasImage && analysis.hasText &&
            w < canvasW * (CONFIG.BTN_MAX_WIDTH_RATIO || 0.5)) {
            return "btn";
        }

        // 滑块：长条 + 小块
        if (isSliderPattern(layer)) {
            return "slider";
        }

        return "group";
    }

    // 背景：面积 > 阈值
    var canvasArea = canvasW * canvasH;
    if (canvasArea > 0 && (w * h) / canvasArea > (CONFIG.BG_AREA_RATIO || 0.50)) {
        return "bg";
    }

    // 小图标
    if (Math.max(w, h) < (CONFIG.ICON_MAX_PX || 96)) {
        return "icon";
    }

    // 普通图片
    return "img";
}

/**
 * 构建视觉指纹（用于重复检测）
 * 相同指纹 = 相同分类 + 相同尺寸 → 大概率是复用素材
 */
function buildFingerprint(layer, classification, bounds) {
    // 文字/组/背景不参与去重
    if (!bounds || classification === "txt" || classification === "group" || classification === "bg") {
        return null;
    }
    if (isClippingMask(layer)) return null;
    var analysis = analyzeLayerProcessing(layer);
    if (analysis.hasEffect || analysis.hasMask || analysis.hasNonNormalBlend ||
        analysis.hasReducedOpacity || analysis.hasReducedFillOpacity) return null;
    return classification + "_" + Math.round(bounds.width) + "x" + Math.round(bounds.height);
}

/**
 * 从上下文推断有意义的命名片段
 */
function inferNameHint(layer, classification) {
    // 1. 本身名字有意义 → 直接用
    if (!isGenericName(layer.name) && !hasPluginTag(layer.name)) {
        var cleaned = sanitizeText(layer.name, 15);
        if (cleaned) return cleaned;
    }

    // 2. 文本图层 → 用文本内容
    if (classification === "txt") {
        try {
            var t = sanitizeText(layer.textItem.contents, 10);
            if (t) return t;
        } catch(e) {}
    }

    // 3. 按钮 → 用子文本内容
    if (classification === "btn" && layer.typename === "LayerSet") {
        for (var i = 0; i < layer.layers.length; i++) {
            try {
                if (layer.layers[i].kind === LayerKind.TEXT) {
                    var bt = sanitizeText(layer.layers[i].textItem.contents, 10);
                    if (bt) return bt;
                }
            } catch(e) {}
        }
    }

    // 4. 从父组名推断
    try {
        var parent = layer.parent;
        if (parent && parent.typename === "LayerSet" && !isGenericName(parent.name)) {
            var pt = sanitizeText(parent.name, 10);
            if (pt) return pt;
        }
    } catch(e) {}

    // 5. 用尺寸做标识
    var b = getLayerBounds(layer);
    if (b) return Math.round(b.width) + "x" + Math.round(b.height);
    return "unknown";
}

/**
 * 根据分类返回对应的插件标签后缀
 */
function getPluginTagForClass(classification) {
    switch (classification) {
        case "txt":    return ".txt";
        case "btn":    return ".bt";
        case "slider": return ".sld";
        case "bg":     return ".img";
        case "icon":   return ".img";
        case "img":    return ".img";
        default:       return "";
    }
}

/**
 * 递归收集图层信息
 */
function collectForRename(layers, infos, canvasW, canvasH) {
    for (var i = 0; i < layers.length; i++) {
        var layer = layers[i];
        if (CONFIG.SKIP_HIDDEN_LAYERS && !layer.visible) continue;

        var classification = classifyLayer(layer, canvasW, canvasH);
        var bounds = getLayerBounds(layer);
        var fingerprint = buildFingerprint(layer, classification, bounds);

        infos.push({
            layer: layer,
            classification: classification,
            bounds: bounds,
            fingerprint: fingerprint,
            newName: null
        });

        // 普通组递归子图层；btn/slider 组保持内部结构不动
        if (layer.typename === "LayerSet" && classification === "group") {
            collectForRename(layer.layers, infos, canvasW, canvasH);
        }
    }
}

/**
 * 主命名引擎
 * @returns {{ renamed: Number, duplicates: Number, stats: Object }}
 */
function smartRenameAllLayers(doc) {
    var canvasW = doc.width.as('px');
    var canvasH = doc.height.as('px');

    // Phase 1: 收集所有图层信息
    var infos = [];
    collectForRename(doc.layers, infos, canvasW, canvasH);

    // Phase 2: 按指纹分组找重复
    var fpGroups = {};
    for (var i = 0; i < infos.length; i++) {
        var fp = infos[i].fingerprint;
        if (!fp) continue;
        if (!fpGroups[fp]) fpGroups[fp] = [];
        fpGroups[fp].push(infos[i]);
    }

    // Phase 3: 对每个指纹组，选择有意义的名字作为 canonical
    // 优先选已有好名字的图层
    for (var fp in fpGroups) {
        var group = fpGroups[fp];
        if (group.length <= 1) continue;
        // 找第一个有好名字的成员
        var canonIdx = 0;
        for (var j = 0; j < group.length; j++) {
            if (!isGenericName(group[j].layer.name)) {
                canonIdx = j;
                break;
            }
        }
        // 把 canonical 移到首位
        if (canonIdx !== 0) {
            var tmp = group[0];
            group[0] = group[canonIdx];
            group[canonIdx] = tmp;
        }
    }

    // Phase 4: 生成命名
    var usedNames = {};
    var stats = { txt: 0, btn: 0, slider: 0, bg: 0, icon: 0, img: 0, ref: 0, kept: 0 };

    for (var i = 0; i < infos.length; i++) {
        var info = infos[i];
        if (info.classification === "group") continue;

        // 已有插件标签的不改
        if (hasPluginTag(info.layer.name)) {
            stats.kept++;
            continue;
        }

        // 检查去重：如果是指纹组中的非首个 → @ref:
        if (CONFIG.MARK_DUPLICATES_REF && info.fingerprint) {
            var group = fpGroups[info.fingerprint];
            if (group && group.length > 1 && group[0] !== info) {
                // 先确保 canonical（首个）已有新名字
                if (group[0].newName) {
                    info.newName = "@ref:" + group[0].newName;
                    stats.ref++;
                    continue;
                }
                // canonical 还没命名，等下一轮处理（首个会先被遍历到）
            }
        }

        // 只改通用名
        if (CONFIG.RENAME_ONLY_GENERIC && !isGenericName(info.layer.name)) {
            stats.kept++;
            // 但如果是指纹组的 canonical，记录其名字供 @ref: 使用
            if (info.fingerprint) {
                var grp = fpGroups[info.fingerprint];
                if (grp && grp[0] === info) {
                    // 用原名+标签作为 newName
                    var origName = info.layer.name;
                    if (!hasPluginTag(origName)) {
                        origName = origName + getPluginTagForClass(info.classification);
                    }
                    info.newName = origName;
                }
            }
            continue;
        }

        // 生成名字
        var hint = inferNameHint(info.layer, info.classification);
        var tag = getPluginTagForClass(info.classification);
        var baseName = hint + tag;

        // 确保唯一
        var finalName = baseName;
        var suffix = 1;
        while (usedNames[finalName]) {
            finalName = hint + "_" + suffix + tag;
            suffix++;
        }
        usedNames[finalName] = true;
        info.newName = finalName;
        stats[info.classification]++;
    }

    // Phase 5: 回填未处理的 @ref:（canonical 在后面被遍历到的情况）
    for (var fp in fpGroups) {
        var group = fpGroups[fp];
        if (group.length <= 1) continue;
        if (!group[0].newName) continue;
        for (var j = 1; j < group.length; j++) {
            if (!group[j].newName) {
                group[j].newName = "@ref:" + group[0].newName;
                stats.ref++;
            }
        }
    }

    // Phase 6: 应用
    var renamed = 0;
    for (var i = 0; i < infos.length; i++) {
        var info = infos[i];
        if (!info.newName) continue;
        if (info.newName === info.layer.name) continue;
        try {
            info.layer.name = info.newName;
            renamed++;
        } catch(e) {
            // 锁定图层等情况
        }
    }

    return { renamed: renamed, duplicates: stats.ref, stats: stats };
}

// ╔══════════════════════════════════════════╗
// ║         特 效 检 测                      ║
// ╚══════════════════════════════════════════╝

/**
 * 插件原生支持的特效 charID（不需要栅格化）
 * DrSh=投影, OrGl=外发光, FrFX=描边, SoFi=颜色叠加, ClrO=颜色叠加(别名)
 */
var PLUGIN_HANDLED_EFFECT_IDS = null; // 延迟初始化
function getPluginHandledEffectIDs() {
    if (PLUGIN_HANDLED_EFFECT_IDS === null) {
        PLUGIN_HANDLED_EFFECT_IDS = [
            app.charIDToTypeID('DrSh'),
            app.charIDToTypeID('OrGl'),
            app.charIDToTypeID('FrFX'),
            app.charIDToTypeID('SoFi'),
            app.charIDToTypeID('ClrO')
        ];
    }
    return PLUGIN_HANDLED_EFFECT_IDS;
}

/**
 * 全部10种特效的 ID
 */
var ALL_EFFECT_IDS = null;
function getAllEffectIDs() {
    if (ALL_EFFECT_IDS === null) {
        ALL_EFFECT_IDS = [
            app.charIDToTypeID('DrSh'),
            app.charIDToTypeID('IrSh'),
            app.charIDToTypeID('OrGl'),
            app.charIDToTypeID('IrGl'),
            app.charIDToTypeID('ebbl'),
            app.charIDToTypeID('ChFX'),
            app.charIDToTypeID('SoFi'),
            app.charIDToTypeID('FrFX'),
            app.charIDToTypeID('GrFl'),
            app.charIDToTypeID('ClrO'),
            app.stringIDToTypeID('patternFill')
        ];
    }
    return ALL_EFFECT_IDS;
}

function getBooleanIfPresent(targetDesc, keyID) {
    try {
        if (targetDesc.hasKey(keyID)) return targetDesc.getBoolean(keyID);
    } catch(e) {}
    return null;
}

function getFirstBoolean(targetDesc, keyIDs) {
    for (var idx = 0; idx < keyIDs.length; idx++) {
        var value = getBooleanIfPresent(targetDesc, keyIDs[idx]);
        if (value !== null) return value;
    }
    return null;
}

function isEffectEntryEnabled(effectDesc) {
    var enabled = getFirstBoolean(effectDesc, [
        app.charIDToTypeID('enab'),
        app.stringIDToTypeID('enabled')
    ]);
    return (enabled !== null) ? enabled : true;
}

function getBlendModeName(layer) {
    try {
        return String(layer.blendMode || "")
            .replace(/^BlendMode\./i, "")
            .toLowerCase();
    } catch(e) {
        return "";
    }
}

function isSafeIndependentBlend(layer, isGroup) {
    var mode = getBlendModeName(layer);
    if (!mode || mode === "normal") return true;
    return isGroup && (mode === "passthrough" || mode === "pass_through");
}

function getLayerDebugPath(layer) {
    var parts = [];
    var current = layer;
    try {
        while (current && current.typename !== "Document") {
            parts.unshift(String(current.name || "<unnamed>"));
            current = current.parent;
        }
    } catch(e) {}
    return parts.join("/");
}

/** 分析图片图层的效果、蒙版、透明度和背景依赖。 */
function analyzeLayerProcessing(layer) {
    var cacheKey = "";
    try { cacheKey = String(layer.id || ""); } catch(cacheIdError) {}
    if (cacheKey && LAYER_ANALYSIS_CACHE.hasOwnProperty(cacheKey)) {
        return LAYER_ANALYSIS_CACHE[cacheKey];
    }

    var isGroup = false;
    try { isGroup = layer.typename === "LayerSet"; } catch(e0) {}
    var result = {
        hasEffect: false,
        hasExoticEffect: false,
        hasMask: false,
        hasUserMask: false,
        hasVectorMask: false,
        hasNonNormalBlend: !isSafeIndependentBlend(layer, isGroup),
        blendMode: getBlendModeName(layer),
        opacity: 100,
        fillOpacity: 100,
        hasReducedOpacity: false,
        hasReducedFillOpacity: false
    };
    try {
        result.opacity = Number(layer.opacity);
        result.hasReducedOpacity = result.opacity < 99.999;
    } catch(eOpacity) {}
    if (!isGroup) {
        try {
            result.fillOpacity = Number(layer.fillOpacity);
            result.hasReducedFillOpacity = result.fillOpacity < 99.999;
        } catch(eFillOpacity) {}
    }
    try {
        var ref = new ActionReference();
        ref.putIdentifier(app.charIDToTypeID('Lyr '), layer.id);
        var desc = executeActionGet(ref);

        // ── 检查特效 ──
        var keyLefx = app.charIDToTypeID('Lefx');
        if (desc.hasKey(keyLefx)) {
            var keyFXVisible = app.stringIDToTypeID('layerFXVisible');
            var fxVisible = getBooleanIfPresent(desc, keyFXVisible);
            if (fxVisible !== false) {
                var effectsDesc = desc.getObjectValue(keyLefx);
                var rootEnabled = getFirstBoolean(effectsDesc, [
                    app.charIDToTypeID('enab'),
                    app.stringIDToTypeID('enabled')
                ]);
                if (rootEnabled !== false) {
                    var allIDs = getAllEffectIDs();
                    var handledIDs = getPluginHandledEffectIDs();

                    for (var i = 0; i < allIDs.length; i++) {
                        var eid = allIDs[i];
                        if (!effectsDesc.hasKey(eid)) continue;

                        var isEnabled = false;
                        try {
                            var vt = effectsDesc.getType(eid);
                            if (vt === DescValueType.OBJECTTYPE) {
                                isEnabled = isEffectEntryEnabled(effectsDesc.getObjectValue(eid));
                            } else if (vt === DescValueType.LISTTYPE) {
                                var lst = effectsDesc.getList(eid);
                                for (var li = 0; li < lst.count; li++) {
                                    try {
                                        if (lst.getType(li) === DescValueType.OBJECTTYPE &&
                                            isEffectEntryEnabled(lst.getObjectValue(li))) {
                                            isEnabled = true;
                                            break;
                                        }
                                    } catch(e3) {}
                                }
                            }
                        } catch(e2) {
                            try {
                                isEnabled = isEffectEntryEnabled(effectsDesc.getObjectValue(eid));
                            } catch(e4) {}
                        }

                        if (!isEnabled) continue;

                        result.hasEffect = true;
                        // 检查是否为插件不支持的"奇特"特效
                        var isHandled = false;
                        for (var h = 0; h < handledIDs.length; h++) {
                            if (eid === handledIDs[h]) { isHandled = true; break; }
                        }
                        if (!isHandled) {
                            result.hasExoticEffect = true;
                        }
                    }
                }
            }
        }

        // ── 检查蒙版 ──
        var hasUserMask = getBooleanIfPresent(desc, app.stringIDToTypeID('hasUserMask'));
        var userMaskEnabled = getFirstBoolean(desc, [
            app.stringIDToTypeID('userMaskEnabled'),
            app.charIDToTypeID('UsrM')
        ]);
        if ((hasUserMask === true || hasUserMask === null) && userMaskEnabled === true) {
            result.hasMask = true;
            result.hasUserMask = true;
        }

        var hasVectorMask = getBooleanIfPresent(desc, app.stringIDToTypeID('hasVectorMask'));
        var vectorMaskEnabled = getBooleanIfPresent(desc, app.stringIDToTypeID('vectorMaskEnabled'));
        if ((hasVectorMask === true || hasVectorMask === null) && vectorMaskEnabled === true) {
            result.hasMask = true;
            result.hasVectorMask = true;
        }

    } catch(e) {}
    if (cacheKey) LAYER_ANALYSIS_CACHE[cacheKey] = result;
    return result;
}

/**
 * 兼容旧接口：检查图层是否有需要处理的效果（特效或蒙版）
 * 组总是需要合并（即使只有插件支持的特效，组级合成太复杂）
 */
function hasLayerEffect(layer) {
    var a = analyzeLayerProcessing(layer);
    return a.hasEffect || a.hasMask;
}

// ╔══════════════════════════════════════════╗
// ║         图 层 处 理                      ║
// ╚══════════════════════════════════════════╝

function isClippingMask(layer) {
    try {
        return (layer.typename === "ArtLayer") && layer.grouped;
    } catch(e) {
        return false;
    }
}

function selectLayerForMerge(layer, addToSelection) {
    if (!layer) return false;
    var layerId = 0;
    try { layerId = layer.id || 0; } catch(e) {}
    if (!layerId) return false;

    var desc = new ActionDescriptor();
    var ref = new ActionReference();
    ref.putIdentifier(app.charIDToTypeID('Lyr '), layerId);
    desc.putReference(app.charIDToTypeID('null'), ref);
    if (addToSelection) {
        desc.putEnumerated(
            app.stringIDToTypeID('selectionModifier'),
            app.stringIDToTypeID('selectionModifierType'),
            app.stringIDToTypeID('addToSelection'));
    }
    desc.putBoolean(app.charIDToTypeID('MkVs'), false);
    executeAction(app.charIDToTypeID('slct'), desc, DialogModes.NO);
    return true;
}

function findLayerById(layers, layerId) {
    if (!layers || !layerId) return null;
    for (var i = 0; i < layers.length; i++) {
        var currentId = 0;
        try { currentId = layers[i].id || 0; } catch(e) {}
        if (currentId === layerId) return layers[i];
    }
    return null;
}

function mergeCompleteClippingChain(clippingLayers, baseLayer, layerOwner) {
    var result = { layer: null, layers: null, error: "" };
    try {
        if (!clippingLayers || !clippingLayers.length || !baseLayer || !layerOwner) {
            result.error = "invalid clipping chain";
            return result;
        }
        if (!selectLayerForMerge(baseLayer, false)) {
            result.error = "cannot select clipping base";
            return result;
        }
        for (var i = clippingLayers.length - 1; i >= 0; i--) {
            if (!selectLayerForMerge(clippingLayers[i], true)) {
                result.error = "cannot select clipping member";
                return result;
            }
        }
        // Merge Selected in Photoshop 2025 may discard the clipping base's
        // alpha coverage. Smart Object conversion renders the complete live
        // chain before rasterization and preserves the visual composite.
        executeAction(app.stringIDToTypeID('newPlacedLayer'), undefined, DialogModes.NO);
        var compositeLayer = app.activeDocument.activeLayer;
        if (!compositeLayer || compositeLayer.typename !== "ArtLayer") {
            result.error = "cannot create clipping composite smart object";
            return result;
        }
        compositeLayer.rasterize(RasterizeType.ENTIRELAYER);
        result.layers = layerOwner.layers;
        var activeId = 0;
        try { activeId = app.activeDocument.activeLayer.id || 0; } catch(activeIdError) {}
        result.layer = findLayerById(result.layers, activeId);
        if (!result.layer) result.error = "cannot resolve merged layer from refreshed owner";
        return result;
    } catch(e) {
        result.error = e.message || String(e);
        try { result.layers = layerOwner.layers; } catch(refreshError) {}
        return result;
    }
}

function hasClippingDependents(layer) {
    try {
        if (!layer || !layer.parent || !layer.parent.layers) return false;
        var siblings = layer.parent.layers;
        for (var i = 0; i < siblings.length; i++) {
            if (siblings[i] === layer) return i > 0 && isClippingMask(siblings[i - 1]);
        }
    } catch(e) {}
    return false;
}

// Resolve complete clipping chains before any vector-mask/effect base is
// replaced by bakeImageLayerVisual. The live Photoshop collection is ordered
// top-to-bottom, so scan from the bottom to find a base, expand upward to its
// complete contiguous clipping chain, then merge the entire chain once.
function mergeClippingChainsBeforeBake(layers, stats) {
    if (!layers) return true;

    var layerOwner = null;
    try {
        if (layers.length > 0) layerOwner = layers[0].parent;
    } catch(ownerError) {}

    function currentLayers() {
        try {
            if (layerOwner && layerOwner.layers) return layerOwner.layers;
        } catch(refreshError) {}
        return layers;
    }

    function recordClippingError(message) {
        if (!stats.clippingErrors) stats.clippingErrors = [];
        stats.clippingErrors.push(message);
        stats.errors.push(message);
    }

    while (true) {
        var siblings = currentLayers();
        var clippingLayer = null;
        var clippingIndex = -1;
        for (var i = siblings.length - 1; i >= 0; i--) {
            if (isClippingMask(siblings[i])) {
                clippingLayer = siblings[i];
                clippingIndex = i;
                break;
            }
        }

        if (!clippingLayer) return true;

        var baseLayer = clippingIndex + 1 < siblings.length ? siblings[clippingIndex + 1] : null;
        if (!baseLayer || baseLayer.typename !== "ArtLayer" || isClippingMask(baseLayer)) {
            recordClippingError("剪贴蒙版底板不是可安全一次性合并的 ArtLayer: " + clippingLayer.name);
            return false;
        }
        var chainTopIndex = clippingIndex;
        while (chainTopIndex > 0 && isClippingMask(siblings[chainTopIndex - 1])) {
            chainTopIndex--;
        }
        var clippingLayers = [];
        var clippingIds = [];
        var chainVisible = !!baseLayer.visible;
        for (var chainIndex = chainTopIndex; chainIndex <= clippingIndex; chainIndex++) {
            var chainLayer = siblings[chainIndex];
            clippingLayers.push(chainLayer);
            var chainLayerId = 0;
            try { chainLayerId = chainLayer.id || 0; } catch(chainIdError) {}
            clippingIds.push(chainLayerId);
            if (!chainLayer.visible) chainVisible = false;
        }

        if (!chainVisible) {
            recordClippingError("保留隐藏层时不能安全压平含隐藏成员的剪贴链: " + clippingLayer.name);
            return false;
        }

        var clippingName = String(clippingLayer.name || "");
        var baseName = String(baseLayer.name || "");
        var layerCountBeforeMerge = siblings.length;
        var mergeResult = mergeCompleteClippingChain(clippingLayers, baseLayer, layerOwner);
        var mergedLayer = mergeResult.layer;
        var refreshedSiblings = mergeResult.layers || currentLayers();
        var mergedLayerCount = layerCountBeforeMerge - refreshedSiblings.length;
        var expectedMergedLayerCount = clippingLayers.length;
        var mergedLayerId = 0;
        try { mergedLayerId = mergedLayer.id || 0; } catch(mergedIdError) {}
        var staleClippingId = 0;
        for (var clipIdIndex = 0; clipIdIndex < clippingIds.length; clipIdIndex++) {
            if (clippingIds[clipIdIndex] !== mergedLayerId &&
                findLayerById(refreshedSiblings, clippingIds[clipIdIndex])) {
                staleClippingId = clippingIds[clipIdIndex];
                break;
            }
        }
        if (!mergedLayer || mergedLayerCount !== expectedMergedLayerCount ||
            staleClippingId || mergedLayer.typename !== "ArtLayer" || isClippingMask(mergedLayer)) {
            recordClippingError("剪贴蒙版链预合并失败: " + clippingName +
                " (before=" + layerCountBeforeMerge + ", after=" + refreshedSiblings.length +
                ", expectedDrop=" + expectedMergedLayerCount +
                ", grouped=" + (!!mergedLayer && isClippingMask(mergedLayer)) +
                ", staleClipId=" + staleClippingId +
                ", actionError=" + (mergeResult.error || "") + ")");
            return false;
        }

        try { mergedLayer.name = baseName; } catch(renameMergedError) {}
        stats.clippingMaskMerged += expectedMergedLayerCount;
    }
}

function rasterizeLayerStyle(layer) {
    try {
        app.activeDocument.activeLayer = layer;
        var desc = new ActionDescriptor();
        var ref = new ActionReference();
        ref.putEnumerated(charIDToTypeID('Lyr '), charIDToTypeID('Ordn'), charIDToTypeID('Trgt'));
        desc.putReference(charIDToTypeID('null'), ref);
        desc.putEnumerated(stringIDToTypeID('what'), stringIDToTypeID('rasterizeItem'), stringIDToTypeID('layerStyle'));
        executeAction(stringIDToTypeID('rasterizeLayer'), desc, DialogModes.NO);
        return true;
    } catch(e) { return false; }
}

function getBakedPlacementDelta(renderedBounds, replacementBounds) {
    return {
        x: renderedBounds.left - replacementBounds.left,
        y: renderedBounds.top - replacementBounds.top
    };
}

function clipBoundsToCanvas(bounds, canvasWidth, canvasHeight) {
    if (!bounds) return null;
    var left = Math.max(0, bounds.left);
    var top = Math.max(0, bounds.top);
    var right = Math.min(canvasWidth, bounds.right);
    var bottom = Math.min(canvasHeight, bounds.bottom);
    if (right <= left || bottom <= top) return null;
    return {
        left: left,
        top: top,
        right: right,
        bottom: bottom,
        width: right - left,
        height: bottom - top
    };
}

var IMAGE_RENDER_DOC = null;
var IMAGE_RENDER_SOURCE = null;

function disposeImageRenderDocument() {
    var sourceDoc = IMAGE_RENDER_SOURCE;
    try { if (IMAGE_RENDER_DOC) IMAGE_RENDER_DOC.close(SaveOptions.DONOTSAVECHANGES); } catch(e) {}
    IMAGE_RENDER_DOC = null;
    IMAGE_RENDER_SOURCE = null;
    try { if (sourceDoc) app.activeDocument = sourceDoc; } catch(restoreError) {}
}

function getImageRenderDocument(sourceDoc) {
    if (IMAGE_RENDER_DOC && IMAGE_RENDER_SOURCE === sourceDoc) return IMAGE_RENDER_DOC;
    disposeImageRenderDocument();
    IMAGE_RENDER_SOURCE = sourceDoc;
    IMAGE_RENDER_DOC = app.documents.add(
        sourceDoc.width, sourceDoc.height, sourceDoc.resolution,
        "__PSD2UI_RENDER__", NewDocumentMode.RGB, DocumentFill.TRANSPARENT);
    try { IMAGE_RENDER_DOC.bitsPerChannel = sourceDoc.bitsPerChannel; } catch(bitDepthError) {}
    try {
        if (IMAGE_RENDER_DOC.colorProfileName !== sourceDoc.colorProfileName) {
            IMAGE_RENDER_DOC.convertProfile(sourceDoc.colorProfileName,
                Intent.RELATIVECOLORIMETRIC, true, false);
        }
    } catch(profileError) {}
    return IMAGE_RENDER_DOC;
}

function bakeImageLayerVisual(layer) {
    var sourceDoc = null;
    var renderDoc = null;
    var renderLayer = null;
    var replacement = null;
    var originalName = "";
    var renderedBounds = null;
    try {
        if (!layer || layer.typename !== "ArtLayer" || isClippingMask(layer) ||
            hasClippingDependents(layer)) return null;
        sourceDoc = app.activeDocument;
        originalName = String(layer.name || "");
        var parent = layer.parent;
        if (!parent || !parent.artLayers) return null;
        var originalBounds = getLayerBounds(layer);
        if (!originalBounds || originalBounds.width <= 0 || originalBounds.height <= 0) return null;

        renderDoc = getImageRenderDocument(sourceDoc);

        app.activeDocument = sourceDoc;
        renderLayer = layer.duplicate(renderDoc, ElementPlacement.PLACEATBEGINNING);
        app.activeDocument = renderDoc;
        renderDoc.activeLayer = renderLayer;
        renderedBounds = clipBoundsToCanvas(
            getLayerBounds(renderLayer),
            Math.round(renderDoc.width.as("px")),
            Math.round(renderDoc.height.as("px")));
        if (!renderedBounds || renderedBounds.width <= 0 || renderedBounds.height <= 0) {
            renderLayer.remove();
            renderLayer = null;
            return null;
        }

        renderDoc.selection.selectAll();
        renderDoc.selection.copy(true);
        renderDoc.selection.deselect();
        renderLayer.remove();
        renderLayer = null;

        app.activeDocument = sourceDoc;
        sourceDoc.activeLayer = layer;
        replacement = sourceDoc.paste();
        var replacementBounds = getLayerBounds(replacement);
        if (replacementBounds) {
            var placementDelta = getBakedPlacementDelta(renderedBounds, replacementBounds);
            replacement.translate(
                UnitValue(placementDelta.x, "px"),
                UnitValue(placementDelta.y, "px"));
        }
        replacement.move(layer, ElementPlacement.PLACEBEFORE);
        replacement.name = originalName;
        // Photoshop 2025 treats `grouped = false` as a toggle in this host;
        // never write it as an idempotent setter. Validate the result instead.
        if (isClippingMask(replacement)) {
            throw new Error("烘焙替换层意外继承剪贴状态: " + originalName);
        }
        try { replacement.opacity = 100; } catch(eOpacity) {}
        try { replacement.fillOpacity = 100; } catch(eFill) {}
        try { replacement.blendMode = BlendMode.NORMAL; } catch(eBlend) {}
        try { layer.allLocked = false; } catch(unlockError) {}
        layer.remove();
        if (isClippingMask(replacement)) {
            throw new Error("删除源层后烘焙替换层仍为剪贴状态: " + originalName);
        }
        return replacement;
    } catch(e) {
        try { if (renderLayer) renderLayer.remove(); } catch(renderCleanupError) {}
        try { if (replacement) replacement.remove(); } catch(cleanupError) {}
        return null;
    } finally {
        try { if (sourceDoc) app.activeDocument = sourceDoc; } catch(restoreError) {}
    }
}

function rasterizeGroup(group) {
    try {
        try {
            if (group.grouped) return null;
        } catch(groupedReadError) {}
        if (!selectLayerForMerge(group, false)) return null;
        executeAction(app.stringIDToTypeID('mergeLayersNew'), undefined, DialogModes.NO);
        var mergedLayer = app.activeDocument.activeLayer;
        return isClippingMask(mergedLayer) ? null : mergedLayer;
    } catch(e) {
        return null;
    }
}

function isComponentLayerName(name) {
    if (!name) return false;
    return /\.(bt|tmpbt|sld|tg|tmptg|ipt|tmpipt|dpd|tmpdpd|sv)\s*$/i.test(name);
}

function promoteVisualReferenceName(layer, currentName) {
    var baseName = String(currentName || "")
        .replace(/^@ref:/i, "")
        .replace(/\.(img|png)\s*$/i, "");
    var layerId = 0;
    try { layerId = layer.id || 0; } catch(e) {}
    return baseName + "_visual_" + layerId + ".img";
}

/**
 * 处理图层（递归）
 * v3.0: 对只有插件支持特效的普通图层不再栅格化
 */
function processLayers(layers, statusCallback) {
    var stats = {
        textConverted: 0,
        effectRasterized: 0,
        effectSkipped: 0,
        groupWithEffectMerged: 0,
        groupMerged: 0,
        clippingMaskMerged: 0,
        componentKept: 0,
        imageFullyBaked: 0,
        maskBaked: 0,
        refPromoted: 0,
        skipped: 0,
        errors: [],
        clippingErrors: [],
        riskWarnings: [],
        imageRenderMode: CONFIG.IMAGE_RENDER_MODE
    };
    var psVisualMode = CONFIG.IMAGE_RENDER_MODE === "ps_visual";
    var fatalClippingError = false;
    var fatalClippingPaths = [];

    function addRisk(layer, message) {
        stats.riskWarnings.push(getLayerDebugPath(layer) + ": " + message);
    }

    function processLayerRecursive(layers) {
        var recurseOwner = null;
        try {
            if (layers && layers.length > 0) recurseOwner = layers[0].parent;
        } catch(recurseOwnerError) {}
        if (!mergeClippingChainsBeforeBake(layers, stats)) {
            fatalClippingError = true;
            throw new Error("剪贴蒙版链合并失败，已停止且不会保存半处理 PSD");
        }
        try {
            if (recurseOwner && recurseOwner.layers) layers = recurseOwner.layers;
        } catch(recurseRefreshError) {}

        for (var i = layers.length - 1; i >= 0; i--) {
            var layer = layers[i];

            if (statusCallback) {
                var shortName = layer.name.length > 20 ? layer.name.substring(0, 20) + "..." : layer.name;
                statusCallback("处理中: " + shortName);
            }

            try {
                var layerName = layer.name;
                var layerType = layer.typename;

                if (CONFIG.SKIP_HIDDEN_LAYERS && !layer.visible) {
                    stats.skipped++;
                    continue;
                }

                // 带独立视觉状态的历史 @ref 必须升格，不能继续复用别的 Sprite。
                if (layerName.indexOf("@ref:") === 0) {
                    var refAnalysis = analyzeLayerProcessing(layer);
                    var refHasOwnVisual = refAnalysis.hasEffect || refAnalysis.hasMask ||
                        refAnalysis.hasNonNormalBlend || refAnalysis.hasReducedOpacity ||
                        refAnalysis.hasReducedFillOpacity;
                    if (psVisualMode && refHasOwnVisual) {
                        layerName = promoteVisualReferenceName(layer, layerName);
                        layer.name = layerName;
                        stats.refPromoted++;
                    } else {
                        stats.skipped++;
                        continue;
                    }
                }

                var isGroup = (layerType === "LayerSet");

                // ===== 处理图层组 =====
                if (isGroup) {
                    var groupAnalysis = analyzeLayerProcessing(layer);
                    if (isComponentLayerName(layerName)) {
                        stats.componentKept++;
                        if (psVisualMode && (groupAnalysis.hasEffect || groupAnalysis.hasMask ||
                            groupAnalysis.hasNonNormalBlend || groupAnalysis.hasReducedOpacity)) {
                            addRisk(layer, "组件组存在组级效果/蒙版/透明度/混合模式，已保留结构，请检查最终视觉");
                        }
                        processLayerRecursive(layer.layers);
                    } else if (layerName.toLowerCase().indexOf(".img") !== -1) {
                        if (rasterizeGroup(layer)) {
                            stats.groupMerged++;
                            continue;
                        } else {
                            stats.errors.push("组(.img)合并失败: " + layerName);
                            processLayerRecursive(layer.layers);
                        }
                    } else if (hasLayerEffect(layer) || (psVisualMode &&
                        (groupAnalysis.hasNonNormalBlend || groupAnalysis.hasReducedOpacity))) {
                        // 非组件组的视觉状态由 Photoshop 直接合并
                        if (rasterizeGroup(layer)) {
                            stats.groupWithEffectMerged++;
                            continue;
                        } else {
                            stats.errors.push("带特效组合并失败: " + layerName);
                            processLayerRecursive(layer.layers);
                        }
                    } else {
                        processLayerRecursive(layer.layers);
                    }
                    continue;
                }

                // ===== 剪贴蒙版（前置阶段完成后不应再出现） =====
                if (isClippingMask(layer)) {
                    fatalClippingError = true;
                    fatalClippingPaths.push(layerName);
                    throw new Error("剪贴蒙版前置合并后仍残留: " + layerName);
                }

                // ===== 文本图层 =====
                var isText = false;
                try { isText = (layer.kind === LayerKind.TEXT); } catch(e) {}
                if (isText) {
                    if (layerName.toLowerCase().indexOf(".img") !== -1) {
                        if (bakeImageLayerVisual(layer) || rasterizeLayerStyle(layer))
                            stats.textConverted++;
                        else {
                            stats.errors.push("文本转换失败: " + layerName);
                            stats.skipped++;
                        }
                    }
                    // 普通文本：保留（插件读 lfx2 特效数据）
                    continue;
                }

                // ===== 普通图片图层 =====
                var analysis = analyzeLayerProcessing(layer);

                if (analysis.hasNonNormalBlend) {
                    addRisk(layer, "背景依赖混合模式 " + (analysis.blendMode || "<unknown>") +
                        " 无法作为独立 Sprite 精确烘焙");
                    continue;
                }

                var mustBakeInPhotoshop = analysis.hasMask || analysis.hasExoticEffect;
                var shouldBakeInPhotoshop = psVisualMode && (analysis.hasEffect || analysis.hasMask ||
                    analysis.hasReducedOpacity || analysis.hasReducedFillOpacity);

                if (mustBakeInPhotoshop || shouldBakeInPhotoshop) {
                    if (bakeImageLayerVisual(layer)) {
                        stats.imageFullyBaked++;
                        if (analysis.hasMask) stats.maskBaked++;
                        if (analysis.hasEffect) stats.effectRasterized++;
                    } else {
                        stats.errors.push("图片完整烘焙失败: " + layerName);
                    }
                    continue;
                }

                if (analysis.hasEffect) {
                    stats.effectSkipped++;
                }

            } catch(e) {
                stats.errors.push("处理图层异常 [" + layer.name + "]: " + e.message);
                stats.skipped++;
            }
        }
    }

    disposeImageRenderDocument();
    try {
        processLayerRecursive(layers);
        if (fatalClippingError) {
            var clippingFailureDetails = fatalClippingPaths.length > 0
                ? fatalClippingPaths.join("；")
                : stats.clippingErrors.join("；");
            throw new Error("检测到未解析的剪贴蒙版: " + clippingFailureDetails +
                "。已停止且不会保存半处理 PSD");
        }
        if (stats.errors.length > 0) {
            throw new Error("图片处理失败: " + stats.errors.join("；") +
                "。已停止且不会保存失败处理 PSD");
        }
        return stats;
    } finally {
        disposeImageRenderDocument();
    }
}

// ╔══════════════════════════════════════════╗
// ║         导 出 + 统 计                    ║
// ╚══════════════════════════════════════════╝

function getFileSizeKB(file) {
    try { return file.exists ? (file.length / 1024).toFixed(2) : "未知"; }
    catch(e) { return "未知"; }
}

function exportPSD(doc) {
    var originalName = doc.name;
    var baseName = stripExportSuffix(originalName.replace(/\.psd$/i, ""));
    var defaultFileName = baseName + CONFIG.EXPORT_SUFFIX + ".psd";
    var defaultFolder;
    try { defaultFolder = doc.path; } catch(e) { defaultFolder = Folder.myDocuments; }

    var saveFile = new File(defaultFolder + "/" + defaultFileName);
    saveFile = saveFile.saveDlg("选择PSD导出文件");
    if (saveFile == null) return null;

    if (!/\.psd$/i.test(saveFile.name)) {
        saveFile = new File(saveFile.fsName + ".psd");
    }

    var saveOptions = new PhotoshopSaveOptions();
    saveOptions.embedColorProfile = true;
    saveOptions.alphaChannels = true;
    saveOptions.layers = true;
    saveOptions.maximizeCompatibility = true;
    doc.saveAs(saveFile, saveOptions, true, Extension.LOWERCASE);
    return saveFile;
}

function formatStatsV3(renameResult, processStats, duration, savedFile, mode) {
    var msg = "";

    if (mode === "rename") {
        msg += "✓ 智能命名完成！\n\n";
    } else if (mode === "both") {
        msg += "✓ 智能命名 + 导出完成！\n\n";
    } else {
        msg += "✓ PSD优化导出成功！\n\n";
    }

    // 命名统计
    if (renameResult) {
        msg += "📝 智能命名统计：\n";
        msg += "━━━━━━━━━━━━━━━━━━━━\n";
        var rs = renameResult.stats;
        if (rs.txt > 0) msg += "  txt_ 文本: " + rs.txt + "\n";
        if (rs.btn > 0) msg += "  .bt  按钮: " + rs.btn + "\n";
        if (rs.slider > 0) msg += "  .sld 滑块: " + rs.slider + "\n";
        if (rs.bg > 0) msg += "  bg_  背景: " + rs.bg + "\n";
        if (rs.icon > 0) msg += "  icon_图标: " + rs.icon + "\n";
        if (rs.img > 0) msg += "  img_ 图片: " + rs.img + "\n";
        if (rs.ref > 0) msg += "  @ref 复用: " + rs.ref + "\n";
        if (rs.kept > 0) msg += "  保留原名: " + rs.kept + "\n";
        msg += "  总改名: " + renameResult.renamed + "\n\n";
    }

    // 导出统计
    if (processStats) {
        msg += "📦 导出处理统计：\n";
        msg += "━━━━━━━━━━━━━━━━━━━━\n";
        msg += "  图片模式: " + (processStats.imageRenderMode === "ps_visual" ?
            "PS 视觉优先" : "Unity 可编辑") + "\n";
        if (processStats.textConverted > 0)
            msg += "  文本.img → 图片: " + processStats.textConverted + "\n";
        if (processStats.groupMerged > 0)
            msg += "  组(.img) → 合并: " + processStats.groupMerged + "\n";
        if (processStats.groupWithEffectMerged > 0)
            msg += "  组+特效  → 合并: " + processStats.groupWithEffectMerged + "\n";
        if (processStats.clippingMaskMerged > 0)
            msg += "  剪贴蒙版 → 合并: " + processStats.clippingMaskMerged + "\n";
        if (processStats.imageFullyBaked > 0)
            msg += "  图片 → PS 完整烘焙: " + processStats.imageFullyBaked + "\n";
        if (processStats.maskBaked > 0)
            msg += "  蒙版 → PS 完整烘焙: " + processStats.maskBaked + "\n";
        if (processStats.refPromoted > 0)
            msg += "  @ref → 独立图片: " + processStats.refPromoted + "\n";
        if (processStats.effectRasterized > 0)
            msg += "  图片特效 → PS 烘焙: " + processStats.effectRasterized + "\n";
        if (processStats.effectSkipped > 0)
            msg += "  图片特效 → Unity 兼容: " + processStats.effectSkipped + "\n";

        var total = processStats.textConverted + processStats.groupMerged +
                    processStats.groupWithEffectMerged + processStats.clippingMaskMerged +
                    processStats.imageFullyBaked + processStats.refPromoted;
        msg += "  总处理: " + total + " 个图层\n\n";

        if (processStats.errors.length > 0) {
            msg += "⚠️ 错误 (" + processStats.errors.length + "):\n";
            for (var i = 0; i < Math.min(processStats.errors.length, 8); i++) {
                msg += "  • " + processStats.errors[i] + "\n";
            }
            if (processStats.errors.length > 8) {
                msg += "  ... 还有 " + (processStats.errors.length - 8) + " 个\n";
            }
            msg += "\n";
        }
        if (processStats.riskWarnings.length > 0) {
            msg += "⚠️ 视觉风险 (" + processStats.riskWarnings.length + "):\n";
            for (var rw = 0; rw < Math.min(processStats.riskWarnings.length, 8); rw++) {
                msg += "  • " + processStats.riskWarnings[rw] + "\n";
            }
            msg += "\n";
        }
    }

    msg += "处理耗时: " + duration + " 秒\n";

    if (savedFile != null) {
        msg += "文件大小: " + getFileSizeKB(savedFile) + " KB\n";
        msg += "保存路径: " + savedFile.fsName + "\n\n";
        msg += "✓ 原始文档未被修改。";
    } else if (mode === "rename") {
        msg += "\n⚠️ 命名已应用到当前文档（可 Ctrl+Z 撤销）";
    }

    return msg;
}

// ╔══════════════════════════════════════════╗
// ║         Python 辅助（可选）              ║
// ╚══════════════════════════════════════════╝

/**
 * 将候选图层导出为缩略图和原始 bounds 元数据，供 Python 做去重/视觉特征分析
 * @returns {String} 缩略图文件夹路径，null = 失败
 */
function exportThumbnailsForPython(doc) {
    function _jsonEscape(value) {
        return String(value || '').replace(/\\/g, "\\\\").replace(/"/g, '\\"').replace(/\r/g, "\\r").replace(/\n/g, "\\n");
    }

    function _writeThumbMeta(file, data) {
        var lines = [];
        lines.push('{');
        lines.push('  "version": 1,');
        lines.push('  "layers": {');
        var first = true;
        for (var id in data.layers) {
            if (!data.layers.hasOwnProperty(id)) continue;
            var item = data.layers[id];
            if (!first) lines.push(',');
            first = false;
            lines.push('    "' + _jsonEscape(id) + '": {');
            lines.push('      "id": ' + item.id + ',');
            lines.push('      "name": "' + _jsonEscape(item.name) + '",');
            lines.push('      "width": ' + item.width + ',');
            lines.push('      "height": ' + item.height + ',');
            lines.push('      "bounds": [' + item.bounds.join(', ') + ']');
            lines.push('    }');
        }
        lines.push('');
        lines.push('  }');
        lines.push('}');
        file.encoding = "UTF-8";
        file.open("w");
        file.write(lines.join("\n"));
        file.close();
    }

    try {
        var tmpFolder = new Folder(Folder.temp + "/psd_dedup_thumbs");
        if (tmpFolder.exists) {
            // 清理旧文件
            var oldFiles = tmpFolder.getFiles("*.png");
            for (var i = 0; i < oldFiles.length; i++) oldFiles[i].remove();
        } else {
            tmpFolder.create();
        }

        var canvasW = doc.width.as('px'), canvasH = doc.height.as('px');
        var infos = [];
        collectForRename(doc.layers, infos, canvasW, canvasH);

        // 只导出参与去重的图层（有 fingerprint 的）
        var exported = 0;
        var meta = { version: 1, layers: {} };
        for (var i = 0; i < infos.length; i++) {
            if (!infos[i].fingerprint) continue;
            var layer = infos[i].layer;
            try {
                // 复制图层到新文档，缩小，导出 PNG
                var b = layer.bounds;
                var w = b[2].as('px') - b[0].as('px');
                var h = b[3].as('px') - b[1].as('px');
                if (w <= 0 || h <= 0) continue;
                meta.layers[String(layer.id)] = {
                    id: layer.id,
                    name: layer.name,
                    width: w,
                    height: h,
                    bounds: [b[0].as('px'), b[1].as('px'), w, h]
                };

                doc.activeLayer = layer;
                // 使用 layer ID 作为文件名
                var thumbFile = new File(tmpFolder + "/" + layer.id + ".png");

                // 复制图层 → 新文档 → 裁切 → 缩放 → 保存
                var tmpDoc = app.documents.add(w, h, 72, "thumb_" + layer.id, NewDocumentMode.RGB, DocumentFill.TRANSPARENT);
                app.activeDocument = doc;
                layer.duplicate(tmpDoc);
                app.activeDocument = tmpDoc;
                tmpDoc.flatten();
                // 保留宽高比例，最长边限制到 256，兼顾 pHash 性能与组件形态识别。
                var maxSide = Math.max(w, h);
                if (maxSide > 256) {
                    var scale = 256 / maxSide;
                    tmpDoc.resizeImage(UnitValue(Math.max(1, Math.round(w * scale)), 'px'), UnitValue(Math.max(1, Math.round(h * scale)), 'px'), 72, ResampleMethod.BICUBIC);
                }
                var pngOpts = new PNGSaveOptions();
                pngOpts.compression = 9;
                tmpDoc.saveAs(thumbFile, pngOpts, true, Extension.LOWERCASE);
                tmpDoc.close(SaveOptions.DONOTSAVECHANGES);
                app.activeDocument = doc;
                exported++;
            } catch(e) {
                // 跳过出错的图层
                try { app.activeDocument = doc; } catch(e2) {}
            }
        }

        _writeThumbMeta(new File(tmpFolder + "/thumb_meta.json"), meta);

        return exported > 0 ? tmpFolder.fsName : null;
    } catch(e) {
        return null;
    }
}

// ╔══════════════════════════════════════════╗
// ║         主 入 口                         ║
// ╚══════════════════════════════════════════╝

function showMainMenu() {
    var dlg = new Window("dialog", "PSD智能导出 v3.3", undefined);
    dlg.orientation = "column";
    dlg.alignChildren = ["fill", "top"];

    // 标题
    var titleGroup = dlg.add("group");
    titleGroup.alignment = ["center", "top"];
    titleGroup.add("statictext", undefined, "选择操作模式");

    // 选项
    var optGroup = dlg.add("group");
    optGroup.orientation = "column";
    optGroup.alignChildren = ["fill", "top"];
    optGroup.spacing = 4;

    var rbBoth = optGroup.add("radiobutton", undefined, "智能命名 + 导出 PSD（推荐）");
    var rbExport = optGroup.add("radiobutton", undefined, "只导出 PSD（不改名）");
    var rbRename = optGroup.add("radiobutton", undefined, "只智能命名（预览，可撤销）");
    rbBoth.value = true;

    // 设置区
    var settingsPanel = dlg.add("panel", undefined, "设置");
    settingsPanel.alignChildren = ["fill", "top"];
    settingsPanel.spacing = 2;

    var cbGenericOnly = settingsPanel.add("checkbox", undefined, "只重命名 PS 默认名（保留自定义名）");
    cbGenericOnly.value = CONFIG.RENAME_ONLY_GENERIC;

    var cbDupRef = settingsPanel.add("checkbox", undefined, "相同图片标记 @ref:（插件自动复用）");
    cbDupRef.value = CONFIG.MARK_DUPLICATES_REF;

    var imageModePanel = settingsPanel.add("panel", undefined, "图片导出模式");
    imageModePanel.orientation = "row";
    var rbPsVisual = imageModePanel.add("radiobutton", undefined, "PS 视觉优先");
    var rbUnityEditable = imageModePanel.add("radiobutton", undefined, "Unity 可编辑");
    rbPsVisual.value = CONFIG.IMAGE_RENDER_MODE !== "unity_editable";
    rbUnityEditable.value = !rbPsVisual.value;

    var cbSkipHidden = settingsPanel.add("checkbox", undefined, "跳过隐藏图层");
    cbSkipHidden.value = CONFIG.SKIP_HIDDEN_LAYERS;

    // 按钮
    var btnGroup = dlg.add("group");
    btnGroup.alignment = ["center", "top"];
    var btnOK = btnGroup.add("button", undefined, "开始", { name: "ok" });
    var btnCancel = btnGroup.add("button", undefined, "取消", { name: "cancel" });

    btnOK.onClick = function() {
        // 把UI值写回CONFIG
        CONFIG.RENAME_ONLY_GENERIC = cbGenericOnly.value;
        CONFIG.MARK_DUPLICATES_REF = cbDupRef.value;
        CONFIG.IMAGE_RENDER_MODE = rbUnityEditable.value ? "unity_editable" : "ps_visual";
        CONFIG.SKIP_HIDDEN_LAYERS = cbSkipHidden.value;

        var mode = "both";
        if (rbExport.value) mode = "export";
        if (rbRename.value) mode = "rename";

        dlg.close(1);
        executeFlow(mode);
    };

    dlg.show();
}

function executeFlow(mode) {
    var originalDoc = app.activeDocument;
    var tempDoc = null;
    var startTime = new Date();
    LAYER_ANALYSIS_CACHE = {};

    // 进度窗口
    var progressWin = new Window("palette", "处理中...", undefined);
    var statusText = progressWin.add("statictext", [0, 0, 450, 20], "正在初始化...");
    statusText.justify = "left";
    progressWin.center();

    function updateStatus(msg) {
        statusText.text = msg;
        progressWin.update();
    }

    try {
        app.displayDialogs = DialogModes.NO;
        progressWin.show();

        if (mode === "rename") {
            // ── 只命名：直接在原文档操作（可撤销）──
            updateStatus("正在分析图层结构...");
            var renameResult = smartRenameAllLayers(originalDoc);

            progressWin.close();
            var duration = ((new Date() - startTime) / 1000).toFixed(2);
            app.displayDialogs = DialogModes.ALL;
            alert(formatStatsV3(renameResult, null, duration, null, "rename"));

        } else if (mode === "export") {
            // ── 只导出：复制 → 处理 → 导出 → 关闭 ──
            updateStatus("正在创建副本...");
            tempDoc = originalDoc.duplicate(originalDoc.name, false);
            app.activeDocument = tempDoc;

            updateStatus("正在统一宋体字体...");
            var fontStats = normalizeSongtiTextFonts(tempDoc.layers);
            var processStats = processLayers(tempDoc.layers, updateStatus);
            processStats.songtiFontsNormalized = fontStats.changed;

            updateStatus("正在保存文件...");
            var savedFile = exportPSD(tempDoc);

            updateStatus("正在清理...");
            tempDoc.close(SaveOptions.DONOTSAVECHANGES);
            tempDoc = null;
            app.activeDocument = originalDoc;

            progressWin.close();
            var duration = ((new Date() - startTime) / 1000).toFixed(2);
            app.displayDialogs = DialogModes.ALL;

            if (savedFile) {
                alert(formatStatsV3(null, processStats, duration, savedFile, "export"));
            } else {
                alert("导出已取消。");
            }

        } else {
            // ── 智能命名 + 导出 ──
            updateStatus("正在创建副本...");
            tempDoc = originalDoc.duplicate(originalDoc.name, false);
            app.activeDocument = tempDoc;

            updateStatus("正在智能命名...");
            var renameResult = smartRenameAllLayers(tempDoc);

            updateStatus("正在统一宋体字体...");
            var fontStats = normalizeSongtiTextFonts(tempDoc.layers);
            var processStats = processLayers(tempDoc.layers, updateStatus);
            processStats.songtiFontsNormalized = fontStats.changed;

            updateStatus("正在保存文件...");
            var savedFile = exportPSD(tempDoc);

            updateStatus("正在清理...");
            tempDoc.close(SaveOptions.DONOTSAVECHANGES);
            tempDoc = null;
            app.activeDocument = originalDoc;

            progressWin.close();
            var duration = ((new Date() - startTime) / 1000).toFixed(2);
            app.displayDialogs = DialogModes.ALL;

            if (savedFile) {
                alert(formatStatsV3(renameResult, processStats, duration, savedFile, "both"));
            } else {
                alert("导出已取消。");
            }
        }

    } catch(e) {
        if (tempDoc != null) {
            try { tempDoc.close(SaveOptions.DONOTSAVECHANGES); } catch(ce) {}
        }
        if (progressWin) progressWin.close();
        app.activeDocument = originalDoc;
        app.displayDialogs = DialogModes.ALL;
        alert("❌ 处理过程中发生错误:\n\n" + e.message + "\n行号: " + e.line);
    }
}
