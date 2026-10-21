#!/usr/bin/env bash
# Fails when a project under src/ is missing from every coverage report, or has no line covered there.
#
# Usage: coverage-modules.sh [family ...]
#
# The coverage collector instruments only the assemblies a test process loads. An assembly no test loads is not
# reported at 0 %: it is not reported at all, so Codecov leaves its lines out of the project's coverage instead of
# counting them as missed. AdCodicem.ValueObjects.Dapper and AdCodicem.ValueObjects.NewtonsoftJson shipped that
# way, invisible behind a figure that looked complete. Every project under src/ with code of its own must
# therefore appear in at least one Cobertura report; the meta-package AdCodicem.ValueObjects carries none and is
# skipped. RuntimeClosingTests loads nearly every package to read its IL, which reports it without exercising it, so a
# project must also have a line covered in some report.
#
# With families of .github/ci-families.json, only their projects are checked: the reports of a pull request's run
# hold the tests of its families alone (.github/scripts/ci-plan.sh). With none, every project under src/. Run it after
# the suites, from anywhere in the repository.
set -euo pipefail

cd "$(dirname "$0")/../.."

table=.github/ci-families.json

mapfile -t reports < <(find . -name '*.cobertura.xml' -not -path './website/*' -not -path '*/node_modules/*')
if (( ${#reports[@]} == 0 )); then
  echo "::error::No Cobertura report was found; the test steps must run with --coverage first." >&2
  exit 1
fi

if (( $# == 0 )); then
  mapfile -t projects < <(for project in src/*/*.csproj; do basename "$project" .csproj; done)
else
  for family in "$@"; do
    if ! jq -e --arg family "$family" '.families | has($family)' "$table" > /dev/null; then
      echo "::error::$family is no family of $table." >&2
      exit 1
    fi
  done
  mapfile -t projects < <(jq -r --args '.families[$ARGS.positional[]].projects[]' "$@" < "$table")
fi

# One <package> element per assembly, its attributes in no fixed order, and an assembly in several reports: the best
# line rate it reaches in any of them.
reported="$(grep -ho '<package [^>]*>' "${reports[@]}" | awk '
  {
    name = ""; rate = 0
    if (match($0, / name="[^"]*"/)) name = substr($0, RSTART + 7, RLENGTH - 8)
    if (match($0, / line-rate="[^"]*"/)) rate = substr($0, RSTART + 12, RLENGTH - 13) + 0
    if (name != "" && (!(name in best) || rate > best[name])) best[name] = rate
  }
  END { for (name in best) print name, (best[name] > 0 ? "covered" : "uncovered") }' || true)"

missing=0
for name in "${projects[@]}"; do
  directory="src/$name"
  if ! find "$directory" -name '*.cs' -print -quit | grep -q .; then
    continue
  fi
  case "$(awk -v name="$name" '$1 == name { print $2 }' <<<"$reported")" in
    covered) ;;
    uncovered)
      echo "::error::$name has no line covered in any coverage report: no test of this run exercises it." >&2
      missing=1
      ;;
    *)
      echo "::error::$name is in no coverage report: no test loads it, so Codecov does not count its lines." >&2
      missing=1
      ;;
  esac
done

if (( missing == 0 )); then
  echo "Every project with code${*:+ of ${*}} has lines covered in a coverage report (${#reports[@]} reports read)."
fi
exit "$missing"
