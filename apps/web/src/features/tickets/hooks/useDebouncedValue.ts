import { useEffect, useState } from 'react';

/**
 * Returns `value` after it has stopped changing for `delayMs`. The first render returns the
 * value as is, so a screen's initial load is not delayed.
 */
export function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const handle = window.setTimeout(() => setDebounced(value), delayMs);

    return () => window.clearTimeout(handle);
  }, [value, delayMs]);

  return debounced;
}
