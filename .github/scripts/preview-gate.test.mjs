// Tests of preview-gate.sh: node --test .github/scripts/preview-gate.test.mjs (ci.yml's job "release tooling").
//
// The gate runs as preview.yml and release.yml run it, against a throwaway repository and a flat container that
// node:http serves on 127.0.0.1 (curl answers 000 for a file:// URL), so each decision is replayed against a recorded
// state of nuget.org. Its one seam, PACKAGE_IDS_SCRIPT, replaces package-ids.sh, which needs dotnet, by a script
// reading the package IDs each commit holds in src/ids.txt, each on the train the tree's src/release-trains.json gives
// it, as MSBuild reads it. The gate decides train by train
// (docs/adr/0012-version-the-packages-in-two-trains-independently-of-the-frameworks.md, phase 2). The decisions PR 6
// recorded for the gate of one version come first, both trains on one version and twin-tagged (v<version> and
// satellites-v<version> on one commit, as release.yml tags every release): each reproduces that gate's mode, ref,
// versions and bases, train by train. Then what deciding per train adds. Each train has two packages, so that a train
// can be partly published. A change of Directory.Packages.props' <PackageVersion> entries is not replayed: the gate
// restores the solution filter to read the restore graph.
import { test } from "node:test";
import assert from "node:assert/strict";
import { execFile, execFileSync } from "node:child_process";
import { cpSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { createServer } from "node:http";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const scripts = dirname(fileURLToPath(import.meta.url));

/** The trains, the core first, and the tag prefix release-trains.mjs gives each. */
const TRAINS = ["core", "satellites"];
const PREFIX = { core: "v", satellites: "satellites-v" };
/** The packages of each train at C0. */
const IDS = { core: ["Core.A", "Core.B"], satellites: ["Sat.A", "Sat.B"] };

/** src/release-trains.json: each train's breaking rule, and the projects the core names; the satellites take the rest. */
const list = ({ core = "minor", satellites = "minor", projects = IDS.core } = {}) =>
  `${JSON.stringify({ core: { breaking: core, projects }, satellites: { breaking: satellites, projects: "*" } })}\n`;
/** src/ids.txt: the packable projects of a commit, by package ID. */
const ids = (...more) => `${[...IDS.core, ...IDS.satellites, ...more].join("\n")}\n`;

/** What each commit holds, besides what the commit changes: the files the gate's git archive names must exist. */
const BASE_FILES = {
  "Directory.Build.props": "<Project />\n",
  "Directory.Packages.props": "<Project>\n  <ItemGroup>\n    <PackageVersion Include=\"Lib\" Version=\"1.0.0\" />\n  </ItemGroup>\n</Project>\n",
  "global.json": "{}\n",
  "src/release-trains.json": list(),
  "src/ids.txt": ids(),
  "src/Core.A/A.cs": "class A {}\n",
  "src/Core.B/B.cs": "class B {}\n",
  "src/Sat.A/A.cs": "class SA {}\n",
  "src/Sat.B/B.cs": "class SB {}\n",
};

/** The environment without git's own variables, which would point git at another repository than the test's. */
const withoutGit = () => Object.fromEntries(Object.entries(process.env).filter(([name]) => !name.startsWith("GIT_")));

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
  const run = (...args) =>
    execFileSync("git", args, { cwd: root, encoding: "utf8", env: withoutGit(), stdio: ["ignore", "pipe", "pipe"] });
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
    "# package-ids.sh's interface over src/ids.txt: one ID per line, or with --trains '<id> <train>', the train the tree's",
    "# own src/release-trains.json gives the project, as MSBuild reads it. The gate asks for the trains at HEAD only.",
    "set -euo pipefail",
    "trains=false",
    'if [[ "${1:-}" == --trains ]]; then trains=true; shift; fi',
    'scripts="$PWD/.github/scripts"',
    'cd "${1:-.}"',
    "# A tree holding src/ids.fail stands for one MSBuild cannot evaluate: a partial list, then a failure.",
    'if [[ -f src/ids.fail ]]; then head -n 1 src/ids.txt; exit 1; fi',
    "mapfile -t ids < <(LC_ALL=C sort -u src/ids.txt)",
    'if $trains; then node "$scripts/release-trains.mjs" --list src/release-trains.json trains-of "${ids[@]}"',
    'else printf "%s\\n" "${ids[@]}"; fi',
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
  // semantic-release's tags are lightweight; satellites-v0.2.1, the baseline placed by hand, is annotated.
  const tag = (name, label, annotated = false) =>
    run("tag", ...(annotated ? ["--annotate", "--message", name] : []), name, commits.get(label));
  write(BASE_FILES);
  return {
    root,
    ids,
    commits,
    run,
    commit,
    tag,
    /** v<version> and satellites-v<version> on one commit, as release.yml tags every release. */
    twin: (version, label) => {
      tag(`v${version}`, label);
      tag(`satellites-v${version}`, label, true);
    },
    untag: (name) => run("tag", "--delete", name),
    checkout: (label) => run("checkout", "--quiet", commits.get(label)),
    branch: (name, label) => run("checkout", "--quiet", "-b", name, commits.get(label)),
    remove: () => rmSync(root, { recursive: true, force: true }),
  };
}

/**
 * A flat container serving a recorded state of nuget.org, { "<id>": { "<version>": <commit> } }: each version's
 * index entry and its nuspec, at the lower-cased paths nuget.org serves (/<id>/index.json, /<id>/<version>/<id>.nuspec),
 * the nuspec naming the commit the version was packed from as SourceLink writes it. A version may also be
 * { commit, dependencies: { "<id>": "<range>" } }, whose nuspec lists those dependencies as dotnet pack writes them.
 * Any other path is a 404, as for a package nuget.org never had or a path in another case.
 */
async function feed(state) {
  const packages = new Map(Object.entries(state).map(([id, versions]) => [id.toLowerCase(), { id, versions }]));
  const server = createServer((request, response) => {
    const [, id, index, version, file] = /^\/([^/]+)\/(?:(index\.json)|([^/]+)\/([^/]+\.nuspec))$/.exec(request.url) ?? [];
    const known = id && packages.get(id);
    const answer = (status, body) => {
      response.writeHead(status, { "content-type": "application/json" });
      response.end(body);
    };
    if (!known) return answer(404, "");
    if (index) return answer(200, JSON.stringify({ versions: Object.keys(known.versions) }));
    if (file !== `${id}.nuspec`) return answer(404, "");
    const entry = Object.entries(known.versions).find(([key]) => key.toLowerCase() === version)?.[1];
    if (!entry) return answer(404, "");
    const { commit, dependencies = {} } = typeof entry === "string" ? { commit: entry } : entry;
    const listed = Object.entries(dependencies)
      .map(([name, range]) => `        <dependency id="${name}" version="${range}" exclude="Build,Analyzers" />\n`).join("");
    return answer(200, [
      '<?xml version="1.0" encoding="utf-8"?>',
      '<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">',
      "  <metadata>",
      `    <id>${known.id}</id>`,
      `    <repository type="git" url="https://example.invalid/repo.git" commit="${commit}" />`,
      "    <dependencies>",
      `      <group targetFramework="net10.0">\n${listed}      </group>`,
      "    </dependencies>",
      "  </metadata>",
      "</package>",
      "",
    ].join("\n"));
  });
  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  return {
    url: `http://127.0.0.1:${server.address().port}`,
    close: () => new Promise((resolve) => server.close(resolve)),
  };
}

// The gate's inputs, today's and the one-version gate's, so that none of the machine's reaches a run.
const RUN_ENVIRONMENT = ["GITHUB_OUTPUT", "GITHUB_STEP_SUMMARY", "NEXT_VERSIONS", "RELEASE_TYPES", "GATE_REF",
  "PACKAGE_TRAINS", "EXPECT_BASES", "EXPECT_VERSIONS", "PUSH", "PACKAGE_IDS_SCRIPT", "NUGET_FLAT_CONTAINER",
  "NEXT_VERSION", "RELEASE_TYPE", "PACKAGE_IDS", "EXPECT_BASE", "EXPECT_VERSION"];
// curl reads no_proxy before NO_PROXY: both are replaced, so that no proxy of the machine's reaches the local feed.
const PROXIES = ["http_proxy", "https_proxy", "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "all_proxy", "no_proxy", "NO_PROXY"];

/**
 * Runs the gate of a repository against a feed: { status, outputs, errors, warnings, notes, summary }, every commit
 * hash written as its label ({C1}, or {C1:7} for its short form), so that the outputs read as recorded.
 */
async function gate(repo, state, { args = [], env = {} } = {}) {
  const hash = (label) => repo.commits.get(label) ?? label;
  const served = await feed(Object.fromEntries(Object.entries(state).map(([id, versions]) => [id,
    Object.fromEntries(Object.entries(versions).map(([version, entry]) =>
      [version, typeof entry === "string" ? hash(entry) : { ...entry, commit: hash(entry.commit) }]))])));
  const work = mkdtempSync(join(tmpdir(), "preview-gate-run-"));
  try {
    const environment = withoutGit();
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
    for (const [name, value] of Object.entries(environment)) if (value === undefined) delete environment[name];
    writeFileSync(environment.GITHUB_OUTPUT, "");
    writeFileSync(environment.GITHUB_STEP_SUMMARY, "");
    const { status, stderr } = await new Promise((resolve) => {
      execFile("bash", [join(repo.root, ".github", "scripts", "preview-gate.sh"), ...args],
        { cwd: repo.root, env: environment, encoding: "utf8" },
        (error, stdout, stderr) => resolve({ status: error ? error.code : 0, stderr }));
    });
    const labelled = (text) => {
      for (const [label, full] of repo.commits) text = text.replaceAll(full, `{${label}}`).replaceAll(full.slice(0, 7), `{${label}:7}`);
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

// --- recorded states and expectations -------------------------------------------------------------------------------

/** The packages of the trains named, at one version packed from the commit of a label. */
const on = (trains, version, label) => Object.fromEntries(trains.flatMap((train) => IDS[train]).map((id) => [id, { [version]: label }]));
const core = (version, label) => on(["core"], version, label);
const satellites = (version, label) => on(["satellites"], version, label);
const every = (version, label) => on(TRAINS, version, label);
/** One package at one version, packed from the commit of a label, or { commit, dependencies }. */
const one = (id, version, entry) => ({ [id]: { [version]: entry } });
/** The union of recorded states. */
const and = (...states) => {
  const merged = {};
  for (const state of states) {
    for (const [id, versions] of Object.entries(state)) merged[id] = { ...merged[id], ...versions };
  }
  return merged;
};

/**
 * NEXT_VERSIONS and RELEASE_TYPES as next-version.mjs --trains computes them: [version, type] for the core, then for
 * the satellites, the core's when left out.
 */
const next = (coreTrain, satellitesTrain = coreTrain) => ({
  NEXT_VERSIONS: `core=${coreTrain[0]} satellites=${satellitesTrain[0]}`,
  RELEASE_TYPES: `core=${coreTrain[1]} satellites=${satellitesTrain[1]}`,
});
const PATCH = next(["0.2.2-preview.1", "patch"]);

/** What a decision says of a train: its last stable version, its base as [version, label], and how it is packed. */
const train = (last, base, packed = {}) => ({ last, base, ...packed });
/** Packed at a version computed for HEAD, and pushed. */
const pushed = (version, type, last) => ({ pack: `${version}:${type}:${last}`, push: true });
/** Packed at a version nuget.org already has, partly: pushed to complete it. */
const repaired = (version) => ({ pack: `${version}:repair`, push: true });
/** Packed for the tests and the dependency checks only: never pushed. */
const beside = (version) => ({ pack: `${version}:repair`, push: false });

/**
 * A decision as the gate writes it: its outputs, the reason on stderr, and the job summary, a line per train and the
 * package inputs that changed.
 */
function assertDecision(result, { mode, ref, trains, docsRef = ref, docsVersion, pending = "", reason, changed = [],
  warnings = [] }) {
  const pairs = (pick) => TRAINS.filter((name) => pick(trains[name]) !== undefined)
    .map((name) => `${name}=${pick(trains[name])}`).join(" ");
  const version = (expected) => expected.pack?.split(":")[0];
  const rows = TRAINS.map((name) => `| ${name} | ${PREFIX[name]}${trains[name].last} | ${trains[name].base[0]} ` +
    `(\`{${trains[name].base[1]}:7}\`) | ${version(trains[name]) ?? ""} | ${trains[name].push ? "yes" : "no"} |`);
  const summary = [`### Preview: ${mode}`, "", reason, "", "| Train | Last stable | Base | Packed at | Pushed |",
    "| --- | --- | --- | --- | --- |", ...rows];
  if (changed.length > 0) summary.push("", "Package inputs changed:", "", ...changed.map((path) => `- \`${path}\``));
  assert.deepEqual(result, {
    status: 0,
    outputs: {
      mode,
      ref,
      versions: pairs(version),
      pack: pairs((expected) => expected.pack),
      push: TRAINS.filter((name) => trains[name].push).join(" "),
      bases: pairs((expected) => expected.base[0]),
      "base-commits": pairs((expected) => `{${expected.base[1]}}`),
      "last-stables": pairs((expected) => expected.last),
      "docs-ref": docsRef,
      "docs-version": docsVersion,
      pending,
      reason,
    },
    errors: [],
    warnings,
    notes: [reason],
    summary: `${summary.join("\n")}\n`,
  });
}

/** A refusal: one error, and nothing written for the workflow. */
function assertRefusal(result, error, { warnings = [], notes = [] } = {}) {
  assert.deepEqual(result, { status: 1, outputs: {}, errors: [error], warnings, notes, summary: "" });
}

/** A fixture, removed once the test is done with it, whatever the outcome. */
async function using(make, body) {
  const repo = make();
  try {
    await body(repo);
  } finally {
    repo.remove();
  }
}

/** C0, twin-tagged 0.2.1, holding Core.A and Core.B (core), Sat.A and Sat.B (satellites). */
function released() {
  const repo = repository();
  try {
    repo.commit("C0");
    repo.twin("0.2.1", "C0");
    return repo;
  } catch (error) {
    repo.remove();
    throw error;
  }
}

/**
 * A feature whose preview shipped, F (0.3.0-preview.1), reverted before the release C0 (0.2.1): the preview stays on
 * nuget.org above every later version until a higher one ships.
 */
function revertedFeature() {
  const repo = repository();
  try {
    repo.commit("P0");
    repo.twin("0.2.0", "P0");
    repo.commit("F", { "src/Core.A/A.cs": "class A { int f; }\n" }, "feat: f");
    repo.commit("C0", { "src/Core.A/A.cs": "class A {}\n" }, "revert: feat: f\n\nThis reverts commit 0000000.");
    repo.twin("0.2.1", "C0");
    return repo;
  } catch (error) {
    repo.remove();
    throw error;
  }
}

/** After released(): C1, a core feature previewed as 0.3.0-preview.1, then R, the commit release.yml cuts 0.3.0 from. */
function releasing() {
  const repo = released();
  try {
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "feat: a");
    repo.commit("R", { "CHANGELOG.md": "# 0.3.0\n" }, "chore(release): 0.3.0 [skip ci]");
    return repo;
  } catch (error) {
    repo.remove();
    throw error;
  }
}

/**
 * --recheck as preview.yml's publish job runs it, with no dotnet: each train's IDs from PACKAGE_TRAINS, and an IDs
 * script that does not exist, which fails any run that would read it. A string gives both trains one version.
 */
const recheck = (repo, ref, { bases, versions, push = "core satellites" }) => ({
  args: ["--recheck"],
  env: {
    GATE_REF: repo.commits.get(ref) ?? ref,
    PACKAGE_TRAINS: TRAINS.flatMap((name) => IDS[name].map((id) => `${id}=${name}`)).join(" "),
    EXPECT_BASES: typeof bases === "string" ? `core=${bases} satellites=${bases}` : bases.join(" "),
    EXPECT_VERSIONS: typeof versions === "string" ? `core=${versions} satellites=${versions}` : versions.join(" "),
    PUSH: push,
    PACKAGE_IDS_SCRIPT: join(repo.root, "no-ids.sh"),
  },
});
/** A recheck that goes on: a line per train on stderr, nothing else. */
const went = (...notes) => ({ status: 0, outputs: {}, errors: [], warnings: [], notes, summary: "" });
const still = (name, version, ref) => `${name}: nuget.org still gives ${version} as the base of {${ref}:7}, as when this run decided.`;
const completes = (name, version, ref) =>
  `${name}: ${version} is partly or wholly on nuget.org already, from {${ref}:7}: this run completes it.`;
const moved = (name, found, from, ref, expected) => `${name}: nuget.org now gives ${found} ({${from}:7}) as the base of ` +
  `{${ref}:7}, where this run decided from ${expected}: something was published since. Dispatch preview.yml again ` +
  "rather than re-running this run.";

// --- PR 6's decisions of the gate of one version, both trains on one version ----------------------------------------

test("a package input changed since the bases publishes every train at its version, a package nuget.org never had included", async () => {
  await using(released, async (repo) => {
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n", "src/ids.txt": ids("Sat.C"), "src/Sat.C/C.cs": "class C {}\n" },
      "feat: c");
    const reason = "Package inputs changed since core 0.2.1 and satellites 0.2.1 ({C0:7}): src/Core.A/A.cs src/Sat.C/C.cs src/ids.txt";
    const changed = ["src/Core.A/A.cs", "src/Sat.C/C.cs", "src/ids.txt"];
    assertDecision(await gate(repo, every("0.2.1", "C0"), { env: PATCH }), {
      mode: "publish", ref: "{C1}", docsVersion: "0.2.2-preview.1", reason, changed,
      trains: {
        core: train("0.2.1", ["0.2.1", "C0"], pushed("0.2.2-preview.1", "patch", "0.2.1")),
        satellites: train("0.2.1", ["0.2.1", "C0"], pushed("0.2.2-preview.1", "patch", "0.2.1")),
      },
    });
    assertDecision(await gate(repo, every("0.2.1", "C0"), { env: next(["0.3.0-preview.1", "minor"]) }), {
      mode: "publish", ref: "{C1}", docsVersion: "0.3.0-preview.1", reason, changed,
      trains: {
        core: train("0.2.1", ["0.2.1", "C0"], pushed("0.3.0-preview.1", "minor", "0.2.1")),
        satellites: train("0.2.1", ["0.2.1", "C0"], pushed("0.3.0-preview.1", "minor", "0.2.1")),
      },
    });
  });
});

test("a change of Directory.Packages.props outside its PackageVersion entries publishes every train", async () => {
  await using(released, async (repo) => {
    repo.commit("C1", { "Directory.Packages.props": BASE_FILES["Directory.Packages.props"].replace("<Project>\n",
      "<Project>\n  <PropertyGroup><CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled></PropertyGroup>\n") },
    "build: pin");
    // No commit releases anything: no type, and each train's next patch.
    assertDecision(await gate(repo, every("0.2.1", "C0"), { env: next(["0.2.2-preview.1", ""]) }), {
      mode: "publish", ref: "{C1}", docsVersion: "0.2.2-preview.1",
      reason: "Package inputs changed since core 0.2.1 and satellites 0.2.1 ({C0:7}): Directory.Packages.props (outside the PackageVersion entries)",
      changed: ["Directory.Packages.props (outside the PackageVersion entries)"],
      trains: {
        core: train("0.2.1", ["0.2.1", "C0"], pushed("0.2.2-preview.1", "patch", "0.2.1")),
        satellites: train("0.2.1", ["0.2.1", "C0"], pushed("0.2.2-preview.1", "patch", "0.2.1")),
      },
    });
  });
});

test("nothing a package is built from changed: none, at the stable release and after it", async () => {
  await using(released, async (repo) => {
    const none = {
      mode: "none", docsVersion: "0.2.1", reason: "No package input changed since core 0.2.1 ({C0:7}), satellites 0.2.1 ({C0:7}).",
      trains: { core: train("0.2.1", ["0.2.1", "C0"]), satellites: train("0.2.1", ["0.2.1", "C0"]) },
    };
    assertDecision(await gate(repo, every("0.2.1", "C0"), { env: next(["0.2.2-preview.0", ""]) }), { ...none, ref: "{C0}" });
    repo.commit("C1", { "docs/guide.md": "# Guide\n", "src/Core.A/CLAUDE.md": "# A\n" }, "docs: a guide");
    assertDecision(await gate(repo, every("0.2.1", "C0"), { env: next(["0.2.2-preview.1", ""]) }), { ...none, ref: "{C1}" });
  });
});

test("the satellites partial, the core whole at their commit: they are repaired from it, pending what HEAD adds since", async () => {
  await using(released, async (repo) => {
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "fix: a");
    // The run at C1 pushed the core whole and the satellites in part.
    const partial = and(every("0.2.1", "C0"), core("0.2.2-preview.1", "C1"), one("Sat.A", "0.2.2-preview.1", "C1"));
    const repair = {
      mode: "repair", ref: "{C1}", docsVersion: "0.2.2-preview.1",
      trains: {
        core: train("0.2.1", ["0.2.2-preview.1", "C1"], beside("0.2.2-preview.1")),
        satellites: train("0.2.1", ["0.2.2-preview.1", "C1"], repaired("0.2.2-preview.1")),
      },
    };
    const reason = "Completing from {C1:7}: satellites 0.2.2-preview.1, missing for Sat.B.";
    assertDecision(await gate(repo, partial, { env: PATCH }), { ...repair, pending: "false", reason });
    // A commit since that changes no package: nothing pending, and the site is built from HEAD.
    repo.commit("D1", { "docs/guide.md": "# Guide\n" }, "docs: a guide");
    assertDecision(await gate(repo, partial, { env: next(["0.2.2-preview.2", "patch"]) }),
      { ...repair, docsRef: "{D1}", pending: "false", reason });
    repo.commit("C2", { "src/Sat.B/B.cs": "class SB { }\n" }, "fix: b");
    assertDecision(await gate(repo, partial, { env: next(["0.2.2-preview.3", "patch"]) }), {
      ...repair, pending: "true", changed: ["src/Sat.B/B.cs"],
      reason: `${reason} HEAD has further package changes, for the next run.`,
    });
  });
});

test("both trains published from one commit, then a change: each train's base is that preview, and both are pushed", async () => {
  await using(released, async (repo) => {
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "fix: a");
    repo.commit("C2", { "src/Sat.B/B.cs": "class SB { }\n" }, "fix: b");
    const state = and(every("0.2.1", "C0"), every("0.2.2-preview.1", "C1"));
    // The change is the satellites' alone: the core, whose base is not HEAD, is pushed too.
    assertDecision(await gate(repo, state, { env: next(["0.2.2-preview.2", "patch"]) }), {
      mode: "publish", ref: "{C2}", docsVersion: "0.2.2-preview.2",
      reason: "Package inputs changed since core 0.2.2-preview.1 and satellites 0.2.2-preview.1 ({C1:7}): src/Sat.B/B.cs", changed: ["src/Sat.B/B.cs"],
      trains: {
        core: train("0.2.1", ["0.2.2-preview.1", "C1"], pushed("0.2.2-preview.2", "patch", "0.2.1")),
        satellites: train("0.2.1", ["0.2.2-preview.1", "C1"], pushed("0.2.2-preview.2", "patch", "0.2.1")),
      },
    });
    assertRefusal(await gate(repo, state, { env: PATCH }),
      "NEXT_VERSIONS: 0.2.2-preview.1 is not 0.2.2-preview.2, the patch after v0.2.1 at this commit's height, for the core train.");
    assertRefusal(await gate(repo, state, { env: next(["0.2.2-preview.2", "patch"], ["0.2.2-preview.1", "patch"]) }),
      "NEXT_VERSIONS: 0.2.2-preview.1 is not 0.2.2-preview.2, the patch after satellites-v0.2.1 at this commit's height, for the satellites train.");
  });
});

test("a reverted feature's preview, above every later version, stays out of each base, warns per train, and can hold a train's version", async () => {
  await using(revertedFeature, async (repo) => {
    const state = and(every("0.3.0-preview.1", "F"), every("0.2.1", "C0"));
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "fix: a");
    const below = (name) => `::warning title=Below an earlier version::${name}: 0.2.2-preview.1 sorts below 0.3.0-preview.1, ` +
      "which nuget.org already has: NuGet offers 0.3.0-preview.1 as the latest prerelease until a higher version ships.";
    assertDecision(await gate(repo, state, { env: PATCH }), {
      mode: "publish", ref: "{C1}", docsVersion: "0.2.2-preview.1",
      reason: "Package inputs changed since core 0.2.1 and satellites 0.2.1 ({C0:7}): src/Core.A/A.cs", changed: ["src/Core.A/A.cs"],
      warnings: [below("core"), below("satellites")],
      trains: {
        core: train("0.2.1", ["0.2.1", "C0"], pushed("0.2.2-preview.1", "patch", "0.2.1")),
        satellites: train("0.2.1", ["0.2.1", "C0"], pushed("0.2.2-preview.1", "patch", "0.2.1")),
      },
    });
    const held = (name, id) => `${name}: 0.3.0-preview.1 is already on nuget.org (${id}), packed from another commit. The ` +
      "preview number counts commits since the train's last stable tag, so a preview left above a lower release (a " +
      "feature reverted after its preview shipped) can hold it: the next commit on main moves the number.";
    assertRefusal(await gate(repo, state, { env: next(["0.3.0-preview.1", "minor"]) }), held("core", "Core.A"));
    assertRefusal(await gate(repo, state, { env: next(["0.2.2-preview.1", "patch"], ["0.3.0-preview.1", "minor"]) }),
      held("satellites", "Sat.A"), { warnings: [below("core")] });
  });
});

test("a publish refuses, train by train, a version it does not recompute and a release type it cannot trust", async () => {
  await using(released, async (repo) => {
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "fix: a");
    const state = every("0.2.1", "C0");
    const refused = async (env, error) => assertRefusal(await gate(repo, state, { env }), error);
    await refused(next(["0.3.0-preview.1", "patch"]),
      "NEXT_VERSIONS: 0.3.0-preview.1 is not 0.2.2-preview.1, the patch after v0.2.1 at this commit's height, for the core train.");
    await refused(next(["0.2.2", "patch"]),
      "NEXT_VERSIONS: '0.2.2' is not of the X.Y.Z-preview.N shape that next-version.mjs computes, for the core train.");
    await refused(next(["0.2.2-preview.1", "patch"], ["0.2.2-preview.1.0", "patch"]),
      "NEXT_VERSIONS: '0.2.2-preview.1.0' is not of the X.Y.Z-preview.N shape that next-version.mjs computes, for the satellites train.");
    await refused(next(["0.2.2-preview.1", "huge"]), "RELEASE_TYPES: 'huge' is not major, minor, patch or empty for the core train.");
    await refused(next(["0.2.2-preview.1", "patch"], ["0.2.2-preview.1", "Patch"]),
      "RELEASE_TYPES: 'Patch' is not major, minor, patch or empty for the satellites train.");
    const major = (name) => `RELEASE_TYPES: major for the ${name} train, which the commit analyzer cannot answer while ` +
      "src/release-trains.json rates its breaking change a minor.";
    await refused(next(["1.0.0-preview.1", "major"]), major("core"));
    await refused(next(["0.2.2-preview.1", "patch"], ["1.0.0-preview.1", "major"]), major("satellites"));
  });
});

test("a base nuget.org cannot give a train is refused: off HEAD's history, nothing since its tag, no tag of it, a release half pushed", async () => {
  await using(released, async (repo) => {
    repo.branch("side", "C0");
    repo.commit("S1", { "src/Core.A/A.cs": "class A { int x; }\n" }, "feat: on a side branch");
    repo.run("checkout", "--quiet", "main");
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "fix: a");
    const offHistory = (id) => `${id} 0.2.2-preview.1 was packed from {S1}, which is not an ancestor of {C1:7}: a newer ` +
      "commit was published since, or the history was rewritten.";
    assertRefusal(await gate(repo, and(every("0.2.1", "C0"), core("0.2.2-preview.1", "S1")), { env: PATCH }), offHistory("Core.A"));
    assertRefusal(await gate(repo, and(every("0.2.1", "C0"), satellites("0.2.2-preview.1", "S1")), { env: PATCH }),
      offHistory("Sat.A"));
    const nothing = (tag, name) => `${tag}0.2.1 is tagged, but nuget.org has no version of the ${name} packages from 0.2.1 ` +
      "on: the release's push failed, or nuget.org has not listed it yet. Complete the release, or wait, then dispatch again.";
    assertRefusal(await gate(repo, every("0.2.0", "C0"), { env: PATCH }), nothing("v", "core"));
    assertRefusal(await gate(repo, and(core("0.2.1", "C0"), satellites("0.2.0", "C0")), { env: PATCH }),
      nothing("satellites-v", "satellites"));
    assertRefusal(await gate(repo, and(satellites("0.2.1", "C0"), one("Core.A", "0.2.1", "C0"), one("Core.B", "0.2.0", "C0")),
      { env: PATCH }),
    "core: 0.2.1 is missing from nuget.org for Core.B, but it is not a version preview.yml publishes (a stable release, " +
      "or a preview from before it): complete it by hand (docs/maintaining.md).");
  });
  const untagged = (tag, name) => `No stable tag ${tag}<major>.<minor>.<patch> of the ${name} train is merged into {C0:7}: ` +
    "push the train's baseline tag (satellites-v0.2.1 on 68abdc7 for the satellites, docs/maintaining.md).";
  await using(repository, async (repo) => {
    repo.commit("C0");
    assertRefusal(await gate(repo, every("0.2.1", "C0"), { env: PATCH }), untagged("v", "core"));
    repo.tag("v0.2.1", "C0");
    assertRefusal(await gate(repo, every("0.2.1", "C0"), { env: PATCH }), untagged("satellites-v", "satellites"));
  });
});

test("--recheck goes on while nuget.org gives each train the base the plan saw, or this run's own version, and stops otherwise", async () => {
  await using(released, async (repo) => {
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "fix: a");
    assert.deepEqual(await gate(repo, every("0.2.1", "C0"), recheck(repo, "C1", { bases: "0.2.1", versions: "0.2.2-preview.1" })),
      went(still("core", "0.2.1", "C1"), still("satellites", "0.2.1", "C1")));
    const halfPushed = and(every("0.2.1", "C0"), core("0.2.2-preview.1", "C1"), one("Sat.A", "0.2.2-preview.1", "C1"));
    assert.deepEqual(await gate(repo, halfPushed, recheck(repo, "C1", { bases: "0.2.1", versions: "0.2.2-preview.1" })),
      went(completes("core", "0.2.2-preview.1", "C1"), completes("satellites", "0.2.2-preview.1", "C1")));
    repo.commit("C2", { "src/Sat.B/B.cs": "class SB { }\n" }, "fix: b");
    // A repair's publish job: GATE_REF the base commit it completes, HEAD the commit the run started on, after it. The
    // core, whole at that commit, is packed beside the satellites and not pushed.
    assert.deepEqual(await gate(repo, halfPushed,
      recheck(repo, "C1", { bases: "0.2.2-preview.1", versions: "0.2.2-preview.1", push: "satellites" })),
    went(still("core", "0.2.2-preview.1", "C1"), completes("satellites", "0.2.2-preview.1", "C1")));
    assertRefusal(await gate(repo, and(every("0.2.1", "C0"), every("0.2.2-preview.1", "C1")),
      recheck(repo, "C2", { bases: "0.2.1", versions: "0.2.2-preview.2" })),
    moved("core", "0.2.2-preview.1", "C1", "C2", "0.2.1"));
    assertRefusal(await gate(repo, and(every("0.2.1", "C0"), every("0.2.2-preview.2", "C1")),
      recheck(repo, "C2", { bases: "0.2.1", versions: "0.2.2-preview.2" })),
    "core: 0.2.2-preview.2 is on nuget.org, packed from {C1:7}, not from {C2:7}, which this run packed.");
    assertRefusal(await gate(repo, every("0.2.1", "C0"),
      recheck(repo, "0000000000000000000000000000000000000000", { bases: "0.2.1", versions: "0.2.2-preview.2" })),
    "GATE_REF 0000000000000000000000000000000000000000 is not a commit of this clone.");
    repo.checkout("C1");
    assertRefusal(await gate(repo, every("0.2.1", "C0"), recheck(repo, "C2", { bases: "0.2.1", versions: "0.2.2-preview.2" })),
      "GATE_REF {C2} is not HEAD or one of its ancestors.");
  });
});

test("--report warns, train by train, of package inputs no version on nuget.org carries, and of a version some packages lack", async () => {
  await using(released, async (repo) => {
    const reported = (warnings, ...sections) => ({ status: 0, outputs: {}, errors: [], warnings, notes: [], summary: sections.join("") });
    const whole = (name, version, ref) =>
      `Every package input of the ${name} train in this release is already on nuget.org, in ${version} (\`{${ref}:7}\`).\n`;
    const partly = (name, version, ref, id, hint) => `### Partly published: ${name}\n\n${version}, the version of the ${name} ` +
      `train on nuget.org built from the nearest commit, \`{${ref}:7}\`, is missing for 1 of its packages, which therefore ` +
      `carry none of what changed up to that commit:\n\n- \`${id}\`\n\n${hint}\n`;
    const notPreviewed = (name, version, kind, ref, paths) => `### Not previewed: ${name}\n\nThis release ships package ` +
      `inputs that no preview of the ${name} train carried. They changed since ${version}, the ${kind} of the train on ` +
      `nuget.org built from the nearest commit, \`{${ref}:7}\`:\n\n${paths.map((path) => `- \`${path}\`\n`).join("")}\n` +
      "Dispatching preview.yml first publishes them as a preview.\n";
    const warned = (title, name, text) => `::warning title=${title}::${name}: ${text}`;
    assert.deepEqual(await gate(repo, every("0.2.1", "C0"), { args: ["--report"] }),
      reported([], whole("core", "0.2.1", "C0"), whole("satellites", "0.2.1", "C0")));
    // A stable release some packages lack is completed by hand, never by preview.yml, which refuses to.
    assert.deepEqual(await gate(repo, and(satellites("0.2.1", "C0"), one("Core.A", "0.2.1", "C0")), { args: ["--report"] }), reported(
      [warned("Partly published", "core", "0.2.1 is missing from nuget.org for Core.B.")],
      partly("core", "0.2.1", "C0", "Core.B", "Complete that release by hand first (docs/maintaining.md)."),
      whole("satellites", "0.2.1", "C0")));
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "fix: a");
    const sinceRelease = "this release ships package inputs that no preview carried, changed since 0.2.1: src/Core.A/A.cs";
    assert.deepEqual(await gate(repo, every("0.2.1", "C0"), { args: ["--report"] }), reported(
      [warned("Not previewed", "core", sinceRelease), warned("Not previewed", "satellites", sinceRelease)],
      notPreviewed("core", "0.2.1", "release", "C0", ["src/Core.A/A.cs"]),
      notPreviewed("satellites", "0.2.1", "release", "C0", ["src/Core.A/A.cs"])));
    assert.deepEqual(await gate(repo, and(every("0.2.1", "C0"), core("0.2.2-preview.1", "C1"), one("Sat.A", "0.2.2-preview.1", "C1")),
      { args: ["--report"] }), reported(
      [warned("Partly published", "satellites", "0.2.2-preview.1 is missing from nuget.org for Sat.B.")],
      whole("core", "0.2.2-preview.1", "C1"),
      partly("satellites", "0.2.2-preview.1", "C1", "Sat.B", "Dispatching preview.yml first completes it.")));
    // Each train is compared with its own base: the core previewed whole, the satellites never.
    assert.deepEqual(await gate(repo, and(every("0.2.1", "C0"), core("0.2.2-preview.1", "C1")), { args: ["--report"] }), reported(
      [warned("Not previewed", "satellites", sinceRelease)],
      whole("core", "0.2.2-preview.1", "C1"),
      notPreviewed("satellites", "0.2.1", "release", "C0", ["src/Core.A/A.cs"])));
    repo.commit("C2", { "src/Sat.B/B.cs": "class SB { }\n" }, "fix: b");
    const sincePreview = "this release ships package inputs that no preview carried, changed since 0.2.2-preview.1: src/Sat.B/B.cs";
    assert.deepEqual(await gate(repo, and(every("0.2.1", "C0"), every("0.2.2-preview.1", "C1")), { args: ["--report"] }), reported(
      [warned("Not previewed", "core", sincePreview), warned("Not previewed", "satellites", sincePreview)],
      notPreviewed("core", "0.2.2-preview.1", "preview", "C1", ["src/Sat.B/B.cs"]),
      notPreviewed("satellites", "0.2.2-preview.1", "preview", "C1", ["src/Sat.B/B.cs"])));
  });
});

// --- what deciding per train adds -----------------------------------------------------------------------------------

test("the core complete at HEAD and the satellites never pushed: the satellites alone are pushed, the core packed beside them", async () => {
  await using(released, async (repo) => {
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "fix: a");
    // The run at C1 stopped after the core's push.
    const state = and(every("0.2.1", "C0"), core("0.2.2-preview.1", "C1"));
    assertDecision(await gate(repo, state, { env: PATCH }), {
      mode: "publish", ref: "{C1}", docsVersion: "0.2.2-preview.1",
      reason: "Package inputs changed since satellites 0.2.1 ({C0:7}): src/Core.A/A.cs", changed: ["src/Core.A/A.cs"],
      trains: {
        core: train("0.2.1", ["0.2.2-preview.1", "C1"], beside("0.2.2-preview.1")),
        satellites: train("0.2.1", ["0.2.1", "C0"], pushed("0.2.2-preview.1", "patch", "0.2.1")),
      },
    });
    // Complete only at the version computed for it: at another, the core is pushed.
    assertDecision(await gate(repo, state, { env: next(["0.3.0-preview.1", "minor"]) }), {
      mode: "publish", ref: "{C1}", docsVersion: "0.3.0-preview.1",
      reason: "Package inputs changed since satellites 0.2.1 ({C0:7}): src/Core.A/A.cs", changed: ["src/Core.A/A.cs"],
      trains: {
        core: train("0.2.1", ["0.2.2-preview.1", "C1"], pushed("0.3.0-preview.1", "minor", "0.2.1")),
        satellites: train("0.2.1", ["0.2.1", "C0"], pushed("0.3.0-preview.1", "minor", "0.2.1")),
      },
    });
    // And only at HEAD: once a commit lands, the core is pushed again, at its new height.
    repo.commit("D1", { "docs/guide.md": "# Guide\n" }, "docs: a guide");
    assertDecision(await gate(repo, state, { env: next(["0.2.2-preview.2", "patch"]) }), {
      mode: "publish", ref: "{D1}", docsVersion: "0.2.2-preview.2",
      reason: "Package inputs changed since satellites 0.2.1 ({C0:7}): src/Core.A/A.cs", changed: ["src/Core.A/A.cs"],
      trains: {
        core: train("0.2.1", ["0.2.2-preview.1", "C1"], pushed("0.2.2-preview.2", "patch", "0.2.1")),
        satellites: train("0.2.1", ["0.2.1", "C0"], pushed("0.2.2-preview.2", "patch", "0.2.1")),
      },
    });
    // The version HEAD computes, whole on nuget.org but packed from an older commit, is not complete at HEAD.
    assertRefusal(await gate(repo, and(every("0.2.1", "C0"), core("0.2.2-preview.2", "C1")), { env: next(["0.2.2-preview.2", "patch"]) }),
      "core: 0.2.2-preview.2 is already on nuget.org (Core.A), packed from another commit. The preview number counts " +
      "commits since the train's last stable tag, so a preview left above a lower release (a feature reverted after its " +
      "preview shipped) can hold it: the next commit on main moves the number.");
  });
});

test("the core partial: the core is repaired, and the satellites packed beside it at its version, a placeholder never pushed", async () => {
  await using(released, async (repo) => {
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "fix: a");
    repo.commit("C2", { "src/Sat.A/A.cs": "class SA { }\n" }, "fix: s");
    // The run at C1 stopped inside the core's push: the satellites' base is still the release.
    assertDecision(await gate(repo, and(every("0.2.1", "C0"), one("Core.A", "0.2.2-preview.1", "C1")),
      { env: next(["0.2.2-preview.2", "patch"]) }), {
      mode: "repair", ref: "{C1}", docsVersion: "0.2.2-preview.1", pending: "true", changed: ["src/Sat.A/A.cs"],
      reason: "Completing from {C1:7}: core 0.2.2-preview.1, missing for Core.B. HEAD has further package changes, for the next run.",
      trains: {
        core: train("0.2.1", ["0.2.2-preview.1", "C1"], repaired("0.2.2-preview.1")),
        satellites: train("0.2.1", ["0.2.1", "C0"], beside("0.2.2-preview.1")),
      },
    });
  });
});

test("the satellites partial while the core's base is older: the core is packed at the floor of their dependency on it", async () => {
  await using(released, async (repo) => {
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "fix: a");
    repo.commit("C2", { "src/Sat.A/A.cs": "class SA { }\n" }, "fix: s");
    const at = (dependencies) => and(every("0.2.1", "C0"), core("0.2.2-preview.1", "C1"),
      one("Sat.A", "0.2.2-preview.2", { commit: "C2", dependencies }));
    const env = next(["0.2.2-preview.2", "patch"]);
    // A dependency on another satellite says nothing of the core.
    assertDecision(await gate(repo, at({ "Core.A": "[0.2.2-preview.1, 0.3.0-0.0)", "Core.B": "[0.2.2-preview.1, 0.3.0-0.0)",
      "Sat.B": "[0.2.2-preview.2, 0.3.0-0.0)" }), { env }), {
      mode: "repair", ref: "{C2}", docsVersion: "0.2.2-preview.1", pending: "false",
      reason: "Completing from {C2:7}: satellites 0.2.2-preview.2, missing for Sat.B.",
      trains: {
        core: train("0.2.1", ["0.2.2-preview.1", "C1"], beside("0.2.2-preview.1")),
        satellites: train("0.2.1", ["0.2.2-preview.2", "C2"], repaired("0.2.2-preview.2")),
      },
    });
    assertRefusal(await gate(repo, at({ "Sat.B": "[0.2.2-preview.2, 0.3.0-0.0)" }), { env }),
      "core: the repaired packages name no dependency on the core, so the core they were packed against is unknown: " +
      "complete them by hand (docs/maintaining.md).");
    assertRefusal(await gate(repo, at({ "Core.A": "[0.2.2-preview.1, 0.3.0-0.0)", "Core.B": "[0.2.2-preview.0, 0.3.0-0.0)" }), { env }),
      "core: the repaired packages were packed against several cores (0.2.2-preview.0 0.2.2-preview.1): complete them by " +
      "hand (docs/maintaining.md).");
    assertRefusal(await gate(repo, at({ "Core.A": "[0.2.2-preview.5, 0.3.0-0.0)" }), { env }),
      "core: the repaired packages were packed against core 0.2.2-preview.5, which nuget.org lacks for Core.A: complete " +
      "the core first (docs/maintaining.md).");
  });
});

// nuget.org must list the floor for every core package the repaired commit packed, not for one added since, which no
// version from before it carries.
test("a core package added after the repaired commit leaves the floor of the core it was packed against unchecked for it", async () => {
  await using(released, async (repo) => {
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "fix: a");
    repo.commit("C2", { "src/Sat.A/A.cs": "class SA { }\n" }, "fix: s");
    repo.commit("C3", { "src/release-trains.json": list({ projects: [...IDS.core, "Core.C"] }), "src/ids.txt": ids("Core.C"),
      "src/Core.C/C.cs": "class C {}\n" }, "feat: c");
    const state = and(every("0.2.1", "C0"), core("0.2.2-preview.1", "C1"),
      one("Sat.A", "0.2.2-preview.2", { commit: "C2", dependencies: { "Core.A": "[0.2.2-preview.1, 0.3.0-0.0)" } }));
    assertDecision(await gate(repo, state, { env: next(["0.3.0-preview.3", "minor"]) }), {
      mode: "repair", ref: "{C2}", docsRef: "{C2}", docsVersion: "0.2.2-preview.1", pending: "true",
      changed: ["src/Core.C/C.cs", "src/ids.txt", "src/release-trains.json"],
      reason: "Completing from {C2:7}: satellites 0.2.2-preview.2, missing for Sat.B. HEAD has further package changes, for the next run.",
      trains: {
        core: train("0.2.1", ["0.2.2-preview.1", "C1"], beside("0.2.2-preview.1")),
        satellites: train("0.2.1", ["0.2.2-preview.2", "C2"], repaired("0.2.2-preview.2")),
      },
    });
  });
});

test("both trains partial at one commit: both are repaired from it, the core first", async () => {
  await using(released, async (repo) => {
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "fix: a");
    assertDecision(await gate(repo, and(every("0.2.1", "C0"), one("Core.A", "0.2.2-preview.1", "C1"),
      one("Sat.A", "0.2.2-preview.1", "C1")), { env: PATCH }), {
      mode: "repair", ref: "{C1}", docsVersion: "0.2.2-preview.1", pending: "false",
      reason: "Completing from {C1:7}: core 0.2.2-preview.1, missing for Core.B; satellites 0.2.2-preview.1, missing for Sat.B.",
      trains: {
        core: train("0.2.1", ["0.2.2-preview.1", "C1"], repaired("0.2.2-preview.1")),
        satellites: train("0.2.1", ["0.2.2-preview.1", "C1"], repaired("0.2.2-preview.1")),
      },
    });
  });
});

test("trains partial at two commits fail: one commit is repaired per run, the core's first", async () => {
  await using(released, async (repo) => {
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "fix: a");
    repo.commit("C2", { "src/Sat.A/A.cs": "class SA { }\n" }, "fix: s");
    const env = next(["0.2.2-preview.2", "patch"]);
    assertRefusal(await gate(repo, and(every("0.2.1", "C0"), one("Core.A", "0.2.2-preview.1", "C1"),
      one("Sat.A", "0.2.2-preview.2", "C2")), { env }),
    "satellites: 0.2.2-preview.2 is missing from nuget.org for Sat.B, packed from {C2:7}, while core is repaired from " +
      "{C1:7}: complete satellites 0.2.2-preview.2 by hand (docs/maintaining.md), then dispatch again.");
    // The core's commit is repaired even when it is the newer one.
    assertRefusal(await gate(repo, and(every("0.2.1", "C0"), one("Sat.A", "0.2.2-preview.1", "C1"),
      one("Core.A", "0.2.2-preview.2", "C2")), { env }),
    "satellites: 0.2.2-preview.1 is missing from nuget.org for Sat.B, packed from {C1:7}, while core is repaired from " +
      "{C2:7}: complete satellites 0.2.2-preview.1 by hand (docs/maintaining.md), then dispatch again.");
  });
});

test("after a two-train stable release, nothing changed: none, at the release and after it", async () => {
  await using(releasing, async (repo) => {
    repo.twin("0.3.0", "R");
    const state = and(every("0.2.1", "C0"), every("0.3.0-preview.1", "C1"), every("0.3.0", "R"));
    const none = {
      mode: "none", docsVersion: "0.3.0", reason: "No package input changed since core 0.3.0 ({R:7}), satellites 0.3.0 ({R:7}).",
      trains: { core: train("0.3.0", ["0.3.0", "R"]), satellites: train("0.3.0", ["0.3.0", "R"]) },
    };
    assertDecision(await gate(repo, state, { env: next(["0.3.1-preview.0", ""]) }), { ...none, ref: "{R}" });
    repo.commit("D1", { "docs/guide.md": "# Guide\n" }, "docs: a guide");
    assertDecision(await gate(repo, state, { env: next(["0.3.1-preview.1", ""]) }), { ...none, ref: "{D1}" });
  });
});

test("a satellites change after a two-train stable release publishes both trains, each from the release", async () => {
  await using(releasing, async (repo) => {
    repo.twin("0.3.0", "R");
    repo.commit("D1", { "src/Sat.A/A.cs": "class SA { }\n" }, "fix: s");
    assertDecision(await gate(repo, and(every("0.2.1", "C0"), every("0.3.0-preview.1", "C1"), every("0.3.0", "R")),
      { env: next(["0.3.1-preview.1", ""], ["0.3.1-preview.1", "patch"]) }), {
      mode: "publish", ref: "{D1}", docsVersion: "0.3.1-preview.1",
      reason: "Package inputs changed since core 0.3.0 and satellites 0.3.0 ({R:7}): src/Sat.A/A.cs", changed: ["src/Sat.A/A.cs"],
      trains: {
        core: train("0.3.0", ["0.3.0", "R"], pushed("0.3.1-preview.1", "patch", "0.3.0")),
        satellites: train("0.3.0", ["0.3.0", "R"], pushed("0.3.1-preview.1", "patch", "0.3.0")),
      },
    });
  });
});

test("one version string packed from two commits by two trains: each train's base and version come from its own packages", async () => {
  await using(released, async (repo) => {
    // The satellites released 0.2.2 alone, so their preview number counts from their own tag, and lags the core's.
    repo.commit("C1", { "src/Sat.A/A.cs": "class SA { }\n" }, "fix: s");
    repo.tag("satellites-v0.2.2", "C1");
    repo.commit("C2", { "src/Core.A/A.cs": "class A { }\n" }, "feat: a");
    repo.commit("C3", { "src/Core.B/B.cs": "class B { }\n" }, "feat: b");
    repo.commit("C4", { "src/Sat.B/B.cs": "class SB { }\n" }, "fix: t");
    const atC3 = and(every("0.2.1", "C0"), satellites("0.2.2", "C1"), core("0.3.0-preview.3", "C3"), satellites("0.3.0-preview.2", "C3"));
    // 0.3.0-preview.3 is the core's, from C3, and the satellites' next: theirs to push.
    assertDecision(await gate(repo, atC3, { env: next(["0.3.0-preview.4", "minor"], ["0.3.0-preview.3", "minor"]) }), {
      mode: "publish", ref: "{C4}", docsVersion: "0.3.0-preview.4",
      reason: "Package inputs changed since core 0.3.0-preview.3 and satellites 0.3.0-preview.2 ({C3:7}): src/Sat.B/B.cs", changed: ["src/Sat.B/B.cs"],
      trains: {
        core: train("0.2.1", ["0.3.0-preview.3", "C3"], pushed("0.3.0-preview.4", "minor", "0.2.1")),
        satellites: train("0.2.2", ["0.3.0-preview.2", "C3"], pushed("0.3.0-preview.3", "minor", "0.2.2")),
      },
    });
    // Once pushed, the satellites' 0.3.0-preview.3 is read off their own nuspec, from C4, not the core's, from C3.
    repo.commit("C5", { "docs/guide.md": "# Guide\n" }, "docs: a guide");
    assertDecision(await gate(repo, and(atC3, core("0.3.0-preview.4", "C4"), satellites("0.3.0-preview.3", "C4")),
      { env: next(["0.3.0-preview.5", "minor"], ["0.3.0-preview.4", "minor"]) }), {
      mode: "none", ref: "{C5}", docsVersion: "0.3.0-preview.4",
      reason: "No package input changed since core 0.3.0-preview.4 ({C4:7}), satellites 0.3.0-preview.3 ({C4:7}).",
      trains: {
        core: train("0.2.1", ["0.3.0-preview.4", "C4"]),
        satellites: train("0.2.2", ["0.3.0-preview.3", "C4"]),
      },
    });
  });
});

test("a v tag without its twin, nuget.org holding the satellites stable, fails, and so does the twin without the v tag", async () => {
  await using(releasing, async (repo) => {
    const state = and(every("0.2.1", "C0"), every("0.3.0-preview.1", "C1"), every("0.3.0", "R"));
    repo.tag("v0.3.0", "R");
    const missing = "nuget.org has 0.3.0 of the satellites train (Sat.A), above satellites-v0.2.1, its newest tag merged " +
      "into {R:7}: a satellites-v0.3.0 tag is missing (a release without its twin tag?). Place it on the release commit, " +
      "which the release's other tag names (git tag satellites-v0.3.0 \"v0.3.0^{commit}\", docs/maintaining.md), then " +
      "dispatch again.";
    assertRefusal(await gate(repo, state, { env: next(["0.3.1-preview.0", ""]) }), missing);
    // release.yml's report only warns.
    assert.deepEqual(await gate(repo, state, { args: ["--report"] }), {
      status: 1, outputs: {}, errors: [], notes: [], summary: "",
      warnings: [`::warning title=Not checked::Could not tell whether this release was previewed: ${missing}`],
    });
    repo.untag("v0.3.0");
    repo.tag("satellites-v0.3.0", "R");
    assertRefusal(await gate(repo, state, { env: next(["0.3.1-preview.0", ""]) }),
      "nuget.org has 0.3.0 of the core train (Core.A), above v0.2.1, its newest tag merged into {R:7}: a v0.3.0 tag is " +
      "missing (a release without its twin tag?). Place it on the release commit, which the release's other tag names " +
      "(git tag v0.3.0 \"satellites-v0.3.0^{commit}\", docs/maintaining.md), then dispatch again.");
  });
});

test("a package moved into a train behind its stable version is refused: the train's previews would sort below it", async () => {
  await using(released, async (repo) => {
    repo.commit("C1", { "src/Sat.B/B.cs": "class SB { }\n" }, "fix: b");
    repo.tag("satellites-v0.2.2", "C1");
    repo.commit("C2", { "src/release-trains.json": list({ projects: [...IDS.core, "Sat.B"] }) }, "refactor: move Sat.B into the core");
    assertRefusal(await gate(repo, and(every("0.2.1", "C0"), satellites("0.2.2", "C1")), { env: next(["0.2.2-preview.2", "patch"]) }),
      "nuget.org has 0.2.2 of the core train (Sat.B), above v0.2.1, its newest tag merged into {C2:7}: a v0.2.2 tag is " +
      "missing (a release without its twin tag?). Place it on the release commit, which the release's other tag names " +
      "(git tag v0.2.2 \"satellites-v0.2.2^{commit}\", docs/maintaining.md), then dispatch again.");
  });
});

// ADR 0012 moves a package only once its new train's next version is above its last one. A version a package was
// packed at on another train is no base of its new train, and a version of the train below it is refused.
test("a package moved into a train behind its last preview is refused, not repaired at the other train's version", async () => {
  await using(released, async (repo) => {
    // A satellites feature makes theirs a minor while the core takes a patch; then Sat.B moves into the core.
    repo.commit("C1", { "src/Sat.A/A.cs": "class SA { }\n" }, "feat(s): s");
    repo.commit("C2", { "src/release-trains.json": list({ projects: [...IDS.core, "Sat.B"] }) }, "refactor: move Sat.B into the core");
    const result = await gate(repo, and(every("0.2.1", "C0"), core("0.2.2-preview.1", "C1"), satellites("0.3.0-preview.1", "C1")),
      { env: next(["0.2.2-preview.2", "patch"], ["0.3.0-preview.2", "minor"]) });
    assert.equal(result.outputs.mode, undefined, `decided ${result.outputs.mode}: ${result.outputs.reason}`);
    assert.equal(result.status, 1);
    assert.match(result.errors.join("\n"), /Sat\.B/);
  });
});

test("a satellites patch while the core's line moves fails, a core minor or a core-only release alike: a minor is due", async () => {
  const lineMoved = (type, core, previous = "0.2.1") => `RELEASE_TYPES: ${type} for the satellites train, packed against ` +
    `core ${core} where its last release was packed against ${previous}: the core's line moved, so the train releases a ` +
    "minor at least.";
  await using(released, async (repo) => {
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "feat: a");
    const state = every("0.2.1", "C0");
    assertRefusal(await gate(repo, state, { env: next(["0.3.0-preview.1", "minor"], ["0.2.2-preview.1", "patch"]) }),
      lineMoved("patch", "0.3.0"));
    assertRefusal(await gate(repo, state, { env: next(["0.3.0-preview.1", "minor"], ["0.2.2-preview.1", ""]) }),
      lineMoved("nothing", "0.3.0"));
    const reason = "Package inputs changed since core 0.2.1 and satellites 0.2.1 ({C0:7}): src/Core.A/A.cs";
    assertDecision(await gate(repo, state, { env: next(["0.3.0-preview.1", "minor"]) }), {
      mode: "publish", ref: "{C1}", docsVersion: "0.3.0-preview.1", reason, changed: ["src/Core.A/A.cs"],
      trains: {
        core: train("0.2.1", ["0.2.1", "C0"], pushed("0.3.0-preview.1", "minor", "0.2.1")),
        satellites: train("0.2.1", ["0.2.1", "C0"], pushed("0.3.0-preview.1", "minor", "0.2.1")),
      },
    });
    // A core patch keeps the line: the satellites, releasing nothing, take their next patch.
    assertDecision(await gate(repo, state, { env: next(["0.2.2-preview.1", "patch"], ["0.2.2-preview.1", ""]) }), {
      mode: "publish", ref: "{C1}", docsVersion: "0.2.2-preview.1", reason, changed: ["src/Core.A/A.cs"],
      trains: {
        core: train("0.2.1", ["0.2.1", "C0"], pushed("0.2.2-preview.1", "patch", "0.2.1")),
        satellites: train("0.2.1", ["0.2.1", "C0"], pushed("0.2.2-preview.1", "patch", "0.2.1")),
      },
    });
  });
  // The core released 0.3.0 alone: it releases nothing since, and the satellites, packed against it, preview a minor.
  await using(releasing, async (repo) => {
    repo.tag("v0.3.0", "R");
    repo.commit("D1", { "src/Sat.A/A.cs": "class SA { }\n" }, "fix: s");
    const state = and(every("0.2.1", "C0"), every("0.3.0-preview.1", "C1"), core("0.3.0", "R"));
    assertRefusal(await gate(repo, state, { env: next(["0.3.1-preview.1", ""], ["0.2.2-preview.3", "patch"]) }),
      lineMoved("patch", "0.3.0"));
    assertDecision(await gate(repo, state, { env: next(["0.3.1-preview.1", ""], ["0.3.0-preview.3", "minor"]) }), {
      mode: "publish", ref: "{D1}", docsVersion: "0.3.1-preview.1",
      reason: "Package inputs changed since core 0.3.0 ({R:7}): src/Sat.A/A.cs; since satellites 0.3.0-preview.1 ({C1:7}): " +
        "src/Sat.A/A.cs",
      changed: ["src/Sat.A/A.cs"],
      trains: {
        core: train("0.3.0", ["0.3.0", "R"], pushed("0.3.1-preview.1", "patch", "0.3.0")),
        satellites: train("0.2.1", ["0.3.0-preview.1", "C1"], pushed("0.3.0-preview.3", "minor", "0.2.1")),
      },
    });
  });
  // The satellites released 0.3.0 alone, packed against core 0.2.1: the line is the core's, not theirs. A core patch
  // leaves them a patch; a core minor moves the core's line under them, whatever line their own version is on.
  await using(released, async (repo) => {
    repo.commit("C1", { "src/Sat.A/A.cs": "class SA { }\n" }, "feat(s)!: s");
    repo.tag("satellites-v0.3.0", "C1");
    repo.commit("C2", { "src/Core.A/A.cs": "class A { }\n" }, "fix: a");
    const state = and(every("0.2.1", "C0"), satellites("0.3.0", "C1"));
    assertDecision(await gate(repo, state, { env: next(["0.2.2-preview.2", "patch"], ["0.3.1-preview.1", "patch"]) }), {
      mode: "publish", ref: "{C2}", docsVersion: "0.2.2-preview.2",
      reason: "Package inputs changed since core 0.2.1 ({C0:7}): src/Core.A/A.cs src/Sat.A/A.cs; since satellites 0.3.0 " +
        "({C1:7}): src/Core.A/A.cs",
      changed: ["src/Core.A/A.cs", "src/Sat.A/A.cs"],
      trains: {
        core: train("0.2.1", ["0.2.1", "C0"], pushed("0.2.2-preview.2", "patch", "0.2.1")),
        satellites: train("0.3.0", ["0.3.0", "C1"], pushed("0.3.1-preview.1", "patch", "0.3.0")),
      },
    });
    assertRefusal(await gate(repo, state, { env: next(["0.3.0-preview.2", "minor"], ["0.3.1-preview.1", "patch"]) }),
      lineMoved("patch", "0.3.0"));
  });
});

test("from 1.0 a line is a major: a core minor leaves the satellites a patch, a core major needs a major-breaking train's major", async () => {
  const at = (breaking) => () => {
    const repo = repository();
    try {
      repo.commit("C0", { "src/release-trains.json": list({ core: breaking, satellites: breaking }) });
      repo.twin("1.2.0", "C0");
      repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "feat: a");
      return repo;
    } catch (error) {
      repo.remove();
      throw error;
    }
  };
  const reason = "Package inputs changed since core 1.2.0 and satellites 1.2.0 ({C0:7}): src/Core.A/A.cs";
  await using(at("minor"), async (repo) => {
    assertDecision(await gate(repo, every("1.2.0", "C0"), { env: next(["1.3.0-preview.1", "minor"], ["1.2.1-preview.1", "patch"]) }), {
      mode: "publish", ref: "{C1}", docsVersion: "1.3.0-preview.1", reason, changed: ["src/Core.A/A.cs"],
      trains: {
        core: train("1.2.0", ["1.2.0", "C0"], pushed("1.3.0-preview.1", "minor", "1.2.0")),
        satellites: train("1.2.0", ["1.2.0", "C0"], pushed("1.2.1-preview.1", "patch", "1.2.0")),
      },
    });
  });
  await using(at("major"), async (repo) => {
    const state = every("1.2.0", "C0");
    const lineMoved = (type) => `RELEASE_TYPES: ${type} for the satellites train, packed against core 2.0.0 where its ` +
      "last release was packed against 1.2.0: the core's line moved, so the train releases a major at least.";
    assertRefusal(await gate(repo, state, { env: next(["2.0.0-preview.1", "major"], ["1.2.1-preview.1", "patch"]) }), lineMoved("patch"));
    assertRefusal(await gate(repo, state, { env: next(["2.0.0-preview.1", "major"], ["1.3.0-preview.1", "minor"]) }), lineMoved("minor"));
    assertDecision(await gate(repo, state, { env: next(["2.0.0-preview.1", "major"]) }), {
      mode: "publish", ref: "{C1}", docsVersion: "2.0.0-preview.1", reason, changed: ["src/Core.A/A.cs"],
      trains: {
        core: train("1.2.0", ["1.2.0", "C0"], pushed("2.0.0-preview.1", "major", "1.2.0")),
        satellites: train("1.2.0", ["1.2.0", "C0"], pushed("2.0.0-preview.1", "major", "1.2.0")),
      },
    });
  });
});

test("--recheck of another run's publish fails, and of this run's own half push passes, train by train", async () => {
  await using(released, async (repo) => {
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "fix: a");
    repo.commit("C2", { "src/Sat.A/A.cs": "class SA { }\n" }, "fix: s");
    // This run, at C2, decided from the release for both trains.
    const plan = recheck(repo, "C2", { bases: "0.2.1", versions: "0.2.2-preview.2" });
    assertRefusal(await gate(repo, and(every("0.2.1", "C0"), satellites("0.2.2-preview.1", "C1")), plan),
      moved("satellites", "0.2.2-preview.1", "C1", "C2", "0.2.1"), { notes: [still("core", "0.2.1", "C2")] });
    assert.deepEqual(await gate(repo, and(every("0.2.1", "C0"), core("0.2.2-preview.2", "C2")), plan),
      went(completes("core", "0.2.2-preview.2", "C2"), still("satellites", "0.2.1", "C2")));
    assert.deepEqual(await gate(repo, and(every("0.2.1", "C0"), core("0.2.2-preview.2", "C2"), one("Sat.A", "0.2.2-preview.2", "C2")),
      plan), went(completes("core", "0.2.2-preview.2", "C2"), completes("satellites", "0.2.2-preview.2", "C2")));
    // Each train is held to its own version: the satellites' breaking change makes theirs a minor.
    assert.deepEqual(await gate(repo, and(every("0.2.1", "C0"), core("0.2.2-preview.2", "C2"), one("Sat.A", "0.3.0-preview.2", "C2")),
      recheck(repo, "C2", { bases: "0.2.1", versions: ["core=0.2.2-preview.2", "satellites=0.3.0-preview.2"] })),
    went(completes("core", "0.2.2-preview.2", "C2"), completes("satellites", "0.3.0-preview.2", "C2")));
    // The core complete at HEAD, packed and not pushed: its base is the version it has.
    assert.deepEqual(await gate(repo, and(every("0.2.1", "C0"), core("0.2.2-preview.2", "C2"), one("Sat.A", "0.2.2-preview.2", "C2")),
      recheck(repo, "C2", { bases: ["core=0.2.2-preview.2", "satellites=0.2.1"], versions: "0.2.2-preview.2", push: "satellites" })),
    went(still("core", "0.2.2-preview.2", "C2"), completes("satellites", "0.2.2-preview.2", "C2")));
    // A train this run does not push, here the placeholder of a repair of the core, may not reach its version.
    assertRefusal(await gate(repo, and(every("0.2.1", "C0"), core("0.2.2-preview.1", "C1"), satellites("0.2.2-preview.1", "C1")),
      recheck(repo, "C1", { bases: ["core=0.2.2-preview.1", "satellites=0.2.1"], versions: "0.2.2-preview.1", push: "core" })),
    moved("satellites", "0.2.2-preview.1", "C1", "C1", "0.2.1"), { notes: [completes("core", "0.2.2-preview.1", "C1")] });
  });
});

test("NEXT_VERSIONS and RELEASE_TYPES name every train once, each with a value, RELEASE_TYPES' maybe empty", async () => {
  await using(released, async (repo) => {
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "fix: a");
    const state = every("0.2.1", "C0");
    const types = PATCH.RELEASE_TYPES;
    const refused = async (env, error) => assertRefusal(await gate(repo, state, { env }), error);
    await refused({ RELEASE_TYPES: types }, "NEXT_VERSIONS: no value for the core train.");
    await refused({ NEXT_VERSIONS: "core=0.2.2-preview.1", RELEASE_TYPES: types }, "NEXT_VERSIONS: no value for the satellites train.");
    await refused({ NEXT_VERSIONS: "core=0.2.2-preview.1 satellites", RELEASE_TYPES: types },
      "NEXT_VERSIONS: 'satellites' is not <train>=<value>.");
    await refused({ NEXT_VERSIONS: "core=0.2.2-preview.1 satellites=0.2.2-preview.1 docs=0.2.2-preview.1", RELEASE_TYPES: types },
      "NEXT_VERSIONS: docs is no train of src/release-trains.json.");
    await refused({ NEXT_VERSIONS: "core=0.2.2-preview.1 core=0.2.2-preview.1 satellites=0.2.2-preview.1", RELEASE_TYPES: types },
      "NEXT_VERSIONS: the core train is given twice.");
    await refused({ NEXT_VERSIONS: "core= satellites=0.2.2-preview.1", RELEASE_TYPES: types },
      "NEXT_VERSIONS: the core train is given no value.");
    await refused({ NEXT_VERSIONS: PATCH.NEXT_VERSIONS }, "RELEASE_TYPES: no value for the core train.");
    await refused({ NEXT_VERSIONS: PATCH.NEXT_VERSIONS, RELEASE_TYPES: "core=patch" }, "RELEASE_TYPES: no value for the satellites train.");
    await refused({ NEXT_VERSIONS: PATCH.NEXT_VERSIONS, RELEASE_TYPES: "core=patch satellites=patch satellites=" },
      "RELEASE_TYPES: the satellites train is given twice.");
    // Neither is read off the order of the trains, and runs of spaces separate pairs as one does.
    assertDecision(await gate(repo, state, { env: { NEXT_VERSIONS: "  satellites=0.2.2-preview.1   core=0.2.2-preview.1 ",
      RELEASE_TYPES: "satellites=  core=patch" } }), {
      mode: "publish", ref: "{C1}", docsVersion: "0.2.2-preview.1",
      reason: "Package inputs changed since core 0.2.1 and satellites 0.2.1 ({C0:7}): src/Core.A/A.cs", changed: ["src/Core.A/A.cs"],
      trains: {
        core: train("0.2.1", ["0.2.1", "C0"], pushed("0.2.2-preview.1", "patch", "0.2.1")),
        satellites: train("0.2.1", ["0.2.1", "C0"], pushed("0.2.2-preview.1", "patch", "0.2.1")),
      },
    });
  });
});

test("--recheck's lists name every train once, PUSH trains of the list, and PACKAGE_TRAINS each package's train", async () => {
  await using(released, async (repo) => {
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "fix: a");
    const state = every("0.2.1", "C0");
    const plan = recheck(repo, "C1", { bases: "0.2.1", versions: "0.2.2-preview.1" });
    const refused = async (env, error) => assertRefusal(await gate(repo, state, { ...plan, env: { ...plan.env, ...env } }), error);
    await refused({ EXPECT_BASES: "core=0.2.1" }, "EXPECT_BASES: no value for the satellites train.");
    await refused({ EXPECT_BASES: "core=0.2.1 satellites=0.2.1 docs=0.2.1" }, "EXPECT_BASES: docs is no train of src/release-trains.json.");
    await refused({ EXPECT_VERSIONS: "core=0.2.2-preview.1 satellites=" }, "EXPECT_VERSIONS: the satellites train is given no value.");
    await refused({ EXPECT_VERSIONS: "core=0.2.2-preview.1 0.2.2-preview.1" }, "EXPECT_VERSIONS: '0.2.2-preview.1' is not <train>=<value>.");
    await refused({ PUSH: "core docs" }, "PUSH: docs is no train of src/release-trains.json.");
    await refused({ PACKAGE_TRAINS: "Core.A Core.B=core Sat.A=satellites" }, "PACKAGE_TRAINS: 'Core.A' is not <id>=<train>.");
    await refused({ PACKAGE_TRAINS: "Core.A=core Sat.A=docs" }, "PACKAGE_TRAINS: docs is no train of src/release-trains.json.");
    await refused({ PACKAGE_TRAINS: "Core.A=core Core.B=core" }, "The satellites train has no packable project under src/.");
    // GATE_REF and every list but PUSH are required, as bash's own message says.
    for (const name of ["GATE_REF", "PACKAGE_TRAINS", "EXPECT_BASES", "EXPECT_VERSIONS"]) {
      const result = await gate(repo, state, { ...plan, env: { ...plan.env, [name]: undefined } });
      assert.equal(result.status, 1, name);
      assert.match(result.notes.join("\n"), new RegExp(`${name}: ${name} required with --recheck`), name);
    }
  });
});

// A later commit can revert what lies between two trains' bases: the train with the newer base then carries a change
// HEAD no longer has, which the oldest base alone would not show. Each train's own base decides.
test("a revert after a core repair publishes the core, whose base carries what HEAD reverted", async () => {
  await using(released, async (repo) => {
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "fix: a");
    repo.commit("C2", { "src/Core.A/A.cs": BASE_FILES["src/Core.A/A.cs"] }, "revert: a");
    const reason = "Package inputs changed since core 0.2.2-preview.1 ({C1:7}): src/Core.A/A.cs";
    assertDecision(await gate(repo, and(every("0.2.1", "C0"), core("0.2.2-preview.1", "C1")),
      { env: next(["0.2.2-preview.2", "patch"]) }), {
      mode: "publish", ref: "{C2}", docsVersion: "0.2.2-preview.2", reason, changed: ["src/Core.A/A.cs"],
      trains: {
        core: train("0.2.1", ["0.2.2-preview.1", "C1"], pushed("0.2.2-preview.2", "patch", "0.2.1")),
        satellites: train("0.2.1", ["0.2.1", "C0"], pushed("0.2.2-preview.2", "patch", "0.2.1")),
      },
    });
  });
});

// A package leaves the core once the satellites' next version is above its last one (ADR 0012). Each commit's packages
// are read through that commit's own list: read through HEAD's, the moved package would leave a gap in the satellites at
// the commit that packed it on the core, which no run could fill.
test("a package moved out of the core while the trains are apart is published on its new train, with no repair", async () => {
  await using(released, async (repo) => {
    repo.commit("C1", { "src/Sat.A/A.cs": "class SA { }\n" }, "feat(s): s");
    repo.commit("C2", { "src/release-trains.json": list({ projects: ["Core.A"] }) }, "refactor: move Core.B into the satellites");
    const reason = "Package inputs changed since core 0.2.2-preview.1 and satellites 0.3.0-preview.1 ({C1:7}): " +
      "src/release-trains.json";
    assertDecision(await gate(repo, and(every("0.2.1", "C0"), core("0.2.2-preview.1", "C1"), satellites("0.3.0-preview.1", "C1")),
      { env: next(["0.2.2-preview.2", "patch"], ["0.3.0-preview.2", "minor"]) }), {
      mode: "publish", ref: "{C2}", docsVersion: "0.2.2-preview.2", reason, changed: ["src/release-trains.json"],
      trains: {
        core: train("0.2.1", ["0.2.2-preview.1", "C1"], pushed("0.2.2-preview.2", "patch", "0.2.1")),
        satellites: train("0.2.1", ["0.3.0-preview.1", "C1"], pushed("0.3.0-preview.2", "minor", "0.2.1")),
      },
    });
  });
});

// The packages an older commit packed are evaluated from its tree; a failed evaluation fails the run rather than leave
// a train's gap unseen.
test("an older commit whose packages cannot be read fails the run", async () => {
  await using(released, async (repo) => {
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n", "src/ids.fail": "" }, "fix: a");
    repo.commit("C2", { "src/ids.fail": null }, "chore: c2");
    assertRefusal(await gate(repo, and(every("0.2.1", "C0"), every("0.2.2-preview.1", "C1")),
      { env: next(["0.2.2-preview.2", "patch"]) }), "Could not read the packages {C1:7} packs.");
  });
});

// The line rule reads the core a train's last release was packed against off the tags: a train's tag with no core tag
// merged into it gives none to compare with.
test("a train's tag that merges no core tag refuses a publish of that train", async () => {
  await using(released, async (repo) => {
    repo.commit("S", { "src/Sat.A/A.cs": "class SA { }\n" }, "fix(s): s");
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "fix: a");
    repo.untag("satellites-v0.2.1");
    repo.tag("satellites-v0.2.1", "C0", true);
    repo.untag("v0.2.1");
    repo.tag("v0.2.1", "S");
    assertRefusal(await gate(repo, and(core("0.2.1", "S"), satellites("0.2.1", "C0")),
      { env: next(["0.2.2-preview.1", "patch"], ["0.2.2-preview.2", "patch"]) }),
      "No core tag v<major>.<minor>.<patch> is merged into satellites-v0.2.1.");
  });
});


// A run that decides at a commit older than a release's tags (a Monday run queued behind that release, a re-run of an
// older run) meets the release's stable version unmerged: that is no missing twin tag, and the base search decides as
// the gate of one version did.
test("a run deciding below a release's tags is no missing twin: it decides as before, or refuses the newer commit", async () => {
  await using(released, async (repo) => {
    repo.commit("D", { "src/Core.A/A.cs": "class A { }\n" }, "feat: a");
    repo.commit("R", {}, "chore(release): 0.3.0 [skip ci]");
    repo.twin("0.3.0", "R");
    repo.checkout("D");
    const state = and(every("0.2.1", "C0"), every("0.3.0-preview.1", "D"), every("0.3.0", "D"));
    assertDecision(await gate(repo, state, { env: next(["0.3.0-preview.1", "minor"]) }), {
      mode: "none", ref: "{D}", docsVersion: "0.3.0",
      reason: "No package input changed since core 0.3.0 ({D:7}), satellites 0.3.0 ({D:7}).",
      trains: {
        core: train("0.2.1", ["0.3.0", "D"]),
        satellites: train("0.2.1", ["0.3.0", "D"]),
      },
    });
  });
  await using(released, async (repo) => {
    repo.commit("C1", { "src/Core.A/A.cs": "class A { }\n" }, "fix: a");
    repo.commit("D", { "src/Core.B/B.cs": "class B { }\n" }, "feat: b");
    repo.commit("R", {}, "chore(release): 0.3.0 [skip ci]");
    repo.twin("0.3.0", "R");
    repo.checkout("C1");
    const state = and(every("0.2.1", "C0"), every("0.2.2-preview.1", "C1"), every("0.3.0", "D"));
    const plan = recheck(repo, "C1", { bases: "0.2.1", versions: "0.2.2-preview.1" });
    assertRefusal(await gate(repo, state, plan),
      "Core.A 0.3.0 was packed from {D}, which is not an ancestor of {C1:7}: a newer commit was published since, or the " +
      "history was rewritten.");
  });
});

// A package moved into the core behind the version it was packed at on the satellites is refused whatever the core's
// own newest version: here a reverted feature's preview holds that, above the moved package's.
test("a package moved behind its last version is refused even below a reverted feature's preview of the train", async () => {
  await using(revertedFeature, async (repo) => {
    repo.commit("C1", { "src/Sat.B/B.cs": "class SB { }\n" }, "feat(s): s");
    repo.commit("C2", { "src/release-trains.json": list({ projects: [...IDS.core, "Sat.B"] }) }, "refactor: move Sat.B into the core");
    const state = and(every("0.2.0", "P0"), core("0.3.0-preview.1", "F"), every("0.2.1", "C0"),
      core("0.2.2-preview.1", "C1"), satellites("0.3.0-preview.1", "C1"));
    assertRefusal(await gate(repo, state, { env: next(["0.2.2-preview.2", "patch"], ["0.3.0-preview.2", "minor"]) }),
      "core: 0.2.2-preview.2 sorts below 0.3.0-preview.1, which Sat.B was packed at from {C1:7} on another train: a " +
      "package moves to a train only once the train's next version is above the package's last one (ADR 0012). Revert " +
      "the move until then.");
  });
});
