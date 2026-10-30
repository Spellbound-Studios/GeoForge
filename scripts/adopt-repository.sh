#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
    echo "usage: bash <path-to-template>/scripts/adopt-repository.sh <Name>" >&2
    exit 2
fi

name="$1"
lower="$(printf '%s' "$name" | tr 'A-Z' 'a-z')"
template="$(cd "$(dirname "$0")/.." && pwd)"
project="$PWD"

fail() {
    echo "$1" >&2
    exit 2
}

[ -d "Assets/$name/.git" ] || fail "Assets/$name/.git is not a directory: run this from the dev project root"
[ ! -e ".git" ] || fail "$project/.git already exists"
[ ! -e "Packages/$name" ] || fail "Packages/$name already exists"
[ -z "$(git -C "Assets/$name" status --porcelain)" ] || fail "Assets/$name has uncommitted changes"
[ -f "ProjectSettings/ProjectVersion.txt" ] || fail "ProjectSettings/ProjectVersion.txt is missing: this is not a Unity project"
echo "Preconditions passed"
echo "Warning: the Unity editor must be closed on this project before continuing"

mv "Assets/$name/.git" .git
echo "Moved the git folder to the project root"

git checkout -b layout/packages-folder
echo "Created branch layout/packages-folder"

mkdir -p Packages
mv "Assets/$name" "Packages/$name"
rm -f "Assets/$name.meta"
echo "Moved Assets/$name to Packages/$name"

for file in .gitignore .gitattributes .editorconfig; do
    rm -f "Packages/$name/$file" "Packages/$name/$file.meta"
done
echo "Removed the package copies of .gitignore, .gitattributes and .editorconfig"

mkdir -p Assets/_Project
rm -rf Assets/TutorialInfo Assets/TutorialInfo.meta Assets/Readme.asset Assets/Readme.asset.meta
for entry in Assets/* Assets/.[!.]*; do
    [ -e "$entry" ] || continue
    entry_name="$(basename "$entry")"
    case "$entry_name" in
        _Project|Samples|Samples.meta) continue ;;
    esac
    mv "$entry" "Assets/_Project/$entry_name"
done
echo "Moved project content into Assets/_Project"

if [ ! -f Assets/_Project.meta ]; then
    folder_guid="$(od -An -N16 -tx1 /dev/urandom | tr -d ' 
')"
    printf 'fileFormatVersion: 2
guid: %s
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
' "$folder_guid" > Assets/_Project.meta
    echo "Wrote Assets/_Project.meta"
fi

for settings in ProjectSettings/*.asset; do
    [ -f "$settings" ] || continue
    if grep -q 'Assets/\(Scenes\|Settings\)/' "$settings"; then
        sed -i 's|Assets/Scenes/|Assets/_Project/Scenes/|g; s|Assets/Settings/|Assets/_Project/Settings/|g' "$settings"
        echo "Rewrote moved asset paths in $settings"
    fi
done

copy_missing() {
    if [ ! -e "$2" ]; then
        mkdir -p "$(dirname "$2")"
        cp -R "$1" "$2"
    fi
}

cp "$template/.gitignore" .gitignore
cp "$template/.gitattributes" .gitattributes
copy_missing "$template/.editorconfig" .editorconfig
if [ ! -e .releaserc.yaml ]; then
    sed "s|PackageName|$name|g" "$template/.releaserc.yaml" > .releaserc.yaml
fi
for file in CODEOWNERS AGENTS.md line-text-address.txt managed-files.txt managed-unity-packages.txt; do
    copy_missing "$template/$file" "$file"
done
if [ ! -e skeleton-version.txt ]; then
    printf 'none\n' > skeleton-version.txt
fi
for folder in .githooks scripts; do
    mkdir -p "$folder"
    cp -R "$template/$folder/." "$folder/"
done
rm -f scripts/initialise-package.sh
echo "Copied the managed files from the template"
echo "Not copied: .github/workflows, because the shared workflows they call do not exist yet; sync-template.sh adds them when they do"

git config core.hooksPath .githooks
echo "Enabled the commit message hook"

git add -A
renames="$(git diff --cached --name-status | grep -c '^R' || true)"
additions="$(git diff --cached --name-status | grep -c '^A' || true)"
deletions="$(git diff --cached --name-status | grep -c '^D' || true)"
echo "Staged: $renames renames, $additions additions, $deletions deletions"

if git diff --cached --name-only | grep -Eq '^(Library|Temp|Logs|obj|UserSettings)/'; then
    echo "Staged paths include generated folders. Everything stays staged for inspection." >&2
    exit 1
fi

line_text="$(tr -d '\r\n' < line-text-address.txt)"
title="build($lower)!: move the package to Packages/$name"
body="The repository root is now the dev project and the package is an embedded package at Packages/$name. Consumers install it with ?path=/Packages/$name. The line text: $line_text"

git commit -q -m "$title" -m "$body"
echo "Committed on layout/packages-folder"

default_branch="$(git symbolic-ref refs/remotes/origin/HEAD 2>/dev/null | sed 's|^refs/remotes/origin/||' || true)"
[ -n "$default_branch" ] || default_branch="main"

echo "Next steps:"
echo "git push -u origin layout/packages-folder"
echo "gh pr create --base $default_branch --title \"$title\" --body \"$body\""
echo "After the pull request merges, every consumer takes this manifest line:"
echo "\"com.spellboundstudios.$lower\": \"https://github.com/Spellbound-Studios/$name.git?path=/Packages/$name#$default_branch\""
