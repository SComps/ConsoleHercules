@echo off
REM ============================================================
REM  HyperionTUI - Windows Native Publish Script
REM  Builds a self-contained native executable for the current host
REM ============================================================

set SCRIPT_DIR=%~dp0
set OUTPUT_DIR=%SCRIPT_DIR%publish

echo ============================================================
echo  Publishing HyperionTUI for Windows
echo ============================================================
echo.

dotnet publish "%SCRIPT_DIR%HyperionTUI.vbproj" -c Release -o "%OUTPUT_DIR%"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Windows build failed!
    exit /b %ERRORLEVEL%
) else (
    echo.
    echo ============================================================
    echo  Build complete! Output files are in: %OUTPUT_DIR%
    echo ============================================================
)
