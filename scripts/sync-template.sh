#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat >&2 <<'TEXT'
usage: bash scripts/sync-template.sh [--template <owner/repository>] [--ref <branch>] [--check]

Compares every file named in the template's managed-files.txt with this repository's copy and, where one
drifted, opens a pull request that takes the template's copy. A workflow caller is compared with the ref
after the @ ignored, so a repository may point a caller at a branch of the shared workflows to try a
change. A game repository passes --template Spellbound-Studios/GameTemplate. --check only reports.
TEXT
}

template="Spellbound-Studios/Template"
ref="main"
check="false"
while [ "$#" -gt 0 ]; do
    case "$1" in
        --template) template="${2:-}"; shift 2 ;;
        --ref) ref="${2:-}"; shift 2 ;;
        --check) check="true"; shift ;;
        *) usage; exit 2 ;;
    esac
done

root="$(git rev-parse --show-toplevel)"
cd "$root"

package_name=""
for package_file in Packages/*/package.json; do
    [ -f "$package_file" ] || continue
    package_name="$(basename "$(dirname "$package_file")")"
done

fetch() {
    gh api "repos/$template/contents/$1?ref=$ref" -H "Accept: application/vnd.github.raw+json"
}

normalise() {
    case "$1" in
        .github/workflows/*) tr -d '\r' | sed -E 's/(uses: [^@]*)@.*/\1/' ;;
        *) tr -d '\r' ;;
    esac
}

fill() {
    if [ -n "$package_name" ] && [ "$package_name" != "PackageName" ]; then
        sed "s|PackageName|$package_name|g; s|packagename|$(printf '%s' "$package_name" | tr 'A-Z' 'a-z')|g"
    else
        cat
    fi
}

needs_fill() {
    case "$1" in
        .releaserc.yaml|Packages/*) return 0 ;;
        *) return 1 ;;
    esac
}

fill_if() {
    if needs_fill "$1"; then
        fill
    else
        cat
    fi
}

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

managed="$(fetch managed-files.txt | tr -d '\r')"
drifted=""
while IFS= read -r path; do
    [ -n "$path" ] || continue
    mkdir -p "$work/$(dirname "$path")"
    if ! fetch "$path" | fill_if "$path" > "$work/$path"; then
        echo "The template has no $path on $ref" >&2
        exit 1
    fi
    if [ ! -f "$path" ]; then
        echo "missing: $path"
        drifted="$drifted$path"$'\n'
    elif ! cmp -s <(normalise "$path" < "$path") <(normalise "$path" < "$work/$path"); then
        echo "drifted: $path"
        drifted="$drifted$path"$'\n'
    fi
done <<< "$managed"

if [ -z "$drifted" ]; then
    echo "succeeded: every managed file matches $template@$ref"
    exit 0
fi

if [ "$check" = "true" ]; then
    exit 1
fi

if [ -n "$(git status --porcelain)" ]; then
    echo "The working tree has changes. Commit or stash them before syncing." >&2
    exit 1
fi

default_branch="$(git symbolic-ref refs/remotes/origin/HEAD 2>/dev/null | sed 's|^refs/remotes/origin/||' || true)"
[ -n "$default_branch" ] || default_branch="main"
branch="sync/template-$(date +%Y%m%d)"
git checkout -b "$branch"

while IFS= read -r path; do
    [ -n "$path" ] || continue
    mkdir -p "$(dirname "$path")"
    cp "$work/$path" "$path"
    git add "$path"
    case "$path" in
        .githooks/*|*.sh) git update-index --chmod=+x "$path" ;;
    esac
done <<< "$drifted"

scope="$(printf '%s' "${package_name:-$(basename "$root")}" | tr 'A-Z' 'a-z')"
address="$(tr -d '\r\n' < line-text-address.txt 2>/dev/null || true)"
title="build($scope): sync the managed files from ${template#*/}"
body="What you will see: the managed files that drifted from $template@$ref, replaced by the template's copies. The list is in the commit. The line text: $address"
renormalise=""
if printf '%s' "$drifted" | grep -qFx '.gitattributes'; then
    renormalise="The line ending rules changed. Once this has merged, a person renormalises the repository once, on a branch of its own: git add --renormalize . and then commit what it staged as build($scope): renormalise the line endings"
    body="$body. $renormalise"
fi
git commit -q -m "$title" -m "$body"
git push -u origin "$branch"
gh pr create --base "$default_branch" --title "$title" --body "$body"
[ -z "$renormalise" ] || echo "$renormalise"
