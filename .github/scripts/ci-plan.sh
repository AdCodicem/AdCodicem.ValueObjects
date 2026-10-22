#!/usr/bin/env bash
# Decides what a run of ci.yml builds and tests, and which of its jobs the gate, the required check "build and test",
# holds to success (docs/adr/0013-run-a-targeted-pull-request-ci-and-the-heavy-jobs-before-a-release.md).
#
# Usage: EVENT_NAME=<event> LABELS=<JSON array> AUTHOR=<login> HEAD_REF=<branch> HEAD_REPO=<owner/name> \
#          REPOSITORY=<owner/name> ci-plan.sh [--graph-needed]
#        ci-plan.sh --codecov-flags
#
#   EVENT_NAME  github.event_name: pull_request or push.
#   LABELS      the pull request's labels as the event carries them, when the run starts: a label added later is not
#               seen, not even by "Re-run jobs", until a push starts a new run.
#   AUTHOR      the login that opened the pull request, never github.actor, which is whoever pushed last: the
#               maintainer pushing a line of .github/shipped-dependencies to a Dependabot branch would otherwise drop
#               the heavy jobs.
#   HEAD_REF    its branch.
#   HEAD_REPO   the repository its branch lives in, and REPOSITORY this one: a fork's branch named dependabot/nuget/...
#               is not Dependabot's.
#
# --graph-needed writes only `graph`: true when the plan asks dotnet-affected, which needs the .NET SDK of global.json,
# so that the workflow installs it only then. --codecov-flags prints the flags of the families, which codecov.yml holds
# between its two "ci-families" markers.
#
# A pull request's plan with files under src/ puts README.md, icon.png and the CLAUDE.md files it changes back to the
# base in the checkout: plan locally in a scratch clone.
#
# Every run first checks .github/ci-families.json against the repository, without a build: every project under src/
# in exactly one family, every top-level folder of the unit suite mapped, every file of the unit suite declaring the
# namespace of its folder (the root namespace at the root), every folder a query names mapped to the query's family
# and taken with its sub-namespaces, and codecov.yml's flags as --codecov-flags prints them. So no test can fall outside
# every family's filter.
#
# A push to main runs everything. A pull request runs what the files its merge commit changes against the base branch
# (its first parent, hence a checkout depth of 2) select, each file by the first rule below that matches it:
#   none           CLAUDE.md files, docs/ but docs/docfx/, Markdown outside src/, website/ and skills/ but README.md,
#                  the repository's own settings and templates, and the workflows and scripts ci.yml never runs, each
#                  by name, so that a new one runs everything until it is given a rule; only lint.yml checks them.
#   documentation  README.md, website/docs/ and skills/value-objects/: the generator suite's tests that read them,
#                  [Trait("Reads", "Documentation")]. README.md is also packed, website/docs/ also built into the site.
#   site           website/, docs/docfx/ and the action that builds the site: the job "documentation site".
#   everything     what every project or job reads: Directory.Build.*, Directory.Packages.props, global.json,
#                  nuget.config, .editorconfig, .gitattributes, the solution files, the local tools, ci.yml, this
#                  script, the family table, coverage-modules.sh, unit-families-check.sh, codecov.yml,
#                  tests/Directory.Build.*, and any path no rule names.
#   release        package.json, its lock file, .releaserc.json and next-version.mjs: the job "release tooling".
#   pack           icon.png, .github/shipped-dependencies and the scripts that check the packed set.
#   src            src/: the projects dotnet-affected (.config/dotnet-tools.json) marks changed or affected, through
#                  their references, the files of src/Shared/ they link and the files they import, each selecting its
#                  family, and a project of core every family, since every test runs the generator's output and the
#                  registry; the generator suite, the RDG suite and the compiled model when it marks them. Every
#                  project item counts, so README.md, icon.png and every CLAUDE.md are first put back as the base branch
#                  has them: every package packs the first two and globs the third. Any exit of the tool but 0, and 166
#                  for "nothing affected", runs everything.
#   unit           the unit suite: a file of a folder selects the families of unit.folders, a .cs file of tests at its
#                  root every family with a class in the root namespace, and every family a file of unit.shared, any
#                  other file at its root (a helper, a test configuration), or a .cs file declaring something of the
#                  whole assembly (an [assembly:] or [module:] attribute, a module initializer) before or after the
#                  change.
#   generator      the generator suite, in full.
#   rdg            the Request Delegate Generator suite.
#   fixtures       tests/Fixtures/ but the Functions fixture: every family of the unit suite, which loads them.
#   heavy          what only heavy.yml runs (tests/NativeAot/, tests/Compat/, the integration suite and its Functions
#                  fixture, samples/, native-aot.sh, compiled-model.sh, heavy.yml): a notice asks for the ci:heavy
#                  label; the code among them is built (tests/Compat/, which no solution holds, is only formatted), and
#                  the compiled model's own inputs, tests/NativeAot/'s Directory.Build.props and compiled-model.sh
#                  also generate and compile the model.
#   build          benchmarks/: built.
# Then a family selects every family whose dependsOn names it; core runs the generator suite in full, aspnet the RDG
# suite, efcore the compiled model, text the patterns written for browsers; any affected project under src/ packs and
# builds the site; any .NET lane formats and builds.
#
# heavy.yml runs on a pull request labelled ci:heavy, or one Dependabot opened to update a NuGet package, since
# auto-merge then lands it with nobody reading it. Otherwise it runs after the merge, on its own push to main.
#
# Outputs, to $GITHUB_OUTPUT (stdout outside Actions):
#   heavy           true when the run calls heavy.yml
#   expected        the JSON array of the jobs of ci.yml the gate requires to succeed; it passes any other job it needs
#                   that was skipped
#   format          true when the job format runs: with build, and for tests/Compat/, which no solution holds
#   dotnet          true when the job build runs
#   families        the JSON array of the families whose unit tests run, and whose projects coverage-modules.sh checks
#   unit-filter     the JSON array of the unit suite's --filter-query values, empty when every family runs
#   generator       full, documentation or none
#   rdg, compiled-model, ecmascript, pack, site, release
#                   true when the lane runs
#   coverage        the JSON array of the Codecov flags the run uploads, one upload each
# A summary goes to $GITHUB_STEP_SUMMARY when Actions sets it.
set -euo pipefail

cd "$(dirname "$0")/../.."

table=.github/ci-families.json
unit_suite=tests/AdCodicem.ValueObjects.UnitTests
unit_namespace=AdCodicem.ValueObjects.UnitTests

fail() {
  echo "::error::$*" >&2
  exit 1
}

# codecov.yml's flags: one per family, over its projects and the files of src/Shared/ they link. The generator, RDG and
# integration flags cover src/ and are written in codecov.yml itself.
codecov_flags() {
  local family project shared
  while IFS= read -r family; do
    echo "    - name: $family"
    echo "      paths:"
    while IFS= read -r project; do
      echo "        - src/$project/"
    done < <(jq -r --arg family "$family" '.families[$family].projects[]' "$table")
    while IFS= read -r shared; do
      echo "        - src/Shared/$shared"
    done < <(jq -r --arg family "$family" '.families[$family].projects[] | "src/\(.)/\(.).csproj"' "$table" |
      xargs grep -hoE 'Include="\.\.[\\/]Shared[\\/][^"]*"' | sed -E 's#^Include="\.\.[\\/]Shared[\\/]##; s#"$##; s#\\#/#g' |
      LC_ALL=C sort -u)
  done < <(jq -r '.families | keys_unsorted[]' "$table")
}

check_table() {
  local errors
  errors="$(jq -r '
    (.families | keys) as $families
    | (if (.families | type) != "object" or (.families | length) == 0 then "families is empty" else empty end),
      (.families | to_entries[] | .key as $family | .value
        | (if (.train | IN("core", "satellites")) then empty else "\($family): train is neither core nor satellites" end),
          (if (.projects | type) == "array" and (.projects | length) > 0 and all(.projects[]; type == "string")
           then empty else "\($family): projects is not a list of names" end),
          (.dependsOn[] | select(IN($families[]) | not) | "\($family): dependsOn names no family, \(.)"),
          (if (.unit | type) == "array" and (.unit | length) > 0 and all(.unit[]; type == "string" and startswith("/"))
           then empty else "\($family): unit is not a list of queries" end)),
      (if (.guards | length) > 0 and all(.guards[]; type == "string" and startswith("/")) then empty
       else "guards is not a list of queries" end),
      (.unit.folders | to_entries[]
        | select(.value != "*" and ((.value | type) != "array" or (.value | length) == 0 or any(.value[]; IN($families[]) | not)))
        | "unit.folders.\(.key) is neither \"*\" nor a list of families"),
      (if all(.unit.shared[]; type == "string") then empty else "unit.shared is not a list of paths" end),
      ([.families[].projects[]] | group_by(.)[] | select(length > 1) | "\(.[0]) is in \(length) families")
  ' "$table")" || fail "$table is not the family table ci-plan.sh reads."
  [[ -z "$errors" ]] || fail "$table: $(paste -sd ';' <<<"$errors" | sed 's/;/; /g')."

  local listed actual extra missing
  listed="$(jq -r '.families[].projects[]' "$table" | LC_ALL=C sort -u)"
  actual="$(for project in src/*/*.csproj; do basename "$project" .csproj; done | LC_ALL=C sort -u)"
  missing="$(LC_ALL=C comm -13 <(echo "$listed") <(echo "$actual") | paste -sd ' ' -)"
  extra="$(LC_ALL=C comm -23 <(echo "$listed") <(echo "$actual") | paste -sd ' ' -)"
  [[ -z "$missing" ]] || fail "$table puts no family over $missing: add each project under src/ to one family."
  [[ -z "$extra" ]] || fail "$table names $extra, which is no project under src/."

  listed="$(jq -r '.unit.folders | keys[]' "$table" | LC_ALL=C sort -u)"
  actual="$(git -c core.quotePath=false ls-files "$unit_suite" | sed "s|^$unit_suite/||" | awk -F/ 'NF > 1 { print $1 }' | LC_ALL=C sort -u)"
  missing="$(LC_ALL=C comm -13 <(echo "$listed") <(echo "$actual") | paste -sd ' ' -)"
  extra="$(LC_ALL=C comm -23 <(echo "$listed") <(echo "$actual") | paste -sd ' ' -)"
  [[ -z "$missing" ]] || fail "$table maps no family to $missing of $unit_suite: add each folder to unit.folders."
  [[ -z "$extra" ]] || fail "$table maps $extra, which is no folder of $unit_suite."

  local shared file matched
  local -a suite_files
  mapfile -t suite_files < <(git -c core.quotePath=false ls-files "$unit_suite" | sed "s|^$unit_suite/||")
  while IFS= read -r shared; do
    matched=false
    for file in "${suite_files[@]}"; do
      # shellcheck disable=SC2053 # a glob of the table
      if [[ "$file" == $shared ]]; then
        matched=true
        break
      fi
    done
    [[ "$matched" == true ]] || fail "$table: unit.shared names $shared, which matches no file of $unit_suite."
  done < <(jq -r '.unit.shared[]' "$table")

  # A class sits in the namespace of its folder, which the families' queries filter on: one in another namespace would
  # be selected by the path of its file and filtered by its namespace, so the two must agree.
  local relative expected namespace
  while IFS= read -r file; do
    relative="${file#"$unit_suite/"}"
    while IFS= read -r namespace; do
      if [[ "$relative" == */* ]]; then
        expected="$unit_namespace.${relative%%/*}"
        [[ "$namespace" == "$expected" || "$namespace" == "$expected".* ]] && continue
      else
        expected="$unit_namespace"
        [[ "$namespace" == "$expected" ]] && continue
      fi
      fail "$file declares the namespace $namespace, where its folder filters on $expected."
    done < <(sed -n -E 's/^namespace[[:space:]]+([A-Za-z0-9_.]+).*/\1/p' "$file")
  done < <(git -c core.quotePath=false ls-files "$unit_suite/*.cs")

  # A query naming a folder's namespace belongs to a family that a change to the folder selects, and every folder is
  # named by a query.
  errors="$(jq -r --arg ns "$unit_namespace" '
    .unit.folders as $folders
    | ([.families | to_entries[] | .key as $family | .value.unit[] | {family: $family, query: .}]
       + [.guards[] | {family: null, query: .}]) as $queries
    | ($queries[] | .family as $family | .query
        | [scan(($ns | gsub("\\."; "\\.")) + "\\.([A-Za-z0-9_]+)") | .[0]] | unique[]
        | select($family != null and (($folders[.] // []) as $mapped | ($mapped == "*" or ($mapped | index($family))) | not))
        | "a query of \($family) filters on the folder \(.), whose unit.folders entry does not select \($family)"),
      ($queries[] | .query | . as $query
        | [scan(($ns | gsub("\\."; "\\.")) + "\\.([A-Za-z0-9_]+)") | .[0]] | unique[]
        | . as $folder | select($query | contains("(" + $ns + "." + $folder + ".*)") | not)
        | "the query \($query) filters on the folder \(.) without its sub-namespaces, (\($ns).\(.).*)"),
      ($folders | keys[] | . as $folder
        | select([$queries[].query | test(($ns | gsub("\\."; "\\.")) + "\\." + $folder + "\\b")] | any | not)
        | "no query filters on the folder \($folder)")
  ' "$table")" || fail "$table: its queries could not be read."
  [[ -z "$errors" ]] || fail "$table: $(paste -sd ';' <<<"$errors" | sed 's/;/; /g')."

  if ! diff <(codecov_flags) <(sed -n '/# BEGIN ci-families/,/# END ci-families/{//!p;}' codecov.yml) > /dev/null; then
    fail "codecov.yml's flags differ from $table: replace the lines between its ci-families markers with the output of .github/scripts/ci-plan.sh --codecov-flags."
  fi
}

case "${1:-}" in
  --codecov-flags)
    codecov_flags
    exit 0
    ;;
  --graph-needed | '') ;;
  *) fail "ci-plan.sh takes --graph-needed, --codecov-flags or no argument, not $1." ;;
esac
mode="${1:-plan}"

check_table

event="${EVENT_NAME:?EVENT_NAME is required}"
labels="${LABELS:-[]}"
[[ "$labels" == null ]] && labels='[]'
author="${AUTHOR:-}"
head_ref="${HEAD_REF:-}"
head_repo="${HEAD_REPO:-}"
repository="${REPOSITORY:-}"

if ! jq -e 'type == "array" and all(.[]; type == "string")' <<<"$labels" > /dev/null 2>&1; then
  fail "LABELS is not a JSON array of names: $labels"
fi

mapfile -t all_families < <(jq -r '.families | keys_unsorted[]' "$table")
mapfile -t root_families < <(jq -r --arg ns "$unit_namespace" \
  '.families | to_entries[] | select(any(.value.unit[]; startswith("/*/" + $ns + "/"))) | .key' "$table")

declare -A family=()
everything=false
everything_reason=''
every_family=false
documentation=false
generator_full=false
rdg=false
compiled_model=false
pack=false
site=false
release=false
dotnet=false
format=false
src_paths=()
heavy_inputs=()

run_everything() {
  if [[ "$everything" == false ]]; then
    everything=true
    everything_reason="$1"
  fi
}

# The rule that selects what a changed file runs; the header lists them.
rule() {
  case "$1" in
    CLAUDE.md | */CLAUDE.md) echo none; return ;;
    README.md) echo readme; return ;;
    src/* | website/* | skills/*) ;;
    *.md) echo none; return ;;
  esac
  case "$1" in
    docs/docfx/*) echo site ;;
    docs/*) echo none ;;
    website/docs/*) echo website-docs ;;
    skills/value-objects/*) echo documentation ;;
    LICENSE | .gitignore | .pre-commit-config.yaml | .claude-plugin/* | .github/ISSUE_TEMPLATE/* | \
      .github/DISCUSSION_TEMPLATE/* | .github/CODEOWNERS | .github/FUNDING.yml | .github/dependabot.yml | \
      .github/actionlint.yaml | .github/requirements/*) echo none ;;
    .github/workflows/ci.yml) echo everything ;;
    .github/workflows/heavy.yml) echo heavy-notice ;;
    .github/workflows/benchmarks.yml | .github/workflows/dependabot-auto-merge.yml | \
      .github/workflows/deploy-docs.yml | .github/workflows/lint.yml | .github/workflows/preview.yml | \
      .github/workflows/release.yml | .github/workflows/scorecards.yml | .github/workflows/stale.yml) echo none ;;
    .github/actions/build-site/* | .github/scripts/docfx-postprocess.py) echo site ;;
    .github/ci-families.json | .github/scripts/ci-plan.sh | .github/scripts/coverage-modules.sh | \
      .github/scripts/unit-families-check.sh) echo everything ;;
    .github/scripts/native-aot.sh) echo heavy-notice ;;
    .github/scripts/compiled-model.sh) echo compiled-model ;;
    .github/scripts/ecmascript-patterns.mjs) echo text ;;
    .github/scripts/next-version.mjs) echo release ;;
    .github/shipped-dependencies | .github/scripts/verify-packages.sh | .github/scripts/package-ids.sh | \
      .github/scripts/shipped-dependencies-check.sh) echo pack ;;
    .github/scripts/dependabot-commit-type.sh | .github/scripts/docs-snapshot.sh | \
      .github/scripts/package-assemblies.sh | .github/scripts/preview-gate.sh | .github/scripts/push-packages.sh | \
      .github/scripts/release-pack.sh | .github/scripts/semver.jq) echo none ;;
    package.json | package-lock.json | .releaserc.json) echo release ;;
    website/*) echo site ;;
    Directory.Build.* | Directory.Packages.props | global.json | nuget.config | .editorconfig | .gitattributes | \
      AdCodicem.ValueObjects.slnx | src/AdCodicem.ValueObjects.Packages.slnf | .config/dotnet-tools.json | \
      codecov.yml | tests/Directory.Build.*) echo everything ;;
    icon.png) echo pack ;;
    src/*) echo src ;;
    "$unit_suite"/*) echo unit ;;
    tests/AdCodicem.ValueObjects.GeneratorTests/*) echo generator ;;
    tests/AdCodicem.ValueObjects.RdgTests/*) echo rdg ;;
    tests/AdCodicem.ValueObjects.IntegrationTests/* | tests/Fixtures/AdCodicem.ValueObjects.Fixtures.Functions/*) echo heavy ;;
    tests/Fixtures/*) echo fixtures ;;
    tests/NativeAot/AdCodicem.ValueObjects.CompiledModel/* | \
      tests/NativeAot/AdCodicem.ValueObjects.NativeAot.Domain/* | tests/NativeAot/Directory.*) echo compiled-model ;;
    tests/Compat/*) echo heavy-format ;;
    tests/NativeAot/* | samples/*) echo heavy ;;
    benchmarks/*) echo build ;;
    *) echo unknown ;;
  esac
}

# The families a file of the unit suite selects.
unit_families() {
  local path="$1" relative="${1#"$unit_suite/"}" shared
  while IFS= read -r shared; do
    # shellcheck disable=SC2053 # a glob of the table
    if [[ "$relative" == $shared ]]; then
      every_family=true
      return
    fi
  done < <(jq -r '.unit.shared[]' "$table")
  # What applies to the whole assembly, as the base has it or as the change leaves it, applies to every test. Read
  # whole first: grep -q would close a pipe early, which pipefail reports as a failure.
  local versions
  versions="$(git show "HEAD:$path" 2> /dev/null || true; git show "HEAD^1:$path" 2> /dev/null || true)"
  if [[ "$relative" == *.cs ]] && grep -qE '^[[:space:]]*\[(assembly|module):|ModuleInitializer' <<<"$versions"; then
    every_family=true
    return
  fi
  # At the root: a file that is no source, such as a test configuration, applies to every test, and a source holding no
  # test is a helper, which any folder's tests may use without an import.
  if [[ "$relative" != */* ]] && { [[ "$relative" != *.cs ]] || ! grep -qE '\[(Fact|Theory)' <<<"$versions"; }; then
    every_family=true
    return
  fi
  if [[ "$relative" != */* ]]; then
    local name
    for name in "${root_families[@]}"; do family[$name]=1; done
    return
  fi
  local selected
  selected="$(jq -c --arg folder "${relative%%/*}" '.unit.folders[$folder] // "*"' "$table")"
  if [[ "$selected" == '"*"' ]]; then
    every_family=true
    return
  fi
  while IFS= read -r name; do family[$name]=1; done < <(jq -r '.[]' <<<"$selected")
}

heavy=false
heavy_reason="it runs on its own push to main once this is merged (heavy.yml)"
changed=()
case "$event" in
  pull_request)
    if jq -e 'index("ci:heavy") != null' <<<"$labels" > /dev/null; then
      heavy=true
      heavy_reason="the pull request is labelled ci:heavy"
    elif [[ "$author" == 'dependabot[bot]' && "$head_ref" == dependabot/nuget/* && -n "$repository" &&
      "$head_repo" == "$repository" ]]; then
      heavy=true
      heavy_reason="Dependabot updates a NuGet package, and auto-merge lands it with nobody reading it"
    fi
    if diff="$(git -c core.quotePath=false diff --name-only --no-renames HEAD^1 HEAD 2> /dev/null)"; then
      mapfile -t changed < <(grep . <<<"$diff" || true)
    else
      echo "::warning::The files this pull request changes could not be read (the checkout needs a depth of 2)." >&2
      run_everything "the files this pull request changes could not be read"
    fi
    ;;
  push)
    heavy_reason="heavy.yml runs on this push itself"
    run_everything "a push to main runs everything"
    ;;
  *)
    fail "ci-plan.sh plans pull_request and push runs, not $event."
    ;;
esac

for path in "${changed[@]}"; do
  case "$(rule "$path")" in
    none) ;;
    readme) documentation=true; pack=true ;;
    website-docs) documentation=true; site=true ;;
    documentation) documentation=true ;;
    site) site=true ;;
    everything) run_everything "$path is read by every project or job" ;;
    release) release=true ;;
    pack) pack=true ;;
    src) src_paths+=("$path") ;;
    unit) unit_families "$path" ;;
    generator) generator_full=true ;;
    rdg) rdg=true ;;
    fixtures) every_family=true ;;
    heavy) heavy_inputs+=("$path"); dotnet=true ;;
    heavy-format) heavy_inputs+=("$path"); format=true ;;
    heavy-notice) heavy_inputs+=("$path") ;;
    compiled-model) heavy_inputs+=("$path"); compiled_model=true; dotnet=true ;;
    text) family[text]=1 ;;
    build) dotnet=true ;;
    *) run_everything "no rule of ci-plan.sh names $path" ;;
  esac
done

graph=false
if [[ "$everything" == false && ${#src_paths[@]} -gt 0 ]]; then
  graph=true
fi
if [[ "$mode" == --graph-needed ]]; then
  echo "graph=$graph" >> "${GITHUB_OUTPUT:-/dev/stdout}"
  exit 0
fi

affected=()
if [[ "$graph" == true ]]; then
  dotnet=true
  # Put back what every project globs or packs, so that only the project's own inputs mark it. This rewrites the
  # checkout, which a runner discards: a local run refuses a file it would lose changes of.
  for path in "${changed[@]}"; do
    case "$path" in
      README.md | icon.png | CLAUDE.md | */CLAUDE.md)
        if [[ -z "${GITHUB_ACTIONS:-}" && -n "$(git status --porcelain -- "$path")" ]]; then
          fail "$path has uncommitted changes, which the plan would overwrite: commit them, or plan in a scratch clone."
        fi
        if git cat-file -e "HEAD^1:$path" 2> /dev/null; then
          git checkout -q HEAD^1 -- "$path"
        else
          rm -f -- "$path"
        fi
        ;;
    esac
  done
  version="$(jq -r '.tools["dotnet-affected"].version // empty' .config/dotnet-tools.json)"
  [[ -n "$version" ]] || fail ".config/dotnet-tools.json pins no version of dotnet-affected."
  tools="$(mktemp -d)"
  # Into a folder of its own: `dotnet tool restore` would download every tool of the manifest, DocFX's 100 MB among them.
  status=0
  dotnet tool install dotnet-affected --version "$version" --tool-path "$tools" > /dev/null || status=$?
  # Against the base branch, the files put back above included (--uncommitted all, the default).
  if (( status == 0 )); then
    "$tools/dotnet-affected" --from HEAD^1 --filter-file-path AdCodicem.ValueObjects.slnx --format json \
      --output-dir "$tools" --output-name affected >&2 || status=$?
  fi
  if (( status == 0 )) && [[ -f "$tools/affected.json" ]] &&
    ! jq -e 'type == "array" and all(.[]; (.Name | type) == "string" and (.FilePath | type) == "string" and
      (.FilePath | startswith("/")))' "$tools/affected.json" > /dev/null 2>&1; then
    # A report of another shape would read as nothing affected.
    status=65
  fi
  if (( status == 0 )) && [[ -f "$tools/affected.json" ]]; then
    mapfile -t affected < <(jq -r --arg src "$PWD/src/" '.[] | select(.FilePath | startswith($src)) | .Name' \
      "$tools/affected.json" | LC_ALL=C sort -u)
    # The suites and the compiled model, through what they reference: protobuf-net for the generator suite.
    while IFS= read -r project; do
      case "$project" in
        AdCodicem.ValueObjects.GeneratorTests) generator_full=true ;;
        AdCodicem.ValueObjects.RdgTests) rdg=true ;;
        AdCodicem.ValueObjects.CompiledModel) compiled_model=true ;;
      esac
    done < <(jq -r '.[].Name' "$tools/affected.json")
  elif (( status != 166 )); then
    reason="dotnet-affected could not be installed, failed or wrote a report of another shape (status $status)"
    echo "::warning::$reason: everything runs." >&2
    run_everything "$reason"
  fi
fi

if [[ "$everything" == true ]]; then
  every_family=true
  generator_full=true
  rdg=true
  compiled_model=true
  pack=true
  site=true
  release=true
fi

for project in "${affected[@]}"; do
  name="$(jq -r --arg project "$project" '.families | to_entries[] | select(.value.projects | index($project)) | .key' "$table")"
  [[ -n "$name" ]] || fail "dotnet-affected marks $project, which no family of $table holds."
  family[$name]=1
  [[ "$name" == core ]] && every_family=true
  pack=true
  site=true
done

if [[ "$every_family" == true ]]; then
  for name in "${all_families[@]}"; do family[$name]=1; done
fi

# A family selects every family that depends on it, until none is added.
added=true
while [[ "$added" == true ]]; do
  added=false
  while IFS=$'\t' read -r name dependency; do
    if [[ -n "${family[$dependency]:-}" && -z "${family[$name]:-}" ]]; then
      family[$name]=1
      added=true
    fi
  done < <(jq -r '.families | to_entries[] | .key as $family | .value.dependsOn[] | "\($family)\t\(.)"' "$table")
done

selected=()
for name in "${all_families[@]}"; do
  [[ -n "${family[$name]:-}" ]] && selected+=("$name")
done

[[ -n "${family[core]:-}" ]] && generator_full=true
[[ -n "${family[aspnet]:-}" ]] && rdg=true
[[ -n "${family[efcore]:-}" ]] && compiled_model=true
ecmascript=false
[[ -n "${family[text]:-}" ]] && ecmascript=true

generator=none
if [[ "$generator_full" == true ]]; then
  generator=full
elif [[ "$documentation" == true ]]; then
  generator=documentation
fi

if (( ${#selected[@]} > 0 )) || [[ "$generator" != none || "$rdg" == true || "$compiled_model" == true ||
  "$pack" == true ]]; then
  dotnet=true
fi

families="$(printf '%s\n' "${selected[@]}" | jq -R . | jq -sc 'map(select(length > 0))')"
if (( ${#selected[@]} == ${#all_families[@]} )); then
  unit_filter='[]'
elif (( ${#selected[@]} > 0 )); then
  unit_filter="$(jq -c --argjson families "$families" '[.families[$families[]].unit[], .guards[]]' "$table")"
else
  unit_filter='[]'
fi
coverage="$(jq -c --arg generator "$generator" --arg rdg "$rdg" \
  '. + (if $generator == "full" then ["generator"] else [] end) + (if $rdg == "true" then ["rdg"] else [] end)' \
  <<<"$families")"

expected='["plan"'
[[ "$dotnet" == true ]] && format=true
[[ "$format" == true ]] && expected+=',"format"'
[[ "$dotnet" == true ]] && expected+=',"build"'
[[ "$heavy" == true ]] && expected+=',"heavy"'
[[ "$release" == true ]] && expected+=',"release-tooling"'
expected+=']'

if [[ "$event" == pull_request && "$heavy" == false && ${#heavy_inputs[@]} -gt 0 ]]; then
  echo "::notice::This pull request changes ${#heavy_inputs[@]} file(s) only heavy.yml runs, ${heavy_inputs[0]} first: add the ci:heavy label, then push, to run it before the merge."
fi

{
  echo "heavy=$heavy"
  echo "expected=$expected"
  echo "format=$format"
  echo "dotnet=$dotnet"
  echo "families=$families"
  echo "unit-filter=$unit_filter"
  echo "generator=$generator"
  echo "rdg=$rdg"
  echo "compiled-model=$compiled_model"
  echo "ecmascript=$ecmascript"
  echo "pack=$pack"
  echo "site=$site"
  echo "release=$release"
  echo "coverage=$coverage"
} >> "${GITHUB_OUTPUT:-/dev/stdout}"

if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  {
    echo "### Plan"
    echo
    if [[ "$everything" == true ]]; then
      echo "Everything runs: $everything_reason."
    elif (( ${#changed[@]} == 0 )); then
      echo "The pull request changes no file."
    else
      echo "From the ${#changed[@]} file(s) this pull request changes:"
    fi
    echo
    if (( ${#selected[@]} == ${#all_families[@]} )); then
      echo "- the unit suite: every family"
    elif (( ${#selected[@]} > 0 )); then
      # shellcheck disable=SC2016 # the backticks are Markdown
      echo "- the unit suite: $(printf '`%s` ' "${selected[@]}")and the guards"
    else
      echo "- the unit suite: no family"
    fi
    echo "- the generator suite: $generator"
    lanes=()
    [[ "$rdg" == true ]] && lanes+=("the RDG suite")
    [[ "$compiled_model" == true ]] && lanes+=("the compiled model")
    [[ "$ecmascript" == true ]] && lanes+=("the patterns written for browsers")
    [[ "$pack" == true ]] && lanes+=("the pack and its checks")
    [[ "$site" == true ]] && lanes+=("the documentation site")
    [[ "$release" == true ]] && lanes+=("the release tooling")
    if (( ${#lanes[@]} > 0 )); then
      echo "- $(printf '%s, ' "${lanes[@]}" | sed 's/, $//')"
    fi
    if (( ${#affected[@]} > 0 )); then
      # shellcheck disable=SC2016 # the backticks are Markdown
      echo "- dotnet-affected marks under src/: $(printf '`%s` ' "${affected[@]}")"
    fi
    echo
    if [[ "$heavy" == true ]]; then
      echo "heavy.yml runs on this commit: $heavy_reason."
    else
      echo "heavy.yml does not run here: $heavy_reason."
    fi
    echo
    echo "\`build and test\` requires: $(jq -r 'map("`" + . + "`") | join(", ")' <<<"$expected")."
    if [[ "$heavy" == false && ${#heavy_inputs[@]} -gt 0 ]]; then
      echo
      echo "This pull request changes what only heavy.yml runs; add the \`ci:heavy\` label, then push, to run it here:"
      echo
      # shellcheck disable=SC2016 # the backticks are Markdown
      printf -- '- `%s`\n' "${heavy_inputs[@]}"
    fi
  } >> "$GITHUB_STEP_SUMMARY"
fi
