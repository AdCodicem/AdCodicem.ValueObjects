import type {ReactNode} from 'react';
import ColorModeToggle from '@theme-original/ColorModeToggle';
import type ColorModeToggleType from '@theme/ColorModeToggle';
import type {WrapperProps} from '@docusaurus/types';
import ContrastToggle from '@site/src/components/ContrastToggle';

type Props = WrapperProps<typeof ColorModeToggleType>;

// A wrapper (a safe swizzle): the contrast toggle sits beside the light and dark one, in the navbar and in the mobile
// sidebar alike, since it retunes whichever of the two is active. The class name goes on the pair, not on the
// original toggle alone: it is what hides the toggle from the top bar on a narrow screen, where the mobile sidebar
// shows it instead.
export default function ColorModeToggleWrapper({className, ...props}: Props): ReactNode {
  return (
    <div className={className}>
      <div style={{display: 'flex', alignItems: 'center', gap: '0.25rem'}}>
        <ContrastToggle />
        <ColorModeToggle {...props} />
      </div>
    </div>
  );
}
