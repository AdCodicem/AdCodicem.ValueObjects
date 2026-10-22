#!/usr/bin/env bash
# Compiles the model of tests/NativeAot/AdCodicem.ValueObjects.CompiledModel with `dotnet ef dbcontext optimize`, for
# its relaxed context and its strict one, and builds the project on it.
#
# Usage: compiled-model.sh compile|jit|aot
#
#   compile  The model `optimize` writes by default, and the project built on it, without running it: no database, no
#            Docker. What a pull request runs, since a model that no longer compiles is what this has caught.
#   jit      The same model and build, then a run: a round trip through SQL Server, started in a container (Docker),
#            which fails on a value read back different from the one written.
#   aot      The model `optimize --nativeaot` writes, with the queries of the project precompiled. The project is
#            published with native AOT and fails on a trimming or AOT warning about this library's code; Entity
#            Framework Core's own, and its dependencies', are expected. The binary is not run: two bugs of Entity
#            Framework Core stop its queries.
#
# The model is written under artifacts/compiled-model/, never committed. `optimize` connects to no database. Run it
# from anywhere in the repository.
set -euo pipefail

cd "$(dirname "$0")/../.."

variant="${1:-}"
project=tests/NativeAot/AdCodicem.ValueObjects.CompiledModel
namespace=AdCodicem.ValueObjects.CompiledModel.Compiled

# The model, which the project compiles in from artifacts/compiled-model/<model>/ when built with CompiledModel=<model>:
# compile and jit both write the jit model, compile only builds the project on it, and jit runs it.
case "$variant" in
  compile | jit) model=jit relaxed=() strict=() ;;
  aot) model=aot relaxed=(--nativeaot --precompile-queries) strict=(--nativeaot) ;;
  *)
    echo "Usage: compiled-model.sh compile|jit|aot" >&2
    exit 2
    ;;
esac
out="$PWD/artifacts/compiled-model/$model"

rm -rf "$out"
dotnet tool restore
# `dotnet ef` reads the project's metadata before it builds, which needs its assets file: restore the project here
# rather than rely on a restore run before: ci.yml's build job restores only the plan's solution filter, which may not
# hold this project, and heavy.yml's jobs only the other projects they build.
dotnet restore "$project"

# The queries are precompiled once, for the relaxed context: they are the same calls for both, and a second run would
# intercept each of them twice. Precompiling compiles the project again, in a workspace Entity Framework Core opens
# without --configuration, which would then look for the generator in its Debug output; MSBuild reads the environment
# as properties, so the variable sets the configuration that option does not.
echo "::group::Compile the models"
Configuration=Release dotnet ef dbcontext optimize --project "$project" --configuration Release \
  --context ShopContext --output-dir "$out/Relaxed" --namespace "$namespace.Relaxed" "${relaxed[@]}"
dotnet ef dbcontext optimize --project "$project" --configuration Release --no-build \
  --context StrictShopContext --output-dir "$out/Strict" --namespace "$namespace.Strict" "${strict[@]}"
echo "::endgroup::"

if [[ "$variant" == compile ]]; then
  dotnet build "$project" -c Release -p:CompiledModel=jit
  exit 0
fi
if [[ "$variant" == jit ]]; then
  dotnet run --project "$project" -c Release -p:CompiledModel=jit
  exit 0
fi

echo "::group::Publish with native AOT"
dotnet publish "$project" -c Release -p:CompiledModel=aot -o "$out-bin" 2>&1 | tee "$out-publish.log"
echo "::endgroup::"

# A warning located in this library's sources, or one collapsed for one of its assemblies.
if grep -E "/src/AdCodicem\.ValueObjects[^/]*/.*IL[23][0-9]{3}|IL[23][0-9]{3}: Assembly 'AdCodicem\.ValueObjects" "$out-publish.log"; then
  echo "::error::Publishing a compiled model with native AOT reported a trimming or AOT warning about this library." >&2
  exit 1
fi
