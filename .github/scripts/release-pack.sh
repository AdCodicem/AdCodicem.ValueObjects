#!/usr/bin/env bash
# Packs every publishable project at the version semantic-release computed --
# or, for preview.yml, the version next-version.mjs computed the same way -- and
# checks the packages against the list the release notes link to.
#
# Usage: release-pack.sh <next-version> <release-type> [last-version]
#   release-type: major, minor or patch; or repair, for preview.yml completing a
#   version nuget.org already partly has, which was held to its baseline when it
#   was first packed.
#
# MinVer normally derives the version from git tags. At this point in the run the
# tag for this release does not exist yet -- semantic-release creates it after
# publishing -- so MinVer would stamp a preview version. MINVERVERSIONOVERRIDE is
# MinVer's documented hook for exactly this: an external tool that has already
# decided the version. It is an environment variable, not an MSBuild property.
set -euo pipefail

next_version="${1:?next version required}"
release_type="${2:?release type required}"
last_version="${3:-}"

# Only the packable projects under src/ (the solution filter): no test, benchmark or sample project is
# restored, evaluated or built in the job that produces what gets published.
args=(src/AdCodicem.ValueObjects.Packages.slnf --configuration Release --output artifacts/packages)

# EnablePackageValidation compares the new package's API against a published
# baseline. That check is meaningful for a patch or a minor, and actively wrong
# for a release that is *supposed* to break: a major says so in its version
# number, and while the version is still 0.x a minor is the breaking channel by
# convention, so neither is held to the baseline.
if [[ "$release_type" == "repair" ]]; then
  echo "Completing $next_version, partly published already: skipping package validation baseline."
elif [[ -z "$last_version" ]]; then
  echo "No previous release: skipping package validation baseline."
elif [[ "$release_type" == "major" ]]; then
  echo "Major release: skipping package validation baseline (breaks are intended)."
elif [[ "$next_version" == 0.* && "$release_type" == "minor" ]]; then
  echo "Pre-1.0 minor: skipping package validation baseline (0.x minor is the breaking channel)."
else
  # Only a published package can serve as a baseline, and each package only for
  # itself. v0.1.0 is a tag placed by hand with no package behind it, and a package
  # added since the last release has no version to compare with; restoring a
  # baseline that does not exist fails the pack. So every package is looked up, and
  # the projects nuget.org lacks at that version are packed without one, through
  # the hook in src/Directory.Build.props. A failed lookup stops the script rather
  # than skipping the check.
  validated=()
  without=()
  index="$(mktemp)"
  trap 'rm -f "$index"' EXIT
  for project in src/*/*.csproj; do
    properties="$(dotnet msbuild "$project" -nologo \
      -getProperty:IsPackable -getProperty:PackageId -getProperty:MSBuildProjectName)"
    [[ "$(jq -r .Properties.IsPackable <<<"$properties")" == true ]] || continue
    id="$(jq -r .Properties.PackageId <<<"$properties")"
    code="$(curl --silent --show-error --location --retry 3 --output "$index" --write-out '%{http_code}' \
      "https://api.nuget.org/v3-flatcontainer/${id,,}/index.json")"
    if [[ "$code" == 200 ]] && jq -e --arg v "${last_version,,}" '.versions | index($v)' "$index" >/dev/null; then
      validated+=("$id")
    elif [[ "$code" == 200 || "$code" == 404 ]]; then
      echo "$id $last_version is not on nuget.org: packing it without a baseline."
      without+=("$(jq -r .Properties.MSBuildProjectName <<<"$properties")")
    else
      echo "::error::nuget.org answered HTTP $code for $id; cannot tell whether $last_version is a baseline." >&2
      exit 1
    fi
  done
  if (( ${#validated[@]} == 0 )); then
    echo "$last_version was never published to nuget.org: skipping package validation baseline."
  else
    echo "Validating the API of ${#validated[@]} packages against $last_version."
    args+=("-p:ReleaseBaselineVersion=$last_version")
    (( ${#without[@]} == 0 )) || args+=("-p:ProjectsWithoutBaseline=${without[*]}")
  fi
fi

echo "Packing $next_version ($release_type)."
MINVERVERSIONOVERRIDE="$next_version" dotnet pack "${args[@]}"

# A silent mismatch here would publish a preview-numbered package as a stable
# release, so fail loudly instead of trusting the override took effect.
if ! ls "artifacts/packages/AdCodicem.ValueObjects.${next_version}.nupkg" >/dev/null 2>&1; then
  echo "::error::Expected artifacts/packages/AdCodicem.ValueObjects.${next_version}.nupkg. MinVer did not honour MINVERVERSIONOVERRIDE." >&2
  ls -1 artifacts/packages/ >&2 || true
  exit 1
fi

# The GitHub Release links every package in RELEASE_PACKAGE_IDS to nuget.org
# (see release.yml and .releaserc.json). That list comes from evaluating the
# projects before semantic-release starts, so check it against what was packed:
# a mismatch would publish a release whose links 404, or that omits a package.
# Failing here, in the prepare step, stops the run before anything is pushed.
if [[ -z "${RELEASE_PACKAGE_IDS:-}" ]]; then
  echo "::error::RELEASE_PACKAGE_IDS is not set; the release notes would carry no nuget.org links. Run .github/scripts/package-ids.sh first." >&2
  exit 1
fi
expected="$(tr -s ' ' '\n' <<<"$RELEASE_PACKAGE_IDS" | sed '/^$/d' | LC_ALL=C sort -u)"
packed="$(for package in artifacts/packages/*."${next_version}".nupkg; do
  name="$(basename "$package")"
  echo "${name%."${next_version}".nupkg}"
done | LC_ALL=C sort -u)"
if [[ "$expected" != "$packed" ]]; then
  echo "::error::The packed packages do not match RELEASE_PACKAGE_IDS. A project under src/ missing from src/AdCodicem.ValueObjects.Packages.slnf is not packed." >&2
  diff <(echo "$expected") <(echo "$packed") >&2 || true
  exit 1
fi
# The push takes artifacts/packages/*.nupkg, so nothing else may be there: a package
# at another version would go out with this one.
shopt -s nullglob
all=(artifacts/packages/*.nupkg)
if (( ${#all[@]} != $(wc -l <<<"$expected") )); then
  echo "::error::artifacts/packages holds packages other than the $next_version ones:" >&2
  printf '  %s\n' "${all[@]}" >&2
  exit 1
fi
echo "Packed $(wc -l <<<"$packed") packages, matching RELEASE_PACKAGE_IDS."
