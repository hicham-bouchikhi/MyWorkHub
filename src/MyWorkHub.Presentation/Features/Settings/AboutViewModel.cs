using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using MyWorkHub.Core;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Presentation.ViewModels;

namespace MyWorkHub.Presentation.Features.Settings;

/// <summary>
/// Settings → About: app name, version and where its data lives. The version is the repo-root <c>VERSION</c>
/// file, stamped into every assembly's <see cref="AssemblyInformationalVersionAttribute"/> by
/// <c>Directory.Build.props</c> (with the commit appended by the SDK as <c>+sha</c>).
/// </summary>
public sealed partial class AboutViewModel : ViewModelBase
{
    private const string APP_NAME = "MyWorkHub";
    private const string REPOSITORY_URL = "https://github.com/hicham-bouchikhi/MyWorkHub";

    private const int SHORT_COMMIT_LENGTH = 7;

    private readonly IBrowserLauncher? _browser;

    public AboutViewModel(IBrowserLauncher? browser = null)
    {
        _browser = browser;
        (Version, Commit) = ReadVersion(typeof(AboutViewModel).Assembly);
    }

    public static string AppName => APP_NAME;

    public static string RepositoryUrl => REPOSITORY_URL;

    /// <summary>The release version, e.g. <c>0.1.3</c>.</summary>
    public string Version { get; }

    /// <summary>The short commit the build was made from, or null when the build did not record one.</summary>
    public string? Commit { get; }

    public static string ConfigurationFilePath => AppPaths.UserAppSettingsPath;

    public static string DataFolderPath => AppPaths.RootDir;

    public bool CanOpenRepository => _browser is not null;

    [RelayCommand(CanExecute = nameof(CanOpenRepository))]
    private void OpenRepository() => _browser?.Open(REPOSITORY_URL);

    /// <summary>Splits an informational version <c>0.1.3+0123abcd…</c> into the version and a short commit.</summary>
    internal static (string Version, string? Commit) ReadVersion(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational))
        {
            return (assembly.GetName().Version?.ToString() ?? "unknown", null);
        }

        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        if (plus < 0)
        {
            return (informational, null);
        }

        var commit = informational[(plus + 1)..];
        return (informational[..plus], commit.Length > SHORT_COMMIT_LENGTH ? commit[..SHORT_COMMIT_LENGTH] : commit);
    }
}
