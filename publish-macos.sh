#!/bin/bash
# ============================================================
#  HyperionTUI - AOT Publish Script (macOS)
#  Publishes self-contained native AOT binaries for:
#    - macOS x64 (Intel)
#    - macOS ARM64 (Apple Silicon)
# ============================================================

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
OUTPUT_DIR="$SCRIPT_DIR/publish"

echo "============================================================"
echo " HyperionTUI AOT Publish (macOS)"
echo "============================================================"
echo

# --- macOS x64 (Intel) ---
echo "[1/2] Publishing for osx-x64..."
dotnet publish "$SCRIPT_DIR/HyperionTUI.vbproj" -c Release -r osx-x64 -o "$OUTPUT_DIR/osx-x64"
if [ $? -eq 0 ]; then
    echo "OK: osx-x64"
else
    echo "FAILED: osx-x64"
fi
echo

# --- macOS ARM64 (Apple Silicon) ---
echo "[2/2] Publishing for osx-arm64..."
dotnet publish "$SCRIPT_DIR/HyperionTUI.vbproj" -c Release -r osx-arm64 -o "$OUTPUT_DIR/osx-arm64"
if [ $? -eq 0 ]; then
    echo "OK: osx-arm64"
else
    echo "FAILED: osx-arm64"
fi
echo

echo "============================================================"
echo " Publish complete. Output in: $OUTPUT_DIR"
echo "============================================================"
