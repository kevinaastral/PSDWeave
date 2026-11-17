#!/bin/bash
set -e

EXT_ID="com.psd2ugui.smartexport"
EXT_VERSION="4.4.5"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SRC="${SCRIPT_DIR}/${EXT_ID}"
DEST_ROOT="${HOME}/Library/Application Support/Adobe/CEP/extensions"
DEST="${DEST_ROOT}/${EXT_ID}"
SYSTEM_DEST_ROOT="/Library/Application Support/Adobe/CEP/extensions"
SYSTEM_DEST="${SYSTEM_DEST_ROOT}/${EXT_ID}"

pause_if_interactive() {
    if [ -t 0 ]; then
        echo
        read -r -p "Press Enter to exit..."
    fi
}

echo "============================================"
echo "  PSD2UGUI Smart Export - macOS Installer"
echo "  Version: ${EXT_VERSION}"
echo "============================================"
echo

# Enable unsigned CEP extensions for common Photoshop/CEP versions.
# Photoshop may use different CSXS domains depending on version.
for version in 9 10 11 12 13 14 15 16; do
    defaults write "com.adobe.CSXS.${version}" PlayerDebugMode 1 >/dev/null 2>&1 || true
done
echo "[OK] Debug mode enabled for unsigned CEP extensions (CSXS 9-16)"

if pgrep -if "Adobe Photoshop" >/dev/null 2>&1; then
    echo "[ERROR] Adobe Photoshop is still running."
    echo "        Fully quit Photoshop first, then run this installer again."
    echo "        Otherwise CEP may keep using the old cached panel."
    pause_if_interactive
    exit 1
fi

if [ ! -d "${SRC}" ]; then
    echo "[ERROR] Extension folder not found: ${SRC}"
    pause_if_interactive
    exit 1
fi

if ! grep -q 'btnAddModule' "${SRC}/index.html" 2>/dev/null; then
    echo "[ERROR] Source extension is missing the add-module UI: ${SRC}/index.html"
    echo "        Please update the repository files before installing."
    pause_if_interactive
    exit 1
fi

if ! grep -q 'PSD2UGUI_saveModuleSettings' "${SRC}/jsx/host.jsx" 2>/dev/null; then
    echo "[ERROR] Source extension is missing persistent module settings: ${SRC}/jsx/host.jsx"
    echo "        Please update the repository files before installing."
    pause_if_interactive
    exit 1
fi

if [ -e "${SYSTEM_DEST}" ] || [ -L "${SYSTEM_DEST}" ]; then
    echo "[..] Removing old system-wide installation..."
    if [ -w "${SYSTEM_DEST_ROOT}" ]; then
        rm -rf "${SYSTEM_DEST}"
    else
        sudo rm -rf "${SYSTEM_DEST}"
    fi
    if [ -e "${SYSTEM_DEST}" ] || [ -L "${SYSTEM_DEST}" ]; then
        echo "[ERROR] Could not remove old system-wide version:"
        echo "        ${SYSTEM_DEST}"
        echo "        Photoshop may keep loading that stale extension, so installation is stopped."
        pause_if_interactive
        exit 1
    fi
fi

mkdir -p "${DEST_ROOT}"

if [ -e "${DEST}" ] || [ -L "${DEST}" ]; then
    echo "[..] Removing old installation..."
    rm -rf "${DEST}"
    if [ -e "${DEST}" ] || [ -L "${DEST}" ]; then
        echo "[WARN] Could not remove old version. Close Photoshop and retry."
        pause_if_interactive
        exit 1
    fi
fi

if ln -s "${SRC}" "${DEST}" 2>/dev/null; then
    echo "[OK] Symlink created (changes auto-sync)"
else
    echo "[..] Symlink failed, copying files..."
    if command -v ditto >/dev/null 2>&1; then
        ditto "${SRC}" "${DEST}"
    else
        cp -R "${SRC}" "${DEST}"
    fi
    echo "[OK] Files copied"
fi

echo "[..] Clearing Photoshop CEP cache..."
for cache_dir in \
    "${HOME}/Library/Caches/CSXS" \
    "${HOME}/Library/Caches/CEP" \
    "${HOME}/Library/Application Support/Adobe/CEPServiceManager4" \
    "${HOME}/Library/Application Support/Adobe/CEPServiceManager5" \
    "${HOME}/Library/Application Support/Adobe/CEPServiceManager6" \
    "${HOME}/Library/Application Support/Adobe/CEPServiceManager7" \
    "${HOME}/Library/Application Support/Adobe/CEPServiceManager8" \
    "${HOME}/Library/Application Support/Adobe/CEPServiceManager9" \
    "${HOME}/Library/Application Support/Adobe/CEPServiceManager10" \
    "${HOME}/Library/Application Support/Adobe/CEPServiceManager11" \
    "${HOME}/Library/Application Support/Adobe/CEPServiceManager12" \
    "${HOME}/Library/Application Support/Adobe/CEPServiceManager13" \
    "${HOME}/Library/Application Support/Adobe/CEPServiceManager14" \
    "${HOME}/Library/Application Support/Adobe/CEPServiceManager15" \
    "${HOME}/Library/Application Support/Adobe/CEPServiceManager16"; do
    if [ -e "${cache_dir}" ]; then
        rm -rf "${cache_dir}" >/dev/null 2>&1 || true
    fi
done
echo "[OK] CEP cache cleared"

if [ -f "${DEST}/index.html" ] && grep -q 'btnAddModule' "${DEST}/index.html" 2>/dev/null; then
    echo "[OK] Add-module UI verified in installed extension"
else
    echo "[WARN] Could not verify add-module UI in installed extension."
    echo "       Installed path: ${DEST}"
fi

echo
echo "============================================"
echo "  Installation complete!"
echo "  Installed version: ${EXT_VERSION}"
echo
echo "  If Photoshop is already open, fully quit and relaunch it."
echo "  Restart Photoshop, then go to:"
echo "  Window > Extensions (Legacy) > PSD2UGUI Smart Export"
echo "============================================"
pause_if_interactive
