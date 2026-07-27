@echo off
set SCRIPT_DIR=%~dp0
set OUTPUT_DIR=%SCRIPT_DIR%publish

echo Publishing HyperionTUI...
dotnet publish "%SCRIPT_DIR%HyperionTUI.vbproj" -c Release -o "%OUTPUT_DIR%"

if not exist "%OUTPUT_DIR%\ScriptData" mkdir "%OUTPUT_DIR%\ScriptData"

if not exist "%OUTPUT_DIR%\ScriptData\MasterLogHandler.rex" (
    echo Seeding default MasterLogHandler.rex into publish directory...
    copy "%SCRIPT_DIR%ScriptData\MasterLogHandler.rex" "%OUTPUT_DIR%\ScriptData\" >nul
) else (
    echo Preserving existing user scripts in %OUTPUT_DIR%\ScriptData...
)
