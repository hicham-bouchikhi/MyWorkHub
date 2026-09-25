using MyWorkHub.Core.Features.CliAgent;

namespace MyWorkHub.Infrastructure.Tests.TestDoubles;

/// <summary>Records every <see cref="CliAgentRequest"/> and answers with <see cref="Output"/>.</summary>
internal sealed class FakeCliAgentRunner : ICliAgentRunner
{
    public List<CliAgentRequest> Requests { get; } = [];

    public string Output { get; set; } = "agent output";

    public Task<string> RunAsync(CliAgentRequest request, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        Requests.Add(request);
        return Task.FromResult(Output);
    }
}
