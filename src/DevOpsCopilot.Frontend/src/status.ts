/**
 * The status document served by DevOpsCopilot.Server at /api/status, and the small helpers the
 * dashboard uses to render it. Shape defined in
 * docs/specs/2026-10-25-status-dashboard/requirement.md.
 */

export type WorkerState = 'running' | 'not-running';

export type ServiceState = 'healthy' | 'degraded' | 'unhealthy' | 'unknown';

export interface WorkerStatistics {
  eventsRead: number;
  eventsSkipped: number;
  batchesCheckpointed: number;
  errors: number;
  lastProcessedAt: string | null;
}

export interface WorkerStatus {
  status: WorkerState;
  telemetryEnabled: boolean;
  uptimeSeconds: number;
  statistics: WorkerStatistics | null;
}

export interface ServiceHealth {
  name: string;
  status: ServiceState;
}

export interface StatusDocument {
  worker: WorkerStatus;
  services: ServiceHealth[];
  generatedAt: string;
}

/** Where the dashboard reads the status document from. */
export const STATUS_ENDPOINT = '/api/status';

/**
 * How often the dashboard re-reads the status document. Deliberately a constant and not
 * configuration (decision 6 in the requirement).
 */
export const POLL_INTERVAL_MS = 5000;

/** Reads one status document. Throws when the server does not answer with one. */
export async function fetchStatus(signal?: AbortSignal): Promise<StatusDocument> {
  const response = await fetch(STATUS_ENDPOINT, { signal });
  if (!response.ok) {
    throw new Error(`The status API answered ${response.status}.`);
  }

  return (await response.json()) as StatusDocument;
}

/** Uptime as a short human-readable span, for example "2h 5m" or "41s". */
export function formatUptime(seconds: number): string {
  const whole = Math.max(0, Math.floor(seconds));
  const days = Math.floor(whole / 86400);
  const hours = Math.floor((whole % 86400) / 3600);
  const minutes = Math.floor((whole % 3600) / 60);

  if (days > 0) {
    return `${days}d ${hours}h`;
  }
  if (hours > 0) {
    return `${hours}h ${minutes}m`;
  }
  if (minutes > 0) {
    return `${minutes}m ${whole % 60}s`;
  }

  return `${whole}s`;
}

/** A timestamp as local time, or "Never" when the worker has processed nothing yet. */
export function formatTimestamp(timestamp: string | null): string {
  if (!timestamp) {
    return 'Never';
  }

  const parsed = new Date(timestamp);
  return Number.isNaN(parsed.getTime()) ? 'Never' : parsed.toLocaleString();
}
