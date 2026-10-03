import type {ReactNode} from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';
import MDXContent from '@theme/MDXContent';
import HomepageFeatures from '@site/src/components/HomepageFeatures';
import Icon from '@site/src/components/Icon';
import InstallCommand from '@site/src/components/InstallCommand';
// docs/_homepage-example.md as frozen by the latest stable release; see docusaurus.config.ts.
import HomepageExample from '@homepage-example';

import styles from './index.module.css';

function HomepageHeader() {
  const {siteConfig} = useDocusaurusContext();
  const install = `dotnet add package AdCodicem.ValueObjects${siteConfig.customFields?.hasStable ? '' : ' --prerelease'}`;
  return (
    <header className={styles.hero}>
      <div className={styles.heroInner}>
        <div className={styles.heroText}>
          <p className={styles.eyebrow}>.NET 10 and later · MIT · NuGet</p>
          <Heading as="h1" className={styles.heroTitle}>
            <span className={styles.prefix}>AdCodicem.</span>
            <br />
            ValueObjects
          </Heading>
          <p className={styles.heroLead}>{siteConfig.tagline}</p>
          <p className={styles.heroLede}>
            An IBAN carried as a bare <code>string</code> is validated wherever someone remembered to. Declare
            the type and its rules once instead; the framework carries them into JSON, the database, model
            binding and the OpenAPI document, so they cannot drift apart.
          </p>
          <InstallCommand command={install} />
          <div className={styles.buttons}>
            <Link className={clsx('button button--primary button--lg', styles.button)} to="/docs/introduction">
              Get started
              <Icon name="arrow-right" className={styles.arrow} />
            </Link>
            <Link
              className={clsx('button button--secondary button--lg', styles.button)}
              to="https://github.com/AdCodicem/AdCodicem.ValueObjects">
              <Icon name="github" />
              View on GitHub
            </Link>
          </div>
        </div>
        <div className={styles.heroCode}>
          <MDXContent>
            <HomepageExample />
          </MDXContent>
        </div>
      </div>
    </header>
  );
}

export default function Home(): ReactNode {
  const {siteConfig} = useDocusaurusContext();
  return (
    <Layout title={siteConfig.title} description={siteConfig.tagline}>
      <HomepageHeader />
      <main>
        <HomepageFeatures />
      </main>
    </Layout>
  );
}
