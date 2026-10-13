using CommunityToolkit.Mvvm.Input;

namespace MyWorkHub.Presentation.Navigation;

/// <summary>A page whose data can be reloaded in place; the periodic update check refreshes it while it is shown.</summary>
public interface IRefreshablePage
{
    /// <summary>Reloads the page's data, keeping the selected row when it is still listed.</summary>
    IAsyncRelayCommand RefreshCommand { get; }
}
