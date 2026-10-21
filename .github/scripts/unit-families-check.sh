#!/usr/bin/env bash
# Fails when a test of the unit suite falls outside every family's filter of .github/ci-families.json.
#
# Usage: unit-families-check.sh
#
# A pull request runs the unit tests of the families it selects, through their --filter-query values
# (.github/scripts/ci-plan.sh): a test none of them matches would run on no pull request, only after the merge. The
# plan checks the family table against the folders and namespaces without a build; this lists the built suite with
# every family's queries and the guards', which xUnit ORs, and compares it with the suite listed whole. Run it after the
# build, from anywhere in the repository.
set -euo pipefail

cd "$(dirname "$0")/../.."

list() {
  dotnet test --project tests/AdCodicem.ValueObjects.UnitTests --no-build -c Release --list-tests "$@" |
    sed -n 's/^  //p' | LC_ALL=C sort
}

filter=()
while IFS= read -r query; do
  filter+=(--filter-query "$query")
done < <(jq -r '.families[].unit[], .guards[]' .github/ci-families.json)

whole="$(list)"
filtered="$(list "${filter[@]}")"
if [[ -z "$whole" ]]; then
  echo "::error::The unit suite lists no test." >&2
  exit 1
fi

missing="$(LC_ALL=C comm -23 <(echo "$whole") <(echo "$filtered"))"
if [[ -n "$missing" ]]; then
  echo "::error::$(wc -l <<<"$missing") test(s) of the unit suite match no family's filter of .github/ci-families.json, $(head -1 <<<"$missing") first." >&2
  head -20 <<<"$missing" >&2
  exit 1
fi
echo "Every one of the $(wc -l <<<"$whole") tests of the unit suite matches a family's filter."
