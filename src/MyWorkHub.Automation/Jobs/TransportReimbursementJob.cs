using Microsoft.Extensions.Logging;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.Automations;
using Quartz;

namespace MyWorkHub.Automation.Jobs;

/// <summary>
/// Downloads the transport-pass attestation, then emails it to the billing address. The steps depend on
/// each other: when the download fails, sending is recorded as skipped rather than attempted.
/// </summary>
[DisallowConcurrentExecution]
public sealed class TransportReimbursementJob : AutomationJob
{
    internal const string DOWNLOAD_STEP = "Download attestation";
    internal const string SEND_STEP = "Email attestation";

    private readonly ITransportReimbursementAutomation _transport;

    public TransportReimbursementJob(
        ITransportReimbursementAutomation transport,
        IAutomationLogger runLog,
        ILogger<TransportReimbursementJob> logger,
        INotificationService? notifications = null)
        : base(AutomationCatalog.TransportReimbursement, runLog, logger, notifications)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _transport = transport;
    }

    protected override async Task RunStepsAsync(AutomationStepRecorder steps, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var attestation = await steps.RunAsync(
            DOWNLOAD_STEP,
            _transport.DownloadAttestationAsync,
            a => $"Saved to {a.FilePath}",
            ct).ConfigureAwait(false);

        if (attestation is null)
        {
            steps.Skip(SEND_STEP, "No attestation was downloaded.");
            return;
        }

        await steps.RunAsync(SEND_STEP, c => _transport.SendAttestationAsync(attestation, c), ct).ConfigureAwait(false);
    }
}
