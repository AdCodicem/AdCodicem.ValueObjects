#!/usr/bin/env bash
# Prints the PackageId of every packable project under src/, one per line, sorted.
#
# Usage: package-ids.sh
#
# release.yml exports the result as RELEASE_PACKAGE_IDS before semantic-release
# starts, because the GitHub Release body is rendered from the environment
# semantic-release was started with: nothing a prepare step computes can reach
# it. The list is read from MSBuild rather than written down, so a package added
# to src/ gets its nuget.org link without anyone remembering to add it, and
# release-pack.sh checks it against what was actually packed before anything is
# published. Needs a restored tree, since IsPackable can come from an import.
set -euo pipefail

cd "$(dirname "$0")/../.."

for project in src/*/*.csproj; do
  dotnet msbuild "$project" -nologo -getProperty:IsPackable -getProperty:PackageId |
    jq -r 'select(.Properties.IsPackable == "true") | .Properties.PackageId'
done | LC_ALL=C sort -u
