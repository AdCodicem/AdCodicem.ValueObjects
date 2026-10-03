#!/usr/bin/env bash
# Checks that a directory holds exactly one complete set of packages, all at one
# version, before any of them is pushed.
#
# Usage: verify-packages.sh <packages-dir> <version> <expected-ids>
#   <expected-ids>: the space- or newline-separated package ids package-ids.sh prints.
#   The version must be of the X.Y.Z-preview.N shape preview.yml publishes; ci.yml,
#   which checks the set MinVer packed and publishes nothing, sets ALLOW_ANY_VERSION=true.
#
# Previews publish all the packages or none, so the set is checked as a whole:
# one id missing, one too many, or one at another version stops the run before
# the first push, since nuget.org takes no package back.
set -euo pipefail

packages_dir="${1:?packages directory required}"
version="${2:?version required}"
expected_ids="${3:?expected package ids required}"

if [[ "${ALLOW_ANY_VERSION:-false}" != true && ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+-preview\.[0-9]+$ ]]; then
  echo "::error::$version is not a preview version (X.Y.Z-preview.N). Stable releases go through release.yml." >&2
  exit 1
fi

shopt -s nullglob
expected="$(tr -s ' \n' '\n' <<<"$expected_ids" | sed '/^$/d' | LC_ALL=C sort -u)"
status=0

# Every file must be a package of this version: anything else is a leftover
# from another pack that the push glob would otherwise pick up.
for file in "$packages_dir"/*; do
  name="$(basename "$file")"
  if [[ "$name" != *."$version".nupkg && "$name" != *."$version".snupkg ]]; then
    echo "::error::$name is not a package of $version." >&2
    status=1
  fi
done

packed="$(for package in "$packages_dir"/*."$version".nupkg; do
  basename "$package" ".$version.nupkg"
done | LC_ALL=C sort -u)"
if [[ "$expected" != "$packed" ]]; then
  echo "::error::The packages do not match the packable projects under src/." >&2
  diff <(echo "$expected") <(echo "$packed") >&2 || true
  status=1
fi

# The nuspec is what nuget.org reads, so check the version there too, not only
# in the file name.
for package in "$packages_dir"/*."$version".nupkg; do
  declared="$(unzip -p "$package" '*.nuspec' | sed -n 's:.*<version>\(.*\)</version>.*:\1:p' | head -n 1)"
  if [[ "$declared" != "$version" ]]; then
    echo "::error::$(basename "$package") declares version '$declared'." >&2
    status=1
  fi
done

(( status == 0 )) && echo "$(wc -l <<<"$packed") packages at $version, matching the packable projects."
exit "$status"
