namespace MyWorkHub.Core.Features.Automations;

/// <summary>A downloaded transport-pass attestation.</summary>
/// <param name="FilePath">Where the file was saved on disk.</param>
/// <param name="PeriodStart">First day the pass covers.</param>
/// <param name="PeriodEnd">Last day the pass covers.</param>
public sealed record TransportAttestation(string FilePath, DateOnly PeriodStart, DateOnly PeriodEnd);

/// <summary>
/// The monthly transport reimbursement: fetch the pass attestation from the operator's site, then send it
/// to the billing address. Two calls, so the job records each step separately (and skips sending when the
/// download failed).
/// </summary>
public interface ITransportReimbursementAutomation
{
    /// <summary>Signs in to the operator's site and downloads the current month's attestation to disk.</summary>
    Task<TransportAttestation> DownloadAttestationAsync(CancellationToken ct = default);

    /// <summary>Emails the attestation (as an attachment) to the configured billing address.</summary>
    Task SendAttestationAsync(TransportAttestation attestation, CancellationToken ct = default);
}
