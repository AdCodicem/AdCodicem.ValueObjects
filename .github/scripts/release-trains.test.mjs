// Tests of release-trains.mjs: node --test .github/scripts/release-trains.test.mjs (ci.yml's job "release tooling").
import { test } from "node:test";
import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  analyzerRules, changelogOf, DEPENDENCIES, git, GLOBAL_INPUTS, lineOf, lintCommits, lintSummary, packageVersionTrains,
  parseTrains, parseVersions, previousCoreVersion, readModel, releases, releaseType, restTrain, solutionFilterTrains,
  tagPrefixOf, trainOf, trainsOfCommits, trainsOfPath, validate,
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

// Under Actions, a command writing the job summary would add the tests' throwaway commits to the real one.
const quiet = { ...process.env, GITHUB_STEP_SUMMARY: "" };
const run = (...args) => spawnSync(process.execPath, [script, ...args], { encoding: "utf8", env: quiet });

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

test("an item compiles a file outside its folder only as one literal src/Shared link on one line", () => {
  const root = tree({ "Core.A": {}, "Core.B": {}, Sat: {} });
  const csproj = join(root, "Sat", "Sat.csproj");
  const items = (item) => writeFileSync(csproj, `<Project Sdk="Microsoft.NET.Sdk">\n  <ItemGroup>\n    ${item}\n  </ItemGroup>\n</Project>`);
  try {
    items('<Compile Include="..\\Shared\\Text.cs" Link="Shared\\Text.cs" />');
    assert.deepEqual(validate(LIST, root), []);
    items('<Compile Include="../Shared/Deep/Text.cs" Link="Shared/Text.cs" />');
    assert.deepEqual(validate(LIST, root), []);
    // An item of the project's own folder releases the project's train, whatever its words.
    for (const item of [
      '<Compile Update="Properties\\SharedResources.Designer.cs" DesignTime="True" DependentUpon="SharedResources.resx" />',
      '<Compile Remove="..\\Shared\\Text.cs" />',
      '<Compile Include="Shared\\Local.cs" />',
      '<Compile Include="Other.cs" Link="Shared\\Other.cs" />',
      '<Compile Include="Generated\\*.cs" />',
      '<Compile\n      Include="Local.cs" />',
      `<Compile Include="..\\Shared\\Text.cs" Link="Shared\\Text.cs" Condition="'$(Level)' >= '13'" />`,
      `<Compile Condition="'$(Level)' >= '13'" Include="Local.cs" />`,
    ]) {
      items(item);
      assert.deepEqual(validate(LIST, root), [], item);
    }
    for (const item of [
      '<Compile Include="..\\Shared\\Text*.cs" Link="Shared\\%(Filename)%(Extension)" />',
      '<Compile Include="$(MSBuildThisFileDirectory)..\\Shared\\Text.cs" />',
      '<Compile\n      Include="..\\Shared\\Text.cs" />',
      '<Compile Include="..\\Shared\\A.cs;..\\Shared\\B.cs" />',
      "<Compile Include='..\\Shared\\Text.cs' />",
      '<Compile Include="..\\..\\src\\Shared\\Text.cs" />',
      '<Compile Include="$(Common)Text.cs" Link="Shared\\Text.cs" />',
      '<Compile Include="$(Common)Text.cs" />',
      '<Compile Include="@(Linked)" />',
      '<Compile Include="..\\Core.A\\A.cs" Link="A.cs" />',
      `<Compile Condition="'$(Level)' >= '13'" Include="..\\Shared\\Text.cs" Link="Shared\\Text.cs" />`,
      '<Compile Include="..\\Shared\\..\\Core.A\\A.cs" />',
      '<Compile Include="..\\Shared\\.\\Text.cs" />',
      '<Compile Include="../Shared/Deep/../../Core.A/A.cs" />',
      '<Compile Include="..\\Shared\\\\Text.cs" />',
      '<Compile Include="..\\Shared\\Text.cs " />',
    ]) {
      items(item);
      assert.match(validate(LIST, root).join("\n"),
        /src\/Sat\/Sat\.csproj compiles .*: an item compiles a file outside its project's folder only as one literal/, item);
    }
    // Nor through a file MSBuild imports.
    items('<Compile Include="Local.cs" />\n  </ItemGroup>\n  <Import Project="Links.props" />\n  <ItemGroup>');
    assert.match(validate(LIST, root).join("\n"), /src\/Sat\/Sat\.csproj imports a file/);
    items('<Compile Include="Local.cs" />');
    writeFileSync(join(root, "Sat", "Directory.Build.props"), "<Project />");
    assert.match(validate(LIST, root).join("\n"), /src\/Sat\/Directory\.Build\.props: the trains read a project's items/);
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

// Attribution: which trains a change releases (0012).

/** A model built by hand: two core projects, a satellite, a Shared file each train links, and a restore graph. */
function handModel({ graph = true } = {}) {
  const model = parseTrains(LIST);
  const projects = new Map(["Core.A", "Core.B", "Sat", "Fixtures.Host"].slice(0, 3).map((name) => [name, {}]));
  const links = new Map([
    ["src/Shared/Both.cs", new Set(["core", "satellites"])],
    ["src/Shared/Sat.cs", new Set(["satellites"])],
  ]);
  const packages = new Map([
    ["roslyn", new Set(["core", "satellites"])],
    ["fluentvalidation", new Set(["satellites"])],
  ]);
  return { model, projects, links, graph: graph ? packages : null };
}

const set = (...trains) => new Set(trains);

const PROPS = (entries, property = "true") => [
  "<Project>",
  `  <PropertyGroup><ManagePackageVersionsCentrally>${property}</ManagePackageVersionsCentrally></PropertyGroup>`,
  "  <ItemGroup>",
  ...entries.map((entry) => `    ${entry}`),
  "  </ItemGroup>",
  "</Project>",
].join("\n");

test("each path rule releases its trains, the first that matches winning", () => {
  const m = handModel();
  assert.deepEqual(trainsOfPath(m, "src/Core.A/CLAUDE.md"), set());
  assert.deepEqual(trainsOfPath(m, "src/CLAUDE.md"), set());
  assert.deepEqual(trainsOfPath(m, "src/Core.A/AnalyzerReleases.Unshipped.md"), set());
  for (const path of [...GLOBAL_INPUTS, "nuget.config", "NuGet.Config"]) assert.deepEqual(trainsOfPath(m, path), set("core", "satellites"), path);
  assert.deepEqual(trainsOfPath(m, "src/Shared/Both.cs"), set("core", "satellites"));
  assert.deepEqual(trainsOfPath(m, "src/Shared/Sat.cs"), set("satellites"));
  assert.deepEqual(trainsOfPath(m, "src/Shared/Nobody.cs"), set("core", "satellites"));
  assert.deepEqual(trainsOfPath(m, "src/Core.B/Thing.cs"), set("core"));
  assert.deepEqual(trainsOfPath(m, "src/Sat/Deep/Thing.cs"), set("satellites"));
  assert.deepEqual(trainsOfPath(m, "src/Gone/Gone.csproj"), set("core", "satellites"));
  assert.deepEqual(trainsOfPath(m, "src/Loose.md"), set("core", "satellites"));
  for (const path of ["README.md", "tests/Core.A.Tests/Test.cs", ".github/workflows/ci.yml", "docs/adr/0012.md", "website/x"]) {
    assert.deepEqual(trainsOfPath(m, path), set(), path);
  }
});

test("a changed PackageVersion releases the trains whose restore graph holds its package", () => {
  const m = handModel();
  const before = PROPS([
    '<PackageVersion Include="Roslyn" Version="4.14.0" />',
    '<PackageVersion Include="FluentValidation" Version="12.1.0" />',
    '<!-- a comment -->',
  ]);
  const bumped = before.replace('"12.1.0"', '"12.2.0"');
  assert.deepEqual(packageVersionTrains(m, before, bumped), set("satellites"));
  assert.deepEqual(trainsOfPath(m, "Directory.Packages.props", before, bumped), set("satellites"));
  assert.deepEqual(packageVersionTrains(m, before, before.replace('"4.14.0"', '"4.15.0"')), set("core", "satellites"));
  // Reordered, re-indented, a comment edited: nothing changed.
  const reordered = PROPS([
    '<!-- another comment, <PackageVersion Include="Roslyn" Version="9.0.0" /> -->',
    '<PackageVersion   Include="FluentValidation"\n      Version="12.1.0" />',
    '<PackageVersion Include="Roslyn" Version="4.14.0" />',
  ]);
  assert.deepEqual(packageVersionTrains(m, before, reordered), set());
  // preview-gate.sh collapses an entry's whitespace without trimming it: a space dropped before /> is a change there.
  assert.deepEqual(packageVersionTrains(m, before, before.replace('"12.1.0" />', '"12.1.0"/>')), set("satellites"));
  // A property outside the PackageVersion elements: every train.
  assert.deepEqual(packageVersionTrains(m, before, before.replace(">true<", ">false<")), set("core", "satellites"));
  // A package no graph holds: nothing; an added entry counts as changed.
  assert.deepEqual(packageVersionTrains(m, before, PROPS([
    '<PackageVersion Include="Roslyn" Version="4.14.0" />',
    '<PackageVersion Include="FluentValidation" Version="12.1.0" />',
    '<PackageVersion Include="Unused" Version="1.0.0" />',
  ])), set());
  // No graph read: the marker.
  assert.deepEqual(packageVersionTrains(handModel({ graph: false }), before, bumped), set(DEPENDENCIES));
  assert.throws(() => packageVersionTrains(m, before, "<Project><!-- open"), /never closed/);
});

test("a PackageVersion conditioned on a project releases that project's train, or none", () => {
  const m = handModel();
  // The Wolverine.Http fixture's Roslyn pair: the == entry names no project under src/, the != one is everyone's.
  const pair = (fixture, other) => PROPS([
    `<PackageVersion Include="Roslyn" Version="${other}" Condition="'$(MSBuildProjectName)' != 'Fixtures.Host'" />`,
    `<PackageVersion Include="Roslyn" Version="${fixture}" Condition="'$(MSBuildProjectName)' == 'Fixtures.Host'" />`,
  ]);
  assert.deepEqual(packageVersionTrains(m, pair("5.0.0", "4.14.0"), pair("5.1.0", "4.14.0")), set());
  assert.deepEqual(packageVersionTrains(m, pair("5.0.0", "4.14.0"), pair("5.0.0", "4.15.0")), set("core", "satellites"));
  const named = (version) => PROPS([
    `<PackageVersion Include="Roslyn" Version="${version}" Condition="'$(MSBuildProjectName)' == 'Sat'" />`,
  ]);
  assert.deepEqual(packageVersionTrains(m, named("1.0.0"), named("2.0.0")), set("satellites"));
  // MSBuild compares the name regardless of case.
  const lower = (version) => named(version).replace("== 'Sat'", "== 'sat'");
  assert.deepEqual(packageVersionTrains(m, lower("1.0.0"), lower("2.0.0")), set("satellites"));
});

test("a changed solution filter releases the trains of the projects it adds or removes", () => {
  const m = handModel();
  const filter = (projects, path = "../AdCodicem.ValueObjects.slnx") =>
    JSON.stringify({ solution: { path, projects: projects.map((project) => `src\\${project}\\${project}.csproj`) } });
  assert.deepEqual(solutionFilterTrains(m, filter(["Core.A"]), filter(["Core.A", "Sat"])), set("satellites"));
  assert.deepEqual(solutionFilterTrains(m, filter(["Core.A", "Core.B"]), filter(["Core.A"])), set("core"));
  assert.deepEqual(solutionFilterTrains(m, filter(["Core.A", "Sat"]), filter(["Sat", "Core.A"])), set());
  assert.deepEqual(solutionFilterTrains(m, filter(["Core.A", "Gone"]), filter(["Core.A"])), set("core", "satellites"));
  assert.deepEqual(solutionFilterTrains(m, filter(["Core.A"]), filter(["Core.A"], "../Other.slnx")), set("core", "satellites"));
  assert.deepEqual(solutionFilterTrains(m, null, filter(["Core.A"])), set("core", "satellites"));
  assert.deepEqual(solutionFilterTrains(m, filter(["Core.A"]), `\uFEFF${filter(["Core.A", "Sat"])}`), set("satellites"));
  // MSBuild reads comments and trailing commas there, which JSON refuses: every train, rather than a guess.
  const commented = `// the packable projects\n${filter(["Core.A"])}`;
  assert.deepEqual(solutionFilterTrains(m, filter(["Core.A"]), commented), set("core", "satellites"));
  assert.deepEqual(solutionFilterTrains(m, commented, filter(["Core.A"])), set("core", "satellites"));
  assert.deepEqual(trainsOfPath(m, "src/AdCodicem.ValueObjects.Packages.slnf", filter(["Core.A"]), filter([])), set("core"));
});

/** A throwaway repository holding Core.A, Core.B and Sat under src/, the list, and the given restore graphs. */
function repository() {
  const root = mkdtempSync(join(tmpdir(), "release-trains-repo-"));
  const run = git(root);
  run("init", "--quiet", "--initial-branch=main");
  run("config", "user.email", "test@example.invalid");
  run("config", "user.name", "Test");
  run("config", "commit.gpgsign", "false");
  run("config", "tag.gpgSign", "false");
  run("config", "log.showRoot", "false"); // git log --root lists the root commit's files whatever this says
  const write = (path, text) => {
    mkdirSync(dirname(join(root, path)), { recursive: true });
    writeFileSync(join(root, path), text);
  };
  const commit = (message) => {
    run("add", "--all");
    run("commit", "--quiet", "--allow-empty", "--message", message);
    return run("rev-parse", "HEAD").trim();
  };
  const project = (name, links = []) => write(`src/${name}/${name}.csproj`, [
    '<Project Sdk="Microsoft.NET.Sdk">',
    "  <ItemGroup>",
    ...links.map((file) => `    <Compile Include="..\\Shared\\${file}" Link="Shared\\${file}" />`),
    "  </ItemGroup>",
    "</Project>",
  ].join("\n"));
  const restored = (graphs) => {
    for (const [name, packages] of Object.entries(graphs)) {
      const libraries = Object.fromEntries(packages.map((id) => [`${id}/1.0.0`, { type: "package" }]));
      libraries["Core.A/1.0.0"] = { type: "project" };
      write(`artifacts/obj/${name}/project.assets.json`, JSON.stringify({ version: 3, libraries }));
    }
  };
  write(".gitignore", "artifacts/\n");
  write("src/release-trains.json", LIST);
  return { root, run, write, commit, project, restored, remove: () => rmSync(root, { recursive: true, force: true }) };
}

test("each commit of a range is attributed from its own diff, merges by their first parent", () => {
  const repo = repository();
  try {
    repo.project("Core.A");
    repo.project("Core.B");
    repo.project("Sat");
    const root = repo.commit("feat: the root commit");
    repo.write("src/Shared/Linked.cs", "class Linked {}");
    repo.project("Sat", ["Linked.cs"]);
    const linked = repo.commit("feat(sat): link a new Shared file");
    repo.write("src/Gone/Gone.csproj", "<Project />");
    const added = repo.commit("feat: a project that will go");
    repo.run("rm", "--quiet", "-r", "src/Gone");
    const gone = repo.commit("refactor!: remove it");
    repo.run("checkout", "--quiet", "-b", "side");
    repo.write("src/Core.B/B.cs", "class B {}");
    const side = repo.commit("fix(b): on a branch");
    repo.write("tests/T.cs", "class T {}");
    const tests = repo.commit("test: on the branch too");
    repo.run("checkout", "--quiet", "main");
    repo.write("README.md", "# readme");
    const docs = repo.commit("docs: on main");
    repo.run("merge", "--quiet", "--no-ff", "--no-edit", "side");
    const merge = repo.run("rev-parse", "HEAD").trim();
    repo.restored({ "Core.A": [], "Core.B": [], Sat: [] });

    const m = readModel(repo.root);
    assert.deepEqual([...m.links.get("src/Shared/Linked.cs")], ["satellites"]);
    const commits = trainsOfCommits(m, repo.run, merge);
    const by = new Map(commits.map((commit) => [commit.hash, commit]));
    assert.equal(commits.length, 8);
    assert.deepEqual(by.get(root).trains, set("core", "satellites"), "the root commit adds the list");
    assert.deepEqual(by.get(linked).trains, set("satellites"));
    assert.deepEqual(by.get(added).trains, set("core", "satellites"), "a project folder absent at HEAD");
    assert.deepEqual(by.get(gone).trains, set("core", "satellites"));
    assert.deepEqual(by.get(side).trains, set("core"));
    assert.deepEqual(by.get(tests).trains, set());
    assert.deepEqual(by.get(docs).trains, set());
    assert.deepEqual(by.get(merge).trains, set("core"), "the merge's diff against main");
    assert.deepEqual(by.get(merge).paths.sort(), ["src/Core.B/B.cs", "tests/T.cs"]);
    assert.equal(by.get(side).message, "fix(b): on a branch");
  } finally {
    repo.remove();
  }
});

test("a src/Shared file releases the trains that compiled it at the commit, not those compiling it now", () => {
  const repo = repository();
  try {
    repo.write("src/Shared/Text.cs", "class Text {}");
    repo.project("Core.A", ["Text.cs"]);
    repo.project("Core.B");
    repo.project("Sat");
    const base = repo.commit("build: the base");
    repo.write("src/Shared/Text.cs", "class Text { }");
    const early = repo.commit("fix: while the core alone links it");
    repo.project("Sat", ["Text.cs"]);
    const linking = repo.commit("feat(sat): link it");
    repo.write("src/Shared/Text.cs", "class Text {  }");
    const late = repo.commit("fix: once both link it");
    repo.project("Core.A");
    const unlinking = repo.commit("refactor(a): stop linking it");
    repo.write("src/Shared/Text.cs", "class Text {   }");
    repo.project("Sat");
    const both = repo.commit("fix(sat): change it and stop linking it");
    repo.write("src/Shared/Text.cs", "class Text {    }");
    const nobody = repo.commit("fix: nobody links it");

    const m = readModel(repo.root, { graph: false });
    assert.equal(m.links.has("src/Shared/Text.cs"), false);
    const by = new Map(trainsOfCommits(m, repo.run, `${base}..HEAD`).map((commit) => [commit.hash, commit.trains]));
    assert.deepEqual(by.get(early), set("core"));
    assert.deepEqual(by.get(linking), set("satellites"));
    assert.deepEqual(by.get(late), set("core", "satellites"));
    assert.deepEqual(by.get(unlinking), set("core"));
    assert.deepEqual(by.get(both), set("satellites"), "the parent's links count");
    assert.deepEqual(by.get(nobody), set("core", "satellites"));

    // Every link of a line counts, in the tree and in an older commit's.
    repo.write("src/Shared/Other.cs", "class Other {}");
    repo.write("src/Sat/Sat.csproj", '<Project Sdk="Microsoft.NET.Sdk">\n  <ItemGroup>\n    ' +
      '<Compile Include="..\\Shared\\Other.cs" Link="Shared\\Other.cs" /><Compile Include="..\\Shared\\Text.cs" Link="Shared\\Text.cs" />' +
      "\n  </ItemGroup>\n</Project>");
    const paired = repo.commit("feat(sat): link two files on one line");
    repo.write("src/Shared/Text.cs", "class Text {     }");
    const second = repo.commit("fix: the second file of the line");
    const now = readModel(repo.root, { graph: false });
    assert.deepEqual(now.links.get("src/Shared/Text.cs"), set("satellites"));
    assert.deepEqual(trainsOfCommits(now, repo.run, `${paired}..${second}`)[0].trains, set("satellites"));
  } finally {
    repo.remove();
  }
});

test("a message keeps every byte but NUL, and its commit keeps its paths", () => {
  const repo = repository();
  try {
    repo.project("Core.A");
    repo.project("Core.B");
    repo.project("Sat");
    const base = repo.commit("build: the base");
    repo.write("src/Sat/S.cs", "class S {}");
    const odd = repo.commit("fix(sat): \x01 a thing\n\n\x02 the body\n\nBREAKING CHANGE: \x01\x02 kept");
    repo.write("src/Core.A/A.cs", "class A {}");
    const next = repo.commit("fix(a): the next one");
    const commits = trainsOfCommits(readModel(repo.root, { graph: false }), repo.run, `${base}..HEAD`);
    assert.deepEqual(commits.map(({ hash }) => hash), [next, odd]);
    assert.equal(commits[1].message, "fix(sat): \x01 a thing\n\n\x02 the body\n\nBREAKING CHANGE: \x01\x02 kept");
    assert.deepEqual(commits[1].paths, ["src/Sat/S.cs"]);
    assert.deepEqual(commits[1].trains, set("satellites"));
    assert.deepEqual(commits[0].trains, set("core"));
  } finally {
    repo.remove();
  }
});

test("a commit bumping a dependency reads the props before and after it", () => {
  const repo = repository();
  try {
    repo.project("Core.A");
    repo.project("Core.B");
    repo.project("Sat");
    repo.write("Directory.Packages.props", PROPS(['<PackageVersion Include="FluentValidation" Version="12.1.0" />']));
    const first = repo.commit("build: the props");
    repo.write("Directory.Packages.props", PROPS(['<PackageVersion Include="FluentValidation" Version="12.2.0" />']));
    const bump = repo.commit("fix(deps): take FluentValidation 12.2.0");
    repo.restored({ "Core.A": ["Roslyn"], "Core.B": [], Sat: ["FluentValidation"] });
    const m = readModel(repo.root);
    assert.deepEqual(m.graph.get("fluentvalidation"), set("satellites"));
    assert.equal(m.graph.has("core.a"), false, "a project reference is no package");
    const [latest, oldest] = trainsOfCommits(m, repo.run, bump);
    assert.equal(latest.hash, bump);
    assert.deepEqual(latest.trains, set("satellites"));
    assert.equal(oldest.hash, first);
    assert.deepEqual(oldest.trains, set("core", "satellites"), "a props with no parent changes everything");
    assert.deepEqual(trainsOfCommits(readModel(repo.root, { graph: false }), repo.run, `${first}..${bump}`)[0].trains,
      set(DEPENDENCIES));
    rmSync(join(repo.root, "artifacts", "obj", "Sat"), { recursive: true });
    assert.throws(() => readModel(repo.root), /No restore graph for Sat: run dotnet restore src\/AdCodicem\.ValueObjects\.Packages\.slnf first/);
  } finally {
    repo.remove();
  }
});

test("lint-commits shows each commit's trains and warns on a releasing commit spanning two trains", () => {
  const repo = repository();
  try {
    repo.project("Core.A");
    repo.project("Core.B");
    repo.project("Sat");
    repo.write("src/Shared/Both.cs", "class Both {}");
    repo.write("Directory.Packages.props", PROPS(['<PackageVersion Include="FluentValidation" Version="12.1.0" />']));
    const base = repo.commit("build: the base");
    repo.write("src/Core.A/A.cs", "class A {}");
    repo.write("src/Sat/S.cs", "class S {}");
    const mixed = repo.commit("feat(a,sat): one change in two trains");
    repo.write("src/Core.A/A.cs", "class A { }");
    repo.write("src/Shared/Both.cs", "class Both { }");
    repo.commit("fix(a): a project and a Shared file");
    repo.write("Directory.Packages.props", PROPS(['<PackageVersion Include="FluentValidation" Version="12.2.0" />']));
    repo.commit("fix(deps): take FluentValidation 12.2.0");
    repo.write("src/Core.A/A.cs", "class A {  }");
    repo.write("src/Sat/S.cs", "class S {  }");
    repo.commit("refactor: two trains, releasing nothing");
    const head = repo.commit("docs: | a pipe in the subject");

    const m = readModel(repo.root, { graph: false });
    const lint = lintCommits(m, trainsOfCommits(m, repo.run, `${base}..${head}`));
    assert.deepEqual(lint.rows.map(({ trains, subject }) => `${trains} ${subject}`), [
      "core satellites feat(a,sat): one change in two trains",
      "core satellites fix(a): a project and a Shared file",
      `${DEPENDENCIES} fix(deps): take FluentValidation 12.2.0`,
      "core satellites refactor: two trains, releasing nothing",
      "- docs: | a pipe in the subject",
    ]);
    assert.equal(lint.warnings.length, 1);
    assert.match(lint.warnings[0], new RegExp(`^${mixed.slice(0, 7)} releases core and satellites: `));
    const summary = lintSummary(lint);
    assert.match(summary, /\| docs: \\\| a pipe in the subject \|/);
    assert.match(summary, new RegExp(`${DEPENDENCIES}: a changed <PackageVersion>`));

    // The command line: the table on stdout, the warning as an annotation, the summary to the job's, and exit 0.
    const summaryFile = join(repo.root, "summary.md");
    const at = join(repo.root, "src", "release-trains.json");
    const out = spawnSync(process.execPath, [script, "--list", at, "lint-commits", base, head],
      { encoding: "utf8", env: { ...process.env, GITHUB_STEP_SUMMARY: summaryFile } });
    assert.equal(out.status, 0, out.stderr);
    assert.match(out.stdout, /^[0-9a-f]{7}\tcore satellites\tfeat\(a,sat\): one change in two trains$/m);
    assert.match(out.stdout, /^::warning title=A commit releases two trains::[0-9a-f]{7} releases core and satellites: .*\.$/m);
    assert.equal(readFileSync(summaryFile, "utf8"), summary);
    assert.equal(run("--list", at, "lint-commits", base).status, 1);
    // Another list in the tree's src/: its trains, the tree's projects and history.
    const other = join(repo.root, "src", "other-trains.json");
    writeFileSync(other, JSON.stringify({
      core: { breaking: "minor", projects: ["Core.A", "Core.B", "Sat"] },
      satellites: { breaking: "minor", projects: "*" },
    }));
    assert.match(spawnSync(process.execPath, [script, "--list", other, "lint-commits", base, head], { encoding: "utf8", env: quiet }).stdout,
      /^[0-9a-f]{7}\tcore\tfeat\(a,sat\): one change in two trains$/m);

    // commits reads the graph, and fails closed without it.
    assert.match(run("--list", at, "commits", base, head).stderr, /::error::No restore graph for Core\.A/);
    repo.restored({ "Core.A": [], "Core.B": [], Sat: ["FluentValidation"] });
    const lines = run("--list", at, "commits", base, head).stdout.trimEnd().split("\n");
    assert.equal(lines.length, 5);
    assert.match(lines[2], /^[0-9a-f]{40}\tsatellites\tfix\(deps\): take FluentValidation 12\.2\.0$/);
    assert.match(lines[4], /^[0-9a-f]{40}\t-\tdocs: /);
  } finally {
    repo.remove();
  }
});

test("lint-commits counts a project a commit deletes with the train it rode then", () => {
  const repo = repository();
  try {
    repo.project("Core.A");
    repo.project("Core.B");
    repo.project("Sat");
    repo.project("Sat2");
    const base = repo.commit("build: the base");
    repo.run("rm", "--quiet", "-r", "src/Sat");
    repo.write("src/Core.A/Y.cs", "class Y {}");
    const fold = repo.commit("feat!: fold Sat into the core");
    repo.run("rm", "--quiet", "-r", "src/Core.B");
    repo.write("src/release-trains.json", JSON.stringify({
      core: { breaking: "minor", projects: ["Core.A"] },
      satellites: { projects: "*", breaking: "minor" },
    }));
    repo.write("src/Core.A/Z.cs", "class Z {}");
    const inside = repo.commit("feat!: fold Core.B into Core.A");
    repo.write("src/Core.A/W.cs", "class W {}");
    repo.write("src/Sat2/W.cs", "class W {}");
    const both = repo.commit("feat: the core and Sat2");
    repo.run("rm", "--quiet", "-r", "src/Sat2");
    repo.commit("refactor: drop Sat2");

    const m = readModel(repo.root, { graph: false });
    const { warnings } = lintCommits(m, trainsOfCommits(m, repo.run, `${base}..HEAD`));
    assert.deepEqual(warnings.map((warning) => warning.slice(0, 7)), [fold, both].map((hash) => hash.slice(0, 7)));
    assert.ok(!warnings.some((warning) => warning.startsWith(inside.slice(0, 7))), "Core.B rode the core");
  } finally {
    repo.remove();
  }
});

test("a releasing commit is what the analyzer releases", () => {
  for (const message of ["feat: x", "fix(a): x", "perf: x", "build(pack): x", "docs(readme): x", "refactor!: x",
    "chore: x\n\nBREAKING CHANGE: y", "ci(a)!: x", "chore: x\n\n* BREAKING CHANGE: y", "chore: x\n\n  BREAKING-CHANGE: y",
    "chore: x\n\nBREAKING CHANGE y", "revert: feat(json): a thing\n\nThis reverts commit 0123abc.",
    'Revert "feat: a thing"\n\nThis reverts commit 0123abc.', "FEAT: x", "FIX(a): x",
    "Merge branch 'main' into x\n\nBREAKING CHANGE: y"]) {
    assert.equal(releases(message), true, message);
  }
  for (const message of ["chore: x", "docs: x", "build(deps): x", "test(pack): x", "Merge branch 'x'", "ci: x\n\nfeat: y",
    "chore: BREAKING CHANGE: in the header", "feat: x [skip release]", "fix: x\n\n[release skip]", "PERF: x", "Feat: x"]) {
    assert.equal(releases(message), false, message);
  }
});

test("a line is the major, or 0.<minor> while the major is 0", () => {
  assert.equal(lineOf("0.2.1"), "0.2");
  assert.equal(lineOf("0.3.0-preview.4"), "0.3");
  assert.equal(lineOf("1.4.0"), "1");
  assert.equal(lineOf("12.0.3"), "12");
  assert.throws(() => lineOf("v1.0.0"), /is no version/);
  assert.throws(() => lineOf(undefined), /is no version/);
});

test("the core a train last released against is the highest core tag merged into its tag", () => {
  const repo = repository();
  try {
    repo.commit("feat: one");
    repo.run("tag", "v0.1.0");
    repo.commit("feat: two");
    repo.run("tag", "-a", "-m", "annotated", "v0.2.0");
    repo.run("tag", "v0.10.0-preview.1");
    repo.run("tag", "satellites-v0.2.0");
    repo.commit("feat: three");
    repo.run("tag", "v0.3.0");
    assert.equal(previousCoreVersion(repo.run, "satellites-v0.2.0"), "0.2.0");
    assert.equal(previousCoreVersion(repo.run, "v0.3.0"), "0.3.0");
    repo.run("checkout", "--quiet", "--orphan", "lone");
    repo.commit("feat: unrelated");
    repo.run("tag", "satellites-v9.0.0");
    assert.throws(() => previousCoreVersion(repo.run, "satellites-v9.0.0"), /no stable core tag v<M\.m\.p> is merged into satellites-v9\.0\.0/);
  } finally {
    repo.remove();
  }
});

const RELEASERC = {
  branches: ["main"],
  plugins: [
    ["@semantic-release/commit-analyzer", {
      preset: "conventionalcommits",
      releaseRules: [
        { breaking: true, release: "minor" },
        { type: "build", scope: "pack", release: "patch" },
      ],
    }],
    "@semantic-release/release-notes-generator",
  ],
};

test("the analyzer's rules are read without the slot each train's breaking rule takes", () => {
  const { options, rules } = analyzerRules(RELEASERC);
  assert.equal(options.preset, "conventionalcommits");
  assert.deepEqual(rules, [{ type: "build", scope: "pack", release: "patch" }]);
  const slotless = structuredClone(RELEASERC);
  slotless.plugins[0][1].releaseRules.shift();
  assert.throws(() => analyzerRules(slotless), /first release rule must be \{ breaking: true, release: minor \}/);
  const major = structuredClone(RELEASERC);
  major.plugins[0][1].releaseRules[0].release = "major";
  assert.throws(() => analyzerRules(major), /first release rule/);
  const plugin = { plugins: [["./.github/scripts/release-train-plugin.mjs", { analyzer: { preset: "conventionalcommits",
    releaseRules: [{ type: "docs", scope: "readme", release: "patch" }] } }]] };
  assert.deepEqual(analyzerRules(plugin).rules, [{ type: "docs", scope: "readme", release: "patch" }]);
  plugin.plugins[0][1].analyzer.releaseRules.push({ breaking: true, release: "minor" });
  assert.throws(() => analyzerRules(plugin), /carries a breaking rule/);
  assert.throws(() => analyzerRules({ plugins: ["@semantic-release/github"] }), /lists neither/);
});

test("a train's release type takes its breaking rule, and the breaking type when the core's line moved", async () => {
  const seen = [];
  const analyzer = (type) => async (config, commits) => {
    seen.push({ config, commits });
    return type;
  };
  const base = { breaking: "minor", config: RELEASERC, commits: ["c"], coreVersion: "0.3.0", previousCoreVersion: "0.2.1" };

  // The slot is replaced by the train's own rule, and the analyzer's options kept.
  await releaseType({ ...base, train: "satellites", breaking: "major", analyze: analyzer(null) });
  assert.deepEqual(seen[0].config.releaseRules, [{ breaking: true, release: "major" }, { type: "build", scope: "pack", release: "patch" }]);
  assert.equal(seen[0].config.preset, "conventionalcommits");
  assert.deepEqual(seen[0].commits, ["c"]);

  assert.deepEqual(await releaseType({ ...base, train: "satellites", analyze: analyzer(null) }), { type: "minor", lineMoved: true });
  assert.deepEqual(await releaseType({ ...base, train: "satellites", analyze: analyzer("patch"), coreVersion: "0.2.2" }),
    { type: "patch", lineMoved: false });
  assert.deepEqual(await releaseType({ ...base, train: "satellites", analyze: analyzer(null), coreVersion: "0.2.2" }),
    { type: null, lineMoved: false });
  assert.deepEqual(await releaseType({ ...base, train: "satellites", breaking: "major", analyze: analyzer("major"),
    coreVersion: "1.0.0", previousCoreVersion: "0.9.0" }), { type: "major", lineMoved: true });
  assert.deepEqual(await releaseType({ ...base, train: "satellites", breaking: "major", analyze: analyzer("patch"),
    coreVersion: "2.0.0", previousCoreVersion: "1.4.0" }), { type: "major", lineMoved: true });
  assert.deepEqual(await releaseType({ ...base, train: "satellites", analyze: analyzer("major"), coreVersion: "0.4.0" }),
    { type: "major", lineMoved: true }, "a higher type is kept");

  // The core is never forced, and needs no core version.
  assert.deepEqual(await releaseType({ ...base, train: "core", analyze: analyzer(null), coreVersion: undefined }),
    { type: null, lineMoved: false });
  await assert.rejects(releaseType({ ...base, train: "satellites", analyze: analyzer(null), coreVersion: "0.3" }),
    /the satellites train needs the core's version, not 0\.3/);
  await assert.rejects(releaseType({ ...base, train: "satellites", analyze: analyzer(null), coreVersion: undefined }),
    /needs the core's version/);
});

// preview-gate.sh decides whether anything ships; this module, which trains. Each path of the repository is an input of
// both or of neither, the two files read by content aside.
test("every path the preview gate reads releases a train, and no other", () => {
  const repositoryRoot = join(src, "..");
  const gate = readFileSync(join(repositoryRoot, ".github", "scripts", "preview-gate.sh"), "utf8");
  const block = /^inputs=\(\n([\s\S]*?)\n\)$/m.exec(gate);
  assert.ok(block, "preview-gate.sh holds an inputs=( … ) block");
  const pathspecs = block[1].split("\n").map((line) => line.trim().replace(/^'(.*)'$/, "$1")).filter(Boolean);
  const run = git(repositoryRoot);
  const lines = (text) => text.split("\n").filter(Boolean);
  const inputs = new Set(lines(run("ls-files", "--", ...pathspecs)));
  const m = readModel(repositoryRoot, { graph: false });
  const read = new Set(["Directory.Packages.props", "src/AdCodicem.ValueObjects.Packages.slnf"]);
  const drift = [];
  for (const path of lines(run("ls-files"))) {
    if (read.has(path)) continue;
    const trains = trainsOfPath(m, path);
    if (inputs.has(path) !== trains.size > 0) drift.push(`${path}: ${inputs.has(path) ? "a gate input" : "no gate input"}, trains ${[...trains].join(" ") || "none"}`);
  }
  assert.deepEqual(drift, []);
  // The inputs no file tracked today shows: each literal pathspec of the gate outside src/ is a global input.
  const literal = pathspecs.filter((pathspec) => !pathspec.startsWith(":") && pathspec !== "src");
  assert.deepEqual(literal.filter((path) => !GLOBAL_INPUTS.includes(path)), []);
  assert.deepEqual(GLOBAL_INPUTS.filter((path) => !path.startsWith("src/") && !literal.includes(path)), []);
  assert.ok(pathspecs.includes(":(icase)nuget.config"));
});
