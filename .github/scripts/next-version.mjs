#!/usr/bin/env node
// Prints the version semantic-release would give the next stable release of a commit,
// and the preview version derived from it, without a token and without the remote.
//
// Usage: node .github/scripts/next-version.mjs [--trains] [rev]      (rev defaults to HEAD)
//
// `semantic-release --dry-run` cannot answer this in preview.yml: before it analyses a
// single commit it checks that it may push (`git push --dry-run`, its documented
// behaviour), and @semantic-release/github wants a token. A job that only reads the
// repository passes neither. So this script runs the one step that decides the version,
// the commit analyzer, with the configuration release.yml gives it:
//
//   - the `@semantic-release/commit-analyzer` entry of .releaserc.json, options included,
//     merged over the top-level options as semantic-release merges them;
//   - the commit-analyzer, its preset and semver that semantic-release itself loads,
//     resolved from semantic-release's own directory, so package-lock.json pins both;
//   - the last release chosen as semantic-release chooses it on a release branch: the
//     highest tag that matches `tagFormat`, is a valid version, is not a prerelease and is
//     merged into `rev`;
//   - the commits since that tag, their raw message trimmed, minus `[skip release]`.
//
// The configuration is read at `rev`, so the answer for a commit never changes: a later
// run can recompute the version of a preview that an earlier run left half published.
//
// It refuses a shallow clone, and a history with no stable tag: either would make it
// answer 1.0.0, a preview that would outrank every 0.x version on nuget.org for good.
// This repository always has one (v0.1.0, the baseline placed by hand).
//
// Output, as key=value lines for $GITHUB_OUTPUT:
//   last_tag, last_version  the last stable release
//   release_type            major, minor or patch; empty when no commit would release
//   next_version            the next stable version; the next patch when release_type is empty
//   height                  commits from last_tag to rev
//   preview_version         <next_version>-preview.<height>
//
// --trains adds each release train's own version (0012), which nothing uses yet: the same
// keys prefixed with the train's name (core_last_tag, satellites_next_version, ...), each
// from the train's own last stable tag and the commits of its range that release it
// (release-trains.mjs reads which, from the list and the restore graph of the tree being
// evaluated, so it runs at HEAD only, after `dotnet restore` of the solution filter), plus
// <train>_commits and <train>_range (counts) and, for any train but the core,
// <train>_core_version, <train>_previous_core_version and <train>_line_moved;
// trains, preview_versions, release_types, next_versions and last_versions
// ("core=... satellites=..."); trains_json, all of it with the commits' hashes, on one
// line. One line per train goes to stderr, and a table to $GITHUB_STEP_SUMMARY when set.
import { createRequire } from "node:module";
import { execFileSync } from "node:child_process";
import { appendFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const args = process.argv.slice(2);
const trains = args[0] === "--trains";
const rev = (trains ? args[1] : args[0]) ?? "HEAD";
const root = execFileSync("git", ["rev-parse", "--show-toplevel"], {
  cwd: dirname(fileURLToPath(import.meta.url)),
  encoding: "utf8",
}).trim();
const git = (...args) => execFileSync("git", args, { cwd: root, encoding: "utf8", maxBuffer: 1 << 28 });
const fail = (message) => {
  console.error(`::error::${message}`);
  process.exit(1);
};
if (git("rev-parse", "--is-shallow-repository").trim() !== "false") {
  fail("Shallow clone: the tags and the commits since the last release are missing. Check out with fetch-depth: 0.");
}

// semantic-release resolves its plugins from its own directory first (lib/plugins/utils.js,
// loadPlugin), and the analyzer resolves its preset from its own. Resolving from the same
// place loads the very files release.yml runs, whatever npm hoisted where.
const fromRoot = createRequire(join(root, "package.json"));
const semanticReleaseDir = dirname(fromRoot.resolve("semantic-release/package.json"));
const fromSemanticRelease = createRequire(join(semanticReleaseDir, "lib", "plugins", "utils.js"));
const load = async (id) => import(pathToFileURL(fromSemanticRelease.resolve(id)).href);
const { default: semver } = await load("semver");
const { analyzeCommits } = await load("@semantic-release/commit-analyzer");

// Only the shape release.yml uses is supported; anything else fails rather than guesses.
const config = JSON.parse(git("show", `${rev}:.releaserc.json`));
for (const unsupported of ["extends", "analyzeCommits"]) {
  if (unsupported in config) throw new Error(`.releaserc.json: "${unsupported}" is not supported by ${import.meta.url}`);
}
const tagFormat = config.tagFormat ?? "v${version}";
const entry = (config.plugins ?? []).find(
  (plugin) => (Array.isArray(plugin) ? plugin[0] : plugin) === "@semantic-release/commit-analyzer"
);
if (!entry) throw new Error(".releaserc.json lists no @semantic-release/commit-analyzer");
const STEPS = ["verifyConditions", "analyzeCommits", "verifyRelease", "generateNotes", "prepare", "publish", "addChannel", "success", "fail"];
const globalOptions = Object.fromEntries(
  Object.entries(config).filter(([key]) => key !== "plugins" && !STEPS.includes(key))
);
// lib/plugins/normalize.js: the plugin receives { ...options, ...pluginConfig }.
const pluginConfig = { ...globalOptions, ...(Array.isArray(entry) ? entry[1] ?? {} : {}) };

// lib/branches/get-tags.js + lib/get-last-release.js, for a release branch. The pattern is
// semantic-release's: tagFormat with ${version} turned into (.+), anchored at the start only.
const escape = (text) => text.replace(/[\\^$.*+?()[\]{}|]/g, "\\$&");
const tagPattern = new RegExp(`^${escape(tagFormat.replace("${version}", " ")).replace(" ", "(.+)")}`);
const last = git("tag", "--merged", rev)
  .split("\n")
  .map((tag) => tag.trim())
  .map((tag) => ({ tag, version: tag.match(tagPattern)?.[1] }))
  .filter(({ version }) => version && semver.valid(semver.clean(version)) && !semver.prerelease(version))
  .sort((a, b) => semver.rcompare(a.version, b.version))[0];
if (!last) fail(`No stable tag matching ${tagFormat} is merged into ${rev}.`);

// lib/git.js getCommits: `git log <from>..<to>`, the raw body (%B) trimmed. -z separates the
// records with NUL, which a commit message cannot contain.
const range = `${last.tag}..${rev}`;
const commits = git("log", "-z", "--format=%H%n%B", range)
  .split("\0")
  .filter(Boolean)
  .map((record) => {
    const newline = record.indexOf("\n");
    return { hash: record.slice(0, newline), message: record.slice(newline + 1).trim() };
  })
  // lib/definitions/plugins.js, analyzeCommits.preprocess.
  .filter(({ message }) => !/\[skip\s+release]|\[release\s+skip]/i.test(message));
const height = Number(git("rev-list", "--count", range).trim());

const silent = { log() {}, error() {}, warn() {}, success() {} };
const releaseType = await analyzeCommits(pluginConfig, { commits, logger: silent, cwd: root, env: process.env });

// lib/get-next-version.js, for a release branch with a previous release.
const nextVersion = semver.inc(last.version, releaseType ?? "patch");

const output = {
  last_tag: last.tag,
  last_version: last.version,
  release_type: releaseType ?? "",
  next_version: nextVersion,
  height,
  preview_version: `${nextVersion}-preview.${height}`,
};
// A train's fault (no tag, no restore, a list or configuration it refuses) is an error line, not a stack.
if (trains) Object.assign(output, await trainOutput().catch((error) => fail(error.message)));
for (const [key, value] of Object.entries(output)) console.log(`${key}=${value}`);

// Each train's version, from its own tag and its own commits; the core's first, which the
// others read the line of (0012).
async function trainOutput() {
  if (rev !== "HEAD") fail(`--trains reads the tree checked out, so it computes HEAD only, not ${rev}.`);
  const trainsModule = await import(pathToFileURL(join(root, ".github", "scripts", "release-trains.mjs")).href);
  const { lastStableTag, previousCoreVersion, readModel, releaseType: typeOf, tagPrefixOf, trainsOfCommits } = trainsModule;
  const run = trainsModule.git(root);
  const m = readModel(root);
  const analyze = (rules, commits) =>
    analyzeCommits({ ...globalOptions, ...rules }, { commits, logger: silent, cwd: root, env: process.env });
  const skipped = (message) => /\[skip\s+release]|\[release\s+skip]/i.test(message);
  const rows = [];
  let core;
  for (const train of m.model.names) {
    const prefix = tagPrefixOf(train);
    const from = lastStableTag(run, prefix);
    if (!from) {
      fail(`No stable tag ${prefix}<M.m.p> is merged into HEAD: push satellites-v0.2.1 on 68abdc7, the satellites' ` +
        "baseline, or a new train's own (docs/maintaining.md).");
    }
    const span = `${from.tag}..HEAD`;
    const attributed = trainsOfCommits(m, run, span);
    const own = attributed.filter((commit) => commit.trains.has(train) && !skipped(commit.message));
    const coreVersion = core && (core.type ? core.next : core.last);
    const previous = train === "core" ? undefined : previousCoreVersion(run, from.tag);
    const { type, lineMoved } = await typeOf({
      train, breaking: m.model.trains.get(train).breaking, analyze, config, commits: own, coreVersion,
      previousCoreVersion: previous,
    });
    const next = semver.inc(from.version, type ?? "patch");
    const count = Number(run("rev-list", "--count", span).trim());
    const row = {
      train, tag: from.tag, last: from.version, type, next, height: count, preview: `${next}-preview.${count}`,
      commits: own.map(({ hash }) => hash), range: attributed.map(({ hash }) => hash),
    };
    if (train === "core") {
      row.unowned = attributed.filter((commit) => commit.trains.size === 0).map(({ hash }) => hash);
      core = row;
    } else {
      Object.assign(row, { coreVersion, previousCoreVersion: previous, lineMoved });
    }
    rows.push(row);
    const line = `${train}: ${row.tag} + ${row.commits.length} of ${row.range.length} commits -> ${type ?? "nothing"}, ` +
      `${next} (${row.preview})`;
    console.error(train === "core" ? line : `${line}; core ${previous} -> ${coreVersion}${lineMoved ? ", line moved" : ""}`);
  }

  const keyed = {};
  const each = (pick) => rows.map((row) => `${row.train}=${pick(row)}`).join(" ");
  keyed.trains = rows.map(({ train }) => train).join(" ");
  for (const row of rows) {
    Object.assign(keyed, {
      [`${row.train}_last_tag`]: row.tag,
      [`${row.train}_last_version`]: row.last,
      [`${row.train}_release_type`]: row.type ?? "",
      [`${row.train}_next_version`]: row.next,
      [`${row.train}_height`]: row.height,
      [`${row.train}_preview_version`]: row.preview,
      [`${row.train}_commits`]: row.commits.length,
      [`${row.train}_range`]: row.range.length,
    });
    if (row.train !== "core") {
      Object.assign(keyed, {
        [`${row.train}_core_version`]: row.coreVersion,
        [`${row.train}_previous_core_version`]: row.previousCoreVersion,
        [`${row.train}_line_moved`]: row.lineMoved,
      });
    }
  }
  keyed.preview_versions = each((row) => row.preview);
  keyed.release_types = each((row) => row.type ?? "");
  keyed.next_versions = each((row) => row.next);
  keyed.last_versions = each((row) => row.last);
  keyed.trains_json = JSON.stringify(rows.map((row) => ({
    ...row, type: row.type ?? "", commits: row.commits.join(" "), range: row.range.join(" "),
    ...(row.unowned ? { unowned: row.unowned.join(" ") } : {}),
  })));

  if (process.env.GITHUB_STEP_SUMMARY) {
    const cells = (values) => `| ${values.map((value) => value ?? "").join(" | ")} |`;
    const table = [
      "### Each train's version (not used yet)",
      "",
      cells(["Train", "Last tag", "Commits", "Range", "Type", "Next", "Preview", "Core version", "Previous core version", "Line moved"]),
      cells(Array(10).fill("---")),
      ...rows.map((row) => cells([row.train, row.tag, row.commits.length, row.range.length, row.type ?? "none", row.next, row.preview,
        row.coreVersion, row.previousCoreVersion, row.train === "core" ? "" : row.lineMoved ? "yes" : "no"])),
      "",
      `${core.unowned.length} commit(s) of the core's range release no train.`,
    ];
    appendFileSync(process.env.GITHUB_STEP_SUMMARY, `${table.join("\n")}\n`);
  }
  return keyed;
}
