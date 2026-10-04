#!/bin/bash
# Installs the .NET SDK global.json names, so a Claude Code on the web session can build, test and format.
# Idempotent: a session resumed from the cached container finds the SDK and only re-exports its paths.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

cd "$CLAUDE_PROJECT_DIR"

DOTNET_ROOT="$HOME/.dotnet"
# global.json rolls forward to the latest feature band, as actions/setup-dotnet resolves it in CI: install the newest
# SDK of the major it names.
version=$(sed -n 's/.*"version": *"\([^"]*\)".*/\1/p' global.json | head -n 1)
channel="${version%.*}"            # 10.0.100 -> 10.0

if ! "$DOTNET_ROOT/dotnet" --list-sdks 2>/dev/null | grep -q "^${channel}\."; then
  installer=$(mktemp)
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$installer"
  bash "$installer" --channel "$channel" --install-dir "$DOTNET_ROOT" --no-path
  rm -f "$installer"
fi

export DOTNET_ROOT PATH="$DOTNET_ROOT:$HOME/.dotnet/tools:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  {
    echo "export DOTNET_ROOT=\"$DOTNET_ROOT\""
    echo "export PATH=\"$DOTNET_ROOT:\$HOME/.dotnet/tools:\$PATH\""
    echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1'
  } >> "$CLAUDE_ENV_FILE"
fi

# Restore once, so the container snapshot taken after this hook already holds the packages.
dotnet restore AdCodicem.ValueObjects.slnx
