#!/usr/bin/env bash
# Prints the PackageId of every packable project under src/, one per line, sorted.
#
# Usage: package-ids.sh [--trains | --prefixes] [root]
#   --trains    prints "<id> <train>" instead, the release train MSBuild reads off src/release-trains.json for the
#               project (docs/adr/0012-version-the-packages-in-two-trains-independently-of-the-frameworks.md), which
#               ci.yml compares with release-trains.mjs's reading. A commit older than the list has no train.
#   --prefixes  prints "<id> <train> <tag prefix>", the MinVerTagPrefix src/Directory.Build.props gives the project
#               from its train, which ci.yml compares with release-trains.mjs's tag prefix of that train.
#
# release.yml exports the result as RELEASE_PACKAGE_IDS before semantic-release
# starts, because the GitHub Release body is rendered from the environment
# semantic-release was started with: nothing a prepare step computes can reach
# it. The list is read from MSBuild rather than written down, so a package added
# to src/ gets its nuget.org link without anyone remembering to add it, and
# release-pack.sh checks it against what was actually packed before anything is
# published. preview-gate.sh also passes the root of an older commit's tree, to
# learn what that commit packed.
#
# Evaluation only, no restore: IsPackable comes from src/Directory.Build.props
# and the projects themselves. A package that set it from its build props would
# need a restored tree first.
set -euo pipefail

mode=ids
case "${1:-}" in
  --trains) mode=trains; shift ;;
  --prefixes) mode=prefixes; shift ;;
esac
cd "${1:-$(dirname "$0")/../..}"

for project in src/*/*.csproj; do
  dotnet msbuild "$project" -nologo -getProperty:IsPackable -getProperty:PackageId -getProperty:ReleaseTrain \
    -getProperty:MinVerTagPrefix |
    jq -r --arg mode "$mode" 'select(.Properties.IsPackable == "true") | .Properties |
      if $mode == "trains" then "\(.PackageId) \(.ReleaseTrain)"
      elif $mode == "prefixes" then "\(.PackageId) \(.ReleaseTrain) \(.MinVerTagPrefix)"
      else .PackageId end'
done | LC_ALL=C sort -u
