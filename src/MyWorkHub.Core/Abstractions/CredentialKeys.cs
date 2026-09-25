namespace MyWorkHub.Core.Abstractions;

/// <summary>
/// Every key used with <see cref="ICredentialStore"/>, in one place so no feature slice spells a key
/// string itself. The values are persisted (encrypted) in the user's store and are the ones the
/// pre-rewrite app used — changing one silently loses the saved secret.
/// </summary>
public static class CredentialKeys
{
    /// <summary>Azure DevOps personal access token (REST calls and git clone/fetch for PR review).</summary>
    public const string AZURE_DEVOPS_PAT = "AZDO_PAT";

    /// <summary>Transport operator login (transport-reimbursement automation).</summary>
    public const string TCL_USER = "TCL_USER";

    /// <inheritdoc cref="TCL_USER" />
    public const string TCL_PASSWORD = "TCL_PASSWORD";

    /// <summary>PeopleNet HR portal login (remote-work sync automation).</summary>
    public const string PEOPLENET_USER = "PEOPLENET_USER";

    /// <inheritdoc cref="PEOPLENET_USER" />
    public const string PEOPLENET_PASSWORD = "PEOPLENET_PASSWORD";

    /// <summary>mwork presence-planning login (remote-work sync automation).</summary>
    public const string MWORK_USER = "MWORK_USER";

    /// <inheritdoc cref="MWORK_USER" />
    public const string MWORK_PASSWORD = "MWORK_PASSWORD";
}
