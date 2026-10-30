#!/usr/bin/env bash
set -euo pipefail

if [ "${1:-}" = "--title" ]; then
    line="${2:-}"
elif [ -n "${1:-}" ] && [ -f "$1" ]; then
    line="$(head -n 1 "$1" | tr -d '\r')"
else
    echo "usage: check-commit-message.sh <message-file> | --title \"<text>\"" >&2
    exit 2
fi

case "$line" in
    "Merge "*) exit 0 ;;
esac

if printf '%s\n' "$line" | grep -Eq '^(feat|fix|perf|refactor|docs|test|build|ci|chore)(\([a-z0-9._-]+\))?!?: .{1,100}$'; then
    exit 0
fi

echo "Invalid title: $line" >&2
echo "Expected: type(scope): what changed, with an optional ! before the colon for a breaking change" >&2
echo "Allowed types: feat fix perf refactor docs test build ci chore" >&2
exit 1
