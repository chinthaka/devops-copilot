using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DevOpsCopilot.Daemon.Incident;

public sealed class DiagnosticAgent
{
    public const string AgentName = "DiagnosticAgent";

    private readonly KustoQueryTool _tool;

    public DiagnosticAgent(KustoQueryTool tool, IChatClient? chatClient = null)
    {
        _tool = tool;
        KustoTool = AIFunctionFactory.Create(
            (LogEvent logEvent, CancellationToken cancellationToken) => DiagnoseAsync(logEvent, cancellationToken),
            "query_telemetry",
            "Runs the three read-only Kusto questions for one log event. Does not accept KQL.");
        if (chatClient is not null)
        {
            ChatAgent = new ChatClientAgent(
                chatClient,
                "Call query_telemetry. Summarize only the text it returns. Do not write KQL or add rows.",
                AgentName,
                "Diagnoses one error event with Kusto.",
                new List<AITool> { KustoTool });
        }
    }

    public AIFunction KustoTool { get; }

    public AIAgent? ChatAgent { get; }

    public Task<DiagnosticHandoff> DiagnoseAsync(LogEvent logEvent, CancellationToken cancellationToken) =>
        _tool.DiagnoseAsync(logEvent, cancellationToken);
}
