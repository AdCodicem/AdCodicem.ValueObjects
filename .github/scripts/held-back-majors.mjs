#!/usr/bin/env node
// The majors Dependabot holds back: every pin a `version-update:semver-major` ignore rule of .github/dependabot.yml
// matches, set against the newest stable version its registry lists, so that a major nobody proposes any more still
// shows (docs/adr/0012-version-the-packages-in-two-trains-independently-of-the-frameworks.md, "a weekly check lists
// the majors that it holds back"). A rule that also holds `version-update:semver-minor` back, Roslyn's, whose
// major.minor is the oldest compiler that loads the generator, is read at major.minor.
//
// Usage: node .github/scripts/held-back-majors.mjs [--root <dir>] <report.md>
//   Reads, under the root (by default the repository this script is in), .github/dependabot.yml and the pins its
//   rules can match: Directory.Packages.props and .config/dotnet-tools.json for the nuget entry on "/", package.json
//   at package-lock.json's versions for the npm entry on "/". Writes a Markdown report, appends it to
//   $GITHUB_STEP_SUMMARY, writes held=<count> to $GITHUB_OUTPUT, and prints one ::warning:: per pin held back.
//   NUGET_FLAT_CONTAINER and NPM_REGISTRY replace nuget.org's flat container and npm's registry (the tests' fakes).
//
// It exits 0 whatever it finds, and 1 only when a lookup fails or a file holds a shape it cannot read: an ignore
// rule with a key other than dependency-name and update-types, or under an entry whose pins it has no reader for. A
// reshaped dependabot.yml then fails the check rather than shorten its list. Node's built-ins only, nothing newer
// than Node 20: held-back-majors.yml runs the runner's own Node, with no npm install.
import { appendFileSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const MAJOR = "version-update:semver-major";
const MINOR = "version-update:semver-minor";
const UPDATE_TYPES = [MAJOR, MINOR, "version-update:semver-patch"];

/**
 * The ignore rules of dependabot.yml, each { ecosystem, directory, name, types, line }. A narrow reader of the file's
 * one shape: two-space indentation, an update entry opening with `- package-ecosystem:`, its keys four spaces in, an
 * ignore rule opening with `- dependency-name:` six spaces in, its update-types eight spaces in. Throws on any other
 * shape of an entry's opening, of its ignore key or under it, and when it read fewer rules than the file names
 * dependencies, so that no rule is skipped or filed under another entry.
 */
export function readIgnoreRules(text) {
  const rules = [];
  let entry;
  let inIgnore = false;
  const lines = text.split(/\r?\n/);
  for (let index = 0; index < lines.length; index++) {
    const raw = lines[index];
    const where = `dependabot.yml:${index + 1}`;
    if (/^\s*(#.*)?$/.test(raw)) continue;
    const indent = raw.length - raw.trimStart().length;
    const line = raw.trim();
    let match;
    if (indent === 2 && (match = line.match(/^- package-ecosystem:\s*"?([^"\s]+)"?$/))) {
      entry = { ecosystem: match[1], directory: undefined };
      inIgnore = false;
    } else if (indent === 2 && line.startsWith("-")) {
      throw new Error(`${where}: an update entry must open with - package-ecosystem: "<ecosystem>", not ${line}`);
    } else if (indent <= 2) {
      entry = indent === 0 ? undefined : entry;
      inIgnore = false;
    } else if (entry && indent === 4) {
      if (line.startsWith("-") || (/^(ignore|package-ecosystem)\b/.test(line) && line !== "ignore:")) {
        throw new Error(`${where}: an update entry's key must read ignore: alone, or be another key, not ${line}`);
      }
      inIgnore = line === "ignore:";
      if ((match = line.match(/^directory:\s*"?([^"\s]+)"?$/))) entry.directory = match[1];
    } else if (inIgnore && indent === 6) {
      match = line.match(/^- dependency-name:\s*"([^"]+)"$/);
      if (!match) throw new Error(`${where}: an ignore rule must open with - dependency-name: "<name>", not ${line}`);
      if (entry.directory === undefined) {
        throw new Error(`${where}: an ignore rule under an entry with no single directory has no reader`);
      }
      const { ecosystem, directory } = entry;
      rules.push({ ecosystem, directory, name: match[1], types: [], line: index + 1 });
    } else if (inIgnore && indent === 8) {
      match = line.match(/^update-types:\s*\[(.*)\]$/);
      if (!match || rules.length === 0) {
        throw new Error(`${where}: an ignore rule holds only dependency-name and update-types, not ${line}`);
      }
      const types = match[1].split(",").map((type) => type.trim().replace(/^"|"$/g, "")).filter(Boolean);
      const unknown = types.find((type) => !UPDATE_TYPES.includes(type));
      if (unknown) throw new Error(`${where}: ${unknown} is no update type`);
      rules.at(-1).types.push(...types);
    } else if (inIgnore) {
      throw new Error(`${where}: unexpected indentation under ignore: ${line}`);
    }
  }
  const named = lines.filter((line) => /^\s*-?\s*dependency-name:/.test(line)).length;
  if (named !== rules.length) {
    throw new Error(`dependabot.yml names ${named} dependencies in ignore rules, of which this reader read ` +
      `${rules.length}`);
  }
  const nameOnly = rules.find((rule) => rule.types.length === 0);
  if (nameOnly) {
    throw new Error(`dependabot.yml:${nameOnly.line}: ${nameOnly.name} is ignored by name alone, which holds back ` +
      "every update of it, security fixes included: give it update-types");
  }
  return rules;
}

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

/** The PackageVersion entries of Directory.Packages.props, comments left out, each { id, version, condition }. */
export function readPackageVersions(text) {
  const pins = [];
  const attribute = (attributes, name) => attributes.match(new RegExp(`\\b${name}="([^"]*)"`))?.[1];
  const entries = withoutComments(text, "Directory.Packages.props").matchAll(/<PackageVersion\b([^>]*?)\/>/g);
  for (const [, attributes] of entries) {
    const id = attribute(attributes, "Include");
    const version = attribute(attributes, "Version");
    if (!id || !version) {
      throw new Error(`Directory.Packages.props: a PackageVersion lacks Include or Version: ${attributes.trim()}`);
    }
    pins.push({ id, version, condition: attribute(attributes, "Condition") });
  }
  return pins;
}

/** The pins an entry's rules can match, by "<ecosystem> <directory>"; throws for an entry with no reader. */
export function readPins(root, ecosystem, directory) {
  const read = (path) => readFileSync(join(root, path), "utf8");
  if (ecosystem === "nuget" && directory === "/") {
    const tools = Object.entries(JSON.parse(read(".config/dotnet-tools.json")).tools ?? {})
      .map(([id, tool]) => ({ id, version: tool.version, condition: undefined }));
    return [...readPackageVersions(read("Directory.Packages.props")), ...tools];
  }
  if (ecosystem === "npm" && directory === "/") {
    const manifest = JSON.parse(read("package.json"));
    const lock = JSON.parse(read("package-lock.json"));
    return Object.keys({ ...manifest.dependencies, ...manifest.devDependencies }).map((id) => {
      const version = lock.packages?.[`node_modules/${id}`]?.version;
      if (!version) throw new Error(`package-lock.json resolves no version of ${id}`);
      return { id, version, condition: undefined };
    });
  }
  throw new Error(`no reader for the pins of the ${ecosystem} entry on ${directory}`);
}

/** Whether a dependency-name pattern, where * is the only wildcard, matches an id, ignoring case as Dependabot does. */
export function matches(pattern, id) {
  const source = pattern.split("*").map((part) => part.replace(/[.+?^${}()|[\]\\]/g, "\\$&")).join(".*");
  return new RegExp(`^${source}$`, "i").test(id);
}

/** The numeric parts of a stable version, or undefined for a prerelease or what is no version. */
export function stableParts(version) {
  if (!/^\d+(\.\d+)*$/.test(version)) return undefined;
  return version.split(".").map(Number);
}

const compare = (left, right) => {
  for (let index = 0; index < Math.max(left.length, right.length); index++) {
    const difference = (left[index] ?? 0) - (right[index] ?? 0);
    if (difference !== 0) return difference;
  }
  return 0;
};

/**
 * Whether a pin is held back, given every version its registry lists: { newest, newestOnLine } when the newest stable
 * version is on a later line than the pin's (a later major, or a later major.minor when the minor is held too), else
 * undefined. A pin that is itself a prerelease is read on the line of its numbers.
 */
export function heldBack(pinned, versions, holdsMinor) {
  const pin = stableParts(pinned.split("-")[0]);
  if (!pin) throw new Error(`${pinned} is no version`);
  const stable = versions.map((version) => [version, stableParts(version)]).filter(([, parts]) => parts);
  if (stable.length === 0) return undefined;
  stable.sort(([, left], [, right]) => compare(left, right));
  const width = holdsMinor ? 2 : 1;
  const line = (parts) => [parts[0], parts[1] ?? 0].slice(0, width);
  const [newest, newestParts] = stable.at(-1);
  if (compare(line(newestParts), line(pin)) <= 0) return undefined;
  const onLine = stable.filter(([, parts]) => compare(line(parts), line(pin)) === 0).at(-1);
  return { newest, newestOnLine: onLine?.[0] };
}

/** "only <project>" or "all but <project>" for a pin conditioned on MSBuildProjectName, the condition itself else. */
export function scopeOf(condition) {
  if (!condition) return "";
  const match = condition.match(/^'\$\(MSBuildProjectName\)'\s*(==|!=)\s*'([^']+)'$/);
  if (!match) return condition;
  return match[1] === "==" ? `only ${match[2]}` : `all but ${match[2]}`;
}

async function fetchJson(url, attempts = 3) {
  let failure;
  for (let attempt = 1; attempt <= attempts; attempt++) {
    try {
      const response = await fetch(url, { headers: { accept: "application/json" } });
      if (response.status === 200) return await response.json();
      failure = new Error(`${url} answered ${response.status}`);
      if (response.status === 404) break;
    } catch (error) {
      failure = new Error(`${url}: ${error.message}`);
    }
    if (attempt < attempts) await new Promise((done) => setTimeout(done, 1000 * attempt));
  }
  throw failure;
}

/** Every version a registry lists for an id. */
async function versionsOf(ecosystem, id, env) {
  if (ecosystem === "nuget") {
    const base = (env.NUGET_FLAT_CONTAINER ?? "https://api.nuget.org/v3-flatcontainer").replace(/\/$/, "");
    return (await fetchJson(`${base}/${id.toLowerCase()}/index.json`)).versions ?? [];
  }
  const base = (env.NPM_REGISTRY ?? "https://registry.npmjs.org").replace(/\/$/, "");
  return Object.keys((await fetchJson(`${base}/${id.replaceAll("/", "%2f")}`)).versions ?? {});
}

/**
 * { rows, stale }: the pins held back, each { ecosystem, id, pinned, newest, newestOnLine, held, scope }, in the
 * file's order, and the ignore rules that match no pin of their entry, each { ecosystem, name, line }.
 */
export async function findHeldBack(root, env = process.env) {
  const rules = readIgnoreRules(readFileSync(join(root, ".github/dependabot.yml"), "utf8"));
  const rows = [];
  const stale = [];
  const entries = [...new Set(rules.map((rule) => `${rule.ecosystem} ${rule.directory}`))];
  for (const key of entries) {
    const [ecosystem, directory] = key.split(" ");
    const entryRules = rules.filter((rule) => rule.ecosystem === ecosystem && rule.directory === directory);
    const listed = new Map();
    const pins = readPins(root, ecosystem, directory);
    for (const rule of entryRules.filter((rule) => !pins.some((pin) => matches(rule.name, pin.id)))) {
      stale.push({ ecosystem, name: rule.name, line: rule.line });
    }
    for (const pin of pins) {
      const types = entryRules.filter((rule) => matches(rule.name, pin.id)).flatMap((rule) => rule.types);
      if (!types.includes(MAJOR)) continue;
      const holdsMinor = types.includes(MINOR);
      if (!listed.has(pin.id)) listed.set(pin.id, await versionsOf(ecosystem, pin.id, env));
      const found = heldBack(pin.version, listed.get(pin.id), holdsMinor);
      if (found) {
        rows.push({ ecosystem, id: pin.id, pinned: pin.version, ...found, held: holdsMinor ? "minor" : "major",
          scope: scopeOf(pin.condition) });
      }
    }
  }
  return { rows, stale };
}

/**
 * The line a held pin waits for: the newest version's major, or its major.minor when the minor is held. A row keeps
 * its key while later versions arrive on that line, so that the issue says the list changed only when a pin or a
 * line does.
 */
export const keyOf = (row) => {
  const [major, minor = "0"] = row.newest.split(".");
  return `${row.id}@${row.pinned}:${row.held === "minor" ? `${major}.${minor}` : major}`;
};

/**
 * The report: a heading, a sentence, a table when anything is held back, the rules that match no pin, and a hidden
 * marker of the rows' keys, which held-back-majors.yml compares with the issue's to comment when the list changed.
 */
export function report({ rows, stale = [] }) {
  const marker = `<!-- held-back: ${rows.map(keyOf).sort().join(" ")} -->`;
  const lines = [marker, "## Dependency majors Dependabot holds back", ""];
  if (stale.length > 0) {
    lines.push(
      "Rules of `.github/dependabot.yml` that match no pin, which may be left over from a dependency removed since:",
      "",
      ...stale.map((rule) => `- ${rule.ecosystem} \`${rule.name}\` (line ${rule.line})`),
      "");
  }
  if (rows.length === 0) {
    lines.push("Dependabot holds back no major: every pin its ignore rules match is on its newest line.");
    return `${lines.join("\n")}\n`;
  }
  lines.push(
    "The pins `.github/dependabot.yml` holds back from their newest stable line, which Dependabot no longer " +
      "proposes. A new major is supported by a decision, a dependency floor or a target framework, never by a bump " +
      "(docs/maintaining.md, Dependabot); each rule's reason sits above it in `.github/dependabot.yml`.",
    "",
    "| Ecosystem | Package | Pinned | Newest stable | Newest on the pinned line | Held | Scope |",
    "| --- | --- | --- | --- | --- | --- | --- |",
    ...rows.map((row) => `| ${row.ecosystem} | \`${row.id}\` | ${row.pinned} | ${row.newest} | ` +
      `${row.newestOnLine ?? "—"} | ${row.held} | ${row.scope} |`));
  return `${lines.join("\n")}\n`;
}

async function main(argv, env) {
  const args = [...argv];
  let root = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
  if (args[0] === "--root") {
    root = resolve(args[1]);
    args.splice(0, 2);
  }
  if (args.length !== 1) throw new Error("usage: held-back-majors.mjs [--root <dir>] <report.md>");
  const { rows, stale } = await findHeldBack(root, env);
  const text = report({ rows, stale });
  writeFileSync(args[0], text);
  if (env.GITHUB_STEP_SUMMARY) appendFileSync(env.GITHUB_STEP_SUMMARY, text);
  if (env.GITHUB_OUTPUT) appendFileSync(env.GITHUB_OUTPUT, `held=${rows.length}\n`);
  for (const rule of stale) {
    const where = `dependabot.yml:${rule.line}`;
    console.log(`::warning title=Stale ignore rule::${rule.ecosystem} ${rule.name} (${where}) matches no pin`);
  }
  for (const row of rows) {
    const held = `the ${row.held} is held back`;
    console.log(`::warning title=Held back::${row.id} ${row.pinned}: ${row.newest} is out (${held})`);
  }
  console.log(`${rows.length} pin(s) held back.`);
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main(process.argv.slice(2), process.env).catch((error) => {
    console.error(`::error::${error.message}`);
    process.exit(1);
  });
}
