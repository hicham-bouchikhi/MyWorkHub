namespace MyWorkHub.Core.Features.Automations;

/// <summary>An external website an automation signs in to with the user's own login.</summary>
public enum AutomationSite
{
    /// <summary>The public-transport operator's customer account (transport-pass attestation).</summary>
    TCL,

    /// <summary>The HR portal where remote-work days are declared.</summary>
    PEOPLENET,

    /// <summary>The presence-planning tool where remote-work days are declared.</summary>
    MWORK,
}

/// <summary>A site login. Held only transiently; stored encrypted by the credential store.</summary>
/// <param name="UserName">Login name.</param>
/// <param name="Password">Password.</param>
public sealed record SiteCredentials(string UserName, string Password)
{
    // Keeps the password out of logs, exception messages and debugger displays.
    public override string ToString() => $"{nameof(SiteCredentials)} {{ {nameof(UserName)} = {UserName} }}";
}

/// <summary>The site logins used by the automations, encrypted at rest (never in <c>appsettings.json</c>).</summary>
public interface IAutomationCredentialService
{
    /// <summary>The stored login, or null when none (or only half of one) is stored.</summary>
    SiteCredentials? Get(AutomationSite site);

    /// <summary>Replaces the stored login. Both values must be non-blank.</summary>
    void Save(AutomationSite site, SiteCredentials credentials);

    /// <summary>Forgets the stored login.</summary>
    void Clear(AutomationSite site);
}
