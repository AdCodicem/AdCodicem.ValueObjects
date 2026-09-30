import fs from 'node:fs';
import path from 'node:path';
import {themes as prismThemes} from 'prism-react-renderer';
import type {Config, Plugin} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';
import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';
import sidebars from './sidebars';

// This runs in Node.js - Don't use client-side code here (browser APIs, JSX...)

// The site is versioned (docs/adr/0005-version-the-documentation-site.md). website/docs/ is
// the preview: it describes main, and every merge that publishes a preview package redeploys
// it. Each stable release freezes it into versioned_docs/ through
// .github/scripts/docs-snapshot.sh, one entry per line of versions -- 0.3.x while the major is
// 0, 1.x from 1.0 on -- replaced in place when that line ships again.
function readJson<T>(file: string, fallback: T): T {
  const fullPath = path.join(__dirname, file);
  return fs.existsSync(fullPath) ? (JSON.parse(fs.readFileSync(fullPath, 'utf8')) as T) : fallback;
}

// Stable lines, newest first, as `docusaurus docs:version` records them. Absent until the
// first stable release.
const stableLines = readJson<string[]>('versions.json', []);
// The exact release each line was last frozen at: {"0.3.x": "0.3.2"}.
const releasedVersions = readJson<Record<string, string>>('released-versions.json', {});
const hasStable = stableLines.length > 0;

// deploy-docs.yml sets this from MinVer, so the preview names the package it describes. A local
// build has no published version to name.
const previewVersion = process.env.DOCS_PREVIEW_VERSION;
const previewLabel = previewVersion ? `Preview (${previewVersion})` : 'Preview';

const editRoot = 'https://github.com/AdCodicem/AdCodicem.ValueObjects/edit/main/website';

// The homepage is not versioned, but its example must describe what `dotnet add package`
// installs, so it is a partial that is frozen with the rest of the documentation and read from
// the latest stable snapshot. Before the first stable release there is none, and the preview is
// what the site serves by default anyway.
function homepageExample(): Plugin {
  const partial = '_homepage-example.md';
  const source = hasStable
    ? path.join(__dirname, 'versioned_docs', `version-${stableLines[0]}`, partial)
    : path.join(__dirname, 'docs', partial);
  // Falling back to docs/ here would quietly put unreleased code on the homepage, which is
  // exactly what reading the snapshot is for. A renamed partial has to wait for the next release.
  if (!fs.existsSync(source)) {
    throw new Error(`The homepage example ${path.relative(__dirname, source)} does not exist.`);
  }
  return {
    name: 'homepage-example',
    configureWebpack: () => ({resolve: {alias: {'@homepage-example': source}}}),
  };
}

// llms.txt and llms-full.txt (https://llmstxt.org/), for assistants that read documentation as text rather than
// HTML. Like the homepage example they describe the latest stable line, because that is what an assistant's user
// will install; the preview is only linked. Written after the build, so `npm start` does not serve them.
function llmsTxt(): Plugin {
  const line = hasStable ? stableLines[0] : undefined;
  const docsDir = line ? path.join(__dirname, 'versioned_docs', `version-${line}`) : path.join(__dirname, 'docs');
  const sidebar = line
    ? readJson<SidebarsConfig>(`versioned_sidebars/version-${line}-sidebars.json`, {})
    : sidebars;
  const sections = sidebarSections(sidebar.docsSidebar as SidebarEntry[]);

  return {
    name: 'llms-txt',
    async postBuild({outDir, siteConfig}) {
      const site = `${siteConfig.url}${siteConfig.baseUrl}`;
      const readPage = (id: string) => {
        const source = fs.readFileSync(path.join(docsDir, `${id}.md`), 'utf8');
        const [, frontMatter = '', body = source] = /^---\n([\s\S]*?)\n---\n([\s\S]*)$/.exec(source) ?? [];
        const field = (name: string) =>
          new RegExp(`^${name}:\\s*(.+)$`, 'm').exec(frontMatter)?.[1].trim().replace(/^(['"])(.*)\1$/, '$2');
        const text = body.trim();
        return {
          title: field('title') ?? id,
          url: `${site}docs${field('slug') ?? `/${id}`}`,
          summary: field('description') ?? lede(text),
          text,
        };
      };
      const grouped = sections.map((section) => ({label: section.label, pages: section.ids.map(readPage)}));
      const pages = grouped.flatMap((section) => section.pages);

      const release = line ? releasedVersions[line] ?? line : undefined;
      const scope = release
        ? `These pages describe ${release}, the latest stable release.`
        : 'No stable release exists yet: these pages describe the latest preview.';

      const index = [
        `# ${siteConfig.title}`,
        '',
        `> ${siteConfig.tagline}`,
        '',
        'Annotate a `readonly partial struct` with `[ValueObject<T>]` and a Roslyn source generator writes the rest: ' +
          'construction that normalizes then validates, parsing, formatting, equality, ordering, and the JSON, EF ' +
          'Core, model binding and OpenAPI integrations. Install with `dotnet add package AdCodicem.ValueObjects`. ' +
          scope,
        '',
        ...grouped.flatMap((section) => [
          `## ${section.label}`,
          '',
          ...section.pages.map((page) => `- [${page.title}](${page.url})${page.summary ? `: ${page.summary}` : ''}`),
          '',
        ]),
        '## Optional',
        '',
        `- [Full documentation](${site}llms-full.txt): every page above, as one Markdown file`,
        `- [API reference](${site}docs/api): generated from the XML doc comments`,
        ...(line
          ? [`- [Preview documentation](${site}docs/preview/introduction): describes main, ahead of the latest release`]
          : []),
        '- [Agent skill](https://github.com/AdCodicem/AdCodicem.ValueObjects/tree/main/skills/value-objects): ' +
          'the authoring surface, the wiring of each integration and every diagnostic, written for coding agents',
        '',
      ].join('\n');

      const full = pages.map((page) => `<!-- ${page.url} -->\n\n${page.text}\n`).join('\n');

      fs.writeFileSync(path.join(outDir, 'llms.txt'), index);
      fs.writeFileSync(path.join(outDir, 'llms-full.txt'), `${scope}\n\n${full}`);
    },
  };
}

type SidebarEntry = string | {type?: string; id?: string; label?: string; items?: SidebarEntry[]};

// The hand-written pages in sidebar order: those at the top level under "Docs", then one section per category.
// Autogenerated items are left out, which is what drops the API reference: it is generated, runs to hundreds of
// pages of member signatures, and is linked as a whole instead.
function sidebarSections(items: SidebarEntry[]): {label: string; ids: string[]}[] {
  const collect = (entries: SidebarEntry[], into: string[]): string[] => {
    for (const entry of entries) {
      if (typeof entry === 'string') {
        into.push(entry);
      } else if (entry.type === 'doc' && entry.id) {
        into.push(entry.id);
      } else if (entry.type === 'category' && entry.items) {
        collect(entry.items, into);
      }
    }
    return into;
  };

  const top = {label: 'Docs', ids: [] as string[]};
  const categories: {label: string; ids: string[]}[] = [];
  for (const entry of items) {
    if (typeof entry === 'object' && entry.type === 'category' && entry.items) {
      categories.push({label: entry.label ?? 'Docs', ids: collect(entry.items, [])});
    } else {
      collect([entry], top.ids);
    }
  }
  return [top, ...categories].filter((section) => section.ids.length > 0);
}

// For a page without a `description`: the first sentence of its first paragraph of prose, skipping one that only
// introduces a list or a code block.
function lede(markdown: string): string | undefined {
  let fenced = false;
  for (const block of markdown.split(/\n\s*\n/)) {
    const trimmed = block.trim();
    if ((trimmed.match(/```/g)?.length ?? 0) % 2 === 1) {
      fenced = !fenced;
      continue;
    }
    if (fenced || trimmed === '' || trimmed.endsWith(':') || /^(#|```|\||[-*>] |<|\d+\. )/.test(trimmed)) {
      continue;
    }
    const text = trimmed
      .replace(/\[([^\]]+)\]\([^)]*\)/g, '$1')
      .replace(/\*\*/g, '')
      .replace(/\s+/g, ' ');
    return /^.{40,}?[.!?](?=\s|$)/.exec(text)?.[0] ?? text;
  }
  return undefined;
}

const config: Config = {
  title: 'AdCodicem.ValueObjects',
  tagline: 'An answer to primitive obsession in .NET: single-value DDD value objects, with no reflection and no allocation on the paths that matter.',
  favicon: 'img/favicon.svg',

  future: {
    v4: true, // Improve compatibility with the upcoming Docusaurus v4
  },

  url: 'https://adcodicem.github.io',
  baseUrl: '/AdCodicem.ValueObjects/',

  organizationName: 'AdCodicem',
  projectName: 'AdCodicem.ValueObjects',

  onBrokenLinks: 'throw',
  markdown: {
    // The generated API reference under docs/api/ is full of bare generic
    // signatures -- ValueObjectContract<TValueObject, TValue> and the like --
    // which MDX parses as JSX and rejects. 'detect' keeps MDX for .mdx and
    // treats .md as CommonMark, where those are just text. None of the
    // hand-written pages use MDX features, so nothing is given up.
    format: 'detect',
    hooks: {
      onBrokenMarkdownLinks: 'throw',
    },
  },

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.ts',
          routeBasePath: 'docs',
          // A stable version's page edits its own snapshot under versioned_docs/, which is how a
          // misleading error in released documentation gets corrected. The API reference is
          // generated by DocFX, so there is nothing to edit: the fix belongs in the XML doc comment.
          editUrl: ({versionDocsDirPath, docPath}) =>
            docPath.startsWith('api/') ? undefined : `${editRoot}/${versionDocsDirPath}/${docPath}`,
          versions: {
            // Until a stable release exists the preview is the only version, so it is served at
            // /docs/ and the announcement bar below says so; the unreleased banner would only link
            // the reader to the page they are already on.
            current: {
              label: previewLabel,
              path: hasStable ? 'preview' : '',
              banner: hasStable ? 'unreleased' : 'none',
              badge: true,
              // Keeps search engines on the stable pages, which say the same thing about the
              // version people actually install.
              noIndex: hasStable,
            },
            ...Object.fromEntries(
              stableLines.map((line) => [
                line,
                {label: releasedVersions[line] ? `${line} (${releasedVersions[line]})` : line, badge: true},
              ]),
            ),
          },
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  plugins: [homepageExample, llmsTxt],

  themeConfig: {
    colorMode: {
      respectPrefersColorScheme: true,
    },
    announcementBar: hasStable
      ? undefined
      : {
          id: 'no-stable-release',
          content:
            'No stable release yet: this documentation describes the latest preview' +
            (previewVersion ? `, <code>${previewVersion}</code>` : '') +
            '. Install it with <code>dotnet add package AdCodicem.ValueObjects --prerelease</code>.',
          isCloseable: false,
        },
    navbar: {
      title: 'AdCodicem.ValueObjects',
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'docsSidebar',
          position: 'left',
          label: 'Docs',
        },
        {to: '/contributing', label: 'Contributing', position: 'left'},
        // Both appear with the first stable release: before it, the preview is the only version
        // and /docs/ already serves it.
        ...(hasStable
          ? [
              {
                to: '/docs/preview/introduction',
                label: 'Preview',
                position: 'right' as const,
                activeBaseRegex: '/docs/preview/',
              },
              {type: 'docsVersionDropdown', versions: stableLines, position: 'right' as const},
            ]
          : []),
        {
          href: 'https://www.nuget.org/packages/AdCodicem.ValueObjects',
          label: 'NuGet',
          position: 'right',
        },
        {
          href: 'https://github.com/AdCodicem/AdCodicem.ValueObjects',
          label: 'GitHub',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Docs',
          items: [
            {label: 'Introduction', to: '/docs/introduction'},
            {label: 'Getting Started', to: '/docs/getting-started'},
            {label: 'Packages', to: '/docs/packages'},
          ],
        },
        {
          title: 'More',
          items: [
            {label: 'Contributing', to: '/contributing'},
            {label: 'GitHub', href: 'https://github.com/AdCodicem/AdCodicem.ValueObjects'},
            {label: 'NuGet', href: 'https://www.nuget.org/packages/AdCodicem.ValueObjects'},
            {label: 'Issues', href: 'https://github.com/AdCodicem/AdCodicem.ValueObjects/issues'},
          ],
        },
      ],
      copyright: `Copyright © ${new Date().getFullYear()} AdCodicem. Released under the MIT licence.`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
      additionalLanguages: ['csharp', 'bash'],
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
