import type {ReactNode} from 'react';
import clsx from 'clsx';
import {useHistory, useLocation} from '@docusaurus/router';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import useIsBrowser from '@docusaurus/useIsBrowser';
import {useVersions} from '@docusaurus/plugin-content-docs/client';
import {apiContext} from './context';

export type SearchScope = 'guides' | 'api';

// Which index a search reaches from the current page.
//
// The guides and the API reference have an index each in the version the site serves at /docs/: the latest stable
// line, or the preview until the first one. The plugin resolves a context against the root of the site when it
// builds and against the version's own path in the browser, so in the preview and the older lines, served below
// /docs/<version>/, a context would never be found again; those versions keep one index for both.
export function useSearchScope(): {split: boolean; scope: SearchScope; onSearchPage: boolean} {
  const {
    siteConfig: {baseUrl},
  } = useDocusaurusContext();
  const {pathname, search} = useLocation();
  const versions = useVersions(undefined);
  // The query string is unknown to the server render, so it is read once the page has hydrated, as the plugin does:
  // reading it before would render another picker than the server sent.
  const isBrowser = useIsBrowser();
  const params = new URLSearchParams(isBrowser ? search : '');
  const onSearchPage = pathname.replace(/\/$/, '') === `${baseUrl}search`;

  // The results page names its version in `version`, which the plugin sets for any version but the latest.
  const inVersion = (path: string) => pathname === path || pathname.startsWith(`${path}/`);
  const split = onSearchPage
    ? !params.get('version')
    : !versions.some((version) => !version.isLast && inVersion(version.path));

  const api = `${baseUrl}${apiContext}`;
  const scope: SearchScope = onSearchPage
    ? params.get('ctx') === apiContext
      ? 'api'
      : 'guides'
    : pathname === api || pathname.startsWith(`${api}/`)
      ? 'api'
      : 'guides';

  return {split, scope, onSearchPage};
}

export function searchPlaceholder(split: boolean, scope: SearchScope): string {
  if (!split) {
    return 'Search the docs';
  }
  return scope === 'api' ? 'Search the API reference' : 'Search the guides';
}

const labels: Record<SearchScope, string> = {guides: 'Guides', api: 'API'};

// Picks the index to search. The search bar always searches the index of the page it is on, so choosing the other
// one opens the results page in that index, with what was typed so far; on the results page it switches in place.
export default function SearchScopeToggle({
  scope,
  onSearchPage,
  query,
  className,
}: {
  scope: SearchScope;
  onSearchPage: boolean;
  query: () => string;
  className?: string;
}): ReactNode {
  const {
    siteConfig: {baseUrl},
  } = useDocusaurusContext();
  const history = useHistory();
  const location = useLocation();

  const choose = (target: SearchScope) => {
    if (target === scope) {
      return;
    }
    const params = new URLSearchParams(onSearchPage ? location.search : '');
    if (!onSearchPage && query()) {
      params.set('q', query());
    }
    if (target === 'api') {
      params.set('ctx', apiContext);
    } else {
      params.delete('ctx');
    }
    if (onSearchPage) {
      history.replace({search: params.toString()});
    } else {
      history.push(`${baseUrl}search?${params.toString()}`);
    }
  };

  return (
    <div className={clsx('search-scope', className)} role="group" aria-label="Search in">
      {(['guides', 'api'] as const).map((target) => (
        <button
          key={target}
          type="button"
          className={clsx('search-scope__option', target === scope && 'search-scope__option--active')}
          aria-pressed={target === scope}
          title={target === 'api' ? 'Search the API reference' : 'Search the guides'}
          onClick={() => choose(target)}>
          {labels[target]}
        </button>
      ))}
    </div>
  );
}
