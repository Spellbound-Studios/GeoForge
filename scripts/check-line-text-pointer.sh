#!/usr/bin/env bash
set -uo pipefail

root="$(cd "${1:-.}" && pwd)"
cd "$root"

canonical="https://github.com/Spellbound-Studios/.github/tree/main/line"
failures=0
fail() {
    echo "failed: $1"
    failures=$((failures + 1))
}

if [ -f line/Line.md ] && [ -f line/Roadmap.md ]; then
    address="$canonical"
elif [ ! -f line-text-address.txt ]; then
    fail "line-text-address.txt is missing"
    address=""
else
    address="$(tr -d '\r\n' < line-text-address.txt)"
    lines="$(tr -d '\r' < line-text-address.txt | grep -c . || true)"
    [ "$lines" = 1 ] || fail "line-text-address.txt must hold one line, it holds $lines"
    case "$address" in
        https://github.com/Spellbound-Studios/.github/*) ;;
        *) fail "line-text-address.txt does not point into the organisation's .github repository: $address" ;;
    esac
fi

if [ -n "$address" ]; then
    shopt -s nullglob
    for file in AGENTS.md CONTRIBUTING.md Packages/*/AGENTS.md Packages/*/README.md .github/ISSUE_TEMPLATE/*.yml; do
        [ -f "$file" ] || continue
        case "$file" in
            .github/ISSUE_TEMPLATE/bug.yml|.github/ISSUE_TEMPLATE/config.yml) continue ;;
        esac
        if ! grep -qF "$address" "$file" && ! grep -qF 'line-text-address.txt' "$file"; then
            fail "$file does not point at the line text ($address)"
        fi
    done
    shopt -u nullglob
fi

copies="$(grep -rlF --exclude-dir=.git --exclude-dir=Library --exclude-dir=line --exclude-dir=Tests --exclude-dir='*~' --exclude=check-line-text-pointer.sh -e '## Rules of the line' -e '## The homes procedure' . 2>/dev/null | sed 's|^\./||' || true)"
for file in $copies; do
    fail "$file copies the line text instead of pointing at it"
done

if [ "$failures" = 0 ]; then
    echo "succeeded: every pointer names the line text and nothing copies it"
    exit 0
fi
exit 1
