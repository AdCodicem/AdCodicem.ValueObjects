import {themes as prismThemes} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

// This runs in Node.js - Don't use client-side code here (browser APIs, JSX...)

const config: Config = {
  title: 'AdCodicem.ValueObjects',
  tagline: 'Single-value DDD value objects for .NET, with no reflection and no allocation on the paths that matter.',
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
          editUrl: 'https://github.com/AdCodicem/AdCodicem.ValueObjects/edit/main/website/',
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  themeConfig: {
    colorMode: {
      respectPrefersColorScheme: true,
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
