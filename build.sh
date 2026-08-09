#!/usr/bin/env bash

bash --version 2>&1 | head -n 1

set -euo pipefail
SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)

BUILD_PROJECT_FILE="$SCRIPT_DIR/build/_build.csproj"
TEMP_DIRECTORY="$SCRIPT_DIR/.nuke/temp"
DOTNET_GLOBAL_FILE="$SCRIPT_DIR/global.json"
DOTNET_INSTALL_URL="https://dot.net/v1/dotnet-install.sh"
DOTNET_CHANNEL="STS"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_MULTILEVEL_LOOKUP=0
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export NUKE_TELEMETRY_OPTOUT=1

first_json_value() {
    perl -nle 'print $1 if m{"'"$1"'"\s*:\s*"([^"]+)",?}' <<< "${*:2}"
}

normalize_dotnet_path() {
    local candidate="$1"
    if command -v cygpath >/dev/null 2>&1; then
        candidate="$(cygpath --windows "$candidate")"
        if [[ -f "${candidate}.exe" ]]; then
            candidate="${candidate}.exe"
        fi
    fi

    printf '%s' "$candidate"
}

if command -v dotnet >/dev/null 2>&1 && dotnet --version >/dev/null 2>&1; then
    export DOTNET_EXE
    DOTNET_EXE="$(normalize_dotnet_path "$(command -v dotnet)")"
else
    DOTNET_INSTALL_FILE="$TEMP_DIRECTORY/dotnet-install.sh"
    mkdir -p "$TEMP_DIRECTORY"
    curl --fail --silent --show-error --location --output "$DOTNET_INSTALL_FILE" "$DOTNET_INSTALL_URL"
    chmod +x "$DOTNET_INSTALL_FILE"

    if [[ -f "$DOTNET_GLOBAL_FILE" ]]; then
        DOTNET_VERSION="$(first_json_value version "$(cat "$DOTNET_GLOBAL_FILE")")"
        if [[ -z "$DOTNET_VERSION" ]]; then
            unset DOTNET_VERSION
        fi
    fi

    DOTNET_DIRECTORY="$TEMP_DIRECTORY/dotnet-unix"
    if [[ -v DOTNET_VERSION ]]; then
        "$DOTNET_INSTALL_FILE" --install-dir "$DOTNET_DIRECTORY" --version "$DOTNET_VERSION" --no-path
    else
        "$DOTNET_INSTALL_FILE" --install-dir "$DOTNET_DIRECTORY" --channel "$DOTNET_CHANNEL" --no-path
    fi

    export DOTNET_EXE
    DOTNET_EXE="$(normalize_dotnet_path "$DOTNET_DIRECTORY/dotnet")"
fi

echo "Microsoft (R) .NET SDK version $("$DOTNET_EXE" --version)"

"$DOTNET_EXE" build "$BUILD_PROJECT_FILE" --disable-build-servers --property:UseSharedCompilation=false --nologo --verbosity quiet
"$DOTNET_EXE" run --project "$BUILD_PROJECT_FILE" --no-build -- "$@"
