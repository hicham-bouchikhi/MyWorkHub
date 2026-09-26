using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using MyWorkHub.Core.Features.GraphAuth;
using MyWorkHub.Infrastructure.Features.GraphAuth;
using MyWorkHub.Infrastructure.Tests.TestDoubles;

namespace MyWorkHub.Infrastructure.Tests.Features.GraphAuth;

public sealed class GraphAuthTests
{
    private static readonly Uri _graphUri = new("https://graph.microsoft.com/v1.0/me/messages");

    private static IConfiguration Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    // --- Access token provider (Kiota) -------------------------------------------------

    [Fact]
    public async Task Should_return_the_silently_acquired_token_for_a_graph_request()
    {
        var provider = new GraphAccessTokenProvider(new FakeMsalAuthenticator { SilentToken = "token-123" });

        Assert.Equal("token-123", await provider.GetAuthorizationTokenAsync(_graphUri, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_throw_not_connected_when_no_token_is_available_without_interaction()
    {
        var provider = new GraphAccessTokenProvider(new FakeMsalAuthenticator { SilentToken = null });

        await Assert.ThrowsAsync<GraphNotConnectedException>(() => provider.GetAuthorizationTokenAsync(_graphUri, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("https://evil.example.com/v1.0/me")]
    [InlineData("http://graph.microsoft.com/v1.0/me")]
    public async Task Should_never_hand_out_the_token_for_a_foreign_host_or_plain_http(string url)
    {
        var authenticator = new FakeMsalAuthenticator { SilentToken = "token-123" };
        var provider = new GraphAccessTokenProvider(authenticator);

        Assert.Equal("", await provider.GetAuthorizationTokenAsync(new Uri(url), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, authenticator.SilentCalls);
    }

    [Fact]
    public async Task Should_attach_the_bearer_token_through_the_kiota_authentication_provider()
    {
        var auth = new BaseBearerTokenAuthenticationProvider(
            new GraphAccessTokenProvider(new FakeMsalAuthenticator { SilentToken = "token-123" }));
        var request = new RequestInformation { URI = _graphUri };

        await auth.AuthenticateRequestAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["Bearer token-123"], request.Headers["Authorization"]);
    }

    // --- Connection service -------------------------------------------------------------

    [Fact]
    public async Task Should_report_the_account_name_when_interactive_sign_in_succeeds()
    {
        var service = new GraphConnectionService(new FakeMsalAuthenticator { InteractiveAccountName = "me@cegid.com" });

        var result = await service.SignInAsync(TestContext.Current.CancellationToken);

        Assert.Equal(GraphSignInResult.Success("me@cegid.com"), result);
        Assert.Equal("me@cegid.com", await service.GetSignedInAccountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_report_a_cancelled_sign_in_when_the_user_closes_the_browser()
    {
        var service = new GraphConnectionService(new FakeMsalAuthenticator
        {
            InteractiveFailure = new MsalClientException(MsalError.AuthenticationCanceledError, "closed"),
        });

        var result = await service.SignInAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("Sign-in was cancelled.", result.ErrorMessage);
    }

    [Fact]
    public async Task Should_report_the_msal_message_when_sign_in_fails()
    {
        var service = new GraphConnectionService(new FakeMsalAuthenticator
        {
            InteractiveFailure = new MsalServiceException("invalid_client", "AADSTS700016: app not found"),
        });

        var result = await service.SignInAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("AADSTS700016", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_report_a_missing_client_id_as_a_sign_in_failure_without_building_msal()
    {
        using var authenticator = new MsalAuthenticator(
            AzureAdOptions.FromConfiguration(Configuration()), NullLogger<MsalAuthenticator>.Instance);
        var service = new GraphConnectionService(authenticator);

        var result = await service.SignInAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("AzureAD:ClientId", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_forget_the_account_when_signing_out()
    {
        var authenticator = new FakeMsalAuthenticator { AccountName = "me@cegid.com" };
        var service = new GraphConnectionService(authenticator);

        await service.SignOutAsync(TestContext.Current.CancellationToken);

        Assert.True(authenticator.SignedOut);
        Assert.Null(await service.GetSignedInAccountAsync(TestContext.Current.CancellationToken));
    }

    // --- Options + composition --------------------------------------------------------------

    [Fact]
    public void Should_read_the_azure_ad_registration_from_configuration()
    {
        var options = AzureAdOptions.FromConfiguration(Configuration(
            ("AzureAD:ClientId", " client "), ("AzureAD:TenantId", "tenant")));

        Assert.Equal(new AzureAdOptions("client", "tenant"), options);
    }

    [Fact]
    public void Should_default_to_any_work_tenant_and_no_client_when_unconfigured()
    {
        Assert.Equal(new AzureAdOptions("", "organizations"), AzureAdOptions.FromConfiguration(Configuration()));
    }

    [Fact]
    public void Should_resolve_the_graph_client_and_connection_service_without_touching_msal()
    {
        // Composition validation resolves every page's services at startup: that must not do I/O or
        // fail just because Azure AD is not configured yet.
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        new GraphAuthInfrastructureModule().RegisterServices(services, Configuration());
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<GraphServiceClient>());
        Assert.IsType<GraphConnectionService>(provider.GetRequiredService<IGraphConnectionService>());
    }
}
