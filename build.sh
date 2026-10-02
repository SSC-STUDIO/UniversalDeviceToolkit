#!/usr/bin/env bash
set -euo pipefail

# UniversalDeviceToolkit cross-platform build script
# Usage: ./build.sh [configuration] [runtime]
#   configuration: Debug|Release (default: Release)
#   runtime: linux-x64|osx-arm64|osx-x64|win-x64 (default: auto-detect)

CONFIGURATION="${1:-Release}"
RUNTIME="${2:-}"

# Auto-detect runtime
if [ -z "$RUNTIME" ]; then
    case "$(uname -s)" in
        Linux*)  RUNTIME="linux-x64" ;;
        Darwin*)
            if [ "$(uname -m)" = "arm64" ]; then
                RUNTIME="osx-arm64"
            else
                RUNTIME="osx-x64"
            fi
            ;;
        *)       RUNTIME="linux-x64" ;;
    esac
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# UDT_PLATFORM=linux|macos ./build.sh host
#   Publishes the headless Host for the given platform (net10.0 portable build):
#     linux  -> linux-x64,  macos -> osx-arm64 (Apple Silicon) or osx-x64
#   UDT_RID=linux-x64|osx-arm64|osx-x64 overrides the platform mapping directly.
#   Output: UniversalDeviceToolkit.Host/publish/<rid> (self-contained single file)
if [ "${1:-}" = "host" ]; then
    HOST_RID="${UDT_RID:-}"
    if [ -n "$HOST_RID" ]; then
        case "$HOST_RID" in
            linux-x64|osx-arm64|osx-x64)
                ;;
            *)
                echo "Error: unsupported UDT_RID='$HOST_RID' (expected linux-x64|osx-arm64|osx-x64)" >&2
                exit 1
                ;;
        esac
    else
        UDT_PLATFORM="${UDT_PLATFORM:-$(uname -s | tr '[:upper:]' '[:lower:]')}"
        case "$UDT_PLATFORM" in
            linux)
                HOST_RID="linux-x64"
                ;;
            macos|darwin)
                if [ "$(uname -m)" = "arm64" ]; then
                    HOST_RID="osx-arm64"
                else
                    HOST_RID="osx-x64"
                fi
                ;;
            *)
                echo "Error: unsupported UDT_PLATFORM='$UDT_PLATFORM' (expected linux|macos)" >&2
                exit 1
                ;;
        esac
    fi

    HOST_OUTPUT="$SCRIPT_DIR/UniversalDeviceToolkit.Host/publish/$HOST_RID"
    echo "=== Publishing headless Host ($UDT_PLATFORM / $HOST_RID) ==="
    echo "Output: $HOST_OUTPUT"
    echo ""

    dotnet publish "$SCRIPT_DIR/UniversalDeviceToolkit.Host/UniversalDeviceToolkit.Host.csproj" \
        --configuration Release \
        --runtime "$HOST_RID" \
        -p:UDTWindows=false \
        --self-contained true \
        -p:PublishSingleFile=true \
        --output "$HOST_OUTPUT"

    echo ""
    echo "=== Host publish complete: $HOST_OUTPUT ==="
    exit 0
fi

echo "=== UniversalDeviceToolkit Build ==="
echo "Configuration: $CONFIGURATION"
echo "Runtime: $RUNTIME"
echo ""

# Build cross-platform libraries
echo "--- Building cross-platform libraries ---"
dotnet build "$SCRIPT_DIR/UniversalDeviceToolkit.Lib.Abstractions/UniversalDeviceToolkit.Lib.Abstractions.csproj" \
    --configuration "$CONFIGURATION" --verbosity minimal

dotnet build "$SCRIPT_DIR/UniversalDeviceToolkit.Lib.Shared/UniversalDeviceToolkit.Lib.Shared.csproj" \
    --configuration "$CONFIGURATION" --verbosity minimal

# Build CrossPlatform CLI
echo ""
echo "--- Building CrossPlatform CLI ---"
dotnet build "$SCRIPT_DIR/UniversalDeviceToolkit.CrossPlatform/UniversalDeviceToolkit.CrossPlatform.csproj" \
    --configuration "$CONFIGURATION" --verbosity minimal

# Run the portable test suite (UniversalDeviceToolkit.Tests targets the Windows TFM).
echo ""
echo "--- Running cross-platform tests ---"
dotnet test "$SCRIPT_DIR/UniversalDeviceToolkit.CrossPlatform.Tests/UniversalDeviceToolkit.CrossPlatform.Tests.csproj" \
    --configuration "$CONFIGURATION" --verbosity minimal

echo ""
echo "=== Build complete ==="
