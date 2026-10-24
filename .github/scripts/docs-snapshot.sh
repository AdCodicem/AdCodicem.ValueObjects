#!/usr/bin/env bash
# Freezes website/docs/ as the documentation of a stable release.
#
# Usage: docs-snapshot.sh <version>
#        docs-snapshot.sh "core=<version> satellites=<version>"
#
# release.yml's pack job runs this after release-pack.sh, before semantic-release
# starts, and hands what it writes to the release job, where the files are among
# @semantic-release/git's assets: they land in the release commit the tag points
# at. A dry run runs it too, and commits nothing.
#
# There is one entry per line of the core's versions rather than per release:
# 0.<minor>.x while the core's major is 0, where a minor is the breaking channel,
# and <major>.x from its 1.0 on. A release in a line that already has an entry
# replaces it, so each entry documents the latest release of its line. Releases
# are only ever cut from main, so the line being released is always the newest
# one, and every stable release, a satellites-only one included, re-freezes the
# core's current line.
#
# The second form names each train of src/release-trains.json once, at a stable
# version. The line's entry in website/released-versions.json then names each
# train's version, {"core": "0.3.2", "satellites": "0.4.1"}, where the first form
# writes the one version, "0.3.2". Only this script writes that file.
#
# DOCS_SNAPSHOT_ENTRY_ONLY=true stops once that entry is written, before DocFX
# and Docusaurus run: a seam for docs-snapshot.test.mjs, which no workflow sets.
#
# See docs/adr/0005-version-the-documentation-site.md, as amended by
# docs/adr/0012-version-the-packages-in-two-trains-independently-of-the-frameworks.md.
set -euo pipefail

scripts="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
stable='^([0-9]+)\.([0-9]+)\.([0-9]+)$'

argument="${1:?version required}"

cd "$(git rev-parse --show-toplevel)"

# The arguments of write_entry: the one version, or <train>=<version> for each
# train, the core first, then in the list's order.
entry=()
if [[ "$argument" == *=* ]]; then
  # release-trains.mjs refuses a train the list lacks, a train named twice and
  # a train left out.
  node "$scripts/release-trains.mjs" --list src/release-trains.json versions "$argument"
  trains="$(node "$scripts/release-trains.mjs" --list src/release-trains.json trains)"
  declare -A versions=()
  read -rd '' -a pairs <<<"$argument" || true
  for pair in "${pairs[@]}"; do
    versions["${pair%%=*}"]="${pair#*=}"
  done
  while read -r train _; do
    if [[ ! "${versions[$train]:-}" =~ $stable ]]; then
      echo "::error::'${versions[$train]:-}', the $train train's version, is not a stable version. Only a stable release freezes the documentation." >&2
      exit 1
    fi
    entry+=("$train=${versions[$train]}")
  done <<<"$trains"
  # The line is the core's.
  version="${versions[core]}"
  described="${entry[*]}"
else
  version="$argument"
  entry=("$version")
  described="$version"
fi

if [[ ! "$version" =~ $stable ]]; then
  echo "::error::'$version' is not a stable version. Only a stable release freezes the documentation." >&2
  exit 1
fi
major="${BASH_REMATCH[1]}"
minor="${BASH_REMATCH[2]}"
if [[ "$major" == "0" ]]; then
  line="0.${minor}.x"
else
  line="${major}.x"
fi

# versions.json only records the line; the selector also names the exact
# release, or each train's.
write_entry() {
  node - "$line" "${entry[@]}" <<'EOF'
const fs = require('node:fs');
const [line, ...entry] = process.argv.slice(2);
const file = 'website/released-versions.json';
const released = fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : {};
released[line] = entry.length === 1 && !entry[0].includes('=')
  ? entry[0]
  : Object.fromEntries(entry.map((pair) => [pair.slice(0, pair.indexOf('=')), pair.slice(pair.indexOf('=') + 1)]));
fs.writeFileSync(file, JSON.stringify(released, null, 2) + '\n');
EOF
}

write_entry
echo "website/released-versions.json names $described for $line."
if [[ "${DOCS_SNAPSHOT_ENTRY_ONLY:-}" == "true" ]]; then
  exit 0
fi

# The API reference is generated into website/docs/api/ and not committed, so
# `docs:version` only copies it if it exists. A snapshot without it would have
# the reference of that version fall back to nothing at all.
echo "Generating the API reference for $described."
dotnet tool restore
dotnet docfx docs/docfx/docfx.json
python3 .github/scripts/docfx-postprocess.py website/docs/api

cd website
# Without install scripts: Docusaurus builds without them, and this runs in the
# job whose output is released.
if [[ ! -d node_modules ]]; then
  npm ci --ignore-scripts --no-audit --no-fund
fi

# `docs:version` refuses a name that already exists, so the line's previous
# entry goes first: its folder, its sidebar, and its place in versions.json.
# docusaurus.config.ts derives its version options from versions.json, which is
# what keeps the site loadable between these two steps.
rm -rf "versioned_docs/version-${line}" "versioned_sidebars/version-${line}-sidebars.json"
node - "$line" <<'EOF'
const fs = require('node:fs');
const [line] = process.argv.slice(2);
const file = 'versions.json';
if (fs.existsSync(file)) {
  const kept = JSON.parse(fs.readFileSync(file, 'utf8')).filter((name) => name !== line);
  fs.writeFileSync(file, JSON.stringify(kept, null, 2) + '\n');
}
EOF

echo "Freezing website/docs/ as $line ($described)."
npx docusaurus docs:version "$line"

if ! compgen -G "versioned_docs/version-${line}/api/*.md" >/dev/null; then
  echo "::error::versioned_docs/version-${line}/api/ holds no page: the snapshot carries no API reference." >&2
  exit 1
fi
echo "website/versioned_docs/version-${line} now documents $described."
