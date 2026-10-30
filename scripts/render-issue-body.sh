#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat >&2 <<'TEXT'
usage: bash scripts/render-issue-body.sh <kind> [--<field> "<text>"]...

Prints an issue body with the same headings the issue form of that kind produces, so an issue an agent
opens with gh reads like one a person opened through the form.

kinds and fields:
  epic      --done-when --why-it-matters --stories --waits-on
  story     --outcome --verified-facts --rules --regression-contract --done-when --waits-on
  task      --story --what-to-do --done-when
  bug       --what-happens --steps --expected --where --environment --log
  decision  --question --recommendation --alternative --what-waits --answer

example:
  gh issue create --repo Spellbound-Studios/Roadmap --type Story --title "Players can rejoin a lobby" \
      --body "$(bash scripts/render-issue-body.sh story --outcome "..." --done-when "...")"
TEXT
}

if [ "$#" -lt 1 ]; then
    usage
    exit 2
fi

kind="$1"
shift

case "$kind" in
    epic) fields="done-when:Done when|why-it-matters:Why it matters|stories:Stories|waits-on:Waits on" ;;
    story) fields="outcome:Outcome|verified-facts:Verified facts|rules:Rules|regression-contract:Regression contract|done-when:Done when|waits-on:Waits on" ;;
    task) fields="story:Story|what-to-do:What to do|done-when:Done when" ;;
    bug) fields="what-happens:What happens|steps:Steps|expected:Expected|where:Where|environment:Environment|log:Log" ;;
    decision) fields="question:Question|recommendation:Recommendation|alternative:Alternative|what-waits:What waits on it|answer:Answer" ;;
    *) echo "Unknown kind: $kind" >&2; usage; exit 2 ;;
esac

declare -A values
while [ "$#" -gt 0 ]; do
    case "$1" in
        --*)
            name="${1#--}"
            if ! printf '%s' "|$fields|" | grep -q "|$name:"; then
                echo "Unknown field for $kind: --$name" >&2
                usage
                exit 2
            fi
            values["$name"]="${2:-}"
            shift 2
            ;;
        *) usage; exit 2 ;;
    esac
done

first=1
IFS='|' read -r -a entries <<< "$fields"
for entry in "${entries[@]}"; do
    name="${entry%%:*}"
    label="${entry#*:}"
    if [ -n "${values[$name]+set}" ] && [ -n "${values[$name]}" ]; then
        [ "$first" = 1 ] || printf '\n'
        first=0
        printf '### %s\n\n%s\n' "$label" "${values[$name]}"
    fi
done
