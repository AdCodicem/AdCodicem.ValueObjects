#!/usr/bin/env bash
# Decides which jobs a run of ci.yml calls, and which of them its gate, the required check "build and test", holds to
# success (docs/adr/0013-run-a-targeted-pull-request-ci-and-the-heavy-jobs-before-a-release.md).
#
# Usage: EVENT_NAME=<event> LABELS=<JSON array> AUTHOR=<login> HEAD_REF=<branch> HEAD_REPO=<owner/name> \
#          REPOSITORY=<owner/name> ci-plan.sh
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
# On a pull request, heavy.yml (native AOT, native AOT (Marten), the integration suite and compat (.NET 11)) runs when
# the pull request is labelled ci:heavy, or when Dependabot opened it to update a NuGet package, since auto-merge then
# lands it with nobody reading it. Otherwise heavy.yml runs after the merge, on its own push to main. When the pull
# request changes one of the heavy jobs' own inputs, a notice asks for the label; that reads the files the merge commit
# changes against its first parent, the base branch, so the checkout needs a depth of 2.
#
# Outputs, to $GITHUB_OUTPUT (stdout outside Actions):
#   heavy     true when the run calls heavy.yml, false otherwise
#   expected  the JSON array of the jobs of ci.yml the gate requires to succeed; it passes any other job it needs
# A summary goes to $GITHUB_STEP_SUMMARY when Actions sets it.
set -euo pipefail

event="${EVENT_NAME:?EVENT_NAME is required}"
labels="${LABELS:-[]}"
[[ "$labels" == null ]] && labels='[]'
author="${AUTHOR:-}"
head_ref="${HEAD_REF:-}"
head_repo="${HEAD_REPO:-}"
repository="${REPOSITORY:-}"

if ! jq -e 'type == "array" and all(.[]; type == "string")' <<<"$labels" > /dev/null 2>&1; then
  echo "::error::LABELS is not a JSON array of names: $labels" >&2
  exit 1
fi

heavy=false
reason="it runs on its own push to main once this is merged (heavy.yml)"
case "$event" in
  pull_request)
    if jq -e 'index("ci:heavy") != null' <<<"$labels" > /dev/null; then
      heavy=true
      reason="the pull request is labelled ci:heavy"
    elif [[ "$author" == 'dependabot[bot]' && "$head_ref" == dependabot/nuget/* && -n "$repository" &&
      "$head_repo" == "$repository" ]]; then
      heavy=true
      reason="Dependabot updates a NuGet package, and auto-merge lands it with nobody reading it"
    fi
    ;;
  push)
    reason="heavy.yml runs on this push itself"
    ;;
  *)
    echo "::error::ci-plan.sh plans pull_request and push runs, not $event." >&2
    exit 1
    ;;
esac

expected='["build"]'
if [[ "$heavy" == true ]]; then
  expected='["build","heavy"]'
fi

# The heavy jobs' own inputs, what only they build or run, Markdown aside. The build job compiles them, tests/Compat/
# aside, which is in no solution, and runs none of them.
heavy_paths='^(tests/NativeAot/|tests/Compat/|tests/AdCodicem\.ValueObjects\.IntegrationTests/'
heavy_paths+='|tests/Fixtures/AdCodicem\.ValueObjects\.Fixtures\.Functions/|samples/'
heavy_paths+='|\.github/scripts/(native-aot|compiled-model)\.sh$|\.github/workflows/heavy\.yml$)'
heavy_inputs=()
if [[ "$event" == pull_request && "$heavy" == false ]]; then
  if changed="$(git diff --name-only HEAD^1 HEAD 2> /dev/null)"; then
    mapfile -t heavy_inputs < <(grep -E "$heavy_paths" <<<"$changed" | grep -v '\.md$' || true)
  else
    echo "::warning::The files this pull request changes could not be read (the checkout needs a depth of 2)." >&2
  fi
  if (( ${#heavy_inputs[@]} > 0 )); then
    echo "::notice::This pull request changes ${#heavy_inputs[@]} file(s) only heavy.yml runs, ${heavy_inputs[0]} first: add the ci:heavy label, then push, to run it before the merge."
  fi
fi

{
  echo "heavy=$heavy"
  echo "expected=$expected"
} >> "${GITHUB_OUTPUT:-/dev/stdout}"

if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  {
    echo "### Plan"
    echo
    if [[ "$heavy" == true ]]; then
      echo "heavy.yml runs on this commit: $reason."
    else
      echo "heavy.yml does not run here: $reason."
    fi
    echo
    echo "\`build and test\` requires: $(jq -r 'map("`" + . + "`") | join(", ")' <<<"$expected")."
    if (( ${#heavy_inputs[@]} > 0 )); then
      echo
      echo "This pull request changes what only heavy.yml runs; add the \`ci:heavy\` label, then push, to run it here:"
      echo
      # shellcheck disable=SC2016 # the backticks are Markdown
      printf -- '- `%s`\n' "${heavy_inputs[@]}"
    fi
  } >> "$GITHUB_STEP_SUMMARY"
fi
