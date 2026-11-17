/**
 * Minimal CSInterface wrapper for Adobe CEP extensions.
 * Wraps the native __adobe_cep__ API injected by the CEP runtime.
 */
(function () {
    'use strict';

    window.SystemPath = {
        USER_DATA: 'userData',
        COMMON_FILES: 'commonFiles',
        MY_DOCUMENTS: 'myDocuments',
        APPLICATION: 'application',
        EXTENSION: 'extension',
        HOST_APPLICATION: 'hostApplication'
    };

    function CSInterface() { }

    CSInterface.prototype.evalScript = function (script, callback) {
        if (!callback) callback = function () { };
        if (window.__adobe_cep__) {
            window.__adobe_cep__.evalScript(script, callback);
        }
    };

    CSInterface.prototype.getSystemPath = function (pathType) {
        if (window.__adobe_cep__) {
            return decodeURI(window.__adobe_cep__.getSystemPath(pathType));
        }
        return '';
    };

    CSInterface.prototype.closeExtension = function () {
        if (window.__adobe_cep__) {
            window.__adobe_cep__.closeExtension();
        }
    };

    CSInterface.prototype.getHostEnvironment = function () {
        if (window.__adobe_cep__) {
            try { return JSON.parse(window.__adobe_cep__.getHostEnvironment()); }
            catch (e) { return null; }
        }
        return null;
    };

    CSInterface.prototype.addEventListener = function (type, listener) {
        if (window.__adobe_cep__) {
            window.__adobe_cep__.addEventListener(type, listener);
        }
    };

    CSInterface.prototype.requestOpenExtension = function (id) {
        if (window.__adobe_cep__) {
            window.__adobe_cep__.requestOpenExtension(id);
        }
    };

    window.CSInterface = CSInterface;
})();
