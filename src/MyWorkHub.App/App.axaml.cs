using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using MyWorkHub.Core.Configuration;
using MyWorkHub.UI.Theming;
using MyWorkHub.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;

namespace MyWorkHub.App;

public partial class App : Application
{
    private readonly IServiceProvider? _services;

    private Window? _mainWindow;
    private IScheduler? _scheduler;
    private bool _minimizeToTrayOnClose;
    private bool _isExiting;

    // Parameterless constructor for the Avalonia designer / XAML loader.
    public App()
    {
    }

    public App(IServiceProvider services)
    {
        _services = services;
    }

    internal App(IServiceProvider services, SingleInstanceGuard singleInstance)
        : this(services)
    {
        singleInstance.ShowRequested += OnSingleInstanceShowRequested;
    }

    private void OnSingleInstanceShowRequested(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_mainWindow is null)
                return;
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        });
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && _services is not null)
        {
            var uiOptions = _services.GetRequiredService<IOptions<UiOptions>>().Value;
            _minimizeToTrayOnClose = uiOptions.MinimizeToTrayOnClose;

            RequestedThemeVariant = uiOptions.Theme switch
            {
                "Light" => ThemeVariant.Light,
                "Dark"  => ThemeVariant.Dark,
                _       => ThemeVariant.Default
            };

            // Merge the configured colour palette before building the UI so its App* tokens resolve.
            PaletteManager.Apply(uiOptions.Palette);

            _mainWindow = _services.GetRequiredService<MainWindow>();
            _mainWindow.Closing += OnMainWindowClosing;
            desktop.MainWindow = _mainWindow;

            // Stop the scheduler on the regular exit path (window close when
            // MinimizeToTrayOnClose is false), not just the tray "Quit" command.
            desktop.ShutdownRequested += async (_, _) =>
            {
                if (_scheduler is not null)
                {
                    await _scheduler.Shutdown(waitForJobsToComplete: false);
                }
            };

            // Quartz starts in the background; no jobs are scheduled yet (Phase 10/11).
            _ = StartSchedulerAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task StartSchedulerAsync()
    {
        if (_services is null)
        {
            return;
        }

        var schedulerFactory = _services.GetRequiredService<ISchedulerFactory>();
        _scheduler = await schedulerFactory.GetScheduler();
        await _scheduler.Start();
    }

    // Tray menu: "Open MyWorkHub" — restore and focus the main window.
    private void OnOpenClicked(object? sender, EventArgs e)
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    // Tray menu: "Quit" — bypass the minimize-to-tray guard and shut the app down.
    private async void OnQuitClicked(object? sender, EventArgs e)
    {
        _isExiting = true;
        if (_scheduler is not null)
        {
            await _scheduler.Shutdown(waitForJobsToComplete: false);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    // Closing the window hides it to the tray instead of exiting, when configured.
    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_minimizeToTrayOnClose && !_isExiting)
        {
            e.Cancel = true;
            _mainWindow?.Hide();
        }
    }
}
