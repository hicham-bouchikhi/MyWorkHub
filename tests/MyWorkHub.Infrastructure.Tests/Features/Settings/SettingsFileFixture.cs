using Microsoft.Extensions.Configuration;
using MyWorkHub.Infrastructure.Features.Settings;

namespace MyWorkHub.Infrastructure.Tests.Features.Settings;

/// <summary>
/// A real <c>appsettings.json</c> in a fresh temp directory, loaded the way the app loads it (JSON file
/// provider), with the real <see cref="SettingsService"/> and <see cref="JsonSettingsFile"/> on top — no
/// mocked file I/O.
/// </summary>
internal sealed class SettingsFileFixture : IDisposable
{
    /// <summary>A trimmed-down copy of the shipped defaults, with sections the Settings page never edits.</summary>
    public const string SHIPPED_LIKE_JSON = """
        {
          "AzureAD": {
            "ClientId": "9ff123f8-9be5-437b-b85d-b3e62bf011c5",
            "TenantId": "604e5547-f49c-4481-8476-c14f22fd79cb"
          },
          "AzureDevOps": {
            "OrganizationUrl": "https://dev.azure.com/cegid/",
            "Projects": []
          },
          "Automation": {
            "TransportReimbursement": {
              "Enabled": true,
              "CronExpression": "0 0 9 1 * ?",
              "EmailSubjectTemplate": "Remboursement titre de transport - {{subscription_start}} au {{subscription_end}} (été)"
            }
          },
          "UI": {
            "RefreshIntervalMinutes": 5,
            "MinimizeToTrayOnClose": true,
            "Theme": "System",
            "Palette": "GitHub"
          },
          "Workspace": {
            "WorkFolderPath": "",
            "ClaudeExecutablePath": "",
            "ReviewModelId": "claude-sonnet-5",
            "ReviewAgentPath": ""
          },
          "Email": {
            "FolderIds": [ "inbox" ],
            "MaxPerFolder": 25
          }
        }
        """;

    private readonly ConfigurationRoot _configuration;

    public SettingsFileFixture(string? initialJson = SHIPPED_LIKE_JSON)
    {
        Directory = System.IO.Directory.CreateTempSubdirectory("settings-test-");
        FilePath = Path.Combine(Directory.FullName, "appsettings.json");
        if (initialJson is not null)
        {
            File.WriteAllText(FilePath, initialJson);
        }

        _configuration = (ConfigurationRoot)new ConfigurationBuilder()
            .AddJsonFile(FilePath, optional: true, reloadOnChange: false)
            .Build();
        SettingsFile = new JsonSettingsFile(FilePath);
        Service = new SettingsService(_configuration, SettingsFile);
    }

    public DirectoryInfo Directory { get; }

    public string FilePath { get; }

    /// <summary>The live configuration, as registered in the app's container.</summary>
    public IConfigurationRoot Configuration => _configuration;

    public JsonSettingsFile SettingsFile { get; }

    public SettingsService Service { get; }

    public void Dispose()
    {
        SettingsFile.Dispose();
        _configuration.Dispose();
        Directory.Delete(recursive: true);
    }
}
