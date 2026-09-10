import type {ReactNode} from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';
import CodeBlock from '@theme/CodeBlock';
import HomepageFeatures from '@site/src/components/HomepageFeatures';

import styles from './index.module.css';

const example = `[ValueObject<string>(
    MinLength = 15,
    MaxLength = 34,
    Pattern = "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$",
    SchemaFormat = "iban")]
public readonly partial struct Iban
{
    private static string NormalizeCore(string value) => /* strip separators, upper-case */;

    private static ValidationResult ValidateCore(in string value)
        => HasValidCheckDigits(value)
            ? ValidationResult.Success
            : ValidationResult.InvalidFormat("The IBAN check digits are incorrect.");
}

var iban = Iban.Create("fr76 3000 6000 0112 3456 7890 189");
iban.Value                              // "FR7630006000011234567890189"
JsonSerializer.Serialize(new { iban })  // {"iban":"FR7630006000011234567890189"}`;

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
          <div className="col col--7">
            <CodeBlock language="csharp" className={styles.heroCode}>
              {example}
            </CodeBlock>
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
