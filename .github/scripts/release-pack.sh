#!/usr/bin/env bash
# Packs every publishable project, each at the version of its release train, the one next-version.mjs computed (the
# one semantic-release computes in release.yml's release job, which refuses to publish any other) or the preview
# version derived from it in preview.yml; and checks the packages against the list the release notes link to.
#
# Usage: release-pack.sh <train>=<version>:<release-type>[:<last-version>] ...
#   One argument per train of src/release-trains.json, every train named once
#   (docs/adr/0012-version-the-packages-in-two-trains-independently-of-the-frameworks.md).
#   release-type: major, minor or patch, a version nuget.org must not have yet; repair, for preview.yml: a version
#   nuget.org already has, partly or whole, held to its baseline when it was first packed, or a train it packs beside
#   a repair and never pushes, looked up nowhere; or, for a train that does not release while another does, its last
#   release packed again beside it, so the version must be <last-version>, and every package that release shipped (its
#   tag's tree says which) must be on nuget.org at that version: strict for the core, every package of which nuget.org
#   must have at that version and none at a stable version above it, its API then held to exactly that release's
#   (strict package validation), and keep for any other train, with no baseline. Both read the tag,
#   <prefix><last-version>, so the clone needs it.
#
# NUGET_FLAT_CONTAINER overrides the feed the packages are looked up in. RELEASE_PACK_CHECK_ONLY=true stops once every
# check has passed, before dotnet pack, printing what it would pack: a test seam for release-pack.test.mjs, which no
# workflow sets.
#
# MinVer normally derives the version from git tags. At this point in the run the tag for this release does not exist
# yet -- semantic-release creates it later, in another job -- so MinVer would stamp a preview version.
# RELEASE_TRAIN_VERSIONS ("core=1.2.0 satellites=0.4.0"), which src/Directory.Build.props turns into each project's
# MinVerVersionOverride, MinVer's documented hook for an external tool that has already decided the version, gives
# each train its own. It is an environment variable, not an MSBuild property.
set -euo pipefail

(( $# > 0 )) || { echo "::error::release-pack.sh takes <train>=<version>:<release-type>[:<last-version>] per train." >&2; exit 1; }
scripts="$(cd "$(dirname "$0")" && pwd)"
feed="${NUGET_FLAT_CONTAINER:-https://api.nuget.org/v3-flatcontainer}"

declare -A version=() type=() last=()
for argument in "$@"; do
  [[ "$argument" =~ ^([a-z][a-z0-9-]*)=([^:]+):(major|minor|patch|repair|strict|keep)(:([^:]*))?$ ]] ||
    { echo "::error::$argument is not <train>=<version>:<major|minor|patch|repair|strict|keep>[:<last-version>]." >&2; exit 1; }
  train="${BASH_REMATCH[1]}"
  [[ ! -v "version[$train]" ]] || { echo "::error::The $train train is given twice." >&2; exit 1; }
  version[$train]="${BASH_REMATCH[2]}"
  type[$train]="${BASH_REMATCH[3]}"
  last[$train]="${BASH_REMATCH[5]}"
done
listed="$(node .github/scripts/release-trains.mjs trains)"
mapfile -t trains < <(cut -d' ' -f1 <<<"$listed")
declare -A prefix=()
while read -r name tag_prefix _; do [[ -z "$name" ]] || prefix[$name]="$tag_prefix"; done <<<"$listed"
(( ${#trains[@]} > 0 )) || { echo "::error::release-trains.mjs named no train." >&2; exit 1; }
declare -A known=()
for train in "${trains[@]}"; do
  known[$train]=1
  [[ -v "version[$train]" ]] || { echo "::error::No version for the $train train." >&2; exit 1; }
done
for train in "${!version[@]}"; do
  [[ -v "known[$train]" ]] || { echo "::error::$train is no train of src/release-trains.json." >&2; exit 1; }
done

# The GitHub Release links every package in RELEASE_PACKAGE_IDS to nuget.org (see release.yml and .releaserc.json),
# checked against the packable projects once they are evaluated, below.
if [[ -z "${RELEASE_PACKAGE_IDS:-}" ]]; then
  echo "::error::RELEASE_PACKAGE_IDS is not set; the release notes would carry no nuget.org links. Run .github/scripts/package-ids.sh first." >&2
  exit 1
fi

# Only the packable projects under src/ (the solution filter): no test, benchmark or sample project is
# restored, evaluated or built in the job that produces what gets published.
args=(src/AdCodicem.ValueObjects.Packages.slnf --configuration Release --output artifacts/packages)

# EnablePackageValidation compares the new package's API against a published baseline, train by train. That check
# is meaningful for a patch or a minor, and actively wrong for a release that is *supposed* to break: a major says so
# in its version number, and while a train is still 0.x a minor is the breaking channel by convention, so neither is
# held to the baseline.
declare -A validated=()
strict=()
for train in "${trains[@]}"; do
  # A train that does not release is packed at its last release, which the other trains' packages depend on.
  if [[ "${type[$train]}" == strict || "${type[$train]}" == keep ]] &&
    [[ -z "${last[$train]}" || "${version[$train]}" != "${last[$train]}" ]]; then
    echo "::error::$train: a ${type[$train]} pack is the train's last release packed again, so its version must be its last, '${last[$train]}', not ${version[$train]}." >&2
    exit 1
  fi
  if [[ "${type[$train]}" == strict ]]; then
    echo "$train: packed again at ${last[$train]}, its API held to exactly that release's (strict package validation)."
    validated[$train]=0
    strict+=("$train")
  elif [[ "${type[$train]}" == keep ]]; then
    echo "$train: packed again at ${last[$train]}, with no package validation baseline."
  elif [[ "${type[$train]}" == repair ]]; then
    echo "$train: packed at ${version[$train]}, which nuget.org has in part or whole, or which is never pushed: no package validation baseline."
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
# src/Directory.Build.props; but a strict train's must all be there. Every other package is looked up too, but a
# repair's: a version nuget.org already has would be skipped by the push, and one at or below a stable version of the
# package (a package moved into a train behind its last release) would never be offered. A failed lookup stops the
# script rather than skipping the check.
without=()
declare -A train_of=()
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
index="$work/index.json"
look_up() { # <id> <train>: what nuget.org lists of a package, into $index, a 404 an empty list
  local code
  code="$(curl --silent --show-error --location --retry 3 --output "$index" --write-out '%{http_code}' \
    "$feed/${1,,}/index.json")"
  case "$code" in
    200) ;;
    404) echo '{"versions":[]}' >"$index" ;;
    *) echo "::error::nuget.org answered HTTP $code for $1; cannot tell what it holds of the $2 train." >&2; exit 1 ;;
  esac
}
incomplete() { # <train> <id>
  echo "::error::${prefix[$1]}${last[$1]} is not complete on nuget.org: $2, which it shipped, lacks ${last[$1]}, which the $1 train is packed again at. Complete that release first: Re-run failed jobs of the run that tagged it (docs/maintaining.md)." >&2
  exit 1
}
# A tag of the train at a version nuget.org has, on a commit this one does not descend from: a release cut after it.
released_after() { # <train> <version>
  local commit
  commit="$(git rev-parse --quiet --verify "refs/tags/${prefix[$1]}$2^{commit}")" || return 1
  if git merge-base --is-ancestor "$commit" HEAD; then return 1; fi
}

# A train packed again at its last release is read from that release's tag: the commit it names, and the list of
# trains its pack read (HEAD's for a commit from before the list). A package that release shipped has its project
# there, packable, on this train; any other joined the train since, added or moved in. One that release shipped and
# HEAD no longer packs on the train (removed, renamed or moved out since) is looked up after HEAD's.
declare -A tagged=()
for train in "${trains[@]}"; do
  [[ "${type[$train]}" == strict || "${type[$train]}" == keep ]] || continue
  tag="${prefix[$train]}${last[$train]}"
  tagged[$train]="$(git rev-parse --quiet --verify "refs/tags/$tag^{commit}")" ||
    { echo "::error::$train: there is no tag $tag here, which a ${type[$train]} pack reads that release's packages from (fetch-depth: 0 and the tags)." >&2; exit 1; }
  git show "${tagged[$train]}:src/release-trains.json" >"$work/$train.json" 2>/dev/null ||
    cp src/release-trains.json "$work/$train.json"
done
shipped_in_last() { # <train> <project>
  local csproj answer
  csproj="$(git show "${tagged[$1]}:src/$2/$2.csproj" 2>/dev/null)" || return 1
  ! grep -qiE '<IsPackable>[[:space:]]*false[[:space:]]*</IsPackable>' <<<"$csproj" || return 1
  answer="$(node "$scripts/release-trains.mjs" --list "$work/$1.json" trains-of "$2")" ||
    { echo "::error::Could not read the train $2 rode at ${prefix[$1]}${last[$1]}." >&2; exit 1; }
  [[ "${answer#* }" == "$1" ]]
}
for project in src/*/*.csproj; do
  properties="$(dotnet msbuild "$project" -nologo \
    -getProperty:IsPackable -getProperty:PackageId -getProperty:MSBuildProjectName -getProperty:ReleaseTrain)"
  [[ "$(jq -r .Properties.IsPackable <<<"$properties")" == true ]] || continue
  id="$(jq -r .Properties.PackageId <<<"$properties")"
  name="$(jq -r .Properties.MSBuildProjectName <<<"$properties")"
  train="$(jq -r .Properties.ReleaseTrain <<<"$properties")"
  train_of[$id]="$train"
  [[ "${type[$train]}" != repair ]] || continue
  look_up "$id" "$train"
  # Whether it has the last release, whether it has the version, its newest stable version at or above the version,
  # and its newest stable version above the last release, if any, joined by "|": a tab is IFS whitespace, which read
  # would merge around an empty field. -e fails an index that yields nothing.
  facts="$(jq -e -r -L "$scripts" --arg last "${last[$train],,}" --arg version "${version[$train],,}" 'include "semver";
    def newest_stable(f): [.[] | select(test("-") | not) | select(f)] | max_by(semver_key) // "";
    [.versions[] | ascii_downcase] as $v
    | [ ($last != "" and ($v | index($last)) != null),
        (($v | index($version)) != null),
        ($v | newest_stable(semver_key >= ($version | semver_key))),
        (if $last == "" then "" else $v | newest_stable(semver_key > ($last | semver_key)) end) ]
    | map(tostring) | join("|")' "$index")" ||
    { echo "::error::Could not read the versions nuget.org lists for $id." >&2; exit 1; }
  IFS='|' read -r has_last has_version stable_above stable_above_last <<<"$facts"
  case "${type[$train]}" in
    strict | keep)
      if [[ "${type[$train]}" == strict && -n "$stable_above_last" ]] && released_after "$train" "$stable_above_last"; then
        echo "::error::$id has $stable_above_last on nuget.org, above ${last[$train]}, which the $train train is packed again at: ${prefix[$train]}$stable_above_last was released after this commit, so this run re-runs an older run. Dispatch the workflow again from main; if that release stopped midway, complete it first (docs/maintaining.md)." >&2
        exit 1
      elif [[ "${type[$train]}" == strict && -n "$stable_above_last" ]]; then
        echo "::error::$id has $stable_above_last on nuget.org, above ${last[$train]}, which the $train train is packed again at and the other trains would depend on: the $train train's last tag is behind nuget.org (a release without its tag), or the package moved into the train before the train's next version was above its last published one, which ADR 0012 refuses: revert the move until then (docs/adr/0012-version-the-packages-in-two-trains-independently-of-the-frameworks.md)." >&2
        exit 1
      elif [[ "$has_last" != true ]] && shipped_in_last "$train" "$name"; then
        incomplete "$train" "$id"
      elif [[ "$has_last" != true && "${type[$train]}" == strict ]]; then
        echo "::error::$id joined the $train train after ${prefix[$train]}${last[$train]}, so nuget.org cannot have it at ${last[$train]}, which the other trains would depend on: the $train train must release in this run (a commit to it typed feat, fix, perf, build(pack) or docs(readme))." >&2
        exit 1
      fi
      ;;
    *)
      # A plan made at the tip of main never gives a version nuget.org has, or one a stable version of it sorts above:
      # a train it has whole is a repair, and the preview gate and the tags refuse the rest. One made below a release
      # published since does: a re-run of an older run, or a dispatch queued behind that release in the release group.
      if [[ "$has_version" == true ]] && released_after "$train" "${version[$train]}"; then
        echo "::error::$id already has ${version[$train]} on nuget.org: ${prefix[$train]}${version[$train]} was released after this commit, so this run was queued behind that release or re-runs an older run. Dispatch the workflow again from main; if that release stopped midway, complete it first (docs/maintaining.md)." >&2
        exit 1
      elif [[ "$has_version" == true ]]; then
        echo "::error::$id already has ${version[$train]} on nuget.org, so this run would publish none of it: its plan is older than a release published since (a re-run of an older run, or a dispatch queued behind that release: dispatch again from main), a tag the plan counted from is missing (the train's own, or the core's while release.yml cuts every train at one version: place it on the commit its twin names), or the package moved into the train at that very version, which ADR 0012 refuses (docs/adr/0012-version-the-packages-in-two-trains-independently-of-the-frameworks.md)." >&2
        exit 1
      elif [[ -n "$stable_above" ]] && released_after "$train" "$stable_above"; then
        echo "::error::$id has $stable_above on nuget.org, at or above ${version[$train]}: ${prefix[$train]}$stable_above was released after this commit, so this run was queued behind that release or re-runs an older run. Dispatch the workflow again from main; if that release stopped midway, complete it first (docs/maintaining.md)." >&2
        exit 1
      elif [[ -n "$stable_above" ]]; then
        echo "::error::$id has $stable_above on nuget.org, at or above ${version[$train]}, the version NuGet would keep offering: its plan is older than a release published since (a re-run of an older run, or a dispatch queued behind that release: dispatch again from main), a tag the plan counted from is missing (the train's own, or the core's while release.yml cuts every train at one version: place it on the commit its twin names), or the package moved into the train before the train's next version was above its last published one, which ADR 0012 refuses (docs/adr/0012-version-the-packages-in-two-trains-independently-of-the-frameworks.md)." >&2
        exit 1
      fi
      ;;
  esac
  [[ -v "validated[$train]" ]] || continue
  if [[ "$has_last" == true ]]; then
    validated[$train]=$(( ${validated[$train]} + 1 ))
  else
    echo "$id ${last[$train]} is not on nuget.org: packing it without a baseline."
    without+=("$name")
  fi
done
for train in "${!tagged[@]}"; do
  while read -r name; do
    if [[ "${train_of[$name]:-}" == "$train" ]] || ! shipped_in_last "$train" "$name"; then continue; fi
    look_up "$name" "$train"
    jq -e --arg last "${last[$train],,}" '[.versions[] | ascii_downcase] | index($last) != null' "$index" >/dev/null ||
      incomplete "$train" "$name"
  done < <(git ls-tree -d --name-only "${tagged[$train]}" src/ | sed 's|^src/||')
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
(( ${#strict[@]} == 0 )) || args+=("-p:StrictBaselineTrains=${strict[*]}")

# The GitHub Release links every package in RELEASE_PACKAGE_IDS to nuget.org (see release.yml and .releaserc.json).
# That list comes from evaluating the projects, and semantic-release starts with it, so check it against the packable
# projects: a mismatch would publish a release whose links 404, or that omits a package. Failing here, in the pack
# job, stops the run before anything is pushed.
expected="$(tr -s ' ' '\n' <<<"$RELEASE_PACKAGE_IDS" | sed '/^$/d' | LC_ALL=C sort -u)"
evaluated="$(printf '%s\n' "${!train_of[@]}" | LC_ALL=C sort -u)"
if [[ "$expected" != "$evaluated" ]]; then
  echo "::error::RELEASE_PACKAGE_IDS does not match the packable projects under src/." >&2
  diff <(echo "$expected") <(echo "$evaluated") >&2 || true
  exit 1
fi

versions=()
for train in "${trains[@]}"; do
  versions+=("$train=${version[$train]}")
  echo "Packing the $train train at ${version[$train]} (${type[$train]})."
done
if [[ "${RELEASE_PACK_CHECK_ONLY:-}" == true ]]; then
  echo "RELEASE_TRAIN_VERSIONS=${versions[*]} dotnet pack ${args[*]}"
  exit 0
fi
RELEASE_TRAIN_VERSIONS="${versions[*]}" dotnet pack "${args[@]}"

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
