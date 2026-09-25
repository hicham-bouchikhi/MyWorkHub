namespace MyWorkHub.Presentation.ViewModels;

/// <summary>
/// Base for a page shown in the main content area. Every registered <see cref="PageViewModel"/>
/// must have a view mapped by some <see cref="Core.Modules.IViewModule"/> — startup validation fails
/// otherwise.
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
