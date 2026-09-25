namespace MyWorkHub.Core.Features.CliAgent;

/// <summary>
/// Runs one headless prompt through the user's locally installed AI coding-agent CLI (the <c>claude</c>
/// binary) and returns its final text output. The single place where the CLI security rules live, shared by
/// every AI feature (email digest, pull request review):
/// <list type="bullet">
/// <item>the trusted <see cref="CliAgentRequest.SystemPrompt"/> is always passed as a command-line argument and
/// the (possibly attacker-influenced) <see cref="CliAgentRequest.UserMessage"/> over standard input, so content
/// can never replace the instructions;</item>
/// <item>without a <see cref="CliAgentRequest.WorkingDirectory"/> the CLI runs in a fresh, empty temporary
/// directory, so no project instructions or files leak into the context;</item>
/// <item>the user's existing CLI login is reused — no bare/API-key mode.</item>
/// </list>
/// </summary>
public interface ICliAgentRunner
{
    /// <summary>Runs <paramref name="request"/> and returns the agent's final output.</summary>
    /// <param name="request">Prompt, working directory and tool restrictions of the run.</param>
    /// <param name="progress">Receives the agent's standard output line by line as it arrives.</param>
    /// <param name="ct">Cancels the run and terminates the CLI process tree.</param>
    /// <exception cref="CliAgentException">The CLI could not be started or reported a failure.</exception>
    Task<string> RunAsync(CliAgentRequest request, IProgress<string>? progress = null, CancellationToken ct = default);
}
