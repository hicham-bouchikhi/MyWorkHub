using Microsoft.Kiota.Abstractions;
using MyWorkHub.Core.Features.Automations;
using MyWorkHub.Infrastructure.Features.Automations;
using MyWorkHub.Infrastructure.Features.Automations.Sites;
using MyWorkHub.Infrastructure.Features.Automations.Transport;
using MyWorkHub.Infrastructure.Tests.TestDoubles;

namespace MyWorkHub.Infrastructure.Tests.Features.Automations;

public sealed class TransportReimbursementAutomationTests : IDisposable
{
    private readonly SiteAutomationFixture _fixture = new();
    private readonly FakeGraphRequestAdapter _graph = new(_ => null);
    private readonly DirectoryInfo _files = Directory.CreateTempSubdirectory("mwh-attestation-");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _graph.Dispose();
        _files.Delete(recursive: true);
    }

    private TransportReimbursementAutomation Automation(string facturationEmail = "billing@example.test")
    {
        _fixture.Settings["Automation:TransportReimbursement:FacturationEmail"] = facturationEmail;
        _fixture.Settings["Automation:TransportReimbursement:EmailSubjectTemplate"] = "Pass {{subscription_start}} - {{subscription_end}}";
        return new TransportReimbursementAutomation(
            _fixture.Runner(),
            _fixture.Sites,
            TransportReimbursementOptions.FromConfiguration(_fixture.Configuration),
            new AutomationPaths(SiteAutomationFixture.DOWNLOADS, SiteAutomationFixture.SCREENSHOTS),
            _graph.Client,
            SiteAutomationFixture.Clock);
    }

    private TransportAttestation AttestationOnDisk()
    {
        var path = Path.Combine(_files.FullName, "attestation-septembre.pdf");
        File.WriteAllBytes(path, [0x25, 0x50, 0x44, 0x46]);
        return new TransportAttestation(path, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));
    }

    [Fact]
    public async Task Should_sign_in_and_download_the_attestation_for_the_current_month()
    {
        var attestation = await Automation().DownloadAttestationAsync(Ct);

        var session = Assert.Single(_fixture.Browser.Sessions);
        Assert.Equal(
        [
            "goto https://tcl.example.test/",
            "fill #email=jdoe@example.test",
            "fill #password=tcl-secret",
            "click button[type=submit]",
            $"download text=Attestation -> {SiteAutomationFixture.DOWNLOADS}",
        ], session.Actions);
        Assert.Equal(Path.Combine(SiteAutomationFixture.DOWNLOADS, FakeBrowserSession.DOWNLOADED_FILE_NAME), attestation.FilePath);
        Assert.Equal(new DateOnly(2026, 9, 1), attestation.PeriodStart);
        Assert.Equal(new DateOnly(2026, 9, 30), attestation.PeriodEnd);
    }

    [Fact]
    public async Task Should_name_the_key_to_set_when_the_download_element_is_not_configured()
    {
        _fixture.Settings.Remove("ExternalSites:TCL:Selectors:AttestationDownload");

        var ex = await Assert.ThrowsAsync<AutomationException>(() => Automation().DownloadAttestationAsync(Ct));

        Assert.Contains("ExternalSites:TCL:Selectors:AttestationDownload", ex.Message, StringComparison.Ordinal);
        Assert.Empty(_fixture.Browser.Sessions);
    }

    [Fact]
    public async Task Should_email_the_attestation_as_an_attachment_to_the_billing_address()
    {
        await Automation().SendAttestationAsync(AttestationOnDisk(), Ct);

        var request = Assert.Single(_graph.Requests);
        Assert.Equal(Method.POST, request.HttpMethod);
        Assert.Equal("/me/sendMail", request.GraphPath());
        var body = SiteAutomationFixture.Body(request);
        Assert.Contains("\"address\":\"billing@example.test\"", body, StringComparison.Ordinal);
        Assert.Contains("\"subject\":\"Pass 01/09/2026 - 30/09/2026\"", body, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"attestation-septembre.pdf\"", body, StringComparison.Ordinal);
        Assert.Contains("\"contentBytes\":\"JVBERg==\"", body, StringComparison.Ordinal);
        Assert.Contains("#microsoft.graph.fileAttachment", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_not_send_anything_when_no_billing_address_is_configured()
    {
        var ex = await Assert.ThrowsAsync<AutomationException>(() => Automation(facturationEmail: "").SendAttestationAsync(AttestationOnDisk(), Ct));

        Assert.Contains("Automation:TransportReimbursement:FacturationEmail", ex.Message, StringComparison.Ordinal);
        Assert.Empty(_graph.Requests);
    }

    [Fact]
    public async Task Should_not_send_anything_when_the_downloaded_file_is_gone()
    {
        var missing = new TransportAttestation(Path.Combine(_files.FullName, "gone.pdf"), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        await Assert.ThrowsAsync<AutomationException>(() => Automation().SendAttestationAsync(missing, Ct));

        Assert.Empty(_graph.Requests);
    }

    [Fact]
    public void Should_fall_back_to_default_templates_when_none_are_configured()
    {
        var options = TransportReimbursementOptions.FromConfiguration(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var attestation = new TransportAttestation("a.pdf", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        Assert.Equal("", options.FacturationEmail);
        Assert.Equal(
            "Transport pass attestation - 01/09/2026 to 30/09/2026",
            TransportReimbursementAutomation.Fill(options.SubjectTemplate, attestation));
    }
}
