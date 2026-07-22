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
}
