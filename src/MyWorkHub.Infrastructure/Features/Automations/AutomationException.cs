namespace MyWorkHub.Infrastructure.Features.Automations;

/// <summary>
/// An automation step could not complete for a reason the user can act on — missing login, a site
/// selector not configured, a site that did not behave as scripted. Its message is what the run history
/// shows for the step.
/// </summary>
public sealed class AutomationException : Exception
{
    public AutomationException()
    {
    }

    public AutomationException(string message)
        : base(message)
    {
    }

    public AutomationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
