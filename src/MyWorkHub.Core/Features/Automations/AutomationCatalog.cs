namespace MyWorkHub.Core.Features.Automations;

/// <summary>An automation the app knows how to run.</summary>
/// <param name="Id">Stable id: the run-history key and the configuration section name under
/// <c>Automation</c> (e.g. <c>Automation:RemoteWorkSync</c>).</param>
/// <param name="DisplayName">Name shown to the user.</param>
/// <param name="Description">One-line explanation of what it does.</param>
public sealed record AutomationDescriptor(string Id, string DisplayName, string Description);

/// <summary>The automations shipped with the app.</summary>
public static class AutomationCatalog
{
    public static AutomationDescriptor TransportReimbursement { get; } = new(
        "TransportReimbursement",
        "Transport reimbursement",
        "Downloads this month's transport-pass attestation and emails it to the billing address.");

    public static AutomationDescriptor RemoteWorkSync { get; } = new(
        "RemoteWorkSync",
        "Remote-work sync",
        "Copies this month's remote-work days to the Outlook calendar, PeopleNet and mwork.");

    /// <summary>Every shipped automation, in display order.</summary>
    public static IReadOnlyList<AutomationDescriptor> All { get; } = [TransportReimbursement, RemoteWorkSync];

    /// <summary>Display name for an automation id; the id itself when unknown (e.g. a retired automation's history).</summary>
    public static string DisplayNameOf(string automationId)
        => All.FirstOrDefault(a => string.Equals(a.Id, automationId, StringComparison.Ordinal))?.DisplayName ?? automationId;
}
