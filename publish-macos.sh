#!/bin/bash
# ============================================================
#  HyperionTUI - macOS Native Publish Script
#  Builds a self-contained native executable for the current host
# ============================================================

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
OUTPUT_DIR="$SCRIPT_DIR/publish"

echo "============================================================"
echo " Publishing HyperionTUI for macOS"
echo "============================================================"
echo

dotnet publish "$SCRIPT_DIR/HyperionTUI.vbproj" -c Release -o "$OUTPUT_DIR"

if [ $? -eq 0 ]; then
    echo
    echo "============================================================"
    echo " Build complete! Output files are in: $OUTPUT_DIR"
    echo "============================================================"
else
    echo
    echo "[ERROR] macOS build failed!"
    exit 1
fi
