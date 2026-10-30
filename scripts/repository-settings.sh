#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat >&2 <<'TEXT'
usage: bash scripts/repository-settings.sh <owner/repository> [--art-registry] [--app-id <id>] [--template-repository] [--require-unity-tests] [--check | --apply]

Prints the gh lines that set a repository the way every repository of the organisation is set, for a person
to type: squash merge with the title alone, merged head branches deleted, the default branch changed only
through a pull request with the unity-tests status and code owner review on the judgment fields, and the
App on the bypass list. --art-registry adds the art registry's team agreement: an artist pushes
art/<login>-<slug> branches only and main changes by pull request after the tests, which nothing enforces
on the plan. --template-repository marks the repository as a template. --require-unity-tests adds the
required unity-tests status, once the local gate that posts it exists. --check reads the settings back,
reports drift and prints the organisation's Git LFS storage and downloads against the free 10 GB. --apply
runs the lines instead of printing them, replacing a ruleset of the same name.
TEXT
}

if [ "$#" -lt 1 ]; then
    usage
    exit 2
fi

repository="$1"
shift
art_registry="false"
app_id=""
template_repository="false"
require_unity_tests="false"
mode="print"
while [ "$#" -gt 0 ]; do
    case "$1" in
        --art-registry) art_registry="true"; shift ;;
        --app-id) app_id="${2:-}"; shift 2 ;;
        --template-repository) template_repository="true"; shift ;;
        --require-unity-tests) require_unity_tests="true"; shift ;;
        --check) mode="check"; shift ;;
        --apply) mode="apply"; shift ;;
        *) usage; exit 2 ;;
    esac
done

case "$repository" in
    */*) ;;
    *) echo "The repository is owner/name: $repository" >&2; exit 2 ;;
esac

plan_message="$(gh api "repos/$repository/rulesets" 2>&1 >/dev/null || true)"
rulesets_available="true"
case "$plan_message" in
    *"Upgrade to GitHub"*) rulesets_available="false" ;;
esac

bypass_actors='{ "actor_type": "OrganizationAdmin", "bypass_mode": "always" }'
if [ -n "$app_id" ]; then
    bypass_actors="$bypass_actors,
    { \"actor_id\": $app_id, \"actor_type\": \"Integration\", \"bypass_mode\": \"always\" }"
fi

status_rule=""
if [ "$require_unity_tests" = "true" ]; then
    status_rule=',
    { "type": "required_status_checks", "parameters": {
        "strict_required_status_checks_policy": false,
        "required_status_checks": [ { "context": "unity-tests" } ] } }'
fi

default_branch_ruleset() {
    cat <<BODY
{
  "name": "Default branch",
  "target": "branch",
  "enforcement": "active",
  "bypass_actors": [
    $bypass_actors
  ],
  "conditions": { "ref_name": { "include": ["~DEFAULT_BRANCH"], "exclude": [] } },
  "rules": [
    { "type": "deletion" },
    { "type": "non_fast_forward" },
    { "type": "pull_request", "parameters": {
        "required_approving_review_count": 0,
        "dismiss_stale_reviews_on_push": false,
        "require_code_owner_review": true,
        "require_last_push_approval": false,
        "required_review_thread_resolution": false,
        "allowed_merge_methods": ["squash"] } }$status_rule
  ]
}
BODY
}

merge_settings_line() {
    echo "gh api -X PATCH repos/$repository -F allow_squash_merge=true -F allow_merge_commit=false -F allow_rebase_merge=false -f squash_merge_commit_title=PR_TITLE -f squash_merge_commit_message=BLANK -F delete_branch_on_merge=true"
}

art_registry_agreement() {
    echo "agreement: the art registry's art branch rule (artists push art/<login>-<slug> only, main by pull request after the tests) is a team agreement on this plan"
}

git_lfs_usage() {
    owner="${repository%%/*}"
    read -r year month day hour <<< "$(date -u '+%Y %m %d %H')"
    hours="$(awk -v day="$day" -v hour="$hour" 'BEGIN { hours = (day - 1) * 24 + hour; if (hours < 1) hours = 1; print hours }')"
    refusal_file="$(mktemp)"
    if usage_totals="$(gh api "organizations/$owner/settings/billing/usage?year=$year&month=$month" --jq '[([(.usageItems // [])[] | select(.product == "git_lfs" and .sku == "Git LFS storage") | .quantity] | add // 0), ([(.usageItems // [])[] | select(.product == "git_lfs" and .sku == "Git LFS bandwidth") | .quantity] | add // 0)] | @tsv' 2>"$refusal_file")"; then
        read -r storage downloads <<< "$(printf '%s\n' "$usage_totals" | awk -F '\t' -v hours="$hours" '{ printf "%.1f %.1f\n", $1 / hours, $2 }')"
        echo "git lfs storage: $storage GB of 10 (this month's average across the organisation)"
        echo "git lfs downloads: $downloads GB of 10 this month"
    else
        echo "git lfs: the usage read was refused ($(head -n 1 "$refusal_file"))"
    fi
    rm -f "$refusal_file"
}

if [ "$mode" = "check" ]; then
    failures=0
    drift() {
        echo "drift: $1"
        failures=$((failures + 1))
    }
    settings="$(gh api "repos/$repository" --jq '[.allow_squash_merge, .allow_merge_commit, .allow_rebase_merge, .delete_branch_on_merge, .squash_merge_commit_title, .squash_merge_commit_message, .default_branch, .is_template] | @tsv')"
    IFS=$'\t' read -r squash merge rebase delete_head squash_title squash_message default_branch is_template <<< "$settings"
    [ "$squash" = "true" ] || drift "squash merge is off"
    [ "$merge" = "false" ] || drift "merge commits are allowed"
    [ "$rebase" = "false" ] || drift "rebase merges are allowed"
    [ "$delete_head" = "true" ] || drift "merged head branches are kept"
    [ "$squash_title" = "PR_TITLE" ] || drift "the squash commit title is $squash_title, not the pull request title alone"
    [ "$squash_message" = "BLANK" ] || drift "the squash commit message is $squash_message, not blank"
    if [ "$template_repository" = "true" ] && [ "$is_template" != "true" ]; then
        drift "the repository is not marked as a template"
    fi
    if [ "$rulesets_available" = "false" ]; then
        echo "unenforced: rulesets are not available on this repository's plan, so the default branch $default_branch is a team agreement"
    else
        names="$(gh api "repos/$repository/rulesets" --jq '.[].name')"
        printf '%s\n' "$names" | grep -qx 'Default branch' || drift "the ruleset 'Default branch' is missing"
        if printf '%s\n' "$names" | grep -qx 'Default branch'; then
            id="$(gh api "repos/$repository/rulesets" --jq '.[] | select(.name == "Default branch") | .id')"
            rules="$(gh api "repos/$repository/rulesets/$id" --jq '[.enforcement, ([.rules[].type] | sort | join(",")), ([.rules[] | select(.type == "required_status_checks") | .parameters.required_status_checks[].context] | join(",") | if . == "" then "none" else . end), ([.rules[] | select(.type == "pull_request") | .parameters.require_code_owner_review] | join(",") | if . == "" then "none" else . end)] | @tsv')"
            IFS=$'\t' read -r enforcement rule_types statuses code_owner <<< "$rules"
            [ "$enforcement" = "active" ] || drift "the ruleset 'Default branch' is $enforcement, not active"
            if [ "$require_unity_tests" = "true" ]; then
                [ "$rule_types" = "deletion,non_fast_forward,pull_request,required_status_checks" ] || drift "the ruleset 'Default branch' holds the rules $rule_types"
                [ "$statuses" = "unity-tests" ] || drift "the required status is '$statuses', not unity-tests"
            else
                [ "$rule_types" = "deletion,non_fast_forward,pull_request" ] || drift "the ruleset 'Default branch' holds the rules $rule_types"
                [ "$statuses" = "none" ] || drift "the status '$statuses' is required before the local gate exists"
            fi
            [ "$code_owner" = "true" ] || drift "code owner review is not required"
        fi
    fi
    if [ "$art_registry" = "true" ]; then
        art_registry_agreement
    fi
    git_lfs_usage
    if [ "$failures" = 0 ]; then
        echo "succeeded: $repository is set as every repository is"
        exit 0
    fi
    exit 1
fi

if [ "$mode" = "apply" ]; then
    gh api -X PATCH "repos/$repository" -F allow_squash_merge=true -F allow_merge_commit=false -F allow_rebase_merge=false \
        -f squash_merge_commit_title=PR_TITLE -f squash_merge_commit_message=BLANK -F delete_branch_on_merge=true --silent
    echo "merge settings applied to $repository"
    if [ "$art_registry" = "true" ]; then
        art_registry_agreement
    fi
    if [ "$template_repository" = "true" ]; then
        gh repo edit "$repository" --template
        echo "$repository is a template repository"
    fi
    if [ "$rulesets_available" = "false" ]; then
        echo "unenforced: rulesets are not available on this repository's plan, so the default branch is a team agreement"
        exit 0
    fi
    replace_ruleset() {
        gh api "repos/$repository/rulesets" --jq ".[] | select(.name == \"$1\") | .id" | while read -r id; do
            [ -n "$id" ] || continue
            gh api -X DELETE "repos/$repository/rulesets/$id" --silent
        done
        gh api "repos/$repository/rulesets" --input - --silent
        echo "ruleset '$1' set on $repository"
    }
    default_branch_ruleset | replace_ruleset "Default branch"
    exit 0
fi

if [ "$rulesets_available" = "false" ]; then
    echo "# $repository is private and the organisation's plan does not allow rulesets or branch protection on it."
    echo "# The merge settings below apply; the ruleset will be refused until the plan changes or the repository is public."
    echo
fi

merge_settings_line
if [ "$template_repository" = "true" ]; then
    echo "gh repo edit $repository --template"
fi
echo
echo "gh api repos/$repository/rulesets --input - <<'BODY'"
default_branch_ruleset
echo "BODY"

if [ "$art_registry" = "true" ]; then
    echo
    echo "# The art registry keeps a team agreement where a ruleset would stand:"
    echo "# an artist holds the write role and pushes branches named art/<login>-<slug> only;"
    echo "# main changes only through a pull request that an engineer merges after running the registry's tests through the Unity command line;"
    echo "# nothing enforces this on the plan, and --check reports it as an agreement."
fi
