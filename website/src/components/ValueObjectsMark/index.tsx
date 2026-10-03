import type {ReactNode} from 'react';

// The ValueObjects mark from the design system: a hexagonal shell seen as a cube around a rust disc, the primitive
// it encloses. The full drawing from 48px, the compact one (thicker shell, no facets) below. The shell follows the
// colour scheme in fg-strong; `mono` draws the disc in it too.
export default function ValueObjectsMark({size = 64, mono = false}: {size?: number; mono?: boolean}): ReactNode {
  const ink = 'var(--fg-strong)';
  const compact = size < 48;
  return (
    <svg viewBox="0 0 100 100" width={size} height={size} role="img" aria-label="AdCodicem.ValueObjects">
      <polygon
        points="50,10 84.64,30 84.64,70 50,90 15.36,70 15.36,30"
        fill="none"
        stroke={ink}
        strokeWidth={compact ? 6.4 : 4.6}
        strokeLinejoin="round"
      />
      {!compact && (
        <>
          <line x1="50" y1="28" x2="50" y2="10" stroke={ink} strokeWidth="1.9" strokeLinecap="round" />
          <line x1="69.05" y1="61" x2="84.64" y2="70" stroke={ink} strokeWidth="1.9" strokeLinecap="round" />
          <line x1="30.95" y1="61" x2="15.36" y2="70" stroke={ink} strokeWidth="1.9" strokeLinecap="round" />
        </>
      )}
      <circle cx="50" cy="50" r={compact ? 15 : 14.5} fill={mono ? ink : 'var(--accent)'} />
    </svg>
  );
}
