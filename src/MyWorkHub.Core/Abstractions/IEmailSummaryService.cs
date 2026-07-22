using MyWorkHub.Core.Models;

namespace MyWorkHub.Core.Abstractions;

/// <summary>
/// Produces a short natural-language digest of a set of emails using an AI model.
/// The implementation reuses the Claude CLI's own logged-in session for auth — no API key
/// is handled here.
/// </summary>
public interface IEmailSummaryService
{
    /// <summary>
    /// Returns a short digest of <paramref name="emails"/>. Throws
    /// <see cref="InvalidOperationException"/> when the AI tool is unavailable (e.g. the
    /// Claude CLI is not installed or not logged in).
    /// </summary>
    Task<string> SummarizeAsync(IReadOnlyList<EmailItem> emails, CancellationToken ct = default);
}
