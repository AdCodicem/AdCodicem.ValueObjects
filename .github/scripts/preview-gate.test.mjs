// Tests of preview-gate.sh: node --test .github/scripts/preview-gate.test.mjs (ci.yml's job "release tooling").
//
// The gate runs as preview.yml and release.yml run it, against a throwaway repository and a flat container that
// node:http serves on 127.0.0.1 (curl answers 000 for a file:// URL), so each decision is replayed against a recorded
// state of nuget.org. Its one seam, PACKAGE_IDS_SCRIPT, replaces package-ids.sh, which needs dotnet, by a script
// reading the package IDs each commit holds in src/ids.txt. The outputs below are the gate's decisions as they are
// today (docs/adr/0012-version-the-packages-in-two-trains-independently-of-the-frameworks.md, phase 2): the gate
// that decides per train has to reproduce them for trains cut at one version. A change of Directory.Packages.props'
// <PackageVersion> entries is not replayed: the gate restores the solution filter to read the restore graph.
import { test } from "node:test";
import assert from "node:assert/strict";
import { execFile, execFileSync } from "node:child_process";
import { cpSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { createServer } from "node:http";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const scripts = dirname(fileURLToPath(import.meta.url));
const releaserc = readFileSync(join(scripts, "..", "..", ".releaserc.json"), "utf8");

const LIST = JSON.stringify({ core: { breaking: "minor", projects: ["Pkg.A"] }, satellites: { breaking: "minor", projects: "*" } });

/** What each commit holds, besides what the commit changes: the files the gate's git archive names must exist. */
const BASE_FILES = {
  "Directory.Build.props": "<Project />\n",
  "Directory.Packages.props": "<Project>\n  <ItemGroup>\n    <PackageVersion Include=\"Lib\" Version=\"1.0.0\" />\n  </ItemGroup>\n</Project>\n",
  "global.json": "{}\n",
  ".releaserc.json": releaserc,
  "src/release-trains.json": `${LIST}\n`,
  "src/ids.txt": "Pkg.A core\nPkg.B satellites\n",
  "src/Pkg.A/A.cs": "class A {}\n",
  "src/Pkg.B/B.cs": "class B {}\n",
};

/**
 * A throwaway repository: the gate and what it reads copied into .github/scripts (untracked, so that no commit
 * changes them), a stub for package-ids.sh, which the gate runs through bash (a temporary folder may forbid running
 * a file), and commits named by label. The repository's own configuration keeps the machine's out: no signing, no
 * hook.
 */
function repository() {
  const root = mkdtempSync(join(tmpdir(), "preview-gate-"));
  try {
    return populate(root);
  } catch (error) {
    rmSync(root, { recursive: true, force: true });
    throw error;
  }
}

function populate(root) {
  const run = (...args) => execFileSync("git", args, { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] });
  run("init", "--quiet", "--initial-branch=main");
  for (const [key, value] of [["user.email", "test@example.invalid"], ["user.name", "Test"], ["commit.gpgsign", "false"],
    ["tag.gpgSign", "false"], ["core.hooksPath", "/dev/null"], ["core.autocrlf", "false"]]) {
    run("config", key, value);
  }
  cpSync(scripts, join(root, ".github", "scripts"), { recursive: true });
  writeFileSync(join(root, ".gitignore"), ".github/\nids.sh\n");
  const ids = join(root, "ids.sh");
  writeFileSync(ids, [
    "#!/usr/bin/env bash",
    "# package-ids.sh's interface over src/ids.txt: '<id> <train>' per package.",
    "set -euo pipefail",
    "trains=false",
    'if [[ "${1:-}" == --trains ]]; then trains=true; shift; fi',
    'cd "${1:-.}"',
    "if $trains; then cat src/ids.txt; else cut -d' ' -f1 src/ids.txt; fi | LC_ALL=C sort -u",
    "",
  ].join("\n"));
  const commits = new Map();
  const write = (files) => {
    for (const [path, text] of Object.entries(files)) {
      if (text === null) {
        rmSync(join(root, path), { force: true });
        continue;
      }
      mkdirSync(dirname(join(root, path)), { recursive: true });
      writeFileSync(join(root, path), text);
    }
  };
  const commit = (label, files = {}, message = `chore: ${label}`) => {
    write(files);
    run("add", "--all");
    run("commit", "--quiet", "--allow-empty", "--message", message);
    const hash = run("rev-parse", "HEAD").trim();
    commits.set(label, hash);
    return hash;
  };
  write(BASE_FILES);
  return {
    root,
    ids,
    commits,
    run,
    commit,
    tag: (name, label) => run("tag", name, commits.get(label)),
    checkout: (label) => run("checkout", "--quiet", commits.get(label)),
    branch: (name, label) => run("checkout", "--quiet", "-b", name, commits.get(label)),
    remove: () => rmSync(root, { recursive: true, force: true }),
  };
}

/**
 * A flat container serving a recorded state of nuget.org, { "<id>": { "<version>": "<commit>" } }: each version's
 * index entry and its nuspec, at the lower-cased paths nuget.org serves (/<id>/index.json, /<id>/<version>/<id>.nuspec),
 * the nuspec naming the commit the version was packed from as SourceLink writes it. Any other path is a 404, as for a
 * package nuget.org never had or a path in another case.
 */
async function feed(state) {
  const packages = new Map(Object.entries(state).map(([id, versions]) => [id.toLowerCase(), versions]));
  const server = createServer((request, response) => {
    const [, id, index, version, file] = /^\/([^/]+)\/(?:(index\.json)|([^/]+)\/([^/]+\.nuspec))$/.exec(request.url) ?? [];
    const versions = id && packages.get(id);
    const answer = (status, body) => {
      response.writeHead(status, { "content-type": "application/json" });
      response.end(body);
    };
    if (!versions) return answer(404, "");
    if (index) return answer(200, JSON.stringify({ versions: Object.keys(versions) }));
    if (file !== `${id}.nuspec`) return answer(404, "");
    const commit = Object.entries(versions).find(([key]) => key.toLowerCase() === version)?.[1];
    if (!commit) return answer(404, "");
    return answer(200, `<?xml version="1.0"?><package><metadata><id>${id}</id>` +
      `<repository type="git" url="https://example.invalid/repo.git" commit="${commit}" /></metadata></package>`);
  });
  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  return {
    url: `http://127.0.0.1:${server.address().port}`,
    close: () => new Promise((resolve) => server.close(resolve)),
  };
}

const RUN_ENVIRONMENT = ["GITHUB_OUTPUT", "GITHUB_STEP_SUMMARY", "NEXT_VERSION", "RELEASE_TYPE", "GATE_REF", "PACKAGE_IDS",
  "EXPECT_BASE", "EXPECT_VERSION", "PACKAGE_IDS_SCRIPT", "NUGET_FLAT_CONTAINER"];
// curl reads no_proxy before NO_PROXY: both are replaced, so that no proxy of the machine's reaches the local feed.
const PROXIES = ["http_proxy", "https_proxy", "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "all_proxy", "no_proxy", "NO_PROXY"];

/**
 * Runs the gate of a repository against a feed: { status, outputs, errors, warnings, notes, summary }, every commit
 * hash written as its label ({C1}, or {C1:7} for its short form), so that the outputs read as recorded.
 */
async function gate(repo, state, { args = [], env = {} } = {}) {
  const served = await feed(Object.fromEntries(Object.entries(state).map(([id, versions]) =>
    [id, Object.fromEntries(Object.entries(versions).map(([version, label]) => [version, repo.commits.get(label) ?? label]))])));
  const work = mkdtempSync(join(tmpdir(), "preview-gate-run-"));
  try {
    const environment = { ...process.env };
    for (const name of [...RUN_ENVIRONMENT, ...PROXIES]) delete environment[name];
    Object.assign(environment, {
      NO_PROXY: "127.0.0.1,localhost",
      no_proxy: "127.0.0.1,localhost",
      NUGET_FLAT_CONTAINER: served.url,
      PACKAGE_IDS_SCRIPT: repo.ids,
      GITHUB_OUTPUT: join(work, "output"),
      GITHUB_STEP_SUMMARY: join(work, "summary"),
      ...env,
    });
    writeFileSync(environment.GITHUB_OUTPUT, "");
    writeFileSync(environment.GITHUB_STEP_SUMMARY, "");
    const { status, stderr } = await new Promise((resolve) => {
      execFile("bash", [join(repo.root, ".github", "scripts", "preview-gate.sh"), ...args],
        { cwd: repo.root, env: environment, encoding: "utf8" },
        (error, stdout, stderr) => resolve({ status: error ? error.code : 0, stderr }));
    });
    const labelled = (text) => {
      for (const [label, hash] of repo.commits) text = text.replaceAll(hash, `{${label}}`).replaceAll(hash.slice(0, 7), `{${label}:7}`);
      return text;
    };
    const lines = labelled(stderr).split("\n").filter(Boolean);
    const outputs = Object.fromEntries(labelled(readFileSync(environment.GITHUB_OUTPUT, "utf8")).split("\n").filter(Boolean)
      .map((line) => [line.slice(0, line.indexOf("=")), line.slice(line.indexOf("=") + 1)]));
    return {
      status,
      outputs,
      errors: lines.filter((line) => line.startsWith("::error::")).map((line) => line.slice("::error::".length)),
      warnings: lines.filter((line) => line.startsWith("::warning")),
      notes: lines.filter((line) => !line.startsWith("::")),
      summary: labelled(readFileSync(environment.GITHUB_STEP_SUMMARY, "utf8")),
    };
  } finally {
    await served.close();
    rmSync(work, { recursive: true, force: true });
  }
}

// --- the recorded decisions ---------------------------------------------------------------------------------------------

/** Both packages at one version, packed from the commit of a label. */
const both = (version, label) => ({ "Pkg.A": { [version]: label }, "Pkg.B": { [version]: label } });
/** The union of recorded states. */
const and = (...states) => {
  const merged = {};
  for (const state of states) {
    for (const [id, versions] of Object.entries(state)) merged[id] = { ...merged[id], ...versions };
  }
  return merged;
};

/** A decision as the gate writes it: its outputs, the reason on stderr, and the job summary listing what changed. */
function assertDecision(result, { mode, ref, version = "", base, baseCommit, docsRef = ref, docsVersion, pending = "", reason,
  changed = [], warnings = [] }) {
  const summary = [`### Preview: ${mode}`, "", reason];
  if (changed.length > 0) summary.push("", `Package inputs changed since ${base}:`, "", ...changed.map((path) => `- \`${path}\``));
  assert.deepEqual(result, {
    status: 0,
    outputs: {
      mode, ref, version, "base-version": base, "base-commit": baseCommit, "last-stable": "0.2.1", "docs-ref": docsRef,
      "docs-version": docsVersion, pending, reason,
    },
    errors: [],
    warnings,
    notes: [reason],
    summary: `${summary.join("\n")}\n`,
  });
}

/** A refusal: one error, and nothing written for the workflow. */
function assertRefusal(result, error) {
  assert.deepEqual(result, { status: 1, outputs: {}, errors: [error], warnings: [], notes: [], summary: "" });
}

/** C0, tagged v0.2.1, holding Pkg.A (core) and Pkg.B (satellites). */
function released() {
  const repo = repository();
  try {
    repo.commit("C0");
    repo.tag("v0.2.1", "C0");
    return repo;
  } catch (error) {
    repo.remove();
    throw error;
  }
}

/**
 * A feature whose preview shipped, F (0.3.0-preview.1), reverted before the release C0 (v0.2.1): the preview stays on
 * nuget.org above every later version until a higher one ships.
 */
function revertedFeature() {
  const repo = repository();
  try {
    repo.commit("P0");
    repo.tag("v0.2.0", "P0");
    repo.commit("F", { "src/Pkg.A/A.cs": "class A { int f; }\n" }, "feat: f");
    repo.commit("C0", { "src/Pkg.A/A.cs": "class A {}\n" }, "revert: feat: f\n\nThis reverts commit 0000000.");
    repo.tag("v0.2.1", "C0");
    return repo;
  } catch (error) {
    repo.remove();
    throw error;
  }
}

const PATCH = { NEXT_VERSION: "0.2.2-preview.1", RELEASE_TYPE: "patch" };

test("a package input changed since the base publishes NEXT_VERSION, a package nuget.org never had included", async () => {
  const repo = released();
  try {
    repo.commit("C1", { "src/Pkg.A/A.cs": "class A { }\n", "src/ids.txt": "Pkg.A core\nPkg.B satellites\nPkg.C satellites\n",
      "src/Pkg.C/C.cs": "class C {}\n" }, "feat: c");
    const reason = "Package inputs changed since 0.2.1 ({C0:7}): src/Pkg.A/A.cs src/Pkg.C/C.cs src/ids.txt";
    assertDecision(await gate(repo, both("0.2.1", "C0"), { env: PATCH }), {
      mode: "publish", ref: "{C1}", version: "0.2.2-preview.1", base: "0.2.1", baseCommit: "{C0}",
      docsVersion: "0.2.2-preview.1", reason, changed: ["src/Pkg.A/A.cs", "src/Pkg.C/C.cs", "src/ids.txt"],
    });
    assertDecision(await gate(repo, both("0.2.1", "C0"), { env: { NEXT_VERSION: "0.3.0-preview.1", RELEASE_TYPE: "minor" } }), {
      mode: "publish", ref: "{C1}", version: "0.3.0-preview.1", base: "0.2.1", baseCommit: "{C0}",
      docsVersion: "0.3.0-preview.1", reason, changed: ["src/Pkg.A/A.cs", "src/Pkg.C/C.cs", "src/ids.txt"],
    });
  } finally {
    repo.remove();
  }
});

test("a change of Directory.Packages.props outside its PackageVersion entries publishes", async () => {
  const repo = released();
  try {
    repo.commit("C1", { "Directory.Packages.props": BASE_FILES["Directory.Packages.props"].replace("<Project>\n",
      "<Project>\n  <PropertyGroup><CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled></PropertyGroup>\n") },
    "build: pin");
    assertDecision(await gate(repo, both("0.2.1", "C0"), { env: { NEXT_VERSION: "0.2.2-preview.1", RELEASE_TYPE: "" } }), {
      mode: "publish", ref: "{C1}", version: "0.2.2-preview.1", base: "0.2.1", baseCommit: "{C0}", docsVersion: "0.2.2-preview.1",
      reason: "Package inputs changed since 0.2.1 ({C0:7}): Directory.Packages.props (outside the PackageVersion entries)",
      changed: ["Directory.Packages.props (outside the PackageVersion entries)"],
    });
  } finally {
    repo.remove();
  }
});

test("nothing a package is built from changed: none, at the stable release and after it", async () => {
  const repo = released();
  try {
    const none = { mode: "none", base: "0.2.1", baseCommit: "{C0}", docsVersion: "0.2.1",
      reason: "No package input changed since 0.2.1 ({C0:7})." };
    assertDecision(await gate(repo, both("0.2.1", "C0")), { ...none, ref: "{C0}" });
    repo.commit("C1", { "docs/guide.md": "# Guide\n", "src/Pkg.A/CLAUDE.md": "# A\n" }, "docs: a guide");
    assertDecision(await gate(repo, both("0.2.1", "C0")), { ...none, ref: "{C1}" });
  } finally {
    repo.remove();
  }
});

test("a version some packages of the base commit lack is repaired from that commit, pending what HEAD adds since", async () => {
  const repo = released();
  try {
    repo.commit("C1", { "src/Pkg.A/A.cs": "class A { }\n" }, "fix: a");
    const partial = and(both("0.2.1", "C0"), { "Pkg.A": { "0.2.2-preview.1": "C1" } });
    const repair = { mode: "repair", ref: "{C1}", version: "0.2.2-preview.1", base: "0.2.2-preview.1", baseCommit: "{C1}",
      docsVersion: "0.2.2-preview.1" };
    const reason = "0.2.2-preview.1 is missing from nuget.org for Pkg.B: completing it from {C1:7}.";
    assertDecision(await gate(repo, partial), { ...repair, pending: "false", reason });
    // A commit since that changes no package: nothing pending, and the site is built from HEAD.
    repo.commit("D1", { "docs/guide.md": "# Guide\n" }, "docs: a guide");
    assertDecision(await gate(repo, partial), { ...repair, docsRef: "{D1}", pending: "false", reason });
    repo.commit("C2", { "src/Pkg.B/B.cs": "class B { }\n" }, "fix: b");
    assertDecision(await gate(repo, partial), {
      ...repair, pending: "true", changed: ["src/Pkg.B/B.cs"],
      reason: "0.2.2-preview.1 is missing from nuget.org for Pkg.B: completing it from {C1:7}. HEAD has further package changes, for the next run.",
    });
  } finally {
    repo.remove();
  }
});

test("a publish after a preview takes the preview's commit as its base, and the height since the stable tag", async () => {
  const repo = released();
  try {
    repo.commit("C1", { "src/Pkg.A/A.cs": "class A { }\n" }, "fix: a");
    repo.commit("C2", { "src/Pkg.B/B.cs": "class B { }\n" }, "fix: b");
    const state = and(both("0.2.1", "C0"), both("0.2.2-preview.1", "C1"));
    assertDecision(await gate(repo, state, { env: { NEXT_VERSION: "0.2.2-preview.2", RELEASE_TYPE: "patch" } }), {
      mode: "publish", ref: "{C2}", version: "0.2.2-preview.2", base: "0.2.2-preview.1", baseCommit: "{C1}",
      docsVersion: "0.2.2-preview.2", reason: "Package inputs changed since 0.2.2-preview.1 ({C1:7}): src/Pkg.B/B.cs",
      changed: ["src/Pkg.B/B.cs"],
    });
    assertRefusal(await gate(repo, state, { env: PATCH }),
      "NEXT_VERSION 0.2.2-preview.1 is not 0.2.2-preview.2, the patch after 0.2.1 at this commit's height.");
  } finally {
    repo.remove();
  }
});

test("a reverted feature's preview, above every later version, stays out of the base, warns, and can hold the version", async () => {
  const repo = revertedFeature();
  try {
    const state = and(both("0.3.0-preview.1", "F"), both("0.2.1", "C0"));
    repo.commit("C1", { "src/Pkg.A/A.cs": "class A { }\n" }, "fix: a");
    assertDecision(await gate(repo, state, { env: PATCH }), {
      mode: "publish", ref: "{C1}", version: "0.2.2-preview.1", base: "0.2.1", baseCommit: "{C0}",
      docsVersion: "0.2.2-preview.1", reason: "Package inputs changed since 0.2.1 ({C0:7}): src/Pkg.A/A.cs",
      changed: ["src/Pkg.A/A.cs"],
      warnings: ["::warning title=Below an earlier version::0.2.2-preview.1 sorts below 0.3.0-preview.1, which nuget.org already has: NuGet offers 0.3.0-preview.1 as the latest prerelease until a higher version ships."],
    });
    assertRefusal(await gate(repo, state, { env: { NEXT_VERSION: "0.3.0-preview.1", RELEASE_TYPE: "minor" } }),
      "0.3.0-preview.1 is already on nuget.org (Pkg.A), packed from another commit. The preview number counts commits since the last stable tag, so a preview left above a lower release (a feature reverted after its preview shipped) can hold it: the next commit on main moves the number.");
  } finally {
    repo.remove();
  }
});

test("a publish refuses a NEXT_VERSION it does not recompute, and a release type it cannot trust", async () => {
  const repo = released();
  try {
    repo.commit("C1", { "src/Pkg.A/A.cs": "class A { }\n" }, "fix: a");
    const state = both("0.2.1", "C0");
    assertRefusal(await gate(repo, state, { env: { NEXT_VERSION: "0.3.0-preview.1", RELEASE_TYPE: "patch" } }),
      "NEXT_VERSION 0.3.0-preview.1 is not 0.2.2-preview.1, the patch after 0.2.1 at this commit's height.");
    assertRefusal(await gate(repo, state, { env: { NEXT_VERSION: "0.2.2", RELEASE_TYPE: "patch" } }),
      "NEXT_VERSION '0.2.2' is not of the X.Y.Z-preview.N shape that next-version.mjs computes.");
    assertRefusal(await gate(repo, state, { env: { NEXT_VERSION: "0.2.2-preview.1", RELEASE_TYPE: "huge" } }),
      "RELEASE_TYPE 'huge' is not major, minor, patch or empty.");
    assertRefusal(await gate(repo, state, { env: { NEXT_VERSION: "1.0.0-preview.1", RELEASE_TYPE: "major" } }),
      "RELEASE_TYPE is major, which the commit analyzer cannot answer while .releaserc.json rates a breaking change a minor.");
  } finally {
    repo.remove();
  }
});

test("a base nuget.org cannot give is refused: off HEAD's history, nothing since the stable tag, no stable tag, a release half pushed", async () => {
  const repo = released();
  try {
    repo.branch("side", "C0");
    repo.commit("S1", { "src/Pkg.A/A.cs": "class A { int x; }\n" }, "feat: on a side branch");
    repo.run("checkout", "--quiet", "main");
    repo.commit("C1", { "src/Pkg.A/A.cs": "class A { }\n" }, "fix: a");
    assertRefusal(await gate(repo, and(both("0.2.1", "C0"), both("0.2.2-preview.1", "S1")), { env: PATCH }),
      "Pkg.A 0.2.2-preview.1 was packed from {S1}, which is not an ancestor of {C1:7}: a newer commit was published since, or the history was rewritten.");
    assertRefusal(await gate(repo, both("0.2.0", "C0"), { env: PATCH }),
      "v0.2.1 is tagged, but nuget.org has no version of these packages from 0.2.1 on: the release's push failed, or nuget.org has not listed it yet. Complete the release, or wait, then dispatch again.");
    assertRefusal(await gate(repo, and(both("0.2.0", "C0"), { "Pkg.A": { "0.2.1": "C0" } })),
      "0.2.1 is missing from nuget.org for Pkg.B, but it is not a version preview.yml publishes (a stable release, or a preview from before it): complete it by hand.");
  } finally {
    repo.remove();
  }
  const untagged = repository();
  try {
    untagged.commit("C0");
    assertRefusal(await gate(untagged, both("0.2.1", "C0")), "No stable tag v<major>.<minor>.<patch> is merged into HEAD.");
  } finally {
    untagged.remove();
  }
});

test("--recheck goes on while nuget.org gives the base the plan saw, or this run's own version, and stops otherwise", async () => {
  const repo = released();
  try {
    repo.commit("C1", { "src/Pkg.A/A.cs": "class A { }\n" }, "fix: a");
    // preview.yml's publish job checks out .github/scripts alone and has no dotnet: the IDs come from PACKAGE_IDS, and
    // an IDs script that does not exist fails any run that would read it.
    const recheck = (ref, expectVersion, expectBase = "0.2.1") => ({ args: ["--recheck"], env: {
      GATE_REF: repo.commits.get(ref) ?? ref, PACKAGE_IDS: "Pkg.A Pkg.B", EXPECT_BASE: expectBase, EXPECT_VERSION: expectVersion,
      PACKAGE_IDS_SCRIPT: join(repo.root, "no-ids.sh") } });
    const went = (note) => ({ status: 0, outputs: {}, errors: [], warnings: [], notes: [note], summary: "" });
    assert.deepEqual(await gate(repo, both("0.2.1", "C0"), recheck("C1", "0.2.2-preview.1")),
      went("nuget.org still gives 0.2.1 as the base of {C1:7}, as when this run decided."));
    assert.deepEqual(await gate(repo, and(both("0.2.1", "C0"), { "Pkg.A": { "0.2.2-preview.1": "C1" } }), recheck("C1", "0.2.2-preview.1")),
      went("0.2.2-preview.1 is partly or wholly on nuget.org already, from {C1:7}: this run completes it."));
    repo.commit("C2", { "src/Pkg.B/B.cs": "class B { }\n" }, "fix: b");
    // A repair's publish job: GATE_REF the base commit it completes, HEAD the commit the run started on, after it.
    assert.deepEqual(await gate(repo, and(both("0.2.1", "C0"), { "Pkg.A": { "0.2.2-preview.1": "C1" } }),
      recheck("C1", "0.2.2-preview.1", "0.2.2-preview.1")),
    went("0.2.2-preview.1 is partly or wholly on nuget.org already, from {C1:7}: this run completes it."));
    assertRefusal(await gate(repo, and(both("0.2.1", "C0"), both("0.2.2-preview.1", "C1")), recheck("C2", "0.2.2-preview.2")),
      "nuget.org now gives 0.2.2-preview.1 ({C1:7}) as the base of {C2:7}, where this run decided from 0.2.1: something was published since. Dispatch preview.yml again rather than re-running this run.");
    assertRefusal(await gate(repo, and(both("0.2.1", "C0"), both("0.2.2-preview.2", "C1")), recheck("C2", "0.2.2-preview.2")),
      "0.2.2-preview.2 is on nuget.org, packed from {C1:7}, not from {C2:7}, which this run packed.");
    assertRefusal(await gate(repo, both("0.2.1", "C0"), recheck("0000000000000000000000000000000000000000", "0.2.2-preview.2")),
      "GATE_REF 0000000000000000000000000000000000000000 is not a commit of this clone.");
    repo.checkout("C1");
    assertRefusal(await gate(repo, both("0.2.1", "C0"), recheck("C2", "0.2.2-preview.2")),
      "GATE_REF {C2} is not HEAD or one of its ancestors.");
  } finally {
    repo.remove();
  }
});

test("--report warns of package inputs no version on nuget.org carries, and of a version some packages lack", async () => {
  const repo = released();
  try {
    const reported = (warnings, summary) => ({ status: 0, outputs: {}, errors: [], warnings, notes: [], summary });
    assert.deepEqual(await gate(repo, both("0.2.1", "C0"), { args: ["--report"] }),
      reported([], "Every package input of this release is already on nuget.org, in 0.2.1 (`{C0:7}`).\n"));
    // A stable release some packages lack is completed by hand, never by preview.yml, which refuses to.
    assert.deepEqual(await gate(repo, { "Pkg.A": { "0.2.1": "C0" } }, { args: ["--report"] }), reported(
      ["::warning title=Partly published::0.2.1 is missing from nuget.org for Pkg.B."],
      "### Partly published\n\n0.2.1, the version on nuget.org built from the nearest commit, `{C0:7}`, is missing for 1 of its packages, which therefore carry none of what changed up to that commit:\n\n- `Pkg.B`\n\nComplete that release by hand first (docs/maintaining.md).\n"));
    repo.commit("C1", { "src/Pkg.A/A.cs": "class A { }\n" }, "fix: a");
    assert.deepEqual(await gate(repo, both("0.2.1", "C0"), { args: ["--report"] }), reported(
      ["::warning title=Not previewed::This release ships package inputs that no preview carried, changed since 0.2.1: src/Pkg.A/A.cs"],
      "### Not previewed\n\nThis release ships package inputs that no preview carried. They changed since 0.2.1, the release on nuget.org built from the nearest commit, `{C0:7}`:\n\n- `src/Pkg.A/A.cs`\n\nDispatching preview.yml first publishes them as a preview.\n"));
    assert.deepEqual(await gate(repo, and(both("0.2.1", "C0"), { "Pkg.A": { "0.2.2-preview.1": "C1" } }), { args: ["--report"] }), reported(
      ["::warning title=Partly published::0.2.2-preview.1 is missing from nuget.org for Pkg.B."],
      "### Partly published\n\n0.2.2-preview.1, the version on nuget.org built from the nearest commit, `{C1:7}`, is missing for 1 of its packages, which therefore carry none of what changed up to that commit:\n\n- `Pkg.B`\n\nDispatching preview.yml first completes it.\n"));
    repo.commit("C2", { "src/Pkg.B/B.cs": "class B { }\n" }, "fix: b");
    assert.deepEqual(await gate(repo, and(both("0.2.1", "C0"), both("0.2.2-preview.1", "C1")), { args: ["--report"] }), reported(
      ["::warning title=Not previewed::This release ships package inputs that no preview carried, changed since 0.2.2-preview.1: src/Pkg.B/B.cs"],
      "### Not previewed\n\nThis release ships package inputs that no preview carried. They changed since 0.2.2-preview.1, the preview on nuget.org built from the nearest commit, `{C1:7}`:\n\n- `src/Pkg.B/B.cs`\n\nDispatching preview.yml first publishes them as a preview.\n"));
  } finally {
    repo.remove();
  }
});
