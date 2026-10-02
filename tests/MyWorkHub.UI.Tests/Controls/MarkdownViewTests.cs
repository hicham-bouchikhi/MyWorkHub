using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using MyWorkHub.UI.Controls.Markdown;

namespace MyWorkHub.UI.Tests.Controls;

public sealed class MarkdownViewTests
{
    private sealed class RecordingCommand(bool canExecute = true) : ICommand
    {
        public List<object?> Executed { get; } = [];

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => canExecute;

        public void Execute(object? parameter) => Executed.Add(parameter);
    }

    private static Button LinkIn(MarkdownView view)
    {
        var paragraph = (SelectableTextBlock)((StackPanel)view.Child!).Children[0];
        return (Button)paragraph.Inlines!.OfType<InlineUIContainer>().Single().Child;
    }

    [Fact]
    public void Should_render_when_the_markdown_changes_and_clear_when_emptied()
    {
        var view = new MarkdownView { Markdown = "# One" };
        var first = view.Child;

        view.Markdown = "# Two";

        Assert.NotNull(view.Child);
        Assert.NotSame(first, view.Child);

        view.Markdown = "";

        Assert.Null(view.Child);
    }

    [Fact]
    public void Should_hand_a_clicked_link_to_the_command()
    {
        var command = new RecordingCommand();
        var view = new MarkdownView { LinkCommand = command, Markdown = "[go](https://example.com/a)" };

        LinkIn(view).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal([new Uri("https://example.com/a")], command.Executed);
    }

    [Fact]
    public void Should_not_execute_a_command_that_refuses_the_link()
    {
        var command = new RecordingCommand(canExecute: false);
        var view = new MarkdownView { LinkCommand = command, Markdown = "[go](https://example.com/a)" };

        LinkIn(view).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Empty(command.Executed);
    }
}
