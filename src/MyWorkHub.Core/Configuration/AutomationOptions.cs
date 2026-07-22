namespace MyWorkHub.Core.Configuration;

public sealed class AutomationOptions
{
    public const string SECTION = "Automation";

    public TransportReimbursementOptions TransportReimbursement { get; set; } = new();
}

public sealed class TransportReimbursementOptions
{
    public bool Enabled { get; set; }
    public string CronExpression { get; set; } = "";
    public string FacturationEmail { get; set; } = "";
    public string EmailSubjectTemplate { get; set; } = "";
    public string EmailBodyTemplate { get; set; } = "";
}
