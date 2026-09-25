using Microsoft.Extensions.Configuration;
using MyWorkHub.Core.Features.Automations;

namespace MyWorkHub.Infrastructure.Features.Automations.Sites;

/// <summary>
/// Names of the selectors a site script reads from <c>ExternalSites:&lt;Site&gt;:Selectors</c>.
/// <para>
/// None of the sites' real page structure is baked into the code: the scripts only know the <em>shape</em>
/// of each flow (open the site, sign in, then download a file or tick days and save), and every element
/// they touch is a Playwright selector the user configures after inspecting the real pages. A selector that
/// is not configured fails the step with a message naming the exact configuration key to set.
/// </para>
/// </summary>
internal static class SiteSelectorKeys
{
    /// <summary>The login form's user-name input.</summary>
    public const string USER_NAME_FIELD = "UserNameField";

    /// <summary>The login form's password input.</summary>
    public const string PASSWORD_FIELD = "PasswordField";

    /// <summary>The button that submits the login form.</summary>
    public const string LOGIN_BUTTON = "LoginButton";

    /// <summary>(TCL) The link or button that downloads the current transport-pass attestation.</summary>
    public const string ATTESTATION_DOWNLOAD = "AttestationDownload";

    /// <summary>
    /// (PeopleNet, mwork) Selector of the element to click to mark one day as remote work, with
    /// <see cref="DATE_PLACEHOLDER"/> standing for the day, e.g. <c>[data-date='{date}']</c>.
    /// </summary>
    public const string REMOTE_DAY_TEMPLATE = "RemoteDayTemplate";

    /// <summary>(PeopleNet, mwork) The button that saves the declared days.</summary>
    public const string SAVE_BUTTON = "SaveButton";

    /// <summary>Replaced in <see cref="REMOTE_DAY_TEMPLATE"/> by the ISO date (<c>yyyy-MM-dd</c>).</summary>
    public const string DATE_PLACEHOLDER = "{date}";
}

/// <summary>Where one external site lives and how to find the elements its script touches.</summary>
/// <param name="Site">Which site.</param>
/// <param name="DisplayName">Name used in messages ("PeopleNet").</param>
/// <param name="ConfigurationPath">The site's configuration section, e.g. <c>ExternalSites:PeopleNet</c>.</param>
/// <param name="BaseUrl">Page the script opens first (normally the login page); null when not configured.</param>
/// <param name="Selectors">Configured selectors by <see cref="SiteSelectorKeys"/> name.</param>
internal sealed record SiteOptions(
    AutomationSite Site,
    string DisplayName,
    string ConfigurationPath,
    Uri? BaseUrl,
    IReadOnlyDictionary<string, string> Selectors)
{
    /// <summary>The base URL, or an <see cref="AutomationException"/> naming the key to set.</summary>
    public Uri RequireBaseUrl()
        => BaseUrl ?? throw new AutomationException(
            $"{DisplayName}: no site address configured. Set {ConfigurationPath}:BaseUrl in appsettings.json.");

    /// <summary>The selector, or an <see cref="AutomationException"/> naming the key to set.</summary>
    public string RequireSelector(string key)
        => Selectors.TryGetValue(key, out var selector)
            ? selector
            : throw new AutomationException(
                $"{DisplayName}: the page element \"{key}\" is not configured. " +
                $"Set {ConfigurationPath}:Selectors:{key} in appsettings.json to a Playwright selector.");
}

/// <summary>
/// The external sites the automations drive (configuration section <c>ExternalSites</c>). Read key by key
/// rather than through the reflection-based configuration binder.
/// </summary>
internal sealed record ExternalSitesOptions(SiteOptions Tcl, SiteOptions PeopleNet, SiteOptions MWork)
{
    public const string SECTION = "ExternalSites";

    public static ExternalSitesOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new ExternalSitesOptions(
            Read(configuration, AutomationSite.TCL, "TCL", "TCL"),
            Read(configuration, AutomationSite.PEOPLENET, "PeopleNet", "PeopleNet"),
            Read(configuration, AutomationSite.MWORK, "MWork", "mwork"));
    }

    private static SiteOptions Read(IConfiguration configuration, AutomationSite site, string key, string displayName)
    {
        var path = $"{SECTION}:{key}";
        var section = configuration.GetSection(path);

        var rawUrl = section["BaseUrl"]?.Trim() ?? "";
        var baseUrl = Uri.TryCreate(rawUrl, UriKind.Absolute, out var parsed)
                      && (parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp)
            ? parsed
            : null;

        var selectors = section.GetSection("Selectors").GetChildren()
            .Where(c => !string.IsNullOrWhiteSpace(c.Value))
            .ToDictionary(c => c.Key, c => c.Value!.Trim(), StringComparer.OrdinalIgnoreCase);

        return new SiteOptions(site, displayName, path, baseUrl, selectors);
    }
}
