// Tests of .releaserc.json and of the semantic-release package.json installs: node --test '.github/scripts/*.test.mjs'
// (ci.yml's job "release tooling", after npm ci). Node's built-ins, and the plugins semantic-release itself loads.
import { test } from "node:test";
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { cpSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const scripts = dirname(fileURLToPath(import.meta.url));
const root = join(scripts, "..", "..");
const fromRoot = createRequire(join(root, "package.json"));
const fromSemanticRelease = createRequire(
  join(dirname(fromRoot.resolve("semantic-release/package.json")), "lib", "plugins", "utils.js"));
const changelogPlugin = await import(pathToFileURL(fromSemanticRelease.resolve("@semantic-release/changelog")).href);
const { template } = await import(pathToFileURL(fromSemanticRelease.resolve("lodash-es")).href);

const MARKER = "<!-- Releases are prepended below this line. -->";
const read = (path) => readFileSync(path, "utf8");
const releaserc = JSON.parse(read(join(root, ".releaserc.json")));
const entry = (name) => releaserc.plugins.find((plugin) => (Array.isArray(plugin) ? plugin[0] : plugin) === name)?.[1];
// Nothing of the machine's environment but what git and node need: no CI variable, no GitHub token, and no git
// configuration of the machine's, its per-user ignore and attributes files ($XDG_CONFIG_HOME/git/) included.
const BASE = { PATH: process.env.PATH, HOME: process.env.HOME, GIT_TERMINAL_PROMPT: "0", GIT_CONFIG_GLOBAL: "/dev/null",
  GIT_CONFIG_NOSYSTEM: "1", XDG_CONFIG_HOME: join(tmpdir(), "release-config-test-no-configuration") };

test("the GitHub Release cuts long notes below GitHub's limit and links the changelog at the tag", () => {
  // GitHub refuses a release body above 125,000 characters: long notes are cut at a line, and the changelog has them.
  const render = (notes) => template(entry("@semantic-release/github").releaseBodyTemplate)({
    nextRelease: { notes, version: "0.3.0", gitTag: "v0.3.0" },
    env: { RELEASE_PACKAGE_IDS: "AdCodicem.ValueObjects AdCodicem.ValueObjects.Json" },
  });
  const short = render("## notes\n");
  assert.ok(short.startsWith("## notes\n"));
  assert.doesNotMatch(short, /and more/);
  assert.match(short, /`AdCodicem\.ValueObjects\.0\.3\.0\.sigstore\.json`/);
  const long = `## [0.3.0]\n\n${"* a line of the notes, one of many, long enough to add up quickly\n".repeat(5000)}`;
  const cut = render(long);
  assert.ok(cut.length < 125000, String(cut.length));
  assert.ok(cut.startsWith(long.slice(0, long.lastIndexOf("\n", 100000))));
  assert.ok(cut.includes("…and more: the whole list is in [CHANGELOG.md](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/v0.3.0/CHANGELOG.md)."));
  assert.match(cut, /\| `AdCodicem\.ValueObjects\.Json` \| \[0\.3\.0\]\(https:\/\/www\.nuget\.org\/packages\/AdCodicem\.ValueObjects\.Json\/0\.3\.0\) \|/);
});

test("the changelog opens with its title and the marker, which is the configuration's title", () => {
  const text = read(join(root, "CHANGELOG.md"));
  assert.ok(text.startsWith("# "));
  const end = text.indexOf(MARKER);
  assert.ok(end > 0);
  assert.doesNotMatch(text.slice(0, end), /^## /m);
  assert.equal(entry("@semantic-release/changelog").changelogTitle, text.slice(0, end + MARKER.length));
});

test("a release writes its entry right below the marker, under one title", async () => {
  const dir = mkdtempSync(join(tmpdir(), "release-config-changelog-"));
  try {
    cpSync(join(root, "CHANGELOG.md"), join(dir, "CHANGELOG.md"));
    const options = entry("@semantic-release/changelog");
    const logger = { log() {}, error() {}, success() {}, warn() {} };
    const context = { cwd: dir, logger, options: {}, nextRelease: { notes: "## [0.3.0](compare) (2026-11-02)\n\n### Features\n\n* a feature\n" } };
    await changelogPlugin.verifyConditions({ ...options }, context);
    await changelogPlugin.prepare({ ...options }, context);
    const before = read(join(root, "CHANGELOG.md"));
    const after = read(join(dir, "CHANGELOG.md"));
    const title = options.changelogTitle;
    assert.ok(after.startsWith(`${title}\n\n## [0.3.0](compare) (2026-11-02)\n`), after.slice(0, 900));
    assert.equal(after.match(/^# Changelog$/gm).length, 1);
    assert.ok(after.includes(before.slice(title.length).trim()), "every earlier entry is kept below");
    assert.ok(after.indexOf("## [0.3.0]") < after.indexOf("## [0.2.1]"));
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test("semantic-release lists every commit when two reads of git's output split a record's separator", async () => {
  // git-log-parser cuts git log's output into commits with split2, which package.json overrides: split2 1.0.0 split
  // each read alone, so a separator two reads cut joined two commits into one, and semantic-release lost the second
  // (release run 12). The git alone on PATH writes the real one's output in two writes, cut inside the first
  // separator. Alone, so that a fake that cannot run (an interpreter path with a space, a temporary folder mounted
  // noexec) fails the test rather than leaving the real git to answer in one read; getBuiltinModule, so that it runs
  // as CommonJS or as a module, whatever package.json sits above the temporary folder.
  const dir = mkdtempSync(join(tmpdir(), "release-config-repo-"));
  const bin = mkdtempSync(join(tmpdir(), "release-config-git-"));
  const git = (...args) => execFileSync("git", args, { cwd: dir, env: BASE, encoding: "utf8" }).trim();
  const commit = (message, path) => {
    mkdirSync(dirname(join(dir, path)), { recursive: true });
    writeFileSync(join(dir, path), `${message}\n`);
    git("add", "-A");
    git("-c", "user.name=test", "-c", "user.email=test@example.invalid", "commit", "-q", "-m", message);
  };
  try {
    git("init", "-q", "-b", "main");
    commit("chore: the start", "README.md");
    git("tag", "v0.2.1");
    commit("fix(a): a fix", "src/A/A.cs");
    commit("fix(b): another fix", "src/B/B.cs");
    writeFileSync(join(bin, "git"), `#!${process.execPath}
const { execFileSync } = process.getBuiltinModule("node:child_process");
const out = execFileSync("git", process.argv.slice(2), { env: { ...process.env, PATH: process.env.REAL_PATH } });
const at = out.indexOf("==END==\\n");
if (at < 0) {
  process.stderr.write("git log wrote no ==END== separator\\n");
  process.exit(1);
}
process.stdout.write(out.subarray(0, at + 4), () => setTimeout(() => process.stdout.write(out.subarray(at + 4)), 200));
`, { mode: 0o755 });
    const lib = join(dirname(fromRoot.resolve("semantic-release/package.json")), "lib", "git.js");
    const { getCommits } = await import(pathToFileURL(lib).href);
    const commits = await getCommits("v0.2.1", "HEAD", { cwd: dir, env: { ...BASE, PATH: bin, REAL_PATH: BASE.PATH } });
    assert.deepEqual(commits.map(({ hash }) => hash), git("rev-list", "v0.2.1..HEAD").split("\n"));
    assert.ok(commits.every(({ committerDate }) => !Number.isNaN(committerDate.getTime())));
  } finally {
    rmSync(bin, { recursive: true, force: true });
    rmSync(dir, { recursive: true, force: true });
  }
});
