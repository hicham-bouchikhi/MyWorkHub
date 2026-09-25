using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyWorkHub.Core.Features.GraphAuth;
using MyWorkHub.Presentation.ViewModels;

namespace MyWorkHub.Presentation.Features.GraphAuth;

/// <summary>
/// Shared behaviour of the Microsoft 365 pages (Email, Calendar, Teams): load on first display, manual
/// refresh, busy/error state, and — when a load reports <see cref="GraphNotConnectedException"/> — a
/// sign-in prompt instead of an error. Signing in on one page makes the shared token cache valid for
/// all of them. Subclasses only fetch and publish their data in <see cref="LoadDataAsync"/>.
/// </summary>
public abstract partial class GraphPageViewModel : PageViewModel
{
    private readonly IGraphConnectionService? _connection;
    private bool _isLoaded;

    /// <param name="title">Page heading.</param>
    /// <param name="isAvailable">Whether the page's data service is registered (optional-service pattern).</param>
    /// <param name="connection">The shared Microsoft 365 sign-in; null disables the sign-in prompt.</param>
    protected GraphPageViewModel(string title, bool isAvailable, IGraphConnectionService? connection)
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

    /// <summary>True when the last load found no usable Microsoft 365 session.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    private bool _needsSignIn;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Loads on first display; later visits keep the cached data (use <see cref="RefreshCommand"/>).</summary>
    [RelayCommand]
    private Task LoadAsync(CancellationToken ct)
        => IsAvailable && !_isLoaded ? ReloadAsync(ct) : Task.CompletedTask;

    [RelayCommand]
    private Task RefreshAsync(CancellationToken ct) => IsAvailable ? ReloadAsync(ct) : Task.CompletedTask;

    /// <summary>Interactive system-browser sign-in, then a reload on success.</summary>
    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private async Task SignInAsync(CancellationToken ct)
    {
        IsBusy = true;
        GraphSignInResult result;
        try
        {
            result = await _connection!.SignInAsync(ct);
        }
        finally
        {
            IsBusy = false;
        }

        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage;
            return;
        }

        await ReloadAsync(ct);
    }

    private bool CanSignIn() => _connection is not null && NeedsSignIn;

    /// <summary>Fetches the page's data and replaces the displayed items. Runs on the UI thread.</summary>
    protected abstract Task LoadDataAsync(CancellationToken ct);

    /// <summary>Called after every successful load (e.g. to apply a pending deep-link focus).</summary>
    protected virtual void OnDataLoaded()
    {
    }

    /// <summary>Shows a failure raised outside the load cycle (e.g. a detail fetch) the same way a load failure is shown.</summary>
    protected void ReportFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is GraphNotConnectedException)
        {
            NeedsSignIn = true;
            ErrorMessage = null;
        }
        else
        {
            ErrorMessage = $"Could not reach Microsoft 365: {exception.Message}";
        }
    }

    private async Task ReloadAsync(CancellationToken ct)
    {
        IsBusy = true;
        try
        {
            await LoadDataAsync(ct);
            _isLoaded = true;
            NeedsSignIn = false;
            ErrorMessage = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ReportFailure(ex);
            return;
        }
        finally
        {
            IsBusy = false;
        }

        OnDataLoaded();
    }
}
