#!/bin/bash
# ============================================================
#  HyperionTUI - AOT Publish Script (Linux)
#  Publishes self-contained native AOT binaries for:
#    - Linux x64
#    - Linux ARM64
# ============================================================

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
OUTPUT_DIR="$SCRIPT_DIR/publish"

echo "============================================================"
echo " HyperionTUI AOT Publish (Linux)"
echo "============================================================"
echo

# --- Linux x64 ---
echo "[1/2] Publishing for linux-x64..."
dotnet publish "$SCRIPT_DIR/HyperionTUI.vbproj" -c Release -r linux-x64 -o "$OUTPUT_DIR/linux-x64"
if [ $? -eq 0 ]; then
    echo "OK: linux-x64"
else
    echo "FAILED: linux-x64"
fi
echo

# --- Linux ARM64 ---
echo "[2/2] Publishing for linux-arm64..."
dotnet publish "$SCRIPT_DIR/HyperionTUI.vbproj" -c Release -r linux-arm64 -o "$OUTPUT_DIR/linux-arm64"
if [ $? -eq 0 ]; then
    echo "OK: linux-arm64"
else
    echo "FAILED: linux-arm64"
fi
echo

echo "============================================================"
echo " Publish complete. Output in: $OUTPUT_DIR"
echo "============================================================"
