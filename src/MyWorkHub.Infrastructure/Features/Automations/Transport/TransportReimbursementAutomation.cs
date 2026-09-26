using System.Globalization;
using Microsoft.Graph;
using Microsoft.Graph.Me.SendMail;
using Microsoft.Graph.Models;
using MyWorkHub.Core.Features.Automations;
using MyWorkHub.Infrastructure.Features.Automations.Sites;

namespace MyWorkHub.Infrastructure.Features.Automations.Transport;

/// <summary>
/// <see cref="ITransportReimbursementAutomation"/>: downloads the attestation from the transport operator's
/// customer account with a scripted browser (sign in, click the configured download element — see
/// <see cref="SiteSelectorKeys.ATTESTATION_DOWNLOAD"/>), then sends it from the user's own mailbox through
/// Microsoft Graph.
/// <para>
/// The period is the current calendar month (a monthly pass); the code does not read the dates printed on
/// the attestation, since the site's page structure is not known to it.
/// </para>
/// </summary>
internal sealed class TransportReimbursementAutomation : ITransportReimbursementAutomation
{
    private const string START_PLACEHOLDER = "{{subscription_start}}";
    private const string END_PLACEHOLDER = "{{subscription_end}}";
    private const string DISPLAY_DATE_FORMAT = "dd/MM/yyyy";

    private readonly SiteScriptRunner _sites;
    private readonly ExternalSitesOptions _siteOptions;
    private readonly TransportReimbursementOptions _options;
    private readonly AutomationPaths _paths;
    private readonly GraphServiceClient _graph;
    private readonly TimeProvider _timeProvider;

    public TransportReimbursementAutomation(
        SiteScriptRunner sites,
        ExternalSitesOptions siteOptions,
        TransportReimbursementOptions options,
        AutomationPaths paths,
        GraphServiceClient graph,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(siteOptions);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _sites = sites;
        _siteOptions = siteOptions;
        _options = options;
        _paths = paths;
        _graph = graph;
        _timeProvider = timeProvider;
    }

    public async Task<TransportAttestation> DownloadAttestationAsync(CancellationToken ct = default)
    {
        var site = _siteOptions.Tcl;
        var downloadLink = site.RequireSelector(SiteSelectorKeys.ATTESTATION_DOWNLOAD);

        var path = await _sites.RunAsync(
            site,
            session => session.DownloadFileAsync(downloadLink, _paths.DownloadsDirectory, ct),
            ct).ConfigureAwait(false);

        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        var periodStart = new DateOnly(today.Year, today.Month, 1);
        return new TransportAttestation(path, periodStart, periodStart.AddMonths(1).AddDays(-1));
    }

    public async Task SendAttestationAsync(TransportAttestation attestation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(attestation);

        if (_options.FacturationEmail.Length == 0)
        {
            throw new AutomationException(
                $"No billing address configured. Set {TransportReimbursementOptions.SECTION}:FacturationEmail in appsettings.json.");
        }

        if (!File.Exists(attestation.FilePath))
        {
            throw new AutomationException($"The downloaded attestation is missing: {attestation.FilePath}");
        }

        var message = await BuildMessageAsync(attestation, ct).ConfigureAwait(false);
        await _graph.Me.SendMail
            .PostAsync(new SendMailPostRequestBody { Message = message, SaveToSentItems = true }, cancellationToken: ct)
            .ConfigureAwait(false);
    }

    // Graph's inline attachments are sent base64 in the request body, so the file is read here, once, to
    // build the request (attestations are small single-page documents, well under Graph's 3 MB inline limit).
    private async Task<Message> BuildMessageAsync(TransportAttestation attestation, CancellationToken ct)
        => new()
        {
            Subject = Fill(_options.SubjectTemplate, attestation),
            Body = new ItemBody { ContentType = BodyType.Text, Content = Fill(_options.BodyTemplate, attestation) },
            ToRecipients = [new Recipient { EmailAddress = new EmailAddress { Address = _options.FacturationEmail } }],
            Attachments =
            [
                new FileAttachment
                {
                    Name = Path.GetFileName(attestation.FilePath),
                    ContentBytes = await File.ReadAllBytesAsync(attestation.FilePath, ct).ConfigureAwait(false),
                },
            ],
        };

    internal static string Fill(string template, TransportAttestation attestation)
        => template
            .Replace(START_PLACEHOLDER, attestation.PeriodStart.ToString(DISPLAY_DATE_FORMAT, CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace(END_PLACEHOLDER, attestation.PeriodEnd.ToString(DISPLAY_DATE_FORMAT, CultureInfo.InvariantCulture), StringComparison.Ordinal);
}
