# Security policy

## Supported versions

Only the latest stable release receives security fixes. There is no
maintenance branch: a fix ships in the next release, never as a patch to an
earlier line, and a project held on an older framework major stays on the
version before the packages moved past it
([ADR-0010](docs/adr/0010-version-every-package-in-lockstep-independently-of-dotnet.md)).
Previews are not supported: they receive no fixes of their own, and a fix
reaches them only as it reaches `main`, in the next weekly preview.

## Reporting a vulnerability

Please **do not** open a public issue for a security vulnerability.

Use GitHub's private vulnerability reporting instead:
<https://github.com/AdCodicem/AdCodicem.ValueObjects/security/advisories/new>, or
the **Security** tab of this repository → **Report a vulnerability**. That opens
an advisory visible only to the maintainer, which is the fastest way to get a fix
moving without disclosing the issue before a patch exists.

Expect an initial response within a few days. If the report is confirmed, the
fix is prepared privately and a GitHub Security Advisory is published alongside
the patched release, crediting the reporter unless you would rather stay
anonymous.

## Verifying a package

Every package is attested together with each assembly inside it: a SLSA build
provenance attestation, signed through Sigstore, proves which workflow run built
each file, and the commit that run was started on. That is the commit the file
was built from, with one exception: a preview that completes a version an
earlier run left partly published. That preview is packed again from the earlier
commit, which its nuspec names in `<repository commit="…"/>` and its assemblies
in their informational version (`<version>+<commit>`), while the attestation
names the commit the completing run started on; so do not use `--source-digest`
to pin a preview to its nuspec's commit. Previews are attested by `preview.yml`
before they are pushed, stable releases by `release.yml`, which also attaches
the bundle to the
[GitHub Release](https://github.com/AdCodicem/AdCodicem.ValueObjects/releases).
That holds from the first versions published since assemblies became subjects,
`0.3.0-preview.<N>` and `0.3.0` on. Earlier stable releases attest at most
their `.nupkg` and `.snupkg` files, and earlier previews nothing.

The files attached to a GitHub Release verify as they are:

```sh
gh attestation verify AdCodicem.ValueObjects.<version>.nupkg --repo AdCodicem/AdCodicem.ValueObjects
```

A package restored from nuget.org is checked through its assemblies.
nuget.org adds its own repository signature to every `.nupkg` it accepts, which
changes the file's digest, but leaves the files inside as they were packed, and
NuGet extracts them unchanged into the global packages folder:

```sh
gh attestation verify \
  ~/.nuget/packages/adcodicem.valueobjects.json/<version>/lib/net10.0/AdCodicem.ValueObjects.Json.dll \
  --repo AdCodicem/AdCodicem.ValueObjects
```

The main package, `AdCodicem.ValueObjects`, ships one assembly, the generator:
`analyzers/dotnet/cs/AdCodicem.ValueObjects.Generators.dll`. To accept only
one kind of build, add `--signer-workflow`:
`AdCodicem/AdCodicem.ValueObjects/.github/workflows/release.yml` accepts stable
releases alone, `AdCodicem/AdCodicem.ValueObjects/.github/workflows/preview.yml`
previews alone.

To restrict restores to packages published by this account on nuget.org, use
`dotnet nuget trust repository nuget.org --owners AdCodicem`.

## Scope

These packages generate code that runs inside a consumer's application and
carry no network or process boundary of their own, so the interesting reports
tend to be:

- A generated member that parses attacker-controlled input unsafely — the
  `Parse` / `TryParse` surface, the regex of the deprecated `Pattern` option
  (including a pattern that makes catastrophic backtracking reachable), or the
  span-based normalizers.

  A pattern declared through `IValueObjectPatternValidator` is a
  `[GeneratedRegex]` the consumer writes, so its match timeout is the
  consumer's to set: the `Pattern` option fixed one second, the hook fixes
  nothing. `VO0026` warns on a `[GeneratedRegex]` without
  `matchTimeoutMilliseconds`. A consumer's pattern that backtracks with no
  timeout is therefore not a finding in this repository.
- A validation rule that can be bypassed, letting a value object hold a value
  its declaration forbids. `CreateUnchecked` is deliberately unchecked and
  documented as such, so its use by a consumer is not in itself a finding.
- An integration that lets a value cross a boundary without its rules — the
  model binder, the EF Core converter, the Dapper handler, or a JSON converter.

A vulnerability in a dependency is better reported upstream; open an issue here
only if this repository pins a version that is known to be affected.
