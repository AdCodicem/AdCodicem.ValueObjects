// Webpack alias declared by the homepage-example plugin in docusaurus.config.ts: the homepage example
// partial of the latest stable documentation, or of the preview before the first stable release.
declare module '@homepage-example' {
  import type {ComponentType} from 'react';

  const HomepageExample: ComponentType;
  export default HomepageExample;
}
