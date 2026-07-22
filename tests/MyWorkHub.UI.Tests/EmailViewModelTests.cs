using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Models;
using MyWorkHub.UI.ViewModels;

namespace MyWorkHub.UI.Tests;

public sealed class EmailViewModelTests
{
    private static EmailItem Mail(string id, string from, string subject) =>
        new(id, from, subject, $"Preview of {subject}", DateTime.UtcNow, IsFlagged: false);

    [Fact]
    public async Task Refresh_loads_important_emails()
    {
        var service = new FakeEmailService([Mail("1", "Alice", "Release notes"), Mail("2", "Bob", "Lunch")]);
        var vm = new EmailViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.IsEmpty);
        Assert.Equal(2, vm.Emails.Count);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task Refresh_with_no_emails_sets_the_empty_flag()
    {
        var vm = new EmailViewModel(new FakeEmailService());

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.Emails);
    }

    [Fact]
    public async Task Refresh_on_service_failure_sets_an_error_message()
    {
        var vm = new EmailViewModel(new FakeEmailService(throwOnCall: true));

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        Assert.Empty(vm.Emails);
        Assert.False(vm.IsEmpty); // the error message stands alone; no "No messages found."
    }

    [Fact]
    public async Task Search_filters_by_sender_or_subject_case_insensitively()
    {
        var service = new FakeEmailService(
        [
            Mail("1", "Alice", "Release notes"),
            Mail("2", "Bob", "Lunch plans"),
            Mail("3", "Carol", "release follow-up"),
        ]);
        var vm = new EmailViewModel(service);
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.SearchText = "release";

        Assert.Equal(2, vm.Emails.Count);
        Assert.All(vm.Emails, row =>
            Assert.True(
                row.Subject.Contains("release", StringComparison.OrdinalIgnoreCase) ||
                row.From.Contains("release", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Clearing_the_search_restores_every_row()
    {
        var service = new FakeEmailService([Mail("1", "Alice", "Release notes"), Mail("2", "Bob", "Lunch")]);
        var vm = new EmailViewModel(service);
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.SearchText = "lunch";
        Assert.Single(vm.Emails);

        vm.SearchText = null;
        Assert.Equal(2, vm.Emails.Count);
    }

    [Fact]
    public void Should_return_valid_palette_index_for_any_folder_name()
    {
        var index = FolderColorPicker.IndexFor("Some Deeply Nested Folder");

        Assert.InRange(index, 0, FolderColorPicker.PaletteSize - 1);
    }

    [Theory]
    [InlineData("Inbox", "inbox")]
    [InlineData("ARCHIVE", "archive")]
    public void Should_return_same_palette_index_for_same_folder_name_case_insensitively(string a, string b)
    {
        Assert.Equal(FolderColorPicker.IndexFor(a), FolderColorPicker.IndexFor(b));
    }

    [Fact]
    public void EmailRow_exposes_folder_name_from_email_item()
    {
        var item = new EmailItem("id", "from", "subject", "preview", DateTime.UtcNow, IsFlagged: false, FolderName: "Sent Items");

        var row = new EmailRow(item);

        Assert.Equal("Sent Items", row.FolderName);
    }

    [Fact]
    public void Open_launches_the_outlook_web_url_for_the_message()
    {
        var launcher = new FakeBrowserLauncher();
        var vm = new EmailViewModel(new FakeEmailService(), launcher);
        var row = new EmailRow(Mail("abc123", "Alice", "Hi"));

        vm.OpenCommand.Execute(row);

        Assert.Equal("https://outlook.office.com/mail/id/abc123", launcher.LastUrl);
    }

    [Fact]
    public async Task Summarise_row_sets_the_row_summary_and_notifies_success()
    {
        var service = new FakeEmailService([Mail("1", "Alice", "Release notes")]);
        var summary = new FakeEmailSummaryService("• Alice sent release notes.");
        var notifier = new FakeNotificationService();
        var vm = new EmailViewModel(service, summaryService: summary, notification: notifier);
        await vm.RefreshCommand.ExecuteAsync(null);
        var row = vm.Emails.Single();

        await vm.SummarizeRowCommand.ExecuteAsync(row);

        Assert.Equal("• Alice sent release notes.", row.SummaryText);
        Assert.Equal(1, summary.CallCount);
        Assert.Equal(1, notifier.NotifyCount);
        Assert.Equal(NotificationSeverity.SUCCESS, notifier.LastSeverity);
    }

    [Fact]
    public async Task Summarise_row_sends_only_the_targeted_email_to_the_service()
    {
        var service = new FakeEmailService([Mail("1", "Alice", "Release notes"), Mail("2", "Bob", "Lunch")]);
        var summary = new FakeEmailSummaryService();
        var vm = new EmailViewModel(service, summaryService: summary, notification: new FakeNotificationService());
        await vm.RefreshCommand.ExecuteAsync(null);
        var bob = vm.Emails.Single(r => r.Id == "2");

        await vm.SummarizeRowCommand.ExecuteAsync(bob);

        Assert.NotNull(summary.LastEmails);
        Assert.Equal("2", summary.LastEmails!.Single().Id);
    }

    [Fact]
    public async Task Summarising_one_row_leaves_other_rows_summarisable()
    {
        var gated = new GatedEmailSummaryService();
        var service = new FakeEmailService([Mail("1", "Alice", "A"), Mail("2", "Bob", "B")]);
        var vm = new EmailViewModel(service, summaryService: gated, notification: new FakeNotificationService());
        await vm.RefreshCommand.ExecuteAsync(null);
        var rowA = vm.Emails.Single(r => r.Id == "1");
        var rowB = vm.Emails.Single(r => r.Id == "2");

        // Start summarising row A and leave it running (gate not released).
        var running = vm.SummarizeRowCommand.ExecuteAsync(rowA);

        Assert.True(rowA.IsSummarizing);
        Assert.True(vm.SummarizeRowCommand.CanExecute(rowB)); // other rows stay enabled, not greyed

        gated.Release("done");
        await running;
        Assert.False(rowA.IsSummarizing);
    }

    [Fact]
    public async Task Summary_survives_a_refresh()
    {
        var service = new FakeEmailService([Mail("1", "Alice", "Release notes")]);
        var vm = new EmailViewModel(
            service,
            summaryService: new FakeEmailSummaryService("• digest"),
            notification: new FakeNotificationService());
        await vm.RefreshCommand.ExecuteAsync(null);
        var row = vm.Emails.Single();
        await vm.SummarizeRowCommand.ExecuteAsync(row);
        Assert.Equal("• digest", row.SummaryText);

        // The Email view re-runs Refresh on every navigation, rebuilding the rows.
        await vm.RefreshCommand.ExecuteAsync(null);

        var reloaded = vm.Emails.Single();
        Assert.NotSame(row, reloaded);                    // a fresh row object
        Assert.Equal("• digest", reloaded.SummaryText);   // summary restored from the cache
    }

    [Fact]
    public async Task Summary_notification_navigates_to_the_email_page_when_activated()
    {
        var service = new FakeEmailService([Mail("1", "Alice", "Release notes")]);
        var notifier = new FakeNotificationService();
        var nav = new FakeNavigationService();
        var vm = new EmailViewModel(
            service,
            summaryService: new FakeEmailSummaryService(),
            notification: notifier,
            navigation: nav);
        await vm.RefreshCommand.ExecuteAsync(null);

        await vm.SummarizeRowCommand.ExecuteAsync(vm.Emails.Single());

        Assert.NotNull(notifier.LastOnActivated);
        notifier.LastOnActivated!.Invoke();
        Assert.Equal(typeof(EmailViewModel), nav.LastNavigatedTo);
    }

    [Fact]
    public async Task Summarise_passes_full_body_to_summarizer()
    {
        var service = new FakeEmailService([Mail("1", "Alice", "Release notes")]);
        var summary = new FakeEmailSummaryService();
        var vm = new EmailViewModel(service, summaryService: summary, notification: new FakeNotificationService());
        await vm.RefreshCommand.ExecuteAsync(null);
        var row = vm.Emails.Single();

        await vm.SummarizeRowCommand.ExecuteAsync(row);

        Assert.NotNull(summary.LastEmails);
        Assert.NotNull(summary.LastEmails!.Single().FullBody);
        Assert.Equal("Full body of message 1", summary.LastEmails!.Single().FullBody);
    }

    [Fact]
    public async Task Summarise_uses_cached_body_on_second_click()
    {
        var service = new FakeEmailService([Mail("1", "Alice", "Release notes")]);
        var vm = new EmailViewModel(service, summaryService: new FakeEmailSummaryService(), notification: new FakeNotificationService());
        await vm.RefreshCommand.ExecuteAsync(null);
        var row = vm.Emails.Single();

        await vm.SummarizeRowCommand.ExecuteAsync(row);
        await vm.SummarizeRowCommand.ExecuteAsync(row);

        Assert.Equal(1, service.GetBodyCallCount);
    }

    [Fact]
    public async Task Full_body_survives_a_refresh()
    {
        var service = new FakeEmailService([Mail("1", "Alice", "Release notes")]);
        var summary = new FakeEmailSummaryService();
        var vm = new EmailViewModel(service, summaryService: summary, notification: new FakeNotificationService());
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.SummarizeRowCommand.ExecuteAsync(vm.Emails.Single());

        await vm.RefreshCommand.ExecuteAsync(null);

        var reloaded = vm.Emails.Single();
        Assert.Equal("Full body of message 1", reloaded.FullBody);
    }

    [Fact]
    public async Task Summarise_falls_back_to_preview_when_body_fetch_fails()
    {
        var service = new FakeEmailService([Mail("1", "Alice", "Release notes")]);
        var summary = new FakeEmailSummaryService("• fallback summary");
        var vm = new EmailViewModel(
            new BodyThrowingEmailService(service),
            summaryService: summary,
            notification: new FakeNotificationService());
        await vm.RefreshCommand.ExecuteAsync(null);
        var row = vm.Emails.Single();

        await vm.SummarizeRowCommand.ExecuteAsync(row);

        Assert.NotNull(row.SummaryText); // summarisation completed despite body fetch failure
        Assert.Null(row.FullBody);       // body was never stored
        Assert.NotNull(summary.LastEmails);
        Assert.Null(summary.LastEmails!.Single().FullBody); // fallback: Preview was used
    }

    private sealed class GatedEmailSummaryService : IEmailSummaryService
    {
        private readonly TaskCompletionSource<string> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<string> SummarizeAsync(System.Collections.Generic.IReadOnlyList<EmailItem> emails, System.Threading.CancellationToken ct = default) =>
            _gate.Task;

        public void Release(string summary) => _gate.TrySetResult(summary);
    }

    /// <summary>Blocks inside <see cref="SummarizeAsync"/> until its token is cancelled.</summary>
    private sealed class CancellingEmailSummaryService : IEmailSummaryService
    {
        public async Task<string> SummarizeAsync(System.Collections.Generic.IReadOnlyList<EmailItem> emails, System.Threading.CancellationToken ct = default)
        {
            await Task.Delay(System.Threading.Timeout.Infinite, ct).ConfigureAwait(false);
            return "";
        }
    }

    /// <summary>Wraps a delegate to list and count emails, but always throws on <see cref="GetEmailBodyAsync"/>.</summary>
    private sealed class BodyThrowingEmailService : IEmailService
    {
        private readonly IEmailService _inner;

        public BodyThrowingEmailService(IEmailService inner) => _inner = inner;

        public Task<System.Collections.Generic.IReadOnlyList<EmailItem>> GetImportantEmailsAsync(System.Threading.CancellationToken ct = default) =>
            _inner.GetImportantEmailsAsync(ct);

        public Task<int> GetUnreadCountAsync(System.Threading.CancellationToken ct = default) =>
            _inner.GetUnreadCountAsync(ct);

        public Task<System.Collections.Generic.IReadOnlyList<MailFolder>> GetMailFoldersAsync(System.Threading.CancellationToken ct = default) =>
            _inner.GetMailFoldersAsync(ct);

        public Task<string> GetEmailBodyAsync(string id, System.Threading.CancellationToken ct = default) =>
            throw new HttpRequestException("body fetch failed");

        public Task SendEmailAsync(string to, string subject, string body, string? attachmentPath = null, System.Threading.CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    [Fact]
    public async Task Summarise_row_shows_and_clears_a_bell_activity()
    {
        var gated = new GatedEmailSummaryService();
        var center = new NotificationCenterViewModel();
        var service = new FakeEmailService([Mail("1", "Alice", "Release notes")]);
        var vm = new EmailViewModel(
            service,
            summaryService: gated,
            notification: new FakeNotificationService(),
            notificationCenter: center);
        await vm.RefreshCommand.ExecuteAsync(null);
        var row = vm.Emails.Single();

        var running = vm.SummarizeRowCommand.ExecuteAsync(row);

        Assert.True(center.HasActive);
        Assert.Contains("Release notes", center.ActiveOperations.Single().Label, StringComparison.Ordinal);

        gated.Release("• digest");
        await running;

        Assert.False(center.HasActive);
        Assert.Empty(center.ActiveOperations);
    }

    [Fact]
    public async Task Stopping_the_bell_activity_cancels_the_summary_and_warns()
    {
        var center = new NotificationCenterViewModel();
        var notifier = new FakeNotificationService();
        var service = new FakeEmailService([Mail("1", "Alice", "Release notes")]);
        var vm = new EmailViewModel(
            service,
            summaryService: new CancellingEmailSummaryService(),
            notification: notifier,
            notificationCenter: center);
        await vm.RefreshCommand.ExecuteAsync(null);
        var row = vm.Emails.Single();

        var running = vm.SummarizeRowCommand.ExecuteAsync(row);
        center.ActiveOperations.Single().CancelCommand.Execute(null);
        await running;

        Assert.False(center.HasActive);
        Assert.False(row.IsSummarizing);
        Assert.Null(row.SummaryText);
        Assert.Equal(NotificationSeverity.WARNING, notifier.LastSeverity);
    }

    [Fact]
    public async Task Summarise_row_notifies_an_error_when_the_service_fails()
    {
        var service = new FakeEmailService([Mail("1", "Alice", "Release notes")]);
        var notifier = new FakeNotificationService();
        var vm = new EmailViewModel(service, summaryService: new FakeEmailSummaryService(throwOnCall: true), notification: notifier);
        await vm.RefreshCommand.ExecuteAsync(null);
        var row = vm.Emails.Single();

        await vm.SummarizeRowCommand.ExecuteAsync(row);

        Assert.Null(row.SummaryText);
        Assert.Equal(NotificationSeverity.ERROR, notifier.LastSeverity);
    }
}
