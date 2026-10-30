# Working in this repository

## What is where

- The repository root is the Unity dev project.
- The package is `Packages/<Name>`. It is the only folder a consumer receives.
- `Assets/_Project` is scaffolding for the dev project and never ships.

## Starting a package from this template

1. `gh repo create Spellbound-Studios/<Name> --template Spellbound-Studios/Template --private --clone`
2. `bash scripts/initialise-package.sh <Name> "<the one-sentence job>"`, with `--kind tool`, `--requires burst,mathematics,urp` or `--presentation` as the sentence needs.
3. `bash scripts/setup.sh`, then open the folder in Unity Hub.

## Pull request titles

Format: `type(scope): what changed`

- Types: `feat fix perf refactor docs test build ci chore`
- Put a `!` before the colon for a breaking change, for example `feat(scope)!: what changed`.
- Pull requests are squash-merged with the title alone, so the title is the commit message.

## Versions

Never edit `version` in `package.json`. The release writes it.

## Checks

- Run `bash scripts/setup.sh` once after cloning.
- Before asking for review, run the Unity tests through the Unity command line.
- `bash scripts/check-line-text-pointer.sh` fails when a file copies the line text instead of pointing at it.
- `bash scripts/check-line-endings.sh` proves the `.gitattributes` rule: a file committed with LF comes back from a clone with CRLF and its byte order mark, and a shell script stays LF.

## Keeping the shell current

- `bash scripts/sync-template.sh` opens a pull request where a file named in `managed-files.txt` drifted from the template; `--check` only reports.
- `bash scripts/update-skeleton.sh` opens a pull request when PackageCheck's skeleton version moved past the one `skeleton-version.txt` records; that file is a record, never a held version.
- `bash scripts/repository-settings.sh Spellbound-Studios/<Name>` prints the lines that set the repository as every repository is set; `--check` reports drift and prints the organisation's Git LFS storage and downloads against the free 10 GB.

## Issues and the board

- Open issues through the forms. An agent writes the body with `bash scripts/render-issue-body.sh <kind> ...` and opens it with `gh issue create --type <Type>`.
- `bash scripts/sprint-report.sh` prints the current sprint from the board.

## Releasing

- A pull request into `beta` releases a prerelease.
- A person moves `main` at a release.

## The rules

The binding rules are the line text at the address in `line-text-address.txt`. Nothing here copies them.
