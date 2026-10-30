#!/usr/bin/env bash
set -euo pipefail

root="$(git rev-parse --show-toplevel)"
cd "$root"

crlf_extensions="cs asmdef asmref json uss uxml md yml yaml"
lf_files="sample.sh .githooks/commit-msg"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
origin="$work/origin"
clone="$work/clone"

git init -q "$origin"
git -C "$origin" config user.name "check-line-endings"
git -C "$origin" config user.email "check-line-endings@localhost"
git -C "$origin" config core.autocrlf false
git -C "$origin" config core.safecrlf false
cp .gitattributes "$origin/.gitattributes"
mkdir -p "$origin/.githooks" "$origin/Assets/_Project"
for extension in $crlf_extensions; do
    printf '\357\273\277first line\nsecond line\n' > "$origin/Sample.$extension"
done
for file in $lf_files; do
    printf '#!/usr/bin/env bash\necho sample\n' > "$origin/$file"
done
printf 'first line\n' > "$origin/Assets/_Project/figure.png"
git -C "$origin" add -A
git -C "$origin" commit -q -m "test(template): the line ending fixtures"
git -c core.autocrlf=false -c core.eol=lf clone -q "$origin" "$clone"
git -C "$clone" config core.autocrlf false
git -C "$clone" config core.eol lf

failures=0
fail() {
    echo "failed: $1"
    failures=$((failures + 1))
}

carriage_returns() {
    tr -cd '\r' < "$1" | wc -c | tr -d ' '
}

for extension in $crlf_extensions; do
    file="Sample.$extension"
    stored="$(git -C "$clone" ls-files --eol "$file")"
    printf '%s\n' "$stored" | grep -E "^i/lf[[:space:]]+w/crlf[[:space:]]+attr/text eol=crlf[[:space:]]" > /dev/null \
        || fail "$file is not stored with LF, checked out with CRLF and declared text eol=crlf: $stored"
    [ "$(carriage_returns "$clone/$file")" -eq 2 ] || fail "$file does not end every line with CRLF"
    [ "$(head -c 3 "$clone/$file" | od -An -tx1 | tr -d ' \n')" = "efbbbf" ] || fail "$file lost its byte order mark"
done

for file in $lf_files; do
    stored="$(git -C "$clone" ls-files --eol "$file")"
    printf '%s\n' "$stored" | grep -E "^i/lf[[:space:]]+w/lf[[:space:]]+attr/text eol=lf[[:space:]]" > /dev/null \
        || fail "$file is not stored and checked out with LF and declared text eol=lf: $stored"
    [ "$(carriage_returns "$clone/$file")" -eq 0 ] || fail "$file carries a carriage return"
done

attributes="$(git -C "$clone" check-attr text filter -- Assets/_Project/figure.png)"
printf '%s\n' "$attributes" | grep -q 'text: unset' || fail "the LFS pattern no longer wins over text=auto: $attributes"
printf '%s\n' "$attributes" | grep -q 'filter: lfs' || fail "the LFS pattern no longer sets the lfs filter: $attributes"

if [ "$failures" = 0 ]; then
    echo "succeeded: every declared text file comes back from a clone with CRLF and its byte order mark, scripts and hooks stay LF, and the LFS patterns still win"
    exit 0
fi
exit 1
