#!/usr/bin/env bash
# Keeps .github/shipped-dependencies true to what the packages ship, so that dependabot-auto-merge.yml titles
# fix(deps) every Dependabot update that changes a package, and no other. The list names the shipped
# dependencies, and marks `!` the packages a project under src/ references without shipping anything of them.
#   - every <dependency> of every packed nuspec, the packages' own aside, must be listed as shipped;
#   - every package a project under src/ references must be listed, as shipped or as `!`: a reference added
#     there, a polyfill compiled into the generator for instance, is classified on the pull request that adds it;
#   - every shipped line must be such a dependency or such a reference, and every `!` line such a reference,
#     so that the list names nothing that is gone.
# IDs are compared without regard to case, as NuGet compares them.
#
# Usage: shipped-dependencies-check.sh [packages-dir]   (default artifacts/packages)
# ci.yml runs it after the pack. SHIPPED_DEPENDENCIES overrides the path of the list (tests).
set -euo pipefail

packages_dir="${1:-artifacts/packages}"
list="${SHIPPED_DEPENDENCIES:-.github/shipped-dependencies}"
root="$(cd "$(dirname "$0")/../.." && pwd)"

shopt -s nullglob
packages=("$packages_dir"/*.nupkg)
if (( ${#packages[@]} == 0 )); then
  echo "::error::No package in $packages_dir; run dotnet pack first." >&2
  exit 1
fi

lower() { tr '[:upper:]' '[:lower:]'; }
lines="$(sed -e 's/#.*//' -e 's/[[:space:]]//g' "$list" | sed '/^$/d' | lower)"
shipped="$(sed '/^!/d' <<<"$lines" | LC_ALL=C sort -u)"
not_shipped="$(sed -n 's/^!//p' <<<"$lines" | LC_ALL=C sort -u)"
declared="$(for package in "${packages[@]}"; do unzip -p "$package" '*.nuspec'; done |
  { grep -oP '<dependency\b[^>]*\bid="\K[^"]+' || true; } | lower | sed '/^adcodicem\.valueobjects\(\.\|$\)/d' |
  LC_ALL=C sort -u)"
referenced="$(cat "$root"/src/*/*.csproj "$root"/src/Directory.Build.* |
  { grep -oP '<PackageReference\b[^>]*\bInclude="\K[^"]+' || true; } | lower | LC_ALL=C sort -u)"

status=0
# Prints the lines of $1 that $2 lacks, one per line.
missing_from() { LC_ALL=C comm -23 <(sed '/^$/d' <<<"$1") <(sed '/^$/d' <<<"$2"); }
report() { # <message> <ids>
  local id
  while read -r id; do
    [[ -n "$id" ]] || continue
    echo "::error file=$list::$id ${1}" >&2
    status=1
  done <<<"$2"
}

# A Dependabot security update can pin a transitive dependency of a package that the list does not
# name yet. Its pull request opens as chore(deps), with auto-merge queued, and dependabot-auto-merge.yml,
# which reads the list from the base branch, never retitles it: the title has to change by hand first.
hint=""
if [[ "${GITHUB_HEAD_REF:-}" == dependabot/* ]]; then
  hint=" On this Dependabot pull request, retitle it fix(deps): (fix(deps)!: across a major) before adding the line, or it merges as chore(deps)."
fi
report "is a dependency of a package but is not listed as shipped: a Dependabot update of it would land as chore(deps) and release nothing.$hint" \
  "$(missing_from "$declared" "$shipped")"
report "is referenced by a project under src/ but not listed: list it as shipped, or as \`!\` if no package ships anything of it." \
  "$(missing_from "$referenced" "$(printf '%s\n' "$shipped" "$not_shipped" | LC_ALL=C sort -u)")"
report "is listed as shipped, but no package declares it and no project under src/ references it." \
  "$(missing_from "$shipped" "$(printf '%s\n' "$declared" "$referenced" | LC_ALL=C sort -u)")"
report "is listed as \`!\`, but no project under src/ references it." \
  "$(missing_from "$not_shipped" "$referenced")"
report "is listed both as shipped and as \`!\`." \
  "$(LC_ALL=C comm -12 <(echo "$shipped") <(echo "$not_shipped") | sed '/^$/d')"

if (( status == 0 )); then
  echo "$(grep -c . <<<"$shipped") shipped dependencies listed, the $(grep -c . <<<"$declared") the nuspecs declare among them; $(grep -c . <<<"$not_shipped") referenced packages ship nothing."
fi
exit "$status"
