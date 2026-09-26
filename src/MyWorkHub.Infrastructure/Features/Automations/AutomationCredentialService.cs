using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.Automations;

namespace MyWorkHub.Infrastructure.Features.Automations;

/// <summary>
/// <see cref="IAutomationCredentialService"/> over the encrypted <see cref="ICredentialStore"/>, storing
/// each site's login under its <see cref="CredentialKeys"/> user/password pair.
/// </summary>
internal sealed class AutomationCredentialService : IAutomationCredentialService
{
    private readonly ICredentialStore _store;

    public AutomationCredentialService(ICredentialStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public SiteCredentials? Get(AutomationSite site)
    {
        var (userKey, passwordKey) = KeysFor(site);
        var userName = _store.Get(userKey);
        var password = _store.Get(passwordKey);
        return string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password)
            ? null
            : new SiteCredentials(userName, password);
    }

    public void Save(AutomationSite site, SiteCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentException.ThrowIfNullOrWhiteSpace(credentials.UserName, nameof(credentials));
        ArgumentException.ThrowIfNullOrWhiteSpace(credentials.Password, nameof(credentials));

        var (userKey, passwordKey) = KeysFor(site);
        _store.Save(userKey, credentials.UserName.Trim());
        _store.Save(passwordKey, credentials.Password);
    }

    public void Clear(AutomationSite site)
    {
        var (userKey, passwordKey) = KeysFor(site);
        _store.Delete(userKey);
        _store.Delete(passwordKey);
    }

    private static (string UserKey, string PasswordKey) KeysFor(AutomationSite site)
        => site switch
        {
            AutomationSite.TCL => (CredentialKeys.TCL_USER, CredentialKeys.TCL_PASSWORD),
            AutomationSite.PEOPLENET => (CredentialKeys.PEOPLENET_USER, CredentialKeys.PEOPLENET_PASSWORD),
            AutomationSite.MWORK => (CredentialKeys.MWORK_USER, CredentialKeys.MWORK_PASSWORD),
            _ => throw new ArgumentOutOfRangeException(nameof(site), site, "Unknown automation site."),
        };
}
