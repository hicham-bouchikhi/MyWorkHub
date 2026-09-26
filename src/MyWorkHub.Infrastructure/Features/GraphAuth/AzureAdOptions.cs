using Microsoft.Extensions.Configuration;

namespace MyWorkHub.Infrastructure.Features.GraphAuth;

/// <summary>
/// The Azure AD app registration used for Microsoft 365 sign-in (configuration section <c>AzureAD</c>).
/// Read key by key rather than through the reflection-based configuration binder.
/// </summary>
/// <param name="ClientId">Application (client) id of the public-client registration; empty when unconfigured.</param>
/// <param name="TenantId">Directory (tenant) id, or <c>organizations</c> for any work/school tenant.</param>
internal sealed record AzureAdOptions(string ClientId, string TenantId)
{
    public const string SECTION = "AzureAD";

    private const string DEFAULT_TENANT = "organizations";

    public static AzureAdOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SECTION);
        var tenantId = section[nameof(TenantId)];
        return new AzureAdOptions(
            section[nameof(ClientId)]?.Trim() ?? "",
            string.IsNullOrWhiteSpace(tenantId) ? DEFAULT_TENANT : tenantId.Trim());
    }
}
