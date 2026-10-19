namespace DevOpsCopilot.Daemon.Incident;

public interface IPostmortemSearch
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken cancellationToken);
}
