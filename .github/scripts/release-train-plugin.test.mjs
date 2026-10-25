// Tests of release-train-plugin.mjs and release-train.releaserc.json, and of the one-version changelog's title:
// node --test .github/scripts/release-train-plugin.test.mjs (ci.yml's job "release tooling").
//
// Each case builds a throwaway repository: two trains (Core.A on the core, Sat.B on the satellites, a src/Shared file
// both link), this repository's scripts, configurations and changelogs, its node_modules linked, both trains tagged
// at 0.2.1 and a bare remote on disk. The plan comes from next-version.mjs --trains in that repository, as the pack job
// computes it, and semantic-release runs through its JavaScript API with release-train.releaserc.json, minus the
// GitHub entry, which needs the API, in an environment holding the plan alone: no CI variable reaches it. It runs in a
// process of its own, since it hooks the standard streams of its process, which carry the test runner's reports.
import { test } from "node:test";
import assert from "node:assert/strict";
import { execFile, execFileSync } from "node:child_process";
import { cpSync, mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { promisify } from "node:util";
import { analyzerRules, tagPrefixOf } from "./release-trains.mjs";
import { MARKER } from "./release-train-plugin.mjs";

const scripts = dirname(fileURLToPath(import.meta.url));
const root = join(scripts, "..", "..");
const fromRoot = createRequire(join(root, "package.json"));
const SEMANTIC_RELEASE = fromRoot.resolve("semantic-release");
const fromSemanticRelease = createRequire(
  join(dirname(fromRoot.resolve("semantic-release/package.json")), "lib", "plugins", "utils.js"));
const changelogPlugin = await import(pathToFileURL(fromSemanticRelease.resolve("@semantic-release/changelog")).href);
const { template } = await import(pathToFileURL(fromSemanticRelease.resolve("lodash-es")).href);

const read = (path) => readFileSync(path, "utf8");
const releaserc = JSON.parse(read(join(root, ".releaserc.json")));
const trainConfig = JSON.parse(read(join(scripts, "release-train.releaserc.json")));
const entry = (config, name) => config.plugins.find((plugin) => (Array.isArray(plugin) ? plugin[0] : plugin) === name)?.[1];
const LIST = JSON.stringify({
  core: { breaking: "minor", projects: ["Core.A"] },
  satellites: { breaking: "minor", projects: "*" },
}, null, 2);
// Nothing of the machine's environment but what git and node need: no CI variable, no GitHub token, no step summary,
// and no git configuration of the machine's, its per-user ignore and attributes files ($XDG_CONFIG_HOME/git/) included.
const BASE = { PATH: process.env.PATH, HOME: process.env.HOME, GIT_TERMINAL_PROMPT: "0", GIT_CONFIG_GLOBAL: "/dev/null",
  GIT_CONFIG_NOSYSTEM: "1", XDG_CONFIG_HOME: join(tmpdir(), "release-train-plugin-test-no-configuration") };

/** A throwaway repository with both trains at 0.2.1 and a bare remote, removed by its remove(). */
function repository() {
  const dir = mkdtempSync(join(tmpdir(), "release-train-"));
  const work = join(dir, "work");
  const remote = join(dir, "remote.git");
  const git = (...args) => execFileSync("git", args, { cwd: work, encoding: "utf8", env: BASE }).trim();
  execFileSync("git", ["init", "--quiet", "--bare", "--initial-branch=main", remote], { env: BASE });
  mkdirSync(work);
  git("init", "--quiet", "--initial-branch=main");
  for (const [key, value] of [["user.email", "test@example.invalid"], ["user.name", "Test"], ["commit.gpgsign", "false"],
    ["tag.gpgSign", "false"], ["core.hooksPath", "/dev/null"]]) git("config", key, value);
  const write = (path, text) => {
    mkdirSync(dirname(join(work, path)), { recursive: true });
    writeFileSync(join(work, path), text);
  };
  const commit = (message, files = {}) => {
    for (const [path, text] of Object.entries(files)) write(path, text);
    git("add", "--all");
    git("commit", "--quiet", "--allow-empty", "--message", message);
    return git("rev-parse", "HEAD");
  };
  const project = (name) => write(`src/${name}/${name}.csproj`, [
    '<Project Sdk="Microsoft.NET.Sdk">',
    "  <ItemGroup>",
    '    <Compile Include="..\\Shared\\Both.cs" Link="Shared\\Both.cs" />',
    "  </ItemGroup>",
    "</Project>",
  ].join("\n"));
  for (const file of ["release-trains.mjs", "next-version.mjs", "release-train-plugin.mjs", "release-train.releaserc.json"]) {
    cpSync(join(scripts, file), join(work, ".github", "scripts", file));
  }
  for (const file of [".releaserc.json", "CHANGELOG.md", "CHANGELOG.satellites.md"]) cpSync(join(root, file), join(work, file));
  symlinkSync(join(root, "node_modules"), join(work, "node_modules"), "dir");
  write(".gitignore", "node_modules\nartifacts/\n");
  write("src/release-trains.json", LIST);
  write("src/Shared/Both.cs", "class Both {}\n");
  project("Core.A");
  project("Sat.B");
  // next-version.mjs reads each project's restore graph, for the trains a <PackageVersion> releases.
  for (const name of ["Core.A", "Sat.B"]) {
    write(`artifacts/obj/${name}/project.assets.json`, JSON.stringify({ version: 3, libraries: {} }));
  }
  const base = commit("build: the trains");
  git("tag", "v0.2.1");
  git("tag", "satellites-v0.2.1");
  git("remote", "add", "origin", remote);
  git("push", "--quiet", "origin", "main", "--tags");
  return { dir, root: work, remote, git, write, commit, base, remove: () => rmSync(dir, { recursive: true, force: true }) };
}

/** The pack job's plan of HEAD, next-version.mjs --trains's rows, by train. */
function planOf(repo) {
  const out = execFileSync(process.execPath, [".github/scripts/next-version.mjs", "--trains"],
    { cwd: repo.root, encoding: "utf8", env: BASE, stdio: ["ignore", "pipe", "ignore"] });
  const line = out.split("\n").find((text) => text.startsWith("trains_json="));
  return Object.fromEntries(JSON.parse(line.slice("trains_json=".length)).map((row) => [row.train, row]));
}

/** The environment a train's release starts with, the plan written to a file of its own, as release.yml hands it over. */
let plans = 0;
function envOf(repo, plan, train, { run = "", packed } = {}) {
  const file = join(repo.dir, `plan-${++plans}.json`);
  writeFileSync(file, JSON.stringify(Object.values(plan)));
  const row = plan[train];
  return {
    ...BASE,
    RELEASE_TRAIN: train,
    RELEASE_TRAIN_PLAN: file,
    RELEASE_RUN_COMMITS: run,
    PACKED_VERSION: packed ?? (row?.type ? row.next : ""),
  };
}

// Runs semantic-release with the options given and writes what it returned, or the errors it threw, to a file.
const RUNNER = `import { writeFileSync } from "node:fs";
const [module, options, out] = process.argv.slice(2);
const { default: semanticRelease } = await import(module);
try {
  const result = await semanticRelease(JSON.parse(options), { cwd: process.cwd(), env: process.env });
  writeFileSync(out, JSON.stringify({ result: result && { nextRelease: result.nextRelease } }));
} catch (error) {
  const messages = [error, ...(error.errors ?? [])].map(({ message }) => message);
  writeFileSync(out, JSON.stringify({ error: { message: messages.join("\\n") } }));
}
`;

/** semantic-release over a train: { result, error, log }, a dry run unless told otherwise. */
async function release(repo, train, env, { dryRun = true, tagFormat = `${tagPrefixOf(train)}\${version}` } = {}) {
  const config = JSON.parse(read(join(repo.root, ".github", "scripts", "release-train.releaserc.json")));
  config.plugins = config.plugins.filter((plugin) => plugin[0] !== "@semantic-release/github");
  const options = { ...config, tagFormat, dryRun, ci: false, repositoryUrl: pathToFileURL(repo.remote).href };
  const out = join(repo.dir, "outcome.json");
  rmSync(out, { force: true });
  writeFileSync(join(repo.dir, "run.mjs"), RUNNER);
  let log;
  try {
    const { stdout, stderr } = await promisify(execFile)(process.execPath,
      [join(repo.dir, "run.mjs"), pathToFileURL(SEMANTIC_RELEASE).href, JSON.stringify(options), out],
      { cwd: repo.root, env, encoding: "utf8", maxBuffer: 1 << 26 });
    log = `${stdout}${stderr}`;
  } catch (error) {
    log = `${error.stdout ?? ""}${error.stderr ?? ""}`;
  }
  let outcome;
  try {
    outcome = JSON.parse(read(out));
  } catch {
    outcome = { error: { message: "semantic-release ended without an outcome" } };
  }
  return { ...outcome, log };
}

const released = (outcome, version, type) => {
  assert.equal(outcome.error, undefined, `${outcome.error?.message}\n${outcome.log}`);
  assert.notEqual(outcome.result, false, outcome.log);
  assert.equal(outcome.result.nextRelease.version, version, outcome.log);
  assert.equal(outcome.result.nextRelease.type, type, outcome.log);
  return outcome.result.nextRelease.notes;
};
const nothing = (outcome) => {
  assert.equal(outcome.error, undefined, `${outcome.error?.message}\n${outcome.log}`);
  assert.equal(outcome.result, false, outcome.log);
};
const refused = (outcome, message) => {
  assert.ok(outcome.error, `expected a refusal\n${outcome.log}`);
  assert.match(outcome.error.message, message, outcome.log);
};

test("the train configuration's rules and notes are the one-version configuration's", () => {
  const [train, one] = [analyzerRules(trainConfig), analyzerRules(releaserc)];
  assert.deepEqual(train.rules, one.rules);
  const { releaseRules: _, ...options } = one.options;
  assert.deepEqual({ ...train.options, releaseRules: undefined }, { ...options, releaseRules: undefined });
  const plugin = entry(trainConfig, "./.github/scripts/release-train-plugin.mjs");
  assert.deepEqual(plugin.notes, entry(releaserc, "@semantic-release/release-notes-generator"));
  assert.equal(trainConfig.tagFormat, releaserc.tagFormat);
  assert.deepEqual(trainConfig.branches, releaserc.branches);
  const git = entry(trainConfig, "@semantic-release/git");
  for (const asset of entry(releaserc, "@semantic-release/git").assets) assert.ok(git.assets.includes(asset), asset);
  assert.ok(git.assets.includes("CHANGELOG.*.md"));
  assert.match(git.message, /^chore\(release\): \$\{nextRelease\.gitTag\} \[skip ci\]/);
  const github = entry(trainConfig, "@semantic-release/github");
  assert.equal(github.draftRelease, true);
  assert.equal(github.successCommentCondition, false);
});

test("the GitHub Release names each train's bundle and links each of its packages", () => {
  const body = entry(trainConfig, "@semantic-release/github").releaseBodyTemplate;
  const render = (train, ids, notes = "## notes") => template(body)({
    nextRelease: { notes, version: "0.3.0", gitTag: `${tagPrefixOf(train)}0.3.0` }, env: { RELEASE_TRAIN: train, RELEASE_PACKAGE_IDS: ids },
  });
  const core = render("core", "AdCodicem.ValueObjects AdCodicem.ValueObjects.Json");
  assert.match(core, /`AdCodicem\.ValueObjects\.0\.3\.0\.sigstore\.json`/);
  assert.match(core, /\| `AdCodicem\.ValueObjects\.Json` \| \[0\.3\.0\]\(https:\/\/www\.nuget\.org\/packages\/AdCodicem\.ValueObjects\.Json\/0\.3\.0\) \|/);
  assert.match(core, /adcodicem\.valueobjects\.json\/0\.3\.0\/lib\/net10\.0\/AdCodicem\.ValueObjects\.Json\.dll/);
  const satellites = render("satellites", "AdCodicem.ValueObjects.Dapper AdCodicem.ValueObjects.Testing");
  assert.match(satellites, /`AdCodicem\.ValueObjects\.satellites\.0\.3\.0\.sigstore\.json`/);
  assert.match(satellites, /gh attestation verify AdCodicem\.ValueObjects\.Dapper\.0\.3\.0\.nupkg/);
  assert.match(satellites, /adcodicem\.valueobjects\.dapper\/0\.3\.0\/lib\/net10\.0\/AdCodicem\.ValueObjects\.Dapper\.dll/);
  assert.doesNotMatch(satellites, /Json/);
  assert.equal(render("core", "").trim(), "## notes");
  // GitHub refuses a release body above 125,000 characters: long notes are cut at a line, and the changelog has them.
  const long = `## [0.3.0]\n\n### Documentation\n\n${"* a page no train owns, one of many ([abcdef0](https://example.invalid))\n".repeat(5000)}`;
  for (const [train, file] of [["core", "CHANGELOG.md"], ["satellites", "CHANGELOG.satellites.md"]]) {
    const cut = render(train, "AdCodicem.ValueObjects.Dapper", long);
    assert.ok(cut.length < 125000, `${train}: ${cut.length}`);
    assert.ok(cut.startsWith(long.slice(0, long.lastIndexOf("\n", 100000))));
    assert.ok(cut.includes(`…and more: the whole list is in [${file}](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/` +
      `${tagPrefixOf(train)}0.3.0/${file}).`), train);
    assert.match(cut, /### NuGet packages/);
  }
  assert.doesNotMatch(render("core", "AdCodicem.ValueObjects"), /and more/);
});

test("the one-version GitHub Release cuts long notes the same way", () => {
  const body = entry(releaserc, "@semantic-release/github").releaseBodyTemplate;
  const render = (notes) => template(body)({
    nextRelease: { notes, version: "0.3.0", gitTag: "v0.3.0" }, env: { RELEASE_PACKAGE_IDS: "AdCodicem.ValueObjects AdCodicem.ValueObjects.Json" },
  });
  const short = render("## notes\n");
  assert.ok(short.startsWith("## notes\n"));
  assert.doesNotMatch(short, /and more/);
  assert.match(short, /`AdCodicem\.ValueObjects\.0\.3\.0\.sigstore\.json`/);
  const cut = render(`## [0.3.0]\n\n${"* a line of the notes, one of many, long enough to add up quickly\n".repeat(5000)}`);
  assert.ok(cut.length < 125000, String(cut.length));
  assert.ok(cut.includes("…and more: the whole list is in [CHANGELOG.md](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/v0.3.0/CHANGELOG.md)."));
  assert.match(cut, /\| `AdCodicem\.ValueObjects\.Json` \|/);
});

test("each changelog opens with its title and the marker, which the one-version configuration's title is", () => {
  for (const file of ["CHANGELOG.md", "CHANGELOG.satellites.md"]) {
    const text = read(join(root, file));
    assert.ok(text.startsWith("# "), file);
    const end = text.indexOf(MARKER);
    assert.ok(end > 0, file);
    assert.doesNotMatch(text.slice(0, end), /^## /m, file);
  }
  const text = read(join(root, "CHANGELOG.md"));
  assert.equal(entry(releaserc, "@semantic-release/changelog").changelogTitle, text.slice(0, text.indexOf(MARKER) + MARKER.length));
});

test("a one-version release writes its entry right below the marker, under one title", async () => {
  const dir = mkdtempSync(join(tmpdir(), "release-train-changelog-"));
  try {
    cpSync(join(root, "CHANGELOG.md"), join(dir, "CHANGELOG.md"));
    const options = entry(releaserc, "@semantic-release/changelog");
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

test("a dry run of each train computes the plan's version, from its own commits", async (t) => {
  const A = { "src/Core.A/A.cs": "class A {}\n" };
  const B = { "src/Sat.B/B.cs": "class B {}\n" };
  // Each history, the version each train releases at (null for none), and what its notes must and must not hold.
  const cases = {
    "a core fix": [(repo) => repo.commit("fix(a): a core fix", A), { core: ["0.2.2", "patch"], satellites: null }],
    "a satellite feature": [(repo) => repo.commit("feat(b): a satellite feature", B),
      { core: null, satellites: ["0.3.0", "minor"] }],
    "a fix on each": [(repo) => {
      repo.commit("fix(a): a core fix", A);
      repo.commit("fix(b): a satellite fix", B);
    }, { core: ["0.2.2", "patch"], satellites: ["0.2.2", "patch"] }],
    "a fix in a Shared file both link": [(repo) => repo.commit("fix: in both", { "src/Shared/Both.cs": "class Both { }\n" }),
      { core: ["0.2.2", "patch"], satellites: ["0.2.2", "patch"] }],
    "a core feature, which moves the core's line": [(repo) => repo.commit("feat(a): a core feature", A),
      { core: ["0.3.0", "minor"], satellites: ["0.3.0", "minor"] }],
    "nothing that releases": [(repo) => {
      repo.commit("docs: a page", { "docs/page.md": "# page\n" });
      repo.commit("ci: a workflow", { ".github/workflows/x.yml": "on: push\n" });
    }, { core: null, satellites: null }],
    // The analyzer's options reach it: the train's breaking rule (a minor while it is 0.x), and the two rules of
    // .releaserc.json that release a patch.
    "a breaking satellite feature": [(repo) => repo.commit("feat(b)!: drop a thing\n\nBREAKING CHANGE: Thing is gone", B),
      { core: null, satellites: ["0.3.0", "minor"] }, { satellites: [/### ⚠ BREAKING CHANGES\n\n\* \*\*b:\*\* Thing is gone/] }],
    "a breaking satellite fix": [(repo) => repo.commit("fix(b)!: change a thing", B),
      { core: null, satellites: ["0.3.0", "minor"] }, { satellites: [/### ⚠ BREAKING CHANGES\n\n\* \*\*b:\*\* change a thing/] }],
    "a breaking core fix, which moves the core's line": [(repo) => repo.commit("fix(a)!: change a thing", A),
      { core: ["0.3.0", "minor"], satellites: ["0.3.0", "minor"] },
      { core: [/### ⚠ BREAKING CHANGES\n\n\* \*\*a:\*\* change a thing/], satellitesNot: [/BREAKING/] }],
    "a satellite's package metadata": [(repo) => repo.commit("build(pack): package metadata", { "src/Sat.B/Sat.B.props": "<Project />\n" }),
      { core: null, satellites: ["0.2.2", "patch"] }],
    "a satellite's package README": [(repo) => repo.commit("docs(readme): its package README", { "src/Sat.B/README.md": "# Sat.B\n" }),
      { core: null, satellites: ["0.2.2", "patch"] }],
    // semantic-release skips [skip release] commits in the analysis, and the notes still list a train's own.
    "a satellite fix beside commits the analysis skips": [(repo) => {
      repo.commit("fix(b): a satellite fix", B);
      repo.commit("fix(b): a skipped satellite fix [skip release]", { "src/Sat.B/C.cs": "class C {}\n" });
      repo.commit("fix(a): a skipped core fix [skip release]", A);
      repo.commit("docs: a page [release skip]", { "docs/page.md": "# page\n" });
    }, { core: null, satellites: ["0.2.2", "patch"] }, { satellites: [/a satellite fix/, /a skipped satellite fix \[skip release\]/] }],
    "only a skipped satellite fix": [(repo) => repo.commit("fix(b): a skipped fix [skip release]", B),
      { core: null, satellites: null }],
    // A commit no train owns ships in no package, so its breaking marks are no break of the core's release; made plain,
    // a reverted one still pairs with its revert, which the notes leave out as they do any reverted commit.
    "a core fix beside breaking commits no train owns": [(repo) => {
      repo.commit("fix(a): a core fix", A);
      repo.commit("ci!: drop the old workflow\n\nBREAKING CHANGE: the x workflow no longer runs", { ".github/workflows/x.yml": "on: push\n" });
      repo.commit("docs!: rewrite the guide", { "docs/guide.md": "# guide\n" });
      repo.commit("ci(x)!: drop a(b)!: thing", { ".github/workflows/y.yml": "on: push\n" });
      repo.commit("docs!: a page soon reverted", { "docs/soon.md": "# soon\n" });
      repo.git("revert", "--no-edit", "HEAD");
    }, { core: ["0.2.2", "patch"], satellites: null },
    { core: [/a core fix/, /### Documentation\n\n\* rewrite the guide/],
      coreNot: [/BREAKING/, /drop the old workflow/, /no longer runs/, /soon reverted/] }],
  };
  for (const [name, [history, expected, wanted = {}]] of Object.entries(cases)) {
    await t.test(name, async () => {
      const repo = repository();
      try {
        history(repo);
        const plan = planOf(repo);
        for (const train of ["core", "satellites"]) {
          const want = expected[train];
          assert.deepEqual(plan[train].type ? [plan[train].next, plan[train].type] : null, want, `the plan of ${train}`);
          const outcome = await release(repo, train, envOf(repo, plan, train));
          if (!want) {
            nothing(outcome);
            continue;
          }
          const notes = released(outcome, ...want);
          for (const pattern of wanted[train] ?? []) assert.match(notes, pattern);
          for (const pattern of wanted[`${train}Not`] ?? []) assert.doesNotMatch(notes, pattern);
          if (train === "satellites") {
            assert.match(notes, new RegExp(`Packed against AdCodicem\\.ValueObjects ${plan.satellites.coreVersion.replaceAll(".", "\\.")}\\.`));
            const moved = /The core's line moved from 0\.2 to 0\.3 since this train's last release, which was packed against 0\.2\.1\./;
            if (plan.satellites.lineMoved) assert.match(notes, moved);
            else assert.doesNotMatch(notes, moved);
          }
        }
      } finally {
        repo.remove();
      }
    });
  }
});

test("a range longer than an environment variable can carry is planned and released", async () => {
  const repo = repository();
  try {
    // 3,300 commits no train owns, written at once by fast-import: their hashes alone pass Linux's 128 KiB cap on one
    // variable, which every process semantic-release starts would inherit.
    const count = 3300;
    let stream = "";
    for (let index = 0; index < count; index++) {
      const message = `docs: page ${index}\n`;
      const text = `${index}\n`;
      stream += `commit refs/heads/main\ncommitter Test <test@example.invalid> ${1700000000 + index} +0000\n` +
        `data ${Buffer.byteLength(message)}\n${message}${index === 0 ? "from refs/heads/main^0\n" : ""}` +
        `M 644 inline docs/page.md\ndata ${Buffer.byteLength(text)}\n${text}\n`;
    }
    execFileSync("git", ["fast-import", "--quiet"], { cwd: repo.root, input: stream, env: BASE });
    repo.git("reset", "--quiet", "--hard", "main");
    repo.commit("fix(a): a core fix", { "src/Core.A/A.cs": "class A {}\n" });
    const plan = planOf(repo);
    assert.equal(plan.core.range.split(" ").length, count + 1);
    assert.ok(plan.core.range.length > 131072);
    released(await release(repo, "core", envOf(repo, plan, "core")), "0.2.2", "patch");
  } finally {
    repo.remove();
  }
});

test("a merge's commits are walked as the plan walked them, and a commit either did not see fails", async () => {
  const repo = repository();
  try {
    repo.git("checkout", "--quiet", "-b", "side");
    repo.commit("fix(b): on a branch", { "src/Sat.B/B.cs": "class B {}\n" });
    repo.git("checkout", "--quiet", "main");
    repo.commit("docs: on main", { "docs/page.md": "# page\n" });
    repo.git("merge", "--quiet", "--no-ff", "--no-edit", "side");
    const plan = planOf(repo);
    assert.equal(plan.satellites.range.split(" ").length, 3);
    released(await release(repo, "satellites", envOf(repo, plan, "satellites")), "0.2.2", "patch");

    const [dropped, ...rest] = plan.satellites.range.split(" ");
    const short = { ...plan, satellites: { ...plan.satellites, range: rest.join(" ") } };
    refused(await release(repo, "satellites", envOf(repo, short, "satellites")),
      new RegExp(`semantic-release sees ${dropped.slice(0, 7)} .* since satellites-v0\\.2\\.1, which the satellites train's plan did not walk`));
    const long = { ...plan, satellites: { ...plan.satellites, range: `${plan.satellites.range} ${repo.base}` } };
    refused(await release(repo, "satellites", envOf(repo, long, "satellites")),
      new RegExp(`The satellites train's plan walked ${repo.base.slice(0, 7)} build: the trains, which semantic-release does not see`));
  } finally {
    repo.remove();
  }
});

test("semantic-release lists every commit when two reads of git's output split a record's separator", async () => {
  // git-log-parser cuts git log's output into commits with split2, which package.json overrides: split2 1.0.0 split
  // each read alone, so a separator two reads cut joined two commits into one, and semantic-release lost the second
  // (release run 12). The git alone on PATH writes the real one's output in two writes, cut inside the first
  // separator. Alone, so that a fake that cannot run (an interpreter path with a space, a temporary folder mounted
  // noexec) fails the test rather than leaving the real git to answer in one read; getBuiltinModule, so that it runs
  // as CommonJS or as a module, whatever package.json sits above the temporary folder.
  const repo = repository();
  const bin = mkdtempSync(join(tmpdir(), "release-train-git-"));
  try {
    repo.commit("fix(a): a core fix", { "src/Core.A/A.cs": "class A {}\n" });
    repo.commit("fix(b): a satellite fix", { "src/Sat.B/B.cs": "class B {}\n" });
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
    const commits = await getCommits("v0.2.1", "HEAD",
      { cwd: repo.root, env: { ...BASE, PATH: bin, REAL_PATH: BASE.PATH } });
    assert.deepEqual(commits.map(({ hash }) => hash), repo.git("rev-list", "v0.2.1..HEAD").split("\n"));
    assert.ok(commits.every(({ committerDate }) => !Number.isNaN(committerDate.getTime())));
  } finally {
    rmSync(bin, { recursive: true, force: true });
    repo.remove();
  }
});

test("a release refuses a version other than the one packed, a plan it cannot read, and a changelog out of shape", async () => {
  const repo = repository();
  try {
    repo.commit("fix(a): a core fix", { "src/Core.A/A.cs": "class A {}\n" });
    repo.commit("fix(b): a satellite fix", { "src/Sat.B/B.cs": "class B {}\n" });
    const plan = planOf(repo);
    refused(await release(repo, "core", envOf(repo, plan, "core", { packed: "0.2.3" })),
      /semantic-release computed 0\.2\.2 for the core train, but the pack job packed '0\.2\.3'/);
    refused(await release(repo, "core", envOf(repo, plan, "core", { packed: "" })),
      /semantic-release computed 0\.2\.2 for the core train, but the pack job packed ''/);
    refused(await release(repo, "satellites", envOf(repo, plan, "satellites"), { tagFormat: "v${version}" }),
      /The satellites train's tags are satellites-v\$\{version\}, not v\$\{version\}/);
    for (const name of ["RELEASE_TRAIN", "RELEASE_TRAIN_PLAN", "RELEASE_RUN_COMMITS", "PACKED_VERSION"]) {
      const { [name]: _, ...unset } = envOf(repo, plan, "core");
      refused(await release(repo, "core", unset), new RegExp(`${name} is not set`));
    }
    refused(await release(repo, "planets", { ...envOf(repo, plan, "core"), RELEASE_TRAIN: "planets" }, { tagFormat: "planets-v${version}" }),
      /planets is no train of src\/release-trains\.json/);
    // The plan as the file carries it: unreadable, without the train's row, a list or a version missing from it.
    refused(await release(repo, "core", { ...envOf(repo, plan, "core"), RELEASE_TRAIN_PLAN: join(repo.dir, "none.json") }),
      /The plan, .*none\.json, cannot be read/);
    refused(await release(repo, "satellites", envOf(repo, { core: plan.core }, "satellites")),
      /The plan, .*, has no row for the satellites train/);
    const without = (train, edit) => ({ ...plan, [train]: edit({ ...plan[train] }) });
    refused(await release(repo, "core", envOf(repo, without("core", ({ unowned, ...row }) => row), "core")),
      /The core train's plan has no unowned list/);
    for (const name of ["commits", "range", "notes"]) {
      refused(await release(repo, "satellites", envOf(repo, without("satellites", (row) => ({ ...row, [name]: undefined })), "satellites")),
        new RegExp(`The satellites train's plan has no ${name} list`));
    }
    for (const name of ["coreVersion", "previousCoreVersion"]) {
      refused(await release(repo, "satellites", envOf(repo, without("satellites", (row) => ({ ...row, [name]: "" })), "satellites")),
        new RegExp(`The satellites train's plan's ${name} is not a stable version: ''`));
    }
    refused(await release(repo, "core", envOf(repo, without("core", (row) => ({ ...row, tag: "v0.2" })), "core")),
      /The core train's plan names no last release of the train: 'v0\.2'/);
    // A plan counting from another release than semantic-release does: a release cut since, or a tag moved.
    refused(await release(repo, "core", envOf(repo, without("core", (row) => ({ ...row, tag: "v0.2.0", last: "0.2.0" })), "core")),
      /semantic-release counts the core train from v0\.2\.1, the plan from v0\.2\.0/);
    rmSync(join(repo.root, "CHANGELOG.satellites.md"));
    refused(await release(repo, "satellites", envOf(repo, plan, "satellites")),
      /CHANGELOG\.satellites\.md, the satellites train's changelog, cannot be read/);
    repo.write("CHANGELOG.satellites.md", `## [9.9.9]\n\n${read(join(root, "CHANGELOG.satellites.md"))}`);
    refused(await release(repo, "satellites", envOf(repo, plan, "satellites")),
      /CHANGELOG\.satellites\.md must open with its "# " title and hold the line/);
    repo.write("CHANGELOG.satellites.md", read(join(root, "CHANGELOG.satellites.md")).replace(MARKER, `## [9.9.9]\n\n${MARKER}`));
    refused(await release(repo, "satellites", envOf(repo, plan, "satellites")),
      /CHANGELOG\.satellites\.md has a release above <!-- Releases are prepended below this line\. -->/);
  } finally {
    repo.remove();
  }
});

test("a real run releases the core, then the satellites past the core's release commit", async () => {
  const repo = repository();
  try {
    const coreFix = repo.commit("fix(a): a core fix", { "src/Core.A/A.cs": "class A {}\n" });
    const satelliteFix = repo.commit("fix(b): a satellite fix", { "src/Sat.B/B.cs": "class B {}\n" });
    const page = repo.commit("docs: a page no train owns", { "docs/page.md": "# page\n" });
    const dispatched = repo.git("rev-parse", "HEAD");
    const plan = planOf(repo);
    assert.equal(plan.core.unowned, page);

    released(await release(repo, "core", envOf(repo, plan, "core"), { dryRun: false }), "0.2.2", "patch");
    const first = repo.git("rev-parse", "HEAD");
    assert.equal(repo.git("rev-parse", `${first}^`), dispatched);
    assert.equal(repo.git("log", "-1", "--format=%s"), "chore(release): v0.2.2 [skip ci]");
    assert.equal(repo.git("rev-parse", "v0.2.2^{commit}"), first);
    const core = read(join(repo.root, "CHANGELOG.md"));
    const title = core.slice(0, core.indexOf(MARKER) + MARKER.length);
    assert.ok(core.startsWith(`${title}\n\n## [0.2.2]`), core.slice(0, 900));
    const entryOf = (text) => text.slice(text.indexOf("## [0.2.2]"), text.indexOf("## [0.2.1]") > 0 ? text.indexOf("## [0.2.1]") : undefined);
    assert.match(entryOf(core), new RegExp(coreFix.slice(0, 7)));
    assert.match(entryOf(core), new RegExp(page.slice(0, 7)), "the core's notes carry the commits no train owns");
    assert.doesNotMatch(entryOf(core), new RegExp(satelliteFix.slice(0, 7)));

    // The core's release commit is no commit of the satellites' plan: named, it is left out; not named, it fails.
    refused(await release(repo, "satellites", envOf(repo, plan, "satellites")),
      new RegExp(`semantic-release sees ${first.slice(0, 7)} chore\\(release\\): v0\\.2\\.2 \\[skip ci\\]`));
    released(await release(repo, "satellites", envOf(repo, plan, "satellites", { run: first }), { dryRun: false }), "0.2.2", "patch");
    const second = repo.git("rev-parse", "HEAD");
    assert.equal(repo.git("rev-parse", `${second}^`), first);
    assert.equal(repo.git("log", "-1", "--format=%s"), "chore(release): satellites-v0.2.2 [skip ci]");
    assert.equal(repo.git("rev-parse", "satellites-v0.2.2^{commit}"), second);
    assert.equal(repo.git("diff", "--name-only", first, second), "CHANGELOG.satellites.md");
    const satellites = read(join(repo.root, "CHANGELOG.satellites.md"));
    const satellitesTitle = satellites.slice(0, satellites.indexOf(MARKER) + MARKER.length);
    assert.ok(satellites.startsWith(`${satellitesTitle}\n\n## [0.2.2]`), satellites);
    assert.match(satellites, new RegExp(satelliteFix.slice(0, 7)));
    assert.doesNotMatch(satellites, new RegExp(`${coreFix.slice(0, 7)}|${page.slice(0, 7)}`));
    assert.match(satellites, /Packed against AdCodicem\.ValueObjects 0\.2\.2\.\n/);
    for (const tag of ["v0.2.2", "satellites-v0.2.2"]) {
      assert.equal(execFileSync("git", ["rev-parse", `${tag}^{commit}`], { cwd: repo.remote, encoding: "utf8" }).trim(),
        repo.git("rev-parse", `${tag}^{commit}`), `${tag} on the remote`);
    }
    assert.equal(execFileSync("git", ["rev-parse", "main"], { cwd: repo.remote, encoding: "utf8" }).trim(), second);
  } finally {
    repo.remove();
  }
});
