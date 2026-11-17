// PSD2UGUI 图层批量命名工具
// 根据 MatchRPG 项目命名规则，自动为选中图层生成标准命名
// 格式: [模块名]_[资源类型]_[描述]_[序号]
// Author: PSD2UIForm Tools
// Version: 1.0
// Last Update: 2026-03-23

#target photoshop

// ==================== UGUI 标签配置（与 LayerTagMenu 保持同步） ====================
var UGUI_TAGS = {
    "img":        ".img",
    "rimg":       ".rimg",
    "txt":        ".txt",
    "tmptxt":     ".tmptxt",
    "msk":        ".msk",
    "col":        ".col",
    "bg":         ".bg",
    "bt":         ".bt",
    "tmpbt":      ".tmpbt",
    "onover":     ".onover",
    "press":      ".press",
    "select":     ".select",
    "disable":    ".disable",
    "bttxt":      ".bttxt",
    "dpd":        ".dpd",
    "tmpdpd":     ".tmpdpd",
    "dpdlb":      ".dpdlb",
    "dpdicon":    ".dpdicon",
    "ipt":        ".ipt",
    "tmpipt":     ".tmpipt",
    "placeholder":".placeholder",
    "ipttxt":     ".ipttxt",
    "tg":         ".tg",
    "tmptg":      ".tmptg",
    "mark":       ".mark",
    "tglb":       ".tglb",
    "sld":        ".sld",
    "fill":       ".fill",
    "handle":     ".handle",
    "sv":         ".sv",
    "vpt":        ".vpt",
    "hbarbg":     ".hbarbg",
    "hbar":       ".hbar",
    "vbarbg":     ".vbarbg",
    "vbar":       ".vbar"
};

// ==================== 项目模块配置 ====================
var MODULES = [
    { name: "common",       label: "Common\n通用" },
    { name: "home",         label: "Home\n主页" },
    { name: "userinfo",     label: "UserInfo\n用户信息" },
    { name: "battle",       label: "Battle\n战斗" },
    { name: "setting",      label: "Setting\n设置" },
    { name: "confirm",      label: "Confirm\n确认框" },
    { name: "hangup",       label: "Hangup\n挂机" },
    { name: "chapterlevel", label: "ChapterLevel\n章节关卡" },
    { name: "levelmap",     label: "LevelMap\n关卡地图" },
    { name: "reward",       label: "Reward\n奖励" },
    { name: "shop",         label: "Shop\n商店" },
    { name: "teach",        label: "Teach\n教学" },
    { name: "tuning",       label: "Tuning\n调教" },
    { name: "worldmap",     label: "WorldMap\n世界地图" },
    { name: "loading",      label: "Loading\n加载" }
];

// ==================== 资源类型配置 ====================
var ASSET_TYPES = [
    { prefix: "btn",      label: "Button\n按钮",      uguiTag: "bt" },
    { prefix: "icon",     label: "Icon\n图标",         uguiTag: "img" },
    { prefix: "img",      label: "Image\n图片/背景",   uguiTag: "img" },
    { prefix: "edit",     label: "Decoration\n装饰",   uguiTag: "img" },
    { prefix: "itembg",   label: "ItemBG\n物品背景",   uguiTag: "img" },
    { prefix: "progress", label: "Progress\n进度条",   uguiTag: "sld" }
];

(function() {
    // ==================== 前置检查 ====================
    if (!app.documents.length) {
        alert("❌ 请先打开一个 PSD 文档");
        return;
    }
    var doc = app.activeDocument;
    if (!doc.activeLayer) {
        alert("❌ 请先选择一个图层");
        return;
    }

    var savedSelection = saveSelection();
    var layerInfos = getSelectedLayerInfosFast();
    if (layerInfos.length === 0) {
        alert("❌ 请先选择一个或多个图层");
        return;
    }

    // ==================== 尝试从 PSD 文件名自动识别模块 ====================
    var docName = doc.name.replace(/\.[^.]+$/, "").toLowerCase();
    var autoModuleIdx = 0;
    for (var mi = 0; mi < MODULES.length; mi++) {
        if (docName.indexOf(MODULES[mi].name) >= 0) {
            autoModuleIdx = mi;
            break;
        }
    }

    // ==================== 创建对话框 ====================
    var w = new Window("dialog", "PSD2UGUI - 批量命名工具");
    w.alignChildren = "fill";
    w.spacing = 10;
    w.margins = 16;

    // ── 选中图层信息 ──
    var infoPanel = w.add("panel", undefined, "选中的图层 (" + layerInfos.length + " 个)");
    infoPanel.alignChildren = "fill";
    infoPanel.margins = 8;
    var displayCount = Math.min(layerInfos.length, 6);
    for (var i = 0; i < displayCount; i++) {
        var itemText = infoPanel.add("statictext", undefined, "  • " + layerInfos[i].name);
        itemText.graphics.font = ScriptUI.newFont(itemText.graphics.font.name, ScriptUI.FontStyle.REGULAR, 10);
    }
    if (layerInfos.length > 6) {
        var moreText = infoPanel.add("statictext", undefined, "  ... 还有 " + (layerInfos.length - 6) + " 个");
        moreText.graphics.font = ScriptUI.newFont(moreText.graphics.font.name, ScriptUI.FontStyle.ITALIC, 10);
    }

    // ── 模块选择 ──
    var modulePanel = w.add("panel", undefined, "① 选择模块 (Module)");
    modulePanel.alignChildren = "fill";
    modulePanel.margins = 10;
    var moduleLabels = [];
    for (var mi = 0; mi < MODULES.length; mi++) {
        moduleLabels.push(MODULES[mi].name + " - " + MODULES[mi].label.split("\n")[1]);
    }
    var moduleRow = modulePanel.add("group");
    moduleRow.add("statictext", undefined, "模块:");
    var moduleDrop = moduleRow.add("dropdownlist", undefined, moduleLabels);
    moduleDrop.selection = autoModuleIdx;
    moduleDrop.preferredSize.width = 240;

    // ── 资源类型选择 ──
    var typePanel = w.add("panel", undefined, "② 选择资源类型 (Type)");
    typePanel.alignChildren = "fill";
    typePanel.margins = 10;
    var typeLabels = [];
    for (var ti = 0; ti < ASSET_TYPES.length; ti++) {
        typeLabels.push(ASSET_TYPES[ti].prefix + " - " + ASSET_TYPES[ti].label.split("\n")[1]);
    }
    var typeRow = typePanel.add("group");
    typeRow.add("statictext", undefined, "类型:");
    var typeDrop = typeRow.add("dropdownlist", undefined, typeLabels);
    typeDrop.selection = 0;
    typeDrop.preferredSize.width = 240;

    // ── 描述 + 序号 ──
    var descPanel = w.add("panel", undefined, "③ 描述与编号");
    descPanel.alignChildren = "fill";
    descPanel.margins = 10;

    var descRow = descPanel.add("group");
    descRow.add("statictext", undefined, "描述 (可选):");
    var descInput = descRow.add("edittext", undefined, "");
    descInput.preferredSize.width = 200;
    descInput.helpTip = "英文小写，例如: close, avatar, game\n留空则只用序号";

    var seqRow = descPanel.add("group");
    seqRow.add("statictext", undefined, "起始序号:");
    var seqInput = seqRow.add("edittext", undefined, "1");
    seqInput.preferredSize.width = 60;
    seqInput.helpTip = "多选时自动递增";

    var seqModeRow = descPanel.add("group");
    var useSeqCheckbox = seqModeRow.add("checkbox", undefined, "使用序号（多图层自动递增）");
    useSeqCheckbox.value = (layerInfos.length > 1);
    var keepOrigNameCheckbox = seqModeRow.add("checkbox", undefined, "保留原名作为描述");
    keepOrigNameCheckbox.value = false;

    // ── UGUI 标签附加 ──
    var tagPanel = w.add("panel", undefined, "④ 附加 UGUI 标签（可选）");
    tagPanel.alignChildren = "fill";
    tagPanel.margins = 10;

    var autoTagCheckbox = tagPanel.add("checkbox", undefined, "根据资源类型自动添加 UGUI 标签");
    autoTagCheckbox.value = true;
    autoTagCheckbox.helpTip = "例如: btn 类型自动添加 .bt 标签";

    var extraTagRow = tagPanel.add("group");
    extraTagRow.add("statictext", undefined, "额外标签:");
    var extraTagLabels = ["无"];
    var extraTagValues = [""];
    for (var tk in UGUI_TAGS) {
        if (UGUI_TAGS.hasOwnProperty(tk)) {
            extraTagLabels.push(UGUI_TAGS[tk] + " (" + tk + ")");
            extraTagValues.push(UGUI_TAGS[tk]);
        }
    }
    var extraTagDrop = extraTagRow.add("dropdownlist", undefined, extraTagLabels);
    extraTagDrop.selection = 0;
    extraTagDrop.preferredSize.width = 200;

    // ── 预览区 ──
    var previewPanel = w.add("panel", undefined, "预览");
    previewPanel.alignChildren = "fill";
    previewPanel.margins = 10;
    var previewText = previewPanel.add("statictext", undefined, "", { multiline: true });
    previewText.preferredSize.height = 60;
    previewText.graphics.font = ScriptUI.newFont(previewText.graphics.font.name, ScriptUI.FontStyle.BOLD, 11);

    // ── 预览更新函数 ──
    function updatePreview() {
        var names = generateNames(3);
        var previewStr = "";
        for (var pi = 0; pi < names.length; pi++) {
            previewStr += (pi > 0 ? "\n" : "") + names[pi].newName;
        }
        if (layerInfos.length > 3) {
            previewStr += "\n...";
        }
        previewText.text = previewStr;
    }

    function generateNames(maxCount) {
        var results = [];
        var moduleIdx = moduleDrop.selection ? moduleDrop.selection.index : 0;
        var typeIdx = typeDrop.selection ? typeDrop.selection.index : 0;
        var moduleName = MODULES[moduleIdx].name;
        var typePrefix = ASSET_TYPES[typeIdx].prefix;
        var desc = descInput.text.replace(/[^a-zA-Z0-9_]/g, "").toLowerCase();
        var startSeq = parseInt(seqInput.text, 10) || 1;
        var useSeq = useSeqCheckbox.value;
        var keepOrig = keepOrigNameCheckbox.value;
        var autoTag = autoTagCheckbox.value;
        var extraTagIdx = extraTagDrop.selection ? extraTagDrop.selection.index : 0;
        var autoUguiTag = autoTag ? (UGUI_TAGS[ASSET_TYPES[typeIdx].uguiTag] || "") : "";
        var extraUguiTag = extraTagValues[extraTagIdx] || "";

        var count = maxCount ? Math.min(layerInfos.length, maxCount) : layerInfos.length;
        for (var ni = 0; ni < count; ni++) {
            var info = layerInfos[ni];
            var parts = [moduleName, typePrefix];

            if (keepOrig) {
                // 使用原图层名作为描述（去掉已有标签和非法字符）
                var cleanOrigName = stripUguiTags(info.name);
                cleanOrigName = cleanOrigName.replace(/[^a-zA-Z0-9_]/g, "").toLowerCase();
                if (cleanOrigName.length > 0) {
                    parts.push(cleanOrigName);
                }
            } else if (desc.length > 0) {
                parts.push(desc);
            }

            if (useSeq) {
                parts.push(String(startSeq + ni));
            }

            var newName = parts.join("_");

            // 附加 UGUI 标签
            if (autoUguiTag.length > 0) {
                newName += autoUguiTag;
            }
            if (extraUguiTag.length > 0 && extraUguiTag !== autoUguiTag) {
                newName += extraUguiTag;
            }

            results.push({ id: info.id, name: info.name, newName: newName });
        }
        return results;
    }

    // 绑定预览更新
    moduleDrop.onChange = updatePreview;
    typeDrop.onChange = updatePreview;
    descInput.onChanging = updatePreview;
    seqInput.onChanging = updatePreview;
    useSeqCheckbox.onClick = updatePreview;
    keepOrigNameCheckbox.onClick = updatePreview;
    autoTagCheckbox.onClick = updatePreview;
    extraTagDrop.onChange = updatePreview;

    // 初始预览
    updatePreview();

    // ── 底部按钮 ──
    w.add("panel", undefined, undefined, { borderStyle: "black" });

    var bottomGroup = w.add("group");
    bottomGroup.alignment = "center";
    bottomGroup.spacing = 12;

    var applyBtn = bottomGroup.add("button", undefined, "✅ 应用命名");
    applyBtn.preferredSize = [120, 32];
    applyBtn.onClick = function() {
        var allNames = generateNames(0);
        batchRenameByInfo(allNames);
        restoreSelection(savedSelection);
        w.close();
        alert("✅ 已完成 " + allNames.length + " 个图层的批量命名");
    };

    var cancelBtn = bottomGroup.add("button", undefined, "取消 (ESC)", { name: "cancel" });
    cancelBtn.preferredSize = [100, 32];

    var tipText = w.add("statictext", undefined,
        "💡 格式: [模块名]_[资源类型]_[描述]_[序号] + UGUI标签");
    tipText.graphics.font = ScriptUI.newFont(tipText.graphics.font.name, ScriptUI.FontStyle.ITALIC, 10);

    // ==================== 工具函数 ====================

    function stripUguiTags(layerName) {
        var name = layerName;
        var found = true;
        while (found) {
            found = false;
            for (var k in UGUI_TAGS) {
                if (UGUI_TAGS.hasOwnProperty(k)) {
                    var suffix = UGUI_TAGS[k];
                    if (name.length >= suffix.length &&
                        name.substring(name.length - suffix.length) === suffix) {
                        name = name.substring(0, name.length - suffix.length);
                        found = true;
                        break;
                    }
                }
            }
        }
        return name;
    }

    function batchRenameByInfo(nameList) {
        for (var i = 0; i < nameList.length; i++) {
            try {
                setLayerNameById(nameList[i].id, nameList[i].newName);
            } catch (e) {}
        }
    }

    // ===== AM 操作函数（共享自 LayerTagMenu） =====

    function saveSelection() {
        var selection = { layerIDs: [] };
        try {
            var ref = new ActionReference();
            ref.putProperty(charIDToTypeID('Prpr'), stringIDToTypeID('targetLayersIDs'));
            ref.putEnumerated(charIDToTypeID('Dcmn'), charIDToTypeID('Ordn'), charIDToTypeID('Trgt'));
            var desc = executeActionGet(ref);
            if (desc.hasKey(stringIDToTypeID('targetLayersIDs'))) {
                var list = desc.getList(stringIDToTypeID('targetLayersIDs'));
                for (var i = 0; i < list.count; i++) {
                    selection.layerIDs.push(list.getReference(i).getIdentifier());
                }
            }
        } catch (e) {}
        return selection;
    }

    function restoreSelection(selection) {
        if (!selection || selection.layerIDs.length === 0) return;
        try {
            var desc = new ActionDescriptor();
            var ref = new ActionReference();
            ref.putIdentifier(charIDToTypeID('Lyr '), selection.layerIDs[0]);
            desc.putReference(charIDToTypeID('null'), ref);
            desc.putBoolean(charIDToTypeID('MkVs'), false);
            executeAction(charIDToTypeID('slct'), desc, DialogModes.NO);
            for (var i = 1; i < selection.layerIDs.length; i++) {
                var desc2 = new ActionDescriptor();
                var ref2 = new ActionReference();
                ref2.putIdentifier(charIDToTypeID('Lyr '), selection.layerIDs[i]);
                desc2.putReference(charIDToTypeID('null'), ref2);
                desc2.putEnumerated(stringIDToTypeID('selectionModifier'),
                    stringIDToTypeID('selectionModifierType'),
                    stringIDToTypeID('addToSelection'));
                desc2.putBoolean(charIDToTypeID('MkVs'), false);
                executeAction(charIDToTypeID('slct'), desc2, DialogModes.NO);
            }
        } catch (e) {}
    }

    function getSelectedLayerInfosFast() {
        var infos = [];
        try {
            var ref = new ActionReference();
            ref.putProperty(charIDToTypeID('Prpr'), stringIDToTypeID('targetLayersIDs'));
            ref.putEnumerated(charIDToTypeID('Dcmn'), charIDToTypeID('Ordn'), charIDToTypeID('Trgt'));
            var desc = executeActionGet(ref);
            if (desc.hasKey(stringIDToTypeID('targetLayersIDs'))) {
                var list = desc.getList(stringIDToTypeID('targetLayersIDs'));
                for (var i = 0; i < list.count; i++) {
                    var layerID = list.getReference(i).getIdentifier();
                    var ref2 = new ActionReference();
                    ref2.putIdentifier(charIDToTypeID('Lyr '), layerID);
                    var layerDesc = executeActionGet(ref2);
                    infos.push({
                        id: layerID,
                        name: layerDesc.getString(charIDToTypeID('Nm  '))
                    });
                }
                return infos;
            }
        } catch (e) {}
        if (app.activeDocument.activeLayer) {
            try {
                var ref3 = new ActionReference();
                ref3.putEnumerated(charIDToTypeID('Lyr '), charIDToTypeID('Ordn'), charIDToTypeID('Trgt'));
                var desc3 = executeActionGet(ref3);
                infos.push({
                    id: desc3.getInteger(stringIDToTypeID('layerID')),
                    name: app.activeDocument.activeLayer.name
                });
            } catch (e) {}
        }
        return infos;
    }

    function setLayerNameById(layerId, newName) {
        if (layerId === undefined || layerId === null) return;
        // AM 直接改名（无需选中）
        try {
            var desc = new ActionDescriptor();
            var ref = new ActionReference();
            ref.putIdentifier(charIDToTypeID('Lyr '), layerId);
            desc.putReference(charIDToTypeID('null'), ref);
            var nameDesc = new ActionDescriptor();
            nameDesc.putString(charIDToTypeID('Nm  '), newName);
            desc.putObject(charIDToTypeID('T   '), charIDToTypeID('Lyr '), nameDesc);
            executeAction(charIDToTypeID('setd'), desc, DialogModes.NO);
            return;
        } catch (e) {}

        // 回退：选中后 DOM 改名
        try {
            var selDesc = new ActionDescriptor();
            var selRef = new ActionReference();
            selRef.putIdentifier(charIDToTypeID('Lyr '), layerId);
            selDesc.putReference(charIDToTypeID('null'), selRef);
            selDesc.putBoolean(charIDToTypeID('MkVs'), false);
            executeAction(charIDToTypeID('slct'), selDesc, DialogModes.NO);
            var layer = app.activeDocument.activeLayer;
            if (!layer) return;

            // 解锁
            var wasBackground = false;
            try { wasBackground = layer.isBackgroundLayer; if (wasBackground) layer.isBackgroundLayer = false; } catch (e) {}
            var wasLocked = false;
            try { wasLocked = layer.allLocked; if (wasLocked) layer.allLocked = false; } catch (e) {}

            layer.name = newName;

            // 恢复锁定
            try { if (wasLocked) layer.allLocked = true; } catch (e) {}
            try { if (wasBackground) layer.isBackgroundLayer = true; } catch (e) {}
        } catch (e) {}
    }

    // 显示窗口
    w.center();
    w.show();
})();
