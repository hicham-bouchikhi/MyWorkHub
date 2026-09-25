using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Threading;

namespace MyWorkHub.UI.Behaviors;

/// <summary>
/// Shared deep-link behavior for any <see cref="ListBox"/>: bind <c>ScrollIntoViewBehavior.Item</c> to a
/// page view model's "highlighted item" property. Whenever it becomes non-null the list scrolls that
/// item into view, flashes its row (the <c>deepLinkHighlight</c> style class on its
/// <see cref="ListBoxItem"/>, styled in <c>AppStyles.axaml</c>) for <see cref="HighlightDuration"/>, then writes the property back to null.
/// Consuming the value that way means each request fires exactly once: a repeat request for the same
/// item still fires, and a recreated view does not re-flash a stale request.
/// <code>
/// &lt;ListBox ItemsSource="{Binding Items}"
///          SelectedItem="{Binding SelectedItem}"
///          behaviors:ScrollIntoViewBehavior.Item="{Binding HighlightedItem}" /&gt;
/// </code>
/// </summary>
public sealed class ScrollIntoViewBehavior
{
    // Style class set on the revealed item's container while it is highlighted (AppStyles.axaml).
    private const string HIGHLIGHT_CLASS = "deepLinkHighlight";

    /// <summary>How long the revealed row stays highlighted.</summary>
    public static readonly TimeSpan HighlightDuration = TimeSpan.FromSeconds(1.5);

    /// <summary>The item to reveal; two-way by default so the behavior can reset it to null once handled.</summary>
    public static readonly AttachedProperty<object?> ItemProperty =
        AvaloniaProperty.RegisterAttached<ScrollIntoViewBehavior, ListBox, object?>(
            "Item", defaultBindingMode: BindingMode.TwoWay);

    static ScrollIntoViewBehavior()
    {
        ItemProperty.Changed.AddClassHandler<ListBox>(OnItemChanged);
    }

    private ScrollIntoViewBehavior()
    {
    }

    public static object? GetItem(ListBox listBox)
    {
        ArgumentNullException.ThrowIfNull(listBox);
        return listBox.GetValue(ItemProperty);
    }

    public static void SetItem(ListBox listBox, object? value)
    {
        ArgumentNullException.ThrowIfNull(listBox);
        listBox.SetValue(ItemProperty, value);
    }

    private static void OnItemChanged(ListBox listBox, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is not { } item)
        {
            return;
        }

        // Deferred to after layout: on a first visit the request arrives while the list is still being
        // populated and its item containers do not exist yet.
        Dispatcher.UIThread.Post(() => Reveal(listBox, item), DispatcherPriority.Loaded);
    }

    private static void Reveal(ListBox listBox, object item)
    {
        // A newer request (or a reset) superseded this one while it was queued.
        if (!ReferenceEquals(listBox.GetValue(ItemProperty), item))
        {
            return;
        }

        listBox.ScrollIntoView(item);
        if (listBox.ContainerFromItem(item) is { } container)
        {
            container.Classes.Add(HIGHLIGHT_CLASS);
            DispatcherTimer.RunOnce(() => container.Classes.Remove(HIGHLIGHT_CLASS), HighlightDuration);
        }

        listBox.SetCurrentValue(ItemProperty, null);
    }
}
