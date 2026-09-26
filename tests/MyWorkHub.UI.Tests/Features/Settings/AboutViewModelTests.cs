using System.Reflection;
using MyWorkHub.Presentation.Features.Settings;
using MyWorkHub.UI.Tests.Features.AzureDevOps;

namespace MyWorkHub.UI.Tests.Features.Settings;

public sealed class AboutViewModelTests
{
    private static string RepositoryVersionFile()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "VERSION");
            if (File.Exists(candidate) && File.Exists(Path.Combine(directory.FullName, "MyWorkHub.slnx")))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("The repository VERSION file was not found above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void Should_show_the_version_from_the_repository_version_file()
    {
        var expected = File.ReadAllText(RepositoryVersionFile()).Trim();

        Assert.Equal(expected, new AboutViewModel().Version);
    }

    [Fact]
    public void Should_split_the_commit_off_the_informational_version()
    {
        var (version, commit) = AboutViewModel.ReadVersion(typeof(AboutViewModel).Assembly);
        var informational = typeof(AboutViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        Assert.StartsWith(version, informational, StringComparison.Ordinal);
        Assert.DoesNotContain('+', version);
        Assert.True(commit is null || commit.Length <= 7);
    }

    [Fact]
    public void Should_open_the_repository_in_the_browser()
    {
        var browser = new FakeBrowserLauncher();

        new AboutViewModel(browser).OpenRepositoryCommand.Execute(null);

        Assert.Equal([AboutViewModel.RepositoryUrl], browser.Opened);
    }
}
