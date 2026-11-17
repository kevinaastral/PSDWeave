// ============================================
// PSD2UGUI Smart Export - Host ExtendScript
// CEP Panel Backend for Photoshop
// Version: 4.4.5 - Photoshop 2025 clipping-chain fixes and naming fast path
// ============================================
#target photoshop
// Pinyin fallback is loaded lazily at runtime. Hard #include makes the whole
// CEP script fail if an installed package misses vendor files or resolves
// relative include paths differently.

// ╔══════════════════════════════════════════╗
// ║         Mini JSON (ExtendScript)         ║
// ╚══════════════════════════════════════════╝
var _JSON = {
    stringify: function (obj) {
        if (obj === null || obj === undefined) return 'null';
        var t = typeof obj;
        if (t === 'number') return isFinite(obj) ? String(obj) : '0';
        if (t === 'boolean') return String(obj);
        if (t === 'string') {
            return '"' + obj.replace(/\\/g, '\\\\')
                            .replace(/"/g, '\\"')
                            .replace(/\n/g, '\\n')
                            .replace(/\r/g, '\\r')
                            .replace(/\t/g, '\\t') + '"';
        }
        if (obj instanceof Array) {
            var a = [];
            for (var i = 0; i < obj.length; i++) a.push(_JSON.stringify(obj[i]));
            return '[' + a.join(',') + ']';
        }
        if (t === 'object') {
            var p = [];
            for (var k in obj) {
                if (obj.hasOwnProperty(k)) {
                    p.push(_JSON.stringify(String(k)) + ':' + _JSON.stringify(obj[k]));
                }
            }
            return '{' + p.join(',') + '}';
        }
        return 'null';
    },
    parse: function (str) {
        // Safe subset eval - only used for trusted panel-host communication
        return eval('(' + str + ')');
    }
};

// ╔══════════════════════════════════════════╗
// ║         配 置 默 认 值                    ║
// ╚══════════════════════════════════════════╝
var _CONFIG = {
    SKIP_HIDDEN_LAYERS:     true,
    EXPORT_SUFFIX:          "",
    RENAME_ONLY_GENERIC:    true,
    MARK_DUPLICATES_REF:    true,
    BG_AREA_RATIO:          0.50,
    ICON_MAX_PX:            96,
    BTN_MAX_WIDTH_RATIO:    0.50,
    BTN_MIN_IMAGE_CHILDREN: 1,
    SLIDER_ASPECT_MIN:      3.0,
    CONTROL_MAX_CHILDREN:   12,
    IMAGE_RENDER_MODE:      'ps_visual'
};

try {
    if (typeof _ASYNC_EXPORT_STATE !== 'undefined' && _ASYNC_EXPORT_STATE) {
        var _staleExportState = _ASYNC_EXPORT_STATE;
        if (_staleExportState.tempDoc) {
            try { _staleExportState.tempDoc.close(SaveOptions.DONOTSAVECHANGES); } catch (staleCloseError) { }
        }
        if (_staleExportState.originalDoc) {
            try { app.activeDocument = _staleExportState.originalDoc; } catch (staleRestoreError) { }
        }
    }
} catch (staleExportCleanupError) { }
var _ASYNC_EXPORT_STATE = null;

function _normalizeImageRenderMode(value) {
    if (value === true) return 'unity_editable';
    if (value === false) return 'ps_visual';
    return String(value || '').toLowerCase() === 'unity_editable'
        ? 'unity_editable'
        : 'ps_visual';
}

var _CANONICAL_SONGTI_FONT_NAME = 'STSongti-SC-Regular';

function _normalizeFontKey(value) {
    return String(value || '')
        .replace(/^\[/, '')
        .replace(/\]$/, '')
        .replace(/[^A-Za-z0-9\u4e00-\u9fff]+/g, '')
        .toLowerCase();
}

function _isSongtiRelatedFontName(fontName) {
    var key = _normalizeFontKey(fontName);
    if (!key) return false;
    return key.indexOf('songti') >= 0 ||
        key.indexOf('stsong') >= 0 ||
        key.indexOf('simsun') >= 0 ||
        key.indexOf('nsimsun') >= 0 ||
        key.indexOf('\u5b8b\u4f53') >= 0 ||
        key.indexOf('\u5b8b\u9ad4') >= 0 ||
        key.indexOf('\u534e\u6587\u5b8b') >= 0 ||
        key.indexOf('\u83ef\u6587\u5b8b') >= 0 ||
        key.indexOf('\u7ecf\u5178\u5b8b') >= 0 ||
        key.indexOf('\u7d93\u5178\u5b8b') >= 0;
}

function _normalizeSongtiTextFonts(layers) {
    var stats = { changed: 0, skipped: 0, errors: [] };

    function walk(container) {
        for (var i = 0; i < container.length; i++) {
            var layer = container[i];
            try {
                if (layer.typename === 'LayerSet') {
                    walk(layer.layers);
                    continue;
                }

                if (layer.typename !== 'ArtLayer') continue;
                var isText = false;
                try { isText = layer.kind === LayerKind.TEXT; } catch (kindError) { }
                if (!isText) continue;

                var currentFont = '';
                try { currentFont = layer.textItem.font || ''; } catch (fontError) { }
                if (!_isSongtiRelatedFontName(currentFont)) {
                    stats.skipped++;
                    continue;
                }

                if (String(currentFont).toLowerCase() === _CANONICAL_SONGTI_FONT_NAME.toLowerCase()) {
                    stats.skipped++;
                    continue;
                }

                try { layer.allLocked = false; } catch (lockError) { }
                app.activeDocument.activeLayer = layer;
                layer.textItem.font = _CANONICAL_SONGTI_FONT_NAME;
                stats.changed++;
                _DEBUG_LOG.push('[FONT] ' + layer.name + ': ' + currentFont + ' -> ' + _CANONICAL_SONGTI_FONT_NAME);
            } catch (e) {
                stats.errors.push(layer.name + ': ' + e.message);
            }
        }
    }

    walk(layers);
    if (stats.changed > 0 || stats.errors.length > 0) {
        _DEBUG_LOG.push('[FONT] Songti normalize changed=' + stats.changed + ', skipped=' + stats.skipped + ', errors=' + stats.errors.length);
    }
    return stats;
}

// ╔══════════════════════════════════════════╗
// ║      中 英 翻 译 词 典  (v3.3)            ║
// ╚══════════════════════════════════════════╝
var _CN2EN = {
    // ── 通用 UI ──
    '确认': 'confirm', '确定': 'ok', '取消': 'cancel', '关闭': 'close',
    '返回': 'back', '退出': 'quit', '开始': 'start', '暂停': 'pause',
    '继续': 'resume', '重试': 'retry', '跳过': 'skip', '保存': 'save',
    '加载': 'load', '完成': 'done', '提交': 'submit', '重置': 'reset',
    '删除': 'delete', '编辑': 'edit', '添加': 'add', '搜索': 'search',
    '分享': 'share', '复制': 'copy', '粘贴': 'paste', '撤销': 'undo',
    '切换': 'switch', '选择': 'select', '筛选': 'filter', '排序': 'sort',
    // ── 账号 ──
    '登录': 'login', '注册': 'register', '账号': 'account', '密码': 'password',
    '游客': 'guest', '绑定': 'bind', '注销': 'logout', '修改': 'modify',
    // ── 导航 ──
    '首页': 'home', '主页': 'home', '商店': 'shop', '商城': 'mall',
    '背包': 'backpack', '好友': 'friend', '邮件': 'mail', '消息': 'message',
    '任务': 'task', '活动': 'activity', '排行': 'rank', '排行榜': 'leaderboard',
    '成就': 'achievement', '设置': 'setting', '公告': 'notice', '聊天': 'chat',
    '社交': 'social', '工坊': 'workshop', '仓库': 'warehouse',
    // ── 游戏 ──
    '战斗': 'battle', '挑战': 'challenge', '冒险': 'adventure', '探索': 'explore',
    '副本': 'dungeon', '关卡': 'stage', '章节': 'chapter', '世界': 'world',
    '地图': 'map', '竞技场': 'arena', '组队': 'team', '公会': 'guild',
    '赛季': 'season', '剧情': 'story', '试炼': 'trial', '远征': 'expedition',
    '回合': 'round', '阵容': 'lineup', '对话': 'dialog', '过关': 'clear',
    '通关': 'clear', '难度': 'diff', '波次': 'wave', '怪物': 'monster',
    '玩家': 'player', '对手': 'opponent', '队伍': 'team',
    // ── 经济 ──
    '购买': 'buy', '出售': 'sell', '充值': 'recharge', '金币': 'gold',
    '钻石': 'diamond', '体力': 'stamina', '经验': 'exp', '等级': 'lv',
    '价格': 'price', '数量': 'count', '免费': 'free', '折扣': 'discount',
    '货币': 'coin', '余额': 'balance', '上架': 'shelf',
    // ── 角色 ──
    '角色': 'role', '英雄': 'hero', '装备': 'equip', '技能': 'skill',
    '天赋': 'talent', '属性': 'attr', '血量': 'hp', '攻击': 'atk',
    '防御': 'def', '速度': 'speed', '生命': 'life', '魔法': 'mp',
    '战力': 'power', '宠物': 'pet', '坐骑': 'mount', '翅膀': 'wing',
    '伙伴': 'partner', '皮肤': 'skin',
    // ── 动作 ──
    '升级': 'upgrade', '强化': 'enhance', '进化': 'evolve', '觉醒': 'awaken',
    '合成': 'compose', '分解': 'decompose', '领取': 'claim', '使用': 'use',
    '召唤': 'summon', '抽卡': 'gacha', '签到': 'signin', '刷新': 'refresh',
    '解锁': 'unlock', '锁定': 'locked', '交换': 'exchange', '穿戴': 'wear',
    '下载': 'download', '上传': 'upload',
    // ── UI 元素 ──
    '标题': 'title', '名称': 'name', '描述': 'desc', '内容': 'content',
    '入口': 'entry', '出口': 'exit', '菜单': 'menu', '按钮': 'button',
    '进度': 'progress', '时间': 'time', '倒计时': 'countdown',
    '提示': 'tip', '警告': 'warning', '错误': 'error', '通知': 'notify',
    '页签': 'tab', '列表': 'list', '卡片': 'card', '气泡': 'bubble',
    '箭头': 'arrow', '红点': 'reddot', '标签': 'label', '选项': 'option',
    '勾选': 'check', '选中': 'selected', '未选中': 'unselected',
    // ── 状态 ──
    '奖励': 'reward', '礼包': 'gift', '福利': 'welfare',
    '每日': 'daily', '限时': 'limited', '已拥有': 'owned',
    '已完成': 'done', '未完成': 'undone', '更多': 'more',
    '全部': 'all', '在线': 'online', '离线': 'offline',
    '普通': 'normal', '默认': 'default', '高亮': 'highlight',
    '按下': 'pressed', '禁用': 'disabled', '空': 'empty',
    // ── MatchRPG 项目专用 ──
    '调教': 'train', '挂机': 'hangup', '贴纸': 'sticker',
    '珍藏': 'collection',
    // ── 视觉描述（多字优先，避免单字歧义） ──
    '背景': 'bg', '头像': 'avatar', '图标': 'icon', '按钮': 'button',
    '进度条': 'progressbar', '滑块': 'slider', '输入框': 'input',
    '图片': 'image', '面板': 'panel', '弹窗': 'popup',
    '遮罩': 'mask', '边框': 'border', '分割线': 'divider',
    '填充': 'fill', '滑动': 'slide', '滑杆': 'handle', '轨道': 'track',
    '滚动': 'scroll', '滚动条': 'scrollbar', '视口': 'viewport',
    '下拉': 'dropdown', '输入': 'input', '占位': 'placeholder',
    '勾': 'checkmark', '开关': 'toggle',
    '底部': 'bottom', '顶部': 'top',
    '左侧': 'left', '右侧': 'right', '左边': 'left', '右边': 'right',
    '上方': 'top', '下方': 'bottom', '中间': 'center', '中央': 'center',
    '新手': 'newbie', '新增': 'add'
};

// 调试日志数组（每次执行重置，导出同步保存到PSD文件夹）
var _DEBUG_LOG = [];
var _CEDICT = null;
var _NAMING_RULES_APPLIED = false;
var _SCORING_CONFIG = null;
var _PINYIN_LOAD_STATE = 0; // 0 = not tried, 1 = loaded, -1 = unavailable

function _readJsonFile(file) {
    if (!file || !file.exists) return null;
    try {
        file.encoding = 'UTF-8';
        file.open('r');
        var text = file.read();
        file.close();
        if (!text) return null;
        return _JSON.parse(text);
    } catch (e) {
        try { file.close(); } catch (ce) { }
        _DEBUG_LOG.push('[CONFIG-ERROR] ' + file.fsName + ': ' + e.message);
        return null;
    }
}

function _getConfigFolder() {
    var root = _getPSExtensionRootFolder();
    if (!root) return null;
    return new Folder(root.fsName + '/com.psd2ugui.smartexport/config');
}

function _getUserDataFolder() {
    try {
        var folder = new Folder(Folder.userData.fsName + '/PSD2UGUI_SmartExport');
        if (!folder.exists) folder.create();
        return folder;
    } catch (e) {
        return null;
    }
}

function _getPanelSettingsFile() {
    var folder = _getUserDataFolder();
    if (!folder) return null;
    return new File(folder.fsName + '/panel_settings.json');
}

function _getExtensionFolder() {
    try {
        var f = new File($.fileName);
        return f.parent.parent;
    } catch (e) {
        return null;
    }
}

function _ensurePinyinLoaded() {
    if (_PINYIN_LOAD_STATE !== 0) return _PINYIN_LOAD_STATE === 1;
    _PINYIN_LOAD_STATE = -1;

    try {
        if (typeof pinyinUtil !== 'undefined' && pinyinUtil && pinyinUtil.getPinyin) {
            _PINYIN_LOAD_STATE = 1;
            return true;
        }
    } catch (e0) { }

    try {
        var ext = _getExtensionFolder();
        if (!ext) return false;

        var dictFile = new File(ext.fsName + '/jsx/vendor/pinyinjs/pinyin_dict_notone.jsx');
        var utilFile = new File(ext.fsName + '/jsx/vendor/pinyinjs/pinyinUtil.jsx');
        if (!dictFile.exists || !utilFile.exists) {
            _DEBUG_LOG.push('[PINYIN-WARN] pinyin vendor missing: ' + dictFile.fsName);
            return false;
        }

        $.evalFile(dictFile);
        $.evalFile(utilFile);
        if (typeof pinyinUtil !== 'undefined' && pinyinUtil && pinyinUtil.getPinyin) {
            _PINYIN_LOAD_STATE = 1;
            return true;
        }
    } catch (e) {
        _DEBUG_LOG.push('[PINYIN-ERROR] ' + e.message);
    }
    return false;
}

function _applyNamingRulesConfig() {
    if (_NAMING_RULES_APPLIED) return;
    _NAMING_RULES_APPLIED = true;

    var folder = _getConfigFolder();
    if (!folder) return;

    var rules = _readJsonFile(new File(folder.fsName + '/naming_rules.json'));
    if (!rules) return;

    if (rules.dictOverrides) {
        for (var k in rules.dictOverrides) {
            if (rules.dictOverrides.hasOwnProperty(k) && rules.dictOverrides[k]) {
                _CN2EN[k] = String(rules.dictOverrides[k]);
            }
        }
    }
}

function _loadScoringConfig() {
    if (_SCORING_CONFIG !== null) return _SCORING_CONFIG;
    _SCORING_CONFIG = { bindThreshold: 0.60, reviewThreshold: 0.60 };

    var folder = _getConfigFolder();
    if (!folder) return _SCORING_CONFIG;

    var cfg = _readJsonFile(new File(folder.fsName + '/scoring.json'));
    if (!cfg) return _SCORING_CONFIG;

    if (typeof cfg.bindThreshold === 'number') _SCORING_CONFIG.bindThreshold = cfg.bindThreshold;
    if (typeof cfg.reviewThreshold === 'number') _SCORING_CONFIG.reviewThreshold = cfg.reviewThreshold;
    return _SCORING_CONFIG;
}

function _hasChinese(text) {
    return !!text && /[\u4e00-\u9fff]/.test(text);
}

function _getPSExtensionRootFolder() {
    try {
        var f = new File($.fileName);
        return f.parent.parent.parent;
    } catch (e) {
        return null;
    }
}

function _cedictDefinitionToSlug(definitions) {
    if (!definitions) return '';
    var defs = definitions.split('/');
    for (var i = 0; i < defs.length; i++) {
        var def = defs[i].replace(/^\s+|\s+$/g, '');
        if (!def) continue;
        if (/^(surname|variant of|old variant of|see\s)/i.test(def)) continue;
        if (/^(abbr\.|short for|classifier for|CL:)/i.test(def)) continue;

        def = def.replace(/\([^)]*\)/g, ' ');
        def = def.replace(/\b(sb|sth)\b/gi, ' ');
        def = def.replace(/\bto\s+/i, '');
        def = def.split(';')[0].split(',')[0];
        def = def.replace(/&/g, ' and ');
        def = def.replace(/[^a-zA-Z0-9\s_\-]+/g, ' ');

        var words = def.replace(/^\s+|\s+$/g, '').split(/\s+/);
        var kept = [];
        for (var w = 0; w < words.length && kept.length < 3; w++) {
            var word = words[w].toLowerCase();
            if (!word || /^(the|a|an|of|for|in|on|at|by|with|and|or)$/.test(word)) continue;
            kept.push(word);
        }
        if (kept.length) return _sanitizeText(kept.join('_'), 24);
    }
    return '';
}

function _loadCedict() {
    if (_CEDICT !== null) return _CEDICT;

    _CEDICT = { terms: {}, maxLen: 0, loaded: false, error: '' };
    try {
        var root = _getPSExtensionRootFolder();
        if (!root) {
            _CEDICT.error = '无法定位 PSExtension 目录';
            return _CEDICT;
        }

        var dictFile = new File(root.fsName + '/cedict_ts.u8');
        if (!dictFile.exists) {
            _CEDICT.error = '未找到 cedict_ts.u8';
            return _CEDICT;
        }

        dictFile.encoding = 'UTF-8';
        if (!dictFile.open('r')) {
            _CEDICT.error = '无法打开 cedict_ts.u8';
            return _CEDICT;
        }

        var line, match, traditional, simplified, slug;
        while (!dictFile.eof) {
            line = dictFile.readln();
            if (!line || line.charAt(0) === '#') continue;
            match = line.match(/^(\S+)\s+(\S+)\s+\[[^\]]+\]\s+\/(.+)\/$/);
            if (!match) continue;

            traditional = match[1];
            simplified = match[2];
            if (!_hasChinese(simplified) && !_hasChinese(traditional)) continue;
            if (simplified.length > 10 && traditional.length > 10) continue;

            slug = _cedictDefinitionToSlug(match[3]);
            if (!slug) continue;

            if (!_CEDICT.terms[simplified]) {
                _CEDICT.terms[simplified] = slug;
                if (simplified.length > _CEDICT.maxLen) _CEDICT.maxLen = simplified.length;
            }
            if (traditional !== simplified && !_CEDICT.terms[traditional]) {
                _CEDICT.terms[traditional] = slug;
                if (traditional.length > _CEDICT.maxLen) _CEDICT.maxLen = traditional.length;
            }
        }
        dictFile.close();
        _CEDICT.loaded = true;
        _DEBUG_LOG.push('[CEDICT] loaded terms=' + _CEDICT.maxLen + ' maxLen, file=' + dictFile.fsName);
    } catch (e) {
        _CEDICT.error = e.message;
        try { if (dictFile) dictFile.close(); } catch (ce) { }
    }
    return _CEDICT;
}

function _lookupCedict(text, allowSingleChar) {
    if (!text || (!_hasChinese(text))) return '';
    if (!allowSingleChar && text.length < 2) return '';
    var dict = _loadCedict();
    if (!dict.loaded) return '';
    return dict.terms[text] || '';
}

function _getTranslateMaxSegmentLen(text) {
    var maxLen = 6;
    var dict = _loadCedict();
    if (dict.loaded && dict.maxLen > maxLen) maxLen = dict.maxLen;
    return Math.min(10, Math.min(maxLen, text.length));
}

function _toPinyinSlug(text, maxLen) {
    if (!text || !_hasChinese(text)) return '';
    try {
        _ensurePinyinLoaded();
        if (typeof pinyinUtil !== 'undefined' && pinyinUtil && pinyinUtil.getPinyin) {
            var py = pinyinUtil.getPinyin(text, '_', false, false);
            py = py.replace(/[^a-zA-Z0-9_]+/g, '_').replace(/^_+|_+$/g, '').replace(/_+/g, '_');
            return _sanitizeText(py, maxLen || 24);
        }
    } catch (e) { }
    return '';
}

/**
 * 中文文本 -> 英文命名片段
 * 优先级:
 *  1. 本地词典精确匹配
 *  2. 已是英文 -> 直接 sanitize
 *  3. 本地词典贪心最长匹配分词
 *  4. 未命中中文字符 -> 离线拼音兜底
 */
function _translateText(text) {
    _applyNamingRulesConfig();

    if (!text) return '';
    text = text.replace(/[\r\n]+/g, ' ').replace(/^\s+|\s+$/g, '');
    if (!text) return '';

    // 1. 本地词典精确匹配
    if (_CN2EN[text]) return _CN2EN[text];

    // 2. CEDICT 精确匹配（低于项目词库，高于拼音兜底）
    var exactCedict = _lookupCedict(text, true);
    if (exactCedict) return exactCedict;

    // 3. 已是纯英文/数字
    if (/^[a-zA-Z0-9\s_\-]+$/.test(text)) {
        // 纯数字/数字+空格/数字+符号 -> 返回空（由调用者决定默认desc）
        if (/^[\d\s_\-\+\.\,\%]+$/.test(text)) return '';
        return _sanitizeText(text, 20);
    }

    // 4. 本地词典 + CEDICT 贪心最长匹配
    var parts = [];
    var i = 0;
    while (i < text.length) {
        var matched = false;
        var maxLen = Math.min(_getTranslateMaxSegmentLen(text), text.length - i);
        for (var len = maxLen; len >= 1; len--) {
            var seg = text.substring(i, i + len);
            var segText = _CN2EN[seg] || _lookupCedict(seg, false);
            if (segText) {
                // 单字匹配保护：如果只匹配到1个汉字且后面还跟着汉字，跳过（避免歧义）
                if (len === 1 && i + 1 < text.length && /[\u4e00-\u9fff]/.test(text.charAt(i + 1))) {
                    continue; // 试更短的？不，len=1已是最短，直接跳过这个字
                }
                parts.push(segText);
                i += len;
                matched = true;
                break;
            }
        }
        if (!matched) {
            var ch = text.charAt(i);
            if (/[a-zA-Z]/.test(ch)) {
                // 连续英文字母聚合
                var wordStart = i;
                while (i < text.length && /[a-zA-Z]/.test(text.charAt(i))) i++;
                parts.push(text.substring(wordStart, i));
            } else if (/[\u4e00-\u9fff]/.test(ch)) {
                var py = _toPinyinSlug(ch, 8);
                if (py) parts.push(py);
                i++;
            } else {
                // 未匹配的汉字/数字/符号 -> 跳过
                i++;
            }
            matched = true;
        }
    }

    var result = parts.join('_').replace(/_+/g, '_').replace(/^_|_$/g, '');
    if (result.length > 20) {
        var cutAt = result.lastIndexOf('_', 20);
        if (cutAt > 10) {
            result = result.substring(0, cutAt);
        } else {
            result = result.substring(0, 20);
        }
    }
    // 如果结果只剩数字/下划线 -> 返回空
    if (!result || /^[\d_]+$/.test(result)) {
        result = _toPinyinSlug(text, 24);
    }
    if (!result || /^[\d_]+$/.test(result)) return '';
    return result.toLowerCase();
}

/**
 * 获取组内第一段文本内容（递归一层子组）
 * 用于组件命名：按钮名 = 按钮内的文字的翻译
 */
function _getFirstTextContent(group) {
    if (group.typename !== 'LayerSet') return '';
    for (var i = 0; i < group.layers.length; i++) {
        var child = group.layers[i];
        if (!child.visible) continue;
        try {
            if (child.typename === 'ArtLayer' && child.kind === LayerKind.TEXT) {
                return child.textItem.contents;
            }
        } catch (e) { }
        // 递归一层子组
        if (child.typename === 'LayerSet') {
            var sub = _getFirstTextContent(child);
            if (sub) return sub;
        }
    }
    return '';
}

// ╔══════════════════════════════════════════╗
// ║      CEP Panel Entry Points              ║
// ╚══════════════════════════════════════════╝

/**
 * 获取当前文档信息（面板显示用）
 */
function PSD2UGUI_getDocInfo() {
    try {
        if (!app.documents.length) return 'null';
        var doc = app.activeDocument;
        var fileName = doc.name.replace(/\.[^.]+$/, '');

        var guessModule = '';
        var lower = fileName.toLowerCase();
        var knownModules = ['common', 'home', 'battle', 'shop', 'setting',
            'confirm', 'hangup', 'chapterlevel', 'levelmap', 'reward',
            'teach', 'tuning', 'worldmap', 'loading', 'userinfo',
            'dungeon', 'train', 'sticker', 'backpack', 'collection', 'friends'];
        for (var i = 0; i < knownModules.length; i++) {
            if (lower.indexOf(knownModules[i]) >= 0) {
                guessModule = knownModules[i];
                break;
            }
        }

        var count = 0;
        var countTruncated = false;
        var maxDocInfoLayers = 500;
        function countLayers(layers) {
            for (var i = 0; i < layers.length; i++) {
                count++;
                if (count >= maxDocInfoLayers) {
                    countTruncated = true;
                    return;
                }
                if (layers[i].typename === 'LayerSet') countLayers(layers[i].layers);
                if (countTruncated) return;
            }
        }
        countLayers(doc.layers);

        return _JSON.stringify({
            name: doc.name, baseName: fileName,
            width: Math.round(doc.width.as('px')),
            height: Math.round(doc.height.as('px')),
            layerCount: count, layerCountTruncated: countTruncated, guessModule: guessModule
        });
    } catch (e) {
        return _JSON.stringify({ error: e.message });
    }
}

function PSD2UGUI_loadModuleSettings() {
    try {
        var file = _getPanelSettingsFile();
        var data = _readJsonFile(file);
        if (!data) return _JSON.stringify({ modules: null, lastModule: '' });
        return _JSON.stringify({
            modules: data.modules || null,
            lastModule: data.lastModule || ''
        });
    } catch (e) {
        return _JSON.stringify({ error: e.message });
    }
}

function PSD2UGUI_saveModuleSettings(jsonText) {
    try {
        var file = _getPanelSettingsFile();
        if (!file) return _JSON.stringify({ ok: false, error: '无法创建用户配置目录' });

        var data = _JSON.parse(String(jsonText || '{}'));
        file.encoding = 'UTF-8';
        if (!file.open('w')) return _JSON.stringify({ ok: false, error: '无法写入模块配置文件' });
        file.write(_JSON.stringify({
            modules: data.modules || [],
            lastModule: data.lastModule || ''
        }));
        file.close();
        return _JSON.stringify({ ok: true, path: file.fsName });
    } catch (e) {
        try { if (file) file.close(); } catch (closeError) { }
        return _JSON.stringify({ ok: false, error: e.message });
    }
}

function _getExportPathSettingsFile() {
    var folder = _getUserDataFolder();
    if (!folder) return null;
    return new File(folder.fsName + '/export_paths.json');
}

function _getDocumentPathKey(doc) {
    if (!doc) return '';
    try { return String(doc.fullName.fsName || '').toLowerCase(); } catch (e1) { }
    try { return String(doc.path.fsName + '/' + doc.name).toLowerCase(); } catch (e2) { }
    try { return String(doc.name || '').toLowerCase(); } catch (e3) { }
    return '';
}

function _loadRememberedExportFile(doc) {
    var settingsFile = _getExportPathSettingsFile();
    var data = _readJsonFile(settingsFile);
    var key = _getDocumentPathKey(doc);
    if (!data || !key || !data.byDocument || !data.byDocument[key]) return null;

    var file = new File(String(data.byDocument[key]));
    if (!/\.psd$/i.test(file.name) || !file.parent || !file.parent.exists) return null;
    return file;
}

function _rememberExportFile(doc, file) {
    if (!doc || !file) return;
    var settingsFile = _getExportPathSettingsFile();
    var key = _getDocumentPathKey(doc);
    if (!settingsFile || !key) return;

    var data = _readJsonFile(settingsFile) || {};
    if (!data.byDocument) data.byDocument = {};
    data.byDocument[key] = file.fsName;
    data.lastExportPath = file.fsName;

    settingsFile.encoding = 'UTF-8';
    if (!settingsFile.open('w')) return;
    settingsFile.write(_JSON.stringify(data));
    settingsFile.close();
}

function _configureExecution(renameGenericOnly, markDuplicatesRef, imageRenderMode, skipHidden) {
    _CONFIG.RENAME_ONLY_GENERIC = (renameGenericOnly !== false);
    _CONFIG.MARK_DUPLICATES_REF = (markDuplicatesRef !== false);
    _CONFIG.IMAGE_RENDER_MODE = _normalizeImageRenderMode(imageRenderMode);
    _CONFIG.SKIP_HIDDEN_LAYERS = (skipHidden !== false);
    _LAYER_ANALYSIS_CACHE = {};
}

function PSD2UGUI_getHostVersion() {
    return '4.4.5';
}

function PSD2UGUI_beginExport(moduleName, mode, renameGenericOnly, markDuplicatesRef, imageRenderMode, skipHidden) {
    _configureExecution(renameGenericOnly, markDuplicatesRef, imageRenderMode, skipHidden);
    if (!moduleName) moduleName = 'img';
    try {
        if (!app.documents.length) return _JSON.stringify({ error: '请先打开一个 PSD 文件' });
        return _JSON.stringify(_beginChunkedExportFlow(moduleName, mode || 'both'));
    } catch (e) {
        _cancelChunkedExportFlow();
        return _JSON.stringify({ error: e.message, line: e.line });
    }
}

function _normalizeDisposableWorkPath(value) {
    try {
        var file = value instanceof File ? value : new File(String(value || ''));
        return String(file.fsName || '').replace(/\\/g, '/').toLowerCase();
    } catch (e) {
        return '';
    }
}

function _validateDisposableWorkDocument(doc, expectedWorkPath) {
    var actualPath = '';
    try { actualPath = _normalizeDisposableWorkPath(doc.fullName); } catch (e) { }
    var expectedPath = _normalizeDisposableWorkPath(expectedWorkPath);
    if (!actualPath || !expectedPath || actualPath !== expectedPath) {
        return '一次性工作 PSD 路径与当前文档不一致';
    }
    if (actualPath.indexOf('/temp/psd2uiform/') < 0 || !/\.psd2ui-work\.psd$/i.test(actualPath)) {
        return '一次性工作 PSD 必须位于 Temp/PSD2UIForm 且以 .psd2ui-work.psd 结尾';
    }
    return null;
}

/**
 * 在已经过严格路径校验的一次性工作 PSD 上原地执行 Smart Export。
 * 调用方必须先从艺术源创建该工作副本；此入口会关闭工作文档，绝不用于艺术源。
 */
function PSD2UGUI_beginDisposableExport(moduleName, mode, renameGenericOnly,
    markDuplicatesRef, imageRenderMode, skipHidden, expectedWorkPath) {
    _configureExecution(renameGenericOnly, markDuplicatesRef, imageRenderMode, skipHidden);
    if (!moduleName) moduleName = 'img';
    try {
        if (!app.documents.length) return _JSON.stringify({ error: '请先打开一个 PSD 文件' });
        var validationError = _validateDisposableWorkDocument(app.activeDocument, expectedWorkPath);
        if (validationError) return _JSON.stringify({ error: validationError });
        return _JSON.stringify(_beginChunkedExportFlow(moduleName, mode || 'both', true));
    } catch (e) {
        _cancelChunkedExportFlow();
        return _JSON.stringify({ error: e.message, line: e.line });
    }
}

function PSD2UGUI_prepareExportStep() {
    try {
        return _JSON.stringify(_prepareNextChunkedExportStep());
    } catch (e) {
        _cancelChunkedExportFlow();
        return _JSON.stringify({ error: e.message, line: e.line });
    }
}

function PSD2UGUI_finishExport() {
    try {
        return _JSON.stringify(_finishChunkedExportFlow());
    } catch (e) {
        _cancelChunkedExportFlow();
        return _JSON.stringify({ error: e.message, line: e.line });
    }
}

function PSD2UGUI_cancelExport() {
    _cancelChunkedExportFlow();
    return _JSON.stringify({ cancelled: true });
}

/**
 * 主入口：执行智能命名/导出
 */
function PSD2UGUI_execute(moduleName, mode, renameGenericOnly, markDuplicatesRef, imageRenderMode, skipHidden) {
    _configureExecution(renameGenericOnly, markDuplicatesRef, imageRenderMode, skipHidden);

    if (!moduleName) moduleName = 'img';

    try {
        if (!app.documents.length) {
            return _JSON.stringify({ error: '请先打开一个 PSD 文件' });
        }
        return _executeFlow(moduleName, mode || 'both');
    } catch (e) {
        return _JSON.stringify({ error: e.message, line: e.line });
    }
}

/**
 * 预览分类结果（不做任何修改）
 * v3.1: 组件显示 $分类 前缀
 */
function PSD2UGUI_preview(moduleName, skipHidden) {
    _CONFIG.SKIP_HIDDEN_LAYERS = (skipHidden !== false);
    _LAYER_ANALYSIS_CACHE = {};
    try {
        if (!app.documents.length) return _JSON.stringify({ error: '无文档' });
        var doc = app.activeDocument;
        var cw = doc.width.as('px'), ch = doc.height.as('px');
        if (!moduleName) moduleName = 'img';

        var infos = [];
        _collectForRename(doc.layers, infos, cw, ch, false);
        var seqCounters = {};

        var result = [];
        for (var i = 0; i < infos.length; i++) {
            var info = infos[i];
            if (info.classification === 'group') continue;
            var origName = info.layer.name;
            var newName = _buildLayerName(info, moduleName, seqCounters);
            result.push({
                original: origName,
                proposed: newName,
                classification: (info.shouldBind ? '@' : '') + info.classification,
                w: info.bounds ? Math.round(info.bounds.width) : 0,
                h: info.bounds ? Math.round(info.bounds.height) : 0
            });
        }
        return _JSON.stringify({ layers: result, total: result.length });
    } catch (e) {
        return _JSON.stringify({ error: e.message });
    }
}

// ╔══════════════════════════════════════════╗
// ║         工 具 函 数                      ║
// ╚══════════════════════════════════════════╝

function _isGenericName(name) {
    if (!name) return true;
    // 循环去除 "副本/拷贝/copy" 后缀（处理 "图层 1 副本 2 副本"）
    var n = _stripKnownPluginTags(name);
    var prev;
    do {
        prev = n;
        n = n.replace(/\s*(副本|拷贝)\s*\d*$/g, '')
             .replace(/\s*copy\s*\d*$/gi, '');
    } while (n !== prev);
    n = n.replace(/^\s+|\s+$/g, '');
    if (_isSymbolOnlyLayerName(n)) return true;
    if (_isShapeGeometryLayerName(n)) return true;
    if (/^(Layer|图层|Rectangle|矩形|Shape|形状|Ellipse|椭圆|Line|线条|Path|路径|Group|组|Background|背景|Polygon|多边形|Frame|Vector Smart Object|Smart Object|Color Fill|Gradient Fill|颜色填充|渐变填充|Adjustment|调整)\s*\d*$/i.test(n))
        return true;
    if (/^\d+$/.test(n)) return true;
    return false;
}

function _isSymbolOnlyLayerName(name) {
    if (!name) return true;
    var n = String(name).replace(/^\s+|\s+$/g, '');
    if (!n) return true;
    n = n.replace(/^@ref:/i, '').replace(/^@/, '');
    n = n.replace(/#nobind\b|!bind\b|\[nobind\]/ig, '');
    n = n.replace(/#bind\b|\[bind\]/ig, '');
    n = n.replace(/^\s+|\s+$/g, '');
    if (!n) return true;
    return !/[a-zA-Z0-9\u4e00-\u9fff]/.test(n);
}

function _normalizeGeometryName(name) {
    var n = _stripKnownPluginTags(name || '');
    n = n.replace(/^@ref:/i, '').replace(/^@/, '');
    var prev;
    do {
        prev = n;
        n = n.replace(/\s*(副本|拷贝)\s*\d*$/g, '')
             .replace(/\s*copy\s*\d*$/gi, '')
             .replace(/[_\-\s]*(kaobei|copy|fuben)[_\-\s]*\d*$/gi, '');
    } while (n !== prev);
    n = n.replace(/^\s+|\s+$/g, '').toLowerCase();
    n = n.replace(/[\s_\-]+/g, '');
    return n;
}

function _isShapeGeometryLayerName(name) {
    var n = _normalizeGeometryName(name);
    if (!n) return false;
    // 这些只描述绘制几何，不表达 UI 功能；不能作为最终语义名保留。
    return /^(圆角矩形|圓角矩形|矩形|三角形|形状|形狀|椭圆|橢圓|多边形|多邊形|线条|線條|路径|路徑|yuanjiaojuxing|juxing|sanjiaoxing|xingzhuang|tuoyuan|duobianxing|xiantiao|lujing|rectangle|roundedrectangle|roundrect|rect|triangle|shape|ellipse|oval|polygon|line|path)\d*$/.test(n);
}

/**
 * 判断层名是否像数字/价格/数据文本（需要强制改名）
 * 匹配：纯数字、¥99.9、Lv.10、100%、100/200、1,234、+50、12:30 等
 */
function _isNumericLayerName(name) {
    if (!name) return false;
    var n = name.replace(/^\s+|\s+$/g, '');
    return /^[\+\-]?[\¥\$\€\£]?[\d\s\,\.\-\/\:\+\%\×]+[\u5206\u5143\u4e07\u4ebf\u7ea7\u5c42\u9636\u661f]*$/.test(n);
}

function _hasPluginTag(name) {
    if (!name) return false;
    return /\.(img|rimg|txt|tmptxt|msk|col|bg|bt|tmpbt|sld|sv|tg|tmptg|ipt|tmpipt|dpd|tmpdpd|fill|handle|vpt|hbarbg|hbar|vbarbg|vbar|onover|press|select|disable|bttxt|dpdlb|dpdicon|placeholder|ipttxt|mark|tglb)\s*$/i.test(name);
}

function _getPluginTagWithoutDot(name) {
    if (!name) return '';
    var m = String(name).match(/\.([a-zA-Z0-9_]+)\s*$/);
    return m ? m[1].toLowerCase() : '';
}

function _classifyByExplicitTag(name) {
    var tag = _getPluginTagWithoutDot(name);
    switch (tag) {
        case 'bt':
        case 'tmpbt':
            return 'btn';
        case 'txt':
        case 'tmptxt':
            return 'txt';
        case 'sld':
            return 'slider';
        case 'tg':
        case 'tmptg':
            return 'toggle';
        case 'ipt':
        case 'tmpipt':
            return 'input';
        case 'dpd':
        case 'tmpdpd':
            return 'dropdown';
        case 'sv':
            return 'scrollview';
        case 'bg':
            return 'bg';
        case 'img':
        case 'rimg':
            return 'img';
        default:
            return '';
    }
}

function _isGenericTextLayerName(name) {
    if (!name) return true;
    var n = _stripKnownPluginTags(name).replace(/^\s+|\s+$/g, '').toLowerCase();
    return !n || /^(txt|text|label|tmptext|tmptxt|文本|文字)\s*\d*$/.test(n) || _isGenericName(n);
}

function _sanitizeText(text, maxLen) {
    if (!text) return '';
    maxLen = maxLen || 12;
    var s = text.replace(/[\r\n]+/g, '').replace(/^\s+|\s+$/g, '');
    s = s.replace(/[\\\/\:\*\?\"\<\>\|\.\,\;\!\@\#\$\%\^\&\(\)\[\]\{\}\s]+/g, '_');
    s = s.replace(/^_+|_+$/g, '').replace(/_+/g, '_');
    if (s.length > maxLen) {
        // 词边界截断：在 maxLen 范围内找最后一个 '_' 位置
        var cutAt = s.lastIndexOf('_', maxLen);
        if (cutAt > maxLen / 2) {
            s = s.substring(0, cutAt);
        } else {
            s = s.substring(0, maxLen);
        }
    }
    return s.toLowerCase();
}

function _getLayerBounds(layer) {
    try {
        var b = layer.bounds;
        var l = b[0].as('px'), t = b[1].as('px');
        var r = b[2].as('px'), bt = b[3].as('px');
        return { left: l, top: t, right: r, bottom: bt, width: r - l, height: bt - t };
    } catch (e) {
        return null;
    }
}

function _isTextLayer(layer) {
    if (!layer || layer.typename !== 'ArtLayer') return false;
    try { return layer.kind === LayerKind.TEXT; } catch (e) { return false; }
}

function _getTextLayerContent(layer) {
    if (!_isTextLayer(layer)) return '';
    try { return layer.textItem.contents || ''; } catch (e) { return ''; }
}

function _cleanTextContent(text) {
    return String(text || '').replace(/[\r\n]+/g, ' ').replace(/^\s+|\s+$/g, '');
}

function _isShortUiText(text) {
    var clean = _cleanTextContent(text);
    if (!clean) return false;
    return clean.length <= 12 && !/[\r\n，,。；;：:、]/.test(text);
}

function _getLayerCenter(bounds) {
    return {
        x: bounds.left + bounds.width / 2,
        y: bounds.top + bounds.height / 2
    };
}

function _getNearbyTextInfo(layer, options) {
    options = options || {};
    var layerBounds = _getLayerBounds(layer);
    if (!layerBounds) return null;

    var best = null;
    var bestScore = 999999999;
    var center = _getLayerCenter(layerBounds);
    var expandX = options.expandX || 48;
    var expandY = options.expandY || 80;
    var maxLen = options.maxLen || 12;

    try {
        var parent = layer.parent;
        if (!parent || !parent.layers) return null;

        for (var i = 0; i < parent.layers.length; i++) {
            var sibling = parent.layers[i];
            if (!sibling || sibling === layer || !_isTextLayer(sibling) || !sibling.visible) continue;

            var text = _getTextLayerContent(sibling);
            var clean = _cleanTextContent(text);
            if (!clean || clean.length > maxLen) continue;

            var textBounds = _getLayerBounds(sibling);
            if (!textBounds) continue;

            var textCenter = _getLayerCenter(textBounds);
            var within = textCenter.x >= layerBounds.left - expandX &&
                textCenter.x <= layerBounds.right + expandX &&
                textCenter.y >= layerBounds.top - expandY &&
                textCenter.y <= layerBounds.bottom + expandY;
            if (!within) continue;

            var dx = textCenter.x - center.x;
            var dy = textCenter.y - center.y;
            var score = dx * dx + dy * dy;
            if (score < bestScore) {
                bestScore = score;
                best = {
                    layer: sibling,
                    text: text,
                    cleanText: clean,
                    bounds: textBounds,
                    score: score
                };
            }
        }
    } catch (e) { }

    return best;
}

function _looksLikeButtonRect(bounds, canvasW, canvasH) {
    if (!bounds || bounds.width <= 0 || bounds.height <= 0) return false;
    if (canvasW > 0 && bounds.width >= canvasW * 0.65) return false;
    if (canvasH > 0 && bounds.height >= canvasH * 0.18) return false;
    if (bounds.width < 64 || bounds.height < 28) return false;
    var aspect = bounds.width / Math.max(1, bounds.height);
    return aspect >= 1.45 && aspect <= 7.5;
}

function _hasNegativeControlName(name) {
    return _nameHas(name, /(^|[_\-\s])(itembg|item_bg|item\-bg|bg|background|panel|pane|card|cell|list|content|title|mask|banner|divider|line)([_\-\s]|$)|底图|背景|面板|卡片|列表|内容|标题|遮罩|横幅|分割线/);
}

function _hasButtonSemanticName(name) {
    return _nameHas(name, /(^|[_\-\s])(btn|button)([_\-\s]|$)|common_btn|btn_|button_|按钮|关闭|返回|确认|取消|领取|购买|出售|开始|继续|重试|提交|保存|删除|编辑|添加/);
}

// ╔══════════════════════════════════════════╗
// ║    分 类 引 擎  (层级感知 v3.1)          ║
// ╚══════════════════════════════════════════╝

/**
 * 组件分类列表
 * 组件: btn, slider, toggle, input, dropdown, scrollview
 * txt/img 默认不绑定；需要绑定时可在原始 PSD 图层名加 #bind，最终命名不保留该标记。
 * 非组件: txt, img, bg, icon, group
 *
 * 绑定意图写入 manifest/PsdLayerMarker，Unity 侧不再依赖 @ 前缀判断。
 */
function _isComponentClass(cls) {
    return cls === 'btn' || cls === 'slider' || cls === 'toggle' ||
        cls === 'input' || cls === 'dropdown' || cls === 'scrollview';
}

function _hasExplicitBindIntent(name) {
    return /(^|[\s_\-#\[\(])bind($|[\s_\-\]\)])/i.test(name || '') || /^@/.test(name || '');
}

function _hasExplicitNoBindIntent(name) {
    return /#nobind\b|!bind\b|\[nobind\]/i.test(name || '');
}

function _shouldBindByDefault(layer, classification) {
    if (!_isComponentClass(classification)) return false;

    var name = layer && layer.name ? layer.name : '';
    if (_hasExplicitBindIntent(name)) return true;
    if (_hasExplicitNoBindIntent(name)) return false;

    // 交互入口默认需要代码事件或状态控制。
    if (classification === 'btn' || classification === 'toggle' ||
        classification === 'input' || classification === 'dropdown') {
        return true;
    }

    // 进度条/列表并不总是需要绑定，只在命名或子文本表现出数据语义时默认绑定。
    var semantic = name;
    try {
        if (layer.typename === 'LayerSet') {
            semantic += ' ' + _getFirstTextContent(layer);
        }
    } catch (e) { }

    if (classification === 'slider') {
        return /progress|slider|sld|hp|exp|level|time|count|value|进度|血|经验|等级|时间|数量|滑块/.test(semantic.toLowerCase());
    }

    if (classification === 'scrollview') {
        return /scroll|list|content|viewport|item|sv|列表|滚动|内容|条目/.test(semantic.toLowerCase());
    }

    return false;
}

function _getKindName(classification) {
    switch (classification) {
        case 'btn': return 'button';
        case 'txt': return 'text';
        case 'bg': return 'background';
        case 'img': return 'image';
        default: return classification || 'unknown';
    }
}

function _getBindingCategory(classification) {
    switch (classification) {
        case 'btn':
        case 'toggle':
        case 'input':
        case 'dropdown':
            return 'interactive';
        case 'slider':
        case 'scrollview':
            return 'data';
        case 'txt':
            return 'display';
        default:
            return 'visual';
    }
}

function _getClassificationConfidence(layer, classification, canvasW, canvasH, insideComponent) {
    if (insideComponent) return classification === 'group' ? 0.25 : 0.85;
    if (classification === 'group') return 0.30;
    if (classification === 'txt') return 1.00;
    if (classification === 'bg' || classification === 'icon' || classification === 'img') return 0.82;

    var score = 0.60;
    var name = layer && layer.name ? layer.name : '';
    if (_hasExplicitBindIntent(name)) score += 0.25;
    if (_hasExplicitNoBindIntent(name)) score -= 0.20;

    if (classification === 'btn' && _hasButtonSemanticName(name)) score += 0.18;
    if (classification === 'btn' && !_hasButtonSemanticName(name)) {
        try {
            var nearbyText = _getNearbyTextInfo(layer, { expandX: 64, expandY: 96, maxLen: 12 });
            if (nearbyText && _isShortUiText(nearbyText.text)) score += 0.12;
        } catch (e1) { }
    }
    if (classification === 'slider' && _nameHas(name, /slider|sld|progress|进度|滑块|血条|经验条/)) score += 0.18;
    if (classification === 'toggle' && _nameHas(name, /toggle|checkbox|tg|勾选|开关/)) score += 0.18;
    if (classification === 'input' && _nameHas(name, /input|field|ipt|输入|搜索/)) score += 0.18;
    if (classification === 'dropdown' && _nameHas(name, /dropdown|dpd|下拉|选项/)) score += 0.18;
    if (classification === 'scrollview' && _nameHas(name, /scroll|list|sv|viewport|列表|滚动|视口/)) score += 0.18;

    var bounds = _getLayerBounds(layer);
    if (bounds && canvasW > 0 && canvasH > 0) {
        var areaRatio = (bounds.width * bounds.height) / Math.max(1, canvasW * canvasH);
        if (areaRatio > 0.70 && _isComponentClass(classification)) score -= 0.20;
        if (areaRatio < 0.001 && classification !== 'icon') score -= 0.10;
    }

    if (score < 0) score = 0;
    if (score > 1) score = 1;
    return Math.round(score * 100) / 100;
}

function _normalizeLayerName(name) {
    return (name || '').replace(/^\s+|\s+$/g, '').toLowerCase();
}

function _stripKnownPluginTags(name) {
    var n = name || '';
    var prev;
    do {
        prev = n;
        n = n.replace(/\.(img|rimg|txt|tmptxt|msk|col|bg|bt|tmpbt|sld|sv|tg|tmptg|ipt|tmpipt|dpd|tmpdpd|fill|handle|vpt|hbarbg|hbar|vbarbg|vbar|onover|press|select|disable|bttxt|dpdlb|dpdicon|placeholder|ipttxt|mark|tglb)\s*$/i, '');
    } while (n !== prev);
    return n;
}

function _nameHas(name, pattern) {
    return pattern.test(_normalizeLayerName(_stripKnownPluginTags(name)));
}

function _hasNonButtonGroupName(name) {
    return _hasNegativeControlName(name);
}

/**
 * 分析组的直接子层构成（不递归）
 */
function _analyzeGroupChildren(group) {
    var result = {
        hasImage: false, hasText: false,
        textContent: '', imageCount: 0, textCount: 0,
        hasSubGroup: false, subGroupCount: 0,
        visualCount: 0, totalVisible: 0, names: ''
    };
    for (var i = 0; i < group.layers.length; i++) {
        var child = group.layers[i];
        if (!child.visible) continue;
        result.totalVisible++;
        result.names += ' ' + child.name;
        try {
            if (child.typename === 'ArtLayer') {
                try {
                    if (child.kind === LayerKind.TEXT) {
                        result.hasText = true;
                        result.textCount++;
                        if (!result.textContent) result.textContent = child.textItem.contents;
                    } else {
                        result.hasImage = true;
                        result.imageCount++;
                        result.visualCount++;
                    }
                } catch (e2) { result.hasImage = true; result.imageCount++; result.visualCount++; }
            } else if (child.typename === 'LayerSet') {
                result.hasSubGroup = true;
                result.subGroupCount++;
                result.visualCount++;
            }
        } catch (e) { }
    }
    return result;
}

function _isGenericContainerGroupName(name) {
    var n = _normalizeLayerName(_stripKnownPluginTags(name || ''));
    n = n.replace(/[\s_\-]+/g, '');
    n = n.replace(/拷贝\d*|copy\d*|副本\d*/ig, '');
    if (!n) return true;
    return /^(组|圖層組|图层组|group|grp|zu|layergroup)\d*$/i.test(n);
}

function _hasExplicitComponentSemantic(name) {
    return _nameHas(name || '', /btn|button|common_btn|slider|sld|progress|toggle|checkbox|input|dropdown|scroll|scrollview|按钮|按鈕|关闭|關閉|确定|確認|取消|使用|购买|購買|领取|領取|进度|進度|滑块|滑桿|勾选|勾選|复选|複選|输入|輸入|下拉|滚动|滾動|列表/);
}

function _isActionButtonText(text) {
    var t = String(text || '').replace(/^\s+|\s+$/g, '');
    if (!_isShortUiText(t)) return false;
    if (/^[\d\s\/.,+\-＋－]+$/.test(t)) return false;
    if (/^(min|max)$/i.test(t)) return false;
    return /确定|確認|确认|取消|关闭|關閉|使用|购买|購買|领取|領取|兑换|兌換|出售|卖出|賣出|合成|升级|升級|前往|打开|打開|领取|保存|提交|重置|搜索|筛选|篩選|ok|yes|no|use|buy|sell|close|submit|save|search|filter|go/i.test(t);
}

function _looksLikeEmptyStructuralGroup(group) {
    if (!group || group.typename !== 'LayerSet') return false;
    if (_classifyByExplicitTag(group.name)) return false;

    var analysis = _analyzeGroupChildren(group);
    if (analysis.totalVisible <= 0) return true;

    var semantic = (group.name || '') + ' ' + analysis.names;
    if (_hasExplicitComponentSemantic(semantic)) return false;
    if (!_isGenericContainerGroupName(group.name)) return false;

    // Photoshop 组本身通常没有可导出像素；若可见内容都来自子节点，
    // 且没有控件语义，就只作为结构容器保留，避免生成空 Image/Button。
    return analysis.hasSubGroup || analysis.totalVisible > 1;
}

/**
 * 检测数量步进器/使用量选择器：
 * MIN / MAX + - / + + 数量文本，这类不是 Slider。
 */
function _collectGroupTextAndNames(group, result) {
    result = result || { names: '', texts: '', textCount: 0, visibleCount: 0 };
    if (!group || group.typename !== 'LayerSet') return result;

    for (var i = 0; i < group.layers.length; i++) {
        var child = group.layers[i];
        if (!child || !child.visible) continue;

        result.visibleCount++;
        result.names += ' ' + (child.name || '');

        try {
            if (_isTextLayer(child)) {
                result.textCount++;
                result.texts += ' ' + _getTextLayerContent(child);
            } else if (child.typename === 'LayerSet') {
                _collectGroupTextAndNames(child, result);
            }
        } catch (e) { }
    }

    return result;
}

function _looksLikeAmountStepper(group) {
    var collected = _collectGroupTextAndNames(group);
    var semantic = _normalizeLayerName((group.name || '') + ' ' + collected.names + ' ' + collected.texts);

    var hasMin = /(^|[^a-z0-9])min([^a-z0-9]|$)|最小|下限/.test(semantic);
    var hasMax = /(^|[^a-z0-9])max([^a-z0-9]|$)|最大|上限/.test(semantic);
    var hasMinus = /(^|[^a-z0-9])(jian|minus|sub|subtract|reduce|decrease)([^a-z0-9]|$)|减|－/.test(semantic);
    var hasPlus = /(^|[^a-z0-9])(jia|plus|add|increase)([^a-z0-9]|$)|加|＋|\+/.test(semantic);
    var hasCountValue = /\d+\s*\/\s*\d+/.test(collected.texts);
    var hasAmountWords = /数量|使用量|个数|count|amount|num|number/.test(semantic);

    if (hasMin && hasMax && (hasMinus || hasPlus)) return true;
    if (hasMinus && hasPlus && (hasCountValue || hasAmountWords)) return true;
    return hasMin && hasMax && collected.textCount >= 2 && collected.visibleCount >= 4;
}

/**
 * 检测滑块结构：一个长条(轨道) + 至少一个小块(手柄/填充)
 */
function _isSliderPattern(group) {
    if (_hasNonButtonGroupName(group.name)) return false;
    if (_looksLikeAmountStepper(group)) return false;

    var analysis = _analyzeGroupChildren(group);

    var children = [];
    var childNames = '';
    for (var i = 0; i < group.layers.length; i++) {
        if (!group.layers[i].visible) continue;
        childNames += ' ' + group.layers[i].name;
        var b = _getLayerBounds(group.layers[i]);
        if (b && b.width > 0 && b.height > 0) children.push(b);
    }
    if (children.length < 2) return false;

    var maxArea = -1, trackIdx = -1;
    for (var i = 0; i < children.length; i++) {
        var area = children[i].width * children[i].height;
        if (area > maxArea) { maxArea = area; trackIdx = i; }
    }
    var track = children[trackIdx];
    var aspect = Math.max(track.width, track.height) / Math.max(1, Math.min(track.width, track.height));
    if (aspect < _CONFIG.SLIDER_ASPECT_MIN) return false;

    var hasSliderName = _nameHas(group.name + ' ' + childNames, /slider|sld|progress|bar|fill|handle|进度|滑块|滑杆|血条|经验条|能量条|填充/);
    var allowUnnamedStructuralSlider = !hasSliderName &&
        children.length <= 4 &&
        analysis.textCount === 0 &&
        !analysis.hasSubGroup;

    for (var i = 0; i < children.length; i++) {
        if (i === trackIdx) continue;
        var areaRatio = (children[i].width * children[i].height) / maxArea;
        var sameAxis = (track.width >= track.height)
            ? (children[i].height <= track.height * 1.8)
            : (children[i].width <= track.width * 1.8);
        if (areaRatio < 0.45 && sameAxis) {
            if (hasSliderName) return true;
            return allowUnnamedStructuralSlider && areaRatio < 0.18;
        }
    }
    return false;
}

function _isTogglePattern(group, canvasW, canvasH) {
    var bounds = _getLayerBounds(group);
    var analysis = _analyzeGroupChildren(group);
    if (!bounds || !analysis.hasImage || analysis.totalVisible > _CONFIG.CONTROL_MAX_CHILDREN) return false;
    if (canvasH > 0 && bounds.height >= canvasH * 0.25) return false;

    var names = group.name + ' ' + analysis.names;
    if (!_nameHas(names, /toggle|checkbox|checkmark|mark|tg|tmptg|tglb|勾选|复选|勾|选中|开关/)) {
        return false;
    }
    return analysis.visualCount <= 6 && analysis.textCount <= 2;
}

function _isInputPattern(group, canvasW, canvasH) {
    var bounds = _getLayerBounds(group);
    var analysis = _analyzeGroupChildren(group);
    if (!bounds || !analysis.hasImage || analysis.totalVisible > _CONFIG.CONTROL_MAX_CHILDREN) return false;
    if (canvasH > 0 && bounds.height >= canvasH * 0.25) return false;

    var names = group.name + ' ' + analysis.names;
    if (!_nameHas(names, /input|ipt|tmpipt|placeholder|ipttxt|field|search|输入框|输入|文本框|搜索框|占位/)) {
        return false;
    }
    return analysis.textCount <= 2;
}

function _isDropdownPattern(group, canvasW, canvasH) {
    var bounds = _getLayerBounds(group);
    var analysis = _analyzeGroupChildren(group);
    if (!bounds || !analysis.hasImage || analysis.totalVisible > _CONFIG.CONTROL_MAX_CHILDREN) return false;
    if (canvasH > 0 && bounds.height >= canvasH * 0.30) return false;

    var names = group.name + ' ' + analysis.names;
    if (!_nameHas(names, /dropdown|dpd|tmpdpd|arrow|dpdicon|dpdlb|下拉|箭头|选项/)) {
        return false;
    }
    return analysis.textCount <= 2 || _nameHas(names, /arrow|dpdicon|箭头/);
}

function _isScrollViewPattern(group, canvasW, canvasH) {
    var bounds = _getLayerBounds(group);
    var analysis = _analyzeGroupChildren(group);
    if (!bounds || analysis.totalVisible < 1) return false;

    var names = group.name + ' ' + analysis.names;
    if (!_nameHas(names, /scroll|scrollview|sv|viewport|vpt|hbar|vbar|滚动|列表|视口|滑动条/)) {
        return false;
    }
    return analysis.subGroupCount >= 1 || _nameHas(names, /viewport|vpt|hbar|vbar|视口|滑动条/);
}

/**
 * 按钮结构验证（v3.3 改进版）：
 *  路线A（标准按钮）:
 *    - 至少 1 个图像子层 + 至少 1 个文字子层，且文字内容非空
 *    - 图像数 <= textCount*4 （放宽：支持三态按钮 normal/pressed/disabled + 底图）
 *  路线B（图标按钮）:
 *    - 组名含 btn/按钮/close/关闭 等按钮关键词
 *    - 1-3 个图像子层，0 个文字
 *  共同约束:
 *    - 宽度 < 画布的 BTN_MAX_WIDTH_RATIO
 *    - 高度 < 画布高的 30%
 *    - 总可见子层 <= 8
 *    - 子组 <= 1
 */
function _isButtonPattern(group, canvasW, canvasH) {
    var analysis = _analyzeGroupChildren(group);

    // 宽高限制（共同约束）
    var bounds = _getLayerBounds(group);
    if (!bounds || bounds.width <= 0) return false;
    if (_hasNonButtonGroupName(group.name)) return false;
    if (bounds.width >= canvasW * _CONFIG.BTN_MAX_WIDTH_RATIO) return false;
    if (canvasH > 0 && bounds.height >= canvasH * 0.30) return false;
    if (analysis.totalVisible > 8) return false;
    if (analysis.subGroupCount > 1) return false;

    var names = group.name + ' ' + analysis.names;
    var hasButtonName = _hasButtonSemanticName(names);

    // 路线A: 有图 + 有文字
    if (analysis.hasImage && analysis.hasText && analysis.textContent) {
        if (analysis.imageCount < _CONFIG.BTN_MIN_IMAGE_CHILDREN) return false;
        if (analysis.imageCount > analysis.textCount * 4) return false;
        if (hasButtonName) return true;
        // 未命名按钮通常是短文本 + 少量视觉层；避免把复杂卡片/列表项误判成按钮。
        return analysis.textCount <= 2 && analysis.visualCount <= 5 &&
            _isActionButtonText(analysis.textContent);
    }

    // 路线B: 图标按钮（无文字，但组名暗示是按钮）
    if (analysis.hasImage && !analysis.hasText) {
        if (hasButtonName) {
            if (analysis.visualCount >= 1 && analysis.visualCount <= 3) return true;
        }
    }

    // 路线C: 按钮背景组 + 文本被拆为兄弟层，而不是组内子层。
    if (analysis.hasImage && !analysis.hasText && _looksLikeButtonRect(bounds, canvasW, canvasH)) {
        if (hasButtonName) return true;
        var nearbyText = _getNearbyTextInfo(group, { expandX: 64, expandY: 96, maxLen: 12 });
        if (nearbyText && _isActionButtonText(nearbyText.text)) return true;
    }

    return false;
}

/**
 * 完整分类（用于顶层/非组件内部的图层）
 * @returns "txt" | "btn" | "slider" | "toggle" | "input" | "dropdown" | "scrollview" | "bg" | "icon" | "img" | "group"
 */
function _classifyLayer(layer, canvasW, canvasH) {
    var explicitClass = _classifyByExplicitTag(layer.name);
    if (explicitClass) return explicitClass;

    // ── 文本 -> txt ──
    if (layer.typename === 'ArtLayer') {
        try { if (layer.kind === LayerKind.TEXT) return 'txt'; } catch (e) { }
    }

    var bounds = _getLayerBounds(layer);
    if (!bounds || bounds.width <= 0 || bounds.height <= 0) return 'group';
    var w = bounds.width, h = bounds.height;

    // ── 组 -> 组件检测 ──
    if (layer.typename === 'LayerSet') {
        if (_looksLikeEmptyStructuralGroup(layer)) return 'group';
        // 显式控件优先，避免被按钮/滑块的宽泛结构吞掉
        if (_isScrollViewPattern(layer, canvasW, canvasH)) return 'scrollview';
        if (_isDropdownPattern(layer, canvasW, canvasH)) return 'dropdown';
        if (_isInputPattern(layer, canvasW, canvasH)) return 'input';
        if (_isTogglePattern(layer, canvasW, canvasH)) return 'toggle';
        // 滑块优先于按钮（结构特征更明确）
        if (_isSliderPattern(layer)) return 'slider';
        // 按钮（v3.2 传入canvasH）
        if (_isButtonPattern(layer, canvasW, canvasH)) return 'btn';
        // 其他 -> 透明组
        return 'group';
    }

    // ── 普通图层 ──
    if (_hasButtonSemanticName(layer.name) && !_hasNegativeControlName(layer.name) &&
        w >= 24 && h >= 20 &&
        !(canvasW > 0 && w >= canvasW * 0.65) &&
        !(canvasH > 0 && h >= canvasH * 0.18)) {
        return 'btn';
    }

    if (!_hasNegativeControlName(layer.name) && _looksLikeButtonRect(bounds, canvasW, canvasH)) {
        var nearbyText = _getNearbyTextInfo(layer, { expandX: 64, expandY: 96, maxLen: 12 });
        if (nearbyText && _isActionButtonText(nearbyText.text)) return 'btn';
    }

    var canvasArea = canvasW * canvasH;
    if (canvasArea > 0 && (w * h) / canvasArea > _CONFIG.BG_AREA_RATIO) return 'bg';
    if (Math.max(w, h) < _CONFIG.ICON_MAX_PX) return 'icon';
    return 'img';
}

/**
 * 基础分类（用于组件内部的子层）
 * 组件内部不产生嵌套组件：只返回 txt / img / group
 */
function _classifyLayerBasic(layer) {
    if (layer.typename === 'ArtLayer') {
        try { if (layer.kind === LayerKind.TEXT) return 'txt'; } catch (e) { }
        return 'img';
    }
    if (layer.typename === 'LayerSet') return 'group';
    return 'img';
}

// ╔══════════════════════════════════════════╗
// ║       命 名 规 则 映 射                   ║
// ╚══════════════════════════════════════════╝

/**
 * 分类 -> 资源类型前缀
 */
function _getTypePrefix(classification) {
    switch (classification) {
        case 'txt':    return 'txt';
        case 'btn':    return 'btn';
        case 'slider': return 'progress';
        case 'toggle': return 'toggle';
        case 'input': return 'input';
        case 'dropdown': return 'dropdown';
        case 'scrollview': return 'scroll';
        case 'bg':     return 'img';
        case 'icon':   return 'icon';
        case 'img':    return 'img';
        default:       return 'img';
    }
}

/**
 * 分类 -> UGUI 插件标签后缀
 */
function _getPluginTag(classification) {
    switch (classification) {
        case 'txt':    return '.txt';
        case 'btn':    return '.bt';
        case 'slider': return '.sld';
        case 'toggle': return '.tg';
        case 'input': return '.ipt';
        case 'dropdown': return '.dpd';
        case 'scrollview': return '.sv';
        default:       return '.img';
    }
}

// ╔══════════════════════════════════════════╗
// ║  智 能 命 名 引 擎  v3.1 ($ + 翻译)     ║
// ╚══════════════════════════════════════════╝

function _buildFingerprint(layer, classification, bounds) {
    if (!bounds || classification === 'txt' || classification === 'group' || classification === 'bg')
        return null;
    if (_isClippingMask(layer)) return null;
    var analysis = _analyzeLayerProcessing(layer);
    if (analysis.hasEffect || analysis.hasMask || analysis.hasNonNormalBlend ||
        analysis.hasReducedOpacity || analysis.hasReducedFillOpacity) return null;
    return classification + '_' + Math.round(bounds.width) + 'x' + Math.round(bounds.height);
}

/**
 * 推断描述片段（v3.3 改进版）
 *
 * 优先级:
 *  1. txt 数值 -> 用父组名上下文（不再统一返回 'num'）
 *  2. txt -> 翻译文本内容
 *  3. btn/slider -> 翻译子文本
 *  4. 原名有意义 -> 翻译/sanitize
 *  5. 父组名
 *  6. 空 -> 纯序号
 *
 * @param {Layer}  layer
 * @param {String} classification
 * @param {String} moduleName  — 用于去重：如 desc==module 则丢弃
 */
function _inferDesc(layer, classification, moduleName) {
    // --- 内部：尝试从父组获取上下文 ---
    function _parentContext() {
        try {
            var p = layer.parent;
            if (p && p.typename === 'LayerSet' && !_isGenericName(p.name)) {
                var pt = _translateText(p.name);
                if (pt) return pt;
                pt = _sanitizeText(p.name, 10);
                if (pt) return pt;
            }
        } catch (e) { }
        return '';
    }

    function _semanticTextDesc(text) {
        var trimmed = (text || '').replace(/[\r\n]+/g, ' ').replace(/^\s+|\s+$/g, '');
        if (!trimmed) return '';
        if (/^[\+\-]?[\u00a5\$\u20ac\u00a3]?[\d\,\.]+[\%]?$/.test(trimmed)) {
            var numericCtx = _parentContext();
            if (numericCtx && numericCtx !== moduleName) return numericCtx;
            return 'num';
        }
        if (/[\$\u00A5\u20AC\u00A3]/.test(trimmed) || /\d+\s*元/.test(trimmed)) return 'price';
        if (/\d+\s*\/\s*\d+/.test(trimmed)) return 'count';
        if (/[Ll]v[\.\s]*\d/.test(trimmed) || /等级/.test(trimmed)) return 'level';
        if (/\d+\s*%/.test(trimmed)) return 'progress';
        if (/[\r\n，,。；;：:、]/.test(text) || trimmed.length > 18) return 'desc';
        if (/标题|title|名称|名字|name/.test(trimmed)) return 'title';
        if (/描述|说明|详情|介绍|content|desc/.test(trimmed)) return 'desc';
        if (/数量|数值|价格|等级|经验|血量|体力|战力|count|price|level|exp|hp|power/i.test(trimmed)) return _translateText(trimmed) || 'value';
        if (trimmed.length <= 8) return 'label';
        return 'label';
    }

    function _nearbyTextContent() {
        var info = _getNearbyTextInfo(layer, { expandX: 64, expandY: 96, maxLen: 12 });
        return info ? info.text : '';
    }

    // 1. 文本图层 -> 翻译其内容
    if (classification === 'txt') {
        try {
            var textContent = layer.textItem.contents;
            var semantic = _semanticTextDesc(textContent);
            if (semantic) return semantic;
        } catch (e) { }
    }

    // 2. 组件 -> 优先用子文本/相邻文本表达功能语义
    if (_isComponentClass(classification)) {
        var childText = '';
        if (layer.typename === 'LayerSet') {
            childText = _getFirstTextContent(layer);
        } else if (classification === 'btn') {
            childText = _nearbyTextContent();
        }
        if (childText) {
            var t = _translateText(childText);
            if (t) return t;
        }
    }

    // 3. 原名有意义 -> 尝试翻译
    if (!_isGenericName(layer.name) && !_hasPluginTag(layer.name)) {
        var translated2 = _translateText(layer.name);
        if (translated2) return translated2;
        var cleaned = _sanitizeText(layer.name, 15);
        if (cleaned) return cleaned;
    }

    // 4. 父组名
    var pd = _parentContext();
    if (pd) return pd;

    // 5. 空
    return '';
}

/**
 * 构建完整图层名
 *
 * === 命 名 规 则 ===
 *   组件:   [module]_[type]_[desc]_[seq][.tag]
 *   资源:   [module]_[type]_[desc]_[seq][.tag]
 *
 * 绑定意图不再写入 @ 前缀，而是写入 manifest/PsdLayerMarker。
 *
 * 例如:
 *    home_btn_login_01.bt      <- 按钮组件 "登录"
 *    home_progress_vol_01.sld  <- 滑块组件
 *    home_txt_title_01.txt     <- 普通文本（需要绑定时在原 PSD 名加 #bind）
 *    home_img_bg_01.img        <- 背景
 *    home_icon_mail_01.img     <- 图标
 */
function _buildLayerName(info, moduleName, seqCounters) {
    if (info.lockedName) return info.lockedName;

    var isComp = info.isComponent;
    var shouldBind = info.shouldBind === true;
    var typePrefix = _getTypePrefix(info.classification);
    var tag = _getPluginTag(info.classification);
    var desc = _inferDesc(info.layer, info.classification, moduleName);

    // 去冗余：desc 不应重复 module 或 type 前缀
    if (desc === moduleName || desc === typePrefix) desc = '';

    // 序号 key: 同类型统一计数；绑定意图由 manifest 记录，不污染最终图层名。
    var seqKey = moduleName + '_' + typePrefix;
    if (!seqCounters[seqKey]) seqCounters[seqKey] = 0;
    seqCounters[seqKey]++;
    var seq = seqCounters[seqKey];
    var seqStr = (seq < 10) ? ('0' + seq) : ('' + seq);

    var name;
    if (desc) {
        name = moduleName + '_' + typePrefix + '_' + desc + '_' + seqStr + tag;
    } else {
        name = moduleName + '_' + typePrefix + '_' + seqStr + tag;
    }
    return name;
}

function _getLayerId(layer) {
    try {
        if (typeof layer.id !== 'undefined') return Number(layer.id);
    } catch (e) { }
    return 0;
}

function _escapePathPart(name) {
    return String(name || '').replace(/\//g, '_').replace(/^\s+|\s+$/g, '');
}

function _countFlatIndexSlots(layer) {
    if (!layer) return 0;
    if (layer.typename !== 'LayerSet') return 1;
    var total = 1;
    try {
        for (var i = 0; i < layer.layers.length; i++) {
            var child = layer.layers[i];
            if (_CONFIG.SKIP_HIDDEN_LAYERS && !child.visible) continue;
            total += _countFlatIndexSlots(child);
        }
    } catch (e) { }
    return total;
}

function _getSuggestedPropFromName(name, moduleName, classification) {
    var raw = String(name || '');
    raw = raw.replace(/^@/, '').replace(/^@ref:/i, '');
    raw = raw.replace(/\.[a-zA-Z0-9_]+$/i, '');
    raw = raw.replace(/#nobind\b|!bind\b|\[nobind\]/ig, '');
    raw = raw.replace(/#bind\b|\[bind\]/ig, '');

    var typePrefix = _getTypePrefix(classification);
    var parts = raw.split(/[_\-\s]+/);
    var kept = [];
    for (var i = 0; i < parts.length; i++) {
        var p = parts[i];
        if (!p) continue;
        if (i === 0 && moduleName && p.toLowerCase() === moduleName.toLowerCase()) continue;
        if (p.toLowerCase() === typePrefix.toLowerCase()) continue;
        if (/^\d+$/.test(p)) continue;
        kept.push(p);
    }

    if (!kept.length) kept.push(typePrefix || 'node');

    var out = '';
    for (var j = 0; j < kept.length; j++) {
        var part = kept[j].replace(/[^a-zA-Z0-9]/g, '');
        if (!part) continue;
        out += part.length === 1 ? part.toUpperCase() : part.charAt(0).toUpperCase() + part.substring(1);
    }
    if (!out) out = 'Node';
    if (/^\d/.test(out)) out = 'Node' + out;
    return out;
}

/**
 * 递归收集图层信息（v3.1 层级感知）
 *
 * @param {LayerSets|Layers}  layers
 * @param {Array}             infos          - 输出数组
 * @param {Number}            canvasW
 * @param {Number}            canvasH
 * @param {Boolean}           insideComponent - true = 当前在组件内部，禁止嵌套组件
 */
function _collectForRename(layers, infos, canvasW, canvasH, insideComponent, state, parentPath) {
    if (!state) state = { nextFlatIndex: 0 };
    parentPath = parentPath || '';

    for (var i = 0; i < layers.length; i++) {
        var layer = layers[i];
        if (_CONFIG.SKIP_HIDDEN_LAYERS && !layer.visible) continue;

        var slotCount = _countFlatIndexSlots(layer);
        var flatIndex = layer.typename === 'LayerSet'
            ? state.nextFlatIndex + slotCount - 1
            : state.nextFlatIndex;
        var originalName = layer.name;
        var layerPath = parentPath ? (parentPath + '/' + _escapePathPart(originalName)) : _escapePathPart(originalName);

        // ── 分类 ──
        var classification;
        if (insideComponent) {
            classification = _classifyLayerBasic(layer);
        } else {
            classification = _classifyLayer(layer, canvasW, canvasH);
        }

        var bounds = _getLayerBounds(layer);
        var isComp = !insideComponent && _isComponentClass(classification);
        var confidence = _getClassificationConfidence(layer, classification, canvasW, canvasH, insideComponent);
        var scoring = _loadScoringConfig();
        var explicitBind = _hasExplicitBindIntent(originalName);
        var explicitNoBind = _hasExplicitNoBindIntent(originalName);
        var shouldBind = !insideComponent && classification !== 'group' && confidence >= scoring.bindThreshold &&
            (explicitBind || (isComp && _shouldBindByDefault(layer, classification)));
        if (explicitNoBind) shouldBind = false;
        var fingerprint = _buildFingerprint(layer, classification, bounds);

        _DEBUG_LOG.push('[COLLECT] "' + layer.name + '" cls=' + classification
            + ' comp=' + isComp + ' bind=' + shouldBind + ' confidence=' + confidence + ' flat=' + flatIndex + ' inside=' + insideComponent
            + ' type=' + layer.typename
            + (bounds ? ' ' + Math.round(bounds.width) + 'x' + Math.round(bounds.height) : ' noBounds'));

        infos.push({
            layer: layer,
            classification: classification,
            bounds: bounds,
            fingerprint: fingerprint,
            layerId: _getLayerId(layer),
            flatIndex: flatIndex,
            path: layerPath,
            originalName: originalName,
            confidence: confidence,
            isComponent: isComp,
            shouldBind: shouldBind,
            issues: confidence < scoring.reviewThreshold ? ['low_confidence'] : [],
            newName: null
        });

        // ── 递归 ──
        if (layer.typename === 'LayerSet') {
            var childState = { nextFlatIndex: state.nextFlatIndex + 1 };
            if (isComp) {
                // 组件组(btn/sld): 子层标记 insideComponent=true
                _collectForRename(layer.layers, infos, canvasW, canvasH, true, childState, layerPath);
            } else if (classification === 'group') {
                // 普通组: 保持当前 insideComponent 状态
                _collectForRename(layer.layers, infos, canvasW, canvasH, insideComponent, childState, layerPath);
            }
            // img/bg/icon 分类的组不递归
        }
        state.nextFlatIndex += slotCount;
    }
}

function _getManifestBaseName(doc, targetFile) {
    if (targetFile) {
        try {
            return _stripExportSuffix(targetFile.name.replace(/\.[^.]+$/, ''));
        } catch (te) { }
    }

    try {
        return _stripExportSuffix(doc.name.replace(/\.[^.]+$/, ''));
    } catch (e) {
        return 'ui';
    }
}

function _stripExportSuffix(baseName) {
    baseName = String(baseName || '');
    if (_CONFIG.EXPORT_SUFFIX) {
        var escapedSuffix = _CONFIG.EXPORT_SUFFIX.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
        baseName = baseName.replace(new RegExp(escapedSuffix + '$', 'i'), '');
    }
    return baseName.replace(/_UGUI$/i, '');
}

function _loadManifestLocks(doc) {
    var locks = { byId: {}, byPath: {} };
    try {
        var docFolder;
        try { docFolder = doc.path; } catch (e1) { docFolder = Folder.myDocuments; }
        var baseName = _getManifestBaseName(doc);
        var files = [
            new File(docFolder + '/' + baseName + '.ui_manifest.json'),
            new File(docFolder + '/' + baseName + '_binding.json')
        ];

        for (var f = 0; f < files.length; f++) {
            var manifest = _readJsonFile(files[f]);
            if (!manifest) continue;
            var layers = manifest.layers || manifest.components || [];
            for (var i = 0; i < layers.length; i++) {
                var entry = layers[i];
                var locked = entry.lockedName || entry.proposedName || entry.name;
                if (!locked) continue;
                var id = entry.id || entry.layerId || 0;
                if (id) locks.byId[id] = locked;
                if (entry.path) locks.byPath[entry.path] = locked;
            }
        }
    } catch (e) {
        _DEBUG_LOG.push('[LOCKS-ERROR] ' + e.message);
    }
    return locks;
}

function _applyManifestLocks(doc, infos) {
    var locks = _loadManifestLocks(doc);
    for (var i = 0; i < infos.length; i++) {
        var info = infos[i];
        var locked = '';
        if (info.layerId && locks.byId[info.layerId]) locked = locks.byId[info.layerId];
        if (!locked && info.path && locks.byPath[info.path]) locked = locks.byPath[info.path];
        if (locked) {
            var expectedTag = _getPluginTag(info.classification).replace(/^\./, '').toLowerCase();
            var lockedTag = _getPluginTagWithoutDot(locked);
            if (lockedTag && lockedTag !== expectedTag) {
                _DEBUG_LOG.push('[LOCKED-NAME-SKIP] ' + info.originalName + ' locked=' + locked + ' expected=.' + expectedTag);
                continue;
            }
            if (!lockedTag && !/^@ref:/i.test(locked)) {
                _DEBUG_LOG.push('[LOCKED-NAME-SKIP] ' + info.originalName + ' locked=' + locked + ' reason=no-plugin-tag');
                continue;
            }
            info.lockedName = locked;
            _DEBUG_LOG.push('[LOCKED-NAME] ' + info.originalName + ' -> ' + locked);
        }
    }
}

/**
 * 主命名引擎 (v3.1)
 * @returns {{ renamed: Number, stats: Object }}
 */
function _smartRenameAllLayers(doc, moduleName) {
    var canvasW = doc.width.as('px');
    var canvasH = doc.height.as('px');

    // Phase 1: 收集（层级感知）
    var infos = [];
    _collectForRename(doc.layers, infos, canvasW, canvasH, false);
    _applyManifestLocks(doc, infos);

    // Phase 2: 指纹分组（组件不参与去重）
    var fpGroups = {};
    for (var i = 0; i < infos.length; i++) {
        var fp = infos[i].fingerprint;
        if (!fp || infos[i].isComponent) continue;
        if (!fpGroups[fp]) fpGroups[fp] = [];
        fpGroups[fp].push(infos[i]);
    }

    // Phase 3: 指纹组中，优先选有好名字的做 canonical
    for (var fp in fpGroups) {
        var group = fpGroups[fp];
        if (group.length <= 1) continue;
        var canonIdx = 0;
        for (var j = 0; j < group.length; j++) {
            if (!_isGenericName(group[j].layer.name)) { canonIdx = j; break; }
        }
        if (canonIdx !== 0) {
            var tmp = group[0]; group[0] = group[canonIdx]; group[canonIdx] = tmp;
        }
    }

    // Phase 4: 生成命名
    var seqCounters = {};
    var usedNames = {};
    var stats = { txt: 0, btn: 0, slider: 0, toggle: 0, input: 0, dropdown: 0, scrollview: 0, bg: 0, icon: 0, img: 0, ref: 0, kept: 0 };

    for (var i = 0; i < infos.length; i++) {
        var info = infos[i];
        if (info.classification === 'group') continue;

        var layerName = info.layer.name;
        var hasTag = _hasPluginTag(layerName);

        // 已有插件标签的非组件层一般保留；但纯符号/形状类无效名即使带 .img 也必须重命名。
        if (hasTag && !info.isComponent && !_isGenericName(layerName) &&
            !(info.classification === 'txt' && _isGenericTextLayerName(layerName))) {
            stats.kept++;
            _DEBUG_LOG.push('[KEPT-tag] ' + layerName + ' cls=' + info.classification);
            continue;
        }

        // 去重检测（仅非组件资源）
        if (!info.isComponent && _CONFIG.MARK_DUPLICATES_REF && info.fingerprint) {
            var group = fpGroups[info.fingerprint];
            if (group && group.length > 1 && group[0] !== info) {
                if (group[0].newName) {
                    info.newName = '@ref:' + group[0].newName;
                    stats.ref++;
                    _DEBUG_LOG.push('[REF] ' + info.layer.name + ' -> ' + info.newName);
                    continue;
                }
            }
        }

        // 只改通用名模式（组件始终改名以确保类型标签和 manifest 绑定语义一致）
        // 纯数字文本层也始终改名（txt分类 + 层名像number）
        var isNumericTxt = (info.classification === 'txt' && _isNumericLayerName(info.layer.name));
        if (!info.isComponent && info.classification !== 'txt' && !isNumericTxt && _CONFIG.RENAME_ONLY_GENERIC && !_isGenericName(info.layer.name)) {
            stats.kept++;
            _DEBUG_LOG.push('[KEPT-nongeneric] ' + info.layer.name + ' cls=' + info.classification + ' comp=' + info.isComponent);
            if (info.fingerprint) {
                var grp = fpGroups[info.fingerprint];
                if (grp && grp[0] === info) {
                    var origName = info.layer.name;
                    if (!_hasPluginTag(origName)) origName = origName + _getPluginTag(info.classification);
                    info.newName = origName;
                }
            }
            continue;
        }

        // 构建名字
        var baseName = _buildLayerName(info, moduleName, seqCounters);

        // 确保唯一
        var finalName = baseName;
        var sfx = 2;
        while (usedNames[finalName]) {
            var tag = _getPluginTag(info.classification);
            var bare = finalName;
            var tagIdx = finalName.lastIndexOf(tag);
            if (tagIdx > 0) bare = finalName.substring(0, tagIdx);
            finalName = bare + '_' + sfx + tag;
            sfx++;
        }
        usedNames[finalName] = true;
        info.newName = finalName;
        _DEBUG_LOG.push('[RENAME] ' + info.layer.name + ' -> ' + finalName + ' cls=' + info.classification + ' comp=' + info.isComponent);

        // 统计
        if (info.classification === 'slider') stats.slider++;
        else if (stats.hasOwnProperty(info.classification)) stats[info.classification]++;
    }

    // Phase 5: 回填 @ref
    for (var fp in fpGroups) {
        var group = fpGroups[fp];
        if (group.length <= 1 || !group[0].newName) continue;
        for (var j = 1; j < group.length; j++) {
            if (!group[j].newName) {
                group[j].newName = '@ref:' + group[0].newName;
                stats.ref++;
            }
        }
    }

    // Phase 6: 应用
    var renamed = 0;
    for (var i = 0; i < infos.length; i++) {
        if (!infos[i].newName || infos[i].newName === infos[i].layer.name) continue;
        try {
            infos[i].layer.name = infos[i].newName;
            renamed++;
        } catch (e) { }
    }

    return { renamed: renamed, stats: stats, infos: infos };
}

// ╔══════════════════════════════════════════╗
// ║         特 效 检 测                      ║
// ╚══════════════════════════════════════════╝

var _PLUGIN_FX_IDS = null;
var _IMAGE_PROCESS_LOG = [];
var _LAYER_ANALYSIS_CACHE = {};
var _IMAGE_RENDER_DOC = null;
var _IMAGE_RENDER_SOURCE = null;

function _disposeImageRenderDocument() {
    var sourceDoc = _IMAGE_RENDER_SOURCE;
    try { if (_IMAGE_RENDER_DOC) _IMAGE_RENDER_DOC.close(SaveOptions.DONOTSAVECHANGES); } catch (e) { }
    _IMAGE_RENDER_DOC = null;
    _IMAGE_RENDER_SOURCE = null;
    try { if (sourceDoc) app.activeDocument = sourceDoc; } catch (restoreError) { }
}

function _getImageRenderDocument(sourceDoc) {
    if (_IMAGE_RENDER_DOC && _IMAGE_RENDER_SOURCE === sourceDoc) return _IMAGE_RENDER_DOC;
    _disposeImageRenderDocument();
    _IMAGE_RENDER_SOURCE = sourceDoc;
    _IMAGE_RENDER_DOC = app.documents.add(
        sourceDoc.width, sourceDoc.height, sourceDoc.resolution,
        '__PSD2UI_RENDER__', NewDocumentMode.RGB, DocumentFill.TRANSPARENT);
    try { _IMAGE_RENDER_DOC.bitsPerChannel = sourceDoc.bitsPerChannel; } catch (bitDepthError) { }
    try {
        if (_IMAGE_RENDER_DOC.colorProfileName !== sourceDoc.colorProfileName) {
            _IMAGE_RENDER_DOC.convertProfile(sourceDoc.colorProfileName,
                Intent.RELATIVECOLORIMETRIC, true, false);
        }
    } catch (profileError) { }
    return _IMAGE_RENDER_DOC;
}

function _getLayerDebugPath(layer) {
    var parts = [];
    var current = layer;
    try {
        while (current && current.typename !== 'Document') {
            parts.unshift(String(current.name || '<unnamed>'));
            current = current.parent;
        }
    } catch (e) { }
    return parts.join('/');
}

function _recordImageProcess(layer, action, detail, severity) {
    var entry = {
        layerId: (layer && layer._psd2uiCaptured) ? layer.layerId : 0,
        name: (layer && layer._psd2uiCaptured) ? layer.name : '',
        path: (layer && layer._psd2uiCaptured) ? layer.path : '',
        action: action || '',
        detail: detail || '',
        severity: severity || 'info'
    };
    if (!layer || !layer._psd2uiCaptured) {
        try { entry.layerId = layer.id || 0; } catch (e1) { }
        try { entry.name = String(layer.name || ''); } catch (e2) { }
        entry.path = _getLayerDebugPath(layer);
    }
    _IMAGE_PROCESS_LOG.push(entry);
    _DEBUG_LOG.push('[IMAGE-' + String(entry.severity).toUpperCase() + '] ' + entry.path +
        ' -> ' + entry.action + (entry.detail ? ' (' + entry.detail + ')' : ''));
    return entry;
}

function _captureImageLayerInfo(layer) {
    var info = { _psd2uiCaptured: true, layerId: 0, name: '', path: '' };
    try { info.layerId = layer.id || 0; } catch (e1) { }
    try { info.name = String(layer.name || ''); } catch (e2) { }
    info.path = _getLayerDebugPath(layer);
    return info;
}

function _getBlendModeName(layer) {
    try {
        return String(layer.blendMode || '')
            .replace(/^BlendMode\./i, '')
            .toLowerCase();
    } catch (e) {
        return '';
    }
}

function _isSafeIndependentBlend(layer, isGroup) {
    var mode = _getBlendModeName(layer);
    if (!mode || mode === 'normal') return true;
    return isGroup && (mode === 'passthrough' || mode === 'pass_through');
}

function _getPluginFxIDs() {
    if (!_PLUGIN_FX_IDS) {
        _PLUGIN_FX_IDS = [
            app.charIDToTypeID('DrSh'),
            app.charIDToTypeID('OrGl'),
            app.charIDToTypeID('FrFX'),
            app.charIDToTypeID('SoFi'),
            app.charIDToTypeID('ClrO')
        ];
    }
    return _PLUGIN_FX_IDS;
}

var _ALL_FX_IDS = null;
function _getAllFxIDs() {
    if (!_ALL_FX_IDS) {
        _ALL_FX_IDS = [
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
    return _ALL_FX_IDS;
}

function _getBool(desc, keyID) {
    try { if (desc.hasKey(keyID)) return desc.getBoolean(keyID); } catch (e) { }
    return null;
}

function _getFirstBool(desc, keyIDs) {
    for (var i = 0; i < keyIDs.length; i++) {
        var v = _getBool(desc, keyIDs[i]);
        if (v !== null) return v;
    }
    return null;
}

function _fxEntryEnabled(effectDesc) {
    var en = _getFirstBool(effectDesc, [
        app.charIDToTypeID('enab'),
        app.stringIDToTypeID('enabled')
    ]);
    return (en !== null) ? en : true;
}

function _analyzeLayerProcessing(layer) {
    var cacheKey = '';
    try { cacheKey = String(layer.id || ''); } catch (cacheIdError) { }
    if (cacheKey && _LAYER_ANALYSIS_CACHE.hasOwnProperty(cacheKey)) {
        return _LAYER_ANALYSIS_CACHE[cacheKey];
    }

    var isGroup = false;
    try { isGroup = layer.typename === 'LayerSet'; } catch (e0) { }
    var result = {
        hasEffect: false,
        hasExoticEffect: false,
        hasMask: false,
        hasUserMask: false,
        hasVectorMask: false,
        hasNonNormalBlend: !_isSafeIndependentBlend(layer, isGroup),
        blendMode: _getBlendModeName(layer),
        opacity: 100,
        fillOpacity: 100,
        hasReducedOpacity: false,
        hasReducedFillOpacity: false
    };
    try {
        result.opacity = Number(layer.opacity);
        result.hasReducedOpacity = result.opacity < 99.999;
    } catch (eOpacity) { }
    if (!isGroup) {
        try {
            result.fillOpacity = Number(layer.fillOpacity);
            result.hasReducedFillOpacity = result.fillOpacity < 99.999;
        } catch (eFillOpacity) { }
    }
    try {
        var ref = new ActionReference();
        ref.putIdentifier(app.charIDToTypeID('Lyr '), layer.id);
        var desc = executeActionGet(ref);

        var keyLefx = app.charIDToTypeID('Lefx');
        if (desc.hasKey(keyLefx)) {
            var fxVis = _getBool(desc, app.stringIDToTypeID('layerFXVisible'));
            if (fxVis !== false) {
                var efxDesc = desc.getObjectValue(keyLefx);
                var rootEn = _getFirstBool(efxDesc, [app.charIDToTypeID('enab'), app.stringIDToTypeID('enabled')]);
                if (rootEn !== false) {
                    var allIDs = _getAllFxIDs();
                    var handledIDs = _getPluginFxIDs();
                    for (var i = 0; i < allIDs.length; i++) {
                        var eid = allIDs[i];
                        if (!efxDesc.hasKey(eid)) continue;
                        var isEnabled = false;
                        try {
                            var vt = efxDesc.getType(eid);
                            if (vt === DescValueType.OBJECTTYPE) {
                                isEnabled = _fxEntryEnabled(efxDesc.getObjectValue(eid));
                            } else if (vt === DescValueType.LISTTYPE) {
                                var lst = efxDesc.getList(eid);
                                for (var li = 0; li < lst.count; li++) {
                                    try {
                                        if (lst.getType(li) === DescValueType.OBJECTTYPE &&
                                            _fxEntryEnabled(lst.getObjectValue(li))) {
                                            isEnabled = true; break;
                                        }
                                    } catch (e3) { }
                                }
                            }
                        } catch (e2) {
                            try { isEnabled = _fxEntryEnabled(efxDesc.getObjectValue(eid)); } catch (e4) { }
                        }
                        if (!isEnabled) continue;
                        result.hasEffect = true;
                        var isHandled = false;
                        for (var h = 0; h < handledIDs.length; h++) {
                            if (eid === handledIDs[h]) { isHandled = true; break; }
                        }
                        if (!isHandled) result.hasExoticEffect = true;
                    }
                }
            }
        }

        var hasUM = _getBool(desc, app.stringIDToTypeID('hasUserMask'));
        var umEn = _getFirstBool(desc, [app.stringIDToTypeID('userMaskEnabled'), app.charIDToTypeID('UsrM')]);
        if ((hasUM === true || hasUM === null) && umEn === true) {
            result.hasMask = true;
            result.hasUserMask = true;
        }

        var hasVM = _getBool(desc, app.stringIDToTypeID('hasVectorMask'));
        var vmEn = _getBool(desc, app.stringIDToTypeID('vectorMaskEnabled'));
        if ((hasVM === true || hasVM === null) && vmEn === true) {
            result.hasMask = true;
            result.hasVectorMask = true;
        }
    } catch (e) { }
    if (cacheKey) _LAYER_ANALYSIS_CACHE[cacheKey] = result;
    return result;
}

function _hasLayerEffect(layer) {
    var a = _analyzeLayerProcessing(layer);
    return a.hasEffect || a.hasMask;
}

/**
 * 判断图层名是否为组件（@ 前缀 + .bt/.sld 标签）
 * 用于 _processLayers 决定是否保留组结构
 */
function _isComponentLayerName(name) {
    if (!name) return false;
    return /\.(bt|tmpbt|sld|tg|tmptg|ipt|tmpipt|dpd|tmpdpd|sv)\s*$/i.test(name);
}

// ╔══════════════════════════════════════════╗
// ║   图 层 处 理  (v3.1 组件组保留结构)      ║
// ╚══════════════════════════════════════════╝

function _isClippingMask(layer) {
    try { return (layer.typename === 'ArtLayer') && layer.grouped; } catch (e) { return false; }
}

function _selectLayerForMerge(layer, addToSelection) {
    if (!layer) return false;
    var layerId = _getLayerId(layer);
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

function _findLayerById(layers, layerId) {
    if (!layers || !layerId) return null;
    for (var i = 0; i < layers.length; i++) {
        if (_getLayerId(layers[i]) === layerId) return layers[i];
    }
    return null;
}

function _mergeCompleteClippingChain(clippingLayers, baseLayer, layerOwner) {
    var result = { layer: null, layers: null, error: '' };
    try {
        if (!clippingLayers || !clippingLayers.length || !baseLayer || !layerOwner) {
            result.error = 'invalid clipping chain';
            return result;
        }

        // Photoshop 2025's Merge Selected can keep only the clipped member's
        // transparency even though it removes the opaque clipping base. The
        // exported pixels then fade to transparent and reveal unrelated layers
        // below. Converting the complete live chain to one Smart Object lets
        // Photoshop render the clipping result first; rasterizing that object
        // preserves both the clipped RGB and the base layer's alpha coverage.
        if (!_selectLayerForMerge(baseLayer, false)) {
            result.error = 'cannot select clipping base';
            return result;
        }
        for (var i = clippingLayers.length - 1; i >= 0; i--) {
            if (!_selectLayerForMerge(clippingLayers[i], true)) {
                result.error = 'cannot select clipping member';
                return result;
            }
        }

        executeAction(app.stringIDToTypeID('newPlacedLayer'), undefined, DialogModes.NO);
        var compositeLayer = app.activeDocument.activeLayer;
        if (!compositeLayer || compositeLayer.typename !== 'ArtLayer') {
            result.error = 'cannot create clipping composite smart object';
            return result;
        }
        compositeLayer.rasterize(RasterizeType.ENTIRELAYER);
        result.layers = layerOwner.layers;
        var activeLayerId = _getLayerId(app.activeDocument.activeLayer);
        result.layer = _findLayerById(result.layers, activeLayerId);
        if (!result.layer) result.error = 'cannot resolve merged layer from refreshed owner';
        return result;
    } catch (e) {
        result.error = e.message || String(e);
        try { result.layers = layerOwner.layers; } catch (refreshError) { }
        return result;
    }
}

function _hasClippingDependents(layer) {
    try {
        if (!layer || !layer.parent || !layer.parent.layers) return false;
        var siblings = layer.parent.layers;
        var layerId = _getLayerId(layer);
        for (var i = 0; i < siblings.length; i++) {
            var sameLayer = siblings[i] === layer;
            if (!sameLayer && layerId) sameLayer = _getLayerId(siblings[i]) === layerId;
            if (sameLayer) return i > 0 && _isClippingMask(siblings[i - 1]);
        }
    } catch (e) { }
    return false;
}

/**
 * Collapse every clipping chain before any base layer is replaced by the
 * Photoshop visual bake. Photoshop automatically clears `grouped` on the
 * layers above a clipping base when that base is removed, so doing this in
 * the normal bottom-to-top image pass silently destroys the chain.
 *
 * The scan deliberately finds the bottom-most clipping member first, then
 * expands upward to the complete contiguous chain. Photoshop 2025 must merge
 * [clip3, clip2, clip1, base] in one Merge Selected action; pairwise merges
 * leave the intermediate result grouped and silently release the upper chain.
 */
function _mergeClippingChainsBeforeBake(layers, stats) {
    if (!layers) return true;

    var layerOwner = null;
    try {
        if (layers.length > 0) layerOwner = layers[0].parent;
    } catch (ownerError) { }

    function currentLayers() {
        try {
            if (layerOwner && layerOwner.layers) return layerOwner.layers;
        } catch (refreshError) { }
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
            if (_isClippingMask(siblings[i])) {
                clippingLayer = siblings[i];
                clippingIndex = i;
                break;
            }
        }

        if (!clippingLayer) return true;

        var clippingPath = _getLayerDebugPath(clippingLayer);
        var baseLayer = clippingIndex + 1 < siblings.length ? siblings[clippingIndex + 1] : null;
        if (!baseLayer || baseLayer.typename !== 'ArtLayer' || _isClippingMask(baseLayer)) {
            var unsupportedBaseDetail = '剪贴蒙版底板不是可安全一次性合并的 ArtLayer';
            recordClippingError(unsupportedBaseDetail + ': ' + clippingPath);
            _recordImageProcess(_captureImageLayerInfo(clippingLayer),
                'merge_clipping_failed', unsupportedBaseDetail, 'error');
            return false;
        }

        var chainTopIndex = clippingIndex;
        while (chainTopIndex > 0 && _isClippingMask(siblings[chainTopIndex - 1])) {
            chainTopIndex--;
        }
        var clippingLayers = [];
        var clippingInfos = [];
        var clippingIds = [];
        var chainVisible = !!baseLayer.visible;
        for (var chainIndex = chainTopIndex; chainIndex <= clippingIndex; chainIndex++) {
            var chainLayer = siblings[chainIndex];
            clippingLayers.push(chainLayer);
            clippingInfos.push(_captureImageLayerInfo(chainLayer));
            clippingIds.push(_getLayerId(chainLayer));
            if (!chainLayer.visible) chainVisible = false;
        }

        if (!chainVisible) {
            // A previous clipping-chain merge can make Photoshop refresh the
            // visibility state of later siblings.  The initial hidden-layer
            // pass therefore is not always sufficient.  In skip-hidden mode,
            // retry the chain-safe removal against the current live sibling
            // collection before treating the PSD as unsafe.
            if (_CONFIG.SKIP_HIDDEN_LAYERS) {
                var hiddenChainRemoved = _removeHiddenLayers(siblings);
                if (hiddenChainRemoved > 0) {
                    stats.skipped += hiddenChainRemoved;
                    continue;
                }
            }
            var hiddenChainDetail = '保留隐藏层时不能安全压平含隐藏成员的剪贴链';
            recordClippingError(hiddenChainDetail + ': ' + clippingPath);
            _recordImageProcess(_captureImageLayerInfo(clippingLayer),
                'merge_clipping_failed', hiddenChainDetail, 'error');
            return false;
        }

        var baseName = String(baseLayer.name || '');
        var layerCountBeforeMerge = siblings.length;
        var mergeResult = _mergeCompleteClippingChain(clippingLayers, baseLayer, layerOwner);
        var mergedLayer = mergeResult.layer;
        var refreshedSiblings = mergeResult.layers || currentLayers();
        var mergedLayerCount = layerCountBeforeMerge - refreshedSiblings.length;
        var expectedMergedLayerCount = clippingLayers.length;
        var mergedLayerId = _getLayerId(mergedLayer);
        var staleClippingId = 0;
        for (var clipIdIndex = 0; clipIdIndex < clippingIds.length; clipIdIndex++) {
            // Photoshop 2025 may reuse the top clipping layer ID for the
            // merged result. That one ID is valid only when it resolves to the
            // refreshed active result; every other old clipping ID must vanish.
            if (clippingIds[clipIdIndex] !== mergedLayerId &&
                _findLayerById(refreshedSiblings, clippingIds[clipIdIndex])) {
                staleClippingId = clippingIds[clipIdIndex];
                break;
            }
        }
        if (!mergedLayer || mergedLayerCount !== expectedMergedLayerCount ||
            staleClippingId || mergedLayer.typename !== 'ArtLayer' || _isClippingMask(mergedLayer)) {
            var failureDetail = '剪贴蒙版链预合并失败；已终止导出以保护底板和 grouped 关系';
            recordClippingError(failureDetail + ': ' + clippingPath +
                ' (before=' + layerCountBeforeMerge + ', after=' + refreshedSiblings.length +
                ', expectedDrop=' + expectedMergedLayerCount +
                ', grouped=' + (!!mergedLayer && _isClippingMask(mergedLayer)) +
                ', staleClipId=' + staleClippingId +
                ', actionError=' + (mergeResult.error || '') + ')');
            _recordImageProcess(clippingInfos[clippingInfos.length - 1],
                'merge_clipping_failed', failureDetail, 'error');
            return false;
        }

        // Merge Selected may choose a clipped layer's name in some Photoshop
        // versions. Keep the base name so manifest/name matching stays stable.
        try { mergedLayer.name = baseName; } catch (renameMergedError) { }

        stats.clippingMaskMerged += expectedMergedLayerCount;
        for (var infoIndex = 0; infoIndex < clippingInfos.length; infoIndex++) {
            var mergeEntry = _recordImageProcess(
                clippingInfos[infoIndex], 'merge_clipping',
                '剪贴蒙版按完整连续链一次性合并', 'info');
            if (mergeEntry) mergeEntry.resultLayerId = mergedLayerId;
        }
        _recordImageProcess(
            _captureImageLayerInfo(mergedLayer), 'merge_clipping_visual',
            '剪贴链最终 Photoshop 像素', 'info');
    }
}

function _rasterizeLayerStyle(layer) {
    try {
        app.activeDocument.activeLayer = layer;
        var desc = new ActionDescriptor();
        var ref = new ActionReference();
        ref.putEnumerated(app.charIDToTypeID('Lyr '), app.charIDToTypeID('Ordn'), app.charIDToTypeID('Trgt'));
        desc.putReference(app.charIDToTypeID('null'), ref);
        desc.putEnumerated(app.stringIDToTypeID('what'), app.stringIDToTypeID('rasterizeItem'), app.stringIDToTypeID('layerStyle'));
        executeAction(app.stringIDToTypeID('rasterizeLayer'), desc, DialogModes.NO);
        return true;
    } catch (e) { return false; }
}

function _getBakedPlacementDelta(renderedBounds, replacementBounds) {
    return {
        x: renderedBounds.left - replacementBounds.left,
        y: renderedBounds.top - replacementBounds.top
    };
}

function _clipBoundsToCanvas(bounds, canvasWidth, canvasHeight) {
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

/** Render one layer through Photoshop's compositor, then replace it with pixels. */
function _bakeImageLayerVisual(layer) {
    var sourceDoc = null;
    var renderDoc = null;
    var renderLayer = null;
    var replacement = null;
    var originalName = '';
    var renderedBounds = null;
    var bakeStage = 'precheck';
    try {
        if (!layer || layer.typename !== 'ArtLayer' || _isClippingMask(layer) ||
            _hasClippingDependents(layer)) return null;
        sourceDoc = app.activeDocument;
        originalName = String(layer.name || '');
        var parent = layer.parent;
        if (!parent || !parent.artLayers) return null;
        var originalBounds = _getLayerBounds(layer);
        if (!originalBounds || originalBounds.width <= 0 || originalBounds.height <= 0) return null;

        bakeStage = 'create-render-document';
        renderDoc = _getImageRenderDocument(sourceDoc);

        bakeStage = 'duplicate-to-render-document';
        app.activeDocument = sourceDoc;
        renderLayer = layer.duplicate(renderDoc, ElementPlacement.PLACEATBEGINNING);
        bakeStage = 'measure-rendered-layer';
        app.activeDocument = renderDoc;
        renderDoc.activeLayer = renderLayer;
        renderedBounds = _clipBoundsToCanvas(
            _getLayerBounds(renderLayer),
            Math.round(renderDoc.width.as('px')),
            Math.round(renderDoc.height.as('px')));
        if (!renderedBounds || renderedBounds.width <= 0 || renderedBounds.height <= 0) {
            renderLayer.remove();
            renderLayer = null;
            return null;
        }

        bakeStage = 'copy-rendered-composite';
        renderDoc.selection.selectAll();
        renderDoc.selection.copy(true);
        renderDoc.selection.deselect();
        renderLayer.remove();
        renderLayer = null;

        bakeStage = 'paste-rendered-composite';
        app.activeDocument = sourceDoc;
        sourceDoc.activeLayer = layer;
        replacement = sourceDoc.paste();
        var replacementBounds = _getLayerBounds(replacement);
        if (replacementBounds) {
            var placementDelta = _getBakedPlacementDelta(renderedBounds, replacementBounds);
            replacement.translate(
                UnitValue(placementDelta.x, 'px'),
                UnitValue(placementDelta.y, 'px'));
        }
        bakeStage = 'replace-source-layer';
        replacement.move(layer, ElementPlacement.PLACEBEFORE);
        replacement.name = originalName;
        // Never assign `grouped = false` here. In Photoshop 2025 ExtendScript
        // that write behaves like a toggle and can clip an already-ungrouped
        // pasted layer without throwing. Validate the native result only.
        if (_isClippingMask(replacement)) {
            throw new Error('烘焙替换层意外继承剪贴状态: ' + originalName);
        }
        try { replacement.opacity = 100; } catch (eOpacity) { }
        try { replacement.fillOpacity = 100; } catch (eFill) { }
        try { replacement.blendMode = BlendMode.NORMAL; } catch (eBlend) { }
        try { layer.allLocked = false; } catch (unlockError) { }
        layer.remove();
        if (_isClippingMask(replacement)) {
            throw new Error('删除源层后烘焙替换层仍为剪贴状态: ' + originalName);
        }
        return replacement;
    } catch (e) {
        _DEBUG_LOG.push('[BAKE-ERROR] ' + originalName + ' stage=' + bakeStage +
            ' message=' + (e.message || e) + ' line=' + (e.line || 0));
        try { if (renderLayer) renderLayer.remove(); } catch (renderCleanupError) { }
        try { if (replacement) replacement.remove(); } catch (cleanupError) { }
        return null;
    } finally {
        try { if (sourceDoc) app.activeDocument = sourceDoc; } catch (restoreError) { }
    }
}

function _rasterizeGroup(group) {
    var groupName = '';
    var groupStage = 'precheck';
    try {
        groupName = String(group.name || '');
        try {
            if (group.grouped) return null;
        } catch (groupedReadError) { }
        groupStage = 'select-group';
        if (!_selectLayerForMerge(group, false)) return null;
        groupStage = 'merge-group';
        executeAction(app.stringIDToTypeID('mergeLayersNew'), undefined, DialogModes.NO);
        groupStage = 'validate-result';
        var mergedLayer = app.activeDocument.activeLayer;
        return _isClippingMask(mergedLayer) ? null : mergedLayer;
    } catch (e) {
        _DEBUG_LOG.push('[GROUP-MERGE-ERROR] ' + groupName + ' stage=' + groupStage +
            ' message=' + (e.message || e) + ' line=' + (e.line || 0));
        return null;
    }
}

function _addImageRisk(stats, layer, message) {
    var text = _getLayerDebugPath(layer) + ': ' + message;
    stats.riskWarnings.push(text);
    _recordImageProcess(layer, 'warning', message, 'warning');
}

function _promoteVisualReferenceName(layer, currentName) {
    var baseName = String(currentName || '')
        .replace(/^@ref:/i, '')
        .replace(/\.(img|png)\s*$/i, '');
    var layerId = 0;
    try { layerId = layer.id || 0; } catch (e) { }
    return baseName + '_visual_' + layerId + '.img';
}

/**
 * 图层处理引擎 (v3.1)
 *
 * 改进：
 * - 组件组（$xxx.bt / $xxx.sld）不合并 -> 保持结构让插件解析
 * - 组件组上的特效保留（插件读 lfx2 数据处理）
 * - 只有 .img 标记组 和 非组件带特效组 才合并
 */
function _processLayers(layers) {
    var stats = {
        textConverted: 0, effectRasterized: 0, effectSkipped: 0,
        groupWithEffectMerged: 0, groupMerged: 0,
        clippingMaskMerged: 0, componentKept: 0,
        imageFullyBaked: 0, maskBaked: 0, refPromoted: 0,
        skipped: 0, errors: [], clippingErrors: [], riskWarnings: [],
        imageRenderMode: _CONFIG.IMAGE_RENDER_MODE
    };
    var psVisualMode = (_CONFIG.IMAGE_RENDER_MODE === 'ps_visual');
    var fatalClippingError = false;
    var fatalClippingPaths = [];

    function recurse(layers) {
        var recurseOwner = null;
        try {
            if (layers && layers.length > 0) recurseOwner = layers[0].parent;
        } catch (recurseOwnerError) { }
        if (!_mergeClippingChainsBeforeBake(layers, stats)) {
            fatalClippingError = true;
            throw new Error('剪贴蒙版链合并失败，Smart Export 已停止且不会保存半处理 PSD');
        }
        try {
            if (recurseOwner && recurseOwner.layers) layers = recurseOwner.layers;
        } catch (recurseRefreshError) { }

        for (var i = layers.length - 1; i >= 0; i--) {
            var layer = layers[i];
            try {
                var layerName = layer.name;
                if (_CONFIG.SKIP_HIDDEN_LAYERS && !layer.visible) { stats.skipped++; continue; }
                if (layerName.indexOf('@ref:') === 0) {
                    var refAnalysis = _analyzeLayerProcessing(layer);
                    var refHasOwnVisual = refAnalysis.hasEffect || refAnalysis.hasMask ||
                        refAnalysis.hasNonNormalBlend || refAnalysis.hasReducedOpacity ||
                        refAnalysis.hasReducedFillOpacity;
                    if (psVisualMode && refHasOwnVisual) {
                        var oldRefInfo = _captureImageLayerInfo(layer);
                        layerName = _promoteVisualReferenceName(layer, layerName);
                        layer.name = layerName;
                        stats.refPromoted++;
                        _recordImageProcess(oldRefInfo, 'promote_reference',
                            '带视觉状态的 @ref 改为独立图片 ' + layerName, 'warning');
                    } else {
                        stats.skipped++;
                        continue;
                    }
                }

                var isGroup = (layer.typename === 'LayerSet');

                if (isGroup) {
                    var groupAnalysis = _analyzeLayerProcessing(layer);
                    // ── 组件组 ($xxx.bt / $xxx.sld) -> 保留结构, 递归处理子层 ──
                    if (_isComponentLayerName(layerName)) {
                        stats.componentKept++;
                        if (psVisualMode && (groupAnalysis.hasEffect || groupAnalysis.hasMask ||
                            groupAnalysis.hasNonNormalBlend || groupAnalysis.hasReducedOpacity)) {
                            _addImageRisk(stats, layer,
                                '组件组存在组级效果/蒙版/透明度/混合模式，已保留结构，请检查最终视觉');
                        }
                        recurse(layer.layers);
                        continue;
                    }

                    // ── .img 标记组 -> 合并成单个图片 ──
                    if (layerName.toLowerCase().indexOf('.img') !== -1) {
                        var imgGroupInfo = _captureImageLayerInfo(layer);
                        if (_rasterizeGroup(layer)) {
                            stats.groupMerged++;
                            _recordImageProcess(imgGroupInfo, 'merge_group', '.img 图片组合并', 'info');
                            continue;
                        }
                        else { stats.errors.push('组(.img)合并失败: ' + layerName); recurse(layer.layers); }
                    }
                    // ── 带视觉状态的普通组 -> 合并 ──
                    else if (_hasLayerEffect(layer) || (psVisualMode &&
                        (groupAnalysis.hasNonNormalBlend || groupAnalysis.hasReducedOpacity))) {
                        var visualGroupInfo = _captureImageLayerInfo(layer);
                        if (_rasterizeGroup(layer)) {
                            stats.groupWithEffectMerged++;
                            _recordImageProcess(visualGroupInfo, 'merge_group_visual',
                                '普通组视觉状态由 Photoshop 合并', 'info');
                            continue;
                        } else {
                            stats.errors.push('带视觉状态组合并失败: ' + layerName);
                            recurse(layer.layers);
                        }
                    }
                    // ── 普通组 -> 递归 ──
                    else {
                        recurse(layer.layers);
                    }
                    continue;
                }

                // ── 剪贴蒙版（前置阶段完成后不应再出现） ──
                if (_isClippingMask(layer)) {
                    fatalClippingError = true;
                    var residualClippingPath = _getLayerDebugPath(layer);
                    fatalClippingPaths.push(residualClippingPath);
                    _recordImageProcess(
                        _captureImageLayerInfo(layer), 'unexpected_clipping',
                        '前置合并后由后续 Photoshop 操作重新产生 grouped 状态', 'error');
                    throw new Error('剪贴蒙版前置合并后仍残留: ' + residualClippingPath);
                }

                // ── 文本 ──
                var isText = false;
                try { isText = (layer.kind === LayerKind.TEXT); } catch (e) { }
                if (isText) {
                    if (layerName.toLowerCase().indexOf('.img') !== -1) {
                        if (_bakeImageLayerVisual(layer) || _rasterizeLayerStyle(layer)) stats.textConverted++;
                        else { stats.errors.push('文本转换失败: ' + layerName); stats.skipped++; }
                    }
                    continue;
                }

                // ── 普通图片层 ──
                var analysis = _analyzeLayerProcessing(layer);
                if (analysis.hasNonNormalBlend) {
                    _addImageRisk(stats, layer,
                        '背景依赖混合模式 ' + (analysis.blendMode || '<unknown>') + ' 无法作为独立 Sprite 精确烘焙');
                    continue;
                }

                var mustBakeInPhotoshop = analysis.hasMask || analysis.hasExoticEffect;
                var shouldBakeInPhotoshop = psVisualMode && (analysis.hasEffect || analysis.hasMask ||
                    analysis.hasReducedOpacity || analysis.hasReducedFillOpacity);

                if (mustBakeInPhotoshop || shouldBakeInPhotoshop) {
                    var imageLayerInfo = _captureImageLayerInfo(layer);
                    var bakedImageLayer = _bakeImageLayerVisual(layer);
                    if (bakedImageLayer) {
                        stats.imageFullyBaked++;
                        if (analysis.hasMask) stats.maskBaked++;
                        if (analysis.hasEffect) stats.effectRasterized++;
                        var reasons = [];
                        if (analysis.hasEffect) reasons.push('effect');
                        if (analysis.hasUserMask) reasons.push('userMask');
                        if (analysis.hasVectorMask) reasons.push('vectorMask');
                        if (analysis.hasReducedOpacity) reasons.push('opacity=' + analysis.opacity);
                        if (analysis.hasReducedFillOpacity) reasons.push('fillOpacity=' + analysis.fillOpacity);
                        var bakedEntry = _recordImageProcess(imageLayerInfo, 'bake_image_visual', reasons.join(','), 'info');
                        bakedEntry.resultLayerId = _getLayerId(bakedImageLayer);
                    } else {
                        stats.errors.push('图片完整烘焙失败: ' + layerName);
                        _recordImageProcess(imageLayerInfo, 'bake_failed', 'Photoshop render-copy failed', 'error');
                    }
                    continue;
                }

                if (analysis.hasEffect) {
                    stats.effectSkipped++;
                    _recordImageProcess(layer, 'unity_effect_fallback', 'Unity 可编辑模式保留图片效果', 'info');
                }
            } catch (e) {
                stats.errors.push('[' + layer.name + ']: ' + e.message);
                stats.skipped++;
            }
        }
    }

    _disposeImageRenderDocument();
    try {
        recurse(layers);
        if (fatalClippingError) {
            var clippingFailureDetails = fatalClippingPaths.length > 0
                ? fatalClippingPaths.join('；')
                : stats.clippingErrors.join('；');
            throw new Error('检测到未解析的剪贴蒙版: ' + clippingFailureDetails +
                '。Smart Export 已停止且不会保存半处理 PSD');
        }
        if (stats.errors.length > 0) {
            throw new Error('图片处理失败: ' + stats.errors.join('；') +
                '。Smart Export 已停止且不会保存失败处理 PSD');
        }
        return stats;
    } finally {
        _disposeImageRenderDocument();
    }
}

function _removeHiddenLayers(layers) {
    var removed = 0;
    if (!layers) return removed;

    var removeQueue = [];
    function enqueueUnique(layer) {
        if (!layer) return;
        for (var q = 0; q < removeQueue.length; q++) {
            if (removeQueue[q] === layer) return;
        }
        removeQueue.push(layer);
    }

    // If a hidden layer is the base of a clipping chain, every clipped layer
    // above it is visually hidden as well. Remove dependents first; deleting
    // only the base would make Photoshop silently clear their grouped flags.
    for (var i = 0; i < layers.length; i++) {
        var candidate = layers[i];
        if (!candidate || candidate.visible) continue;

        if (!_isClippingMask(candidate)) {
            for (var above = i - 1; above >= 0 && _isClippingMask(layers[above]); above--) {
                enqueueUnique(layers[above]);
            }
        }
        enqueueUnique(candidate);
    }

    for (var removeIndex = 0; removeIndex < removeQueue.length; removeIndex++) {
        var hiddenLayer = removeQueue[removeIndex];
        try {
            var hiddenName = hiddenLayer.name;
            hiddenLayer.remove();
            removed++;
            _DEBUG_LOG.push('[REMOVE-HIDDEN-CHAIN-SAFE] ' + hiddenName);
        } catch (e) {
            _DEBUG_LOG.push('[REMOVE-HIDDEN-ERROR] ' +
                (hiddenLayer ? hiddenLayer.name : '<unknown>') + ': ' + e.message);
        }
    }

    // Recurse only through groups that survived the removal pass.
    for (var groupIndex = layers.length - 1; groupIndex >= 0; groupIndex--) {
        var group = layers[groupIndex];
        try {
            if (group && group.visible && group.typename === 'LayerSet') {
                removed += _removeHiddenLayers(group.layers);
            }
        } catch (groupError) {
            _DEBUG_LOG.push('[REMOVE-HIDDEN-ERROR] ' +
                (group ? group.name : '<unknown>') + ': ' + groupError.message);
        }
    }

    return removed;
}

// ╔══════════════════════════════════════════╗
// ║         调 试 日 志                       ║
// ╚══════════════════════════════════════════╝

/**
 * 将调试日志写入文件（与PSD同目录，.log后缀）
 * @param {Document} doc - 用于获取文件路径
 * @param {String} mode - 执行模式
 */
function _writeDebugLog(doc, mode) {
    if (!_DEBUG_LOG.length) return null;
    try {
        var docFolder;
        try { docFolder = doc.path; } catch (e) { docFolder = Folder.myDocuments; }
        var baseName = doc.name.replace(/\.[^.]+$/, '');
        var logFile = new File(docFolder + '/' + baseName + '_SmartExport.log');
        logFile.encoding = 'UTF-8';
        logFile.open('w');
        logFile.writeln('=== PSD2UGUI Smart Export Debug Log ===');
        logFile.writeln('Date: ' + new Date().toString());
        logFile.writeln('Document: ' + doc.name);
        logFile.writeln('Mode: ' + mode);
        logFile.writeln('Canvas: ' + Math.round(doc.width.as('px')) + 'x' + Math.round(doc.height.as('px')));
        logFile.writeln('Total log entries: ' + _DEBUG_LOG.length);
        logFile.writeln('========================================');
        logFile.writeln('');
        for (var i = 0; i < _DEBUG_LOG.length; i++) {
            logFile.writeln(_DEBUG_LOG[i]);
        }
        logFile.writeln('');
        logFile.writeln('=== END ===');
        logFile.close();
        return logFile.fsName;
    } catch (e) {
        return null;
    }
}

// ╔══════════════════════════════════════════╗
// ║         导 出 + 流 程                    ║
// ╚══════════════════════════════════════════╝

function _choosePSDExportFile(doc, sourceDoc) {
    var pathOwner = sourceDoc || doc;
    var remembered = _loadRememberedExportFile(pathOwner);
    if (remembered) {
        _DEBUG_LOG.push('[OUTPUT] reuse=' + remembered.fsName);
        return remembered;
    }

    var baseName = _stripExportSuffix(doc.name.replace(/\.psd$/i, ''));
    var defaultFileName = baseName + _CONFIG.EXPORT_SUFFIX + '.psd';
    var settings = _readJsonFile(_getExportPathSettingsFile());
    var lastExportPath = settings && settings.lastExportPath;
    if (!lastExportPath) {
        var userData = _getUserDataFolder();
        var legacySettings = userData && _readJsonFile(new File(userData.fsName + '/export_settings.json'));
        if (legacySettings && legacySettings.lastExportFolder) {
            lastExportPath = legacySettings.lastExportFolder + '/' + defaultFileName;
        }
    }

    var defaultFolder = lastExportPath ? new File(String(lastExportPath)).parent : null;
    if (!defaultFolder || !defaultFolder.exists) {
        try { defaultFolder = pathOwner.path; } catch (e) { defaultFolder = Folder.myDocuments; }
    }
    if (!defaultFolder || !defaultFolder.exists) defaultFolder = Folder.myDocuments;

    var saveFile = new File(defaultFolder.fsName + '/' + defaultFileName);
    saveFile = saveFile.saveDlg('选择PSD导出位置');
    if (!saveFile) return null;
    if (!/\.psd$/i.test(saveFile.name)) saveFile = new File(saveFile.fsName + '.psd');
    _rememberExportFile(pathOwner, saveFile);
    return saveFile;
}

function _savePSDToFile(doc, saveFile) {
    if (!saveFile) return null;
    var opts = new PhotoshopSaveOptions();
    opts.embedColorProfile = true;
    opts.alphaChannels = true;
    opts.layers = true;
    opts.maximizeCompatibility = true;
    doc.saveAs(saveFile, opts, true, Extension.LOWERCASE);
    return saveFile;
}

function _exportPSD(doc) {
    return _savePSDToFile(doc, _choosePSDExportFile(doc, doc));
}

// ╔══════════════════════════════════════════╗
// ║     Binding Manifest (v4.0)              ║
// ╚══════════════════════════════════════════╝

/**
 * 检测文本内容的数据模式
 */
function _detectContentPattern(text) {
    if (!text) return 'none';
    // currency: ¥ $ € £ 或 数字+元
    if (/[\$\u00A5\u20AC\u00A3]/.test(text) || /\d+\s*\u5143/.test(text)) return 'currency';
    // ratio: x/y
    if (/\d+\s*\/\s*\d+/.test(text)) return 'ratio';
    // level: Lv. 或 等级
    if (/[Ll]v[\.\s]*\d/.test(text) || /\u7b49\u7ea7/.test(text)) return 'level';
    // time: xx:xx 或 分/秒/时
    if (/\d+:\d+/.test(text) || /\d+\s*[\u5206\u79d2\u65f6]/.test(text)) return 'time';
    // percentage
    if (/\d+\s*%/.test(text)) return 'percentage';
    // pure number
    if (/^\s*[\d,\.\+\-]+\s*$/.test(text)) return 'number';
    // has text content
    return 'label';
}

/**
 * 生成绑定清单 JSON（v4.0）
 * 捕获完整 PSD 上下文：层级、文本样本、空间位置、兄弟图层
 * Unity 侧可据此进行自动/半自动数据绑定
 *
 * @param {Document} doc         - 原始文档（用于路径）
 * @param {String}   moduleName  - 模块名
 * @param {Array}    infos       - _collectForRename 输出（可含 newName）
 * @returns {String|null} 输出文件路径
 */
function _generateBindingManifest(doc, moduleName, infos, targetFile) {
    try {
        var docFolder;
        if (targetFile) {
            try { docFolder = targetFile.parent; } catch (tf) { docFolder = null; }
        }
        if (!docFolder) {
            try { docFolder = doc.path; } catch (e) { docFolder = Folder.myDocuments; }
        }
        var baseName = _getManifestBaseName(doc, targetFile);

        var layers = [];
        for (var i = 0; i < infos.length; i++) {
            var info = infos[i];
            if (info.classification === 'group') continue;

            var currentLayerName = '';
            try { currentLayerName = String(info.layer.name || ''); } catch (eCurrentName) { }
            var proposedName = info.newName || info.lockedName || currentLayerName;
            if (currentLayerName && info.newName && info.newName.indexOf('@ref:') === 0 &&
                currentLayerName.indexOf('@ref:') !== 0) {
                proposedName = currentLayerName;
            }
            var textSample = '';
            if (info.classification === 'txt') {
                try { textSample = info.layer.textItem.contents; } catch (eText) { }
            } else if (_isComponentClass(info.classification)) {
                // Image baking and clipping-chain flattening may invalidate
                // the live Layer reference kept by the rename snapshot.  A
                // manifest must still be emitted from the captured metadata.
                try {
                    if (info.layer && info.layer.typename === 'LayerSet') {
                        textSample = _getFirstTextContent(info.layer);
                    }
                } catch (eComponentText) { }
            }

            var hierarchy = [];
            try {
                var p = info.layer.parent;
                while (p && p.typename === 'LayerSet') {
                    hierarchy.push(p.name);
                    p = p.parent;
                }
            } catch (eHier) { }
            hierarchy.reverse();

            var siblings = [];
            try {
                var parent = info.layer.parent;
                if (parent && parent.typename === 'LayerSet') {
                    for (var j = 0; j < parent.layers.length; j++) {
                        var sib = parent.layers[j];
                        if (sib === info.layer) continue;
                        var sibType = 'unknown';
                        if (sib.typename === 'ArtLayer') {
                            try {
                                sibType = (sib.kind === LayerKind.TEXT) ? 'txt' : 'img';
                            } catch (e2) { sibType = 'img'; }
                        } else if (sib.typename === 'LayerSet') {
                            sibType = 'group';
                        }
                        siblings.push(sibType + ':' + sib.name);
                    }
                }
            } catch (eSib) { }

            var bounds = [0, 0, 0, 0];
            if (info.bounds) {
                bounds = [
                    Math.round(info.bounds.left),
                    Math.round(info.bounds.top),
                    Math.round(info.bounds.width),
                    Math.round(info.bounds.height)
                ];
            }
            var tag = _getPluginTag(info.classification).replace(/^\./, '');
            var suggestedProp = _getSuggestedPropFromName(proposedName, moduleName, info.classification);
            var issues = info.issues || [];

            var entry = {
                id: info.layerId || 0,
                layerId: info.layerId || 0,
                flatIndex: info.flatIndex,
                path: info.path,
                name: proposedName,
                originalName: info.originalName,
                proposedName: proposedName,
                lockedName: info.lockedName || null,
                type: info.classification,
                kind: _getKindName(info.classification),
                psd2uguiTag: tag,
                isComponent: info.isComponent,
                shouldBind: info.shouldBind === true,
                confidence: info.confidence,
                binding: {
                    shouldBind: info.shouldBind === true,
                    suggestedProp: suggestedProp,
                    category: _getBindingCategory(info.classification)
                },
                bounds: bounds,
                rect: bounds,
                text: textSample,
                textSample: textSample,
                contentPattern: _detectContentPattern(textSample),
                hierarchy: hierarchy,
                siblings: siblings,
                issues: issues
            };

            layers.push(entry);
        }

        var manifest = {
            schema: 'psd2ugui.manifest/2.0',
            version: '2.0',
            imageRenderMode: _CONFIG.IMAGE_RENDER_MODE,
            imageProcessing: _IMAGE_PROCESS_LOG,
            psd: {
                name: targetFile ? targetFile.name : doc.name,
                width: Math.round(doc.width.as('px')),
                height: Math.round(doc.height.as('px'))
            },
            document: targetFile ? targetFile.name : doc.name,
            module: moduleName,
            canvas: [Math.round(doc.width.as('px')), Math.round(doc.height.as('px'))],
            exportDate: new Date().toString(),
            generatedAt: new Date().toString(),
            componentCount: layers.length,
            naming: {
                module: moduleName,
                ruleSetVersion: '2.0'
            },
            layers: layers,
            components: layers
        };

        var manifestFile = new File(docFolder + '/' + baseName + '.ui_manifest.json');
        manifestFile.encoding = 'UTF-8';
        manifestFile.open('w');
        manifestFile.write(_JSON.stringify(manifest));
        manifestFile.close();

        var legacyFile = new File(docFolder + '/' + baseName + '_binding.json');
        legacyFile.encoding = 'UTF-8';
        legacyFile.open('w');
        legacyFile.write(_JSON.stringify(manifest));
        legacyFile.close();

        return manifestFile.fsName;
    } catch (e) {
        _DEBUG_LOG.push('[MANIFEST-ERROR] ' + e.message);
        return null;
    }
}

function _appendPerformanceLog(stats, totalDuration) {
    if (!stats) return;
    _DEBUG_LOG.push('[PERF] duplicate=' + (stats.duplicateDuration || '0.00') +
        ' rename=' + (stats.renameDuration || '0.00') +
        ' font=' + (stats.fontDuration || '0.00') +
        ' saveDialog=' + (stats.saveDialogDuration || '0.00') +
        ' process=' + (stats.processDuration || '0.00') +
        ' psdSave=' + (stats.psdSaveDuration || '0.00') +
        ' manifest=' + (stats.manifestDuration || '0.00') +
        ' close=' + (stats.tempCloseDuration || '0.00') +
        ' total=' + totalDuration);
}

function _restoreChunkedOriginalDocument(state) {
    if (!state || !state.originalDoc) return;
    try { app.activeDocument = state.originalDoc; } catch (e) { }
}

function _cancelChunkedExportFlow() {
    var state = _ASYNC_EXPORT_STATE;
    _ASYNC_EXPORT_STATE = null;
    try { app.displayDialogs = DialogModes.NO; } catch (dialogSuppressError) { }
    if (state && state.tempDoc) {
        try { state.tempDoc.close(SaveOptions.DONOTSAVECHANGES); } catch (closeError) { }
        state.tempDoc = null;
    }
    _restoreChunkedOriginalDocument(state);
    try { app.displayDialogs = DialogModes.ALL; } catch (dialogError) { }
}

function _beginChunkedExportFlow(moduleName, mode, useActiveDocumentAsDisposable) {
    _cancelChunkedExportFlow();

    var originalDoc = app.activeDocument;
    var disposableWorkDoc = (useActiveDocumentAsDisposable === true);
    var state = {
        moduleName: moduleName,
        mode: mode === 'export' ? 'export' : 'both',
        originalDoc: originalDoc,
        tempDoc: disposableWorkDoc ? originalDoc : null,
        disposableWorkDoc: disposableWorkDoc,
        savedFile: null,
        renameResult: null,
        fontStats: null,
        preparePhase: disposableWorkDoc ? 'rename' : 'duplicate',
        finishPhase: 'process',
        processStats: null,
        bindingPath: null,
        startTime: new Date(),
        duplicateDuration: '0.00',
        renameDuration: '0.00',
        fontDuration: '0.00',
        saveDialogDuration: '0.00'
    };
    _ASYNC_EXPORT_STATE = state;
    _DEBUG_LOG = [];
    _IMAGE_PROCESS_LOG = [];
    _LAYER_ANALYSIS_CACHE = {};
    _DEBUG_LOG.push('[START-CHUNKED] module=' + moduleName + ' mode=' + state.mode +
        ' imageRenderMode=' + _CONFIG.IMAGE_RENDER_MODE +
        ' disposableWorkDoc=' + disposableWorkDoc);

    return {
        mode: 'begin',
        exportMode: state.mode,
        hostVersion: PSD2UGUI_getHostVersion(),
        ready: false,
        phase: state.preparePhase
    };
}

function _prepareNextChunkedExportStep() {
    var state = _ASYNC_EXPORT_STATE;
    if (!state) return { error: '没有进行中的分段导出' };
    if (state.preparePhase === 'done') {
        return { ready: true };
    }

    app.displayDialogs = DialogModes.NO;
    try {
        if (state.preparePhase === 'duplicate') {
            var duplicateStart = new Date();
            state.tempDoc = state.originalDoc.duplicate(state.originalDoc.name, false);
            state.duplicateDuration = ((new Date() - duplicateStart) / 1000).toFixed(2);
            app.activeDocument = state.tempDoc;
            state.preparePhase = 'rename';
            return { ready: false, phase: 'duplicate', nextPhase: state.preparePhase };
        }

        app.activeDocument = state.tempDoc;
        if (state.preparePhase === 'rename') {
            var renameStart = new Date();
            state.renameResult = _smartRenameAllLayers(state.tempDoc, state.moduleName);
            state.renameDuration = ((new Date() - renameStart) / 1000).toFixed(2);
            state.preparePhase = 'font';
            return { ready: false, phase: 'rename', nextPhase: state.preparePhase };
        }

        if (state.preparePhase === 'font') {
            var fontStart = new Date();
            state.fontStats = _normalizeSongtiTextFonts(state.tempDoc.layers);
            state.fontDuration = ((new Date() - fontStart) / 1000).toFixed(2);
            state.preparePhase = 'choose';
            return { ready: false, phase: 'font', nextPhase: state.preparePhase };
        }

        if (state.preparePhase === 'choose') {
            var saveDialogStart = new Date();
            state.savedFile = _choosePSDExportFile(state.tempDoc, state.originalDoc);
            state.saveDialogDuration = ((new Date() - saveDialogStart) / 1000).toFixed(2);
            if (!state.savedFile) {
                _cancelChunkedExportFlow();
                return { cancelled: true };
            }

            state.preparePhase = 'done';
            return { ready: true, phase: 'choose' };
        }

        return { error: '未知准备阶段: ' + state.preparePhase };
    } finally {
        app.displayDialogs = DialogModes.ALL;
    }
}

function _finishChunkedExportFlow() {
    var state = _ASYNC_EXPORT_STATE;
    if (!state || !state.tempDoc) {
        return { error: '没有可完成的分段导出' };
    }

    app.activeDocument = state.tempDoc;
    app.displayDialogs = DialogModes.NO;
    try {
        if (state.finishPhase === 'process') {
            var processStart = new Date();
            var hiddenRemoved = _CONFIG.SKIP_HIDDEN_LAYERS ? _removeHiddenLayers(state.tempDoc.layers) : 0;
            var processStats = _processLayers(state.tempDoc.layers);
            processStats.processDuration = ((new Date() - processStart) / 1000).toFixed(2);
            processStats.duplicateDuration = state.duplicateDuration;
            processStats.renameDuration = state.renameDuration;
            processStats.fontDuration = state.fontDuration;
            processStats.saveDialogDuration = state.saveDialogDuration;
            processStats.songtiFontsNormalized = state.fontStats.changed;
            processStats.hiddenRemoved = hiddenRemoved;
            processStats.imageRenderMode = _CONFIG.IMAGE_RENDER_MODE;
            state.processStats = processStats;
            state.finishPhase = 'save';
            return { done: false, phase: 'process', nextPhase: state.finishPhase };
        }

        if (state.finishPhase === 'save') {
            var psdSaveStart = new Date();
            _savePSDToFile(state.tempDoc, state.savedFile);
            state.processStats.psdSaveDuration = ((new Date() - psdSaveStart) / 1000).toFixed(2);
            state.finishPhase = 'manifest';
            return { done: false, phase: 'save', nextPhase: state.finishPhase };
        }

        if (state.finishPhase === 'manifest') {
            var manifestStart = new Date();
            state.bindingPath = _generateBindingManifest(
                state.originalDoc, state.moduleName, state.renameResult.infos, state.savedFile);
            state.processStats.manifestDuration = ((new Date() - manifestStart) / 1000).toFixed(2);
            state.finishPhase = 'close';
            return { done: false, phase: 'manifest', nextPhase: state.finishPhase };
        }

        if (state.finishPhase === 'close') {
            var logPath = null;
            if (state.disposableWorkDoc) {
                var preCloseDuration = ((new Date() - state.startTime) / 1000).toFixed(2);
                _appendPerformanceLog(state.processStats, preCloseDuration);
                logPath = _writeDebugLog(state.tempDoc, state.mode);
            }
            var closeStart = new Date();
            state.tempDoc.close(SaveOptions.DONOTSAVECHANGES);
            state.processStats.tempCloseDuration = ((new Date() - closeStart) / 1000).toFixed(2);
            state.tempDoc = null;
            _restoreChunkedOriginalDocument(state);

            var perfDuration = ((new Date() - state.startTime) / 1000).toFixed(2);
            if (!state.disposableWorkDoc) {
                _appendPerformanceLog(state.processStats, perfDuration);
                logPath = _writeDebugLog(state.originalDoc, state.mode);
            }
            var duration = ((new Date() - state.startTime) / 1000).toFixed(2);
            var savedSize = (state.savedFile.length / 1024).toFixed(1);

            var response = {
                done: true,
                mode: state.mode,
                duration: duration,
                imageRenderMode: _CONFIG.IMAGE_RENDER_MODE,
                renamed: state.renameResult.renamed,
                stats: state.renameResult.stats,
                process: state.processStats,
                savedPath: state.savedFile.fsName,
                savedSize: savedSize,
                logPath: logPath,
                bindingPath: state.bindingPath
            };

            _ASYNC_EXPORT_STATE = null;
            return response;
        }

        return { error: '未知收尾阶段: ' + state.finishPhase };
    } finally {
        app.displayDialogs = DialogModes.ALL;
    }
}

function _executeFlow(moduleName, mode) {
    var originalDoc = app.activeDocument;
    var tempDoc = null;
    var startTime = new Date();
    _DEBUG_LOG = [];
    _IMAGE_PROCESS_LOG = [];
    _LAYER_ANALYSIS_CACHE = {};
    _DEBUG_LOG.push('[START] module=' + moduleName + ' mode=' + mode +
        ' imageRenderMode=' + _CONFIG.IMAGE_RENDER_MODE);

    try {
        app.displayDialogs = DialogModes.NO;

        if (mode === 'rename') {
            var renameResult = _smartRenameAllLayers(originalDoc, moduleName);
            var bindingPath = _generateBindingManifest(originalDoc, moduleName, renameResult.infos);
            var logPath = _writeDebugLog(originalDoc, mode);
            var dur = ((new Date() - startTime) / 1000).toFixed(2);
            app.displayDialogs = DialogModes.ALL;
            return _JSON.stringify({
                mode: 'rename', duration: dur,
                imageRenderMode: _CONFIG.IMAGE_RENDER_MODE,
                renamed: renameResult.renamed, stats: renameResult.stats,
                savedPath: null,
                logPath: logPath,
                bindingPath: bindingPath
            });

        } else if (mode === 'export') {
            // 导出模式不改原 PSD，但导出的临时 PSD 必须先走智能命名，
            // 否则 manifest 会记录旧中文/拼音名，Unity 侧也无法按语义绑定。
            var duplicateStart = new Date();
            tempDoc = originalDoc.duplicate(originalDoc.name, false);
            var duplicateDuration = ((new Date() - duplicateStart) / 1000).toFixed(2);
            app.activeDocument = tempDoc;
            var renameStart = new Date();
            var renameResult = _smartRenameAllLayers(tempDoc, moduleName);
            var renameDuration = ((new Date() - renameStart) / 1000).toFixed(2);
            var fontStart = new Date();
            var fontStats = _normalizeSongtiTextFonts(tempDoc.layers);
            var fontDuration = ((new Date() - fontStart) / 1000).toFixed(2);
            var saveDialogStart = new Date();
            var savedFile = _choosePSDExportFile(tempDoc, originalDoc);
            var saveDialogDuration = ((new Date() - saveDialogStart) / 1000).toFixed(2);
            if (!savedFile) {
                tempDoc.close(SaveOptions.DONOTSAVECHANGES);
                tempDoc = null;
                app.activeDocument = originalDoc;
                app.displayDialogs = DialogModes.ALL;
                return _JSON.stringify({ cancelled: true });
            }
            var processStart = new Date();
            var hiddenRemoved = _CONFIG.SKIP_HIDDEN_LAYERS ? _removeHiddenLayers(tempDoc.layers) : 0;
            var processStats = _processLayers(tempDoc.layers);
            processStats.processDuration = ((new Date() - processStart) / 1000).toFixed(2);
            processStats.duplicateDuration = duplicateDuration;
            processStats.renameDuration = renameDuration;
            processStats.fontDuration = fontDuration;
            processStats.saveDialogDuration = saveDialogDuration;
            processStats.songtiFontsNormalized = fontStats.changed;
            processStats.hiddenRemoved = hiddenRemoved;
            processStats.imageRenderMode = _CONFIG.IMAGE_RENDER_MODE;
            var psdSaveStart = new Date();
            _savePSDToFile(tempDoc, savedFile);
            processStats.psdSaveDuration = ((new Date() - psdSaveStart) / 1000).toFixed(2);
            var manifestStart = new Date();
            var bindingPath = savedFile ? _generateBindingManifest(originalDoc, moduleName, renameResult.infos, savedFile) : null;
            processStats.manifestDuration = ((new Date() - manifestStart) / 1000).toFixed(2);
            var closeStart = new Date();
            tempDoc.close(SaveOptions.DONOTSAVECHANGES);
            processStats.tempCloseDuration = ((new Date() - closeStart) / 1000).toFixed(2);
            tempDoc = null;
            app.activeDocument = originalDoc;
            var perfDuration = ((new Date() - startTime) / 1000).toFixed(2);
            _appendPerformanceLog(processStats, perfDuration);
            var logPath = _writeDebugLog(originalDoc, mode);
            var dur = ((new Date() - startTime) / 1000).toFixed(2);
            app.displayDialogs = DialogModes.ALL;
            if (!savedFile) return _JSON.stringify({ cancelled: true });
            return _JSON.stringify({
                mode: 'export', duration: dur,
                imageRenderMode: _CONFIG.IMAGE_RENDER_MODE,
                renamed: renameResult.renamed, stats: renameResult.stats,
                process: processStats,
                savedPath: savedFile.fsName,
                savedSize: (savedFile.length / 1024).toFixed(1),
                logPath: logPath,
                bindingPath: bindingPath
            });

        } else {
            // mode === 'both'
            var duplicateStart = new Date();
            tempDoc = originalDoc.duplicate(originalDoc.name, false);
            var duplicateDuration = ((new Date() - duplicateStart) / 1000).toFixed(2);
            app.activeDocument = tempDoc;
            var renameStart = new Date();
            var renameResult = _smartRenameAllLayers(tempDoc, moduleName);
            var renameDuration = ((new Date() - renameStart) / 1000).toFixed(2);
            var fontStart = new Date();
            var fontStats = _normalizeSongtiTextFonts(tempDoc.layers);
            var fontDuration = ((new Date() - fontStart) / 1000).toFixed(2);
            var saveDialogStart = new Date();
            var savedFile = _choosePSDExportFile(tempDoc, originalDoc);
            var saveDialogDuration = ((new Date() - saveDialogStart) / 1000).toFixed(2);
            if (!savedFile) {
                tempDoc.close(SaveOptions.DONOTSAVECHANGES);
                tempDoc = null;
                app.activeDocument = originalDoc;
                app.displayDialogs = DialogModes.ALL;
                return _JSON.stringify({ cancelled: true });
            }
            var processStart = new Date();
            var hiddenRemoved = _CONFIG.SKIP_HIDDEN_LAYERS ? _removeHiddenLayers(tempDoc.layers) : 0;
            var processStats = _processLayers(tempDoc.layers);
            processStats.processDuration = ((new Date() - processStart) / 1000).toFixed(2);
            processStats.duplicateDuration = duplicateDuration;
            processStats.renameDuration = renameDuration;
            processStats.fontDuration = fontDuration;
            processStats.saveDialogDuration = saveDialogDuration;
            processStats.songtiFontsNormalized = fontStats.changed;
            processStats.hiddenRemoved = hiddenRemoved;
            processStats.imageRenderMode = _CONFIG.IMAGE_RENDER_MODE;
            var psdSaveStart = new Date();
            _savePSDToFile(tempDoc, savedFile);
            processStats.psdSaveDuration = ((new Date() - psdSaveStart) / 1000).toFixed(2);
            var manifestStart = new Date();
            var bindingPath = savedFile ? _generateBindingManifest(originalDoc, moduleName, renameResult.infos, savedFile) : null;
            processStats.manifestDuration = ((new Date() - manifestStart) / 1000).toFixed(2);
            var closeStart = new Date();
            tempDoc.close(SaveOptions.DONOTSAVECHANGES);
            processStats.tempCloseDuration = ((new Date() - closeStart) / 1000).toFixed(2);
            tempDoc = null;
            app.activeDocument = originalDoc;
            var perfDuration = ((new Date() - startTime) / 1000).toFixed(2);
            _appendPerformanceLog(processStats, perfDuration);
            var logPath = _writeDebugLog(originalDoc, mode);
            var dur = ((new Date() - startTime) / 1000).toFixed(2);
            app.displayDialogs = DialogModes.ALL;
            if (!savedFile) return _JSON.stringify({ cancelled: true });
            return _JSON.stringify({
                mode: 'both', duration: dur,
                imageRenderMode: _CONFIG.IMAGE_RENDER_MODE,
                renamed: renameResult.renamed, stats: renameResult.stats,
                process: processStats,
                savedPath: savedFile.fsName,
                savedSize: (savedFile.length / 1024).toFixed(1),
                logPath: logPath,
                bindingPath: bindingPath
            });
        }

    } catch (e) {
        if (tempDoc) {
            try { tempDoc.close(SaveOptions.DONOTSAVECHANGES); } catch (ce) { }
        }
        app.activeDocument = originalDoc;
        app.displayDialogs = DialogModes.ALL;
        return _JSON.stringify({ error: e.message, line: e.line });
    }
}
