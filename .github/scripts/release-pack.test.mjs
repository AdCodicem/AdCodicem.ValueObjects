// Tests of release-pack.sh's checks: node --test .github/scripts/release-pack.test.mjs (ci.yml's job "release tooling").
//
// The script runs against this repository's own projects, as MSBuild evaluates them, its own tags, and a flat container
// that node:http serves on 127.0.0.1 in place of nuget.org (NUGET_FLAT_CONTAINER), with RELEASE_PACK_CHECK_ONLY=true,
// its seam: it stops once every check has passed, before dotnet pack, and prints what it would pack. Each case records
// a state of nuget.org for the repository's packages. A strict or keep pack reads its train's last release from the
// tag, so those cases pack again the trains' first release of their own, v0.2.1 and satellites-v0.2.1, which are the
// same commit, and need the tags (fetch-depth: 0 in the job); a case needing other tags, or none of the later ones,
// reads them from a throwaway GIT_DIR (retagged). It needs dotnet, as the job that packs does, and evaluates every
// project once per case that gets that far, so the cases run side by side.
import { test } from "node:test";
import assert from "node:assert/strict";
import { execFile, execFileSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { createServer } from "node:http";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const scripts = dirname(fileURLToPath(import.meta.url));
const root = join(scripts, "..", "..");

/** Every packable project's package ID and train, as package-ids.sh evaluates them. */
const PACKAGES = execFileSync("bash", [join(scripts, "package-ids.sh"), "--trains"], { cwd: root, encoding: "utf8" })
  .trim().split("\n").map((line) => line.split(" "));
const IDS = PACKAGES.map(([id]) => id);
const of = (train) => PACKAGES.filter(([, name]) => name === train).map(([id]) => id);

/** Whether a tag's tree holds a project: every project there rode one version, the trains' first release. */
const at = (tag, id) => {
  try {
    execFileSync("git", ["cat-file", "-e", `${tag}^{commit}:src/${id}/${id}.csproj`], { cwd: root, stdio: "ignore" });
    return true;
  } catch {
    return false;
  }
};
const shipped = (train, tag) => of(train).filter((id) => at(tag, id));
const joined = (train, tag) => of(train).filter((id) => !at(tag, id));
const escaped = (text) => text.replaceAll(".", "\\.");

/**
 * A flat container serving { "<id>": ["<version>", ...] }: an ID absent, or null, answers 404 as nuget.org does for a
 * package it has never seen, a number answers that HTTP status, and a string answers 200 with that body.
 */
async function feed(state) {
  const packages = new Map(Object.entries(state).map(([id, versions]) => [id.toLowerCase(), versions]));
  const server = createServer((request, response) => {
    const id = /^\/([^/]+)\/index\.json$/.exec(request.url)?.[1];
    const versions = id ? packages.get(id) : undefined;
    if (typeof versions === "number") {
      response.writeHead(versions).end();
    } else if (typeof versions === "string") {
      response.writeHead(200, { "content-type": "application/json" }).end(versions);
    } else if (Array.isArray(versions)) {
      response.writeHead(200, { "content-type": "application/json" }).end(JSON.stringify({ versions }));
    } else {
      response.writeHead(404).end();
    }
  });
  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  return { url: `http://127.0.0.1:${server.address().port}`, close: () => new Promise((resolve) => server.close(resolve)) };
}

/**
 * A throwaway git directory borrowing this repository's objects, its HEAD this repository's, holding tags on a commit
 * of another commit's tree, with no parent, src/release-trains.json edited and files added: the script reads the tags
 * through GIT_DIR, and evaluates the working tree as usual. Returns the directory, which the caller removes.
 */
function retagged(tags, { from = "HEAD", list = (same) => same, files = {} } = {}) {
  const dir = mkdtempSync(join(tmpdir(), "release-pack-"));
  const gitDir = join(dir, "git");
  const ours = (...args) => execFileSync("git", args, { cwd: root, encoding: "utf8" }).trim();
  execFileSync("git", ["init", "--quiet", "--bare", gitDir]);
  writeFileSync(join(gitDir, "objects", "info", "alternates"),
    `${join(ours("rev-parse", "--path-format=absolute", "--git-common-dir"), "objects")}\n`);
  const env = { ...process.env, GIT_DIR: gitDir, GIT_INDEX_FILE: join(dir, "index"), GIT_AUTHOR_NAME: "test",
    GIT_AUTHOR_EMAIL: "test@example.com", GIT_COMMITTER_NAME: "test", GIT_COMMITTER_EMAIL: "test@example.com" };
  const git = (args, input) => execFileSync("git", args, { cwd: root, env, input, encoding: "utf8" }).trim();
  git(["update-ref", "--no-deref", "HEAD", ours("rev-parse", "HEAD")]);
  git(["read-tree", ours("rev-parse", `${from}^{tree}`)]);
  const edited = { "src/release-trains.json": JSON.stringify(list(JSON.parse(readFileSync(join(root, "src", "release-trains.json"), "utf8"))), null, 2), ...files };
  for (const [path, text] of Object.entries(edited)) {
    git(["update-index", "--add", "--cacheinfo", `100644,${git(["hash-object", "-w", "--stdin"], text)},${path}`]);
  }
  const commit = git(["commit-tree", git(["write-tree"]), "-m", "a release this repository does not hold"]);
  for (const tag of tags) git(["tag", tag, commit]);
  return dir;
}
const through = (dir) => ({ env: { GIT_DIR: join(dir, "git"), GIT_WORK_TREE: root } });

/**
 * The packages the trains' first release shipped that HEAD no longer packs, at what nuget.org holds of them: a strict
 * or keep pack of that release looks them up through the tag's tree.
 */
const FIRST = execFileSync("git", ["ls-tree", "-d", "--name-only", "v0.2.1^{commit}", "src/"], { cwd: root, encoding: "utf8" })
  .trim().split("\n").map((path) => path.replace(/^src\//, "")).filter((id) => at("v0.2.1", id) && !IDS.includes(id) &&
    !/<IsPackable>\s*false\s*<\/IsPackable>/i.test(execFileSync("git", ["show", `v0.2.1^{commit}:src/${id}/${id}.csproj`],
      { cwd: root, encoding: "utf8" })));

/** Every package of the trains given at the versions given ({ core: [...], satellites: [...] }), then the overrides. */
const state = (byTrain, more = {}) => ({
  ...Object.fromEntries(FIRST.map((id) => [id, ["0.2.0", "0.2.1"]])),
  ...Object.fromEntries(Object.entries(byTrain).flatMap(([train, versions]) => of(train).map((id) => [id, versions]))),
  ...more,
});

/** Runs the script's checks against a state of nuget.org: { status, stdout, stderr }. */
async function pack(args, nuget, { ids = IDS, env = {} } = {}) {
  const served = await feed(nuget);
  try {
    const environment = { ...process.env, NUGET_FLAT_CONTAINER: served.url, RELEASE_PACK_CHECK_ONLY: "true",
      RELEASE_PACKAGE_IDS: ids.join(" "), NO_PROXY: "127.0.0.1,localhost", no_proxy: "127.0.0.1,localhost", ...env };
    for (const name of ["http_proxy", "https_proxy", "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "all_proxy"]) delete environment[name];
    return await new Promise((resolve) => {
      execFile("bash", [join(scripts, "release-pack.sh"), ...args], { cwd: root, env: environment, encoding: "utf8" },
        (error, stdout, stderr) => resolve({ status: error ? error.code : 0, stdout, stderr }));
    });
  } finally {
    await served.close();
  }
}

const passes = (result, ...lines) => {
  assert.equal(result.status, 0, result.stderr);
  for (const line of lines) assert.match(result.stdout, line);
};
const fails = (result, error) => {
  assert.notEqual(result.status, 0, result.stdout);
  assert.match(result.stderr, error);
  assert.doesNotMatch(result.stdout, /dotnet pack/);
};

test("the repository's trains each have packages the trains' first release shipped and packages added since", () => {
  for (const [train, tag] of [["core", "v0.2.1"], ["satellites", "satellites-v0.2.1"]]) {
    assert.ok(shipped(train, tag).length > 0, `${train}: ${of(train).join(" ")}`);
    assert.ok(joined(train, tag).length > 1, `${train}: ${of(train).join(" ")}`);
  }
});

test("strict, keep and a release type each check what nuget.org holds before anything is packed", async () => {
  const [core, satellite] = [of("core")[0], of("satellites")[0]];
  const [coreShipped, coreJoined] = [shipped("core", "v0.2.1")[0], joined("core", "v0.2.1")[0]];
  const [satelliteShipped, [satelliteMoved, satelliteNew]] =
    [shipped("satellites", "satellites-v0.2.1")[0], joined("satellites", "satellites-v0.2.1")];
  // The trains' first release again, its list putting a satellite of that tree on the core: the satellites did not
  // ship it, the core did. Then satellites-v0.2.1 with a satellite since removed, and a release cut after HEAD.
  const toCore = (list) => ({ ...list, core: { ...list.core, projects: [...list.core.projects, satelliteShipped] } });
  const elsewhere = retagged(["v0.2.1", "satellites-v0.2.1"], { from: "satellites-v0.2.1", list: toCore });
  const gone = "AdCodicem.ValueObjects.Gone";
  const removed = retagged(["satellites-v0.2.1"], { from: "satellites-v0.2.1", files: {
    [`src/${gone}/${gone}.csproj`]: execFileSync("git", ["show", `satellites-v0.2.1:src/${satelliteShipped}/${satelliteShipped}.csproj`],
      { cwd: root, encoding: "utf8" }) } });
  const later = retagged(["v0.3.1"]);
  const laterStrict = retagged(["v0.2.1", "v0.3.1"], { from: "v0.2.1" });
  // A case about a version nuget.org has reads only the trains' first release, so that no release this clone fetches
  // later, which this commit does not descend from, changes its message.
  const pinned = retagged(["v0.2.1", "satellites-v0.2.1"], { from: "v0.2.1" });
  const cases = {
    // A core that does not release is packed at its last release and held to exactly its API: every package must be
    // there, with no fallback. One missing is either that release stopped midway, or a package that joined the core
    // since, which only a release of the core can publish.
    strict: pack(["core=0.2.1:strict:0.2.1", "satellites=0.3.0:minor:0.2.1"],
      state({ core: ["0.2.1"], satellites: ["0.2.1"] })),
    strictPartial: pack(["core=0.2.1:strict:0.2.1", "satellites=0.3.0:minor:0.2.1"],
      state({ core: ["0.2.1"], satellites: ["0.2.1"] }, { [coreShipped]: ["0.2.0"] })),
    strictJoined: pack(["core=0.2.1:strict:0.2.1", "satellites=0.3.0:minor:0.2.1"],
      state({ core: ["0.2.1"], satellites: ["0.2.1"] }, { [coreJoined]: null })),
    strictMovedOut: pack(["core=0.2.1:strict:0.2.1", "satellites=0.3.0:minor:0.2.1"],
      state({ core: ["0.2.1"], satellites: ["0.2.1"] }, { [satelliteShipped]: null }), through(elsewhere)),
    strictAhead: pack(["core=0.2.1:strict:0.2.1", "satellites=0.3.0:minor:0.2.1"],
      state({ core: ["0.2.1"], satellites: ["0.2.1"] }, { [coreJoined]: ["0.2.1", "0.3.0"] }), through(pinned)),
    strictLater: pack(["core=0.2.1:strict:0.2.1", "satellites=0.3.0:minor:0.2.1"],
      state({ core: ["0.2.1", "0.3.1"], satellites: ["0.2.1"] }), through(laterStrict)),
    strictNoLast: pack(["core=0.3.0:strict", "satellites=0.4.0:minor:0.3.0"], state({})),
    strictNotLast: pack(["core=0.3.1:strict:0.3.0", "satellites=0.4.0:minor:0.3.0"],
      state({ core: ["0.3.0"], satellites: ["0.3.0"] })),
    // Any other train that does not release is packed at its last release, with no baseline, once every package of
    // that release is there. A package that joined the train since passes, whatever versions it had: one moved in has
    // versions of another train's, a new one none at all (a 404).
    keep: pack(["core=0.3.0:minor:0.2.1", "satellites=0.2.1:keep:0.2.1"],
      state({ core: ["0.2.1"], satellites: ["0.2.0", "0.2.1"] }, { [satelliteMoved]: ["0.2.0"], [satelliteNew]: null })),
    keepPartial: pack(["core=0.3.0:minor:0.2.1", "satellites=0.2.1:keep:0.2.1"],
      state({ core: ["0.2.1"], satellites: ["0.2.1"] }, { [satelliteShipped]: null })),
    keepElsewhere: pack(["core=0.3.0:minor:0.2.1", "satellites=0.2.1:keep:0.2.1"],
      state({ core: ["0.2.1"], satellites: ["0.2.1"] }, { [satelliteShipped]: null }), through(elsewhere)),
    keepRemoved: pack(["core=0.3.0:minor:0.2.1", "satellites=0.2.1:keep:0.2.1"],
      state({ core: ["0.2.1"], satellites: ["0.2.1"] }), through(removed)),
    keepUntagged: pack(["core=0.3.0:minor:0.2.1", "satellites=0.2.5:keep:0.2.5"], state({})),
    // A version nuget.org has is no release of this run; nor is one below a stable version a package already has.
    // Whether the train is held to a baseline or not, and with the release's tag when it was cut after this commit.
    minorListed: pack(["core=0.3.0:minor:0.2.1", "satellites=0.3.0:minor:0.2.1"],
      state({ core: ["0.2.1"], satellites: ["0.2.1"] }, { [core]: ["0.2.1", "0.3.0"] }), through(pinned)),
    majorBelow: pack(["core=1.0.0:major:0.3.0", "satellites=1.0.0:major:0.3.0"],
      state({ core: ["0.3.0"], satellites: ["0.3.0"] }, { [satellite]: ["0.3.0", "1.1.0"] }), through(pinned)),
    // A stable version between the last release and this one is no refusal: a package moved in below the version.
    minorBetween: pack(["core=0.4.0:minor:0.3.0", "satellites=0.4.0:minor:0.3.0"],
      state({ core: ["0.3.0"], satellites: ["0.3.0"] }, { [core]: ["0.3.0", "0.3.1"] }), through(pinned)),
    queued: pack(["core=0.3.1:patch:0.3.0", "satellites=0.3.1:patch:0.3.0"],
      state({ core: ["0.3.0"], satellites: ["0.3.0"] }, { [core]: ["0.3.0", "0.3.1"] }), through(later)),
    queuedPreview: pack(["core=0.3.1-preview.4:patch:0.3.0", "satellites=0.3.1-preview.4:patch:0.3.0"],
      state({ core: ["0.3.0"], satellites: ["0.3.0"] }, { [core]: ["0.3.0", "0.3.1"] }), through(later)),
    listedTagged: pack(["core=0.2.1:patch:0.2.0", "satellites=0.2.1:patch:0.2.0"],
      state({ core: ["0.2.0"], satellites: ["0.2.0"] }, { [core]: ["0.2.0", "0.2.1"] })),
    patchListed: pack(["core=0.3.1:patch:0.3.0", "satellites=0.3.1:patch:0.3.0"],
      state({ core: ["0.3.0"], satellites: ["0.3.0"] }, { [core]: ["0.3.0", "0.3.1"] }), through(pinned)),
    patchBelow: pack(["core=0.3.1:patch:0.3.0", "satellites=0.3.1:patch:0.3.0"],
      state({ core: ["0.3.0"], satellites: ["0.3.0"] }, { [satellite]: ["0.4.0"] }), through(pinned)),
    // A train held to a baseline none of its packages has is packed without one.
    neverPublished: pack(["core=0.3.1:patch:0.3.0", "satellites=0.3.1:patch:0.3.0"],
      state({ core: null, satellites: ["0.3.0"] })),
    // A preview of the next version is above every stable version below it; a package new since the last release is
    // packed without a baseline, the rest held to it.
    preview: pack(["core=0.3.1-preview.4:patch:0.3.0", "satellites=0.3.1-preview.4:patch:0.3.0"],
      state({ core: ["0.3.0", "0.3.1-preview.3"], satellites: ["0.3.0"] }, { [core]: null })),
    // A repair looks nothing up.
    repair: pack(["core=0.3.1-preview.4:repair", "satellites=0.3.1-preview.4:repair"],
      state({ core: ["0.3.1-preview.4"], satellites: ["9.9.9"] })),
    // A lookup that fails stops the script; so does a set of packages other than the release notes'.
    unavailable: pack(["core=0.3.1:patch:0.3.0", "satellites=0.3.1:patch:0.3.0"],
      state({ core: ["0.3.0"], satellites: ["0.3.0"] }, { [core]: 500 })),
    unreadable: pack(["core=0.3.1:patch:0.3.0", "satellites=0.3.1:patch:0.3.0"],
      state({ core: ["0.3.0"], satellites: ["0.3.0"] }, { [core]: "" })),
    otherIds: pack(["core=0.3.1:patch:0.3.0", "satellites=0.3.1:patch:0.3.0"],
      state({ core: ["0.3.0"], satellites: ["0.3.0"] }), { ids: IDS.filter((id) => id !== satellite) }),
    malformed: pack(["core=0.3.0:frozen:0.3.0", "satellites=0.3.0:keep:0.3.0"], state({})),
    none: pack([], state({})),
    noIds: pack(["core=0.3.1:patch:0.3.0", "satellites=0.3.1:patch:0.3.0"], state({}), { ids: [] }),
    missing: pack(["core=0.3.1:patch:0.3.0"], state({})),
    twice: pack(["core=0.3.1:patch:0.3.0", "core=0.3.1:patch:0.3.0"], state({})),
    unknown: pack(["core=0.3.1:patch:0.3.0", "satellites=0.3.1:patch:0.3.0", "planets=0.1.0:minor"], state({})),
  };
  const results = Object.fromEntries(await Promise.all(Object.entries(cases).map(async ([name, run]) => [name, await run])));
  for (const dir of [elsewhere, removed, later, laterStrict, pinned]) rmSync(dir, { recursive: true, force: true });
  const unbaselined = (result) =>
    assert.doesNotMatch(result.stdout, /ReleaseBaselineVersions|ProjectsWithoutBaseline|validating the API|without a baseline/);

  passes(results.strict, /core: packed again at 0\.2\.1, its API held to exactly that release's/,
    /-p:ReleaseBaselineVersions=core=0\.2\.1/, /-p:StrictBaselineTrains=core/, /RELEASE_TRAIN_VERSIONS=core=0\.2\.1 satellites=0\.3\.0/);
  assert.doesNotMatch(results.strict.stdout, /without a baseline/);
  fails(results.strictPartial, new RegExp(`v0\\.2\\.1 is not complete on nuget\\.org: ${escaped(coreShipped)}, which it shipped, ` +
    "lacks 0\\.2\\.1, which the core train is packed again at\\. Complete that release first"));
  fails(results.strictJoined, new RegExp(`${escaped(coreJoined)} joined the core train after v0\\.2\\.1, so nuget\\.org cannot ` +
    "have it at 0\\.2\\.1, which the other trains would depend on: the core train must release in this run"));
  fails(results.strictMovedOut, new RegExp(`v0\\.2\\.1 is not complete on nuget\\.org: ${escaped(satelliteShipped)}, which it ` +
    "shipped, lacks 0\\.2\\.1"));
  fails(results.strictAhead, new RegExp(`${escaped(coreJoined)} has 0\\.3\\.0 on nuget\\.org, above 0\\.2\\.1, which the core train ` +
    "is packed again at and the other trains would depend on: the core train's last tag is behind nuget\\.org \\(a release " +
    "without its tag\\), or the package moved into the train before the train's next version was above its last published one"));
  fails(results.strictLater, new RegExp(`has 0\\.3\\.1 on nuget\\.org, above 0\\.2\\.1, which the core train is packed again at: ` +
    "v0\\.3\\.1 was released after this commit, so this run re-runs an older run\\. Dispatch the workflow again from main"));
  fails(results.strictNoLast, /core: a strict pack is the train's last release packed again, so its version must be its last, '', not 0\.3\.0/);
  fails(results.strictNotLast, /core: a strict pack is the train's last release packed again, so its version must be its last, '0\.3\.0', not 0\.3\.1/);
  passes(results.keep, /satellites: packed again at 0\.2\.1, with no package validation baseline/,
    /RELEASE_TRAIN_VERSIONS=core=0\.3\.0 satellites=0\.2\.1/);
  assert.doesNotMatch(results.keep.stdout, /StrictBaselineTrains/);
  unbaselined(results.keep);
  fails(results.keepPartial, new RegExp(`satellites-v0\\.2\\.1 is not complete on nuget\\.org: ${escaped(satelliteShipped)}, ` +
    "which it shipped, lacks 0\\.2\\.1"));
  passes(results.keepElsewhere, /satellites: packed again at 0\.2\.1, with no package validation baseline/);
  unbaselined(results.keepElsewhere);
  fails(results.keepRemoved, new RegExp(`satellites-v0\\.2\\.1 is not complete on nuget\\.org: ${escaped(gone)}, which it shipped, ` +
    "lacks 0\\.2\\.1"));
  fails(results.keepUntagged, /satellites: there is no tag satellites-v0\.2\.5 here, which a keep pack reads that release's packages from/);
  const stale = "its plan is older than a release published since \\(a re-run of an older run, or a dispatch queued behind " +
    "that release: dispatch again from main\\), a tag the plan counted from is missing";
  fails(results.minorListed, new RegExp(`${escaped(core)} already has 0\\.3\\.0 on nuget\\.org, so this run would publish none of it: ${stale}`));
  fails(results.majorBelow, new RegExp(`${escaped(satellite)} has 1\\.1\\.0 on nuget\\.org, at or above 1\\.0\\.0, the version NuGet ` +
    `would keep offering: ${stale}`));
  passes(results.minorBetween, /RELEASE_TRAIN_VERSIONS=core=0\.4\.0 satellites=0\.4\.0/);
  fails(results.queued, new RegExp(`${escaped(core)} already has 0\\.3\\.1 on nuget\\.org: v0\\.3\\.1 was released after this commit, ` +
    "so this run was queued behind that release or re-runs an older run\\. Dispatch the workflow again from main"));
  fails(results.queuedPreview, new RegExp(`${escaped(core)} has 0\\.3\\.1 on nuget\\.org, at or above 0\\.3\\.1-preview\\.4: v0\\.3\\.1 ` +
    "was released after this commit"));
  // v0.2.1 is merged into HEAD: no release was cut after this commit, so the message gives every cause.
  fails(results.listedTagged, new RegExp(`${escaped(core)} already has 0\\.2\\.1 on nuget\\.org, so this run would publish none of it: ${stale}`));
  fails(results.patchListed, new RegExp(`${escaped(core)} already has 0\\.3\\.1 on nuget\\.org, so this run would publish none of it: ` +
    `${stale} \\(the train's own, or the core's while release\\.yml cuts every train at one version: place it on the commit ` +
    "its twin names\\), or the package moved into the train at that very version"));
  fails(results.patchBelow, new RegExp(`${escaped(satellite)} has 0\\.4\\.0 on nuget\\.org, at or above 0\\.3\\.1, the version NuGet ` +
    `would keep offering: ${stale}.*, or the package moved into the train before the train's next version was above`));
  passes(results.preview, new RegExp(`${escaped(core)} 0\\.3\\.0 is not on nuget\\.org: packing it without a baseline`),
    /-p:ReleaseBaselineVersions=(core=0\.3\.0 satellites=0\.3\.0|satellites=0\.3\.0 core=0\.3\.0)/,
    new RegExp(`-p:ProjectsWithoutBaseline=${escaped(core)}(\\s|$)`),
    /RELEASE_TRAIN_VERSIONS=core=0\.3\.1-preview\.4 satellites=0\.3\.1-preview\.4/);
  passes(results.neverPublished, /core: 0\.3\.0 was never published to nuget\.org: no package validation baseline/,
    /-p:ReleaseBaselineVersions=satellites=0\.3\.0(\s|$)/);
  assert.doesNotMatch(results.neverPublished.stdout, /ReleaseBaselineVersions=core=|satellites=0\.3\.0 core=0\.3\.0/);
  passes(results.repair, /core: packed at 0\.3\.1-preview\.4, which nuget\.org has in part or whole, or which is never pushed/);
  fails(results.unavailable, new RegExp(`nuget\\.org answered HTTP 500 for ${escaped(core)}`));
  fails(results.unreadable, new RegExp(`Could not read the versions nuget\\.org lists for ${escaped(core)}\\.`));
  fails(results.otherIds, /RELEASE_PACKAGE_IDS does not match the packable projects under src\//);
  fails(results.malformed, /core=0\.3\.0:frozen:0\.3\.0 is not <train>=<version>:<major\|minor\|patch\|repair\|strict\|keep>/);
  fails(results.none, /release-pack\.sh takes <train>=<version>:<release-type>\[:<last-version>\] per train/);
  fails(results.noIds, /RELEASE_PACKAGE_IDS is not set/);
  fails(results.missing, /No version for the satellites train/);
  fails(results.twice, /The core train is given twice/);
  fails(results.unknown, /planets is no train of src\/release-trains\.json/);
});
