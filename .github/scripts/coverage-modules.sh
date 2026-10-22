#!/usr/bin/env bash
# Fails when a project under src/ is missing from a coverage report, or has no line covered there.
#
# Usage: coverage-modules.sh <report> <family>
#        coverage-modules.sh <report> --generator
#
# The coverage collector instruments only the assemblies a test process loads. An assembly no test loads is not
# reported at 0 %: it is not reported at all, so Codecov leaves its lines out of the project's coverage instead of
# counting them as missed. AdCodicem.ValueObjects.Dapper and AdCodicem.ValueObjects.NewtonsoftJson shipped that
# way, invisible behind a figure that looked complete. Every project under src/ with code of its own must therefore
# appear in the report of the tests that cover it; the meta-package AdCodicem.ValueObjects carries none and is
# skipped. RuntimeClosingTests loads every package of its family to read its IL, which reports it without exercising
# it, so a project must also have a line covered there.
#
# With a family of .github/ci-families.json, its projects are checked in the report of its project of the unit suite
# (.github/workflows/ci.yml's job unit), but the generator and the code fix, which only a compiler loads and the
# generator suite covers: --generator checks those two in the generator suite's report. Run it after the tests, from
# anywhere in the repository.
set -euo pipefail

cd "$(dirname "$0")/../.."

table=.github/ci-families.json
compiler=(AdCodicem.ValueObjects.Generators AdCodicem.ValueObjects.CodeFixes)

(( $# == 2 )) || { echo "::error::coverage-modules.sh takes a report and a family, or a report and --generator." >&2; exit 1; }
report="$1"
[[ -f "$report" ]] || { echo "::error::$report is no coverage report; the tests must run with --coverage first." >&2; exit 1; }

if [[ "$2" == --generator ]]; then
  projects=("${compiler[@]}")
  tests="the generator suite"
else
  tests="the $2 family"
  if ! jq -e --arg family "$2" '.families | has($family)' "$table" > /dev/null; then
    echo "::error::$2 is no family of $table." >&2
    exit 1
  fi
  mapfile -t projects < <(jq -r --arg family "$2" --args '.families[$family].projects[] | select(IN($ARGS.positional[]) | not)' \
    "${compiler[@]}" < "$table")
fi

# One <package> element per assembly, its attributes in no fixed order, and an assembly linked into several of the
# report's: the best line rate it reaches.
reported="$(grep -o '<package [^>]*>' "$report" | awk '
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
      echo "::error::$name has no line covered in $report: no test of $tests exercises it." >&2
      missing=1
      ;;
    *)
      echo "::error::$name is not in $report: no test of $tests loads it, so Codecov does not count its lines." >&2
      missing=1
      ;;
  esac
done

if (( missing == 0 )); then
  echo "Every project with code that $tests covers has lines covered in $report."
fi
exit "$missing"
