# website — the documentation site

Docusaurus, published to https://adcodicem.github.io/AdCodicem.ValueObjects/. Edit the source of truth first (the
README, a `CLAUDE.md`, the benchmark numbers), then the page here that reorganises it.

```bash
npm ci
npm run docs:api    # regenerate docs/api/ from the XML doc comments (DocFX); needs a real Python 3
npm start           # local dev server
npm run build       # production build; fails on a broken internal link
```

- Run `npm run docs:api` before `start` or `build` on a fresh checkout: `sidebars.ts` builds its API reference category
  from `docs/api/`, which is generated and gitignored. It ends with `python3 .github/scripts/docfx-postprocess.py`.
- `docs/` is the preview: it describes `main` and is served under `/docs/preview/`, redeployed by every `preview.yml` run
  that decides, labelled with the nuget.org version describing the commit it builds. A change there reaches the stable
  pages at the next release, not before (ADR 0005, amended by ADR 0009).
- `versioned_docs/` holds one frozen snapshot per line of the core (`0.<minor>.x` before its 1.0, `<major>.x` after),
  generated API reference included, written wholesale by `.github/scripts/docs-snapshot.sh` in `release.yml`'s pack job
  and committed by semantic-release. Never add or remove an entry by hand. Edit a released page only to correct an error
  that misleads users of that release, and land the same fix in `docs/`, or the next snapshot of the line discards it.
- `released-versions.json` maps each line to the release it was frozen at: one version (`"0.2.1"`), or each train's
  (`{"core": "0.3.2", "satellites": "0.4.1"}`), which `describeLine` in `docusaurus.config.ts` turns into the version
  label and the scope sentence of `llms.txt` (`core 0.3.2, satellites 0.4.1`, or the one version every train shipped).
  Only `docs-snapshot.sh` writes it.
- A pull request changing `website/`, `docs/docfx/`, the action or a project under `src/` builds the site without
  deploying it in `ci.yml`'s `documentation site` job, through `.github/actions/build-site`, the action
  `deploy-docs.yml` builds with, so a broken link fails there; every push to `main` builds it too.
- Keep each `<c>` of an XML doc comment on one line: DocFX writes it as a `<code>` over a line break, which MDX closes
  early, and the build then warns for the API page ("No "p" element in scope but a "p" end tag seen").
- Every ```` ```csharp ```` block of `docs/` (and the homepage example, `docs/_homepage-example.md`) is compiled by
  GeneratorTests' `DocumentationSnippetTests`; tag a wiring or usage fragment ```` ```csharp skip ````
  (`tests/AdCodicem.ValueObjects.GeneratorTests/CLAUDE.md`). `versioned_docs/` is out of its scope.
- `package.json` carries `overrides` for `postcss-selector-parser`, `qs`, `serialize-javascript`, `tinypool` and `uuid`,
  transitive under Docusaurus, which pins ranges too tight to pick up the patched versions. Never run
  `npm audit fix --force`: it "fixes" them by downgrading `@docusaurus/core`. Drop an override only once Docusaurus widens
  the range that holds it back: `tinypool` once `@docusaurus/core` asks for `^2.1.2` (3.10.2 asks for `^1.0.2`, and no
  1.x is patched; the pool builds the static pages, `future.v4` turning on its worker threads), `postcss-selector-parser`
  once `@docusaurus/cssnano-preset` leaves cssnano 6 (it asks for `cssnano-preset-advanced` `^6.1.2`, and only 7.1.6 is
  patched).
- After touching the overrides, re-run `npm audit`: it reports `braces` alone, which has no patched release.
