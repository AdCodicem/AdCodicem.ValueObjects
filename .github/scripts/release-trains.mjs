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
//
// --list reads another copy of the list (an older commit's, a test's); by default the one under src/ of the
// repository this script is in. `check` reads the projects of the folder the list is in.
//
// MSBuild reads the same file at evaluation (src/Directory.Build.props), with no JSON parser: a regular expression
// finds the innermost object holding the project's name as a string. That holds whatever the order of the keys, as
// long as a train's object holds no other object, a name sits in one train only and no string holds a brace, which
// `check` makes sure of, so the two readers cannot disagree. ci.yml compares their answers at every pack. Node's
// built-ins only, nothing newer than Node 20: the plan job runs the runner's own Node, with no npm install.
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const TRAIN_NAME = /^[a-z][a-z0-9-]*$/;
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
    const references = [...text.matchAll(/<ProjectReference\b[^>]*\bInclude="([^"]+)"/g)].map(([, include]) =>
      include.replaceAll("\\", "/").split("/").at(-1).replace(/\.csproj$/, "")
    );
    projects.set(entry.name, { references, setsPackageId: /<PackageId\b/.test(text) });
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
  for (const [project, { references, setsPackageId }] of projects) {
    // The list, MSBuild's reading and the package ids are one name: a PackageId of another spelling would ride a
    // train under a name the list does not know.
    if (setsPackageId) faults.push(`src/${project}/${project}.csproj sets a PackageId: the trains name packages by project`);
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
    default:
      console.error("::error::release-trains.mjs takes check, trains, trains-of or versions.");
      return 2;
  }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    process.exitCode = main(process.argv.slice(2));
  } catch (error) {
    console.error(`::error::${error.message}`);
    process.exitCode = 1;
  }
}
