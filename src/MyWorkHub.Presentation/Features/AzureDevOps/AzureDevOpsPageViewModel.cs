using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Presentation.Navigation;
using MyWorkHub.Presentation.ViewModels;

namespace MyWorkHub.Presentation.Features.AzureDevOps;

/// <summary>
/// Shared behaviour of the Azure DevOps pages (Pull requests, Work items): load on first display, manual
/// refresh, busy/error state, and — when a load reports <see cref="AzureDevOpsNotConnectedException"/> — a
/// personal-access-token prompt instead of an error. A token entered on one page is stored for both.
/// Subclasses only fetch and publish their data in <see cref="LoadDataAsync"/>.
/// </summary>
public abstract partial class AzureDevOpsPageViewModel : PageViewModel, IRefreshablePage
{
    private readonly IAzureDevOpsConnectionService? _connection;
    private bool _isLoaded;

    /// <param name="title">Page heading.</param>
    /// <param name="isAvailable">Whether the page's data service is registered (optional-service pattern).</param>
    /// <param name="connection">The shared token store; null disables the token prompt.</param>
    protected AzureDevOpsPageViewModel(string title, bool isAvailable, IAzureDevOpsConnectionService? connection)
        : base(title)
    {
        IsAvailable = isAvailable;
        _connection = connection;
    }

    /// <summary>False when the page's data service is not registered; the page then shows a notice.</summary>
    public bool IsAvailable { get; }

    /// <summary>Whether at least one load has succeeded (the displayed items are real data).</summary>
    protected bool HasLoaded => _isLoaded;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>True when the last load could not authenticate; the page then asks for a token.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private bool _needsToken;

    /// <summary>Why a token is needed (none stored, rejected, organization not configured…).</summary>
    [ObservableProperty]
    private string? _connectionProblem;

    /// <summary>The token being typed into the prompt; cleared once accepted so it does not linger in memory.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _personalAccessToken = "";

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Loads on first display; later visits keep the cached data (use <see cref="RefreshCommand"/>).</summary>
    [RelayCommand]
    private Task LoadAsync(CancellationToken ct)
        => IsAvailable && !_isLoaded ? ReloadAsync(ct) : Task.CompletedTask;

    [RelayCommand]
    private Task RefreshAsync(CancellationToken ct) => IsAvailable ? ReloadAsync(ct) : Task.CompletedTask;

    /// <summary>Verifies and stores the entered token, then reloads on success.</summary>
    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync(CancellationToken ct)
    {
        IsBusy = true;
        AzureDevOpsConnectResult result;
        try
        {
            result = await _connection!.ConnectAsync(PersonalAccessToken, ct);
        }
        finally
        {
            IsBusy = false;
        }

        if (!result.Succeeded)
        {
            ConnectionProblem = result.ErrorMessage;
            return;
        }

        PersonalAccessToken = "";
        await ReloadAsync(ct);
    }

    private bool CanConnect()
        => _connection is not null && NeedsToken && !string.IsNullOrWhiteSpace(PersonalAccessToken);

    /// <summary>Fetches the page's data and replaces the displayed items. Runs on the UI thread.</summary>
    protected abstract Task LoadDataAsync(CancellationToken ct);

    /// <summary>Called after every successful load (e.g. to apply a pending deep-link focus).</summary>
    protected virtual void OnDataLoaded()
    {
    }

    private async Task ReloadAsync(CancellationToken ct)
    {
        IsBusy = true;
        try
        {
            await LoadDataAsync(ct);
            _isLoaded = true;
            NeedsToken = false;
            ConnectionProblem = null;
            ErrorMessage = null;
        }
        catch (AzureDevOpsNotConnectedException ex)
        {
            NeedsToken = true;
            ConnectionProblem = ex.Message;
            ErrorMessage = null;
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = $"Could not reach Azure DevOps: {ex.Message}";
            return;
        }
        finally
        {
            IsBusy = false;
        }

        OnDataLoaded();
    }
}
