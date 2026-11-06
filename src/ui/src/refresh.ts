import { useEffect, useState } from 'preact/hooks';

const RefreshInterval = 30_000;

/**
 * A counter that moves every 30 seconds while the window is visible, and whenever the window is shown again or gets
 * the focus back; screens ask the host again when it moves.
 */
export function useRefreshTick(): number {
  const [tick, setTick] = useState(0);

  useEffect(() => {
    const refresh = (): void => setTick((value) => value + 1);
    const refreshIfVisible = (): void => {
      if (document.visibilityState === 'visible') {
        refresh();
      }
    };

    const timer = window.setInterval(refreshIfVisible, RefreshInterval);
    document.addEventListener('visibilitychange', refreshIfVisible);
    window.addEventListener('focus', refresh);
    return () => {
      window.clearInterval(timer);
      document.removeEventListener('visibilitychange', refreshIfVisible);
      window.removeEventListener('focus', refresh);
    };
  }, []);

  return tick;
}
