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
# A pull request's plan with files under src/ puts the package READMEs, icon.png and the CLAUDE.md files it changes
# back to the base in the checkout: plan locally in a scratch clone.
#
# Every run first checks .github/ci-families.json against the repository, without a build: every project under src/
# in exactly one family, every project of the unit suite a family's, every project but tests/Compat/'s in the solution,
# src/release-trains.json as release-trains.mjs check holds it, each family's projects on one release train,
# no file of tests (a [Fact], a [Theory] or a contract class) compiled into a second project but RuntimeClosingTests.cs
# and DescriptorTests.Registered.cs, which every project runs over its own family, and codecov.yml's flags as
# --codecov-flags prints them. So every other test belongs to one family, and runs in its project alone.
#
# A push to main runs everything. A pull request runs what the files its merge commit changes against the base branch
# (its first parent, hence a checkout depth of 2) select, each file by the first rule below that matches it:
#   none           CLAUDE.md files, docs/ but docs/docfx/, Markdown outside src/, website/ and skills/ but README.md,
#                  the repository's own settings and templates, and the workflows and scripts ci.yml never runs, each
#                  by name, so that a new one runs everything until it is given a rule; only lint.yml checks them.
#   documentation  README.md, the package READMEs (src/PackageReadme.md, src/AdCodicem.ValueObjects/README.md),
#                  website/docs/ and skills/value-objects/: the generator suite's tests that read them,
#                  [Trait("Reads", "Documentation")]. The package READMEs are also packed, website/docs/ also built
#                  into the site.
#   site           website/, docs/docfx/ and the action that builds the site: the job "documentation site".
#   everything     what every project or job reads: Directory.Build.*, Directory.Packages.props, global.json,
#                  nuget.config, .editorconfig, .gitattributes, the solution files, the local tools, ci.yml, this
#                  script, the family table, the list of release trains (which every project reads by a property
#                  function, invisible to dotnet-affected) and release-trains.mjs, which this script runs,
#                  coverage-modules.sh, codecov.yml, tests/Directory.Build.*, and any path no rule names.
#   release        package.json, its lock file, .releaserc.json, next-version.mjs and release-trains.test.mjs: the job
#                  "release tooling".
#   pack           icon.png, .github/shipped-dependencies and the scripts that check the packed set.
#   graph          src/, the unit suite's projects and tests/UnitTests.props, which they import, tests/Fixtures/ but
#                  the Functions fixture, and the .NET code of the rules below: the projects dotnet-affected
#                  (.config/dotnet-tools.json) marks changed or affected, through their references, the files they
#                  link (src/Shared/, the unit suite's shared sources) and the files they import. A project under src/
#                  selects its family, and a project of core every family, since every test runs the generator's output
#                  and the registry; a family's project of the unit suite selects that family; the generator suite,
#                  the RDG suite and the compiled model run when it marks them, and the job "build" builds every
#                  project it marks but the unit suite's, which each family's job builds. Every project item counts, so
#                  the package READMEs, icon.png and every CLAUDE.md are first put back as the base branch has them:
#                  the packages pack the first two and every project globs the third. Any exit of the tool but 0, and
#                  166 for "nothing affected", runs everything.
#   generator      the generator suite, in full.
#   rdg            the Request Delegate Generator suite.
#   heavy          what only heavy.yml runs (tests/NativeAot/, tests/Compat/, the integration suite and its Functions
#                  fixture, samples/, native-aot.sh, compiled-model.sh, heavy.yml): a notice asks for the ci:heavy
#                  label; the code among them is built (tests/Compat/, which no solution holds, is only formatted), and
#                  the compiled model's own inputs, tests/NativeAot/'s Directory.Build.props and compiled-model.sh
#                  also generate and compile the model.
#   build          benchmarks/: built.
# Then core runs the generator suite in full, aspnet the RDG suite, efcore the compiled model; any affected project
# under src/ packs and builds the site; any .NET lane formats.
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
#   families        the JSON array of the families whose project of the unit suite the job unit builds and runs, one
#                   job each, and whose projects coverage-modules.sh checks in its report
#   build           the JSON array of the projects the job build builds, through a solution filter: what dotnet-affected
#                   marks but the unit suite's projects, and the suites the run selects; every project of the solution
#                   but the unit suite's when everything runs
#   generator       full, documentation or none
#   rdg, compiled-model, pack, site, release
#                   true when the lane runs
#   coverage        the JSON array of the Codecov flags the run uploads, one upload each
# A summary goes to $GITHUB_STEP_SUMMARY when Actions sets it.
set -euo pipefail

# -P: the physical path, which dotnet-affected reports the projects under, even through a symbolic link.
cd -P "$(dirname "$0")/../.."

table=.github/ci-families.json
solution=AdCodicem.ValueObjects.slnx

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
    echo "      carryforward: true"
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
    (if (.families | type) != "object" or (.families | length) == 0 then "families is empty" else empty end),
      (.families | to_entries[] | .key as $family | .value
        | (if (.tests | type) == "string" and (.tests | test("^AdCodicem\\.ValueObjects\\.UnitTests(\\.[A-Za-z]+)?$"))
           then empty else "\($family): tests names no project of the unit suite" end),
          (if (.projects | type) == "array" and (.projects | length) > 0 and all(.projects[]; type == "string")
           then empty else "\($family): projects is not a list of names" end)),
      ([.families[].projects[]] | group_by(.)[] | select(length > 1) | "\(.[0]) is in \(length) families"),
      ([.families[].tests] | group_by(.)[] | select(length > 1) | "\(.[0]) is the project of \(length) families")
  ' "$table")" || fail "$table is not the family table ci-plan.sh reads."
  [[ -z "$errors" ]] || fail "$table: $(paste -sd ';' <<<"$errors" | sed 's/;/; /g')."

  local listed actual extra missing
  listed="$(jq -r '.families[].projects[]' "$table" | LC_ALL=C sort -u)"
  actual="$(for project in src/*/*.csproj; do basename "$project" .csproj; done | LC_ALL=C sort -u)"
  missing="$(LC_ALL=C comm -13 <(echo "$listed") <(echo "$actual") | paste -sd ' ' -)"
  extra="$(LC_ALL=C comm -23 <(echo "$listed") <(echo "$actual") | paste -sd ' ' -)"
  [[ -z "$missing" ]] || fail "$table puts no family over $missing: add each project under src/ to one family."
  [[ -z "$extra" ]] || fail "$table names $extra, which is no project under src/."

  # Every project of the unit suite is one family's.
  listed="$(jq -r '.families[].tests' "$table" | LC_ALL=C sort -u)"
  actual="$(for project in tests/AdCodicem.ValueObjects.UnitTests*/*.csproj; do basename "$project" .csproj; done |
    LC_ALL=C sort -u)"
  missing="$(LC_ALL=C comm -13 <(echo "$listed") <(echo "$actual") | paste -sd ' ' -)"
  extra="$(LC_ALL=C comm -23 <(echo "$listed") <(echo "$actual") | paste -sd ' ' -)"
  [[ -z "$missing" ]] || fail "$table gives no family the unit suite's $missing: name each project in a family's tests."
  [[ -z "$extra" ]] || fail "$table names $extra, which is no project tests/<name>/<name>.csproj."

  # Every project but tests/Compat/'s, which no solution holds, is in the solution, where dotnet-affected looks for the
  # projects a change marks: one left out would be selected by no change to its own files.
  missing="$(LC_ALL=C comm -23 <(git -c core.quotePath=false ls-files '*.csproj' | grep -v '^tests/Compat/' | LC_ALL=C sort) \
    <(sed -n -E 's/.*<Project Path="([^"]*)".*/\1/p' "$solution" | LC_ALL=C sort) | paste -sd ' ' -)"
  [[ -z "$missing" ]] || fail "$solution does not hold $missing: add each project to the solution."

  # The one list of release trains (src/release-trains.json, ADR 0012) reads alike in MSBuild and in the module, and
  # agrees with the tree; each family's projects ride one train, so that what a family's lanes test one train releases.
  node .github/scripts/release-trains.mjs check || fail "src/release-trains.json does not hold: see the errors above."
  local trains spans
  trains="$(jq -r '.families[].projects[]' "$table" | xargs node .github/scripts/release-trains.mjs trains-of)" ||
    fail "release-trains.mjs could not name the train of the family table's projects."
  spans="$(LC_ALL=C join <(jq -r '.families | to_entries[] | .key as $family | .value.projects[] | "\(.) \($family)"' \
    "$table" | LC_ALL=C sort) <(LC_ALL=C sort <<<"$trains") | awk '{ print $2, $3 }' | LC_ALL=C sort -u |
    awk '{ count[$1]++; trains[$1] = trains[$1] " " $2 } END { for (family in count) if (count[family] > 1) print family ":" trains[family] }' |
    LC_ALL=C sort | paste -sd ';' - | sed 's/;/; /g')"
  [[ -z "$spans" ]] || fail "$table: a family's projects ride two release trains ($spans): move a project to the family of its train."

  # A source a project of the unit suite compiles from another's folder holds no test, or the test would run in both:
  # but the two that every project runs over its own family, RuntimeClosingTests.cs (the IL of the family's packages)
  # and DescriptorTests.Registered.cs (the value objects the project's registry holds). Every Compile item counts,
  # whatever its Link and its layout; $(UnitTestsSources) is the core project's folder, any other path is relative to
  # the item's file.
  local file include path source
  shopt -s globstar nullglob
  for file in tests/UnitTests.props tests/AdCodicem.ValueObjects.UnitTests*/*.csproj; do
    while IFS= read -r include; do
      path="${include//\\//}"
      # shellcheck disable=SC2016 # the MSBuild property, as the file writes it
      if [[ "$path" == '$(UnitTestsSources)'* ]]; then
        # shellcheck disable=SC2016 # the MSBuild property, as the file writes it
        path="tests/AdCodicem.ValueObjects.UnitTests/${path#'$(UnitTestsSources)'}"
      else
        path="$(dirname "$file")/$path"
      fi
      # shellcheck disable=SC2206 # the item's own glob, as MSBuild expands it
      local -a sources=($path)
      (( ${#sources[@]} > 0 )) || fail "$file links $include, which matches no file."
      for source in "${sources[@]}"; do
        [[ -f "$source" ]] || fail "$file links $include, which matches no file."
        [[ "$file" == *.csproj && "$(realpath -m "$source")" == "$(realpath -m "$(dirname "$file")")"/* ]] && continue
        case "$(basename "$source")" in RuntimeClosingTests.cs | DescriptorTests.Registered.cs) continue ;; esac
        if grep -qE '\[(Fact|Theory)\b|:[[:space:]]*ValueObjectContract<' "$source"; then
          fail "$file links $source, which holds tests: they would run in two projects. Link a file of helpers only."
        fi
      done
    done < <(tr '\r\n' '  ' < "$file" | grep -oE '<Compile[[:space:]][^>]*>' |
      sed -n -E 's/.*[[:space:]]Include="([^"]*)".*/\1/p' | tr ';' '\n')
  done
  shopt -u globstar nullglob

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
graph_paths=()
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
    README.md) echo documentation; return ;;
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
      .github/scripts/release-trains.mjs) echo everything ;;
    .github/scripts/native-aot.sh) echo heavy-notice ;;
    .github/scripts/compiled-model.sh) echo compiled-model ;;
    .github/scripts/ecmascript-patterns.mjs) echo text ;;
    .github/scripts/next-version.mjs | .github/scripts/release-trains.test.mjs) echo release ;;
    .github/shipped-dependencies | .github/scripts/verify-packages.sh | .github/scripts/package-ids.sh | \
      .github/scripts/shipped-dependencies-check.sh) echo pack ;;
    .github/scripts/dependabot-commit-type.sh | .github/scripts/docs-snapshot.sh | \
      .github/scripts/package-assemblies.sh | .github/scripts/preview-gate.sh | .github/scripts/push-packages.sh | \
      .github/scripts/release-pack.sh | .github/scripts/semver.jq) echo none ;;
    package.json | package-lock.json | .releaserc.json) echo release ;;
    website/*) echo site ;;
    src/PackageReadme.md | src/AdCodicem.ValueObjects/README.md) echo readme ;;
    Directory.Build.* | Directory.Packages.props | global.json | nuget.config | .editorconfig | .gitattributes | \
      AdCodicem.ValueObjects.slnx | src/AdCodicem.ValueObjects.Packages.slnf | src/release-trains.json | \
      .config/dotnet-tools.json | codecov.yml | tests/Directory.Build.*) echo everything ;;
    icon.png) echo pack ;;
    src/* | tests/UnitTests.props | tests/AdCodicem.ValueObjects.UnitTests/* | tests/AdCodicem.ValueObjects.UnitTests.*/*)
      echo graph ;;
    tests/AdCodicem.ValueObjects.GeneratorTests/*) echo generator ;;
    tests/AdCodicem.ValueObjects.RdgTests/*) echo rdg ;;
    tests/AdCodicem.ValueObjects.IntegrationTests/* | tests/Fixtures/AdCodicem.ValueObjects.Fixtures.Functions/*) echo heavy ;;
    tests/Fixtures/*) echo graph ;;
    tests/NativeAot/AdCodicem.ValueObjects.CompiledModel/* | \
      tests/NativeAot/AdCodicem.ValueObjects.NativeAot.Domain/* | tests/NativeAot/Directory.*) echo compiled-model ;;
    tests/Compat/*) echo heavy-format ;;
    tests/NativeAot/* | samples/*) echo heavy ;;
    benchmarks/*) echo build ;;
    *) echo unknown ;;
  esac
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
    # Codecov reads codecov.yml from the pull request's own head, the merge commit's second parent: one branched before
    # the family flags would take each targeted upload whole and carry nothing forward, so it runs everything.
    head_codecov="$(git show HEAD^2:codecov.yml 2> /dev/null || true)"
    if git rev-parse -q --verify HEAD^2 > /dev/null && ! grep -q '# BEGIN ci-families' <<<"$head_codecov"; then
      run_everything "the pull request's head has no family flags in codecov.yml: merge main into it to run its families"
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
  # The format job checks every .cs file of the repository, whichever rule the file falls under.
  [[ "$path" == *.cs ]] && format=true
  case "$(rule "$path")" in
    none) ;;
    readme) documentation=true; pack=true ;;
    website-docs) documentation=true; site=true ;;
    documentation) documentation=true ;;
    site) site=true ;;
    everything) run_everything "$path is read by every project or job" ;;
    release) release=true ;;
    pack) pack=true ;;
    graph) graph_paths+=("$path") ;;
    generator) graph_paths+=("$path"); generator_full=true ;;
    rdg) graph_paths+=("$path"); rdg=true ;;
    heavy) graph_paths+=("$path"); heavy_inputs+=("$path") ;;
    heavy-format) heavy_inputs+=("$path"); format=true ;;
    heavy-notice) heavy_inputs+=("$path") ;;
    compiled-model) graph_paths+=("$path"); heavy_inputs+=("$path"); compiled_model=true ;;
    text) family[text]=1 ;;
    build) graph_paths+=("$path") ;;
    *) run_everything "no rule of ci-plan.sh names $path" ;;
  esac
done

graph=false
if [[ "$everything" == false && ${#graph_paths[@]} -gt 0 ]]; then
  graph=true
fi
if [[ "$mode" == --graph-needed ]]; then
  echo "graph=$graph" >> "${GITHUB_OUTPUT:-/dev/stdout}"
  exit 0
fi

affected=()
marked=()
if [[ "$graph" == true ]]; then
  # Put back what every project globs or packs, so that only the project's own inputs mark it. This rewrites the
  # checkout, which a runner discards: a local run refuses a file it would lose changes of.
  for path in "${changed[@]}"; do
    case "$path" in
      src/PackageReadme.md | src/AdCodicem.ValueObjects/README.md | icon.png | CLAUDE.md | */CLAUDE.md)
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
  # 110 MB a local run would otherwise leave behind.
  trap 'rm -rf -- "$tools"' EXIT
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
    # Every project it marks, by its path in the repository, and the unit suite's, which select their families.
    mapfile -t marked < <(jq -r --arg root "$PWD/" '.[] | .FilePath | select(startswith($root)) | ltrimstr($root)' \
      "$tools/affected.json" | LC_ALL=C sort -u)
    # The suites and the compiled model, through what they reference: protobuf-net for the generator suite.
    while IFS= read -r project; do
      case "$project" in
        AdCodicem.ValueObjects.GeneratorTests) generator_full=true ;;
        AdCodicem.ValueObjects.RdgTests) rdg=true ;;
        AdCodicem.ValueObjects.CompiledModel) compiled_model=true ;;
      esac
      name="$(jq -r --arg project "$project" '.families | to_entries[] | select(.value.tests == $project) | .key' "$table")"
      [[ -n "$name" ]] && family[$name]=1
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

selected=()
for name in "${all_families[@]}"; do
  [[ -n "${family[$name]:-}" ]] && selected+=("$name")
done

[[ -n "${family[core]:-}" ]] && generator_full=true
[[ -n "${family[aspnet]:-}" ]] && rdg=true
[[ -n "${family[efcore]:-}" ]] && compiled_model=true

generator=none
if [[ "$generator_full" == true ]]; then
  generator=full
elif [[ "$documentation" == true ]]; then
  generator=documentation
fi

# What the job build builds: every project of the solution but the unit suite's when everything runs, otherwise what
# dotnet-affected marks but the unit suite's, and the suites the run selects; each family's job builds its own project.
mapfile -t unit_projects < <(jq -r '.families[].tests | "tests/\(.)/\(.).csproj"' "$table")
if [[ "$everything" == true ]]; then
  mapfile -t marked < <(sed -n -E 's/.*<Project Path="([^"]*)".*/\1/p' "$solution")
fi
[[ "$generator" != none ]] && marked+=(tests/AdCodicem.ValueObjects.GeneratorTests/AdCodicem.ValueObjects.GeneratorTests.csproj)
[[ "$rdg" == true ]] && marked+=(tests/AdCodicem.ValueObjects.RdgTests/AdCodicem.ValueObjects.RdgTests.csproj)
build="$(printf '%s\n' "${marked[@]}" | LC_ALL=C sort -u |
  jq -Rsc --args 'split("\n") | map(select(length > 0)) - $ARGS.positional' "${unit_projects[@]}")"

if [[ "$build" != '[]' || "$generator" != none || "$rdg" == true || "$compiled_model" == true || "$pack" == true ]]; then
  dotnet=true
fi

families="$(printf '%s\n' "${selected[@]}" | jq -R . | jq -sc 'map(select(length > 0))')"
coverage="$(jq -c --arg generator "$generator" --arg rdg "$rdg" \
  '. + (if $generator == "full" then ["generator"] else [] end) + (if $rdg == "true" then ["rdg"] else [] end)' \
  <<<"$families")"

expected='["plan"'
[[ "$dotnet" == true ]] && format=true
[[ "$format" == true ]] && expected+=',"format"'
(( ${#selected[@]} > 0 )) && expected+=',"unit"'
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
  echo "build=$build"
  echo "generator=$generator"
  echo "rdg=$rdg"
  echo "compiled-model=$compiled_model"
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
      echo "- the unit suite: $(printf '`%s` ' "${selected[@]}" | sed 's/ $//')"
    else
      echo "- the unit suite: no family"
    fi
    echo "- the job build: $(jq -r 'if length == 0 then "nothing to build" else "\(length) project(s)" end' <<<"$build")"
    echo "- the generator suite: $generator"
    lanes=()
    [[ "$rdg" == true ]] && lanes+=("the RDG suite")
    [[ "$compiled_model" == true ]] && lanes+=("the compiled model")
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
