---
title: Use With an AI Coding Agent
sidebar_label: AI coding agents
slug: /how-to/ai-agents
description: Teach a coding agent the AdCodicem.ValueObjects authoring surface with the agent skill, installed as a Claude Code plugin or read as plain Markdown.
---

# Use with an AI coding agent

Because the whole implementation is generated, a model that has never seen this library guesses its surface
wrong: a hand-written factory, a `record struct`, a `JsonConverter` nobody needs, a rule that never runs because
its interface was not declared.

The repository ships an agent skill that states the surface precisely: the attribute options, the hook
interfaces, the wiring of each integration, and every diagnostic with its fix. Every C# snippet in it is compiled
by the generator's test suite, so it cannot drift away from what the generator accepts.

## Claude Code

```
/plugin marketplace add AdCodicem/AdCodicem.ValueObjects
/plugin install adcodicem-valueobjects@adcodicem
```

The skill loads whenever a project references AdCodicem.ValueObjects, a primitive is being wrapped in a domain
type, or a `VO00xx` diagnostic needs fixing.

## Other agents

The skill is plain Markdown under
[`skills/value-objects/`](https://github.com/AdCodicem/AdCodicem.ValueObjects/tree/main/skills/value-objects):
point any agent at `SKILL.md` and its `references/` folder.

## For assistants that read documentation

This site publishes [`llms.txt`](pathname:///llms.txt), an index of these pages for language models, and
[`llms-full.txt`](pathname:///llms-full.txt), the documentation of the latest release in a single Markdown file.
