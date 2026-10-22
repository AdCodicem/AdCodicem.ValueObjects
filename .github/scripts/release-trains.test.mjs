// Tests of release-trains.mjs: node --test .github/scripts/release-trains.test.mjs (ci.yml's job "release tooling").
import { test } from "node:test";
import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  changelogOf, parseTrains, parseVersions, restTrain, tagPrefixOf, trainOf, validate,
} from "./release-trains.mjs";

const script = fileURLToPath(new URL("./release-trains.mjs", import.meta.url));
const src = join(dirname(script), "..", "..", "src");
const list = readFileSync(join(src, "release-trains.json"), "utf8");

const LIST = JSON.stringify({
  $comment: "A list of two trains.",
  core: { breaking: "minor", projects: ["Core.A", "Core.B"] },
  satellites: { projects: "*", breaking: "minor" },
});

/** A src/ folder holding the given projects, each { references, packageId }, and the list. */
function tree(projects, text = LIST) {
  const root = mkdtempSync(join(tmpdir(), "release-trains-"));
  for (const [name, { references = [], packageId = false } = {}] of Object.entries(projects)) {
    mkdirSync(join(root, name));
    const items = references.map((reference) => `    <ProjectReference Include="..\\${reference}\\${reference}.csproj" />`);
    writeFileSync(join(root, name, `${name}.csproj`), [
      '<Project Sdk="Microsoft.NET.Sdk">',
      packageId ? `  <PropertyGroup><PackageId>${name}.Renamed</PackageId></PropertyGroup>` : "",
      "  <ItemGroup>", ...items, "  </ItemGroup>",
      "</Project>",
    ].join("\r\n"));
  }
  writeFileSync(join(root, "release-trains.json"), text);
  return root;
}

const run = (...args) => spawnSync(process.execPath, [script, ...args], { encoding: "utf8" });

// MSBuild's reading, src/Directory.Build.props: the innermost object holding the quoted name, else the "*" train.
function msbuildTrainOf(text, project) {
  const escaped = project.replace(/[\\^$.*+?()[\]{}|]/g, "\\$&");
  const named = new RegExp(`"([a-z][a-z0-9-]*)"\\s*:\\s*\\{[^{}]*"${escaped}"`).exec(text);
  if (named) return named[1];
  return /"([a-z][a-z0-9-]*)"\s*:\s*\{[^{}]*"projects"\s*:\s*"\*"/.exec(text)?.[1];
}

test("the repository's list holds, and MSBuild reads every project's train as the module does", () => {
  assert.deepEqual(validate(list, src), []);
  const model = parseTrains(list);
  assert.equal(model.names[0], "core");
  for (const project of readdirSync(src, { withFileTypes: true }).filter((entry) => entry.isDirectory() && entry.name !== "Shared")) {
    assert.equal(msbuildTrainOf(list, project.name), trainOf(model, project.name), project.name);
  }
  assert.equal(trainOf(model, "AdCodicem.ValueObjects"), "core");
  assert.equal(trainOf(model, "AdCodicem.ValueObjects.Generators"), "core");
  assert.equal(trainOf(model, "AdCodicem.ValueObjects.AI"), "satellites");
});

test("MSBuild's reading ignores the order of the keys and the layout", () => {
  const shuffled = '{"satellites":{"breaking":"minor","projects":"*"},"core":{"projects":["Core.A","Core.B"],"breaking":"major"}}';
  for (const text of [LIST, shuffled, JSON.stringify(JSON.parse(LIST), null, 4).replaceAll("\n", "\r\n")]) {
    assert.deepEqual(validate(text), []);
    for (const project of ["Core.A", "Core.B", "Core", "Core.A.Other", "Sat"]) {
      assert.equal(msbuildTrainOf(text, project), trainOf(parseTrains(text), project), project);
    }
  }
});

test("a project the list does not name rides the * train, by its exact name", () => {
  const model = parseTrains(LIST);
  assert.equal(restTrain(model), "satellites");
  assert.equal(trainOf(model, "Core.A"), "core");
  assert.equal(trainOf(model, "Core.A.Other"), "satellites");
  assert.equal(trainOf(model, "Core"), "satellites");
});

test("the tags and changelogs follow the train", () => {
  assert.equal(tagPrefixOf("core"), "v");
  assert.equal(tagPrefixOf("satellites"), "satellites-v");
  assert.equal(changelogOf("core"), "CHANGELOG.md");
  assert.equal(changelogOf("satellites"), "CHANGELOG.satellites.md");
});

test("a shape MSBuild could misread is refused", () => {
  const faults = (value) => validate(JSON.stringify(value)).join("\n");
  const core = { breaking: "minor", projects: ["Core.A"] };
  const rest = { breaking: "minor", projects: "*" };
  assert.match(faults({ core: { ...core, family: { projects: ["X"] } }, satellites: rest }), /core\.family: a train has a breaking rule/);
  assert.match(faults({ $comment: "see {this}", core, satellites: rest }), /brace/);
  assert.match(faults({ $comment: "uses $(Version)", core, satellites: rest }), /MSBuild would expand/);
  assert.match(faults({ $comment: "100%25", core, satellites: rest }), /MSBuild would expand/);
  assert.match(faults({ satellites: rest }), /no core train/);
  assert.match(faults({ core, satellites: rest, more: rest }), /2 trains take the projects no train names/);
  assert.match(faults({ core: { ...core, projects: ["Core.A", "Core.A"] }, satellites: rest }), /Core\.A is in core and in core/);
  assert.match(faults({ core, satellites: { breaking: "minor", projects: ["Core.A"] }, rest }), /Core\.A is in core and in satellites/);
  assert.match(faults({ core: { ...core, breaking: "patch" }, satellites: rest }), /core\.breaking is "patch"/);
  assert.match(faults({ Core: core, core, satellites: rest }), /"Core" is not a train name/);
  assert.match(faults({ core: { ...core, projects: [] }, satellites: rest }), /core\.projects is neither/);
  assert.match(faults({ $comment: 3, core, satellites: rest }), /\$comment is neither/);
  assert.match(validate("[1]").join(), /not a train list/);
  assert.match(validate("{").join(), /not a train list/);
});

test("the list must agree with the projects under src/", () => {
  const root = tree({
    "Core.A": {},
    "Core.B": { references: ["Core.A", "Sat"] },
    Sat: { references: ["Core.A"], packageId: true },
  }, JSON.stringify({ core: { breaking: "minor", projects: ["Core.A", "Core.B", "Core.Gone"] }, satellites: { breaking: "minor", projects: "*" } }));
  try {
    const faults = validate(readFileSync(join(root, "release-trains.json"), "utf8"), root).join("\n");
    assert.match(faults, /core names Core\.Gone, which is no project/);
    assert.match(faults, /Core\.B \(core\) references Sat \(satellites\)/);
    assert.match(faults, /src\/Sat\/Sat\.csproj sets a PackageId/);
    assert.doesNotMatch(faults, /Sat \(satellites\) references/);
  } finally {
    rmSync(root, { recursive: true });
  }
});

test("versions give each project its train's, and refuse a train left out or unknown", () => {
  const model = parseTrains(LIST);
  const versions = parseVersions(model, " core=1.2.0  satellites=0.4.0-preview.3 ");
  assert.equal(versions.get("core"), "1.2.0");
  assert.equal(versions.get("satellites"), "0.4.0-preview.3");
  assert.throws(() => parseVersions(model, "core=1.2.0"), /no version for the satellites train/);
  assert.throws(() => parseVersions(model, "core=1.2.0 satellites=1 other=2"), /other is no train/);
  assert.throws(() => parseVersions(model, "core=1 core=2 satellites=3"), /core is given two versions/);
  assert.throws(() => parseVersions(model, "core satellites=3"), /is not <train>=<version>/);
});

test("the command line answers each command, and fails closed", () => {
  const root = tree({ "Core.A": {}, "Core.B": {}, Sat: { references: ["Core.A"] } });
  const at = join(root, "release-trains.json");
  try {
    assert.equal(run("--list", at, "check").status, 0);
    assert.equal(run("--list", at, "trains").stdout, "core v minor\nsatellites satellites-v minor\n");
    assert.equal(run("--list", at, "trains-of", "Core.B", "Sat").stdout, "Core.B core\nSat satellites\n");
    assert.equal(run("--list", at, "versions", "core=1.0.0 satellites=0.2.0", "Core.A", "Sat").stdout,
      "Core.A=1.0.0\nSat=0.2.0\n");
    const missing = run("--list", at, "versions", "core=1.0.0", "Sat");
    assert.equal(missing.status, 1);
    assert.match(missing.stderr, /::error::no version for the satellites train/);
    assert.equal(run("--list", at, "nothing").status, 2);
    writeFileSync(at, JSON.stringify({ core: { breaking: "minor", projects: ["Core.A"] } }));
    const broken = run("--list", at, "check");
    assert.equal(broken.status, 1);
    assert.match(broken.stderr, /::error::src\/release-trains\.json: 0 trains take/);
    assert.equal(run("--list", at, "trains").status, 1);
  } finally {
    rmSync(root, { recursive: true });
  }
});

test("the repository's own command line reads the repository's list", () => {
  const out = execFileSync(process.execPath, [script, "trains-of", "AdCodicem.ValueObjects.Json", "AdCodicem.ValueObjects.Marten"], { encoding: "utf8" });
  assert.equal(out, "AdCodicem.ValueObjects.Json core\nAdCodicem.ValueObjects.Marten satellites\n");
});
