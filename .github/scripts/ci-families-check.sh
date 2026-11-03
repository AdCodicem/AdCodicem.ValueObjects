#!/usr/bin/env bash
# Checks .github/ci-families.json against the repository, and writes what ci.yml's matrices read from it
# (docs/adr/0016-run-every-job-on-every-pull-request.md). Every run of ci.yml runs every job: the families only split
# the unit suite into a test project each, which a job of the `unit` matrix builds and tests.
#
# Usage: ci-families-check.sh
#        ci-families-check.sh --codecov-flags
#
# --codecov-flags prints the flags of the families, which codecov.yml holds between its two "ci-families" markers.
#
# The check, without a build: every project under src/ in exactly one family, every project of the unit suite a
# family's, every project but tests/Compat/'s in the solution, no file of tests (a [Fact], a [Theory] or a contract
# class) compiled into a second project but RuntimeClosingTests.cs and DescriptorTests.Registered.cs, which every
# project runs over its own family, and codecov.yml's flags as --codecov-flags prints them. So every other test belongs
# to one family, and runs in its project alone.
#
# Outputs, to $GITHUB_OUTPUT (stdout outside Actions):
#   families  the JSON array of the families, whose project of the unit suite the job unit builds and runs, one job
#             each, and whose projects coverage-modules.sh checks in its report
#   build     the JSON array of the projects the job build builds, through a solution filter: every project of the
#             solution but the unit suite's, which each family's job builds
#   coverage  the JSON array of the Codecov flags the run uploads, one upload each: the families, generator and rdg
set -euo pipefail

cd "$(dirname "$0")/../.."

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
  ' "$table")" || fail "$table is not the family table ci-families-check.sh reads."
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

  # Every project but tests/Compat/'s, which no solution holds, is in the solution, which the job build builds: one
  # left out would be built and tested by no job.
  missing="$(LC_ALL=C comm -23 <(git -c core.quotePath=false ls-files '*.csproj' | grep -v '^tests/Compat/' | LC_ALL=C sort) \
    <(sed -n -E 's/.*<Project Path="([^"]*)".*/\1/p' "$solution" | LC_ALL=C sort) | paste -sd ' ' -)"
  [[ -z "$missing" ]] || fail "$solution does not hold $missing: add each project to the solution."

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
    fail "codecov.yml's flags differ from $table: replace the lines between its ci-families markers with the output of .github/scripts/ci-families-check.sh --codecov-flags."
  fi
}

case "${1:-}" in
  --codecov-flags)
    codecov_flags
    exit 0
    ;;
  '') ;;
  *) fail "ci-families-check.sh takes --codecov-flags or no argument, not $1." ;;
esac

check_table

families="$(jq -c '.families | keys_unsorted' "$table")"
mapfile -t unit_projects < <(jq -r '.families[].tests | "tests/\(.)/\(.).csproj"' "$table")
build="$(sed -n -E 's/.*<Project Path="([^"]*)".*/\1/p' "$solution" | LC_ALL=C sort -u |
  jq -Rsc --args 'split("\n") | map(select(length > 0)) - $ARGS.positional' "${unit_projects[@]}")"
coverage="$(jq -c '. + ["generator", "rdg"]' <<<"$families")"

{
  echo "families=$families"
  echo "build=$build"
  echo "coverage=$coverage"
} >> "${GITHUB_OUTPUT:-/dev/stdout}"
