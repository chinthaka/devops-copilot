import {
  formatTimestamp,
  formatUptime,
  type ServiceHealth,
  type StatusDocument,
} from './status';
import { useStatus } from './useStatus';

/** The services the document always enumerates, with the label the dashboard shows for each. */
const SERVICE_LABELS: Record<string, string> = {
  worker: 'Worker',
  eventhubs: 'Event Hubs',
  storage: 'Storage',
};

function StatusDashboard() {
  const { status, error, loading } = useStatus();

  if (loading && status === null) {
    return (
      <section className="dashboard" aria-labelledby="dashboard-heading">
        <h2 id="dashboard-heading" className="visually-hidden">
          Worker status
        </h2>
        <div className="card" role="status" aria-live="polite">
          Reading the status…
        </div>
      </section>
    );
  }

  return (
    <section className="dashboard" aria-labelledby="dashboard-heading">
      <h2 id="dashboard-heading" className="visually-hidden">
        Worker status
      </h2>

      {error && (
        <div className="error-message" role="alert">
          {error}
        </div>
      )}

      {status && <StatusView status={status} />}
    </section>
  );
}

function StatusView({ status }: { status: StatusDocument }) {
  const { worker, services, generatedAt } = status;
  const running = worker.status === 'running';
  const statistics = worker.statistics;

  return (
    <>
      <div className="card worker-card">
        <div className="section-header">
          <h3 className="section-title">Worker</h3>
          <span
            className={`state-pill ${running ? 'state-running' : 'state-not-running'}`}
            data-testid="worker-status"
          >
            {running ? 'Running' : 'Not running'}
          </span>
        </div>

        <dl className="stat-grid">
          <Stat
            id="uptime"
            label="Uptime"
            value={formatUptime(worker.uptimeSeconds)}
          />
          {statistics ? (
            <>
              <Stat id="eventsRead" label="Events read" value={statistics.eventsRead} />
              <Stat id="eventsSkipped" label="Events skipped" value={statistics.eventsSkipped} />
              <Stat
                id="batchesCheckpointed"
                label="Batches checkpointed"
                value={statistics.batchesCheckpointed}
              />
              <Stat id="errors" label="Errors" value={statistics.errors} />
              <Stat
                id="lastProcessedAt"
                label="Last processed event"
                value={formatTimestamp(statistics.lastProcessedAt)}
              />
            </>
          ) : (
            <div className="stat stat-wide">
              <dt className="stat-label">Statistics</dt>
              <dd className="stat-value stat-note" data-testid="telemetry-disabled">
                Host telemetry is disabled
              </dd>
            </div>
          )}
        </dl>
      </div>

      <div className="card services-card">
        <div className="section-header">
          <h3 className="section-title">Services</h3>
        </div>
        <ul className="service-list">
          {services.map((service) => (
            <ServiceRow key={service.name} service={service} />
          ))}
        </ul>
      </div>

      <p className="generated-at">
        Updated <time dateTime={generatedAt}>{formatTimestamp(generatedAt)}</time>
      </p>
    </>
  );
}

function Stat({ id, label, value }: { id: string; label: string; value: string | number }) {
  return (
    <div className="stat">
      <dt className="stat-label">{label}</dt>
      <dd className="stat-value" data-testid={`stat-${id}`}>
        {value}
      </dd>
    </div>
  );
}

function ServiceRow({ service }: { service: ServiceHealth }) {
  const label = SERVICE_LABELS[service.name] ?? service.name;

  return (
    <li className="service-row">
      <span className="service-name">{label}</span>
      <span
        className={`health-pill health-${service.status}`}
        data-testid={`service-${service.name}`}
      >
        {service.status}
      </span>
    </li>
  );
}

export default StatusDashboard;
