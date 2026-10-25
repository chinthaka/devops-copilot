import { useEffect, useState } from 'react';
import { POLL_INTERVAL_MS, fetchStatus, type StatusDocument } from './status';

export interface StatusPoll {
  status: StatusDocument | null;
  error: string | null;
  loading: boolean;
}

/**
 * Reads the status document on mount and then every <paramref name="intervalMs" />. A failed read
 * leaves the last good document on screen and reports the error alongside it, so a brief server
 * hiccup does not blank the dashboard.
 */
export function useStatus(intervalMs: number = POLL_INTERVAL_MS): StatusPoll {
  const [status, setStatus] = useState<StatusDocument | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    const read = async () => {
      try {
        const document = await fetchStatus();
        if (!cancelled) {
          setStatus(document);
          setError(null);
        }
      } catch (failure) {
        if (!cancelled) {
          setError(failure instanceof Error ? failure.message : 'The status API could not be read.');
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    void read();
    const timer = setInterval(() => void read(), intervalMs);

    return () => {
      cancelled = true;
      clearInterval(timer);
    };
  }, [intervalMs]);

  return { status, error, loading };
}
