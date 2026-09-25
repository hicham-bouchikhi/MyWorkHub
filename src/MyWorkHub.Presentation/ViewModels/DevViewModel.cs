using MyWorkHub.Core.Abstractions;
using CommunityToolkit.Mvvm.Input;

namespace MyWorkHub.UI.ViewModels;

public sealed partial class DevViewModel : PageViewModel
{
    private readonly INotificationService? _notificationService;

    public DevViewModel(INotificationService? notificationService = null) : base("Developer")
    {
        _notificationService = notificationService;
    }

    [RelayCommand]
    private void TestNotificationInfo()
    {
        _notificationService?.Notify(
            "Test Notification",
            "This is an informational test notification.",
            NotificationSeverity.INFORMATION);
    }

    [RelayCommand]
    private void TestNotificationSuccess()
    {
        _notificationService?.Notify(
            "Success!",
            "This is a success test notification.",
            NotificationSeverity.SUCCESS);
    }

    [RelayCommand]
    private void TestNotificationWarning()
    {
        _notificationService?.Notify(
            "Warning",
            "This is a warning test notification.",
            NotificationSeverity.WARNING);
    }

    [RelayCommand]
    private void TestNotificationError()
    {
        _notificationService?.Notify(
            "Error",
            "This is an error test notification.",
            NotificationSeverity.ERROR);
    }
}
