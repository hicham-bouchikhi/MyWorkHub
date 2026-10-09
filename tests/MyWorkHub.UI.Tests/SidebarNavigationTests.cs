using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Navigation;
using MyWorkHub.Presentation.ViewModels;
using MyWorkHub.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.UI.Tests;

/// <summary>
/// Clicks the real <see cref="MainWindow"/> sidebar: the feature list and the footer are two list boxes, so
/// selecting in one must not be undone by the other.
/// </summary>
public sealed class SidebarNavigationTests
{
    private static (MainWindow Window, MainWindowViewModel Shell) ShowShell()
    {
        var services = new ServiceCollection()
            .AddTransient<PlainPageViewModel>()
            .AddTransient<SamplePageViewModel>()
            .AddTransient<DeepLinkPageViewModel>()
            .BuildServiceProvider();
        var navigation = new NavigationService(services);
        var shell = new MainWindowViewModel(navigation, new NotificationCenterViewModel(navigation), [
            new("Dashboard", "D", typeof(PlainPageViewModel), 0),
            new("Pull requests", "P", typeof(SamplePageViewModel), 40),
            new("Settings", "S", typeof(DeepLinkPageViewModel), int.MaxValue, IsFooter: true),
        ]);
        var window = new MainWindow { DataContext = shell };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, shell);
    }

    private static void Click(MainWindow window, string label)
    {
        var entry = window.GetVisualDescendants().OfType<ListBoxItem>()
            .Single(item => item.DataContext is NavigationItem navigationItem && navigationItem.Label == label);
        var point = entry.TranslatePoint(new Point(entry.Bounds.Width / 2, entry.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Should_open_a_footer_page_when_it_is_clicked_again_after_visiting_a_feature_page()
    {
        var (window, shell) = ShowShell();

        Click(window, "Settings");
        Click(window, "Pull requests");
        Click(window, "Settings");

        Assert.IsType<DeepLinkPageViewModel>(shell.Navigation.CurrentPage);
        Assert.Equal("Settings", shell.SelectedItem?.Label);
    }

    [AvaloniaFact]
    public void Should_keep_the_clicked_entry_as_the_page_title_when_switching_between_the_lists()
    {
        var (window, shell) = ShowShell();

        Click(window, "Settings");
        Assert.Equal("Settings", shell.SelectedItem?.Label);

        Click(window, "Dashboard");
        Assert.Equal("Dashboard", shell.SelectedItem?.Label);
        Assert.IsType<PlainPageViewModel>(shell.Navigation.CurrentPage);
    }
}
