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


echo ============================================================
echo  Publish complete. Output in: %OUTPUT_DIR%
echo ============================================================
pause
