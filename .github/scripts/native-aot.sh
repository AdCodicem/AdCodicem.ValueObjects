#!/usr/bin/env bash
# Publishes a native AOT application of tests/NativeAot, AdCodicem.ValueObjects.NativeAot unless another is named, and
# fails on a trimming or AOT warning, or on any difference between what it does under the JIT and what it does as a
# native binary.
#
# Usage: native-aot.sh [output directory, artifacts/native-aot by default] [project, the main application by default]
#
# The main application references every package that claims to be AOT-compatible but two: the HotChocolate one, whose
# framework warns on its own publish and starts natively only rooted (CLAUDE.md), and the protobuf-net one, which
# AdCodicem.ValueObjects.NativeAot.ProtobufNet publishes apart. Each application runs a fixed script over every value
# object of its domain, one line per scenario, into the file it is given: once under the JIT, through dotnet run, and
# once as the binary the native AOT compiler wrote. The two files must be identical; behaviour.diff holds the
# difference when they are not. The analyzers see what each library calls, but they do not run it, and a warning
# suppressed with a justification stays unproven until a native binary runs past it. The application also fails on its
# own when a scenario throws where neither run should. Needs clang and zlib, as any native AOT publish on Linux does.
# Run it from anywhere in the repository.
#
# A project may hold aot-warnings.allowed: one extended regular expression per line, # for a comment. A warning of the
# publish that matches one is expected and does not fail the run; any other does. Only the protobuf-net application
# has one: protobuf-net's annotations reach the nested converters of every value object at each call into the model
# its generator writes, and protobuf-net.Core warns for itself.
set -euo pipefail

cd "$(dirname "$0")/../.."

out="${1:-artifacts/native-aot}"
mkdir -p "$out"
out="$(cd "$out" && pwd)" # dotnet run starts the application from the project's folder
project="${2:-tests/NativeAot/AdCodicem.ValueObjects.NativeAot}"
name="$(basename "$project")"
binary="$out/bin/$name"
allowed="$project/aot-warnings.allowed"

echo "::group::Run under the JIT"
dotnet run --project "$project" -c Release -- "$out/jit.txt"
echo "::endgroup::"

# PublishAot is set by the project when NativeAot is true, never as -p:PublishAot, which would reach the
# netstandard2.0 generator the projects reference too.
echo "::group::Publish with native AOT"
dotnet publish "$project" -c Release -p:NativeAot=true -o "$out/bin" 2>&1 | tee "$out/publish.log"
echo "::endgroup::"

# TreatWarningsAsErrors reaches the AOT compiler, which then fails the publish itself. This does not rely on it.
warnings="$(grep -E 'IL[23][0-9]{3}' "$out/publish.log" || true)"
if [[ -f "$allowed" ]]; then
  warnings="$(grep -vEf <(sed -e 's/#.*//' -e '/^[[:space:]]*$/d' "$allowed") <<<"$warnings" || true)"
fi
if [[ -n "$warnings" ]]; then
  echo "$warnings"
  echo "::error::The native AOT publish reported a trimming or AOT warning." >&2
  exit 1
fi

echo "::group::Run the native binary"
"$binary" "$out/native.txt"
echo "::endgroup::"

if ! diff -u "$out/jit.txt" "$out/native.txt" > "$out/behaviour.diff"; then
  cat "$out/behaviour.diff"
  echo "::error::The native binary does not behave as the application does under the JIT: see behaviour.diff." >&2
  exit 1
fi

size="$(stat -c %s "$binary")"
echo "Identical behaviour over $(wc -l < "$out/jit.txt") scenarios. Native binary: $size bytes."
if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  {
    echo "### Native AOT"
    echo
    echo "| Binary | Size | Scenarios |"
    echo "| --- | ---: | ---: |"
    echo "| $name | $size bytes ($(numfmt --to=iec-i --suffix=B --format=%.1f "$size")) | $(wc -l < "$out/jit.txt") |"
  } >> "$GITHUB_STEP_SUMMARY"
fi
