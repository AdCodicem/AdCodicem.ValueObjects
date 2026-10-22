#!/usr/bin/env bash
# Checks that a directory holds exactly one complete set of packages, each at the version of its release train,
# before any of them is pushed.
#
# Usage: verify-packages.sh <packages-dir> <packages>
#   <packages>: the space- or newline-separated <id>=<version> pairs of the set, every packable project at its
#   train's version (release-trains.mjs versions prints them). Each version must be of the X.Y.Z-preview.N shape
#   preview.yml publishes. ci.yml, which checks the set MinVer packed and publishes nothing, and release.yml, whose
#   versions are stable, set ALLOW_ANY_VERSION=true.
#
# Previews publish all the packages or none, so the set is checked as a whole: one id missing, one too many, or one
# at another version stops the run before the first push, since nuget.org takes no package back. The pairs name each
# file: the trains share the folder and may share a number
# (docs/adr/0012-version-the-packages-in-two-trains-independently-of-the-frameworks.md).
set -euo pipefail

packages_dir="${1:?packages directory required}"
pairs="${2:?package=version pairs required}"

declare -A expected=()
status=0
for pair in $pairs; do
  if [[ ! "$pair" =~ ^([A-Za-z0-9_.-]+)=([0-9A-Za-z.+-]+)$ ]]; then
    echo "::error::$pair is not <id>=<version>." >&2
    exit 1
  fi
  id="${BASH_REMATCH[1]}" version="${BASH_REMATCH[2]}"
  [[ ! -v "expected[$id]" ]] || { echo "::error::$id is given twice." >&2; exit 1; }
  expected[$id]="$version"
  if [[ "${ALLOW_ANY_VERSION:-false}" != true && ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+-preview\.[0-9]+$ ]]; then
    echo "::error::$id $version is not a preview version (X.Y.Z-preview.N). Stable releases go through release.yml." >&2
    status=1
  fi
done
(( ${#expected[@]} > 0 )) || { echo "::error::No package to check." >&2; exit 1; }

# Every file must be a package of the set: anything else is a leftover from another pack that would ride along.
shopt -s nullglob
declare -A known=()
for id in "${!expected[@]}"; do
  known["$id.${expected[$id]}.nupkg"]=1
  known["$id.${expected[$id]}.snupkg"]=1
done
for file in "$packages_dir"/*; do
  name="$(basename "$file")"
  if [[ ! -v "known[$name]" ]]; then
    echo "::error::$name is no package of the set." >&2
    status=1
  fi
done

# The nuspec is what nuget.org reads, so check the version there too, not only in the file name.
for id in "${!expected[@]}"; do
  package="$packages_dir/$id.${expected[$id]}.nupkg"
  if [[ ! -f "$package" ]]; then
    echo "::error::$(basename "$package") is missing." >&2
    status=1
    continue
  fi
  declared="$(unzip -p "$package" '*.nuspec' | sed -n 's:.*<version>\(.*\)</version>.*:\1:p' | head -n 1)"
  if [[ "$declared" != "${expected[$id]}" ]]; then
    echo "::error::$(basename "$package") declares version '$declared'." >&2
    status=1
  fi
done

if (( status == 0 )); then
  echo "${#expected[@]} packages, each at its train's version: $(printf '%s\n' "${expected[@]}" | LC_ALL=C sort -u | paste -sd ' ' -)."
fi
exit "$status"
