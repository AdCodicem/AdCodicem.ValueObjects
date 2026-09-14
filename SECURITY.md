# Security policy

## Supported versions

This project has not reached 1.0 yet. Only the most recently published version
receives security fixes — while the version number starts with `0.`, the fix
ships in the next release rather than as a patch to an older line.

## Reporting a vulnerability

Please **do not** open a public issue for a security vulnerability.

Use GitHub's private vulnerability reporting instead: the **Security** tab of
this repository → **Report a vulnerability**. That opens an advisory visible
only to the maintainer, which is the fastest way to get a fix moving without
disclosing the issue before a patch exists.

Expect an initial response within a few days. If the report is confirmed, the
fix is prepared privately and a GitHub Security Advisory is published alongside
the patched release, crediting the reporter unless you would rather stay
anonymous.

## Scope

These packages generate code that runs inside a consumer's application and
carry no network or process boundary of their own, so the interesting reports
tend to be:

- A generated member that parses attacker-controlled input unsafely — the
  `Parse` / `TryParse` surface, the `Pattern` regex (including a pattern that
  makes catastrophic backtracking reachable), or the span-based normalizers.
- A validation rule that can be bypassed, letting a value object hold a value
  its declaration forbids. `CreateUnchecked` is deliberately unchecked and
  documented as such, so its use by a consumer is not in itself a finding.
- An integration that lets a value cross a boundary without its rules — the
  model binder, the EF Core converter, the Dapper handler, or a JSON converter.

A vulnerability in a dependency is better reported upstream; open an issue here
only if this repository pins a version that is known to be affected.
