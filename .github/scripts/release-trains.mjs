#!/usr/bin/env node
// The release trains of src/release-trains.json, read as JSON
// (docs/adr/0012-version-the-packages-in-two-trains-independently-of-the-frameworks.md).
//
// Usage: node .github/scripts/release-trains.mjs [--list <file>] <command> [arguments]
//   check                         refuses a list MSBuild could misread, or that the tree contradicts; one line per
//                                 fault, exit 1
//   trains                        "<train> <tag prefix> <breaking>" per train, the core first
//   trains-of <project>...        "<project> <train>" per project; a project no train names rides the train of "*"
//   versions "<train=version ...>" <project>...
//                                 "<project>=<version>" per project, at its train's version
//   commits <from> <to>           "<commit>\t<trains or ->\t<subject>" per commit of from..to, the oldest first; reads
//                                 the restore graph a dotnet restore of the solution filter leaves in artifacts/obj
//   lint-commits <from> <to>      the same table, without the graph, to the job summary when there is one, and a
//                                 warning for each releasing commit changing the projects of two trains; exit 0
//
// --list reads another copy of the list (an older commit's, a test's); by default the one under src/ of the
// repository this script is in. `check` reads the projects of the folder the list is in, `commits` and
// `lint-commits` the projects and the history of the repository that folder is in, so a list they read sits in the
// src/ folder of a tree.
//
// MSBuild reads the same file at evaluation (src/Directory.Build.props), with no JSON parser: a regular expression
// finds the innermost object holding the project's name as a string. That holds whatever the order of the keys, as
// long as a train's object holds no other object, a name sits in one train only and no string holds a brace, which
// `check` makes sure of, so the two readers cannot disagree. ci.yml compares their answers at every pack. Node's
// built-ins only, nothing newer than Node 20: the plan job runs the runner's own Node, with no npm install.
import { execFileSync } from "node:child_process";
import { appendFileSync, existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const TRAIN_NAME = /^[a-z][a-z0-9-]*$/;
/**
 * A link to a file of src/Shared, as every project writes it: the file's path relative to src/Shared is group 1, a
 * run of plain segments (no ".", "..", empty segment, whitespace, wildcard or expansion), so that it names the path
 * git reports.
 */
const SHARED_LINK =
  /<Compile\b[^>\n]*\bInclude="\.\.[\\/]Shared[\\/]((?:(?!\.\.?[\\/])[^"*?;$%@\s\\/]+[\\/])*(?!\.\.?")[^"*?;$%@\s\\/]+)"[^>\n]*>/;
/** An element's start tag, a quoted value read whole, so that a ">" inside one does not end it. */
const startTag = (name) => new RegExp(`<${name}\\b(?:[^>"']|"[^"]*"|'[^']*')*>`, "g");
const BREAKING = ["minor", "major"];

/** The trains of the list's text, the core first, then in the list's order. Throws on what is not a list at all. */
export function parseTrains(text) {
  const json = JSON.parse(text);
  if (json === null || typeof json !== "object" || Array.isArray(json)) {
    throw new Error("the list is not a JSON object");
  }
  const names = Object.keys(json).filter((key) => key !== "$comment");
  names.sort((a, b) => (a === "core" ? -1 : b === "core" ? 1 : 0));
  const trains = new Map(names.map((name) => [name, json[name]]));
  return { json, names, trains };
}

/** The train that takes every project the list does not name, or undefined. */
export function restTrain(model) {
  return model.names.find((name) => model.trains.get(name)?.projects === "*");
}

/** The train of a project: the train naming it, else the train of "*". */
export function trainOf(model, project) {
  for (const name of model.names) {
    const projects = model.trains.get(name)?.projects;
    if (Array.isArray(projects) && projects.includes(project)) return name;
  }
  return restTrain(model);
}

/** v for the core, <train>-v for any other (0012, Tags). */
export const tagPrefixOf = (train) => (train === "core" ? "v" : `${train}-v`);

/** CHANGELOG.md for the core, CHANGELOG.<train>.md for any other. */
export const changelogOf = (train) => (train === "core" ? "CHANGELOG.md" : `CHANGELOG.${train}.md`);

/** Every string of a JSON value, keys included. */
function* strings(value) {
  if (typeof value === "string") yield value;
  else if (Array.isArray(value)) for (const item of value) yield* strings(item);
  else if (value && typeof value === "object") {
    for (const [key, item] of Object.entries(value)) {
      yield key;
      yield* strings(item);
    }
  }
}

/** The projects under src/, each with what its csproj references and sets. */
function readProjects(src) {
  const projects = new Map();
  for (const entry of readdirSync(src, { withFileTypes: true })) {
    const file = join(src, entry.name, `${entry.name}.csproj`);
    if (!entry.isDirectory() || !existsSync(file)) continue;
    const text = readFileSync(file, "utf8");
    const references = [...text.matchAll(startTag("ProjectReference"))]
      .map(([item]) => /\bInclude\s*=\s*"([^"]+)"/.exec(item)?.[1])
      .filter(Boolean)
      .map((include) => include.replaceAll("\\", "/").split("/").at(-1).replace(/\.csproj$/, ""));
    projects.set(entry.name, { references, setsPackageId: /<PackageId\b/.test(text), text });
  }
  return projects;
}

/** What is wrong with the list, as messages; none when MSBuild and this module read it alike. */
export function validate(text, src) {
  let model;
  try {
    model = parseTrains(text);
  } catch (error) {
    return [`not a train list: ${error.message}`];
  }
  const faults = [];
  const { json, names, trains } = model;
  const comment = json.$comment;
  if (comment !== undefined && typeof comment !== "string" &&
      !(Array.isArray(comment) && comment.every((line) => typeof line === "string"))) {
    faults.push("$comment is neither a string nor a list of strings");
  }
  for (const value of strings(json)) {
    if (/[{}]/.test(value)) faults.push(`a string holds a brace, which MSBuild's reading cannot tell from an object: ${JSON.stringify(value)}`);
    // MSBuild expands $( @( %( and unescapes %XX in what it reads.
    if (/[$@%]\(|%[0-9A-Fa-f]{2}/.test(value)) faults.push(`a string holds what MSBuild would expand: ${JSON.stringify(value)}`);
  }
  if (!trains.has("core")) faults.push("no core train: the core's tags are v<version>, and it is released first");
  const seen = new Map();
  for (const name of names) {
    const train = trains.get(name);
    if (!TRAIN_NAME.test(name)) faults.push(`${JSON.stringify(name)} is not a train name (${TRAIN_NAME.source})`);
    if (train === null || typeof train !== "object" || Array.isArray(train)) {
      faults.push(`${name} is not an object`);
      continue;
    }
    for (const key of Object.keys(train)) {
      if (key !== "breaking" && key !== "projects") faults.push(`${name}.${key}: a train has a breaking rule and its projects only`);
    }
    if (!BREAKING.includes(train.breaking)) faults.push(`${name}.breaking is ${JSON.stringify(train.breaking)}, not one of ${BREAKING.join(", ")}`);
    if (train.projects === "*") continue;
    if (!Array.isArray(train.projects) || train.projects.length === 0 ||
        !train.projects.every((project) => typeof project === "string" && project !== "")) {
      faults.push(`${name}.projects is neither "*" nor a list of project names`);
      continue;
    }
    for (const project of train.projects) {
      if (seen.has(project)) faults.push(`${project} is in ${seen.get(project)} and in ${name}`);
      else seen.set(project, name);
    }
  }
  const rest = names.filter((name) => trains.get(name)?.projects === "*");
  if (rest.length !== 1) faults.push(`${rest.length} trains take the projects no train names ("projects": "*"); exactly one must`);
  if (src === undefined || faults.length > 0) return faults;

  const projects = readProjects(src);
  for (const [project, train] of seen) {
    if (!projects.has(project)) faults.push(`${train} names ${project}, which is no project src/${project}/${project}.csproj`);
  }
  for (const [project, { references, setsPackageId, text }] of projects) {
    // The list, MSBuild's reading and the package ids are one name: a PackageId of another spelling would ride a
    // train under a name the list does not know.
    if (setsPackageId) faults.push(`src/${project}/${project}.csproj sets a PackageId: the trains name packages by project`);
    // A project's folder releases its train, and a src/Shared file the trains whose items link it, read line by line
    // in an older commit's tree too: an item compiling anything else outside the folder, through "..", a property or
    // an item, would compile a change no rule gives this train.
    for (const [item] of text.matchAll(startTag("Compile"))) {
      const include = /\bInclude\s*=\s*(["'])([\s\S]*?)\1/.exec(item)?.[2] ?? "";
      if (/\.\.|[$@%]\(/.test(include) && !SHARED_LINK.test(item)) {
        faults.push(`src/${project}/${project}.csproj compiles ${JSON.stringify(item)}: an item compiles a file outside ` +
          'its project\'s folder only as one literal <Compile Include="..\\Shared\\<file>" ... /> on one line, which ' +
          "release-trains.mjs reads");
      }
    }
    // Items are read off the csproj alone: an imported file could compile anything.
    if (/<Import\b/.test(text)) faults.push(`src/${project}/${project}.csproj imports a file: the trains read a project's items off its csproj alone`);
    for (const name of ["Directory.Build.props", "Directory.Build.targets"]) {
      if (existsSync(join(src, project, name))) faults.push(`src/${project}/${name}: the trains read a project's items off its csproj alone`);
    }
    // A core project never references another train (0012): the core is released first, against nothing else.
    if (trainOf(model, project) !== "core") continue;
    for (const reference of references) {
      const train = trainOf(model, reference);
      if (train !== "core") faults.push(`${project} (core) references ${reference} (${train}): a core project references the core only`);
    }
  }
  return faults;
}

/** The list as a model, failing on any fault. */
export function loadTrains(file) {
  const text = readFileSync(file, "utf8");
  const faults = validate(text);
  if (faults.length > 0) throw new Error(`${file}: ${faults.join("; ")}`);
  return parseTrains(text);
}

/** "core=1.2.0 satellites=0.4.0" as a map, refusing a train the model lacks and any train left without a version. */
export function parseVersions(model, text) {
  const versions = new Map();
  for (const pair of text.split(/\s+/).filter(Boolean)) {
    const match = /^([^=]+)=(\S+)$/.exec(pair);
    if (!match) throw new Error(`${JSON.stringify(pair)} is not <train>=<version>`);
    if (!model.trains.has(match[1])) throw new Error(`${match[1]} is no train of the list`);
    if (versions.has(match[1])) throw new Error(`${match[1]} is given two versions`);
    versions.set(match[1], match[2]);
  }
  for (const name of model.names) {
    if (!versions.has(name)) throw new Error(`no version for the ${name} train`);
  }
  return versions;
}

// Which trains a change releases (0012, "A commit releases the trains whose package inputs it changes"), read off the
// tree being evaluated: its list, its projects and the files they link, and the restore graph a `dotnet restore` of the
// solution filter left in artifacts/obj. preview-gate.sh's inputs are the same set of paths, which the tests hold.

/** The build inputs every project reads, which release every train; nuget.config is matched apart, in any case. */
export const GLOBAL_INPUTS = [
  "Directory.Build.props",
  "Directory.Build.targets",
  "Directory.Build.rsp",
  "src/Directory.Build.props",
  "src/Directory.Build.targets",
  "global.json",
  ".gitattributes",
  "icon.png",
  "src/PackageReadme.md",
  "src/release-trains.json",
  ".github/scripts/release-pack.sh",
];

/** What a changed `<PackageVersion>` releases when no restore graph was read: unknown, which `lint-commits` names. */
export const DEPENDENCIES = "deps";

const PACKAGES_PROPS = "Directory.Packages.props";
const SOLUTION_FILTER = "src/AdCodicem.ValueObjects.Packages.slnf";

/** The text outside XML comments, scanned from each opening to its closing; throws on a comment never closed. */
export function withoutComments(text, file) {
  let kept = "";
  let from = 0;
  for (let start = text.indexOf("<!--"); start !== -1; start = text.indexOf("<!--", from)) {
    const end = text.indexOf("-->", start + 4);
    if (end === -1) throw new Error(`${file}: a comment opened at offset ${start} is never closed`);
    kept += text.slice(from, start);
    from = end + 3;
  }
  return kept + text.slice(from);
}

/**
 * The model of a tree: { model, projects, links, graph }. links maps 'src/Shared/<file>' to the trains of the projects
 * compiling it; graph maps each package id, lower-cased, to the trains of the projects whose restore graph holds it, or
 * is null when not asked for (graph: false), as lint.yml reads a pull request without restoring it. list is the train
 * list to read, the tree's own by default.
 */
export function readModel(root, { graph = true, list = join(root, "src", "release-trains.json") } = {}) {
  const model = loadTrains(list);
  const projects = readProjects(join(root, "src"));
  const links = new Map();
  for (const [project, { text }] of projects) addLinks(links, model, project, text);
  let packages = null;
  if (graph) {
    packages = new Map();
    for (const project of projects.keys()) {
      const assets = join(root, "artifacts", "obj", project, "project.assets.json");
      if (!existsSync(assets)) {
        throw new Error(`No restore graph for ${project}: run dotnet restore src/AdCodicem.ValueObjects.Packages.slnf first`);
      }
      const libraries = JSON.parse(readFileSync(assets, "utf8")).libraries ?? {};
      for (const [key, library] of Object.entries(libraries)) {
        if (library.type !== "package") continue;
        const id = key.slice(0, key.indexOf("/")).toLowerCase();
        if (!packages.has(id)) packages.set(id, new Set());
        packages.get(id).add(trainOf(model, project));
      }
    }
  }
  return { model, projects, links, graph: packages };
}

const everyTrain = (m) => new Set(m.model.names);

/** Adds to links the src/Shared files a project's csproj text compiles: every link, none crossing a line. */
function addLinks(links, model, project, text) {
  for (const [, file] of text.matchAll(new RegExp(SHARED_LINK.source, "g"))) {
    const path = `src/Shared/${file.replaceAll("\\", "/")}`;
    if (!links.has(path)) links.set(path, new Set());
    links.get(path).add(trainOf(model, project));
  }
}

/**
 * The links of a commit's tree, read with one git grep: a src/Shared file releases the trains that compiled it then,
 * not those compiling it now. A revision that does not exist (a root commit's parent) links nothing.
 */
function linksAt(m, git, rev) {
  const links = new Map();
  let lines;
  try {
    lines = git("grep", "-I", "--no-color", "-e", "Shared", rev, "--", "src/*/*.csproj").split("\n");
  } catch {
    return links; // no match, or no such revision
  }
  const prefix = `${rev}:`;
  for (const line of lines) {
    const match = /^src\/([^/]+)\/\1\.csproj:(.*)$/.exec(line.startsWith(prefix) ? line.slice(prefix.length) : line);
    if (match) addLinks(links, m.model, match[1], match[2]);
  }
  return links;
}

/** The `<PackageVersion>` entries of a Directory.Packages.props text, and what is left of it, whitespace removed. */
function packageVersions(text) {
  const bare = withoutComments(text ?? "", PACKAGES_PROPS);
  const entries = [...bare.matchAll(/<PackageVersion\b([^>]*?)\/>/g)].map(([, attributes]) => {
    // preview-gate.sh's key: whitespace collapsed, not trimmed, so a space removed before /> counts as a change there.
    const normalized = attributes.replace(/\s+/g, " ");
    const id = (/\bInclude="([^"]+)"/.exec(normalized)?.[1] ?? "").toLowerCase();
    return { key: `${id}\t${normalized}`, id, condition: /\bCondition="([^"]*)"/.exec(normalized)?.[1] };
  });
  return { entries, rest: bare.replace(/<PackageVersion\b[^>]*?\/>/g, "").replace(/\s+/g, "") };
}

/**
 * The trains a change of Directory.Packages.props releases, as preview-gate.sh reads it: any change outside the
 * `<PackageVersion>` elements releases every train; a changed entry, the trains whose restore graph holds its package,
 * or the train of the one project its Condition names (`'$(MSBuildProjectName)' == '<X>'`), and none when X is no
 * project under src/. Without a graph, DEPENDENCIES stands for the trains it cannot read.
 */
export function packageVersionTrains(m, before, after) {
  const old = packageVersions(before);
  const now = packageVersions(after);
  if (old.rest !== now.rest) return everyTrain(m);
  const oldKeys = new Set(old.entries.map(({ key }) => key));
  const nowKeys = new Set(now.entries.map(({ key }) => key));
  const changed = [...old.entries.filter(({ key }) => !nowKeys.has(key)), ...now.entries.filter(({ key }) => !oldKeys.has(key))];
  const trains = new Set();
  for (const { id, condition } of changed) {
    const named = condition && /^'\$\(MSBuildProjectName\)'\s*==\s*'([^']+)'$/.exec(condition)?.[1];
    if (named) {
      // MSBuild compares the two strings regardless of case.
      const project = [...m.projects.keys()].find((name) => name.toLowerCase() === named.toLowerCase());
      if (project) trains.add(trainOf(m.model, project));
    } else if (m.graph === null) {
      trains.add(DEPENDENCIES);
    } else {
      for (const train of m.graph.get(id) ?? []) trains.add(train);
    }
  }
  return trains;
}

/**
 * The trains a change of the solution filter releases: those of the projects it adds or removes, and every train when
 * either side does not read as JSON (MSBuild also takes comments and trailing commas there).
 */
export function solutionFilterTrains(m, before, after) {
  const read = (text) => {
    if (text === null || text === undefined) return { path: undefined, projects: new Set() };
    let solution;
    try {
      ({ solution } = JSON.parse(text.replace(/^\uFEFF/, "")));
    } catch {
      return null;
    }
    return { path: solution?.path, projects: new Set((solution?.projects ?? []).map((project) => project.replaceAll("\\", "/"))) };
  };
  const old = read(before);
  const now = read(after);
  if (old === null || now === null || old.path !== now.path) return everyTrain(m);
  const trains = new Set();
  for (const path of [...old.projects, ...now.projects]) {
    if (old.projects.has(path) && now.projects.has(path)) continue;
    const project = path.split("/").at(-1).replace(/\.csproj$/, "");
    if (!m.projects.has(project)) return everyTrain(m);
    trains.add(trainOf(m.model, project));
  }
  return trains;
}

/**
 * The trains a changed path releases, the first rule that matches winning; before and after are the file's text at
 * the parent and at the commit, read only for Directory.Packages.props and the solution filter. m.links says which
 * trains compile a src/Shared file: trainsOfCommits hands it the links of the commit and its parent.
 */
export function trainsOfPath(m, path, before, after) {
  if (/^src\/(.+\/)?(CLAUDE\.md|AnalyzerReleases\.[^/]+\.md)$/.test(path)) return new Set();
  if (GLOBAL_INPUTS.includes(path) || path.toLowerCase() === "nuget.config") return everyTrain(m);
  if (path === PACKAGES_PROPS) return packageVersionTrains(m, before, after);
  if (path === SOLUTION_FILTER) return solutionFilterTrains(m, before, after);
  if (path.startsWith("src/Shared/")) return new Set(m.links.get(path) ?? m.model.names);
  const project = /^src\/([^/]+)\//.exec(path)?.[1];
  if (project && m.projects.has(project)) return new Set([trainOf(m.model, project)]);
  if (path.startsWith("src/")) return everyTrain(m);
  return new Set();
}

/**
 * Every commit of a range with the trains it releases, newest first as git log lists them:
 * [{ hash, message, paths, trains, spans }], spans being the trains of the project folders it changes (projectTrains).
 * A merge commit lists its diff against its first parent, a root commit every file it adds; every commit of the range
 * is walked, as semantic-release's getCommits walks them. A src/Shared file releases the trains that compiled it in
 * the commit or its parent.
 */
export function trainsOfCommits(m, git, range) {
  // A message holds any byte but NUL: the messages are read apart, split on NUL as next-version.mjs reads them, and
  // each path goes to the commit whose hash precedes it.
  const commits = git("log", "-z", "--format=%H%n%B", range).split("\0").filter(Boolean).map((record) => {
    const newline = record.indexOf("\n");
    return { hash: record.slice(0, newline), message: record.slice(newline + 1).trim(), paths: [], trains: new Set() };
  });
  const tokens = git("log", "-z", "--root", "--diff-merges=first-parent", "--no-renames", "--name-only", "--format=%H", range)
    .split("\0").map((token) => token.trim()).filter(Boolean);
  let index = -1;
  for (const token of tokens) {
    if (token === commits[index + 1]?.hash) index++;
    else commits[index].paths.push(token);
  }
  for (const commit of commits) {
    const { hash, paths, trains } = commit;
    commit.spans = projectTrains(m, paths, formerTrain(git, hash));
    let at = m;
    if (paths.some((path) => path.startsWith("src/Shared/"))) {
      const links = linksAt(m, git, `${hash}^1`);
      for (const [path, linking] of linksAt(m, git, hash)) {
        if (!links.has(path)) links.set(path, new Set());
        for (const train of linking) links.get(path).add(train);
      }
      at = { ...m, links };
    }
    for (const path of paths) {
      const read = path === PACKAGES_PROPS || path === SOLUTION_FILTER;
      const show = (rev) => {
        try {
          return git("show", `${rev}:${path}`);
        } catch {
          return null;
        }
      };
      for (const train of trainsOfPath(at, path, read ? show(`${hash}^1`) : undefined, read ? show(hash) : undefined)) {
        trains.add(train);
      }
    }
  }
  return commits;
}

/** The line a version is on: its major, or 0.<minor> while the major is 0 (0012). */
export function lineOf(version) {
  const match = /^(\d+)\.(\d+)\.\d+/.exec(version ?? "");
  if (!match) throw new Error(`${JSON.stringify(version)} is no version`);
  return match[1] === "0" ? `0.${match[2]}` : match[1];
}

const versionParts = (version) => version.split(".").map(Number);
const compareVersions = (left, right) => {
  const [a, b] = [versionParts(left), versionParts(right)];
  for (let index = 0; index < 3; index++) if (a[index] !== b[index]) return a[index] - b[index];
  return 0;
};

/** The highest stable tag of a prefix merged into a commit, as { tag, version }, or undefined. */
export function lastStableTag(git, prefix, rev = "HEAD") {
  const pattern = new RegExp(`^${prefix.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}(\\d+\\.\\d+\\.\\d+)$`);
  return git("tag", "--merged", rev)
    .split("\n")
    .map((tag) => tag.trim())
    .map((tag) => ({ tag, version: pattern.exec(tag)?.[1] }))
    .filter(({ version }) => version)
    .sort((left, right) => compareVersions(right.version, left.version))[0];
}

/** The core's version a train's last release was packed against: the highest v<M.m.p> merged into its tag. */
export function previousCoreVersion(git, tag) {
  const last = lastStableTag(git, tagPrefixOf("core"), tag);
  if (!last) throw new Error(`no stable core tag v<M.m.p> is merged into ${tag}`);
  return last.version;
}

const TYPES = ["patch", "minor", "major"];
const higher = (left, right) => (TYPES.indexOf(left ?? "") >= TYPES.indexOf(right ?? "") ? left : right) ?? null;

/**
 * The analyzer's own rules, without the breaking rule each train brings: from the commit analyzer's entry of
 * .releaserc.json, whose first rule must be the slot { breaking: true, release: "minor" } a train's rule replaces; or
 * from the release-train plugin's analyzer option, which must carry no breaking rule at all.
 */
export function analyzerRules(config) {
  const entries = (config.plugins ?? []).map((plugin) => (Array.isArray(plugin) ? plugin : [plugin, {}]));
  const analyzer = entries.find(([name]) => name === "@semantic-release/commit-analyzer");
  if (analyzer) {
    const [slot, ...rules] = analyzer[1]?.releaseRules ?? [];
    if (JSON.stringify(slot) !== JSON.stringify({ breaking: true, release: "minor" })) {
      throw new Error(".releaserc.json: the commit analyzer's first release rule must be { breaking: true, release: minor }, " +
        "the slot each train's breaking rule takes");
    }
    return { options: analyzer[1], rules };
  }
  const plugin = entries.find(([name]) => name === "./.github/scripts/release-train-plugin.mjs");
  if (!plugin) throw new Error(".releaserc.json lists neither the commit analyzer nor the release-train plugin");
  const options = plugin[1]?.analyzer ?? {};
  const rules = options.releaseRules ?? [];
  if (rules.some((rule) => "breaking" in rule)) {
    throw new Error(".releaserc.json: the release-train plugin's analyzer carries a breaking rule, which each train brings");
  }
  return { options, rules };
}

/**
 * A train's release type and whether the core's line moved under it (0012): the commit analyzer over the train's own
 * commits, with the train's breaking rule before the analyzer's rules, then, for any train but the core, the breaking
 * type at least when the core's line moved since the core this train last released against. The core's version is
 * always passed in, never guessed from a tag.
 */
export async function releaseType({ train, breaking, analyze, config, commits, coreVersion, previousCoreVersion: previous }) {
  const { options, rules } = analyzerRules(config);
  const pluginConfig = { ...options, releaseRules: [{ breaking: true, release: breaking }, ...rules] };
  let type = (await analyze(pluginConfig, commits)) ?? null;
  if (train === "core") return { type, lineMoved: false };
  if (!/^\d+\.\d+\.\d+$/.test(coreVersion ?? "")) throw new Error(`the ${train} train needs the core's version, not ${coreVersion}`);
  const lineMoved = previous !== undefined && lineOf(coreVersion) !== lineOf(previous);
  if (lineMoved) type = higher(type, breaking);
  return { type, lineMoved };
}

/**
 * Whether a commit message releases anything, as .releaserc.json's analyzer reads it: feat, fix, perf, build(pack),
 * docs(readme), a !, a BREAKING CHANGE note or a revert, never a message semantic-release skips ([skip release]). The
 * patterns are the conventionalcommits preset's and conventional-commits-parser's, which the analyzer reads.
 */
export function releases(message) {
  if (/\[skip\s+release]|\[release\s+skip]/i.test(message)) return false;
  if (/^(?:Revert|revert:)\s"?([\s\S]+?)"?\s*This reverts commit (\w*)\./i.test(message)) return true;
  const [header, ...body] = message.split("\n");
  // A note is read whatever the header, and the analyzer's default rules release JSHint's FEAT and FIX too.
  if (body.some((line) => /^[\s|*]*(?:BREAKING CHANGE|BREAKING-CHANGE)[:\s]+/i.test(line))) return true;
  const match = /^(\w+)(?:\(([^)]*)\))?(!)?:/.exec(header);
  if (!match) return false;
  const [, type, scope, bang] = match;
  if (bang) return true;
  return ["feat", "fix", "perf", "FEAT", "FIX"].includes(type) || (type === "build" && scope === "pack") ||
    (type === "docs" && scope === "readme");
}

/**
 * The trains of the project folders a commit changes: src/Shared, the global inputs and the dependencies aside. A
 * folder that is no project in the tree read counts with the train formerTrainOf gives it, or not at all.
 */
export function projectTrains(m, paths, formerTrainOf = () => undefined) {
  const trains = new Set();
  for (const path of paths) {
    const project = /^src\/([^/]+)\//.exec(path)?.[1];
    if (!project || trainsOfPath(m, path).size === 0) continue;
    const train = m.projects.has(project) ? trainOf(m.model, project) : formerTrainOf(project);
    if (train) trains.add(train);
  }
  return trains;
}

/**
 * The train a project absent from the tree read rode in a commit's parent, or else in the commit, from that
 * revision's own list: the list of the tree no longer names a project it dropped. Undefined when neither had it.
 */
function formerTrain(git, hash) {
  const seen = new Map();
  return (project) => {
    if (!seen.has(project)) {
      let train;
      for (const rev of [`${hash}^1`, hash]) {
        try {
          git("cat-file", "-e", `${rev}:src/${project}/${project}.csproj`);
          train = trainOf(parseTrains(git("show", `${rev}:src/release-trains.json`)), project);
          break;
        } catch {
          // no such project, or no list, at that revision
        }
      }
      seen.set(project, train);
    }
    return seen.get(project);
  };
}

const subjectOf = (message) => message.split("\n", 1)[0];
/** Trains in the list's order, the core first, then the deps marker; "-" for none. */
const named = (m, trains) => {
  const order = [...m.model.names, DEPENDENCIES];
  return trains.size > 0 ? [...trains].sort((a, b) => order.indexOf(a) - order.indexOf(b)).join(" ") : "-";
};

/**
 * What lint.yml shows of a pull request's commits, oldest first: { rows, warnings }. A releasing commit whose project
 * folders belong to two trains or more is warned about: each train would release it under its own version.
 */
export function lintCommits(m, commits) {
  const rows = [];
  const warnings = [];
  for (const commit of [...commits].reverse()) {
    rows.push({ hash: commit.hash, trains: named(m, commit.trains), subject: subjectOf(commit.message) });
    const spanned = commit.spans ?? projectTrains(m, commit.paths);
    if (releases(commit.message) && spanned.size > 1) {
      warnings.push(`${commit.hash.slice(0, 7)} releases ${named(m, spanned).replaceAll(" ", " and ")}: each train releases it under ` +
        "its own version and changelog. Split it by train, unless the change holds only together");
    }
  }
  return { rows, warnings };
}

/** The lint's summary: one row per commit, and what the deps marker stands for when a row carries it. */
export function lintSummary({ rows }) {
  const cell = (text) => text.replaceAll("|", "\\|");
  const lines = ["### Trains each commit releases", "", "| Commit | Trains | Subject |", "| --- | --- | --- |"];
  for (const row of rows) lines.push(`| ${row.hash.slice(0, 7)} | ${row.trains} | ${cell(row.subject)} |`);
  if (rows.some((row) => row.trains.split(" ").includes(DEPENDENCIES))) {
    lines.push("", `${DEPENDENCIES}: a changed <PackageVersion> releases the trains whose restore graph holds the package, ` +
      "which this check does not restore; Monday's version job reads them.");
  }
  return `${lines.join("\n")}\n`;
}

function main(argv) {
  const repository = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
  let list = join(repository, "src", "release-trains.json");
  const args = [...argv];
  if (args[0] === "--list") {
    list = resolve(args[1] ?? "");
    args.splice(0, 2);
  }
  const [command, ...rest] = args;
  const out = (lines) => lines.length > 0 && process.stdout.write(`${lines.join("\n")}\n`);
  switch (command) {
    case "check": {
      // The list lives in src/, beside the projects it names.
      const faults = validate(readFileSync(list, "utf8"), dirname(list));
      for (const fault of faults) console.error(`::error::src/release-trains.json: ${fault}.`);
      return faults.length === 0 ? 0 : 1;
    }
    case "trains": {
      const model = loadTrains(list);
      out(model.names.map((name) => `${name} ${tagPrefixOf(name)} ${model.trains.get(name).breaking}`));
      return 0;
    }
    case "trains-of": {
      const model = loadTrains(list);
      out(rest.map((project) => `${project} ${trainOf(model, project)}`));
      return 0;
    }
    case "versions": {
      const model = loadTrains(list);
      const versions = parseVersions(model, rest[0] ?? "");
      out(rest.slice(1).map((project) => `${project}=${versions.get(trainOf(model, project))}`));
      return 0;
    }
    case "commits":
    case "lint-commits": {
      const [from, to] = rest;
      if (!from || !to) throw new Error(`${command} takes <from> <to>`);
      // The tree the list is in: the repository's own, or the one --list names.
      const root = resolve(dirname(list), "..");
      const m = readModel(root, { graph: command === "commits", list });
      const commits = trainsOfCommits(m, git(root), `${from}..${to}`);
      if (command === "commits") {
        out([...commits].reverse().map(({ hash, message, trains }) => `${hash}\t${named(m, trains)}\t${subjectOf(message)}`));
        return 0;
      }
      // Advice, never a gate: the required check's result is the commit messages' alone.
      const lint = lintCommits(m, commits);
      if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, lintSummary(lint));
      out(lint.rows.map(({ hash, trains, subject }) => `${hash.slice(0, 7)}\t${trains}\t${subject}`));
      for (const warning of lint.warnings) console.log(`::warning title=A commit releases two trains::${warning}.`);
      return 0;
    }
    default:
      console.error("::error::release-trains.mjs takes check, trains, trains-of, versions, commits or lint-commits.");
      return 2;
  }
}

/** git in a repository, its output as text; a failing command throws. */
export const git = (cwd) => (...args) =>
  execFileSync("git", args, { cwd, encoding: "utf8", maxBuffer: 256 * 1024 * 1024, stdio: ["ignore", "pipe", "pipe"] });

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    process.exitCode = main(process.argv.slice(2));
  } catch (error) {
    console.error(`::error::${error.message}`);
    process.exitCode = 1;
  }
}
