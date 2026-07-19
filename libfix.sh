#!/bin/bash

# Define the library we need to link
LIB_NAME="libc.so.6"

# Find the location of libc.so.6
# This searches common library paths to ensure portability
LIBC_PATH=$(find /lib /usr/lib /lib64 /usr/lib64 -name "$LIB_NAME" -type f 2>/dev/null | head -n 1)

if [ -f "$LIBC_PATH" ]; then
    echo "Found $LIB_NAME at: $LIBC_PATH"
    
    # Check if a file/link named libdl.so already exists to prevent errors
    if [ -e "libdl.so" ] || [ -L "libdl.so" ]; then
        echo "Removing existing libdl.so..."
        rm "libdl.so"
    fi
    
    # Create the symbolic link
    ln -s "$LIBC_PATH" "libdl.so"
    echo "Successfully created symlink: libdl.so -> $LIBC_PATH"
else
    echo "Error: Could not locate $LIB_NAME on this system."
    exit 1
fi