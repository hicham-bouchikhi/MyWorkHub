using Microsoft.Extensions.Configuration;

namespace MyWorkHub.Infrastructure.Features.Automations.Transport;

/// <summary>
/// Where and how the attestation is sent (configuration section <c>Automation:TransportReimbursement</c>;
/// the schedule keys of the same section are read by the Automation project). Templates may contain
/// <c>{{subscription_start}}</c> and <c>{{subscription_end}}</c>.
/// </summary>
/// <param name="FacturationEmail">Billing address the attestation is sent to; empty when not configured.</param>
/// <param name="SubjectTemplate">Email subject template.</param>
/// <param name="BodyTemplate">Email body template (plain text).</param>
internal sealed record TransportReimbursementOptions(string FacturationEmail, string SubjectTemplate, string BodyTemplate)
{
    public const string SECTION = "Automation:TransportReimbursement";

    private const string DEFAULT_SUBJECT = "Transport pass attestation - {{subscription_start}} to {{subscription_end}}";
    private const string DEFAULT_BODY = "Hello,\n\nPlease find attached my transport pass attestation for {{subscription_start}} to {{subscription_end}}.\n";

    public static TransportReimbursementOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SECTION);
        return new TransportReimbursementOptions(
            section[nameof(FacturationEmail)]?.Trim() ?? "",
            NonBlankOr(section["EmailSubjectTemplate"], DEFAULT_SUBJECT),
            NonBlankOr(section["EmailBodyTemplate"], DEFAULT_BODY));
    }

    private static string NonBlankOr(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;
}
