using MyWorkHub.Infrastructure.Ai;

namespace MyWorkHub.Infrastructure.Tests.Ai;

public sealed class ClaudeCliArgumentsTests
{
    [Fact]
    public void Summary_args_run_headless_print_with_plain_text_output()
    {
        var args = ClaudeCliArguments.ForSummary("SYS").ToList();

        Assert.Contains("-p", args);
        var formatIndex = args.IndexOf("--output-format");
        Assert.True(formatIndex >= 0 && args[formatIndex + 1] == "text");
    }

    [Fact]
    public void Summary_args_pass_the_trusted_instructions_as_a_system_prompt()
    {
        var args = ClaudeCliArguments.ForSummary("my-system-prompt").ToList();

        var idx = args.IndexOf("--system-prompt");
        Assert.True(idx >= 0);
        Assert.Equal("my-system-prompt", args[idx + 1]);
    }

    [Fact]
    public void Summary_args_deny_execution_mutation_network_and_subagent_tools()
    {
        var args = ClaudeCliArguments.ForSummary("SYS").ToList();

        var idx = args.IndexOf("--disallowedTools");
        Assert.True(idx >= 0);
        var denied = args[idx + 1];
        foreach (var tool in new[] { "Bash", "Edit", "Write", "WebFetch", "WebSearch", "Task" })
        {
            Assert.Contains(tool, denied, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Summary_args_ignore_ambient_mcp_servers()
    {
        var args = ClaudeCliArguments.ForSummary("SYS").ToList();

        Assert.Contains("--strict-mcp-config", args);
    }

    [Fact]
    public void Summary_args_never_bypass_permissions()
    {
        var args = ClaudeCliArguments.ForSummary("SYS").ToList();

        Assert.DoesNotContain("--dangerously-skip-permissions", args);
        Assert.DoesNotContain("--bare", args); // would disable OAuth and break this app's auth
    }
}
