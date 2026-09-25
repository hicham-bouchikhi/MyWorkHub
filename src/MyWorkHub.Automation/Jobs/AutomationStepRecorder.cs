using Microsoft.Extensions.Logging;
using MyWorkHub.Core.Features.Automations;

namespace MyWorkHub.Automation.Jobs;

/// <summary>
/// Runs the steps of one automation run, each inside its own <c>try/catch</c>, and records every step's
/// outcome. This is the isolation boundary between steps: a step that throws is recorded as
/// <see cref="AutomationStepStatus.FAILED"/> with its error message and the recorder returns normally, so
/// the job goes on to its next step. Only cancellation of the run itself escapes (the whole run stops).
/// </summary>
public sealed partial class AutomationStepRecorder
{
    private readonly List<AutomationStepResult> _results = [];
    private readonly ILogger _logger;

    public AutomationStepRecorder(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>Every recorded step, in execution order.</summary>
    public IReadOnlyList<AutomationStepResult> Results => _results;

    /// <summary>Runs one step in isolation. Returns whether it succeeded.</summary>
    public async Task<bool> RunAsync(string name, Func<CancellationToken, Task> step, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(step);

        var result = await RunAsync<object>(
            name,
            async c =>
            {
                await step(c).ConfigureAwait(false);
                return name;
            },
            describe: null,
            ct).ConfigureAwait(false);
        return result is not null;
    }

    /// <summary>
    /// Runs one step that produces a value, in isolation. Returns the value, or null when the step failed.
    /// <paramref name="describe"/> may turn the value into a note shown with the successful step.
    /// </summary>
    public async Task<T?> RunAsync<T>(
        string name,
        Func<CancellationToken, Task<T>> step,
        Func<T, string?>? describe,
        CancellationToken ct)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(step);
        ct.ThrowIfCancellationRequested();

        try
        {
            var value = await step(ct).ConfigureAwait(false);
            _results.Add(new AutomationStepResult(name, AutomationStepStatus.SUCCESS, describe?.Invoke(value)));
            return value;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The run is being stopped: not a step failure.
            throw;
        }
        catch (Exception ex)
        {
            // Deliberately broad (CA1031 is off for this reason): whatever one external system throws, the
            // remaining steps must still run.
            LogStepFailed(ex, name);
            Fail(name, ex.Message);
            return null;
        }
    }

    /// <summary>Records a step that failed without being run (e.g. its input could not be obtained).</summary>
    public void Fail(string name, string message)
        => _results.Add(new AutomationStepResult(name, AutomationStepStatus.FAILED, message));

    /// <summary>Records a step that was not attempted, with the reason.</summary>
    public void Skip(string name, string reason)
        => _results.Add(new AutomationStepResult(name, AutomationStepStatus.SKIPPED, reason));

    [LoggerMessage(Level = LogLevel.Warning, Message = "Automation step '{Step}' failed")]
    private partial void LogStepFailed(Exception exception, string step);
}
