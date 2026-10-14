#!/usr/bin/env node
// Checks the patterns the Blazor package writes for browsers in a JavaScript engine, as a browser compiles the pattern
// attribute of an input: new RegExp("^(?:" + pattern + ")$", "v"), the v flag the HTML standard compiles it with.
//
// Usage: node .github/scripts/ecmascript-patterns.mjs [table]
//        (default tests/AdCodicem.ValueObjects.UnitTests/Forms/EcmaScriptPatterns.json)
//
// The table is the one EcmaScriptPatternTests pins: each .NET pattern, the attribute the package writes for it, or null
// where it writes none, and texts .NET accepts and refuses. The unit test checks the written attribute and .NET's
// verdicts; this script checks that every attribute written compiles, accepts each text .NET accepts and refuses each
// text .NET refuses. ci.yml's documentation site job runs it, where Node is installed for the site.
import { readFileSync } from "node:fs";

const table = process.argv[2] ?? "tests/AdCodicem.ValueObjects.UnitTests/Forms/EcmaScriptPatterns.json";
const entries = JSON.parse(readFileSync(table, "utf8"));
let failures = 0;
let checked = 0;

const fail = (entry, message) => {
  failures++;
  console.log(`::error file=${table}::${entry.name} (${entry.pattern}): ${message}`);
};

for (const entry of entries) {
  if (entry.written === null) {
    continue;
  }

  let regex;
  try {
    regex = new RegExp(`^(?:${entry.written})$`, "v");
  } catch (error) {
    fail(entry, `the browser has no compiled pattern: ${error.message}`);
    continue;
  }

  for (const text of entry.accepted) {
    if (!regex.test(text)) {
      fail(entry, `a browser refuses ${JSON.stringify(text)}, which .NET accepts`);
    }
  }

  for (const text of entry.refused) {
    if (regex.test(text)) {
      fail(entry, `a browser accepts ${JSON.stringify(text)}, which .NET refuses`);
    }
  }

  checked++;
}

if (failures > 0) {
  process.exit(1);
}

console.log(`${checked} patterns written for browsers read as .NET reads them (Node ${process.version}).`);
