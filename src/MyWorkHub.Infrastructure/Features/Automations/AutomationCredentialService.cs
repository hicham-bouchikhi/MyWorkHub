using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.Automations;

namespace MyWorkHub.Infrastructure.Features.Automations;

/// <summary>
/// <see cref="IAutomationCredentialService"/> over the encrypted <see cref="ICredentialStore"/>. Key names
/// (<c>TCL_USER</c>, <c>TCL_PASSWORD</c>, <c>PEOPLENET_USER</c>, …) are the ones the pre-rewrite app used, so
/// logins saved by it keep working.
/// </summary>
internal sealed class AutomationCredentialService : IAutomationCredentialService
{
    private const string USER_SUFFIX = "_USER";
    private const string PASSWORD_SUFFIX = "_PASSWORD";

    private readonly ICredentialStore _store;

    public AutomationCredentialService(ICredentialStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public SiteCredentials? Get(AutomationSite site)
    {
        var userName = _store.Get(KeyPrefix(site) + USER_SUFFIX);
        var password = _store.Get(KeyPrefix(site) + PASSWORD_SUFFIX);
        return string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password)
            ? null
            : new SiteCredentials(userName, password);
    }

    public void Save(AutomationSite site, SiteCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentException.ThrowIfNullOrWhiteSpace(credentials.UserName, nameof(credentials));
        ArgumentException.ThrowIfNullOrWhiteSpace(credentials.Password, nameof(credentials));

        _store.Save(KeyPrefix(site) + USER_SUFFIX, credentials.UserName.Trim());
        _store.Save(KeyPrefix(site) + PASSWORD_SUFFIX, credentials.Password);
    }

    public void Clear(AutomationSite site)
    {
        _store.Delete(KeyPrefix(site) + USER_SUFFIX);
        _store.Delete(KeyPrefix(site) + PASSWORD_SUFFIX);
    }

    private static string KeyPrefix(AutomationSite site)
        => site switch
        {
            AutomationSite.TCL => "TCL",
            AutomationSite.PEOPLENET => "PEOPLENET",
            AutomationSite.MWORK => "MWORK",
            _ => throw new ArgumentOutOfRangeException(nameof(site), site, "Unknown automation site."),
        };
}
