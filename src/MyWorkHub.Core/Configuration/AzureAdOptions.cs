namespace MyWorkHub.Core.Configuration;

public sealed class AzureAdOptions
{
    public const string SECTION = "AzureAD";

    public string ClientId { get; set; } = "";
    public string TenantId { get; set; } = "common";
}
