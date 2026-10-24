// Tests of docs-snapshot.sh: node --test .github/scripts/docs-snapshot.test.mjs (ci.yml's job "release tooling").
//
// The script runs with DOCS_SNAPSHOT_ENTRY_ONLY=true, its seam: it writes the line's entry of
// website/released-versions.json and stops before DocFX and Docusaurus, so each case runs in a throwaway repository
// holding that file and a list of trains, and reads back what the release commit would carry. Both are fixtures, not
// the repository's own files: every stable release rewrites released-versions.json, and the list changes whenever a
// project moves between trains.
import { test } from "node:test";
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { chmodSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { delimiter, dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const scripts = dirname(fileURLToPath(import.meta.url));
const LIST = `${JSON.stringify({
  core: { breaking: "minor", projects: ["Pkg.A"] },
  satellites: { breaking: "minor", projects: "*" },
})}\n`;
const RELEASED = '{\n  "0.2.x": "0.2.1"\n}\n';

/** A throwaway repository holding a list of trains and website/, with or without released-versions.json. */
function tree({ list = LIST, released = RELEASED } = {}) {
  const root = mkdtempSync(join(tmpdir(), "docs-snapshot-"));
  try {
    const run = (...args) => spawnSync("git", args, { cwd: root, encoding: "utf8" });
    if (run("init", "--quiet").status !== 0) throw new Error("git init failed");
    mkdirSync(join(root, "website"));
    for (const [path, text] of [["src/release-trains.json", list], ["website/released-versions.json", released]]) {
      if (text === null) continue;
      mkdirSync(dirname(join(root, path)), { recursive: true });
      writeFileSync(join(root, path), text);
    }
    return root;
  } catch (error) {
    rmSync(root, { recursive: true, force: true });
    throw error;
  }
}

/**
 * Runs the script in a fresh tree, from its root or from the folder `cwd` names, and gives its exit status, its
 * output and the file it leaves. `env` is added to the environment, where a value of undefined removes a variable.
 */
function snapshot(argument, { cwd = ".", env = {}, setup, ...options } = {}) {
  const root = tree(options);
  try {
    const environment = { ...process.env, DOCS_SNAPSHOT_ENTRY_ONLY: "true", ...(setup?.(root) ?? {}), ...env };
    for (const [name, value] of Object.entries(environment)) {
      if (value === undefined) delete environment[name];
    }
    const { status, stdout, stderr } = spawnSync("bash", [join(scripts, "docs-snapshot.sh"), argument], {
      cwd: join(root, cwd),
      encoding: "utf8",
      env: environment,
    });
    const file = join(root, "website", "released-versions.json");
    let text = null;
    try {
      text = readFileSync(file, "utf8");
    } catch {
      // The script wrote no file.
    }
    return { status, stdout, stderr, text };
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

const released = (entries) => `${JSON.stringify(entries, null, 2)}\n`;

function assertWrites(argument, entries, options) {
  const { status, stderr, text } = snapshot(argument, options);
  assert.equal(status, 0, `${argument}: ${stderr}`);
  assert.equal(text, released(entries), argument);
}

function assertRefuses(argument, error, options) {
  const { status, stderr, text } = snapshot(argument, options);
  assert.notEqual(status, 0, `${argument} was accepted`);
  assert.match(stderr, error, argument);
  assert.equal(text, options?.released === undefined ? RELEASED : options.released, `${argument} changed the file`);
}

test("one version writes the line's entry as that version, as every release before the trains did", () => {
  assertWrites("0.3.0", { "0.2.x": "0.2.1", "0.3.x": "0.3.0" });
  assertWrites("0.2.4", { "0.2.x": "0.2.4" });
  assertWrites("1.2.0", { "0.2.x": "0.2.1", "1.x": "1.2.0" });
  assertWrites("0.3.0", { "0.3.x": "0.3.0" }, { released: null });
});

test("a version per train writes the line's entry as each train's version, the core first, then in the list's order", () => {
  assertWrites("core=0.3.2 satellites=0.3.2", { "0.2.x": "0.2.1", "0.3.x": { core: "0.3.2", satellites: "0.3.2" } });
  assertWrites("satellites=0.2.3 core=0.2.2", { "0.2.x": { core: "0.2.2", satellites: "0.2.3" } });
  assertWrites(" core=0.3.0\tsatellites=0.3.1 ", { "0.2.x": "0.2.1", "0.3.x": { core: "0.3.0", satellites: "0.3.1" } });
  const three = JSON.stringify({
    satellites: { breaking: "minor", projects: "*" },
    extras: { breaking: "major", projects: ["Pkg.C"] },
    core: { breaking: "minor", projects: ["Pkg.A"] },
  });
  assertWrites("satellites=0.4.0 extras=2.0.0 core=0.3.0",
    { "0.2.x": "0.2.1", "0.3.x": { core: "0.3.0", satellites: "0.4.0", extras: "2.0.0" } }, { list: three });
});

test("the line follows the core's version, whatever the other trains' are", () => {
  assertWrites("core=0.3.2 satellites=0.4.1", { "0.2.x": "0.2.1", "0.3.x": { core: "0.3.2", satellites: "0.4.1" } });
  assertWrites("core=1.0.0 satellites=0.5.0", { "0.2.x": "0.2.1", "1.x": { core: "1.0.0", satellites: "0.5.0" } });
  assertWrites("core=0.2.1 satellites=1.3.0", { "0.2.x": { core: "0.2.1", satellites: "1.3.0" } });
});

test("a prerelease freezes nothing, in either form", () => {
  assertRefuses("0.3.0-preview.1", /::error::'0\.3\.0-preview\.1' is not a stable version/);
  assertRefuses("v0.3.0", /is not a stable version/);
  assertRefuses("core=0.3.0 satellites=0.4.0-preview.2", /::error::'0\.4\.0-preview\.2', the satellites train's version, is not a stable/);
  assertRefuses("core=0.3.0-preview.1 satellites=0.4.0", /::error::'0\.3\.0-preview\.1', the core train's version, is not a stable/);
  assertRefuses("core=0.3.0 satellites==0.4.0", /'=0\.4\.0', the satellites train's version, is not a stable/);
  assertRefuses("core=0.3.0 satellites=", /is not <train>=<version>/);
});

test("a train the list lacks, a train named twice and a train left out are refused", () => {
  assertRefuses("core=0.3.0", /::error::no version for the satellites train/);
  assertRefuses("satellites=0.4.0", /::error::no version for the core train/);
  assertRefuses("core=0.3.0 satellites=0.4.0 extras=1.0.0", /::error::extras is no train of the list/);
  assertRefuses("core=0.3.0 core=0.3.1 satellites=0.4.0", /::error::core is given two versions/);
  assertRefuses("Core=0.3.0 satellites=0.4.0", /::error::Core is no train of the list/);
  assertRefuses("core=0.3.0 satellites=0.4.0", /::error::.*release-trains\.json/, { list: "{\n" });
});

test("the entry lands in the repository's website/, wherever the script is run from", () => {
  assertWrites("0.3.0", { "0.2.x": "0.2.1", "0.3.x": "0.3.0" }, { cwd: "website" });
  assertWrites("core=0.3.0 satellites=0.4.0", { "0.2.x": "0.2.1", "0.3.x": { core: "0.3.0", satellites: "0.4.0" } },
    { cwd: "src" });
});

test("without the seam, the entry is written before DocFX runs", () => {
  // A dotnet that fails stops the script at its first DocFX step, `dotnet tool restore`.
  const setup = (root) => {
    const bin = join(root, "bin");
    mkdirSync(bin);
    writeFileSync(join(bin, "dotnet"), "#!/usr/bin/env bash\nexit 3\n");
    chmodSync(join(bin, "dotnet"), 0o755);
    return { PATH: `${bin}${delimiter}${process.env.PATH}` };
  };
  const { status, stdout, text } = snapshot("core=0.3.0 satellites=0.4.0", { setup, env: { DOCS_SNAPSHOT_ENTRY_ONLY: undefined } });
  assert.notEqual(status, 0, "the script went past a failing dotnet");
  assert.match(stdout, /Generating the API reference for core=0\.3\.0 satellites=0\.4\.0\./);
  assert.equal(text, released({ "0.2.x": "0.2.1", "0.3.x": { core: "0.3.0", satellites: "0.4.0" } }));
});
