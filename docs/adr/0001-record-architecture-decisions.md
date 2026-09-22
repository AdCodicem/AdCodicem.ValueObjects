# 1. Record architecture decisions

Date: 2026-09-14

## Status

Accepted

## Context

Decisions about this project's structure, dependencies and trade-offs tend to get made once — in a pull request
discussion, or a conversation — and then forgotten. Later nobody knows *why* something is the way it is, only
that it is.

This repository already documents a good deal of its reasoning: [Design
decisions](https://adcodicem.github.io/AdCodicem.ValueObjects/docs/design-decisions) on the site explains the shape
of the generated code, and `CLAUDE.md` lists the constraints that cost real debugging time. What neither
records is *when* a decision was taken, what was rejected, or what would have to change for it to be revisited.

## Decision

We will record significant, hard-to-reverse architectural decisions as Architecture Decision Records in
`docs/adr/`, one file per decision, following [`adr-template.md`](adr-template.md) — the format proposed by
Michael Nygard.

Not every decision needs one. An ADR is warranted when a decision is costly to reverse, or when a future
contributor would otherwise have to re-derive it from git blame. Where the reasoning is already written down on
the documentation site, the ADR links to it rather than copying it — the site is the explanation, the ADR is
the record.

## Consequences

Slightly more friction for the decisions that qualify. In exchange, the reasoning behind the current shape
stays discoverable instead of living only in closed threads.

A second, subtler cost: two places now describe the same decisions from different angles, and they can drift.
The rule above — ADRs link, the site explains — is what keeps that bounded.
