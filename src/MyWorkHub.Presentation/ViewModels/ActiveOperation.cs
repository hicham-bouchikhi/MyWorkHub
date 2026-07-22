using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MyWorkHub.UI.ViewModels;

/// <summary>
/// One operation currently running (e.g. a PR review or an email summary), shown in the bell's
/// "In progress" list. Carries the display label and a cancel callback so the flyout can offer a
/// Stop button that routes back to the owner's own cancellation path. The callback is a UI-thread
/// <see cref="Action"/>, mirroring how <see cref="NotificationEntry"/> holds its click action.
/// </summary>
public sealed partial class ActiveOperation : ObservableObject
{
    private readonly Action _cancel;

    public ActiveOperation(string label, Action cancel)
    {
        ArgumentNullException.ThrowIfNull(cancel);

        Label = label;
        _cancel = cancel;
    }

    /// <summary>Human-readable description of the running operation.</summary>
    public string Label { get; }

    /// <summary>True once a stop has been requested; disables the Stop button so it fires once.</summary>
    [ObservableProperty]
    private bool _isCancelling;

    /// <summary>Requests cancellation of the operation. Idempotent — a second click is a no-op.</summary>
    [RelayCommand]
    private void Cancel()
    {
        if (IsCancelling)
        {
            return;
        }

        IsCancelling = true;
        _cancel();
    }
}
