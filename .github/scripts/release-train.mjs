#!/usr/bin/env node
// Releases one train of src/release-trains.json through semantic-release's JavaScript API
// (docs/adr/0012-version-the-packages-in-two-trains-independently-of-the-frameworks.md), from the plan release.yml's
// pack job computed at the dispatched commit: release.yml's release job runs it once per train, the core first.
//
// Usage: node .github/scripts/release-train.mjs <train> --plan <file> --config <file> [--packages <dir>]
//          [--documentation <file>] [--dry-run]
//   --plan           next-version.mjs --trains's trains_json, each row with its train's package ids (ids), which
//                    release-train-plugin.mjs reads too
//   --config         the semantic-release configuration (release-train.releaserc.json); the driver's own options,
//                    tagFormat and dryRun, take precedence over its
//   --packages       the folder holding the packed files (artifacts/packages by default)
//   --documentation  the frozen documentation, the archive of website/'s files docs-snapshot.sh wrote
//   --dry-run        semantic-release's dry run: nothing is resumed, committed, tagged or pushed
//
// Environment: RELEASE_REV, the dispatched commit, which the plan was computed at (github.sha); GITHUB_TOKEN, the
// release App's token, for semantic-release, for the driver's own fetches, lookups and tag push, dry runs included (an
// extra header in git's environment, never its command line), and for completing a train found half-done;
// GITHUB_REPOSITORY, GITHUB_API_URL and GITHUB_SERVER_URL, as GitHub Actions sets them. GITHUB_OUTPUT, when set,
// receives release (whether the plan releases the train), version (the train's version, the one packed), tag (when it
// releases), commit (its release commit, on a run that is not a dry run) and resumed; GITHUB_STEP_SUMMARY, when set,
// what semantic-release computed, and the notes.
//
// In order:
//  1. It fetches main and the tags, and refuses a release commit of any train of the plan that the dispatched commit
//     holds untagged, since that train's last tag: a run stopped between its two pushes, and semantic-release, counting
//     from the last tag merged, would release that version a second time. Re-run failed jobs on that run completes it;
//     every train is checked, so that the first step refuses before any train is released.
//  2. A releasing train's files are staged in artifacts/release/, the folder the configuration's GitHub assets name,
//     since @semantic-release/github does not template their paths: the .nupkg and .snupkg of each of its ids.
//  3. Resume, for a releasing train on a run that is not a dry run, before the sync: a train whose tag is on origin, or
//     whose release commit (`chore(release): <tag> [skip ci]`) main holds untagged, is finished whatever main holds
//     since, so that Re-run failed jobs completes it. The release commit must be this dispatch's: it descends from
//     RELEASE_REV past the other trains' release commits alone, main holds it, and it touches only the configuration's
//     git assets; an untagged one is tagged, and the tag pushed. Its GitHub Release is then completed: done when
//     published, or when its draft carries every staged file at its digest; a file missing, or not uploaded, is
//     uploaded; a file at another digest fails; with no release at all, @semantic-release/github drafts it, with the
//     release commit's notes. semantic-release does not run.
//  4. Sync: every commit main holds since RELEASE_REV must be the release commit of another train of the plan, its
//     tag on origin (an earlier train's, or, on a re-run, a later one's); website/ is restored and HEAD fast-forwarded
//     to main, which semantic-release requires of a branch it pushes. Any other commit fails a releasing train ("main
//     moved since the dispatch"); a train that releases nothing is left there, semantic-release not run.
//  5. The documentation is unpacked into website/, the versioned folders replaced whole: after the sync, since a
//     fast-forward refuses untracked files it would overwrite. The first train that releases commits it.
//  6. semantic-release runs with the configuration, the train's tagFormat and dryRun, and the plugin's environment:
//     RELEASE_TRAIN, RELEASE_TRAIN_PLAN, RELEASE_RUN_COMMITS (the other trains' release commits the sync found),
//     PACKED_VERSION (empty for a train the plan does not release) and RELEASE_PACKAGE_IDS (the train's ids).
//  7. A releasing train must be released at the plan's version, and, on a run that is not a dry run, its tag on its
//     release commit at HEAD, locally and on origin: a dry run semantic-release declines fails, since main moved. A
//     train that releases nothing must release nothing.
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { appendFileSync, copyFileSync, existsSync, mkdirSync, readFileSync, rmSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { loadTrains, tagPrefixOf } from "./release-trains.mjs";

const USAGE = "Usage: release-train.mjs <train> --plan <file> --config <file> [--packages <dir>] [--documentation <file>] " +
  "[--dry-run]";
const STABLE = /^\d+\.\d+\.\d+$/;
const TYPES = ["", "major", "minor", "patch"];
const MAIN = "refs/remotes/origin/main";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
const fromRoot = createRequire(join(root, "package.json"));
const fromSemanticRelease = createRequire(
  join(dirname(fromRoot.resolve("semantic-release/package.json")), "lib", "plugins", "utils.js"));
const load = async (id) => import(pathToFileURL(fromSemanticRelease.resolve(id)).href);

class Refusal extends Error {}
const fail = (message) => {
  throw new Refusal(message);
};
const notice = (message) => console.log(`::notice::${message}`);
const short = (commit) => commit.slice(0, 7);

// Credentials reach git through its environment, never its command line: an extra header for the server's URL, as
// actions/checkout writes it, and none without a token.
const env = process.env;
function credentials() {
  if (!env.GITHUB_TOKEN) return {};
  const server = (env.GITHUB_SERVER_URL ?? "https://github.com").replace(/\/+$/, "");
  const basic = Buffer.from(`x-access-token:${env.GITHUB_TOKEN}`).toString("base64");
  return { GIT_CONFIG_COUNT: "1", GIT_CONFIG_KEY_0: `http.${server}/.extraheader`, GIT_CONFIG_VALUE_0: `AUTHORIZATION: basic ${basic}` };
}
const git = (args, { remote = false } = {}) => execFileSync("git", args, {
  cwd: root, encoding: "utf8", maxBuffer: 1 << 28, stdio: ["ignore", "pipe", "pipe"],
  env: remote ? { ...env, ...credentials(), GIT_TERMINAL_PROMPT: "0" } : env,
}).trim();
function isAncestor(ancestor, commit) {
  try {
    execFileSync("git", ["merge-base", "--is-ancestor", ancestor, commit], { cwd: root, stdio: "ignore" });
    return true;
  } catch (error) {
    if (error.status === 1) return false;
    throw error;
  }
}
const subjectOf = (commit) => git(["log", "-1", "--format=%s", commit]);
const commitsOf = (range) => git(["rev-list", "--reverse", range]).split("\n").filter(Boolean);

function parseArguments(args) {
  const options = { dryRun: false, packages: "artifacts/packages" };
  const positional = [];
  for (let index = 0; index < args.length; index++) {
    const arg = args[index];
    if (arg === "--dry-run") {
      options.dryRun = true;
    } else if (["--plan", "--config", "--packages", "--documentation"].includes(arg)) {
      const value = args[++index];
      if (!value) fail(`${arg} needs a value. ${USAGE}`);
      options[arg.slice(2)] = value;
    } else if (arg.startsWith("--")) {
      fail(`Unknown option ${arg}. ${USAGE}`);
    } else {
      positional.push(arg);
    }
  }
  if (positional.length !== 1 || !options.plan || !options.config) fail(USAGE);
  return { ...options, train: positional[0] };
}

/** The plan's rows, every train of the list in its order, each checked, and the train's. */
function readPlan(file, train, model) {
  let rows;
  try {
    rows = JSON.parse(readFileSync(file, "utf8"));
  } catch (error) {
    fail(`The plan, ${file}, cannot be read: ${error.message}`);
  }
  const names = Array.isArray(rows) ? rows.map((row) => row?.train).join(" ") : "";
  if (names !== model.names.join(" ")) {
    fail(`The plan, ${file}, lists the trains '${names}', not those of src/release-trains.json, ${model.names.join(" ")}.`);
  }
  for (const row of rows) {
    const prefix = tagPrefixOf(row.train);
    if (!STABLE.test(row.last ?? "") || row.tag !== `${prefix}${row.last}`) {
      fail(`The ${row.train} train's plan names no last release of the train: '${row.tag}'.`);
    }
    if (!TYPES.includes(row.type) || (row.type && !STABLE.test(row.next ?? ""))) {
      fail(`The ${row.train} train's plan releases '${row.type}' at '${row.next}'.`);
    }
    if (row.type && !(row.ids ?? "").trim()) fail(`The ${row.train} train's plan lists none of its packages.`);
  }
  const row = rows.find((candidate) => candidate.train === train);
  if (!row) fail(`${train} is no train of src/release-trains.json.`);
  return { rows, row };
}

/** The configuration, of which semantic-release's own search must add nothing, and the entries the driver reads. */
async function readConfiguration(file) {
  let options;
  try {
    options = JSON.parse(readFileSync(file, "utf8"));
  } catch (error) {
    fail(`The configuration, ${file}, cannot be read: ${error.message}`);
  }
  // semantic-release merges the options it is given over the file it finds itself, .releaserc.json: a key only that
  // file holds would reach the release.
  const found = join(root, ".releaserc.json");
  const extra = existsSync(found) && resolve(file) !== found
    ? Object.keys(JSON.parse(readFileSync(found, "utf8"))).filter((key) => !(key in options))
    : [];
  if ("extends" in options) fail(`${file} extends another configuration: give it every option itself.`);
  if (extra.length) {
    fail(`.releaserc.json's ${extra.join(", ")} would reach semantic-release beside ${file}: give ${file} every key ` +
      ".releaserc.json has.");
  }
  const entry = (name) => (options.plugins ?? []).find((plugin) => (Array.isArray(plugin) ? plugin[0] : plugin) === name);
  const gitEntry = entry("@semantic-release/git");
  if (!Array.isArray(gitEntry) || typeof gitEntry[1]?.message !== "string" || !Array.isArray(gitEntry[1]?.assets)) {
    fail(`${file} gives @semantic-release/git no message and assets, which name a release commit and what it touches.`);
  }
  const { template } = await load("lodash-es");
  const message = template(gitEntry[1].message);
  const glob = (pattern) => new RegExp(`^${pattern.split(/(\*\*|\*)/).map((part) =>
    (part === "**" ? ".*" : part === "*" ? "[^/]*" : part.replace(/[.+?^${}()|[\]\\]/g, "\\$&"))).join("")}$`);
  const assets = gitEntry[1].assets.map((asset) => glob(typeof asset === "string" ? asset : asset.path));
  const github = entry("@semantic-release/github");
  const subject = (tag, version) =>
    message({ branch: "main", lastRelease: {}, nextRelease: { gitTag: tag, version, notes: "" } }).split("\n")[0];
  const placeholder = "\u0000";
  return {
    options,
    subject,
    /** The subject of any release commit of the train whose tags start with prefix, its version captured. */
    released: (prefix) => new RegExp(`^${subject(`${prefix}${placeholder}`, placeholder)
      .replace(/[.*+?^${}()|[\]\\]/g, "\\$&").replace(placeholder, "(\\d+\\.\\d+\\.\\d+)")}$`),
    touches: (path) => assets.some((pattern) => pattern.test(path)),
    github: Array.isArray(github) ? github[1] ?? {} : github ? {} : undefined,
  };
}

/** Why a commit is not the release commit of a tag, or nothing when it is. */
function releaseFault(commit, subject, configuration) {
  if (subjectOf(commit) !== subject) return `its subject is not '${subject}'`;
  if (git(["rev-list", "--parents", "-n", "1", commit]).split(" ").length !== 2) return "it has more than one parent";
  const outside = git(["diff-tree", "--no-commit-id", "--name-only", "-r", commit]).split("\n").filter(Boolean)
    .filter((path) => !configuration.touches(path));
  return outside.length ? `it changes ${outside.join(", ")}, which no release commit writes` : undefined;
}

/** The subjects of the release commits the plan's other trains may make, each to its tag. */
const othersOf = (rows, row, configuration) =>
  new Map(rows.filter((candidate) => candidate.type && candidate.train !== row.train).map((candidate) => {
    const tag = `${tagPrefixOf(candidate.train)}${candidate.next}`;
    return [configuration.subject(tag, candidate.next), tag];
  }));

/** Why a commit main holds since the dispatched commit is no other train's release commit of the plan, or nothing. */
function otherFault(commit, others, configuration) {
  const subject = subjectOf(commit);
  const tag = others.get(subject);
  if (!tag) return "no other train of this release made it";
  return releaseFault(commit, subject, configuration) ?? (tagOnOrigin(tag) === commit ? undefined : `${tag} is not on it on origin`);
}

/**
 * Why a commit is not this dispatch's release commit of a tag, or nothing: one that descends from the dispatched commit
 * past the other trains' release commits alone, main holds, and is a release commit.
 */
function ownFault(commit, { rev, subject, others, configuration }) {
  if (commit === rev || !isAncestor(rev, commit)) return "it does not descend from the dispatched commit";
  if (!isAncestor(commit, MAIN)) return "main does not hold it";
  const fault = releaseFault(commit, subject, configuration);
  if (fault) return fault;
  for (const between of commitsOf(`${rev}..${commit}^`)) {
    const other = otherFault(between, others, configuration);
    if (other) return `it follows ${short(between)} ${subjectOf(between)} (${other})`;
  }
  return undefined;
}

/**
 * A release commit of a train at or below the dispatched commit whose tag origin lacks: a run stopped between the two
 * pushes, and semantic-release, counting from the last tag merged, would release its version a second time. Every
 * train of the plan is checked, the core's step first, so that none is released before the refusal.
 */
function unfinished({ rows, rev, configuration }) {
  for (const row of rows) {
    const pattern = configuration.released(tagPrefixOf(row.train));
    for (const line of git(["log", "--format=%H %s", `${row.tag}..${rev}`]).split("\n").filter(Boolean)) {
      const [commit, subject] = [line.slice(0, 40), line.slice(41)];
      const version = pattern.exec(subject)?.[1];
      if (!version) continue;
      const tag = `${tagPrefixOf(row.train)}${version}`;
      if (tagOnOrigin(tag) !== commit) {
        fail(`${short(commit)} ${subject}, which the dispatched commit holds, has no ${tag} on origin: complete that ` +
          "release with Re-run failed jobs of the run that made it, then dispatch release.yml again.");
      }
    }
  }
}

/** The commit a tag names on origin, or nothing when origin has no such tag. */
function tagOnOrigin(tag) {
  const line = git(["ls-remote", "origin", `refs/tags/${tag}`], { remote: true });
  if (!line) return undefined;
  return git(["rev-parse", "--verify", "--quiet", `${line.split(/\s+/)[0]}^{commit}`]);
}

/** The train's files, staged where the configuration's GitHub assets look, with their digests. */
function stage(row, version, packages) {
  const folder = join(root, "artifacts", "release");
  rmSync(folder, { recursive: true, force: true });
  mkdirSync(folder, { recursive: true });
  if (!row.type) return [];
  const staged = [];
  for (const id of row.ids.split(/\s+/).filter(Boolean)) {
    for (const extension of ["nupkg", "snupkg"]) {
      const name = `${id}.${version}.${extension}`;
      if (!existsSync(join(packages, name))) {
        if (extension === "nupkg") fail(`${name}, a package of the ${row.train} train, is not in ${packages}.`);
        continue;
      }
      const path = join(folder, name);
      copyFileSync(join(packages, name), path);
      staged.push({ name, path, sha256: createHash("sha256").update(readFileSync(path)).digest("hex") });
    }
  }
  return staged;
}

/** The GitHub API, as the release App's token reaches it. */
function githubApi() {
  if (!env.GITHUB_TOKEN) fail("GITHUB_TOKEN is not set: completing a GitHub Release needs the release App's token.");
  if (!/^[^/\s]+\/[^/\s]+$/.test(env.GITHUB_REPOSITORY ?? "")) fail("GITHUB_REPOSITORY is not <owner>/<repository>.");
  const base = (env.GITHUB_API_URL ?? "https://api.github.com").replace(/\/+$/, "");
  const request = async (method, url, { body, type } = {}) => {
    const response = await fetch(url.startsWith("http") ? url : `${base}${url}`, {
      method,
      body,
      headers: {
        accept: "application/vnd.github+json",
        authorization: `Bearer ${env.GITHUB_TOKEN}`,
        "x-github-api-version": "2022-11-28",
        ...(type ? { "content-type": type } : {}),
      },
    });
    if (!response.ok) fail(`GitHub answered ${method} ${url} with ${response.status}: ${await response.text()}`);
    return response.status === 204 ? undefined : response.json();
  };
  return { request, repository: env.GITHUB_REPOSITORY };
}

/** The GitHub Release of a tag, drafts included, which only an authenticated listing returns. */
async function releaseOf(api, tag) {
  for (let page = 1; ; page++) {
    const releases = await api.request("GET", `/repos/${api.repository}/releases?per_page=100&page=${page}`);
    const found = releases.find((release) => release.tag_name === tag);
    if (found || releases.length < 100) return found;
  }
}

/**
 * The GitHub Release of a tagged train, completed: a published one, or a draft carrying every staged file at its
 * digest, is done; a file missing, or left half uploaded, is uploaded; a file at another digest is another run's.
 * With no release, @semantic-release/github drafts it as semantic-release would have, the release commit's notes in it.
 */
async function completeRelease({ tag, version, commit, staged, configuration, semanticEnv }) {
  if (!configuration.github) fail(`The configuration has no @semantic-release/github entry to draft ${tag}'s release with.`);
  const api = githubApi();
  const release = await releaseOf(api, tag);
  if (!release) {
    const github = await load("@semantic-release/github");
    const logger = { log: console.log, error: console.error, warn: console.warn, success: console.log };
    const context = {
      cwd: root,
      env: semanticEnv,
      logger,
      options: { repositoryUrl: git(["config", "--get", "remote.origin.url"]) },
      branch: { name: "main", type: "release", main: true },
      nextRelease: { type: "", gitTag: tag, version, name: tag, gitHead: commit, channel: null,
        notes: git(["log", "-1", "--format=%b", commit]) },
    };
    // publish verifies the token and the options first, as verifyConditions does.
    await github.publish({ ...configuration.github }, context);
    console.log(`Drafted ${tag}'s GitHub Release, with ${staged.length} files.`);
    return;
  }
  if (!release.draft) {
    console.log(`${tag}'s GitHub Release is published.`);
    return;
  }
  const assets = new Map((release.assets ?? []).map((asset) => [asset.name, asset]));
  let uploaded = 0;
  for (const file of staged) {
    const asset = assets.get(file.name);
    if (asset?.state === "uploaded") {
      if (asset.digest !== `sha256:${file.sha256}`) {
        fail(`${tag}'s draft holds ${file.name} at ${asset.digest ?? "no digest"}, not sha256:${file.sha256}, which this ` +
          "run packed: another run released it.");
      }
      continue;
    }
    if (asset) await api.request("DELETE", `/repos/${api.repository}/releases/assets/${asset.id}`);
    const target = `${release.upload_url.replace(/\{[^}]*\}$/, "")}?name=${encodeURIComponent(file.name)}`;
    const added = await api.request("POST", target, { body: readFileSync(file.path), type: "application/octet-stream" });
    if (added?.digest && added.digest !== `sha256:${file.sha256}`) fail(`GitHub holds ${file.name} at ${added.digest}.`);
    uploaded++;
  }
  console.log(`${tag}'s draft carries every file of the train${uploaded ? `, ${uploaded} uploaded now` : ""}.`);
}

/**
 * A releasing train semantic-release left half-done, finished without it: its release commit, tagged here when it was
 * pushed without its tag, and its GitHub Release completed. Nothing when the train has neither tag nor release commit.
 */
async function resume({ tag, rev, subject, rows, row, configuration, ...rest }) {
  const owned = { rev, subject, others: othersOf(rows, row, configuration), configuration };
  let commit = tagOnOrigin(tag);
  if (commit) {
    const fault = ownFault(commit, owned);
    if (fault) fail(`${tag} is on origin, on ${short(commit)} ${subjectOf(commit)}, but ${fault}: another run released it.`);
    console.log(`${tag} is on origin, on ${short(commit)}: completing its release, without semantic-release.`);
  } else {
    const found = commitsOf(`${rev}..${MAIN}`).filter((candidate) => subjectOf(candidate) === subject);
    if (!found.length) return undefined;
    if (found.length > 1) fail(`main holds ${found.length} commits '${subject}' since the dispatched commit.`);
    [commit] = found;
    const fault = ownFault(commit, owned);
    if (fault) fail(`${short(commit)} ${subject} is on main untagged, but ${fault}.`);
    const local = git(["tag", "--list", tag]) ? git(["rev-parse", `refs/tags/${tag}^{commit}`]) : undefined;
    if (local && local !== commit) fail(`${tag} names ${short(local)} here, not ${short(commit)}.`);
    if (!local) git(["tag", tag, commit]);
    git(["push", "--quiet", "origin", `refs/tags/${tag}:refs/tags/${tag}`], { remote: true });
    notice(`${short(commit)}, ${tag}'s release commit, was on main without its tag: tagged it.`);
  }
  await completeRelease({ tag, commit, configuration, ...rest });
  return commit;
}

/**
 * Main synced: the other trains' release commits main holds since the dispatched commit, HEAD fast-forwarded to it,
 * website/ restored first. Nothing when main moved otherwise and the train releases nothing.
 */
function sync({ rows, rev, row, configuration }) {
  if (!isAncestor(rev, MAIN)) fail(`main no longer holds ${short(rev)}, the dispatched commit: dispatch release.yml again.`);
  const head = git(["rev-parse", "HEAD"]);
  if (!isAncestor(rev, head) || !isAncestor(head, MAIN)) {
    fail(`HEAD, ${short(head)}, is neither the dispatched commit, ${short(rev)}, nor a commit main holds since.`);
  }
  const others = othersOf(rows, row, configuration);
  const run = [];
  for (const commit of commitsOf(`${rev}..${MAIN}`)) {
    const subject = subjectOf(commit);
    const fault = otherFault(commit, others, configuration);
    if (fault) {
      if (row.type) fail(`main moved since the dispatch (${short(commit)} ${subject}: ${fault}): dispatch release.yml again.`);
      // Nothing to release, and nothing to check semantic-release against: a later step may well complete that commit.
      notice(`main holds ${short(commit)} ${subject} since the dispatch (${fault}). The ${row.train} train releases ` +
        "nothing, so semantic-release does not run for it.");
      return undefined;
    }
    others.delete(subject);
    run.push(commit);
  }
  git(["checkout", "--", "website"]);
  git(["clean", "-fdq", "--", "website/versioned_docs", "website/versioned_sidebars"]);
  if (head !== git(["rev-parse", MAIN])) git(["merge", "--ff-only", "--quiet", MAIN]);
  return run;
}

/** The frozen documentation, in place of the versioned folders. */
function unpack(file) {
  for (const folder of ["versioned_docs", "versioned_sidebars"]) {
    rmSync(join(root, "website", folder), { recursive: true, force: true });
  }
  execFileSync("tar", ["-xf", resolve(file), "-C", join(root, "website")], { stdio: "inherit" });
}

function output(values) {
  for (const [key, value] of Object.entries(values)) console.log(`${key}=${value}`);
  if (env.GITHUB_OUTPUT) {
    appendFileSync(env.GITHUB_OUTPUT, Object.entries(values).map(([key, value]) => `${key}=${value}\n`).join(""));
  }
}

function summarize(lines) {
  if (env.GITHUB_STEP_SUMMARY) appendFileSync(env.GITHUB_STEP_SUMMARY, `${lines.join("\n")}\n`);
}

async function main() {
  const options = parseArguments(process.argv.slice(2));
  const model = loadTrains(join(root, "src", "release-trains.json"));
  const { rows, row } = readPlan(options.plan, options.train, model);
  const { train } = options;
  const prefix = tagPrefixOf(train);
  const version = row.type ? row.next : row.last;
  const tag = row.type ? `${prefix}${row.next}` : "";
  const rev = env.RELEASE_REV ?? "";
  if (!/^[0-9a-f]{40}$/.test(rev)) fail(`RELEASE_REV, '${rev}', is not the dispatched commit's hash.`);
  const configuration = await readConfiguration(options.config);
  const subject = configuration.subject(tag, version);
  const semanticEnv = {
    ...env,
    RELEASE_TRAIN: train,
    RELEASE_TRAIN_PLAN: resolve(options.plan),
    RELEASE_RUN_COMMITS: "",
    PACKED_VERSION: row.type ? version : "",
    RELEASE_PACKAGE_IDS: (row.ids ?? "").trim(),
  };

  git(["fetch", "--quiet", "--force", "--tags", "origin", `+refs/heads/main:${MAIN}`], { remote: true });
  try {
    git(["rev-parse", "--verify", "--quiet", `${rev}^{commit}`]);
  } catch {
    fail(`RELEASE_REV, ${rev}, is no commit of this clone.`);
  }
  unfinished({ rows, rev, configuration });
  const staged = stage(row, version, resolve(options.packages));

  const done = (values) => output({ release: String(Boolean(row.type)), version, tag, commit: "", resumed: "false", ...values });
  if (row.type && !options.dryRun) {
    const commit = await resume({ tag, version, rev, subject, rows, row, configuration, staged, semanticEnv });
    if (commit) {
      summarize([`### ${train}`, "", `${tag} was on origin or on main already: its release was completed, without semantic-release.`]);
      return done({ commit, resumed: "true" });
    }
  }

  const run = sync({ rows, rev, row, configuration });
  if (!run) {
    summarize([`### ${train}`, "", "Releases nothing; main holds other commits since the dispatch, so semantic-release did not run."]);
    return done({});
  }
  if (options.documentation) unpack(options.documentation);

  const { default: semanticRelease } = await import(pathToFileURL(fromRoot.resolve("semantic-release")).href);
  let result;
  try {
    result = await semanticRelease(
      { ...configuration.options, tagFormat: `${prefix}\${version}`, dryRun: options.dryRun },
      { cwd: root, env: { ...semanticEnv, RELEASE_RUN_COMMITS: run.join(" ") } });
  } catch (error) {
    const reason = [error, ...(error.errors ?? [])].map(({ message }) => message?.split("\n")[0]).find(Boolean);
    fail(`semantic-release failed for the ${train} train, as its log above says${reason ? `: ${reason}` : "."}`);
  }

  let commit = "";
  if (!row.type) {
    if (result) fail(`semantic-release released ${result.nextRelease?.version} of the ${train} train, which the plan does not release.`);
    summarize([`### ${train}`, "", `Releases nothing, as the plan computed from ${row.tag}.`]);
    return done({});
  }
  if (!result) {
    fail(`semantic-release releases nothing of the ${train} train, where the plan releases ${version}: main moved since ` +
      "the dispatch. Dispatch release.yml again.");
  }
  if (result.nextRelease.version !== version) {
    fail(`semantic-release released ${result.nextRelease.version} of the ${train} train, not ${version}, the version packed.`);
  }
  if (!options.dryRun) {
    commit = git(["rev-parse", "HEAD"]);
    const fault = releaseFault(commit, subject, configuration) ??
      (git(["rev-parse", `refs/tags/${tag}^{commit}`]) === commit && tagOnOrigin(tag) === commit
        ? undefined : `${tag} is not on it, here and on origin`);
    if (fault) fail(`HEAD, ${short(commit)}, is no release commit of ${tag}: ${fault}.`);
  }
  summarize([`### ${train}`, "", `${options.dryRun ? "Would release" : "Released"} ${tag} (${row.type}), from ${row.tag}, ` +
    "as the plan computed.", "", "<details><summary>Notes</summary>", "", result.nextRelease.notes, "", "</details>"]);
  return done({ commit });
}

try {
  await main();
} catch (error) {
  console.error(`::error::${error instanceof Refusal ? error.message : error.stack ?? error}`);
  process.exitCode = 1;
}
