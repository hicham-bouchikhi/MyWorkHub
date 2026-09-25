using System.Net;
using System.Text;
using MyWorkHub.Infrastructure.Features.AzureDevOps;
using static MyWorkHub.Infrastructure.Tests.TestDoubles.StubHttpMessageHandler;

namespace MyWorkHub.Infrastructure.Tests.Features.AzureDevOps;

public sealed class AzureDevOpsConnectionServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_verify_the_new_token_then_store_it()
    {
        using var fixture = new AzureDevOpsFixture(storedToken: null);

        var result = await fixture.Connection.ConnectAsync("  fresh-pat  ", Ct);

        Assert.True(result.Succeeded);
        Assert.Equal(AzureDevOpsFixture.ME_NAME, result.DisplayName);
        Assert.Equal("fresh-pat", fixture.Credentials.Get(AzureDevOpsClient.PAT_CREDENTIAL_KEY));
        var check = Assert.Single(fixture.Handler.Requests);
        Assert.Equal("/cegid/_apis/connectionData", check.Uri.AbsolutePath);
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(":fresh-pat")), check.Authorization);
    }

    [Fact]
    public async Task Should_keep_the_previous_token_and_explain_why_when_the_new_one_is_rejected()
    {
        using var fixture = new AzureDevOpsFixture();
        fixture.ConnectionDataResponse = _ => Status(HttpStatusCode.Unauthorized);

        var result = await fixture.Connection.ConnectAsync("wrong-pat", Ct);

        Assert.False(result.Succeeded);
        Assert.Contains("rejected", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(AzureDevOpsFixture.PAT, fixture.Credentials.Get(AzureDevOpsClient.PAT_CREDENTIAL_KEY));
    }

    [Fact]
    public async Task Should_report_a_network_failure_instead_of_throwing()
    {
        using var fixture = new AzureDevOpsFixture(storedToken: null);
        fixture.ConnectionDataResponse = _ => throw new HttpRequestException("no route to host");

        var result = await fixture.Connection.ConnectAsync("fresh-pat", Ct);

        Assert.False(result.Succeeded);
        Assert.Contains("no route to host", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(fixture.Credentials.Values);
    }

    [Fact]
    public async Task Should_report_a_missing_organization_url()
    {
        using var fixture = new AzureDevOpsFixture(organizationUrl: null, storedToken: null);

        var result = await fixture.Connection.ConnectAsync("fresh-pat", Ct);

        Assert.False(result.Succeeded);
        Assert.Contains("OrganizationUrl", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_refuse_a_blank_token_without_asking_azure_devops(string token)
    {
        using var fixture = new AzureDevOpsFixture(storedToken: null);

        var result = await fixture.Connection.ConnectAsync(token, Ct);

        Assert.False(result.Succeeded);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public void Should_forget_the_token_on_disconnect()
    {
        using var fixture = new AzureDevOpsFixture();

        fixture.Connection.Disconnect();

        Assert.Null(fixture.Credentials.Get(AzureDevOpsClient.PAT_CREDENTIAL_KEY));
    }

    [Fact]
    public void Should_store_the_token_under_the_key_the_previous_app_used()
    {
        // A token saved by the pre-rewrite app (CredentialKeys.AZURE_DEVOPS_PAT) keeps working.
        Assert.Equal("AZDO_PAT", AzureDevOpsClient.PAT_CREDENTIAL_KEY);
    }
}
