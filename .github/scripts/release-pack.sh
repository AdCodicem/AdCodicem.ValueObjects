#!/usr/bin/env bash
# Packs every publishable project, each at the version of its release train, the one next-version.mjs computed (the
# one semantic-release computes in release.yml's release job, which refuses to publish any other) or the preview
# version derived from it in preview.yml; and checks the packages against the list the release notes link to.
#
# Usage: release-pack.sh <train>=<version>:<release-type>[:<last-version>] ...
#   One argument per train of src/release-trains.json, every train named once
#   (docs/adr/0012-version-the-packages-in-two-trains-independently-of-the-frameworks.md).
#   release-type: major, minor or patch; or repair, for preview.yml completing a version nuget.org already partly
#   has, which was held to its baseline when it was first packed.
#
# MinVer normally derives the version from git tags. At this point in the run the tag for this release does not exist
# yet -- semantic-release creates it later, in another job -- so MinVer would stamp a preview version.
# RELEASE_TRAIN_VERSIONS ("core=1.2.0 satellites=0.4.0"), which src/Directory.Build.props turns into each project's
# MinVerVersionOverride, MinVer's documented hook for an external tool that has already decided the version, gives
# each train its own. It is an environment variable, not an MSBuild property.
set -euo pipefail

(( $# > 0 )) || { echo "::error::release-pack.sh takes <train>=<version>:<release-type>[:<last-version>] per train." >&2; exit 1; }

declare -A version=() type=() last=()
for argument in "$@"; do
  [[ "$argument" =~ ^([a-z][a-z0-9-]*)=([^:]+):(major|minor|patch|repair)(:([^:]*))?$ ]] ||
    { echo "::error::$argument is not <train>=<version>:<major|minor|patch|repair>[:<last-version>]." >&2; exit 1; }
  train="${BASH_REMATCH[1]}"
  [[ ! -v "version[$train]" ]] || { echo "::error::The $train train is given twice." >&2; exit 1; }
  version[$train]="${BASH_REMATCH[2]}"
  type[$train]="${BASH_REMATCH[3]}"
  last[$train]="${BASH_REMATCH[5]}"
done
listed="$(node .github/scripts/release-trains.mjs trains)"
mapfile -t trains < <(cut -d' ' -f1 <<<"$listed")
(( ${#trains[@]} > 0 )) || { echo "::error::release-trains.mjs named no train." >&2; exit 1; }
declare -A known=()
for train in "${trains[@]}"; do
  known[$train]=1
  [[ -v "version[$train]" ]] || { echo "::error::No version for the $train train." >&2; exit 1; }
done
for train in "${!version[@]}"; do
  [[ -v "known[$train]" ]] || { echo "::error::$train is no train of src/release-trains.json." >&2; exit 1; }
done

# Only the packable projects under src/ (the solution filter): no test, benchmark or sample project is
# restored, evaluated or built in the job that produces what gets published.
args=(src/AdCodicem.ValueObjects.Packages.slnf --configuration Release --output artifacts/packages)

# EnablePackageValidation compares the new package's API against a published baseline, train by train. That check
# is meaningful for a patch or a minor, and actively wrong for a release that is *supposed* to break: a major says so
# in its version number, and while a train is still 0.x a minor is the breaking channel by convention, so neither is
# held to the baseline.
declare -A validated=()
for train in "${trains[@]}"; do
  if [[ "${type[$train]}" == repair ]]; then
    echo "$train: completing ${version[$train]}, partly published already: no package validation baseline."
  elif [[ -z "${last[$train]}" ]]; then
    echo "$train: no previous release: no package validation baseline."
  elif [[ "${type[$train]}" == major ]]; then
    echo "$train: major release: no package validation baseline (breaks are intended)."
  elif [[ "${version[$train]}" == 0.* && "${type[$train]}" == minor ]]; then
    echo "$train: pre-1.0 minor: no package validation baseline (0.x minor is the breaking channel)."
  else
    validated[$train]=0
  fi
done

# Only a published package can serve as a baseline, and each package only for itself. v0.1.0 is a tag placed by
# hand with no package behind it, and a package added since its train's last release has no version to compare
# with; restoring a baseline that does not exist fails the pack. So every package of a validated train is looked up,
# and the projects nuget.org lacks at that version are packed without one, through the hook in
# src/Directory.Build.props. A failed lookup stops the script rather than skipping the check.
without=()
declare -A train_of=()
index="$(mktemp)"
trap 'rm -f "$index"' EXIT
for project in src/*/*.csproj; do
  properties="$(dotnet msbuild "$project" -nologo \
    -getProperty:IsPackable -getProperty:PackageId -getProperty:MSBuildProjectName -getProperty:ReleaseTrain)"
  [[ "$(jq -r .Properties.IsPackable <<<"$properties")" == true ]] || continue
  id="$(jq -r .Properties.PackageId <<<"$properties")"
  train="$(jq -r .Properties.ReleaseTrain <<<"$properties")"
  train_of[$id]="$train"
  [[ -v "validated[$train]" ]] || continue
  code="$(curl --silent --show-error --location --retry 3 --output "$index" --write-out '%{http_code}' \
    "https://api.nuget.org/v3-flatcontainer/${id,,}/index.json")"
  if [[ "$code" == 200 ]] && jq -e --arg v "${last[$train],,}" '.versions | index($v)' "$index" >/dev/null; then
    validated[$train]=$(( ${validated[$train]} + 1 ))
  elif [[ "$code" == 200 || "$code" == 404 ]]; then
    echo "$id ${last[$train]} is not on nuget.org: packing it without a baseline."
    without+=("$(jq -r .Properties.MSBuildProjectName <<<"$properties")")
  else
    echo "::error::nuget.org answered HTTP $code for $id; cannot tell whether ${last[$train]} is a baseline." >&2
    exit 1
  fi
done
baselines=()
for train in "${!validated[@]}"; do
  if (( ${validated[$train]} == 0 )); then
    echo "$train: ${last[$train]} was never published to nuget.org: no package validation baseline."
  else
    echo "$train: validating the API of ${validated[$train]} packages against ${last[$train]}."
    baselines+=("$train=${last[$train]}")
  fi
done
(( ${#baselines[@]} == 0 )) || args+=("-p:ReleaseBaselineVersions=${baselines[*]}")
(( ${#without[@]} == 0 )) || args+=("-p:ProjectsWithoutBaseline=${without[*]}")

versions=()
for train in "${trains[@]}"; do
  versions+=("$train=${version[$train]}")
  echo "Packing the $train train at ${version[$train]} (${type[$train]})."
done
RELEASE_TRAIN_VERSIONS="${versions[*]}" dotnet pack "${args[@]}"

# The GitHub Release links every package in RELEASE_PACKAGE_IDS to nuget.org (see release.yml and .releaserc.json).
# That list comes from evaluating the projects, and semantic-release starts with it, so check it against what was
# packed: a mismatch would publish a release whose links 404, or that omits a package. Failing here, in the pack job,
# stops the run before anything is pushed.
if [[ -z "${RELEASE_PACKAGE_IDS:-}" ]]; then
  echo "::error::RELEASE_PACKAGE_IDS is not set; the release notes would carry no nuget.org links. Run .github/scripts/package-ids.sh first." >&2
  exit 1
fi
expected="$(tr -s ' ' '\n' <<<"$RELEASE_PACKAGE_IDS" | sed '/^$/d' | LC_ALL=C sort -u)"
evaluated="$(printf '%s\n' "${!train_of[@]}" | LC_ALL=C sort -u)"
if [[ "$expected" != "$evaluated" ]]; then
  echo "::error::RELEASE_PACKAGE_IDS does not match the packable projects under src/." >&2
  diff <(echo "$expected") <(echo "$evaluated") >&2 || true
  exit 1
fi

# A silent mismatch here would publish a preview-numbered package as a stable release, or a package at another
# train's version, so every package must be there at exactly its train's version, and nothing else: the push takes
# each package by its id and version, and the island installs whatever the folder holds.
status=0
for id in "${!train_of[@]}"; do
  if [[ ! -f "artifacts/packages/$id.${version[${train_of[$id]}]}.nupkg" ]]; then
    echo "::error::Expected artifacts/packages/$id.${version[${train_of[$id]}]}.nupkg, the ${train_of[$id]} train's version: RELEASE_TRAIN_VERSIONS did not take, or a project under src/ is missing from src/AdCodicem.ValueObjects.Packages.slnf." >&2
    status=1
  fi
done
shopt -s nullglob
all=(artifacts/packages/*.nupkg)
if (( ${#all[@]} != ${#train_of[@]} )); then
  echo "::error::artifacts/packages holds packages other than this run's:" >&2
  printf '  %s\n' "${all[@]}" >&2
  status=1
fi
(( status == 0 )) || { ls -1 artifacts/packages/ >&2 || true; exit 1; }
echo "Packed ${#train_of[@]} packages, matching RELEASE_PACKAGE_IDS: ${versions[*]}."
