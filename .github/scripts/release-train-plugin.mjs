// A semantic-release plugin that releases one train of src/release-trains.json
// (docs/adr/0012-version-the-packages-in-two-trains-independently-of-the-frameworks.md): the commits that release it,
// its version, its notes and its changelog, from the plan the pack job computed with next-version.mjs --trains.
//
// It keeps no state: each step reads the plan again, through the environment the release starts with.
//   RELEASE_TRAIN           the train, a name of src/release-trains.json
//   RELEASE_TRAIN_PLAN      a file holding the plan, next-version.mjs --trains's trains_json, of which the train's row
//                           gives its tag, its commits (the hashes of its range that release it), its range (every hash
//                           of <prefix><last>..HEAD as the plan walked it), its notes (the hashes attributed to it,
//                           [skip release] ones included), the core's unowned (the hashes no train owns, which its notes
//                           carry), and any other train's coreVersion and previousCoreVersion (the core it is packed
//                           against, and the one its last release was)
//   RELEASE_RUN_COMMITS     the release commits this run made for the trains before this one, which no plan walked
//   PACKED_VERSION          the version the pack job packed the train at; empty when it packed it at nothing new
// Every one must be set, an empty list or version included, so that a step never reads a variable the run forgot. The
// lists travel in a file: a variable reaches every process semantic-release starts, and Linux refuses to start one
// with a variable above 128 KiB, about 3,200 hashes. The caller's options must take precedence over the configuration
// file's (tagFormat, dryRun), as semantic-release's own merge gives them.
//
// Its options: { analyzer, notes }, the commit analyzer's and the release notes generator's, as .releaserc.json gives
// them, the analyzer's without a breaking rule, which src/release-trains.json gives each train. The commit analyzer,
// the notes generator and the changelog plugin are the ones semantic-release itself loads, resolved from its own
// directory as next-version.mjs resolves them, so that package-lock.json pins them.
import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { changelogOf, lineOf, loadTrains, releaseType, tagPrefixOf } from "./release-trains.mjs";

/** The name a configuration lists this plugin under, which release-trains.mjs's analyzerRules reads. */
export const PLUGIN = "./.github/scripts/release-train-plugin.mjs";
/** The line every changelog keeps above its releases: its title ends there, and a release is written below it. */
export const MARKER = "<!-- Releases are prepended below this line. -->";
const VARIABLES = ["RELEASE_TRAIN", "RELEASE_TRAIN_PLAN", "RELEASE_RUN_COMMITS", "PACKED_VERSION"];
const STABLE = /^\d+\.\d+\.\d+$/;

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
const fromRoot = createRequire(join(root, "package.json"));
const fromSemanticRelease = createRequire(
  join(dirname(fromRoot.resolve("semantic-release/package.json")), "lib", "plugins", "utils.js"));
const load = async (id) => import(pathToFileURL(fromSemanticRelease.resolve(id)).href);

/** A refusal semantic-release reports as its own, code and all. */
const refuse = (message) => Object.assign(new Error(message), { semanticRelease: true, code: "ERELEASETRAIN" });
const hashes = (text) => new Set((text ?? "").split(/\s+/).filter(Boolean));
const subject = (message) => (message ?? "").split("\n")[0];
const skipsRelease = (message) => /\[skip\s+release]|\[release\s+skip]/i.test(message);

/** The plan, as the environment and the file it names carry it, checked. */
function plan({ env, cwd, options }) {
  for (const name of VARIABLES) if (env[name] === undefined) throw refuse(`${name} is not set.`);
  const train = env.RELEASE_TRAIN;
  const model = loadTrains(join(cwd, "src", "release-trains.json"));
  if (!model.names.includes(train)) throw refuse(`${train} is no train of src/release-trains.json.`);
  const tagFormat = `${tagPrefixOf(train)}\${version}`;
  if (options.tagFormat !== tagFormat) {
    throw refuse(`The ${train} train's tags are ${tagFormat}, not ${options.tagFormat}: pass its tagFormat.`);
  }
  let rows;
  try {
    rows = JSON.parse(readFileSync(resolve(cwd, env.RELEASE_TRAIN_PLAN), "utf8"));
  } catch (error) {
    throw refuse(`The plan, ${env.RELEASE_TRAIN_PLAN}, cannot be read: ${error.message}`);
  }
  const row = Array.isArray(rows) ? rows.find((candidate) => candidate?.train === train) : undefined;
  if (!row) throw refuse(`The plan, ${env.RELEASE_TRAIN_PLAN}, has no row for the ${train} train.`);
  const lists = ["commits", "range", "notes", ...(train === "core" ? ["unowned"] : [])];
  for (const name of lists) {
    if (typeof row[name] !== "string") throw refuse(`The ${train} train's plan has no ${name} list.`);
  }
  if (row.tag !== `${tagPrefixOf(train)}${row.last}` || !STABLE.test(row.last ?? "")) {
    throw refuse(`The ${train} train's plan names no last release of the train: '${row.tag}'.`);
  }
  if (train !== "core") {
    for (const name of ["coreVersion", "previousCoreVersion"]) {
      if (!STABLE.test(row[name] ?? "")) throw refuse(`The ${train} train's plan's ${name} is not a stable version: '${row[name] ?? ""}'.`);
    }
  }
  return {
    train,
    tag: row.tag,
    breaking: model.trains.get(train).breaking,
    commits: hashes(row.commits),
    range: hashes(row.range),
    notes: hashes(row.notes),
    extra: train === "core" ? hashes(row.unowned) : new Set(),
    run: hashes(env.RELEASE_RUN_COMMITS),
    coreVersion: row.coreVersion,
    previousCoreVersion: row.previousCoreVersion,
  };
}

/** The changelog of the train and its title: the text from its start through the marker line, in the shape required. */
function changelog(train, cwd) {
  const file = changelogOf(train);
  let text;
  try {
    text = readFileSync(join(cwd, file), "utf8");
  } catch {
    throw refuse(`${file}, the ${train} train's changelog, cannot be read.`);
  }
  const end = text.indexOf(MARKER);
  if (!text.startsWith("# ") || end < 0) {
    throw refuse(`${file} must open with its "# " title and hold the line ${MARKER}, which releases are written below.`);
  }
  if (/^## /m.test(text.slice(0, end))) {
    throw refuse(`${file} has a release above ${MARKER}: move the title and the line back above every release.`);
  }
  return { changelogFile: file, changelogTitle: text.slice(0, end + MARKER.length) };
}

/** The options a loaded plugin receives: this plugin's, without its own two, then the ones given. */
const optionsFor = (pluginConfig, given) => {
  const { analyzer, notes, ...shared } = pluginConfig;
  return { ...shared, ...given };
};

export async function verifyConditions(pluginConfig, context) {
  const { train } = plan(context);
  const { verifyConditions: verify } = await load("@semantic-release/changelog");
  await verify(changelog(train, context.cwd), { ...context, options: { ...context.options, prepare: undefined } });
}

/**
 * The train's release type, from its own commits, the line rule applied (release-trains.mjs releaseType, as
 * next-version.mjs computes it). Every commit semantic-release sees must be one the plan walked, but the release
 * commits of this run, and every one the plan walked must be one semantic-release sees, but those it skips
 * ([skip release]): a range read from another tag, or through a merge the plan did not follow, fails rather than drops.
 */
export async function analyzeCommits(pluginConfig, context) {
  const { train, tag, breaking, commits, range, run, coreVersion, previousCoreVersion } = plan(context);
  const from = context.lastRelease?.gitTag ?? "the first commit";
  if (from !== tag) {
    throw refuse(`semantic-release counts the ${train} train from ${from}, the plan from ${tag}: dispatch release.yml again.`);
  }
  const seen = context.commits.filter(({ hash }) => !run.has(hash));
  for (const { hash, message } of seen) {
    if (!range.has(hash)) {
      throw refuse(`semantic-release sees ${hash.slice(0, 7)} ${subject(message)} since ${from}, which the ${train} ` +
        "train's plan did not walk: dispatch release.yml again.");
    }
  }
  const seenHashes = new Set(seen.map(({ hash }) => hash));
  for (const hash of range) {
    if (seenHashes.has(hash)) continue;
    let message;
    try {
      message = execFileSync("git", ["show", "-s", "--format=%B", hash], { cwd: context.cwd, encoding: "utf8" });
    } catch {
      message = "";
    }
    if (!skipsRelease(message)) {
      throw refuse(`The ${train} train's plan walked ${hash.slice(0, 7)} ${subject(message)}, which semantic-release ` +
        `does not see since ${from}: dispatch release.yml again.`);
    }
  }
  const kept = seen.filter(({ hash }) => commits.has(hash));
  const { analyzeCommits: analyze } = await load("@semantic-release/commit-analyzer");
  const { type, lineMoved } = await releaseType({
    train,
    breaking,
    analyze: (rules, picked) => analyze(optionsFor(pluginConfig, rules), { ...context, commits: picked }),
    config: { plugins: [[PLUGIN, pluginConfig]] },
    commits: kept,
    coreVersion,
    previousCoreVersion: train === "core" ? undefined : previousCoreVersion,
  });
  const line = train === "core" ? "" : `, core ${previousCoreVersion} -> ${coreVersion}${lineMoved ? ", line moved" : ""}`;
  context.logger.log(`${train}: ${kept.length} of ${seen.length} commits, ${type ?? "nothing"}${line}`);
  return type ?? null;
}

/** The version must be the one packed, a dry run included: semantic-release would otherwise release another. */
export async function verifyRelease(pluginConfig, context) {
  const { train } = plan(context);
  const packed = context.env.PACKED_VERSION;
  if (context.nextRelease.version !== packed) {
    throw refuse(`semantic-release computed ${context.nextRelease.version} for the ${train} train, but the pack job ` +
      `packed '${packed}'.`);
  }
}

/** The conventionalcommits preset's breakingHeaderPattern: a header it reads as breaking. */
const BREAKING_HEADER = /^(\w*)(?:\((.*)\))?!: (.*)$/;
/** A header with every ! before a colon the preset would read as breaking taken out. */
const plainHeader = (header) => {
  let plain = header;
  while (BREAKING_HEADER.test(plain)) {
    const next = plain.replace(/^(\w*(?:\(.*\))?)!: /, "$1: ");
    if (next === plain) break;
    plain = next;
  }
  return plain;
};
/**
 * A message as the notes read it, its breaking marks (a ! before the colon, a BREAKING CHANGE note) made plain, the
 * patterns conventional-commits-parser reads: a commit that ships in no package breaks nothing a release carries. A
 * revert's quoted header is made plain the same way, so that the notes still pair it with the commit it reverts.
 */
const unbroken = (message) => message.split("\n").map((line, index) => {
  if (index > 0) return line.replace(/^([\s|*]*)(?:BREAKING CHANGE|BREAKING-CHANGE)(?=[:\s])/i, "$1Note");
  const revert = /^((?:Revert|revert:)\s"?)(.*?)("?)$/i.exec(line);
  return revert ? `${revert[1]}${plainHeader(revert[2])}${revert[3]}` : plainHeader(line);
}).join("\n");

/**
 * The notes of the train's own commits, [skip release] ones included, the core's with the commits no train owns
 * (documentation, the site, the decisions), their breaking marks made plain, and, for any other train, the core it is
 * packed against.
 */
export async function generateNotes(pluginConfig, context) {
  const { train, notes: own, extra, coreVersion, previousCoreVersion } = plan(context);
  const kept = context.commits.filter(({ hash }) => own.has(hash) || extra.has(hash))
    .map((commit) => (own.has(commit.hash) ? commit : { ...commit, message: unbroken(commit.message) }));
  const { generateNotes: generate } = await load("@semantic-release/release-notes-generator");
  const notes = await generate(optionsFor(pluginConfig, pluginConfig.notes ?? {}), { ...context, commits: kept });
  if (train === "core") return notes;
  const moved = lineOf(coreVersion) !== lineOf(previousCoreVersion)
    ? ` The core's line moved from ${lineOf(previousCoreVersion)} to ${lineOf(coreVersion)} since this train's last ` +
      `release, which was packed against ${previousCoreVersion}.`
    : "";
  return `${notes.trimEnd()}\n\nPacked against AdCodicem.ValueObjects ${coreVersion}.${moved}\n`;
}

/** The release, written below the marker of the train's changelog, its title kept on top. */
export async function prepare(pluginConfig, context) {
  const { train } = plan(context);
  const { prepare: write } = await load("@semantic-release/changelog");
  await write(changelog(train, context.cwd), context);
}
