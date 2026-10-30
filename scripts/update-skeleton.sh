#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat >&2 <<'TEXT'
usage: bash scripts/update-skeleton.sh [--repository <owner/repository>] [--ref <branch>] [--path <folder>] [--check]

Compares the skeleton version recorded in skeleton-version.txt with the version PackageCheck's branch
declares in PackageCheckKit and, when the skeleton moved, copies every skeleton file into
Packages/PackageName with the template's values filled in, records the new version and opens a pull
request. skeleton-version.txt is a record of which skeleton the template holds, never a held version:
the template's manifest follows PackageCheck by branch. --check only reports.
TEXT
}

repository="Spellbound-Studios/PackageCheck"
ref="beta"
skeleton_path="Packages/PackageCheck/Skeleton~"
check="false"
while [ "$#" -gt 0 ]; do
    case "$1" in
        --repository) repository="${2:-}"; shift 2 ;;
        --ref) ref="${2:-}"; shift 2 ;;
        --path) skeleton_path="${2:-}"; shift 2 ;;
        --check) check="true"; shift ;;
        *) usage; exit 2 ;;
    esac
done

root="$(git rev-parse --show-toplevel)"
cd "$root"

if [ ! -f scripts/initialise-package.sh ]; then
    echo "succeeded: only the template takes the skeleton; a package made from it holds its bootstrap already"
    exit 0
fi

name_prefix="Spellbound."
foundation_package="com.spellboundstudios.core"
foundation_define="SPELLBOUND_CORE"
copyright_line="// Copyright (c) Spellbound Studios. All rights reserved."

fill() {
    sed "s|COPYRIGHT_LINE|$copyright_line|g; s|NAME_PREFIX|$name_prefix|g; s|FOUNDATION_PACKAGE|$foundation_package|g; s|FOUNDATION_DEFINE|$foundation_define|g; s|FOUNDATION_RANGE|CORE_RANGE|g"
}

fetch() {
    gh api "repos/$repository/contents/$1?ref=$2" -H "Accept: application/vnd.github.raw+json"
}

recorded="$(tr -d '\r\n' < skeleton-version.txt)"
if [ "$recorded" = "none" ] || [ -z "$recorded" ]; then
    recorded="0"
fi
if ! printf '%s' "$recorded" | grep -Eq '^[0-9]+$'; then
    echo "skeleton-version.txt must record a skeleton version number, it records: $recorded" >&2
    exit 1
fi

tip="$(gh api "repos/$repository/commits/$ref" --jq .sha)"
kit_path="$(dirname "$skeleton_path")/Editor/PackageCheckKit.cs"
latest="$(fetch "$kit_path" "$tip" | tr -d '\r' | sed -nE 's/^.*SkeletonVersion = ([0-9]+);.*$/\1/p')"
if [ -z "$latest" ]; then
    echo "$kit_path on $repository@$ref declares no SkeletonVersion" >&2
    exit 1
fi

if [ "$latest" = "$recorded" ]; then
    echo "succeeded: the skeleton is at version $recorded, which $repository@$ref declares"
    exit 0
fi

if [ "$latest" -lt "$recorded" ]; then
    echo "skeleton-version.txt records version $recorded and $repository@$ref declares $latest" >&2
    exit 1
fi

files="$(gh api "repos/$repository/git/trees/$tip?recursive=1" --jq '.tree[] | select(.type == "blob") | .path' | grep "^$skeleton_path/" || true)"
if [ -z "$files" ]; then
    echo "$repository@$ref holds nothing under $skeleton_path" >&2
    exit 1
fi

echo "The skeleton moved from version $recorded to $latest. Its files at $repository@$ref:"
printf '%s\n' "$files" | sed "s|^$skeleton_path/|  |"

if [ "$check" = "true" ]; then
    exit 1
fi

if [ -n "$(git status --porcelain)" ]; then
    echo "The working tree has changes. Commit or stash them before updating the skeleton." >&2
    exit 1
fi

git checkout -b "skeleton/$latest"

while IFS= read -r folder; do
    [ -n "$folder" ] || continue
    case "$folder" in
        Runtime|Editor|Tests|Samples~|Documentation~) continue ;;
    esac
    target="Packages/PackageName/$(printf '%s' "$folder" | fill)"
    git rm -r -q --ignore-unmatch "$target"
    rm -rf "$target"
done < <(printf '%s\n' "$files" | sed "s|^$skeleton_path/||" | grep '/' | cut -d/ -f1 | sort -u)

while IFS= read -r file; do
    [ -n "$file" ] || continue
    target="Packages/PackageName/$(printf '%s' "${file#$skeleton_path/}" | fill)"
    mkdir -p "$(dirname "$target")"
    fetch "$file" "$tip" | tr -d '\r' | fill > "$target"
    git add "$target"
done <<< "$files"

printf '%s\n' "$latest" > skeleton-version.txt
git add skeleton-version.txt

default_branch="$(git symbolic-ref refs/remotes/origin/HEAD 2>/dev/null | sed 's|^refs/remotes/origin/||' || true)"
[ -n "$default_branch" ] || default_branch="main"
address="$(tr -d '\r\n' < line-text-address.txt 2>/dev/null || true)"
title="build(template): take PackageCheck's skeleton version $latest"
body="What you will see: every skeleton file of $repository@$ref under Packages/PackageName with the template's values filled in, and skeleton-version.txt recording version $latest. The line text: $address"
git commit -q -m "$title" -m "$body"
git push -u origin "skeleton/$latest"
gh pr create --base "$default_branch" --title "$title" --body "$body"
