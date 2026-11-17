@echo off
chcp 65001 >nul
echo ============================================
echo   PSD2UGUI Smart Export - Installer
echo   Version: 4.4.5
echo ============================================
echo.

:: Set PlayerDebugMode for common CEP versions (unsigned extension support)
:: Newer Photoshop builds may use CSXS 13+
for %%v in (9 10 11 12 13 14 15 16) do (
    reg add "HKCU\SOFTWARE\Adobe\CSXS.%%v" /v PlayerDebugMode /t REG_SZ /d 1 /f >nul 2>&1
)
echo [OK] Debug mode enabled for unsigned CEP extensions (CSXS 9-16)

:: Target directory
set "SRC=%~dp0com.psd2ugui.smartexport"
set "DEST=%APPDATA%\Adobe\CEP\extensions\com.psd2ugui.smartexport"
set "EXT_VERSION=4.4.5"

:: Check source
if not exist "%SRC%" (
    echo [ERROR] Extension folder not found: %SRC%
    pause
    exit /b 1
)

:: Remove old installation
if exist "%DEST%" (
    echo [..] Removing old installation...
    rmdir /s /q "%DEST%" 2>nul
    if exist "%DEST%" (
        echo [WARN] Could not remove old version. Close Photoshop and retry.
        pause
        exit /b 1
    )
)

:: Try symlink first (best for development, requires admin)
mklink /D "%DEST%" "%SRC%" >nul 2>&1
if not errorlevel 1 (
    echo [OK] Symlink created (changes auto-sync)
) else (
    :: Fallback to copy
    echo [..] Symlink failed, copying files...
    xcopy /E /I /Y /Q "%SRC%" "%DEST%" >nul
    if errorlevel 1 (
        echo [ERROR] Copy failed
        pause
        exit /b 1
    )
    echo [OK] Files copied
)

echo.
echo ============================================
echo   Installation complete!
echo   Installed version: %EXT_VERSION%
echo.
echo   If Photoshop is already open, fully quit and relaunch it.
echo   Restart Photoshop, then go to:
echo   Window ^> Extensions (Legacy) ^> PSD2UGUI Smart Export
echo ============================================
pause
