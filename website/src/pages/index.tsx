import type {ReactNode} from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';
import MDXContent from '@theme/MDXContent';
import HomepageFeatures from '@site/src/components/HomepageFeatures';
// docs/_homepage-example.md as frozen by the latest stable release; see docusaurus.config.ts.
import HomepageExample from '@homepage-example';

import styles from './index.module.css';

function HomepageHeader() {
  const {siteConfig} = useDocusaurusContext();
  return (
    <header className={clsx('hero hero--primary', styles.heroBanner)}>
      <div className="container">
        <div className="row">
          <div className="col col--5">
            <Heading as="h1" className={styles.heroTitle}>
              {siteConfig.title}
            </Heading>
            <p className="hero__subtitle">{siteConfig.tagline}</p>
            <p className={styles.heroLede}>
              Declare the type and its rules once; the framework carries them into JSON, the database, model
              binding and the OpenAPI document, so they cannot drift apart.
            </p>
            <div className={styles.buttons}>
              <Link className="button button--primary button--lg" to="/docs/introduction">
                Get Started
              </Link>
              <Link
                className="button button--secondary button--lg"
                to="https://github.com/AdCodicem/AdCodicem.ValueObjects">
                View on GitHub
              </Link>
            </div>
          </div>
          <div className={clsx('col col--7', styles.heroCode)}>
            <MDXContent>
              <HomepageExample />
            </MDXContent>
          </div>
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
