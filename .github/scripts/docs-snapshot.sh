#!/usr/bin/env bash
# Freezes website/docs/ as the documentation of a stable release.
#
# Usage: docs-snapshot.sh <version>
#
# semantic-release runs this in its prepare step, after release-pack.sh and
# before @semantic-release/git: the files written here are among that plugin's
# assets, so they land in the release commit the tag points at. A dry run skips
# prepare and writes nothing.
#
# There is one entry per line of versions rather than per release: 0.<minor>.x
# while the major is 0, where a minor is the breaking channel, and <major>.x
# from 1.0 on. A release in a line that already has an entry replaces it, so each
# entry documents the latest release of its line. Releases are only ever cut from
# main, so the line being released is always the newest one.
#
# See docs/adr/0005-version-the-documentation-site.md.
set -euo pipefail

version="${1:?version required}"

if [[ ! "$version" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)$ ]]; then
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

cd "$(git rev-parse --show-toplevel)"

# The API reference is generated into website/docs/api/ and not committed, so
# `docs:version` only copies it if it exists. A snapshot without it would have
# the reference of that version fall back to nothing at all.
echo "Generating the API reference for $version."
dotnet tool restore
dotnet docfx docs/docfx/docfx.json
python3 .github/scripts/docfx-postprocess.py website/docs/api

cd website
if [[ ! -d node_modules ]]; then
  npm ci --no-audit --no-fund
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

echo "Freezing website/docs/ as $line ($version)."
npx docusaurus docs:version "$line"

# versions.json only records the line; the selector also names the exact release.
node - "$line" "$version" <<'EOF'
const fs = require('node:fs');
const [line, version] = process.argv.slice(2);
const file = 'released-versions.json';
const released = fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : {};
released[line] = version;
fs.writeFileSync(file, JSON.stringify(released, null, 2) + '\n');
EOF

if ! compgen -G "versioned_docs/version-${line}/api/*.md" >/dev/null; then
  echo "::error::versioned_docs/version-${line}/api/ holds no page: the snapshot carries no API reference." >&2
  exit 1
fi
echo "website/versioned_docs/version-${line} now documents $version."
