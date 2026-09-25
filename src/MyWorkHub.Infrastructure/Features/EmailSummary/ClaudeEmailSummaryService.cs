using System.Globalization;
using MyWorkHub.Core.Features.CliAgent;
using MyWorkHub.Core.Features.Email;
using MyWorkHub.Core.Features.EmailSummary;
using MyWorkHub.Core.Features.GraphAuth;

namespace MyWorkHub.Infrastructure.Features.EmailSummary;

/// <summary>
/// <see cref="IEmailSummaryService"/> over the shared <see cref="ICliAgentRunner"/>. The digest is pure text
/// summarization of untrusted content, so the run is fully locked down: isolated empty temp directory, every
/// dangerous tool denied, ambient MCP servers ignored, and the emails nonce-quarantined
/// (<see cref="EmailSummaryPrompt"/>).
/// </summary>
internal sealed class ClaudeEmailSummaryService : IEmailSummaryService
{
    /// <summary>
    /// Denied tools: execution (Bash), mutation (Edit/Write/NotebookEdit), network egress (WebFetch/WebSearch)
    /// and delegation to sub-agents (Task, named Agent in newer CLI versions — both are denied).
    /// </summary>
    internal static readonly IReadOnlyList<string> DisallowedTools =
        ["Bash", "Edit", "Write", "NotebookEdit", "WebFetch", "WebSearch", "Task", "Agent"];

    /// <summary>Upper bound on messages per digest (the list is newest first, so the oldest are dropped).</summary>
    internal const int MAX_EMAILS = 50;

    /// <summary>Per-message body cap, keeping one long thread from crowding out the rest of the digest.</summary>
    internal const int MAX_BODY_LENGTH = 4000;

    internal const string NO_EMAILS_MESSAGE = "There are no emails to summarize.";

    private readonly ICliAgentRunner _runner;
    private readonly IEmailService _email;

    public ClaudeEmailSummaryService(ICliAgentRunner runner, IEmailService email)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(email);
        _runner = runner;
        _email = email;
    }

    public async Task<string> SummarizeAsync(
        IReadOnlyList<EmailItem> emails, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(emails);
        if (emails.Count == 0)
        {
            return NO_EMAILS_MESSAGE;
        }

        var selected = emails.Take(MAX_EMAILS).ToList();
        var entries = new List<EmailDigestEntry>(selected.Count);
        for (var i = 0; i < selected.Count; i++)
        {
            progress?.Report(string.Create(CultureInfo.InvariantCulture, $"Reading email {i + 1} of {selected.Count}…"));
            entries.Add(new EmailDigestEntry(selected[i], await ReadBodyAsync(selected[i], ct).ConfigureAwait(false)));
        }

        var prompt = EmailSummaryPrompt.Build(entries);
        progress?.Report("Summarizing with Claude…");

        var request = new CliAgentRequest(
            prompt.SystemPrompt,
            prompt.UserMessage,
            WorkingDirectory: null,
            ModelId: null,
            DisallowedTools: DisallowedTools,
            StrictMcpConfig: true);
        return await _runner.RunAsync(request, progress, ct).ConfigureAwait(false);
    }

    // A body that cannot be fetched (message moved/deleted since the list loaded) degrades to its preview;
    // being signed out is not a per-message problem and fails the whole digest.
    private async Task<string> ReadBodyAsync(EmailItem email, CancellationToken ct)
    {
        string body;
        try
        {
            body = await _email.GetEmailBodyAsync(email.Id, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or GraphNotConnectedException))
        {
            body = email.Preview;
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            body = email.Preview;
        }

        return body.Length <= MAX_BODY_LENGTH ? body : body[..MAX_BODY_LENGTH] + " […]";
    }
}
