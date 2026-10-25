using DevOpsCopilot.Application;
using DevOpsCopilot.Domain;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DevOpsCopilot.Infrastructure.Agents;

/// <summary>
/// The Microsoft Agent Framework surface over the evidence stage. The tool is fixed and builds its
/// own KQL, so a model can summarise the result but can neither write a query nor add a row.
/// Without a chat client the agent is just the tool, which is how the worker runs today.
/// </summary>
public sealed class DiagnosticAgent
{
    public const string AgentName = "DiagnosticAgent";

    private readonly GatherEvidence _evidence;

    public DiagnosticAgent(GatherEvidence evidence, IChatClient? chatClient = null)
    {
        _evidence = evidence;
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
        _evidence.DiagnoseAsync(logEvent, cancellationToken);
}
