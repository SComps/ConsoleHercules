#!/bin/bash
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
OUTPUT_DIR="$SCRIPT_DIR/publish"

echo "Publishing HyperionTUI..."
dotnet publish "$SCRIPT_DIR/HyperionTUI.vbproj" -c Release -o "$OUTPUT_DIR"

mkdir -p "$OUTPUT_DIR/ScriptData"

if [ ! -f "$OUTPUT_DIR/ScriptData/MasterLogHandler.rex" ]; then
    echo "Seeding default MasterLogHandler.rex into publish directory..."
    cp "$SCRIPT_DIR/ScriptData/MasterLogHandler.rex" "$OUTPUT_DIR/ScriptData/"
else
    echo "Preserving existing user scripts in $OUTPUT_DIR/ScriptData..."
fi
