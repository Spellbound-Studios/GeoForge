#!/usr/bin/env bash
set -euo pipefail

root="$(git rev-parse --show-toplevel)"
cd "$root"

git config core.hooksPath .githooks
echo "Commit message hook enabled from .githooks"

editor_version="$(grep '^m_EditorVersion:' ProjectSettings/ProjectVersion.txt | head -n 1 | sed 's/^m_EditorVersion:[[:space:]]*//' | tr -d '\r')"
echo "This project needs Unity editor $editor_version"

if command -v unity >/dev/null 2>&1; then
    echo "The agent context install is not available yet. It will run here when the core context-install operation exists."
else
    echo "The Unity command line is not installed. Install it with: curl -fsSL https://unity.com/install.sh | bash (or on Windows PowerShell: irm https://unity.com/install.ps1 | iex)"
fi
exit 0
