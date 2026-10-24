#!/usr/bin/env bash
# Decides what preview.yml publishes, from what nuget.org already has, train by train: each release train of
# src/release-trains.json goes out at its own version, every package of the train at that version, or none of it
# (docs/adr/0012-version-the-packages-in-two-trains-independently-of-the-frameworks.md).
#
# Usage: NEXT_VERSIONS='core=<x.y.z-preview.n> satellites=<...>' RELEASE_TYPES='core=<major|minor|patch|> ...' \
#          preview-gate.sh
#                                   what preview.yml does: mode=publish, repair or none. NEXT_VERSIONS and
#                                   RELEASE_TYPES are what next-version.mjs --trains computed for HEAD, every
#                                   train named once; a publish checks each pushed train's version against its type.
#        preview-gate.sh --report   for release.yml: warns, train by train, when HEAD carries package inputs that
#                                   no version of the train on nuget.org carries, so no preview did, and when the
#                                   version it compares with is on some of the train's packages only
#        GATE_REF=<commit> PACKAGE_TRAINS='<id>=<train> ...' EXPECT_BASES='<train>=<version> ...' \
#          EXPECT_VERSIONS='<train>=<version> ...' PUSH='<train> ...' preview-gate.sh --recheck
#                                   for preview.yml's publish job, just before it logs in: fails unless, for every
#                                   train, the base nuget.org gives GATE_REF is still the one the plan saw, or, for a
#                                   train this run pushes, the version being published (a push of this very run that
#                                   stopped midway). A re-run of an old run, after a newer commit was published,
#                                   stops there. Needs neither dotnet nor a restore: the IDs come from PACKAGE_TRAINS.
#
# A train's base is a version on nuget.org and the commit it was packed from, read off its nuspec
# (<repository commit="..."/>, which SourceLink writes): of the versions from the train's last stable tag
# (<prefix><major>.<minor>.<patch>, v for the core, <train>-v for any other) merged into HEAD on, across the train's
# package IDs, the one packed from the commit nearest to HEAD. Not the previous run of the workflow: a run can stop
# after part of its push, a stable release publishes too, and nuget.org is the one record that survives both.
#
#   repair   a package of a train lacks the version its base commit packed: a push stopped midway. nuget.org has no
#            transaction, so the missing IDs are completed at that version, from that commit, before anything newer
#            is published. Every train missing IDs must have its base at the same commit; the other trains are packed
#            there too, for the tests and the dependency checks, and never pushed.
#   publish  a package input changed between a train's base commit and HEAD, each train's own: a revert can undo what
#            lies between two bases. Every train is pushed, but a train nuget.org already has whole at HEAD at the
#            version computed for it.
#
# An older commit's packages ride the trains of that commit's own list (HEAD's for a commit from before the list), as
# its pack read them: a version a package was packed at on another train is no base of its new one, and a pushed
# version below such a version is refused (ADR 0012 moves a package only once its train's next version is above the
# package's last one).
#   none     nothing a package is built from changed.
#
# Outputs, to $GITHUB_OUTPUT (stdout outside Actions), each list in the order of release-trains.mjs trains, the core
# first:
#   mode              publish | repair | none
#   ref               the commit to test, pack and publish: HEAD, or the base commit for a repair
#   versions          "<train>=<version> ...": the version each train is packed at; empty for none
#   pack              "<train>=<version>:<type>[:<last>] ...": release-pack.sh's arguments, <type> repair for a
#                     version nuget.org already has, partly or whole, or for a train packed and never pushed
#   push              "<train> ...": the trains to push, the core first; empty for none
#   bases             "<train>=<version> ...": each train's base version, never empty: a tag nuget.org has nothing
#                     of the train from fails
#   base-commits      "<train>=<commit> ...": the commits they were packed from
#   last-stables      "<train>=<version> ...": each train's last stable release merged into HEAD, read off its tags
#   docs-ref          the commit /docs/preview/ is built from: HEAD, unless a repair leaves HEAD with package
#                     changes nuget.org does not have yet
#   docs-version      the label of /docs/preview/: the core's version published, or the core's base version
#   pending           for a repair, whether HEAD has package changes beyond the repaired commit
#   reason            one line, for the job summary
#
# A lookup that fails fails the run rather than guessing. A guess that publishes adds a version to nuget.org for good;
# a guess that skips hides a change until someone notices. A failed run publishes nothing, shows in the Actions tab,
# and the next dispatch decides again. With --report the same failure is a warning: release.yml runs this step with
# continue-on-error.
#
# NUGET_FLAT_CONTAINER overrides the feed, to replay a decision against a recorded state.
#
# PACKAGE_IDS_SCRIPT replaces package-ids.sh, which evaluates the projects with dotnet: a test seam for
# preview-gate.test.mjs, which replays the gate against a throwaway repository and a local feed (a change of a
# <PackageVersion> entry still restores with dotnet, which it does not replay). No workflow sets it, nor may: the gate
# must read the IDs package-ids.sh prints, the set preview.yml's pack job computes and publishes.
set -euo pipefail

report=false recheck=false
case "${1:-}" in
  --report) report=true ;;
  --recheck) recheck=true ;;
  "") ;;
  *) echo "::error::Unknown argument '$1'." >&2; exit 1 ;;
esac

root="$(cd "$(dirname "$0")/../.." && pwd)"
scripts="$root/.github/scripts"
feed="${NUGET_FLAT_CONTAINER:-https://api.nuget.org/v3-flatcontainer}"
package_ids="${PACKAGE_IDS_SCRIPT:-$scripts/package-ids.sh}"
output="${GITHUB_OUTPUT:-/dev/stdout}"
summary="${GITHUB_STEP_SUMMARY:-/dev/stderr}"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
cd "$root"

fail() {
  if $report; then
    echo "::warning title=Not checked::Could not tell whether this release was previewed: $*" >&2
  else
    echo "::error::$*" >&2
  fi
  exit 1
}

# What can change the bytes or the metadata of a package, apart from Directory.Packages.props,
# which is filtered below. Not docs/ or website/, which no package carries; not the repository's
# README.md, which no package carries either: each carries src/PackageReadme.md, or a README of
# its own under src/ (ADR 0012); not LICENSE, which
# PackageLicenseExpression replaces; not .editorconfig, nor the analyzer release-tracking files
# under src/, which change diagnostics only, nor the CLAUDE.md files under src/, which no package
# carries, both excluded at the end; not the solution, which selects
# projects and changes nothing in them. The solution filter the pack goes through is under src/
# and counts, since it decides what is packed. release-pack.sh is in, as it holds the pack command
# line.
inputs=(
  src
  icon.png
  Directory.Build.props
  Directory.Build.targets
  Directory.Build.rsp
  global.json
  ':(icase)nuget.config'
  .gitattributes
  .github/scripts/release-pack.sh
  ':(exclude,glob)src/**/AnalyzerReleases.*.md'
  ':(exclude,glob)src/**/CLAUDE.md'
)

# --- the trains --------------------------------------------------------------------------------

# "<name> <tag prefix> <breaking>" per train, the core first, from the list of the commit the scripts run at.
listed="$(node "$scripts/release-trains.mjs" trains)" || fail "release-trains.mjs could not read src/release-trains.json."
declare -A prefix=() breaking=()
trains=()
while read -r name tag_prefix rule; do
  [[ -n "$name" ]] || continue
  trains+=("$name")
  prefix[$name]="$tag_prefix"
  breaking[$name]="$rule"
done <<<"$listed"
(( ${#trains[@]} > 0 )) || fail "release-trains.mjs named no train."

# Reads "<train>=<value> ..." into the associative array named first: every train of the list once, no other. A value
# may be empty only when the third argument says so.
parse_pairs() {
  local -n into="$1"
  local what="$2" empty="${3:-false}" pair train value
  local words=()
  read -ra words <<<"${!what:-}"
  for pair in "${words[@]}"; do
    [[ "$pair" == *=* ]] || fail "$what: '$pair' is not <train>=<value>."
    train="${pair%%=*}" value="${pair#*=}"
    [[ -v "prefix[$train]" ]] || fail "$what: $train is no train of src/release-trains.json."
    [[ ! -v "into[$train]" ]] || fail "$what: the $train train is given twice."
    [[ -n "$value" || "$empty" == true ]] || fail "$what: the $train train is given no value."
    into["$train"]="$value"
  done
  for train in "${trains[@]}"; do
    [[ -v "into[$train]" ]] || fail "$what: no value for the $train train."
  done
}

# "<train>=<value> ..." of an associative array, in the order of the trains.
pairs_of() {
  local -n from="$1"
  local train words=()
  for train in "${trains[@]}"; do
    [[ -v "from[$train]" ]] && words+=("$train=${from[$train]}")
  done
  echo "${words[*]}"
}

# --- nuget.org -------------------------------------------------------------------------------

# One package's versions, lowercased, one per line, and nothing on a 404: nuget.org has never had
# it. The flat container lists unlisted versions too, which is wanted: an unlisted version can still
# be installed by number.
versions_of() {
  local code
  code="$(curl --silent --show-error --location --retry 3 --output "$work/index.json" \
    --write-out '%{http_code}' "$feed/${1,,}/index.json")" || fail "nuget.org did not answer for $1."
  case "$code" in
    200) jq -r '.versions[] | ascii_downcase' "$work/index.json" ;;
    404) ;;
    *) fail "nuget.org answered HTTP $code for $1." ;;
  esac
}

# The newest of the versions on stdin, one per line, in SemVer order -- not in the order the feed
# lists them.
newest() {
  jq -Rrs -L "$scripts" 'include "semver"; split("\n") | map(select(length > 0)) | max_by(semver_key) // empty'
}

# Whether the first version sorts above the second, in SemVer order.
above() {
  jq -en -L "$scripts" --arg new "$1" --arg old "$2" 'include "semver"; ($new | semver_key) > ($old | semver_key)' >/dev/null
}

# $work/published holds one "<id> <version> <version>..." line per package ID looked up.
# The first of the IDs given after the version that has it, if any.
holder_in() {
  local version="$1"
  shift
  awk -v v="$version" -v list="$*" 'BEGIN { n = split(list, wanted, " "); for (i = 1; i <= n; i++) keep[wanted[i]] = i }
    ($1 in keep) { for (i = 2; i <= NF; i++) if ($i == v) { found[keep[$1]] = $1 } }
    END { for (i = 1; i <= n; i++) if (i in found) { print found[i]; exit } }' "$work/published"
}
has_version() { awk -v id="$1" -v v="$2" '$1 == id { for (i = 2; i <= NF; i++) if ($i == v) found = 1 } END { exit !found }' "$work/published"; }
# Every version of the IDs given, one per line.
versions_in() { awk -v list="$*" 'BEGIN { n = split(list, wanted, " "); for (i = 1; i <= n; i++) keep[wanted[i]] = 1 }
  ($1 in keep) { for (i = 2; i <= NF; i++) print $i }' "$work/published"; }

nuspec_of() {
  curl --fail --silent --show-error --location --retry 3 "$feed/${1,,}/$2/${1,,}.nuspec" ||
    fail "nuget.org did not serve the nuspec of $1 $2."
}

commit_of() {
  local commit
  commit="$(curl --fail --silent --show-error --location --retry 3 "$feed/${1,,}/$2/${1,,}.nuspec" |
    grep -oP '<repository\b[^>]*\bcommit="\K[0-9a-f]{40}')" || fail "The nuspec of $1 $2 names no commit."
  git cat-file -e "$commit^{commit}" 2>/dev/null ||
    fail "$1 $2 was packed from $commit, which this clone does not have (fetch-depth: 0?)."
  git merge-base --is-ancestor "$commit" "$head" ||
    fail "$1 $2 was packed from $commit, which is not an ancestor of ${head:0:7}: a newer commit was published since, or the history was rewritten."
  echo "$commit"
}

record() { # <id>...: appends "<id> <versions...>" to $work/published for each ID not there yet
  local id versions
  for id in "$@"; do
    [[ -f "$work/published" ]] && awk -v id="$id" '$1 == id { found = 1 } END { exit !found }' "$work/published" && continue
    versions="$(versions_of "$id")"
    echo "$id $(paste -sd ' ' <<<"$versions")" >>"$work/published"
  done
}

# --- package inputs --------------------------------------------------------------------------

# The list of trains a commit packed with, as a file: its own src/release-trains.json, or HEAD's for a commit from
# before the list, when every package rode one version.
list_at() {
  local file="$work/lists/$1.json"
  if [[ ! -f "$file" ]]; then
    mkdir -p "$work/lists"
    git show "$1:src/release-trains.json" >"$file" 2>/dev/null || cp "$root/src/release-trains.json" "$file"
  fi
  echo "$file"
}

# Whether a package rode a train when a commit packed it: a version it was packed at while on another train is no
# version of this one (a package moved since). The list at that commit says, HEAD's when it is the same file.
rode() { # <train> <commit> <id>
  [[ "$(git rev-parse --quiet --verify "$2:src/release-trains.json" 2>/dev/null)" != \
    "$(git rev-parse --quiet --verify "HEAD:src/release-trains.json" 2>/dev/null)" ]] || return 0
  local answer
  answer="$(node "$scripts/release-trains.mjs" --list "$(list_at "$2")" trains-of "$3")" ||
    fail "Could not read the train $3 rode at ${2:0:7}."
  [[ "${answer#* }" == "$1" ]]
}

# "<id> <train>" per package a commit packs, evaluated from its own tree: a package added since has no reason to carry
# that commit's version. The train is the one the commit's own list gives the project, which is what that commit's
# pack read (HEAD's for a commit from before the list).
ids_at() {
  if [[ "$1" == "$(git rev-parse HEAD)" ]]; then
    bash "$package_ids" --trains || fail "Could not read the packages ${1:0:7} packs."
  else
    rm -rf "$work/tree" && mkdir -p "$work/tree"
    git archive "$1" -- src Directory.Build.props Directory.Packages.props global.json | tar -x -C "$work/tree"
    local ids
    ids="$(bash "$package_ids" "$work/tree")" || fail "Could not read the packages ${1:0:7} packs."
    [[ -n "$ids" ]] || return 0
    # shellcheck disable=SC2086 # one ID per word
    node "$scripts/release-trains.mjs" --list "$(list_at "$1")" trains-of $ids ||
      fail "Could not read the trains of the packages ${1:0:7} packs."
  fi
}

# The IDs of a train, one line of "<id> <train>" per package on stdin.
of_train() { awk -v t="$1" '$2 == t { print $1 }'; }

# Every package in the restore graph of the projects under src/, lowercased: what they reference,
# what that pulls in, and the build-time packages -- MinVer, PolySharp, Roslyn, and SourceLink,
# which src/Directory.Build.props adds under Actions only. Restored only when needed.
shipped=""
load_shipped() {
  [[ -z "$shipped" ]] || return 0
  dotnet restore src/AdCodicem.ValueObjects.Packages.slnf --verbosity quiet >&2 ||
    fail "Could not restore the packable projects."
  local assets=() project
  for project in src/*/*.csproj; do
    project="$(basename "$project" .csproj)"
    [[ -f "artifacts/obj/$project/project.assets.json" ]] || fail "No restore graph for $project."
    assets+=("artifacts/obj/$project/project.assets.json")
  done
  shipped="$(jq -r '.libraries | to_entries[] | select(.value.type == "package")
    | .key | split("/")[0] | ascii_downcase' "${assets[@]}" | LC_ALL=C sort -u)"
}

# Directory.Packages.props also pins the test, benchmark and sample packages. Comments are dropped;
# a change to anything but a PackageVersion element counts, and so does the PackageVersion of a
# package in the graph above.
strip_comments() { perl -0777 -pe 's/<!--.*?-->//gs'; }
package_versions() {
  strip_comments | perl -0777 -ne 'while (/<PackageVersion\b([^>]*?)\/>/gs) {
    my $a = $1; $a =~ s/\s+/ /g; my ($id) = $a =~ /Include="([^"]+)"/; print lc($id), "\t", $a, "\n" }' |
    LC_ALL=C sort
}
other_settings() { strip_comments | perl -0777 -pe 's/<PackageVersion\b[^>]*?\/>//gs; s/\s+//g'; }

# Sets $changed to what changed between two commits, one entry per line; empty when nothing did.
# Called as a plain statement, never in a condition or a $( ), so that set -e still holds inside.
changed=""
package_inputs_changed() {
  local before after versions hits
  changed="$(git diff --name-only "$1" "$2" -- "${inputs[@]}")"
  before="$(git show "$1:Directory.Packages.props")"
  after="$(git show "$2:Directory.Packages.props")"
  [[ "$before" != "$after" ]] || return 0
  if [[ "$(other_settings <<<"$before")" != "$(other_settings <<<"$after")" ]]; then
    changed="$(printf '%s\n' "$changed" "Directory.Packages.props (outside the PackageVersion entries)" | sed '/^$/d')"
    return 0
  fi
  versions="$(LC_ALL=C comm -3 <(package_versions <<<"$before") <(package_versions <<<"$after") |
    sed 's/^\t//' | cut -f1 | LC_ALL=C sort -u)"
  [[ -n "$versions" ]] || return 0
  load_shipped
  hits="$(LC_ALL=C comm -12 <(echo "$versions") <(echo "$shipped") | sed 's/^/Directory.Packages.props: /')"
  [[ -z "$hits" ]] || changed+=$'\n'"$hits"
  changed="$(sed '/^$/d' <<<"$changed")"
}

# --- each train's state --------------------------------------------------------------------------

# The commit decided about: HEAD, or for --recheck the commit the plan packed, which must be HEAD or
# one of its ancestors (a repair packs the base commit).
if $recheck; then
  : "${GATE_REF:?GATE_REF required with --recheck}" "${PACKAGE_TRAINS:?PACKAGE_TRAINS required with --recheck}"
  : "${EXPECT_BASES:?EXPECT_BASES required with --recheck}" "${EXPECT_VERSIONS:?EXPECT_VERSIONS required with --recheck}"
  head="$(git rev-parse --verify --quiet "$GATE_REF^{commit}")" || fail "GATE_REF $GATE_REF is not a commit of this clone."
  git merge-base --is-ancestor "$head" HEAD || fail "GATE_REF $GATE_REF is not HEAD or one of its ancestors."
  head_ids=""
  read -ra words <<<"$PACKAGE_TRAINS"
  for pair in "${words[@]}"; do
    [[ "$pair" =~ ^([^=]+)=([^=]+)$ ]] || fail "PACKAGE_TRAINS: '$pair' is not <id>=<train>."
    [[ -v "prefix[${BASH_REMATCH[2]}]" ]] || fail "PACKAGE_TRAINS: ${BASH_REMATCH[2]} is no train of src/release-trains.json."
    head_ids+="${BASH_REMATCH[1]} ${BASH_REMATCH[2]}"$'\n'
  done
else
  head="$(git rev-parse HEAD)"
  head_ids="$(ids_at "$head")"
fi
[[ -n "$head_ids" ]] || fail "No packable project under src/."

declare -A ids=() last=() base=() base_version=() missing=()
for train in "${trains[@]}"; do
  ids[$train]="$(of_train "$train" <<<"$head_ids" | paste -sd ' ')"
  [[ -n "${ids[$train]}" ]] || fail "The $train train has no packable project under src/."
  # shellcheck disable=SC2086 # one ID per word
  record ${ids[$train]}

  # The train's last stable release merged into the commit, read off its tags as next-version.mjs reads them.
  last[$train]="$(git tag --merged "$head" --list "${prefix[$train]}*" |
    sed -n "s/^${prefix[$train]}\([0-9]\{1,\}\.[0-9]\{1,\}\.[0-9]\{1,\}\)$/\1/p" | newest)"
  [[ -n "${last[$train]}" ]] ||
    fail "No stable tag ${prefix[$train]}<major>.<minor>.<patch> of the $train train is merged into ${head:0:7}: push the train's baseline tag (satellites-v0.2.1 on 68abdc7 for the satellites, docs/maintaining.md)."

  # A stable version of the train above its last tag is a release whose tag is nowhere: its twin tag was never
  # placed. Every version would then be computed from the wrong release. A tag that exists but is not merged here is a
  # release cut after this commit (a run queued behind it, a re-run of an older run): the base search below meets its
  # version as any other.
  # shellcheck disable=SC2086 # one ID per word
  stable="$(versions_in ${ids[$train]} | grep -v -- - | newest || true)"
  if [[ -n "$stable" ]] && above "$stable" "${last[$train]}" &&
    ! git rev-parse --quiet --verify "refs/tags/${prefix[$train]}$stable^{commit}" >/dev/null; then
    # shellcheck disable=SC2086 # one ID per word
    other="${trains[0]}"
    [[ "$other" != "$train" ]] || other="${trains[1]:-$train}"
    # shellcheck disable=SC2086 # one ID per word
    fail "nuget.org has $stable of the $train train ($(holder_in "$stable" ${ids[$train]})), above ${prefix[$train]}${last[$train]}, its newest tag merged into ${head:0:7}: a ${prefix[$train]}$stable tag is missing (a release without its twin tag?). Place it on the release commit, which the release's other tag names (git tag ${prefix[$train]}$stable \"${prefix[$other]}$stable^{commit}\", docs/maintaining.md), then dispatch again."
  fi

  # The base: of the versions nuget.org has of the train from its last stable release on, the one packed from the
  # commit nearest to HEAD. Not the highest in SemVer order: a feature reverted after its preview shipped leaves that
  # preview above every later version, and each later commit would then differ from it. A preview and the stable
  # release cut from the same commit tie, and the release, the higher, wins: the base is then the newest version of
  # any kind, which is what release.yml asks about too.
  # shellcheck disable=SC2086 # one ID per word
  candidates="$(versions_in ${ids[$train]} | sed '/^$/d' | LC_ALL=C sort -u |
    jq -Rrs -L "$scripts" --arg floor "${last[$train]}" 'include "semver";
      split("\n") | map(select(length > 0) | select(semver_key >= ($floor | semver_key)))
      | sort_by(semver_key) | reverse | .[]')"
  best=""
  if [[ -n "$candidates" ]]; then
    mapfile -t candidates <<<"$candidates"
    read -ra train_ids <<<"${ids[$train]}"
    for candidate in "${candidates[@]}"; do
      # The first package holding the version that rode the train when it was packed: a package moved here from
      # another train brings versions that are not this train's.
      commit=""
      for id in "${train_ids[@]}"; do
        has_version "$id" "$candidate" || continue
        packed="$(commit_of "$id" "$candidate")"
        if rode "$train" "$packed" "$id"; then
          commit="$packed"
          break
        fi
      done
      [[ -n "$commit" ]] || continue
      steps="$(git rev-list --count "$commit..$head")"
      if [[ -z "$best" ]] || (( steps < best )); then
        best="$steps" base[$train]="$commit" base_version[$train]="$candidate"
      fi
    done
  fi

  # The tag is there, so nuget.org must have something of the train from it on: the release itself at least. Nothing
  # means the release's push failed (semantic-release tags before it publishes) or nuget.org has not listed it yet.
  # Publishing on top would be a guess, so stop instead.
  [[ -n "$best" ]] ||
    fail "${prefix[$train]}${last[$train]} is tagged, but nuget.org has no version of the $train packages from ${last[$train]} on: the release's push failed, or nuget.org has not listed it yet. Complete the release, or wait, then dispatch again."
done

# preview.yml's publish job asks whether nuget.org still gives each train the base the plan saw.
if $recheck; then
  declare -A expect_base=() expect_version=() pushing=()
  parse_pairs expect_base EXPECT_BASES
  parse_pairs expect_version EXPECT_VERSIONS
  read -ra words <<<"${PUSH:-}"
  for train in "${words[@]}"; do
    [[ -v "prefix[$train]" ]] || fail "PUSH: $train is no train of src/release-trains.json."
    pushing[$train]=1
  done
  for train in "${trains[@]}"; do
    found="${base_version[$train]}" from="${base[$train]}"
    if [[ -v "pushing[$train]" && "$found" == "${expect_version[$train],,}" ]]; then
      [[ "$from" == "$head" ]] ||
        fail "$train: ${expect_version[$train]} is on nuget.org, packed from ${from:0:7}, not from ${head:0:7}, which this run packed."
      echo "$train: ${expect_version[$train]} is partly or wholly on nuget.org already, from ${head:0:7}: this run completes it." >&2
    elif [[ "$found" == "${expect_base[$train],,}" ]]; then
      echo "$train: nuget.org still gives $found as the base of ${head:0:7}, as when this run decided." >&2
    else
      fail "$train: nuget.org now gives $found (${from:0:7}) as the base of ${head:0:7}, where this run decided from ${expect_base[$train]}: something was published since. Dispatch preview.yml again rather than re-running this run."
    fi
  done
  exit 0
fi

# What each base commit packed of its train: it may hold an ID that HEAD no longer does, and lack one HEAD adds.
for train in "${trains[@]}"; do
  base_ids="$(ids_at "${base[$train]}" | of_train "$train")"
  [[ -n "$base_ids" ]] || continue
  mapfile -t base_ids <<<"$base_ids"
  record "${base_ids[@]}"
  gaps=()
  for id in "${base_ids[@]}"; do
    has_version "$id" "${base_version[$train]}" || gaps+=("$id")
  done
  missing[$train]="${gaps[*]}"
done

# release.yml asks, train by train, whether HEAD ships anything that no version of the train on nuget.org carries:
# what changed since its base, and the packages the base version never reached, which carry none of it.
if $report; then
  for train in "${trains[@]}"; do
    version="${base_version[$train]}" from="${base[$train]}"
    package_inputs_changed "$from" "$head"
    if [[ -n "${missing[$train]}" ]]; then
      read -ra gaps <<<"${missing[$train]}"
      hint="Dispatching preview.yml first completes it."
      [[ "$version" == *-* ]] || hint="Complete that release by hand first (docs/maintaining.md)."
      {
        echo "### Partly published: $train"
        echo
        echo "$version, the version of the $train train on nuget.org built from the nearest commit, \`${from:0:7}\`, is missing for ${#gaps[@]} of its packages, which therefore carry none of what changed up to that commit:"
        echo
        # shellcheck disable=SC2016 # backticks for Markdown
        printf -- '- `%s`\n' "${gaps[@]}"
        echo
        echo "$hint"
      } >>"$summary"
      echo "::warning title=Partly published::$train: $version is missing from nuget.org for ${gaps[*]}." >&2
    fi
    if [[ -z "$changed" ]]; then
      [[ -n "${missing[$train]}" ]] ||
        echo "Every package input of the $train train in this release is already on nuget.org, in $version (\`${from:0:7}\`)." >>"$summary"
      continue
    fi
    kind=preview
    [[ "$version" == *-* ]] || kind=release
    {
      echo "### Not previewed: $train"
      echo
      echo "This release ships package inputs that no preview of the $train train carried. They changed since $version, the $kind of the train on nuget.org built from the nearest commit, \`${from:0:7}\`:"
      echo
      # shellcheck disable=SC2016 # backticks for Markdown
      sed 's/^/- `/; s/$/`/' <<<"$changed"
      echo
      echo "Dispatching preview.yml first publishes them as a preview."
    } >>"$summary"
    echo "::warning title=Not previewed::$train: this release ships package inputs that no preview carried, changed since $version: $(paste -sd ' ' <<<"$changed")" >&2
  done
  exit 0
fi

# --- decision --------------------------------------------------------------------------------

declare -A next=() type=()
parse_pairs next NEXT_VERSIONS
parse_pairs type RELEASE_TYPES true

mode="" ref="$head" pending="" docs_ref="$head" reason=""
# shellcheck disable=SC2034 # pack is read through pairs_of's name reference
declare -A version=() pack=()
push=()

repairing=()
for train in "${trains[@]}"; do
  [[ -z "${missing[$train]}" ]] || repairing+=("$train")
done

if (( ${#repairing[@]} > 0 )); then
  # One commit is repaired per run: the base commit of the first train missing IDs, the core first. A train missing
  # IDs from another commit waits for no one: two partial versions at two commits are completed by hand.
  mode=repair
  ref="${base[${repairing[0]}]}"
  for train in "${repairing[@]}"; do
    [[ "${base[$train]}" == "$ref" ]] ||
      fail "$train: ${base_version[$train]} is missing from nuget.org for ${missing[$train]}, packed from ${base[$train]:0:7}, while ${repairing[0]} is repaired from ${ref:0:7}: complete $train ${base_version[$train]} by hand (docs/maintaining.md), then dispatch again."
    [[ "${base_version[$train]}" =~ ^[0-9]+\.[0-9]+\.[0-9]+-preview\.[0-9]+$ ]] ||
      fail "$train: ${base_version[$train]} is missing from nuget.org for ${missing[$train]}, but it is not a version preview.yml publishes (a stable release, or a preview from before it): complete it by hand (docs/maintaining.md)."
    version[$train]="${base_version[$train]}"
    pack[$train]="${version[$train]}:repair"
    push+=("$train")
  done
  reason="Completing from ${ref:0:7}:"
  for train in "${repairing[@]}"; do
    reason+=" $train ${version[$train]}, missing for ${missing[$train]};"
  done
  reason="${reason%;}."

  # Every other train is packed at the repaired commit too, for the tests and the dependency checks, and never pushed.
  for train in "${trains[@]}"; do
    [[ ! -v "version[$train]" ]] || continue
    if [[ "${base[$train]}" == "$ref" ]]; then
      # Whole at that commit: packed at the version nuget.org has from it.
      version[$train]="${base_version[$train]}"
    elif [[ "$train" == core ]]; then
      # The core the repaired packages were packed against: the floor of their dependency on it. The core's packages
      # are those the repaired commit packed.
      core_at_ref="$(ids_at "$ref" | of_train core | paste -sd ' ')"
      [[ -n "$core_at_ref" ]] || fail "core: ${ref:0:7} packs no package of the core."
      # shellcheck disable=SC2086 # one ID per word
      record $core_at_ref
      floors=""
      for repaired in "${repairing[@]}"; do
        read -ra repaired_ids <<<"${ids[$repaired]}"
        for id in "${repaired_ids[@]}"; do
          has_version "$id" "${version[$repaired]}" || continue
          # shellcheck disable=SC2086 # one ID per word
          floors+="$(nuspec_of "$id" "${version[$repaired]}" |
            { grep -oP '<dependency\b[^>]*\bid="\K[^"]+"[^>]*\bversion="\[[^,\]]+' || true; } |
            awk -F'"' -v core=" $core_at_ref " 'index(tolower(core), " " tolower($1) " ") { sub(/.*\[/, ""); print }')"$'\n'
        done
      done
      floors="$(sed '/^$/d' <<<"$floors" | LC_ALL=C sort -u)"
      [[ -n "$floors" ]] ||
        fail "core: the repaired packages name no dependency on the core, so the core they were packed against is unknown: complete them by hand (docs/maintaining.md)."
      (( $(wc -l <<<"$floors") == 1 )) ||
        fail "core: the repaired packages were packed against several cores ($(paste -sd ' ' <<<"$floors")): complete them by hand (docs/maintaining.md)."
      version[core]="${floors,,}"
      read -ra core_ids <<<"$core_at_ref"
      for id in "${core_ids[@]}"; do
        has_version "$id" "${version[core]}" ||
          fail "core: the repaired packages were packed against core ${version[core]}, which nuget.org lacks for $id: complete the core first (docs/maintaining.md)."
      done
    else
      # A placeholder at the core's version, which the loop has set already (the core comes first): nothing of it is
      # pushed, and its dependencies on the core point at the core packed beside it.
      version[$train]="${version[core]}"
    fi
    pack[$train]="${version[$train]}:repair"
  done

  package_inputs_changed "$ref" "$head"
  if [[ -n "$changed" ]]; then
    pending=true
    docs_ref="$ref"
    reason+=" HEAD has further package changes, for the next run."
  else
    pending=false
  fi
else
  # What changed since each train's base: what that train's packages on nuget.org do not carry yet. Every train's own,
  # since a later commit can revert what lies between two bases, which then leaves the newer one alone behind HEAD.
  # Trains on one base commit share their changes, and the reason names them together.
  union="" reason="" seen=" "
  for train in "${trains[@]}"; do
    from="${base[$train]}"
    [[ "$seen" != *" $from "* ]] || continue
    seen+="$from "
    package_inputs_changed "$from" "$head"
    [[ -n "$changed" ]] || continue
    union+="$changed"$'\n'
    named=()
    for other in "${trains[@]}"; do
      [[ "${base[$other]}" != "$from" ]] || named+=("$other ${base_version[$other]}")
    done
    joined="${named[0]}"
    for (( index = 1; index < ${#named[@]}; index++ )); do joined+=" and ${named[$index]}"; done
    reason+="; since $joined (${from:0:7}): $(paste -sd ' ' <<<"$changed")"
  done
  changed="$(sed '/^$/d' <<<"$union" | awk '!seen[$0]++')"
  if [[ -n "$changed" ]]; then
    mode=publish
    reason="Package inputs changed ${reason#; }"
  else
    mode=none
    reason="No package input changed since"
    for train in "${trains[@]}"; do
      reason+=" $train ${base_version[$train]} (${base[$train]:0:7}),"
    done
    reason="${reason%,}."
  fi
fi

# A publish takes each train's version next-version.mjs computed for HEAD. That job runs third-party JavaScript, so
# only the release type is taken from it: the version is computed again here, from the train's last stable tag and
# the commits since, and must be the same. A major is refused while the list rates the train's breaking change a
# minor, which the commit analyzer then cannot answer. The line rule is checked too: a train other than the core
# packed against a core of another line than the one its last release was packed against breaks, whatever its own
# commits. The version must also be new: one that nuget.org already has was packed from another commit, and pushing
# would skip every package as a duplicate.
if [[ "$mode" == publish ]]; then
  level() { case "$1" in major) echo 3 ;; minor) echo 2 ;; patch) echo 1 ;; *) echo 0 ;; esac; }
  line_of() { IFS=. read -r major minor _ <<<"$1"; if [[ "$major" == 0 ]]; then echo "0.$minor"; else echo "$major"; fi; }
  bump() {
    local major minor patch
    IFS=. read -r major minor patch <<<"$1"
    case "${2:-patch}" in
      major) echo "$((10#$major + 1)).0.0" ;;
      minor) echo "$major.$((10#$minor + 1)).0" ;;
      patch) echo "$major.$minor.$((10#$patch + 1))" ;;
    esac
  }
  for train in "${trains[@]}"; do
    [[ "${type[$train]}" =~ ^(major|minor|patch)?$ ]] ||
      fail "RELEASE_TYPES: '${type[$train]}' is not major, minor, patch or empty for the $train train."
  done
  # The core's next stable version, which every other train is packed against: its last one when it releases nothing.
  core_version="${last[core]}"
  [[ -z "${type[core]}" ]] || core_version="$(bump "${last[core]}" "${type[core]}")"

  for train in "${trains[@]}"; do
    candidate="${next[$train]}"
    if [[ "${base[$train]}" == "$head" && "${base_version[$train]}" == "${candidate,,}" ]]; then
      # nuget.org has it whole from HEAD already: packed again for the tests, never pushed.
      version[$train]="$candidate"
      pack[$train]="$candidate:repair"
      continue
    fi
    [[ "$candidate" =~ ^[0-9]+\.[0-9]+\.[0-9]+-preview\.[0-9]+$ ]] ||
      fail "NEXT_VERSIONS: '$candidate' is not of the X.Y.Z-preview.N shape that next-version.mjs computes, for the $train train."
    kind="${type[$train]}"
    [[ "$kind" != major || "${breaking[$train]}" != minor ]] ||
      fail "RELEASE_TYPES: major for the $train train, which the commit analyzer cannot answer while src/release-trains.json rates its breaking change a minor."
    if [[ "$train" != core ]]; then
      # The core the train's last release was packed against: the newest core tag merged into the train's own.
      previous="$(git tag --merged "${prefix[$train]}${last[$train]}" --list 'v*' |
        sed -n 's/^v\([0-9]\{1,\}\.[0-9]\{1,\}\.[0-9]\{1,\}\)$/\1/p' | newest)"
      [[ -n "$previous" ]] || fail "No core tag v<major>.<minor>.<patch> is merged into ${prefix[$train]}${last[$train]}."
      if [[ "$(line_of "$core_version")" != "$(line_of "$previous")" ]] &&
        (( $(level "$kind") < $(level "${breaking[$train]}") )); then
        fail "RELEASE_TYPES: ${kind:-nothing} for the $train train, packed against core $core_version where its last release was packed against $previous: the core's line moved, so the train releases a ${breaking[$train]} at least."
      fi
    fi
    expected="$(bump "${last[$train]}" "$kind")-preview.$(git rev-list --count "${prefix[$train]}${last[$train]}..$head")"
    [[ "$candidate" == "$expected" ]] ||
      fail "NEXT_VERSIONS: $candidate is not $expected, the ${kind:-patch} after ${prefix[$train]}${last[$train]} at this commit's height, for the $train train."
    # shellcheck disable=SC2086 # one ID per word
    holder="$(holder_in "${candidate,,}" ${ids[$train]})"
    [[ -z "$holder" ]] ||
      fail "$train: $candidate is already on nuget.org ($holder), packed from another commit. The preview number counts commits since the train's last stable tag, so a preview left above a lower release (a feature reverted after its preview shipped) can hold it: the next commit on main moves the number."
    # shellcheck disable=SC2086 # one ID per word
    newest_version="$(versions_in ${ids[$train]} | newest || true)"
    if [[ -n "$newest_version" ]] && ! above "${candidate,,}" "$newest_version"; then
      # Below a version of the train's own (a feature reverted after its preview shipped), NuGet only offers the higher
      # one for a while. Below a version a package was packed at on another train, it never offers the train's again:
      # ADR 0012 moves a package only once the train's next version is above the package's last one. Each package's
      # own last version says which.
      read -ra train_ids <<<"${ids[$train]}"
      for id in "${train_ids[@]}"; do
        own="$(versions_in "$id" | newest || true)"
        if [[ -z "$own" ]] || above "${candidate,,}" "$own"; then continue; fi
        packed="$(commit_of "$id" "$own")"
        rode "$train" "$packed" "$id" ||
          fail "$train: $candidate sorts below $own, which $id was packed at from ${packed:0:7} on another train: a package moves to a train only once the train's next version is above the package's last one (ADR 0012). Revert the move until then."
      done
      echo "::warning title=Below an earlier version::$train: $candidate sorts below $newest_version, which nuget.org already has: NuGet offers $newest_version as the latest prerelease until a higher version ships." >&2
    fi
    version[$train]="$candidate"
    # shellcheck disable=SC2034 # read through pairs_of's name reference
    pack[$train]="$candidate:${kind:-patch}:${last[$train]}"
    push+=("$train")
  done
fi

docs_version="${base_version[core]}"
if [[ "$mode" == publish ]]; then docs_version="${version[core]}"; fi

echo "$reason" >&2
{
  echo "mode=$mode"
  echo "ref=$ref"
  echo "versions=$(pairs_of version)"
  echo "pack=$(pairs_of pack)"
  echo "push=${push[*]}"
  echo "bases=$(pairs_of base_version)"
  echo "base-commits=$(pairs_of base)"
  echo "last-stables=$(pairs_of last)"
  echo "docs-ref=$docs_ref"
  echo "docs-version=$docs_version"
  echo "pending=$pending"
  echo "reason=$reason"
} >>"$output"
{
  echo "### Preview: $mode"
  echo
  echo "$reason"
  echo
  echo "| Train | Last stable | Base | Packed at | Pushed |"
  echo "| --- | --- | --- | --- | --- |"
  for train in "${trains[@]}"; do
    pushed=no
    for pushing_train in "${push[@]}"; do [[ "$pushing_train" != "$train" ]] || pushed=yes; done
    echo "| $train | ${prefix[$train]}${last[$train]} | ${base_version[$train]} (\`${base[$train]:0:7}\`) | ${version[$train]:-} | $pushed |"
  done
  if [[ -n "$changed" ]]; then
    echo
    echo "Package inputs changed:"
    echo
    # shellcheck disable=SC2016 # backticks for Markdown
    sed 's/^/- `/; s/$/`/' <<<"$changed"
  fi
} >>"$summary"
