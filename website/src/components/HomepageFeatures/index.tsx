import type {ReactNode} from 'react';
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

// Numbered columns separated by hairline rules, as the design system lays out a row of principles.
function Feature({title, description, index}: FeatureItem & {index: number}) {
  return (
    <div className={styles.feature}>
      <span className={styles.number}>{String(index + 1).padStart(2, '0')}</span>
      <Heading as="h3" className={styles.title}>
        {title}
      </Heading>
      <p className={styles.description}>{description}</p>
    </div>
  );
}

export default function HomepageFeatures(): ReactNode {
  return (
    <section className={styles.features}>
      <div className={styles.grid}>
        {FeatureList.map((props, idx) => (
          <Feature key={idx} index={idx} {...props} />
        ))}
      </div>
    </section>
  );
}
