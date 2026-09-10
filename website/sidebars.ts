import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

// This runs in Node.js - Don't use client-side code here (browser APIs, JSX...)

const sidebars: SidebarsConfig = {
  docsSidebar: [
    'introduction',
    'getting-started',
    'packages',
    'authoring-guide',
    'design-decisions',
    'entity-identifiers',
    'testing',
    'benchmarks',
    'contributing',
  ],
};

export default sidebars;
