import type {ReactNode} from 'react';
import Link from '@docusaurus/Link';
import {PageMetadata} from '@docusaurus/theme-common';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';
import ValueObjectsMark from '@site/src/components/ValueObjectsMark';
import styles from './styles.module.css';

// Ejected (a safe swizzle, meant to be customized): the design system's 404, from its DocsKit.
export default function NotFound(): ReactNode {
  return (
    <>
      <PageMetadata title="Page not found" />
      <Layout>
        <main className={styles.page}>
          <div className={styles.content}>
            <ValueObjectsMark size={64} mono />
            <p className={styles.label}>404 · Page not found</p>
            <Heading as="h1" className={styles.title}>
              This page is not in the book.
            </Heading>
            <p className={styles.text}>
              It may have moved between versions: each release line keeps its own copy of the documentation, and a
              page added or renamed since exists in some of them only.
            </p>
            <div className={styles.buttons}>
              <Link className="button button--primary" to="/docs/introduction">
                Go to the introduction
              </Link>
              <Link
                className="button button--secondary"
                to="https://github.com/AdCodicem/AdCodicem.ValueObjects/issues/new">
                Report a broken link
              </Link>
            </div>
          </div>
        </main>
      </Layout>
    </>
  );
}
