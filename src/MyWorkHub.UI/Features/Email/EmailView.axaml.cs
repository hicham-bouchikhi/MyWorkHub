using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using MyWorkHub.Core;
using MyWorkHub.Presentation.Features.Email;

namespace MyWorkHub.UI.Features.Email;

public partial class EmailView : UserControl
{
    private readonly ReaderNavigationGuard _navigation = new();
    private EmailViewModel? _viewModel;

    public EmailView()
    {
        InitializeComponent();

        Reader.EnvironmentRequested += OnReaderEnvironmentRequested;
        Reader.AdapterCreated += (_, _) => ShowDocument();
        Reader.NavigationStarted += OnReaderNavigationStarted;
        Reader.NewWindowRequested += OnReaderNewWindowRequested;
    }

    // Loading is triggered by the view appearing (not by the view model's constructor) so a
    // deep-link focus request issued during navigation is queued before the list arrives.
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _viewModel?.LoadCommand.Execute(null);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as EmailViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ShowDocument();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EmailViewModel.SelectedDocument))
        {
            ShowDocument();
        }
    }

    private void ShowDocument()
    {
        if (_viewModel is not null)
        {
            _navigation.ExpectDocument();
            Reader.NavigateToString(_viewModel.SelectedDocument);
        }
    }

    // No cookies, cache or storage survive the session: nothing a message does is remembered.
    private static void OnReaderEnvironmentRequested(object? sender, WebViewEnvironmentRequestedEventArgs e)
    {
        switch (e)
        {
            case WindowsWebView2EnvironmentRequestedEventArgs webView2:
                webView2.IsInPrivateModeEnabled = true;
                webView2.UserDataFolder = Path.Combine(AppPaths.RootDir, "webview");
                break;
            case GtkWebViewEnvironmentRequestedEventArgs gtk:
                gtk.EphemeralDataManager = true;
                break;
            case AppleWKWebViewEnvironmentRequestedEventArgs webKit:
                webKit.NonPersistentDataStore = true;
                break;
        }
    }

    // The pane only ever shows the document we gave it (see ReaderNavigationGuard); a followed web or mail link
    // goes to the default browser instead.
    private void OnReaderNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
    {
        var decision = _navigation.Decide(e.Request);
        if (decision == ReaderNavigation.ALLOW)
        {
            return;
        }

        e.Cancel = true;
        if (decision == ReaderNavigation.OPEN_EXTERNALLY)
        {
            _viewModel?.OpenLink(e.Request!);
        }
    }

    private void OnReaderNewWindowRequested(object? sender, WebViewNewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (e.Request is { } request)
        {
            _viewModel?.OpenLink(request);
        }
    }
}
