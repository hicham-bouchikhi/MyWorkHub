using Microsoft.Extensions.Configuration;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using MyWorkHub.Core.Features.Automations;
using MyWorkHub.Infrastructure.Features.Automations;
using MyWorkHub.Infrastructure.Features.Automations.Sites;
using MyWorkHub.Infrastructure.Tests.TestDoubles;

namespace MyWorkHub.Infrastructure.Tests.Features.Automations;

/// <summary>Shared wiring for the site-automation tests: configured sites, stored logins, a fake browser.</summary>
internal sealed class SiteAutomationFixture
{
    public static readonly DateTimeOffset Now = new(2026, 9, 25, 8, 30, 0, TimeSpan.Zero);

    public const string DOWNLOADS = "/data/downloads";
    public const string SCREENSHOTS = "/data/errors";

    public FakeBrowserService Browser { get; } = new();

    public InMemoryCredentialStore Store { get; } = new();

    public AutomationCredentialService Credentials { get; }

    public Dictionary<string, string?> Settings { get; } = new()
    {
        ["ExternalSites:PeopleNet:BaseUrl"] = "https://peoplenet.example.test/login",
        ["ExternalSites:PeopleNet:Selectors:UserNameField"] = "#user",
        ["ExternalSites:PeopleNet:Selectors:PasswordField"] = "#pass",
        ["ExternalSites:PeopleNet:Selectors:LoginButton"] = "#login",
        ["ExternalSites:PeopleNet:Selectors:RemoteDayTemplate"] = "[data-date='{date}']",
        ["ExternalSites:PeopleNet:Selectors:SaveButton"] = "#save",
        ["ExternalSites:TCL:BaseUrl"] = "https://tcl.example.test/",
        ["ExternalSites:TCL:Selectors:UserNameField"] = "#email",
        ["ExternalSites:TCL:Selectors:PasswordField"] = "#password",
        ["ExternalSites:TCL:Selectors:LoginButton"] = "button[type=submit]",
        ["ExternalSites:TCL:Selectors:AttestationDownload"] = "text=Attestation",
    };

    public SiteAutomationFixture()
    {
        Credentials = new AutomationCredentialService(Store);
        Credentials.Save(AutomationSite.PEOPLENET, new SiteCredentials("jdoe", "pn-secret"));
        Credentials.Save(AutomationSite.TCL, new SiteCredentials("jdoe@example.test", "tcl-secret"));
    }

    public IConfiguration Configuration => new ConfigurationBuilder().AddInMemoryCollection(Settings).Build();

    public ExternalSitesOptions Sites => ExternalSitesOptions.FromConfiguration(Configuration);

    /// <summary>Frozen at <see cref="Now"/> with UTC as the local zone, so local-time output is machine-independent.</summary>
    public static TimeProvider Clock { get; } = new UtcClock();

    public SiteScriptRunner Runner()
        => new(Browser, Credentials, new AutomationPaths(DOWNLOADS, SCREENSHOTS), Clock);

    /// <summary>A Graph adapter answering calendar reads with <paramref name="events"/> and echoing writes.</summary>
    public static FakeGraphRequestAdapter Graph(params Event[] events)
        => new(request => request.HttpMethod == Method.GET ? new EventCollectionResponse { Value = [.. events] } : new Event());

    public static string Body(RequestInformation request)
    {
        request.Content.Position = 0;
        using var reader = new StreamReader(request.Content, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private sealed class UtcClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
