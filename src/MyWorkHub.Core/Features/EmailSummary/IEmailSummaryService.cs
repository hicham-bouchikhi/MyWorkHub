using MyWorkHub.Core.Features.CliAgent;
using MyWorkHub.Core.Features.Email;
using MyWorkHub.Core.Features.GraphAuth;

namespace MyWorkHub.Core.Features.EmailSummary;

/// <summary>AI digest of the user's recent mail, produced by the local agent CLI (<see cref="ICliAgentRunner"/>).</summary>
public interface IEmailSummaryService
{
    /// <summary>
    /// Summarizes <paramref name="emails"/> (their full bodies are fetched on demand) into a short digest.
    /// Email content is treated as untrusted data: it is quarantined from the instructions and the agent
    /// runs with no tools.
    /// </summary>
    /// <param name="emails">Messages to summarize, newest first.</param>
    /// <param name="progress">Receives human-readable progress (body fetching, then the agent's output).</param>
    /// <param name="ct">Cancels the fetches and the agent run.</param>
    /// <exception cref="CliAgentException">The agent CLI is missing or failed.</exception>
    /// <exception cref="GraphNotConnectedException">The user is not signed in to Microsoft 365.</exception>
    Task<string> SummarizeAsync(
        IReadOnlyList<EmailItem> emails, IProgress<string>? progress = null, CancellationToken ct = default);
}
