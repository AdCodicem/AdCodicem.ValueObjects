import {type ReactNode, useEffect, useState} from 'react';
import clsx from 'clsx';
import styles from './styles.module.css';

// Also read by the script the `contrast` plugin puts in <head> (docusaurus.config.ts), which applies the choice
// before the first paint. 'accessible' or 'brand'; absent until the reader chooses, which defers to prefers-contrast.
const storageKey = 'adcodicem-contrast';

function isAccessible(): boolean {
  return document.documentElement.getAttribute('data-contrast') === 'accessible';
}

// Switches between the design system's brand colour schemes and its accessible ones, which keep the same grounds and
// accent but reach WCAG 2 AA on every pair drawn. Independent of light and dark: it applies to whichever is active.
export default function ContrastToggle({className}: {className?: string}): ReactNode {
  // Rendered unpressed on the server; corrected once the attribute the <head> script set can be read.
  const [accessible, setAccessible] = useState(false);
  useEffect(() => setAccessible(isAccessible()), []);

  const toggle = () => {
    const next = !isAccessible();
    if (next) {
      document.documentElement.setAttribute('data-contrast', 'accessible');
    } else {
      document.documentElement.removeAttribute('data-contrast');
    }
    try {
      localStorage.setItem(storageKey, next ? 'accessible' : 'brand');
    } catch {
      // Storage unavailable (private window, blocked site data): the choice holds for this page only.
    }
    setAccessible(next);
  };

  return (
    <div className={clsx(styles.toggle, className)}>
      <button
        type="button"
        className={clsx('clean-btn', styles.button, accessible && styles.active)}
        onClick={toggle}
        aria-pressed={accessible}
        aria-label="Accessible contrast (WCAG 2 AA)"
        title={accessible ? 'Accessible contrast: on' : 'Accessible contrast: off'}>
        <svg viewBox="0 0 24 24" width="20" height="20" aria-hidden="true">
          <circle cx="12" cy="12" r="9" fill="none" stroke="currentColor" strokeWidth="2" />
          <path d="M12 3a9 9 0 0 1 0 18z" fill={accessible ? 'currentColor' : 'none'} stroke="currentColor" strokeWidth="2" />
        </svg>
      </button>
    </div>
  );
}
