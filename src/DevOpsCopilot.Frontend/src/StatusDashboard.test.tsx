import { act } from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import StatusDashboard from './StatusDashboard';
import { POLL_INTERVAL_MS, type StatusDocument } from './status';

const notRunning: StatusDocument = {
  worker: {
    status: 'not-running',
    telemetryEnabled: true,
    uptimeSeconds: 0,
    statistics: {
      eventsRead: 0,
      eventsSkipped: 0,
      batchesCheckpointed: 0,
      errors: 0,
      lastProcessedAt: null,
    },
  },
  services: [
    { name: 'worker', status: 'unhealthy' },
    { name: 'eventhubs', status: 'unknown' },
    { name: 'storage', status: 'unknown' },
  ],
  generatedAt: '2026-10-25T12:00:00Z',
};

const running: StatusDocument = {
  worker: {
    status: 'running',
    telemetryEnabled: true,
    uptimeSeconds: 125,
    statistics: {
      eventsRead: 12,
      eventsSkipped: 4,
      batchesCheckpointed: 9,
      errors: 2,
      lastProcessedAt: '2026-10-25T11:59:00Z',
    },
  },
  services: [
    { name: 'worker', status: 'healthy' },
    { name: 'eventhubs', status: 'healthy' },
    { name: 'storage', status: 'degraded' },
  ],
  generatedAt: '2026-10-25T12:00:05Z',
};

/** Answers each poll with the next document, then repeats the last one. */
function answerWith(...documents: StatusDocument[]) {
  const last = documents[documents.length - 1];
  const fetchMock = vi.fn(() => Promise.resolve(response(last)));
  for (const document of documents) {
    fetchMock.mockResolvedValueOnce(response(document));
  }
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function response(document: StatusDocument) {
  return { ok: true, status: 200, json: () => Promise.resolve(document) } as Response;
}

describe('StatusDashboard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it('renders not running and zeros', async () => {
    // Arrange
    answerWith(notRunning);

    // Act
    render(<StatusDashboard />);

    // Assert
    await waitFor(() => expect(screen.getByTestId('worker-status')).toHaveTextContent(/not running/i));
    expect(screen.getByTestId('stat-eventsRead')).toHaveTextContent('0');
    expect(screen.getByTestId('stat-eventsSkipped')).toHaveTextContent('0');
    expect(screen.getByTestId('stat-batchesCheckpointed')).toHaveTextContent('0');
    expect(screen.getByTestId('stat-errors')).toHaveTextContent('0');
    expect(screen.getByTestId('stat-lastProcessedAt')).toHaveTextContent('Never');
    expect(screen.getByTestId('stat-uptime')).toHaveTextContent('0s');
  });

  it('renders live statistics', async () => {
    // Arrange
    answerWith(running);

    // Act
    render(<StatusDashboard />);

    // Assert
    await waitFor(() => expect(screen.getByTestId('worker-status')).toHaveTextContent(/^running$/i));
    expect(screen.getByTestId('stat-eventsRead')).toHaveTextContent('12');
    expect(screen.getByTestId('stat-eventsSkipped')).toHaveTextContent('4');
    expect(screen.getByTestId('stat-batchesCheckpointed')).toHaveTextContent('9');
    expect(screen.getByTestId('stat-errors')).toHaveTextContent('2');
    expect(screen.getByTestId('stat-lastProcessedAt')).not.toHaveTextContent('Never');
    expect(screen.getByTestId('stat-uptime')).toHaveTextContent('2m 5s');
    expect(screen.getByTestId('service-worker')).toHaveTextContent(/healthy/i);
    expect(screen.getByTestId('service-eventhubs')).toHaveTextContent(/healthy/i);
    expect(screen.getByTestId('service-storage')).toHaveTextContent(/degraded/i);
  });

  it('polls the status endpoint', async () => {
    // Arrange: fake timers, so the clock is driven explicitly rather than waited on.
    vi.useFakeTimers();
    const fetchMock = answerWith(notRunning, running);
    render(<StatusDashboard />);
    await act(async () => {});
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(screen.getByTestId('worker-status')).toHaveTextContent(/not running/i);

    // Act
    await act(async () => {
      await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS);
    });

    // Assert
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(screen.getByTestId('worker-status')).toHaveTextContent(/^running$/i);
    expect(screen.getByTestId('stat-eventsRead')).toHaveTextContent('12');
  });
});
