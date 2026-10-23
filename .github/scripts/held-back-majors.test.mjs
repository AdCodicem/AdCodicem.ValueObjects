// The weekly list of the majors Dependabot holds back (held-back-majors.mjs), against copies of the three files it
// reads and both registries faked on 127.0.0.1. Run with: node --test '.github/scripts/*.test.mjs'
import { execFile } from "node:child_process";
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { createServer } from "node:http";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { after, before, describe, test } from "node:test";
import assert from "node:assert/strict";
import { fileURLToPath } from "node:url";
import { heldBack, keyOf, matches, readIgnoreRules, readPackageVersions, scopeOf } from "./held-back-majors.mjs";

const script = join(dirname(fileURLToPath(import.meta.url)), "held-back-majors.mjs");

const DEPENDABOT = `version: 2

updates:
  # A comment, which the reader skips.
  - package-ecosystem: "nuget"
    directory: "/"
    schedule:
      interval: "weekly"
    groups:
      dotnet:
        update-types: ["minor", "patch"]
    ignore:
      - dependency-name: "Contoso.*"
        update-types: ["version-update:semver-major"]
      # Held at its major.minor.
      - dependency-name: "Roslyn.Like*"
        update-types: ["version-update:semver-major", "version-update:semver-minor"]
      - dependency-name: "local-tool"
        update-types: ["version-update:semver-major"]

  - package-ecosystem: "npm"
    directory: "/"
    ignore:
      - dependency-name: "preset"
        update-types: ["version-update:semver-major"]

  - package-ecosystem: "github-actions"
    directories:
      - "/"
    groups:
      actions:
        update-types: ["minor", "patch"]
`;

const PROPS = `<Project>
  <ItemGroup>
    <!-- <PackageVersion Include="Contoso.Commented" Version="1.0.0" /> -->
    <PackageVersion Include="Contoso.Data" Version="2.1.0" />
    <PackageVersion Include="Contoso.Web" Version="2.4.0" />
    <PackageVersion Include="Roslyn.Like" Version="4.14.0" Condition="'$(MSBuildProjectName)' != 'Fixture'" />
    <PackageVersion Include="Roslyn.Like" Version="5.0.0" Condition="'$(MSBuildProjectName)' == 'Fixture'" />
    <PackageVersion Include="Unruled" Version="1.0.0" />
  </ItemGroup>
</Project>
`;

const TOOLS = { version: 1, isRoot: true, tools: { "local-tool": { version: "7.0.1", commands: ["local-tool"] } } };

// The manifest asks for a range below the lock file's version, so that a row naming 9.3.1 proves the lock was read.
const MANIFEST = { name: "fixture", private: true, devDependencies: { preset: "^9.0.0", other: "^1.0.0" } };
const LOCK = {
  name: "fixture",
  lockfileVersion: 3,
  packages: { "node_modules/preset": { version: "9.3.1" }, "node_modules/other": { version: "1.0.0" } },
};

/** What the fake registries list, which a test changes before it runs the script. */
let nuget;
let npm;
let failing;
const defaults = () => {
  nuget = {
    "contoso.data": ["2.0.0", "2.1.0", "3.0.0-preview.1"],
    "contoso.web": ["2.4.0", "2.5.0", "3.0.0", "3.1.0"],
    "roslyn.like": ["4.14.0", "4.14.1"],
    "local-tool": ["7.0.1"],
    unruled: ["1.0.0", "9.0.0"],
  };
  npm = { preset: ["9.3.1", "10.0.0-beta.1"], other: ["1.0.0", "2.0.0"] };
  failing = undefined;
};

const server = createServer((request, response) => {
  const url = new URL(request.url, "http://127.0.0.1");
  if (failing && url.pathname.includes(failing)) {
    response.writeHead(500).end();
    return;
  }
  let versions;
  let match;
  if ((match = url.pathname.match(/^\/nuget\/([^/]+)\/index\.json$/))) {
    versions = nuget[match[1]] && { versions: nuget[match[1]] };
  } else if ((match = url.pathname.match(/^\/npm\/([^/]+)$/))) {
    versions = npm[match[1]] && { versions: Object.fromEntries(npm[match[1]].map((version) => [version, {}])) };
  }
  if (!versions) {
    response.writeHead(404).end();
    return;
  }
  response.writeHead(200, { "content-type": "application/json" }).end(JSON.stringify(versions));
});

let base;
let root;
before(async () => {
  await new Promise((done) => server.listen(0, "127.0.0.1", done));
  base = `http://127.0.0.1:${server.address().port}`;
});
after(() => server.close());

/** A repository holding the fixture files, with one of them replaced when given. */
function repository(overrides = {}) {
  root = mkdtempSync(join(tmpdir(), "held-back-"));
  const files = {
    ".github/dependabot.yml": DEPENDABOT,
    "Directory.Packages.props": PROPS,
    ".config/dotnet-tools.json": JSON.stringify(TOOLS),
    "package.json": JSON.stringify(MANIFEST),
    "package-lock.json": JSON.stringify(LOCK),
    ...overrides,
  };
  for (const [path, text] of Object.entries(files)) {
    mkdirSync(dirname(join(root, path)), { recursive: true });
    writeFileSync(join(root, path), text);
  }
  return root;
}

/** Runs the script over a repository: { code, stdout, stderr, report, summary, output }. */
function run(directory) {
  const report = join(directory, "report.md");
  const summary = join(directory, "summary.md");
  const output = join(directory, "output.txt");
  const env = {
    ...process.env,
    NUGET_FLAT_CONTAINER: `${base}/nuget`,
    NPM_REGISTRY: `${base}/npm`,
    GITHUB_STEP_SUMMARY: summary,
    GITHUB_OUTPUT: output,
  };
  return new Promise((done) => {
    execFile(process.execPath, [script, "--root", directory, report], { env }, (error, stdout, stderr) => {
      const read = (path) => {
        try {
          return readFileSync(path, "utf8");
        } catch {
          return "";
        }
      };
      const code = error ? error.code : 0;
      done({ code, stdout, stderr, report: read(report), summary: read(summary), output: read(output) });
    });
  });
}

const rowsOf = (text) => text.split("\n").filter((line) => line.startsWith("| ") && !line.startsWith("| Ecosystem"));

describe("held-back-majors.mjs", () => {
  test("a pin whose major is held back, on a later stable major, is one row", async () => {
    defaults();
    const result = await run(repository());
    assert.equal(result.code, 0, result.stderr);
    const rows = rowsOf(result.report);
    assert.ok(rows.includes("| nuget | `Contoso.Web` | 2.4.0 | 3.1.0 | 2.5.0 | major |  |"), result.report);
    assert.ok(!rows.some((row) => row.includes("Contoso.Data")), "a later prerelease major is no stable major");
    assert.ok(!rows.some((row) => row.includes("Unruled")), "a pin no rule matches is not held back");
    assert.ok(!rows.some((row) => row.includes("`other`")), "an npm pin no rule matches is not held back");
    assert.equal(result.summary, result.report);
    assert.match(result.stdout, /::warning title=Held back::Contoso\.Web 2\.4\.0: 3\.1\.0 is out/);
  });

  test("a rule holding the minor reads major.minor, and a later patch is no row", async () => {
    defaults();
    nuget["roslyn.like"] = ["4.14.0", "4.14.5", "4.15.0"];
    const rows = rowsOf((await run(repository())).report).filter((row) => row.includes("Roslyn.Like"));
    assert.deepEqual(rows, ["| nuget | `Roslyn.Like` | 4.14.0 | 4.15.0 | 4.14.5 | minor | all but Fixture |"]);
    nuget["roslyn.like"] = ["4.14.0", "4.14.5"];
    assert.ok(!(await run(repository())).report.includes("Roslyn.Like"));
  });

  test("a conditioned pin is a row of its own, scoped to its project", async () => {
    defaults();
    nuget["roslyn.like"] = ["4.14.0", "5.0.0", "5.9.0"];
    const rows = rowsOf((await run(repository())).report).filter((row) => row.includes("Roslyn.Like"));
    assert.deepEqual(rows, [
      "| nuget | `Roslyn.Like` | 4.14.0 | 5.9.0 | 4.14.0 | minor | all but Fixture |",
      "| nuget | `Roslyn.Like` | 5.0.0 | 5.9.0 | 5.0.0 | minor | only Fixture |",
    ]);
  });

  test("one pattern matches every pin it covers, and a local tool is a pin", async () => {
    defaults();
    nuget["contoso.data"] = ["2.1.0", "4.0.0"];
    nuget["local-tool"] = ["7.0.1", "8.0.0"];
    const report = (await run(repository())).report;
    assert.ok(report.includes("`Contoso.Data` | 2.1.0 | 4.0.0 |"), report);
    assert.ok(report.includes("`Contoso.Web` | 2.4.0 | 3.1.0 |"), report);
    assert.ok(report.includes("`local-tool` | 7.0.1 | 8.0.0 |"), report);
  });

  test("an npm major is read at package-lock.json's version", async () => {
    defaults();
    npm.preset = ["9.3.1", "10.4.1"];
    const result = await run(repository());
    assert.ok(rowsOf(result.report).includes("| npm | `preset` | 9.3.1 | 10.4.1 | 9.3.1 | major |  |"), result.report);
    assert.match(result.output, /^held=2$/m);
  });

  test("a rule matching no pin is listed apart, as possibly stale, and fails nothing", async () => {
    defaults();
    const extra = DEPENDABOT.replace(
      '      - dependency-name: "local-tool"\n',
      '      - dependency-name: "Removed.Package"\n        update-types: ["version-update:semver-major"]\n' +
        '      - dependency-name: "local-tool"\n');
    const result = await run(repository({ ".github/dependabot.yml": extra }));
    assert.equal(result.code, 0, result.stderr);
    assert.match(result.report, /match no pin[\s\S]*- nuget `Removed\.Package` \(line \d+\)/);
    assert.match(result.stdout, /::warning title=Stale ignore rule::nuget Removed\.Package/);
  });

  test("the report's first line keys each row by its pin and the line it waits for", async () => {
    defaults();
    const result = await run(repository());
    assert.equal(result.report.split("\n")[0], "<!-- held-back: Contoso.Web@2.4.0:3 -->");
    assert.equal(keyOf({ id: "R", pinned: "4.14.0", newest: "5.9.2", held: "minor" }), "R@4.14.0:5.9");
  });

  test("a registry answering 500 fails the check", async () => {
    defaults();
    failing = "contoso.web";
    const result = await run(repository());
    assert.equal(result.code, 1);
    assert.match(result.stderr, /::error::.*contoso\.web\/index\.json answered 500/);
  });

  test("an ignore rule with another key fails the check", async () => {
    defaults();
    const reshaped = DEPENDABOT.replace(
      '      - dependency-name: "local-tool"\n',
      '      - dependency-name: "local-tool"\n        versions: ["8.x"]\n');
    const result = await run(repository({ ".github/dependabot.yml": reshaped }));
    assert.equal(result.code, 1);
    assert.match(result.stderr, /dependabot\.yml:\d+: an ignore rule holds only dependency-name and update-types/);
  });

  test("an ignore rule under an entry with no reader fails the check", async () => {
    defaults();
    const pip = `${DEPENDABOT}
  - package-ecosystem: "pip"
    directory: "/.github/requirements"
    ignore:
      - dependency-name: "conventional-pre-commit"
        update-types: ["version-update:semver-major"]
`;
    const result = await run(repository({ ".github/dependabot.yml": pip }));
    assert.equal(result.code, 1);
    assert.match(result.stderr, /no reader for the pins of the pip entry on \/\.github\/requirements/);
  });

  test("nothing held back writes the sentence alone and exits 0", async () => {
    defaults();
    nuget["contoso.web"] = ["2.4.0", "2.5.0"];
    const result = await run(repository());
    assert.equal(result.code, 0, result.stderr);
    assert.deepEqual(rowsOf(result.report), []);
    assert.match(result.report, /Dependabot holds back no major/);
    assert.match(result.output, /^held=0$/m);
    rmSync(root, { recursive: true, force: true });
  });
});

describe("the readers", () => {
  test("an ignore rule by name alone is refused: it holds back every security fix", () => {
    const text = '  - package-ecosystem: "nuget"\n    directory: "/"\n    ignore:\n      - dependency-name: "X"\n';
    assert.throws(() => readIgnoreRules(text), /ignored by name alone/);
  });

  test("an update type Dependabot does not have is refused", () => {
    const text = '  - package-ecosystem: "nuget"\n    directory: "/"\n    ignore:\n' +
      '      - dependency-name: "X"\n        update-types: ["version-update:semver-mayor"]\n';
    assert.throws(() => readIgnoreRules(text), /semver-mayor is no update type/);
  });

  test("the repository's own dependabot.yml reads whole, each rule under its entry", () => {
    const own = readFileSync(join(dirname(script), "../dependabot.yml"), "utf8");
    const rules = readIgnoreRules(own);
    assert.equal(rules.length, own.split("\n").filter((line) => /^\s*-?\s*dependency-name:/.test(line)).length);
    const roslyn = rules.find((rule) => rule.name === "Microsoft.CodeAnalysis.CSharp*");
    assert.deepEqual(roslyn.types, ["version-update:semver-major", "version-update:semver-minor"]);
    const preset = rules.find((rule) => rule.name === "conventional-changelog-conventionalcommits");
    assert.deepEqual([preset.ecosystem, preset.directory], ["npm", "/"]);
  });

  // Shapes YAML takes as the same file, which this reader cannot place: each must fail, never drop or misfile a rule.
  for (const [shape, from, to] of [
    ["a comment after an entry's opening",
      '  - package-ecosystem: "npm"\n',
      '  - package-ecosystem: "npm" # tooling\n'],
    ["an entry opening with another key",
      '  - package-ecosystem: "npm"\n    directory: "/"\n',
      '  - directory: "/"\n    package-ecosystem: "npm"\n'],
    ["a comment after ignore:",
      '    ignore:\n      - dependency-name: "preset"',
      '    ignore: # tooling\n      - dependency-name: "preset"'],
    ["a list at the key's own indentation",
      '      - dependency-name: "preset"\n        update-types: ["version-update:semver-major"]\n',
      '    - dependency-name: "preset"\n      update-types: ["version-update:semver-major"]\n'],
  ]) {
    test(`${shape} is refused`, () => {
      assert.ok(DEPENDABOT.includes(from), shape);
      assert.throws(() => readIgnoreRules(DEPENDABOT.replace(from, to)), /dependabot\.yml/);
    });
  }

  test("a commented-out PackageVersion is no pin", () => {
    assert.deepEqual(readPackageVersions(PROPS).map((pin) => pin.id),
      ["Contoso.Data", "Contoso.Web", "Roslyn.Like", "Roslyn.Like", "Unruled"]);
  });

  test("a pattern's * is its only wildcard, and case is ignored", () => {
    assert.ok(matches("Microsoft.Extensions.*", "microsoft.extensions.logging"));
    assert.ok(!matches("Microsoft.Extensions.*", "MicrosoftXExtensions.Logging"));
    assert.ok(matches("xunit.v3*", "xunit.v3.assert"));
  });

  test("a four-part version and a prerelease pin are read on their line", () => {
    assert.deepEqual(heldBack("1.2.3.4", ["1.2.3.4", "2.0.0.0"], false),
      { newest: "2.0.0.0", newestOnLine: "1.2.3.4" });
    assert.equal(heldBack("2.0.0-rc.1", ["2.0.0", "2.1.0"], false), undefined);
  });

  test("a condition on another property is shown as written", () => {
    assert.equal(scopeOf("'$(Configuration)' == 'Debug'"), "'$(Configuration)' == 'Debug'");
    assert.equal(scopeOf(undefined), "");
  });
});
