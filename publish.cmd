@echo off
REM ============================================================
REM  HyperionTUI - Cross-Platform AOT Publish Script (Windows)
REM  Publishes self-contained native AOT binaries for:
REM    - Windows x64
REM    - Linux x64
REM    - Linux ARM64
REM    - macOS x64
REM    - macOS ARM64
REM ============================================================

set PROJECT_DIR=%~dp0
set OUTPUT_DIR=%PROJECT_DIR%publish

echo ============================================================
echo  HyperionTUI AOT Publish
echo ============================================================
echo.

REM --- Windows x64 ---
echo [1/5] Publishing for win-x64...
dotnet publish "%PROJECT_DIR%HyperionTUI.vbproj" -c Release -r win-x64 -o "%OUTPUT_DIR%\win-x64"
if %ERRORLEVEL% NEQ 0 (
    echo FAILED: win-x64
) else (
    echo OK: win-x64
)
echo.

REM --- Linux x64 ---
echo [2/5] Publishing for linux-x64...
dotnet publish "%PROJECT_DIR%HyperionTUI.vbproj" -c Release -r linux-x64 -o "%OUTPUT_DIR%\linux-x64"
if %ERRORLEVEL% NEQ 0 (
    echo FAILED: linux-x64
) else (
    echo OK: linux-x64
)
echo.

REM --- Linux ARM64 ---
echo [3/5] Publishing for linux-arm64...
dotnet publish "%PROJECT_DIR%HyperionTUI.vbproj" -c Release -r linux-arm64 -o "%OUTPUT_DIR%\linux-arm64"
if %ERRORLEVEL% NEQ 0 (
    echo FAILED: linux-arm64
) else (
    echo OK: linux-arm64
)
echo.

REM --- macOS x64 ---
echo [4/5] Publishing for osx-x64...
dotnet publish "%PROJECT_DIR%HyperionTUI.vbproj" -c Release -r osx-x64 -o "%OUTPUT_DIR%\osx-x64"
if %ERRORLEVEL% NEQ 0 (
    echo FAILED: osx-x64
) else (
    echo OK: osx-x64
)
echo.

REM --- macOS ARM64 (Apple Silicon) ---
echo [5/5] Publishing for osx-arm64...
dotnet publish "%PROJECT_DIR%HyperionTUI.vbproj" -c Release -r osx-arm64 -o "%OUTPUT_DIR%\osx-arm64"
if %ERRORLEVEL% NEQ 0 (
    echo FAILED: osx-arm64
) else (
    echo OK: osx-arm64
)
echo.

echo ============================================================
echo  Publish complete. Output in: %OUTPUT_DIR%
echo ============================================================
pause
