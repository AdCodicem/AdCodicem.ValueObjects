import type {ReactNode} from 'react';
import clsx from 'clsx';
import Heading from '@theme/Heading';
import styles from './styles.module.css';

type FeatureItem = {
  title: string;
  description: ReactNode;
};

const FeatureList: FeatureItem[] = [
  {
    title: 'Normalize, then validate, then assign',
    description: (
      <>
        A non-default instance is by construction normalized and valid — everywhere except the EF Core read
        path, which trusts values this same application already validated.
      </>
    ),
  },
  {
    title: 'Rules declared once',
    description: (
      <>
        <code>MaxLength = 34</code> validates the value, sizes the EF Core column, and becomes the OpenAPI{' '}
        <code>maxLength</code> keyword. Anything added to <code>[ValueObject&lt;T&gt;]</code> feeds all three.
      </>
    ),
  },
  {
    title: 'No reflection, no extra allocation',
    description: (
      <>
        Holding 100 000 struct wrappers allocates exactly what holding 100 000 bare values allocates, to the
        byte. The generated equality and hashing keep dictionary lookups and sorts allocation-free too.
      </>
    ),
  },
];

function Feature({title, description}: FeatureItem) {
  return (
    <div className={clsx('col col--4')}>
      <div className={styles.feature}>
        <Heading as="h3">{title}</Heading>
        <p>{description}</p>
      </div>
    </div>
  );
}

export default function HomepageFeatures(): ReactNode {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className="row">
          {FeatureList.map((props, idx) => (
            <Feature key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}
