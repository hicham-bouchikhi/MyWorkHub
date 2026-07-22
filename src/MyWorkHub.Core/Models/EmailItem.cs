namespace MyWorkHub.Core.Models;

/// <summary>A single important inbox message, projected for the email digest.</summary>
public record EmailItem(
    string Id,
    string From,
    string Subject,
    string Preview,
    DateTime ReceivedAt,
    bool IsFlagged,
    string FolderName = "")
{
    /// <summary>Full plain-text body, fetched lazily on summarise. Null until fetched.</summary>
    public string? FullBody { get; init; }
}
