namespace DevOpsCopilot.Worker.Status;

public sealed record DependencyHealthResponse(string Name, string Status);
