using System.Collections.ObjectModel;

namespace MyWorkHub.Core.Configuration;

/// <summary>
/// Which Outlook mail folders feed the email digest, and how many messages to pull
/// from each. Folder ids are Graph folder identifiers; the well-known name "inbox"
/// is used as the safe first-launch default so the digest starts inbox-only.
/// </summary>
public sealed class EmailOptions
{
    public const string SECTION = "Email";

    /// <summary>Graph folder ids (or well-known names) to include in the digest.</summary>
    public Collection<string> FolderIds { get; } = ["inbox"];

    /// <summary>Maximum number of messages pulled from each watched folder.</summary>
    public int MaxPerFolder { get; set; } = 25;
}
