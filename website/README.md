# AdCodicem.ValueObjects docs site

The [documentation site](https://adcodicem.github.io/AdCodicem.ValueObjects/), built with
[Docusaurus](https://docusaurus.io/). Content lives in `docs/`; the sidebar order is explicit in `sidebars.ts`.

```bash
npm ci
npm start          # local dev server with hot reload
npm run build       # production build; fails on a broken internal link
npm run serve        # serve the production build locally
npm run typecheck   # TypeScript, no emit
```

Deployment is automatic: `.github/workflows/deploy-docs.yml` builds and publishes this site to GitHub Pages on
every push to `main` that touches this directory. There is no `deploy` script here — the site is not pushed to
a `gh-pages` branch by hand.
