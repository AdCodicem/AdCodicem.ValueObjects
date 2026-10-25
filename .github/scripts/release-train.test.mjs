// Tests of release-train.mjs: node --test .github/scripts/release-train.test.mjs (ci.yml's job "release tooling").
//
// Each case builds a throwaway repository as release-train-plugin.test.mjs does: two trains (Core.A on the core, Sat.B
// on the satellites) tagged at 0.2.1, this repository's scripts, configurations and changelogs, its node_modules linked,
// a website/ with one frozen line, and a bare remote. Its origin is https://github.com/owner/repo.git, which git rewrites
// to the bare remote (url.<remote>.insteadOf), so that semantic-release, @semantic-release/github and the driver read
// the repository they read in GitHub Actions. A fake GitHub API, node:http on 127.0.0.1, serves the calls of the
// GitHub plugin and of the driver, and records them. The plan comes from next-version.mjs --trains there, each row
// given its train's ids, as release.yml's pack job writes it; the packages are files named as the pack names them.
// The driver runs as release.yml runs it, in a process of its own, with GitHub Actions' variables and nothing else of
// the machine's environment. A re-run of a job is replayed on the same clone, moved back to the dispatched commit with
// only the tags origin has, as a fresh checkout would be.
import { test } from "node:test";
import assert from "node:assert/strict";
import { execFile, execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { chmodSync, cpSync, mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync } from "node:fs";
import { createServer } from "node:http";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { promisify } from "node:util";
import { MARKER } from "./release-train-plugin.mjs";

const scripts = dirname(fileURLToPath(import.meta.url));
const root = join(scripts, "..", "..");
const read = (path) => readFileSync(path, "utf8");
const LIST = JSON.stringify({
  core: { breaking: "minor", projects: ["Core.A"] },
  satellites: { breaking: "minor", projects: "*" },
}, null, 2);
const IDS = { core: "Core.A", satellites: "Sat.B" };
const ORIGIN = "https://github.com/owner/repo.git";
// Nothing of the machine's git configuration either: a global hooks path or signing setting would change the remote's,
// and a per-user ignore or attributes file ($XDG_CONFIG_HOME/git/) what a commit holds.
const BASE = { PATH: process.env.PATH, HOME: process.env.HOME, GIT_TERMINAL_PROMPT: "0", GIT_CONFIG_GLOBAL: "/dev/null",
  GIT_CONFIG_NOSYSTEM: "1", XDG_CONFIG_HOME: join(tmpdir(), "release-train-test-no-configuration") };
const CONFIG = ".github/scripts/release-train.releaserc.json";
const sha256 = (data) => createHash("sha256").update(data).digest("hex");

/** A fake GitHub API: the repository, its releases and their assets, issues and the GraphQL search, recorded. */
async function github() {
  const state = { releases: [], requests: [], issues: 0, next: 1 };
  const server = createServer((request, response) => {
    const chunks = [];
    request.on("data", (chunk) => chunks.push(chunk));
    request.on("end", () => {
      const body = Buffer.concat(chunks);
      const url = new URL(request.url, base());
      state.requests.push(`${request.method} ${url.pathname}`);
      const send = (status, json) => {
        response.writeHead(status, { "content-type": "application/json" });
        response.end(json === undefined ? "" : JSON.stringify(json));
      };
      if (!/^(token|bearer) test-token$/i.test(request.headers.authorization ?? "")) return send(401, { message: "Bad credentials" });
      const json = () => JSON.parse(body.toString("utf8") || "{}");
      let match;
      if (request.method === "GET" && url.pathname === "/repos/owner/repo") {
        return send(200, { full_name: "owner/repo", clone_url: ORIGIN, permissions: { push: true } });
      }
      if (request.method === "GET" && url.pathname === "/repos/owner/repo/releases") {
        const size = Number(url.searchParams.get("per_page") ?? 30);
        const page = Number(url.searchParams.get("page") ?? 1);
        return send(200, state.releases.slice((page - 1) * size, page * size));
      }
      if (request.method === "POST" && url.pathname === "/repos/owner/repo/releases") {
        const given = json();
        if (state.releases.some((release) => release.tag_name === given.tag_name)) return send(422, { message: "already_exists" });
        const id = state.next++;
        const release = { ...given, id, draft: Boolean(given.draft), assets: [], html_url: `${base()}/releases/${id}`,
          upload_url: `${base()}/uploads/${id}/assets{?name,label}` };
        state.releases.push(release);
        return send(201, release);
      }
      if (request.method === "POST" && (match = /^\/uploads\/(\d+)\/assets$/.exec(url.pathname))) {
        const release = state.releases.find(({ id }) => id === Number(match[1]));
        const name = url.searchParams.get("name");
        if (!release) return send(404, { message: "Not Found" });
        if (release.assets.some((asset) => asset.name === name)) return send(422, { message: "already_exists" });
        const asset = { id: state.next++, name, state: "uploaded", size: body.length, digest: `sha256:${sha256(body)}`,
          browser_download_url: `${base()}/download/${name}` };
        release.assets.push(asset);
        return send(201, asset);
      }
      if (request.method === "DELETE" && (match = /^\/repos\/owner\/repo\/releases\/assets\/(\d+)$/.exec(url.pathname))) {
        for (const release of state.releases) release.assets = release.assets.filter(({ id }) => id !== Number(match[1]));
        return send(204);
      }
      if (request.method === "POST" && url.pathname === "/graphql") return send(200, { data: { repository: { issues: { nodes: [] } } } });
      if (request.method === "POST" && url.pathname === "/repos/owner/repo/issues") {
        state.issues++;
        return send(201, { number: state.issues, html_url: `${base()}/issues/${state.issues}` });
      }
      return send(404, { message: "Not Found" });
    });
  });
  await new Promise((done) => server.listen(0, "127.0.0.1", done));
  const base = () => `http://127.0.0.1:${server.address().port}`;
  return { url: base(), state, writes: () => state.requests.filter((line) => !line.startsWith("GET ")), close: () => server.close() };
}

/** A throwaway repository with both trains at 0.2.1, its origin rewritten to a bare remote, removed by its remove(). */
function repository() {
  const dir = mkdtempSync(join(tmpdir(), "release-train-driver-"));
  const work = join(dir, "work");
  const remote = join(dir, "remote.git");
  const git = (...args) => execFileSync("git", args, { cwd: work, encoding: "utf8", env: BASE }).trim();
  execFileSync("git", ["init", "--quiet", "--bare", "--initial-branch=main", remote], { env: BASE });
  mkdirSync(work);
  git("init", "--quiet", "--initial-branch=main");
  for (const [key, value] of [["user.email", "test@example.invalid"], ["user.name", "Test"], ["commit.gpgsign", "false"],
    ["tag.gpgSign", "false"], ["core.hooksPath", "/dev/null"], [`url.${pathToFileURL(remote).href}.insteadOf`, ORIGIN]]) {
    git("config", key, value);
  }
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
  for (const file of ["release-trains.mjs", "next-version.mjs", "release-train-plugin.mjs", "release-train.releaserc.json",
    "release-train.mjs"]) {
    cpSync(join(scripts, file), join(work, ".github", "scripts", file));
  }
  for (const file of [".releaserc.json", "CHANGELOG.md", "CHANGELOG.satellites.md"]) cpSync(join(root, file), join(work, file));
  symlinkSync(join(root, "node_modules"), join(work, "node_modules"), "dir");
  write(".gitignore", "node_modules\nartifacts/\n");
  write("src/release-trains.json", LIST);
  write("src/Shared/Both.cs", "class Both {}\n");
  for (const name of ["Core.A", "Sat.B"]) {
    write(`src/${name}/${name}.csproj`, `<Project Sdk="Microsoft.NET.Sdk">\n  <ItemGroup>\n    ` +
      '<Compile Include="..\\Shared\\Both.cs" Link="Shared\\Both.cs" />\n  </ItemGroup>\n</Project>\n');
    write(`artifacts/obj/${name}/project.assets.json`, JSON.stringify({ version: 3, libraries: {} }));
  }
  write("website/docs/index.md", "# Docs\n");
  write("website/versions.json", '["0.2.x"]\n');
  write("website/released-versions.json", '{"0.2.x": "0.2.1"}\n');
  write("website/versioned_docs/version-0.2.x/index.md", "# Docs 0.2\n");
  write("website/versioned_docs/version-0.2.x/old.md", "# A page frozen again without it\n");
  write("website/versioned_sidebars/version-0.2.x-sidebars.json", "{}\n");
  const base = commit("build: the trains");
  git("tag", "v0.2.1");
  git("tag", "satellites-v0.2.1");
  git("remote", "add", "origin", ORIGIN);
  git("push", "--quiet", "origin", "main", "--tags");
  const onRemote = (...args) => execFileSync("git", args, { cwd: remote, encoding: "utf8", env: BASE }).trim();
  return { dir, root: work, remote, git, write, commit, base, onRemote, remove: () => rmSync(dir, { recursive: true, force: true }) };
}

/**
 * What release.yml's pack job hands the release job, for HEAD as dispatched: the plan, each row with its train's ids,
 * the packed files of every train at its version (a .snupkg for the core's), and the frozen documentation.
 */
let files = 0;
function dispatch(repo, edit = (rows) => rows) {
  repo.git("push", "--quiet", "origin", "main");
  const out = execFileSync(process.execPath, [".github/scripts/next-version.mjs", "--trains"],
    { cwd: repo.root, encoding: "utf8", env: BASE, stdio: ["ignore", "pipe", "ignore"] });
  const line = out.split("\n").find((text) => text.startsWith("trains_json="));
  const rows = edit(JSON.parse(line.slice("trains_json=".length)).map((row) => ({ ...row, ids: IDS[row.train] })));
  const plan = join(repo.dir, `plan-${++files}.json`);
  writeFileSync(plan, JSON.stringify(rows));
  const packages = join(repo.dir, `packages-${files}`);
  rmSync(packages, { recursive: true, force: true });
  mkdirSync(packages);
  for (const row of rows) {
    const version = row.type ? row.next : row.last;
    writeFileSync(join(packages, `${row.ids}.${version}.nupkg`), `${row.ids} ${version} package\n`);
    if (row.train === "core") writeFileSync(join(packages, `${row.ids}.${version}.snupkg`), `${row.ids} ${version} symbols\n`);
  }
  const frozen = join(repo.dir, `frozen-${files}`);
  mkdirSync(join(frozen, "versioned_docs", "version-0.2.x"), { recursive: true });
  mkdirSync(join(frozen, "versioned_sidebars"), { recursive: true });
  writeFileSync(join(frozen, "versions.json"), '["0.2.x"]\n');
  writeFileSync(join(frozen, "released-versions.json"), `{"0.2.x": ${JSON.stringify(Object.fromEntries(rows.map((row) =>
    [row.train, row.type ? row.next : row.last])))}}\n`);
  writeFileSync(join(frozen, "versioned_docs", "version-0.2.x", "index.md"), "# Docs, frozen again\n");
  writeFileSync(join(frozen, "versioned_docs", "version-0.2.x", "new.md"), "# A page new since 0.2.1\n");
  writeFileSync(join(frozen, "versioned_sidebars", "version-0.2.x-sidebars.json"), '{"frozen": true}\n');
  const documentation = join(repo.dir, `documentation-${files}.tar`);
  execFileSync("tar", ["-cf", documentation, "-C", frozen, "versions.json", "released-versions.json", "versioned_docs",
    "versioned_sidebars"]);
  return { rev: repo.git("rev-parse", "HEAD"), rows: Object.fromEntries(rows.map((row) => [row.train, row])), plan, packages,
    documentation };
}

/** The driver over a train, as release.yml runs it: { code, log, outputs, summary }. */
async function drive(repo, api, run, train, { dryRun = true, args, env = {} } = {}) {
  const output = join(repo.dir, `output-${++files}`);
  const summary = join(repo.dir, `summary-${files}`);
  writeFileSync(output, "");
  writeFileSync(summary, "");
  const given = args ?? [train, "--plan", run.plan, "--config", CONFIG, "--packages", run.packages, "--documentation",
    run.documentation, ...(dryRun ? ["--dry-run"] : [])];
  const environment = {
    ...BASE, CI: "true", GITHUB_ACTIONS: "true", GITHUB_ACTION: "__run", GITHUB_EVENT_NAME: "workflow_dispatch",
    GITHUB_REF: "refs/heads/main", GITHUB_SHA: run.rev, GITHUB_REPOSITORY: "owner/repo", GITHUB_SERVER_URL: "https://github.com",
    GITHUB_API_URL: api.url, GITHUB_TOKEN: "test-token", GITHUB_OUTPUT: output, GITHUB_STEP_SUMMARY: summary,
    RELEASE_REV: run.rev, ...env,
  };
  for (const [key, value] of Object.entries(environment)) if (value === undefined) delete environment[key];
  let code = 0;
  let log;
  try {
    const { stdout, stderr } = await promisify(execFile)(process.execPath, [".github/scripts/release-train.mjs", ...given],
      { cwd: repo.root, env: environment, encoding: "utf8", maxBuffer: 1 << 26 });
    log = `${stdout}${stderr}`;
  } catch (error) {
    code = error.code;
    log = `${error.stdout ?? ""}${error.stderr ?? ""}`;
  }
  const outputs = Object.fromEntries(read(output).split("\n").filter(Boolean).map((line) =>
    [line.slice(0, line.indexOf("=")), line.slice(line.indexOf("=") + 1)]));
  return { code, log, outputs, summary: read(summary) };
}

const succeeded = (outcome) => assert.equal(outcome.code, 0, outcome.log);
const refused = (outcome, message) => {
  assert.notEqual(outcome.code, 0, outcome.log);
  assert.match(outcome.log, message);
};
/** A re-run of the job: the clone back at the dispatched commit, with the tags origin has and nothing else. */
function rerun(repo, run) {
  repo.git("reset", "--quiet", "--hard", run.rev);
  repo.git("clean", "-fdq", "--", "website");
  const remote = new Set(repo.onRemote("tag", "--list").split("\n").filter(Boolean));
  for (const tag of repo.git("tag", "--list").split("\n").filter(Boolean)) if (!remote.has(tag)) repo.git("tag", "-d", tag);
}
const tagged = (repo, tag) => {
  try {
    return repo.onRemote("rev-parse", "--verify", "--quiet", `refs/tags/${tag}^{commit}`);
  } catch {
    return undefined;
  }
};
const changed = (repo, commit) => repo.git("diff-tree", "--no-commit-id", "--name-only", "-r", commit).split("\n").sort();

test("a dry run of each train computes the plan's versions, and writes nothing", async (t) => {
  const A = { "src/Core.A/A.cs": "class A {}\n" };
  const B = { "src/Sat.B/B.cs": "class B {}\n" };
  const cases = {
    "both trains": [(repo) => {
      repo.commit("fix(a): a core fix", A);
      repo.commit("feat(b): a satellite feature", B);
    }, { core: "0.2.2", satellites: "0.3.0" }],
    "the satellites alone": [(repo) => repo.commit("fix(b): a satellite fix", B), { core: null, satellites: "0.2.2" }],
    "nothing": [(repo) => repo.commit("docs: a page", { "docs/page.md": "# page\n" }), { core: null, satellites: null }],
  };
  const api = await github();
  try {
    for (const [name, [history, expected]] of Object.entries(cases)) {
      await t.test(name, async () => {
        const repo = repository();
        try {
          history(repo);
          const run = dispatch(repo);
          for (const train of ["core", "satellites"]) {
            const outcome = await drive(repo, api, run, train);
            succeeded(outcome);
            const version = expected[train];
            const prefix = train === "core" ? "v" : "satellites-v";
            assert.deepEqual(outcome.outputs, {
              release: String(Boolean(version)), version: version ?? "0.2.1", tag: version ? `${prefix}${version}` : "",
              commit: "", resumed: "false",
            });
            if (version) {
              assert.match(outcome.summary, new RegExp(`Would release ${prefix}${version.replaceAll(".", "\\.")} `));
              assert.match(outcome.summary, /<details><summary>Notes<\/summary>/);
            } else {
              assert.match(outcome.summary, /Releases nothing, as the plan computed from /);
            }
          }
          assert.equal(repo.git("rev-parse", "HEAD"), run.rev);
          assert.equal(repo.onRemote("rev-parse", "main"), run.rev);
          assert.equal(repo.onRemote("tag", "--list"), "satellites-v0.2.1\nv0.2.1");
          assert.deepEqual(api.writes().filter((line) => line !== "POST /graphql"), []);
        } finally {
          repo.remove();
        }
      });
    }
  } finally {
    api.close();
  }
});

test("a release commits, tags and drafts the core, then the satellites past the core's release commit", async () => {
  const repo = repository();
  const api = await github();
  try {
    const coreFix = repo.commit("fix(a): a core fix", { "src/Core.A/A.cs": "class A {}\n" });
    const satelliteFix = repo.commit("fix(b): a satellite fix", { "src/Sat.B/B.cs": "class B {}\n" });
    const run = dispatch(repo);

    const core = await drive(repo, api, run, "core", { dryRun: false });
    succeeded(core);
    const first = repo.git("rev-parse", "HEAD");
    assert.deepEqual(core.outputs, { release: "true", version: "0.2.2", tag: "v0.2.2", commit: first, resumed: "false" });
    assert.equal(repo.git("rev-parse", `${first}^`), run.rev);
    assert.equal(tagged(repo, "v0.2.2"), first);
    // The first train to release commits the frozen documentation beside its changelog.
    assert.deepEqual(changed(repo, first), ["CHANGELOG.md", "website/released-versions.json",
      "website/versioned_docs/version-0.2.x/index.md", "website/versioned_docs/version-0.2.x/new.md",
      "website/versioned_docs/version-0.2.x/old.md", "website/versioned_sidebars/version-0.2.x-sidebars.json"]);
    assert.throws(() => read(join(repo.root, "website/versioned_docs/version-0.2.x/old.md")), "a page the snapshot dropped");

    const satellites = await drive(repo, api, run, "satellites", { dryRun: false });
    succeeded(satellites);
    const second = repo.git("rev-parse", "HEAD");
    assert.deepEqual(satellites.outputs,
      { release: "true", version: "0.2.2", tag: "satellites-v0.2.2", commit: second, resumed: "false" });
    assert.equal(repo.git("rev-parse", `${second}^`), first);
    assert.equal(tagged(repo, "satellites-v0.2.2"), second);
    assert.deepEqual(changed(repo, second), ["CHANGELOG.satellites.md"]);
    assert.equal(repo.onRemote("rev-parse", "main"), second);
    const changelog = read(join(repo.root, "CHANGELOG.satellites.md"));
    assert.ok(changelog.startsWith(`${changelog.slice(0, changelog.indexOf(MARKER) + MARKER.length)}\n\n## [0.2.2]`));
    assert.match(changelog, new RegExp(satelliteFix.slice(0, 7)));
    assert.doesNotMatch(changelog, new RegExp(coreFix.slice(0, 7)));

    // Each train's draft, with the train's files alone.
    const [coreRelease, satellitesRelease] = api.state.releases;
    assert.equal(coreRelease.tag_name, "v0.2.2");
    assert.equal(coreRelease.draft, true);
    assert.deepEqual(coreRelease.assets.map(({ name }) => name).sort(), ["Core.A.0.2.2.nupkg", "Core.A.0.2.2.snupkg"]);
    assert.match(coreRelease.body, /`AdCodicem\.ValueObjects\.0\.2\.2\.sigstore\.json`/);
    assert.match(coreRelease.body, /\| `Core\.A` \|/);
    assert.equal(satellitesRelease.tag_name, "satellites-v0.2.2");
    assert.deepEqual(satellitesRelease.assets.map(({ name }) => name), ["Sat.B.0.2.2.nupkg"]);
    assert.equal(satellitesRelease.assets[0].digest, `sha256:${sha256(read(join(run.packages, "Sat.B.0.2.2.nupkg")))}`);
    assert.match(satellitesRelease.body, /`AdCodicem\.ValueObjects\.satellites\.0\.2\.2\.sigstore\.json`/);
    assert.match(satellitesRelease.body, /Packed against AdCodicem\.ValueObjects 0\.2\.2\./);
    assert.equal(api.state.issues, 0);

    // A re-run of the whole job finds both trains released, and releases nothing again.
    rerun(repo, run);
    const writes = api.writes().length;
    for (const [train, commit] of [["core", first], ["satellites", second]]) {
      const again = await drive(repo, api, run, train, { dryRun: false });
      succeeded(again);
      assert.equal(again.outputs.resumed, "true");
      assert.equal(again.outputs.commit, commit);
    }
    assert.equal(api.writes().length, writes);
    assert.equal(repo.git("rev-parse", "HEAD"), run.rev, "a train resumed is not synced");
    // A dry run resumes nothing: the core's own release commit is no other train's.
    refused(await drive(repo, api, run, "core"),
      new RegExp(`main moved since the dispatch \\(${first.slice(0, 7)} chore\\(release\\): v0\\.2\\.2 \\[skip ci\\]: no other train`));
    assert.equal(api.writes().length, writes);
  } finally {
    api.close();
    repo.remove();
  }
});

test("a re-run tags a release commit pushed without its tag, and drafts its release", async () => {
  const repo = repository();
  const api = await github();
  try {
    repo.commit("fix(a): a core fix", { "src/Core.A/A.cs": "class A {}\n" });
    repo.commit("fix(b): a satellite fix", { "src/Sat.B/B.cs": "class B {}\n" });
    const run = dispatch(repo);
    // origin takes the release commit, then refuses the tag once.
    const hook = join(repo.remote, "hooks", "pre-receive");
    writeFileSync(hook, '#!/bin/sh\nwhile read old new ref; do\n  case "$ref" in refs/tags/*)\n' +
      '    if [ -f refuse-tags-once ]; then rm refuse-tags-once; echo "tags refused once" >&2; exit 1; fi ;;\n' +
      "  esac\ndone\n");
    chmodSync(hook, 0o755);
    writeFileSync(join(repo.remote, "refuse-tags-once"), "");
    refused(await drive(repo, api, run, "core", { dryRun: false }), /semantic-release failed for the core train, as its log above says: Command failed with exit code 1: git push --tags/);
    const first = repo.onRemote("rev-parse", "main");
    assert.equal(repo.onRemote("log", "-1", "--format=%s", "main"), "chore(release): v0.2.2 [skip ci]");
    assert.equal(tagged(repo, "v0.2.2"), undefined);
    assert.equal(api.state.releases.length, 0);

    // A new dispatch from main as the run left it would release v0.2.2 a second time: refused, a dry run included.
    repo.git("tag", "--delete", "v0.2.2");
    assert.equal(repo.git("rev-parse", "HEAD"), first);
    const fresh = dispatch(repo);
    assert.equal(fresh.rows.core.next, "0.2.2");
    for (const dryRun of [true, false]) {
      refused(await drive(repo, api, fresh, "core", { dryRun }),
        new RegExp(`${first.slice(0, 7)} chore\\(release\\): v0\\.2\\.2 \\[skip ci\\], which the dispatched commit holds, ` +
          "has no v0\\.2\\.2 on origin: complete that release with Re-run failed jobs of the run that made it"));
    }
    assert.equal(repo.onRemote("rev-parse", "main"), first);

    rerun(repo, run);
    refused(await drive(repo, api, run, "satellites", { dryRun: false }),
      new RegExp(`main moved since the dispatch \\(${first.slice(0, 7)} chore\\(release\\): v0\\.2\\.2 \\[skip ci\\]: ` +
        "v0\\.2\\.2 is not on it on origin\\)"));
    const core = await drive(repo, api, run, "core", { dryRun: false });
    succeeded(core);
    assert.match(core.log, /was on main without its tag: tagged it/);
    assert.deepEqual(core.outputs, { release: "true", version: "0.2.2", tag: "v0.2.2", commit: first, resumed: "true" });
    assert.equal(tagged(repo, "v0.2.2"), first);
    const [release] = api.state.releases;
    assert.equal(release.tag_name, "v0.2.2");
    assert.equal(release.draft, true);
    assert.deepEqual(release.assets.map(({ name }) => name).sort(), ["Core.A.0.2.2.nupkg", "Core.A.0.2.2.snupkg"]);
    assert.ok(release.body.startsWith(repo.git("log", "-1", "--format=%b", first)), "the release commit's notes");
    assert.match(release.body, /### NuGet packages/);

    // The satellites go on past the core's release commit, which the re-run's checkout lacks.
    assert.equal(repo.git("rev-parse", "HEAD"), run.rev);
    const satellites = await drive(repo, api, run, "satellites", { dryRun: false });
    succeeded(satellites);
    assert.equal(repo.git("rev-parse", `${satellites.outputs.commit}^`), first);
    assert.equal(tagged(repo, "satellites-v0.2.2"), satellites.outputs.commit);
  } finally {
    api.close();
    repo.remove();
  }
});

test("a re-run completes a train's draft, and refuses one another run released", async () => {
  const repo = repository();
  const api = await github();
  try {
    repo.commit("fix(a): a core fix", { "src/Core.A/A.cs": "class A {}\n" });
    const run = dispatch(repo);
    succeeded(await drive(repo, api, run, "core", { dryRun: false }));
    const [release] = api.state.releases;
    const symbols = release.assets.find(({ name }) => name === "Core.A.0.2.2.snupkg");

    // A configuration without its GitHub entry cannot complete a release.
    const config = JSON.parse(read(join(repo.root, CONFIG)));
    config.plugins = config.plugins.filter((plugin) => plugin[0] !== "@semantic-release/github");
    const withoutGitHub = join(repo.dir, "without-github.json");
    writeFileSync(withoutGitHub, JSON.stringify(config));
    rerun(repo, run);
    refused(await drive(repo, api, run, "core", { args: ["core", "--plan", run.plan, "--config", withoutGitHub, "--packages", run.packages] }),
      /The configuration has no @semantic-release\/github entry to draft v0\.2\.2's release with/);

    // A file missing, then a file left half uploaded: each is uploaded again.
    release.assets = release.assets.filter((asset) => asset !== symbols);
    rerun(repo, run);
    let again = await drive(repo, api, run, "core", { dryRun: false });
    succeeded(again);
    assert.match(again.log, /carries every file of the train, 1 uploaded now/);
    assert.deepEqual(release.assets.map(({ name }) => name).sort(), ["Core.A.0.2.2.nupkg", "Core.A.0.2.2.snupkg"]);
    const broken = release.assets.find(({ name }) => name === "Core.A.0.2.2.nupkg");
    broken.state = "starter";
    again = await drive(repo, api, run, "core", { dryRun: false });
    succeeded(again);
    assert.ok(api.state.requests.includes(`DELETE /repos/owner/repo/releases/assets/${broken.id}`));
    assert.equal(release.assets.find(({ name }) => name === "Core.A.0.2.2.nupkg").state, "uploaded");

    // A file at another digest is another run's.
    release.assets.find(({ name }) => name === "Core.A.0.2.2.snupkg").digest = `sha256:${"0".repeat(64)}`;
    refused(await drive(repo, api, run, "core", { dryRun: false }),
      /v0\.2\.2's draft holds Core\.A\.0\.2\.2\.snupkg at sha256:0{64}, not sha256:[0-9a-f]{64}, which this run packed: another run released it/);

    // Published, it is done, whatever its files.
    release.draft = false;
    const writes = api.writes().length;
    again = await drive(repo, api, run, "core", { dryRun: false });
    succeeded(again);
    assert.match(again.log, /v0\.2\.2's GitHub Release is published/);
    assert.equal(api.writes().length, writes);

    // A tag on another commit than this dispatch's release commit is another run's too.
    repo.onRemote("tag", "-f", "v0.2.2", run.rev);
    refused(await drive(repo, api, run, "core", { dryRun: false }),
      /v0\.2\.2 is on origin, on [0-9a-f]{7} fix\(a\): a core fix, but it does not descend from the dispatched commit: another run released it/);
  } finally {
    api.close();
    repo.remove();
  }
});

test("a train resumes whatever main holds since, and a later train needs main as the run left it", async () => {
  const repo = repository();
  const api = await github();
  try {
    repo.commit("fix(a): a core fix", { "src/Core.A/A.cs": "class A {}\n" });
    repo.commit("fix(b): a satellite fix", { "src/Sat.B/B.cs": "class B {}\n" });
    const run = dispatch(repo);
    succeeded(await drive(repo, api, run, "core", { dryRun: false }));
    // A merge to main between the trains.
    const merged = repo.commit("docs: merged meanwhile", { "docs/page.md": "# page\n" });
    repo.git("push", "--quiet", "origin", "main");
    refused(await drive(repo, api, run, "satellites", { dryRun: false }),
      new RegExp(`main moved since the dispatch \\(${merged.slice(0, 7)} docs: merged meanwhile: no other train of this ` +
        "release made it\\): dispatch release\\.yml again"));
    assert.equal(tagged(repo, "satellites-v0.2.2"), undefined);

    // Re-run: the core, tagged, is complete without a sync; the satellites still refuse.
    rerun(repo, run);
    const core = await drive(repo, api, run, "core", { dryRun: false });
    succeeded(core);
    assert.equal(core.outputs.resumed, "true");
    refused(await drive(repo, api, run, "satellites", { dryRun: true }), /main moved since the dispatch/);
  } finally {
    api.close();
    repo.remove();
  }
});

test("a train that releases nothing leaves main as it moved, and the documentation never stops a fast-forward", async () => {
  const repo = repository();
  const api = await github();
  try {
    repo.commit("fix(b): a satellite fix", { "src/Sat.B/B.cs": "class B {}\n" });
    const run = dispatch(repo);
    repo.commit("docs: merged meanwhile", { "docs/page.md": "# page\n" });
    repo.git("push", "--quiet", "origin", "main");
    repo.git("reset", "--quiet", "--hard", run.rev);
    const core = await drive(repo, api, run, "core");
    succeeded(core);
    assert.match(core.log, /::notice::main holds [0-9a-f]{7} docs: merged meanwhile since the dispatch \(no other train of this release made it\)\. The core train releases nothing, so semantic-release does not run for it/);
    assert.equal(core.outputs.release, "false");
    assert.match(core.summary, /Releases nothing; main holds other commits since the dispatch, so semantic-release did not run/);
    refused(await drive(repo, api, run, "satellites"), /main moved since the dispatch/);
  } finally {
    api.close();
    repo.remove();
  }

  // A re-run of a train that releases nothing goes past the release commit of a train that released after it.
  const later = repository();
  const api3 = await github();
  try {
    later.commit("fix(b): a satellite fix", { "src/Sat.B/B.cs": "class B {}\n" });
    const run = dispatch(later);
    succeeded(await drive(later, api3, run, "core", { dryRun: false }));
    const satellites = await drive(later, api3, run, "satellites", { dryRun: false });
    succeeded(satellites);
    rerun(later, run);
    const core = await drive(later, api3, run, "core", { dryRun: false });
    succeeded(core);
    assert.doesNotMatch(core.log, /main moved/);
    assert.match(core.summary, /Releases nothing, as the plan computed from v0\.2\.1\./);
    assert.equal(later.git("rev-parse", "HEAD"), satellites.outputs.commit);
    const again = await drive(later, api3, run, "satellites", { dryRun: false });
    succeeded(again);
    assert.equal(again.outputs.resumed, "true");
  } finally {
    api3.close();
    later.remove();
  }

  // The frozen documentation left unpacked by an earlier step: website/ is restored, then fast-forwarded to the core's
  // release commit, which holds the same files.
  const again = repository();
  const api2 = await github();
  try {
    again.commit("fix(a): a core fix", { "src/Core.A/A.cs": "class A {}\n" });
    again.commit("fix(b): a satellite fix", { "src/Sat.B/B.cs": "class B {}\n" });
    const run = dispatch(again);
    succeeded(await drive(again, api2, run, "core", { dryRun: false }));
    rerun(again, run);
    execFileSync("tar", ["-xf", run.documentation, "-C", join(again.root, "website")]);
    assert.notEqual(again.git("status", "--porcelain", "--", "website"), "");
    const satellites = await drive(again, api2, run, "satellites", { dryRun: false });
    succeeded(satellites);
    assert.deepEqual(changed(again, satellites.outputs.commit), ["CHANGELOG.satellites.md"]);
    assert.equal(again.git("status", "--porcelain", "--", "website"), "");
  } finally {
    api2.close();
    again.remove();
  }
});

test("the driver refuses a plan, a configuration or a release it cannot stand behind", async () => {
  const repo = repository();
  const api = await github();
  try {
    repo.commit("fix(a): a core fix", { "src/Core.A/A.cs": "class A {}\n" });
    const run = dispatch(repo);
    const plan = (edit) => {
      const file = join(repo.dir, `edited-${++files}.json`);
      writeFileSync(file, JSON.stringify(edit(Object.values(run.rows).map((row) => ({ ...row })))));
      return { ...run, plan: file };
    };
    const usage = /Usage: release-train\.mjs <train> --plan <file> --config <file>/;
    refused(await drive(repo, api, run, "core", { args: [] }), usage);
    refused(await drive(repo, api, run, "core", { args: ["core", "--plan", run.plan] }), usage);
    refused(await drive(repo, api, run, "core", { args: ["core", "--plan"] }), /--plan needs a value/);
    refused(await drive(repo, api, run, "core", { args: ["core", "--plan", run.plan, "--config", CONFIG, "--publish"] }),
      /Unknown option --publish/);
    refused(await drive(repo, api, run, "planets"), /planets is no train of src\/release-trains\.json/);
    refused(await drive(repo, api, run, "core", { env: { RELEASE_REV: undefined } }), /RELEASE_REV, '', is not the dispatched commit's hash/);
    refused(await drive(repo, api, run, "core", { env: { RELEASE_REV: "f".repeat(40) } }), /RELEASE_REV, f{40}, is no commit of this clone/);
    repo.commit("docs: a commit main does not hold", { "docs/local.md": "# local\n" });
    refused(await drive(repo, api, run, "core"),
      /HEAD, [0-9a-f]{7}, is neither the dispatched commit, [0-9a-f]{7}, nor a commit main holds since/);
    repo.git("reset", "--quiet", "--hard", run.rev);
    refused(await drive(repo, api, { ...run, plan: join(repo.dir, "none.json") }, "core"), /The plan, .*none\.json, cannot be read/);
    refused(await drive(repo, api, plan((rows) => rows.reverse()), "core"),
      /lists the trains 'satellites core', not those of src\/release-trains\.json, core satellites/);
    refused(await drive(repo, api, plan((rows) => rows.map((row) => ({ ...row, tag: "v0.2" }))), "core"),
      /The core train's plan names no last release of the train: 'v0\.2'/);
    refused(await drive(repo, api, plan((rows) => rows.map((row) => ({ ...row, type: "huge" }))), "core"),
      /The core train's plan releases 'huge'/);
    refused(await drive(repo, api, plan((rows) => rows.map((row) => ({ ...row, ids: "" }))), "core"),
      /The core train's plan lists none of its packages/);
    refused(await drive(repo, api, { ...run, packages: join(repo.dir, "empty") }, "core"),
      /Core\.A\.0\.2\.2\.nupkg, a package of the core train, is not in/);
    refused(await drive(repo, api, run, "core", { args: ["core", "--plan", run.plan, "--config", join(repo.dir, "none.json")] }),
      /The configuration, .*none\.json, cannot be read/);

    // semantic-release reads .releaserc.json beside the configuration given: a key only it holds would leak in.
    const releaserc = read(join(repo.root, ".releaserc.json"));
    repo.write(".releaserc.json", JSON.stringify({ ...JSON.parse(releaserc), preset: "angular" }));
    refused(await drive(repo, api, run, "core"), /\.releaserc\.json's preset would reach semantic-release beside/);
    repo.write(".releaserc.json", releaserc);

    // A configuration that extends another, and one that names no release commit.
    const configured = (edit) => {
      const file = join(repo.dir, `config-${++files}.json`);
      writeFileSync(file, JSON.stringify(edit(JSON.parse(read(join(repo.root, CONFIG))))));
      return file;
    };
    const dry = (config, given = run) => ["core", "--plan", given.plan, "--config", config, "--packages", given.packages, "--dry-run"];
    refused(await drive(repo, api, run, "core", { args: dry(configured((config) => ({ ...config, extends: "a-shared-config" }))) }),
      /config-\d+\.json extends another configuration: give it every option itself/);
    refused(await drive(repo, api, run, "core",
      { args: dry(configured((config) => ({ ...config, plugins: config.plugins.filter((plugin) => plugin[0] !== "@semantic-release/git") }))) }),
      /config-\d+\.json gives @semantic-release\/git no message and assets/);

    // Past a configuration without the plugin, whose checks would come first, the driver holds the release to the plan.
    const one = JSON.parse(releaserc);
    const plain = configured((config) => ({ ...config, plugins: [
      ...one.plugins.filter((plugin) => ["@semantic-release/commit-analyzer", "@semantic-release/release-notes-generator"].includes(plugin[0])),
      ...config.plugins.filter((plugin) => ["@semantic-release/git", "@semantic-release/github"].includes(plugin[0])),
    ] }));
    writeFileSync(join(run.packages, "Core.A.0.2.3.nupkg"), "Core.A 0.2.3 package\n");
    refused(await drive(repo, api, run, "core",
      { args: dry(plain, plan((rows) => rows.map((row) => (row.train === "core" ? { ...row, next: "0.2.3" } : row)))) }),
      /semantic-release released 0\.2\.2 of the core train, not 0\.2\.3, the version packed/);
    refused(await drive(repo, api, run, "core", { args: dry(plain, plan((rows) => rows.map((row) => (row.train === "core" ? { ...row, type: "" } : row)))) }),
      /semantic-release released 0\.2\.2 of the core train, which the plan does not release/);

    // A version other than the one packed, a release semantic-release does not compute, and one the plan lacks.
    refused(await drive(repo, api, plan((rows) => rows.map((row) => (row.train === "core" ? { ...row, next: "0.2.3" } : row))), "core"),
      /semantic-release failed for the core train, as its log above says: semantic-release computed 0\.2\.2 for the core train, but the pack job packed '0\.2\.3'/);
    const docsOnly = repository();
    try {
      docsOnly.commit("docs: a page", { "docs/page.md": "# page\n" });
      const quiet = dispatch(docsOnly, (rows) => rows.map((row) => (row.train === "core" ? { ...row, type: "patch", next: "0.2.2" } : row)));
      refused(await drive(docsOnly, api, quiet, "core"),
        /semantic-release releases nothing of the core train, where the plan releases 0\.2\.2: main moved since the dispatch/);
      const loud = repository();
      try {
        loud.commit("fix(a): a core fix", { "src/Core.A/A.cs": "class A {}\n" });
        const unplanned = dispatch(loud, (rows) => rows.map((row) => (row.train === "core" ? { ...row, type: "" } : row)));
        refused(await drive(loud, api, unplanned, "core"),
          /semantic-release failed for the core train, as its log above says: semantic-release computed 0\.2\.2 for the core train, but the pack job packed ''/);
      } finally {
        loud.remove();
      }
    } finally {
      docsOnly.remove();
    }
    assert.equal(repo.onRemote("tag", "--list"), "satellites-v0.2.1\nv0.2.1");
  } finally {
    api.close();
    repo.remove();
  }
});

test("a commit that only looks like a release commit is no train's, whatever its tag", async () => {
  const repo = repository();
  const api = await github();
  try {
    repo.commit("fix(a): a core fix", { "src/Core.A/A.cs": "class A {}\n" });
    repo.commit("fix(b): a satellite fix", { "src/Sat.B/B.cs": "class B {}\n" });
    const run = dispatch(repo);
    const subject = "chore(release): v0.2.2 [skip ci]";
    // main, and the core's tag, on a commit of the dispatch's own; the clone back at the dispatched commit.
    const place = (commit) => {
      repo.git("push", "--quiet", "--force", "origin", `${commit}:refs/heads/main`);
      repo.git("push", "--quiet", "--force", "origin", `${commit}:refs/tags/v0.2.2`);
      repo.git("reset", "--quiet", "--hard", run.rev);
    };
    const refusals = async (commit, fault) => {
      const at = commit.slice(0, 7);
      refused(await drive(repo, api, run, "satellites"),
        new RegExp(`main moved since the dispatch \\(${at} chore\\(release\\): v0\\.2\\.2 \\[skip ci\\]: ${fault}\\): ` +
          "dispatch release\\.yml again"));
      refused(await drive(repo, api, run, "core", { dryRun: false }),
        new RegExp(`v0\\.2\\.2 is on origin, on ${at} .*, but ${fault}: another run released it`));
    };

    // A commit writing more than a release commit writes.
    const wider = repo.commit(subject, { "CHANGELOG.md": "# Changelog\n", "src/Core.A/A.cs": "class A { }\n" });
    place(wider);
    await refusals(wider, "it changes src/Core\\.A/A\\.cs, which no release commit writes");

    // A merge, which a release commit never is.
    const changelog = repo.commit("a changelog", { "CHANGELOG.md": "# Changelog\n" });
    const merge = repo.git("commit-tree", `${changelog}^{tree}`, "-p", run.rev, "-p", repo.base, "-m", subject);
    place(merge);
    await refusals(merge, "it has more than one parent");

    // The core's tag on a commit with another subject: the sync never reads it, the resume refuses it.
    const page = repo.commit("docs: a page", { "docs/page.md": "# page\n" });
    place(page);
    refused(await drive(repo, api, run, "core", { dryRun: false }),
      new RegExp(`v0\\.2\\.2 is on origin, on ${page.slice(0, 7)} docs: a page, but its subject is not ` +
        "'chore\\(release\\): v0\\.2\\.2 \\[skip ci\\]': another run released it"));
    // A release commit in every way but one: main does not hold it.
    repo.git("push", "--quiet", "--force", "origin", `${run.rev}:refs/heads/main`);
    const aside = repo.commit(subject, { "CHANGELOG.md": "# Changelog\n" });
    repo.git("push", "--quiet", "--force", "origin", `${aside}:refs/tags/v0.2.2`);
    repo.git("reset", "--quiet", "--hard", run.rev);
    refused(await drive(repo, api, run, "core", { dryRun: false }),
      new RegExp(`v0\\.2\\.2 is on origin, on ${aside.slice(0, 7)} .*, but main does not hold it: another run released it`));

    // Untagged, none of them is a release commit either, and the resume tags none.
    repo.git("push", "--quiet", "origin", ":refs/tags/v0.2.2");
    repo.git("tag", "--delete", "v0.2.2");
    const untagged = async (commit, message) => {
      repo.git("push", "--quiet", "--force", "origin", `${commit}:refs/heads/main`);
      repo.git("reset", "--quiet", "--hard", run.rev);
      refused(await drive(repo, api, run, "core", { dryRun: false }), message);
      assert.equal(tagged(repo, "v0.2.2"), undefined);
    };
    await untagged(wider, new RegExp(`${wider.slice(0, 7)} chore\\(release\\): v0\\.2\\.2 \\[skip ci\\] is on main untagged, ` +
      "but it changes src/Core\\.A/A\\.cs, which no release commit writes"));
    await untagged(merge, /is on main untagged, but it has more than one parent/);
    const once = repo.commit(subject, { "CHANGELOG.md": "# Changelog\n\nonce\n" });
    const twice = repo.commit(subject, { "CHANGELOG.md": "# Changelog\n\ntwice\n" });
    await untagged(twice, /main holds 2 commits 'chore\(release\): v0\.2\.2 \[skip ci\]' since the dispatched commit/);
    // One, while the clone names the tag on another commit.
    repo.git("tag", "v0.2.2", run.rev);
    await untagged(once, new RegExp(`v0\\.2\\.2 names ${run.rev.slice(0, 7)} here, not ${once.slice(0, 7)}`));
    assert.equal(api.state.releases.length, 0);
  } finally {
    api.close();
    repo.remove();
  }
});

test("a release that leaves no release commit is refused, though semantic-release tagged it", async () => {
  const repo = repository();
  const api = await github();
  try {
    repo.commit("fix(a): a core fix", { "src/Core.A/A.cs": "class A {}\n" });
    const run = dispatch(repo);
    // A configuration whose release commit would carry nothing: semantic-release tags the dispatched commit itself.
    const config = JSON.parse(read(join(repo.root, CONFIG)));
    config.plugins = config.plugins.map((plugin) =>
      (plugin[0] === "@semantic-release/git" ? [plugin[0], { ...plugin[1], assets: ["nothing.md"] }] : plugin));
    const file = join(repo.dir, "commits-nothing.json");
    writeFileSync(file, JSON.stringify(config));
    refused(await drive(repo, api, run, "core", { args: ["core", "--plan", run.plan, "--config", file, "--packages", run.packages] }),
      new RegExp(`HEAD, ${run.rev.slice(0, 7)}, is no release commit of v0\\.2\\.2: its subject is not 'chore\\(release\\): v0\\.2\\.2 ` +
        "\\[skip ci\\]'"));
    assert.equal(tagged(repo, "v0.2.2"), run.rev);
  } finally {
    api.close();
    repo.remove();
  }
});

test("a re-run refuses the release a later dispatch made of the same version", async () => {
  const repo = repository();
  const api = await github();
  try {
    repo.commit("fix(a): a core fix", { "src/Core.A/A.cs": "class A {}\n" });
    const earlier = dispatch(repo);
    // The run stops ("main moved"), and the next dispatch, at the commit merged since, releases the same version.
    const merged = repo.commit("docs: merged meanwhile", { "docs/page.md": "# page\n" });
    const later = dispatch(repo);
    succeeded(await drive(repo, api, later, "core", { dryRun: false }));
    const released = repo.git("rev-parse", "HEAD");
    rerun(repo, earlier);
    refused(await drive(repo, api, earlier, "core", { dryRun: false }),
      new RegExp(`v0\\.2\\.2 is on origin, on ${released.slice(0, 7)} chore\\(release\\): v0\\.2\\.2 \\[skip ci\\], but it follows ` +
        `${merged.slice(0, 7)} docs: merged meanwhile \\(no other train of this release made it\\): another run released it`));
    assert.equal(api.state.releases.length, 1);
  } finally {
    api.close();
    repo.remove();
  }
});

test("the driver's git reaches origin with the release App's token, from its environment alone", async () => {
  const repo = repository();
  const api = await github();
  // An origin served over HTTP, which records the authorization of each request git makes, and refuses it.
  const seen = [];
  const origin = createServer((request, response) => {
    seen.push(request.headers.authorization ?? "none");
    response.writeHead(403, { "content-type": "text/plain" });
    response.end("refused\n");
  });
  await new Promise((done) => origin.listen(0, "127.0.0.1", done));
  try {
    repo.commit("fix(a): a core fix", { "src/Core.A/A.cs": "class A {}\n" });
    const run = dispatch(repo);
    const server = `http://127.0.0.1:${origin.address().port}`;
    repo.git("remote", "set-url", "origin", `${server}/owner/repo.git`);
    const outcome = await drive(repo, api, run, "core", { env: { GITHUB_SERVER_URL: server } });
    assert.notEqual(outcome.code, 0, outcome.log);
    assert.ok(seen.length, outcome.log);
    assert.deepEqual([...new Set(seen)], [`basic ${Buffer.from("x-access-token:test-token").toString("base64")}`]);
    seen.length = 0;
    await drive(repo, api, run, "core", { env: { GITHUB_SERVER_URL: server, GITHUB_TOKEN: undefined } });
    assert.deepEqual([...new Set(seen)], ["none"]);
  } finally {
    origin.close();
    api.close();
    repo.remove();
  }
});

test("a new dispatch over any train's release commit left untagged is refused before any train is released", async () => {
  const repo = repository();
  const api = await github();
  try {
    repo.commit("fix(a): a core fix", { "src/Core.A/A.cs": "class A {}\n" });
    repo.commit("fix(b): a satellite fix", { "src/Sat.B/B.cs": "class B {}\n" });
    const run = dispatch(repo);
    succeeded(await drive(repo, api, run, "core", { dryRun: false }));
    // origin takes the satellites' release commit, then refuses its tag.
    const hook = join(repo.remote, "hooks", "pre-receive");
    writeFileSync(hook, '#!/bin/sh\nwhile read old new ref; do\n  case "$ref" in refs/tags/*) echo "tags refused" >&2; exit 1 ;; esac\ndone\n');
    chmodSync(hook, 0o755);
    refused(await drive(repo, api, run, "satellites", { dryRun: false }), /git push --tags/);
    rmSync(hook);
    const second = repo.onRemote("rev-parse", "main");
    assert.equal(repo.onRemote("log", "-1", "--format=%s", "main"), "chore(release): satellites-v0.2.2 [skip ci]");

    // A core fix merged since, and a new dispatch: the core's step, first, refuses for the satellites.
    repo.git("tag", "--delete", "satellites-v0.2.2");
    repo.commit("fix(a): another core fix", { "src/Core.A/A.cs": "class A { }\n" });
    const fresh = dispatch(repo);
    assert.equal(fresh.rows.core.next, "0.2.3");
    for (const dryRun of [true, false]) {
      refused(await drive(repo, api, fresh, "core", { dryRun }),
        new RegExp(`${second.slice(0, 7)} chore\\(release\\): satellites-v0\\.2\\.2 \\[skip ci\\], which the dispatched commit holds, ` +
          "has no satellites-v0\\.2\\.2 on origin"));
    }
    assert.equal(tagged(repo, "v0.2.3"), undefined);
    assert.equal(api.state.releases.length, 1);
  } finally {
    api.close();
    repo.remove();
  }
});
