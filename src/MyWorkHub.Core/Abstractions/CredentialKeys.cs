namespace MyWorkHub.Core.Abstractions;

/// <summary>
/// Well-known keys used with <see cref="ICredentialStore"/>. Centralised so the
/// Settings UI and the consuming services never drift on the string value.
/// </summary>
public static class CredentialKeys
{
    public const string AZURE_DEVOPS_PAT = "AZDO_PAT";

    public const string TCL_USER = "TCL_USER";
    public const string TCL_PASSWORD = "TCL_PASSWORD";
    public const string PEOPLENET_USER = "PEOPLENET_USER";
    public const string PEOPLENET_PASSWORD = "PEOPLENET_PASSWORD";
    public const string MWORK_USER = "MWORK_USER";
    public const string MWORK_PASSWORD = "MWORK_PASSWORD";
}
