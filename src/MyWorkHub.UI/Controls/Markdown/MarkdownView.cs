using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace MyWorkHub.UI.Controls.Markdown;

/// <summary>
/// Read-only, isolated Markdown viewer for untrusted text (work item content, comments): native controls only —
/// no HTML, no script, no network (see <see cref="MarkdownRenderer"/>). Bind <see cref="Markdown"/>; a clicked
/// http/https/mailto link executes <see cref="LinkCommand"/> with the <see cref="Uri"/> — the view never
/// navigates by itself, so the caller decides (e.g. <c>IBrowserLauncher</c>).
/// <code>&lt;md:MarkdownView Markdown="{Binding Description}" LinkCommand="{Binding OpenLinkCommand}" /&gt;</code>
/// </summary>
public sealed class MarkdownView : Border
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownView, string?>(nameof(Markdown));

    public static readonly StyledProperty<ICommand?> LinkCommandProperty =
        AvaloniaProperty.Register<MarkdownView, ICommand?>(nameof(LinkCommand));

    /// <summary>The Markdown source; re-rendered whenever it changes.</summary>
    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    /// <summary>Executed with the link's <see cref="Uri"/> (web and mail links only).</summary>
    public ICommand? LinkCommand
    {
        get => GetValue(LinkCommandProperty);
        set => SetValue(LinkCommandProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == MarkdownProperty)
        {
            Child = string.IsNullOrEmpty(Markdown) ? null : MarkdownRenderer.Render(Markdown, OpenLink);
        }
    }

    private void OpenLink(Uri uri)
    {
        if (LinkCommand is { } command && command.CanExecute(uri))
        {
            command.Execute(uri);
        }
    }
}
