#!/usr/bin/env bash
set -euo pipefail

usage() {
    echo 'usage: bash scripts/sprint-report.sh [--sprint "Sprint N"] [--org <organisation>] [--board <number>]' >&2
}

organisation="Spellbound-Studios"
board=13
wanted=""
while [ "$#" -gt 0 ]; do
    case "$1" in
        --sprint) wanted="${2:-}"; shift 2 ;;
        --org) organisation="${2:-}"; shift 2 ;;
        --board) board="${2:-}"; shift 2 ;;
        *) usage; exit 2 ;;
    esac
done

today="$(date +%F)"

sprints="$(gh api graphql -f query="query { organization(login: \"$organisation\") { projectV2(number: $board) { fields(first: 50) { nodes { ... on ProjectV2IterationField { name configuration { duration iterations { title startDate duration } completedIterations { title startDate duration } } } } } } } }" \
    --jq '.data.organization.projectV2.fields.nodes[] | select(.name == "Sprint") | .configuration | (.iterations + .completedIterations)[] | "\(.title)\t\(.startDate)\t\(.duration)"')"

if [ -z "$sprints" ]; then
    echo "The board has no Sprint field" >&2
    exit 1
fi

epoch() {
    date -d "$1" +%s 2>/dev/null || date -j -f %Y-%m-%d "$1" +%s
}

sprint=""
start=""
length=""
while IFS=$'\t' read -r title first_day days; do
    [ -n "$title" ] || continue
    if [ -n "$wanted" ]; then
        if [ "$title" = "$wanted" ]; then
            sprint="$title"; start="$first_day"; length="$days"
        fi
    else
        begin="$(epoch "$first_day")"
        finish=$((begin + days * 86400))
        now="$(epoch "$today")"
        if [ "$now" -ge "$begin" ] && [ "$now" -lt "$finish" ]; then
            sprint="$title"; start="$first_day"; length="$days"
        fi
    fi
done <<< "$sprints"

if [ -z "$sprint" ]; then
    if [ -n "$wanted" ]; then
        echo "No sprint is called $wanted. Sprints:" >&2
    else
        echo "No sprint runs today ($today). Sprints:" >&2
    fi
    printf '%s\n' "$sprints" | awk -F '\t' '{ print "  " $1 " starts " $2 }' >&2
    exit 1
fi

day=$((($(epoch "$today") - $(epoch "$start")) / 86400 + 1))
last_day="$(date -d "$start + $((length - 1)) days" +%F 2>/dev/null || date -j -v+$((length - 1))d -f %Y-%m-%d "$start" +%F)"

query='query($endCursor: String) { organization(login: "ORG") { projectV2(number: BOARD) { items(first: 100, after: $endCursor) { pageInfo { hasNextPage endCursor } nodes {
  status: fieldValueByName(name: "Status") { ... on ProjectV2ItemFieldSingleSelectValue { name } }
  sprint: fieldValueByName(name: "Sprint") { ... on ProjectV2ItemFieldIterationValue { title } }
  priority: fieldValueByName(name: "Priority") { ... on ProjectV2ItemFieldSingleSelectValue { name } }
  area: fieldValueByName(name: "Area") { ... on ProjectV2ItemFieldSingleSelectValue { name } }
  gates: fieldValueByName(name: "Gates") { ... on ProjectV2ItemFieldNumberValue { number } }
  tokens: fieldValueByName(name: "Tokens") { ... on ProjectV2ItemFieldNumberValue { number } }
  content { ... on Issue { number title state repository { name } issueType { name } assignees(first: 5) { nodes { login } } blockedBy(first: 10) { totalCount nodes { number repository { name } } } } ... on DraftIssue { title } }
} } } } }'
query="${query/ORG/$organisation}"
query="${query/BOARD/$board}"

rows="$(gh api graphql --paginate -f query="$query" \
    --jq '.data.organization.projectV2.items.nodes[] | select(.sprint.title == "'"$sprint"'") | [
        (.status.name // "No status"),
        (.content.issueType.name // "Draft"),
        ((.content.repository.name // "draft") + (if .content.number then "#" + (.content.number | tostring) else "" end)),
        (.content.title // "untitled"),
        ((.content.assignees.nodes // []) | map(.login) | join(",") | if . == "" then "nobody" else . end),
        (.priority.name // ""),
        (.gates.number // 0),
        (.tokens.number // 0),
        ((.content.blockedBy.nodes // []) | map(select(.repository != null) | .repository.name + "#" + (.number | tostring)) | join(",")),
        (.content.state // "")
    ] | @tsv')"

if [ "$day" -lt 1 ]; then
    echo "$sprint, $start to $last_day, starts in $((1 - day)) days"
elif [ "$day" -gt "$length" ]; then
    echo "$sprint, $start to $last_day, ended $((day - length)) days ago"
else
    echo "$sprint, $start to $last_day, day $day of $length"
fi
echo

if [ -z "$rows" ]; then
    echo "Nothing is in this sprint."
    exit 0
fi

printf '%s\n' "$rows" | awk -F '\t' '
{
    count[$1]++; gates[$1] += $7; tokens += $8; total_gates += $7
    if ($1 == "Done") done_gates += $7
    line[$1] = line[$1] sprintf("  %s %s (%s%s, %s gates)%s\n", $3, $4, $5, ($6 == "" ? "" : ", " $6), $7, ($9 == "" ? "" : ", blocked by " $9))
    if ($9 != "") blocked = blocked sprintf("  %s %s waits on %s\n", $3, $4, $9)
    split($5, people, ",")
    for (i in people) if ($1 == "In progress") progress[people[i]] = progress[people[i]] sprintf("  %s %s\n", $3, $4)
}
END {
    order[1] = "Done"; order[2] = "In review"; order[3] = "In progress"; order[4] = "Ready"; order[5] = "Backlog"; order[6] = "No status"
    for (i = 1; i <= 6; i++) if (count[order[i]] > 0) printf "%s: %d items, %d gates\n%s\n", order[i], count[order[i]], gates[order[i]], line[order[i]]
    printf "Gates done %d of %d. Tokens recorded %.2f million.\n", done_gates, total_gates, tokens
    if (blocked != "") printf "\nBlocked:\n%s", blocked
    first = 1
    for (person in progress) { if (first) { printf "\nIn progress by person:\n"; first = 0 } printf "%s:\n%s", person, progress[person] }
}'
