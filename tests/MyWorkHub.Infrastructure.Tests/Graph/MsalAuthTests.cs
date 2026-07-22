using MyWorkHub.Core.Configuration;
using MyWorkHub.Infrastructure.Graph;

namespace MyWorkHub.Infrastructure.Tests.Graph;

public sealed class MsalAuthTests
{
    private const string FAKE_CLIENT_ID = "11111111-1111-1111-1111-111111111111";

    [Fact]
    public void Create_throws_a_clear_error_when_client_id_is_missing()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => MsalPublicClientFactory.Create(new AzureAdOptions { ClientId = "" }));

        Assert.Contains("ClientId", ex.Message, StringComparison.Ordinal);
        Assert.Contains("T010", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_builds_a_public_client_when_client_id_is_present()
    {
        var app = MsalPublicClientFactory.Create(
            new AzureAdOptions { ClientId = FAKE_CLIENT_ID, TenantId = "common" });

        Assert.Equal(FAKE_CLIENT_ID, app.AppConfig.ClientId);
    }

    [Fact]
    public void Token_provider_serves_tokens_only_to_the_graph_host()
    {
        var app = MsalPublicClientFactory.Create(new AzureAdOptions { ClientId = FAKE_CLIENT_ID });
        var provider = new MsalTokenProvider(app, GraphScopes.Delegated);

        Assert.True(provider.AllowedHostsValidator.IsUrlHostValid(new Uri("https://graph.microsoft.com/v1.0/me")));
        Assert.False(provider.AllowedHostsValidator.IsUrlHostValid(new Uri("https://evil.example.com/")));
    }
}
