namespace MyWorkHub.Core.Features.CliAgent;

/// <summary>
/// Thrown by <see cref="ICliAgentRunner"/> when the agent CLI cannot be started (not installed, wrong
/// configured path) or exits with a failure. The message is meant to be shown to the user as is.
/// </summary>
public sealed class CliAgentException : Exception
{
    public CliAgentException()
        : base("The AI agent CLI failed.")
    {
    }

    public CliAgentException(string message)
        : base(message)
    {
    }

    public CliAgentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
