namespace MyWorkHub.UI.ViewModels;

/// <summary>
/// Base for a page shown in the main content area. Phase 3 ships these as titled
/// placeholders; later phases replace the bodies with real content and dependencies.
/// </summary>
public abstract class PageViewModel : ViewModelBase
{
    protected PageViewModel(string title)
    {
        Title = title;
    }

    /// <summary>Heading shown by the page and matched to the sidebar entry.</summary>
    public string Title { get; }
}
