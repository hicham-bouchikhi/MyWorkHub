using Microsoft.Extensions.Configuration;
using MyWorkHub.Core;
using MyWorkHub.Infrastructure.Features.Workspace;

namespace MyWorkHub.Infrastructure.Tests.Features.Workspace;

public sealed class WorkspaceOptionsTests
{
    private static WorkspaceOptions Read(Dictionary<string, string?> values)
        => WorkspaceOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

    [Fact]
    public void Should_default_blank_values_to_claude_on_path_the_app_repos_folder_and_the_cli_defaults()
    {
        var options = Read(new()
        {
            ["Workspace:ClaudeExecutablePath"] = "",
            ["Workspace:WorkFolderPath"] = "  ",
            ["Workspace:ReviewModelId"] = "",
        });

        Assert.Equal(new WorkspaceOptions("claude", AppPaths.ReviewRepositoriesDir, null, null), options);
    }

    [Fact]
    public void Should_read_and_trim_configured_values()
    {
        var options = Read(new()
        {
            ["Workspace:ClaudeExecutablePath"] = " /usr/local/bin/claude ",
            ["Workspace:WorkFolderPath"] = "/src/reviews",
            ["Workspace:ReviewModelId"] = "claude-sonnet-5",
            ["Workspace:ReviewAgentPath"] = "/home/me/agent.md",
        });

        Assert.Equal(new WorkspaceOptions("/usr/local/bin/claude", "/src/reviews", "claude-sonnet-5", "/home/me/agent.md"), options);
    }
}
