using System.Text;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Infrastructure.AzureDevOps;
using MyWorkHub.Infrastructure.Tests.TestDoubles;

namespace MyWorkHub.Infrastructure.Tests.AzureDevOps;

public sealed class AzureDevOpsHelpersTests
{
    [Fact]
    public void Basic_auth_header_encodes_empty_user_and_pat()
    {
        var header = AzureDevOpsAuth.BuildBasicAuthHeaderValue("my-token");

        Assert.StartsWith("Basic ", header, StringComparison.Ordinal);
        var decoded = Encoding.ASCII.GetString(Convert.FromBase64String(header["Basic ".Length..]));
        Assert.Equal(":my-token", decoded);
    }

    [Fact]
    public void GetPatOrThrow_returns_the_stored_pat()
    {
        var store = new FakeCredentialStore();
        store.Save(CredentialKeys.AZURE_DEVOPS_PAT, "stored-pat");

        Assert.Equal("stored-pat", AzureDevOpsAuth.GetPatOrThrow(store));
    }

    [Fact]
    public void GetPatOrThrow_throws_a_clear_error_when_the_pat_is_missing()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => AzureDevOpsAuth.GetPatOrThrow(new FakeCredentialStore()));

        Assert.Contains("PAT", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(10, "Approved")]
    [InlineData(5, "Approved with suggestions")]
    [InlineData(0, "No vote")]
    [InlineData(-5, "Waiting for author")]
    [InlineData(-10, "Rejected")]
    public void Vote_codes_map_to_display_text(int vote, string expected)
    {
        Assert.Equal(expected, PullRequestVote.Describe(vote));
    }

    [Fact]
    public void Wiql_filters_to_me_in_the_current_iteration()
    {
        var wiql = WorkItemWiql.CurrentSprintAssignedToMe;

        Assert.Contains("[System.AssignedTo] = @Me", wiql, StringComparison.Ordinal);
        Assert.Contains("[System.IterationPath] = @CurrentIteration", wiql, StringComparison.Ordinal);
    }

    [Fact]
    public void Pull_request_url_is_built_under_the_project_and_repo()
    {
        var url = AzureDevOpsUrlBuilder.BuildPullRequestUrl("https://dev.azure.com/cegid/", "MyProject", "MyRepo", 42);

        Assert.Equal("https://dev.azure.com/cegid/MyProject/_git/MyRepo/pullrequest/42", url);
    }

    [Fact]
    public void Work_item_url_points_at_the_edit_page()
    {
        var url = AzureDevOpsUrlBuilder.BuildWorkItemUrl("https://dev.azure.com/cegid", "MyProject", 123);

        Assert.Equal("https://dev.azure.com/cegid/MyProject/_workitems/edit/123", url);
    }
}
