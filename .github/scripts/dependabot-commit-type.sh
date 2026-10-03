#!/usr/bin/env bash
# Prints the prefix a Dependabot pull request's title should carry, so that the commit it is squash-merged into
# releases what it changes (.releaserc.json rates the prefixes):
#
#   fix(deps)!   it updates a dependency listed in .github/shipped-dependencies across a major version: the
#                packages move to a new major of something they ship, a breaking change for consumers (a minor
#                while the packages are 0.x, a major from 1.0);
#   fix(deps)    it updates such a dependency within its major: a patch;
#   chore(deps)  it updates only dependencies the packages do not ship (tests, benchmarks, the sample, build
#                tools), which releases nothing; the prefix dependabot.yml gives every NuGet update;
#   (nothing)    for another ecosystem, whose prefix dependabot.yml already sets right.
#
# Usage: dependabot-commit-type.sh <package-ecosystem> <updated-dependencies-json>
#   Both are outputs of dependabot/fetch-metadata: the ecosystem as the branch name spells it (nuget,
#   github_actions, npm_and_yarn, pip), and the JSON array that describes every dependency the pull request
#   updates, each with its dependencyName and its updateType (version-update:semver-major, -minor or -patch).
#   Each dependency is judged on its own update: a major of a test package next to a minor of a shipped one is
#   fix(deps), not fix(deps)!.
# SHIPPED_DEPENDENCIES overrides the path of the list (tests).
#
# It runs in dependabot-auto-merge.yml, from the base branch, never from the pull request: that workflow holds
# a token that can write to the repository.
set -euo pipefail

ecosystem="${1:?package ecosystem required}"
[[ "$ecosystem" == nuget ]] || exit 0
dependencies="${2:?updated dependencies required}"
list="${SHIPPED_DEPENDENCIES:-.github/shipped-dependencies}"

# One package ID per line, comments, blank lines and the `!` lines (referenced but not shipped) dropped; NuGet
# compares IDs without regard to case.
shipped="$(sed -e 's/#.*//' -e 's/[[:space:]]//g' "$list" | sed -e '/^$/d' -e '/^!/d' | tr '[:upper:]' '[:lower:]')"
if [[ -z "$shipped" ]]; then
  echo "::error::$list lists no dependency." >&2
  exit 1
fi

updates="$(jq -r '.[] | [(.dependencyName | ascii_downcase), (.updateType // "")] | @tsv' <<<"$dependencies")"
if [[ -z "$updates" ]]; then
  echo "::error::fetch-metadata reported no updated dependency." >&2
  exit 1
fi

prefix="chore(deps)"
while IFS=$'\t' read -r name update_type; do
  grep -qxF -- "$name" <<<"$shipped" || continue
  if [[ "$update_type" == version-update:semver-major ]]; then
    prefix="fix(deps)!"
    break
  fi
  prefix="fix(deps)"
done <<<"$updates"
echo "$prefix"
