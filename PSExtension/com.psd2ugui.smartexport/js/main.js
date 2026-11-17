(function () {
    'use strict';

    var cs = new CSInterface();

    // ╔══════════════════════════════════════════╗
    // ║         模块管理 (userData + cache)       ║
    // ╚══════════════════════════════════════════╝

    var DEFAULT_MODULES = [
        { id: 'common',   name: 'Common (通用)' },
        { id: 'home',     name: 'Home (主页)' },
        { id: 'battle',   name: 'Battle (战斗)' },
        { id: 'shop',     name: 'Shop (商店)' },
        { id: 'setting',  name: 'Setting (设置)' }
    ];

    function loadModules() {
        try {
            var saved = localStorage.getItem('psd2ugui_modules');
            if (saved) {
                var parsed = JSON.parse(saved);
                if (Array.isArray(parsed) && parsed.length > 0) return parsed;
            }
        } catch (e) { }
        return DEFAULT_MODULES.slice();
    }

    function normalizeModules(value) {
        if (!Array.isArray(value)) return [];
        var result = [];
        var seen = {};
        for (var i = 0; i < value.length; i++) {
            var item = value[i] || {};
            var id = String(item.id || '').replace(/[^a-z0-9_]/gi, '').toLowerCase();
            if (!id || seen[id]) continue;
            var name = String(item.name || id);
            result.push({ id: id, name: name });
            seen[id] = true;
        }
        return result;
    }

    function saveModules(modules) {
        try {
            localStorage.setItem('psd2ugui_modules', JSON.stringify(modules));
        } catch (e) { }
        savePersistentModuleSettings();
    }

    function loadLastModule() {
        try {
            return localStorage.getItem('psd2ugui_lastModule') || '';
        } catch (e) {
            return '';
        }
    }

    function saveLastModule(id) {
        try {
            localStorage.setItem('psd2ugui_lastModule', id);
        } catch (e) { }
        savePersistentModuleSettings();
    }

    function savePersistentModuleSettings() {
        if (!cs || !cs.evalScript) return;
        try {
            var payload = JSON.stringify({
                modules: modules,
                lastModule: loadLastModule()
            });
            cs.evalScript('PSD2UGUI_saveModuleSettings("' + escapeForJsx(payload) + '")');
        } catch (e) { }
    }

    function loadPersistentModuleSettings() {
        if (!cs || !cs.evalScript) return;
        try {
            cs.evalScript('PSD2UGUI_loadModuleSettings()', function (result) {
                if (!result || result === 'null' || result === 'undefined' || result === 'EvalScript error.') return;
                try {
                    var data = JSON.parse(result);
                    if (data.error) return;
                    var persistedModules = normalizeModules(data.modules);
                    if (persistedModules.length > 0) {
                        modules = persistedModules;
                        try {
                            localStorage.setItem('psd2ugui_modules', JSON.stringify(modules));
                        } catch (e1) { }
                    }
                    if (data.lastModule) {
                        try {
                            localStorage.setItem('psd2ugui_lastModule', data.lastModule);
                        } catch (e2) { }
                    }
                    refreshModuleDropdown(data.lastModule || loadLastModule());
                } catch (e) { }
            });
        } catch (e) { }
    }

    var modules = normalizeModules(loadModules());
    if (modules.length < 1) modules = DEFAULT_MODULES.slice();

    // ╔══════════════════════════════════════════╗
    // ║         DOM 引用                          ║
    // ╚══════════════════════════════════════════╝

    var $moduleSelect    = document.getElementById('moduleSelect');
    var $btnAddModule    = document.getElementById('btnAddModule');
    var $btnRemoveModule = document.getElementById('btnRemoveModule');
    var $docInfoSection  = document.getElementById('docInfoSection');
    var $docInfo         = document.getElementById('docInfo');
    var $cbRenameGeneric = document.getElementById('cbRenameGeneric');
    var $cbMarkDup       = document.getElementById('cbMarkDup');
    var $imageModeInputs = document.querySelectorAll('input[name="imageRenderMode"]');
    var $cbSkipHidden    = document.getElementById('cbSkipHidden');
    var $btnBoth         = document.getElementById('btnBoth');
    var $btnExport       = document.getElementById('btnExport');
    var $btnRename       = document.getElementById('btnRename');
    var $btnPreview      = document.getElementById('btnPreview');
    var $statusBar       = document.getElementById('statusBar');
    var $resultsSection  = document.getElementById('resultsSection');
    var $resultsGrid     = document.getElementById('resultsGrid');
    var $resultsTotal    = document.getElementById('resultsTotal');
    var $resultsErrors   = document.getElementById('resultsErrors');
    var $addModuleModal  = document.getElementById('addModuleModal');
    var $newModuleId     = document.getElementById('newModuleId');
    var $newModuleName   = document.getElementById('newModuleName');
    var $modalCancel     = document.getElementById('modalCancel');
    var $modalConfirm    = document.getElementById('modalConfirm');

    var IMAGE_MODE_STORAGE_KEY = 'psd2ugui_imageRenderMode';

    function normalizeImageRenderMode(value) {
        return value === 'unity_editable' ? 'unity_editable' : 'ps_visual';
    }

    function getImageRenderMode() {
        for (var i = 0; i < $imageModeInputs.length; i++) {
            if ($imageModeInputs[i].checked) return normalizeImageRenderMode($imageModeInputs[i].value);
        }
        return 'ps_visual';
    }

    function loadImageRenderMode() {
        var mode = 'ps_visual';
        try { mode = normalizeImageRenderMode(localStorage.getItem(IMAGE_MODE_STORAGE_KEY)); } catch (e) { }
        for (var i = 0; i < $imageModeInputs.length; i++) {
            $imageModeInputs[i].checked = ($imageModeInputs[i].value === mode);
            $imageModeInputs[i].addEventListener('change', function () {
                if (!this.checked) return;
                try { localStorage.setItem(IMAGE_MODE_STORAGE_KEY, normalizeImageRenderMode(this.value)); } catch (e) { }
            });
        }
    }

    // ╔══════════════════════════════════════════╗
    // ║         模块 UI                           ║
    // ╚══════════════════════════════════════════╝

    function refreshModuleDropdown(selectId) {
        $moduleSelect.innerHTML = '';
        for (var i = 0; i < modules.length; i++) {
            var opt = document.createElement('option');
            opt.value = modules[i].id;
            opt.textContent = modules[i].id + ' — ' + modules[i].name;
            $moduleSelect.appendChild(opt);
        }
        if (selectId) {
            $moduleSelect.value = selectId;
        }
        // 如果未选中有效值，用第一个
        if (!$moduleSelect.value && modules.length) {
            $moduleSelect.value = modules[0].id;
        }
    }

    $btnAddModule.addEventListener('click', function () {
        $newModuleId.value = '';
        $newModuleName.value = '';
        $addModuleModal.classList.add('visible');
        $newModuleId.focus();
    });

    $btnRemoveModule.addEventListener('click', function () {
        if (modules.length <= 1) {
            setStatus('至少保留一个模块', 'error');
            return;
        }
        var id = $moduleSelect.value;
        modules = modules.filter(function (m) { return m.id !== id; });
        saveModules(modules);
        refreshModuleDropdown();
        saveLastModule($moduleSelect.value);
        setStatus('已删除模块: ' + id, 'success');
    });

    $modalCancel.addEventListener('click', function () {
        $addModuleModal.classList.remove('visible');
    });

    $modalConfirm.addEventListener('click', function () {
        var id = $newModuleId.value.replace(/[^a-z0-9_]/gi, '').toLowerCase();
        var name = $newModuleName.value.trim() || id;
        if (!id) {
            setStatus('模块ID不能为空（英文小写）', 'error');
            return;
        }
        // 检查重复
        for (var i = 0; i < modules.length; i++) {
            if (modules[i].id === id) {
                setStatus('模块 ' + id + ' 已存在', 'error');
                return;
            }
        }
        modules.push({ id: id, name: name });
        saveModules(modules);
        refreshModuleDropdown(id);
        saveLastModule(id);
        $addModuleModal.classList.remove('visible');
        setStatus('已添加模块: ' + id, 'success');
    });

    $moduleSelect.addEventListener('change', function () {
        saveLastModule($moduleSelect.value);
    });

    // ╔══════════════════════════════════════════╗
    // ║         状态 & 结果                       ║
    // ╚══════════════════════════════════════════╝

    function setStatus(msg, type) {
        $statusBar.textContent = msg;
        $statusBar.className = 'status-bar' + (type ? ' ' + type : '');
    }

    function hideResults() {
        $resultsSection.classList.remove('visible');
    }

    function showResults(data) {
        $resultsGrid.innerHTML = '';
        $resultsErrors.style.display = 'none';

        function addRow(label, value, highlight) {
            var div = document.createElement('div');
            div.className = 'result-item';
            div.innerHTML = '<span class="result-label">' + label + '</span>' +
                '<span class="result-value' + (highlight ? ' highlight' : '') + '">' + value + '</span>';
            $resultsGrid.appendChild(div);
        }

        if (data.imageRenderMode) {
            addRow('图片模式', data.imageRenderMode === 'ps_visual' ? 'PS 视觉优先' : 'Unity 可编辑', true);
        }

        // 命名统计
        if (data.stats) {
            var s = data.stats;
            if (s.txt > 0)    addRow('txt_ 文本', s.txt);
            if (s.btn > 0)    addRow('.bt  按钮', s.btn);
            if (s.slider > 0) addRow('.sld 进度条', s.slider);
            if (s.bg > 0)     addRow('img_ 背景', s.bg);
            if (s.icon > 0)   addRow('icon_图标', s.icon);
            if (s.img > 0)    addRow('img_ 图片', s.img);
            if (s.ref > 0)    addRow('@ref 复用', s.ref, true);
            if (s.kept > 0)   addRow('保留原名', s.kept);
        }

        // 导出统计
        if (data.process) {
            var p = data.process;
            if (p.textConverted > 0)        addRow('文本→图片', p.textConverted);
            if (p.hiddenRemoved > 0)        addRow('隐藏图层→删除', p.hiddenRemoved, true);
            if (p.groupMerged > 0)          addRow('组(.img)→合并', p.groupMerged);
            if (p.groupWithEffectMerged > 0) addRow('组+特效→合并', p.groupWithEffectMerged);
            if (p.clippingMaskMerged > 0)   addRow('剪贴蒙版→合并', p.clippingMaskMerged);
            if (p.imageFullyBaked > 0)      addRow('图片→PS 完整烘焙', p.imageFullyBaked, true);
            if (p.maskBaked > 0)            addRow('蒙版→PS 完整烘焙', p.maskBaked);
            if (p.refPromoted > 0)          addRow('@ref→独立图片', p.refPromoted);
            if (p.duplicateDuration)        addRow('临时副本耗时', p.duplicateDuration + 's');
            if (p.renameDuration)           addRow('智能命名耗时', p.renameDuration + 's');
            if (p.processDuration)          addRow('图片处理耗时', p.processDuration + 's');
            if (p.psdSaveDuration)          addRow('PSD 保存耗时', p.psdSaveDuration + 's');
            if (p.manifestDuration)         addRow('清单耗时', p.manifestDuration + 's');
            if (p.effectRasterized > 0)     addRow('图片特效→PS 烘焙', p.effectRasterized);
            if (p.effectSkipped > 0)        addRow('图片特效→Unity 兼容', p.effectSkipped);
            if (p.componentKept > 0)       addRow('组件结构保留', p.componentKept);
            if (p.riskWarnings && p.riskWarnings.length > 0) addRow('视觉风险', p.riskWarnings.length);
            var resultMessages = [];
            if (p.errors && p.errors.length > 0) {
                resultMessages = resultMessages.concat(p.errors.slice(0, 5));
            }
            if (p.riskWarnings && p.riskWarnings.length > 0) {
                resultMessages = resultMessages.concat(p.riskWarnings.slice(0, 5));
            }
            if (resultMessages.length > 0) {
                $resultsErrors.style.display = 'block';
                $resultsErrors.textContent = '⚠ ' + resultMessages.join('\n⚠ ');
            }
        }

        // 总计
        var totalParts = [];
        if (data.renamed !== undefined) totalParts.push('改名: ' + data.renamed);
        if (data.duration) totalParts.push('耗时: ' + data.duration + 's');
        if (data.savedSize) totalParts.push('文件: ' + data.savedSize + 'KB');
        $resultsTotal.textContent = totalParts.join('  |  ');

        if (data.savedPath) {
            $resultsTotal.textContent += '\n' + data.savedPath;
        }
        if (data.logPath) {
            $resultsTotal.textContent += '\n📋 Log: ' + data.logPath;
        }
        if (data.bindingPath) {
            $resultsTotal.textContent += '\n🔗 Binding: ' + data.bindingPath;
        }

        $resultsSection.classList.add('visible');
    }

    // ╔══════════════════════════════════════════╗
    // ║         执 行                             ║
    // ╚══════════════════════════════════════════╝

    var _running = false;

    function reloadHostScript(callback) {
        try {
            var extensionPath = decodeURI(cs.getSystemPath(SystemPath.EXTENSION) || '');
            extensionPath = extensionPath.replace(/^file:\/\/+/, '').replace(/\\/g, '/').replace(/\/$/, '');
            var hostPath = extensionPath + '/jsx/host.jsx';
            // Keep evalFile at the top level so host functions replace the active
            // ExtendScript engine globals instead of disappearing with an IIFE scope.
            var command = 'var __psd2uguiReloadResult="ok";' +
                'try{$.evalFile(new File("' + escapeForJsx(hostPath) + '"));}' +
                'catch(e){__psd2uguiReloadResult="error:"+e.message+"@"+e.line;}' +
                '__psd2uguiReloadResult;';
            cs.evalScript(command, function (result) {
                if (!result || result.indexOf('error:') === 0 || result === 'EvalScript error.') {
                    callback(result || '宿主脚本无返回');
                    return;
                }
                callback(null);
            });
        } catch (e) {
            callback(e.message);
        }
    }

    function execute(mode) {
        if (_running) return;

        var moduleName = $moduleSelect.value;
        if (!moduleName) {
            setStatus('请先选择模块', 'error');
            return;
        }
        saveLastModule(moduleName);

        _running = true;
        hideResults();
        disableButtons(true);

        function buildExecuteScript(functionName, includeMode) {
            return functionName + '('
                + '"' + escapeForJsx(moduleName) + '",'
                + (includeMode ? ('"' + mode + '",') : '')
                + $cbRenameGeneric.checked + ','
                + $cbMarkDup.checked + ','
                + '"' + getImageRenderMode() + '",'
                + $cbSkipHidden.checked
                + ')';
        }

        function parseHostResult(result) {
            if (!result || result === 'null' || result === 'undefined' || result === 'EvalScript error.') {
                return { error: '执行失败或无返回: ' + (result || 'empty') };
            }
            try {
                return JSON.parse(result);
            } catch (e) {
                return { error: '结果解析失败: ' + e.message };
            }
        }

        function resetRunningState() {
            _running = false;
            disableButtons(false);
        }

        function showCompletedResult(data) {
            resetRunningState();
            if (data.error) { setStatus('错误: ' + data.error, 'error'); return; }
            if (data.cancelled) { setStatus('已取消', ''); return; }
            var modeLabel = { both: '命名+导出', export: '导出', rename: '命名' }[data.mode] || data.mode;
            setStatus('✓ ' + modeLabel + '完成！', 'success');
            showResults(data);
        }

        function cancelAfterError(message) {
            cs.evalScript('PSD2UGUI_cancelExport()', function () {
                resetRunningState();
                setStatus(message, 'error');
            });
        }

        function runLegacyExecute() {
            var script = buildExecuteScript('PSD2UGUI_execute', true);

            setStatus('执行中...', 'running');
            cs.evalScript(script, function (result) {
                showCompletedResult(parseHostResult(result));
            });
        }

        function finishChunkedExport() {
            setStatus('正在处理并保存 PSD...', 'running');
            cs.evalScript('PSD2UGUI_finishExport()', function (result) {
                var data = parseHostResult(result);
                if (data.error) {
                    cancelAfterError('导出收尾失败: ' + data.error);
                    return;
                }
                if (!data.done) {
                    var finishLabels = {
                        process: '图片处理完成，准备保存 PSD...',
                        save: 'PSD 保存完成，准备生成清单...',
                        manifest: '清单生成完成，准备关闭临时副本...'
                    };
                    setStatus(finishLabels[data.phase] || '正在完成导出...', 'running');
                    setTimeout(finishChunkedExport, 30);
                    return;
                }
                showCompletedResult(data);
            });
        }

        function beginChunkedExport() {
            var script = buildExecuteScript('PSD2UGUI_beginExport', true);
            setStatus('正在准备导出...', 'running');
            cs.evalScript(script, function (result) {
                var data = parseHostResult(result);
                if (data.error) {
                    cancelAfterError('导出准备失败: ' + data.error);
                    return;
                }
                if (data.cancelled) {
                    showCompletedResult(data);
                    return;
                }
                setStatus('导出任务已创建...', 'running');
                setTimeout(prepareChunkedExport, 30);
            });
        }

        function prepareChunkedExport() {
            cs.evalScript('PSD2UGUI_prepareExportStep()', function (result) {
                var data = parseHostResult(result);
                if (data.error) {
                    cancelAfterError('导出准备失败: ' + data.error);
                    return;
                }
                if (data.cancelled) {
                    showCompletedResult(data);
                    return;
                }
                if (data.ready) {
                    setStatus('准备完成，正在烘焙图片...', 'running');
                    setTimeout(finishChunkedExport, 30);
                    return;
                }

                var prepareLabels = {
                    duplicate: '临时副本已创建...',
                    rename: '智能命名完成...',
                    font: '字体检查完成...'
                };
                setStatus(prepareLabels[data.phase] || '正在准备导出...', 'running');
                setTimeout(prepareChunkedExport, 30);
            });
        }

        setStatus('正在加载导出脚本...', 'running');
        reloadHostScript(function (error) {
            if (error) {
                resetRunningState();
                setStatus('导出脚本加载失败: ' + error, 'error');
                return;
            }
            if (mode === 'rename') runLegacyExecute();
            else beginChunkedExport();
        });
    }

    function preview() {
        if (_running) return;
        var moduleName = $moduleSelect.value;
        if (!moduleName) { setStatus('请先选择模块', 'error'); return; }

        var script = 'PSD2UGUI_preview("' + escapeForJsx(moduleName) + '",' + $cbSkipHidden.checked + ')';
        _running = true;
        hideResults();
        setStatus('预览分析中...', 'running');
        disableButtons(true);

        cs.evalScript(script, function (result) {
            _running = false;
            disableButtons(false);

            if (!result || result === 'null' || result === 'EvalScript error.') {
                setStatus('预览失败', 'error');
                return;
            }

            try {
                var data = JSON.parse(result);
                if (data.error) { setStatus('错误: ' + data.error, 'error'); return; }

                // 显示预览列表
                $resultsGrid.innerHTML = '';
                var layers = data.layers || [];
                var limit = Math.min(layers.length, 30);
                for (var i = 0; i < limit; i++) {
                    var div = document.createElement('div');
                    div.className = 'result-item';
                    div.style.gridColumn = '1 / -1';
                    div.innerHTML = '<span class="result-label" style="flex:1;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;" title="' + escapeHtml(layers[i].original) + '">'
                        + escapeHtml(layers[i].original) + '</span>'
                        + '<span class="result-value" style="flex:1;text-align:right;" title="' + escapeHtml(layers[i].proposed) + '">'
                        + '→ ' + escapeHtml(layers[i].proposed) + '</span>';
                    $resultsGrid.appendChild(div);
                }
                if (layers.length > limit) {
                    $resultsTotal.textContent = '显示 ' + limit + '/' + data.total + ' 项 (更多省略)';
                } else {
                    $resultsTotal.textContent = '共 ' + data.total + ' 个图层';
                }

                $resultsSection.classList.add('visible');
                setStatus('预览完成 (' + data.total + ' 个图层)', 'success');
            } catch (e) {
                setStatus('解析失败: ' + e.message, 'error');
            }
        });
    }

    function disableButtons(disabled) {
        $btnBoth.disabled = disabled;
        $btnExport.disabled = disabled;
        $btnRename.disabled = disabled;
        $btnPreview.disabled = disabled;
    }

    function escapeForJsx(str) {
        return str.replace(/\\/g, '\\\\').replace(/"/g, '\\"').replace(/'/g, "\\'");
    }

    function escapeHtml(str) {
        return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    // ╔══════════════════════════════════════════╗
    // ║         事件绑定                          ║
    // ╚══════════════════════════════════════════╝

    $btnBoth.addEventListener('click', function () { execute('both'); });
    $btnExport.addEventListener('click', function () { execute('export'); });
    $btnRename.addEventListener('click', function () { execute('rename'); });
    $btnPreview.addEventListener('click', preview);
    window.addEventListener('beforeunload', function () {
        if (_running) cs.evalScript('PSD2UGUI_cancelExport()');
    });

    // Keyboard shortcut for Enter in modal
    $newModuleName.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') $modalConfirm.click();
    });
    $newModuleId.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') $newModuleName.focus();
    });

    // ╔══════════════════════════════════════════╗
    // ║         文档信息刷新                       ║
    // ╚══════════════════════════════════════════╝

    var docInfoRefreshPending = false;

    function refreshDocInfo() {
        if (_running || docInfoRefreshPending) return;
        docInfoRefreshPending = true;

        cs.evalScript('PSD2UGUI_getDocInfo()', function (result) {
            docInfoRefreshPending = false;
            if (!result || result === 'null' || result === 'EvalScript error.') {
                $docInfoSection.style.display = 'none';
                return;
            }
            try {
                var info = JSON.parse(result);
                if (info.error) { $docInfoSection.style.display = 'none'; return; }

                var layerCountText = String(info.layerCount || 0) + (info.layerCountTruncated ? '+' : '');
                $docInfo.textContent = info.name + '  (' + info.width + '×' + info.height + ', ' + layerCountText + ' layers)';
                $docInfoSection.style.display = '';

                // 自动选择匹配的模块
                if (info.guessModule) {
                    for (var i = 0; i < modules.length; i++) {
                        if (modules[i].id === info.guessModule) {
                            $moduleSelect.value = info.guessModule;
                            break;
                        }
                    }
                }
            } catch (e) {
                $docInfoSection.style.display = 'none';
            }
        });
    }

    // ╔══════════════════════════════════════════╗
    // ║         初始化                            ║
    // ╚══════════════════════════════════════════╝

    // 加载模块列表
    refreshModuleDropdown(loadLastModule());
    loadImageRenderMode();
    loadPersistentModuleSettings();

    // 加载文档信息（初始 + 定时刷新）
    setTimeout(refreshDocInfo, 5000);
    setInterval(refreshDocInfo, 30000);

})();
