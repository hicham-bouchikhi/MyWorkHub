namespace MyWorkHub.Core.Tests;

public class AppPathsTests
{
    [Fact]
    public void RootDir_lives_under_the_user_profile_dot_folder()
    {
        Assert.EndsWith(".MyWorkHub", AppPaths.RootDir, StringComparison.Ordinal);
    }

    [Fact]
    public void DbPath_sits_inside_the_root_folder()
    {
        Assert.StartsWith(AppPaths.RootDir, AppPaths.DbPath, StringComparison.Ordinal);
        Assert.EndsWith("MyWorkHub.db", AppPaths.DbPath, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_keep_the_msal_token_cache_directly_inside_the_root_folder()
    {
        Assert.Equal(AppPaths.RootDir, Path.GetDirectoryName(AppPaths.MsalTokenCachePath));
        Assert.Equal("msal_token_cache.bin", Path.GetFileName(AppPaths.MsalTokenCachePath));
    }

    [Fact]
    public void Should_seed_the_review_agent_and_clone_review_repositories_inside_the_root_folder()
    {
        Assert.Equal(Path.Combine(AppPaths.RootDir, "review-agent.md"), AppPaths.ReviewAgentPath);
        Assert.Equal(Path.Combine(AppPaths.RootDir, "repos"), AppPaths.ReviewRepositoriesDir);
    }
}
