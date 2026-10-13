using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Core.Features.Email;
using MyWorkHub.Core.Features.Todo;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Features.Email;
using MyWorkHub.Presentation.Features.PullRequests;
using MyWorkHub.Presentation.Features.Todo;
using MyWorkHub.Presentation.Features.WorkItems;
using MyWorkHub.Presentation.ViewModels;

namespace MyWorkHub.Presentation.Features.Dev;

/// <summary>
/// Developer tooling: fires test notifications so the toast → bell → navigation pipeline can be checked
/// by hand without waiting for a real event.
/// <list type="bullet">
/// <item>One informational-only notification per <see cref="NotificationSeverity"/> (no target).</item>
/// <item>A delayed notification, sent <see cref="DelaySeconds"/> after the click, to check toasts raised while
/// the window is minimized or hidden in the tray.</item>
/// <item>One deep-link notification per deep-link target kind (Todo, Email, Pull request, Work item,
/// Mention). Each looks up a <em>real</em> row through the feature's own service, so clicking the toast
/// or bell entry must open that page, scroll to the row and flash it. When the service is missing,
/// fails (e.g. not signed in) or returns nothing, a synthetic id is sent instead: the page must still
/// open, and — being an unknown id — highlight nothing.</item>
/// </list>
/// UI-only tooling: it reuses the features' Core services and <c>TargetFor…</c> helpers and has no
/// Core interface or infrastructure of its own.
/// </summary>
public sealed partial class DevViewModel : PageViewModel
{
    /// <summary>Email element id sent when no real message can be loaded (never a real Graph id).</summary>
    public static string SyntheticEmailId { get; } = "dev-synthetic-email";

    private const int SYNTHETIC_NUMERIC_ID = 0;

    private readonly INotificationService? _notifications;
    private readonly ITodoRepository? _todos;
    private readonly IEmailService? _email;
    private readonly IAzureDevOpsService? _azureDevOps;

    public DevViewModel(
        INotificationService? notifications = null,
        ITodoRepository? todos = null,
        IEmailService? email = null,
        IAzureDevOpsService? azureDevOps = null)
        : base("Developer")
    {
        _notifications = notifications;
        _todos = todos;
        _email = email;
        _azureDevOps = azureDevOps;
    }

    /// <summary>Every severity, in declaration order — one test button each.</summary>
    public static IReadOnlyList<NotificationSeverity> Severities { get; } = Enum.GetValues<NotificationSeverity>();

    /// <summary>False when no notification service is registered; every button is then disabled.</summary>
    public bool CanNotify => _notifications is not null;

    /// <summary>What the last deep-link button sent (real row or synthetic id, and why).</summary>
    [ObservableProperty]
    private string? _lastResult;

    /// <summary>How long the delayed test waits before notifying, in seconds.</summary>
    [ObservableProperty]
    private decimal _delaySeconds = 5;

    [RelayCommand(CanExecute = nameof(CanNotify), IncludeCancelCommand = true)]
    private async Task SendDelayedTestNotificationAsync(CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds((double)Math.Max(0, DelaySeconds));
        LastResult = $"Delayed test: notifying in {delay.TotalSeconds:0.#} s…";
        try
        {
            await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException)
        {
            LastResult = "Delayed test: cancelled.";
            return;
        }

        _notifications?.Notify(
            "Delayed test notification",
            $"Sent {delay.TotalSeconds:0.#} s after the click. Informational only: clicking it navigates nowhere.");
        LastResult = "Delayed test: sent.";
    }

    [RelayCommand(CanExecute = nameof(CanNotify))]
    private void SendTestNotification(NotificationSeverity severity)
        => _notifications?.Notify(
            $"Test notification ({severity})",
            "Informational only: clicking it navigates nowhere.",
            severity);

    [RelayCommand(CanExecute = nameof(CanNotify))]
    private Task SendTodoDeepLinkAsync(CancellationToken ct)
        => SendDeepLinkAsync(
            "Todo",
            _todos is null ? null : async token =>
                (await _todos.GetAllAsync(token)) is [var todo, ..]
                    ? new DeepLinkSample(TodoViewModel.TargetFor(todo.Id), todo.Title)
                    : null,
            TodoViewModel.TargetFor(Guid.Empty),
            ct);

    [RelayCommand(CanExecute = nameof(CanNotify))]
    private Task SendEmailDeepLinkAsync(CancellationToken ct)
        => SendDeepLinkAsync(
            "Email",
            _email is null ? null : async token =>
                (await _email.GetRecentEmailsAsync(token)) is [var message, ..]
                    ? new DeepLinkSample(EmailViewModel.TargetFor(message.Id), message.Subject)
                    : null,
            EmailViewModel.TargetFor(SyntheticEmailId),
            ct);

    [RelayCommand(CanExecute = nameof(CanNotify))]
    private Task SendPullRequestDeepLinkAsync(CancellationToken ct)
        => SendDeepLinkAsync(
            "Pull request",
            _azureDevOps is null ? null : async token =>
                (await _azureDevOps.GetMyPullRequestsAsync(token)) is [var pr, ..]
                    ? new DeepLinkSample(PullRequestsViewModel.TargetFor(pr.Id), $"!{pr.Id} {pr.Title}")
                    : null,
            PullRequestsViewModel.TargetFor(SYNTHETIC_NUMERIC_ID),
            ct);

    [RelayCommand(CanExecute = nameof(CanNotify))]
    private Task SendWorkItemDeepLinkAsync(CancellationToken ct)
        => SendDeepLinkAsync(
            "Work item",
            _azureDevOps is null ? null : async token =>
                (await _azureDevOps.GetMyWorkItemsAsync(token)) is [var item, ..]
                    ? new DeepLinkSample(WorkItemsViewModel.TargetForWorkItem(item.Id), $"#{item.Id} {item.Title}")
                    : null,
            WorkItemsViewModel.TargetForWorkItem(SYNTHETIC_NUMERIC_ID),
            ct);

    [RelayCommand(CanExecute = nameof(CanNotify))]
    private Task SendMentionDeepLinkAsync(CancellationToken ct)
        => SendDeepLinkAsync(
            "Mention",
            _azureDevOps is null ? null : async token =>
            {
                var workItems = await _azureDevOps.GetMyWorkItemsAsync(token);
                return (await _azureDevOps.GetWorkItemMentionsAsync(workItems, token)) is [var mention, ..]
                    ? new DeepLinkSample(
                        WorkItemsViewModel.TargetForMention(mention.CommentId),
                        $"{mention.AuthorDisplayName} on #{mention.WorkItemId}")
                    : null;
            },
            WorkItemsViewModel.TargetForMention(SYNTHETIC_NUMERIC_ID),
            ct);

    /// <summary>
    /// Sends a deep-link test notification to the row <paramref name="findSample"/> returns, or to
    /// <paramref name="synthetic"/> when there is no service (<paramref name="findSample"/> is null), no
    /// row, or the lookup fails.
    /// </summary>
    private async Task SendDeepLinkAsync(
        string kind,
        Func<CancellationToken, Task<DeepLinkSample?>>? findSample,
        NavigationTarget synthetic,
        CancellationToken ct)
    {
        DeepLinkSample? sample = null;
        var fallbackReason = "the feature's service is not registered";
        if (findSample is not null)
        {
            try
            {
                sample = await findSample(ct);
                fallbackReason = "no row is loaded";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                fallbackReason = ex.Message;
            }
        }

        if (sample is not null)
        {
            _notifications?.Notify(
                $"Deep-link test: {kind}",
                $"Click to open {sample.Target.ViewModelType.Name} and highlight “{sample.Label}”.",
                NotificationSeverity.INFORMATION,
                sample.Target);
            LastResult = $"{kind}: sent a link to the real row “{sample.Label}” (element id {sample.Target.ElementId}).";
            return;
        }

        _notifications?.Notify(
            $"Deep-link test: {kind} (synthetic)",
            $"Click to check that an unknown element id opens the page without highlighting anything ({fallbackReason}).",
            NotificationSeverity.INFORMATION,
            synthetic);
        LastResult = $"{kind}: sent a synthetic element id {synthetic.ElementId} — {fallbackReason}.";
    }

    /// <summary>A real row to deep-link to, with a human-readable label for the notification text.</summary>
    private sealed record DeepLinkSample(NavigationTarget Target, string Label);
}
