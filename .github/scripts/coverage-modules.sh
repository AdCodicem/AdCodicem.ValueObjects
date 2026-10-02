#!/usr/bin/env bash
# Fails when a project under src/ is missing from every coverage report.
#
# Usage: coverage-modules.sh
#
# The coverage collector instruments only the assemblies a test process loads. An assembly no test loads is not
# reported at 0 %: it is not reported at all, so Codecov leaves its lines out of the project's coverage instead of
# counting them as missed. AdCodicem.ValueObjects.Dapper and AdCodicem.ValueObjects.NewtonsoftJson shipped that
# way, invisible behind a figure that looked complete. Every project under src/ with code of its own must
# therefore appear in at least one Cobertura report; the meta-package AdCodicem.ValueObjects carries none and is
# skipped. Run it after the three suites, from anywhere in the repository.
set -euo pipefail

cd "$(dirname "$0")/../.."

mapfile -t reports < <(find . -name '*.cobertura.xml' -not -path './website/*' -not -path '*/node_modules/*')
if (( ${#reports[@]} == 0 )); then
  echo "::error::No Cobertura report was found; the test steps must run with --coverage first." >&2
  exit 1
fi

# One <package> element per assembly; its attributes come in no fixed order.
reported="$(grep -ho '<package [^>]*>' "${reports[@]}" | grep -o ' name="[^"]*"' | sed 's/^ name="//; s/"$//' |
  LC_ALL=C sort -u || true)"

missing=0
for project in src/*/*.csproj; do
  directory="$(dirname "$project")"
  name="$(basename "$project" .csproj)"
  if ! find "$directory" -name '*.cs' -print -quit | grep -q .; then
    continue
  fi
  if ! grep -qxF "$name" <<<"$reported"; then
    echo "::error::$name is in no coverage report: no test loads it, so Codecov does not count its lines." >&2
    missing=1
  fi
done

if (( missing == 0 )); then
  echo "Every project under src/ with code is in a coverage report (${#reports[@]} reports read)."
fi
exit "$missing"
