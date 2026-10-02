using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdInline = Markdig.Syntax.Inlines.Inline;

namespace MyWorkHub.UI.Controls.Markdown;

/// <summary>
/// Turns untrusted Markdown into native Avalonia controls — no browser engine, no script, no network. Isolation
/// rules: raw HTML is never interpreted (it shows as literal text), images are never fetched (their alt text is
/// shown), links only reach <c>onLink</c> and only for http/https/mailto, and input length and nesting depth are
/// capped so a hostile document cannot exhaust the stack or the UI thread. Parsing is Markdig's (fast, CommonMark);
/// one pass over the AST builds the controls.
/// </summary>
internal sealed class MarkdownRenderer
{
    /// <summary>Longer input is truncated (with a notice) before parsing.</summary>
    internal const int MAX_LENGTH = 200_000;

    /// <summary>Blocks nested deeper than this (quotes, lists) are flattened to plain text.</summary>
    internal const int MAX_DEPTH = 16;

    internal const string TRUNCATED_NOTICE = "… (content truncated)";

    private const string MONOSPACE = "Cascadia Mono, Consolas, DejaVu Sans Mono, Liberation Mono, monospace";

    private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .UseTaskLists()
        .UseEmphasisExtras()
        .UseAutoLinks()
        .Build();

    private readonly Action<Uri> _onLink;

    private MarkdownRenderer(Action<Uri> onLink) => _onLink = onLink;

    /// <summary>Renders <paramref name="markdown"/> into a vertical stack of blocks.</summary>
    public static StackPanel Render(string? markdown, Action<Uri> onLink)
    {
        ArgumentNullException.ThrowIfNull(onLink);

        var text = markdown ?? "";
        var truncated = text.Length > MAX_LENGTH;
        if (truncated)
        {
            text = text[..MAX_LENGTH];
        }

        var root = NewStack();
        MarkdownDocument document;
        try
        {
            document = Markdig.Markdown.Parse(text, _pipeline);
        }
        catch (ArgumentException)
        {
            // Markdig refuses pathologically nested input (its own depth limit): show it as plain text.
            root.Children.Add(new SelectableTextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
            return root;
        }

        new MarkdownRenderer(onLink).AddBlocks(root, document, depth: 0);
        if (truncated)
        {
            root.Children.Add(new TextBlock { Text = TRUNCATED_NOTICE, Classes = { "markdownNotice" } });
        }

        return root;
    }

    /// <summary>Only web and mail links are ever handed out; anything else in untrusted content stays inert.</summary>
    internal static Uri? SafeLink(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto" ? uri : null;

    private static StackPanel NewStack() => new() { Spacing = 8, Classes = { "markdown" } };

    private void AddBlocks(Panel target, ContainerBlock container, int depth)
    {
        foreach (var block in container)
        {
            if (Render(block, depth) is { } control)
            {
                target.Children.Add(control);
            }
        }
    }

    private Control? Render(Block block, int depth)
    {
        if (depth > MAX_DEPTH)
        {
            return new SelectableTextBlock { Text = PlainText(block), TextWrapping = TextWrapping.Wrap };
        }

        return block switch
        {
            HeadingBlock heading => Paragraph(heading.Inline, "markdownH" + Math.Clamp(heading.Level, 1, 6)),
            ParagraphBlock paragraph => Paragraph(paragraph.Inline, null),
            CodeBlock code => CodeBlock(code),
            QuoteBlock quote => Quote(quote, depth),
            ListBlock list => List(list, depth),
            Table table => Table(table, depth),
            ThematicBreakBlock => new Separator { Classes = { "markdownRule" } },
            LinkReferenceDefinitionGroup => null,
            ContainerBlock other => Container(other, depth),
            LeafBlock leaf => Paragraph(leaf.Inline, null),
            _ => null,
        };
    }

    private SelectableTextBlock? Paragraph(ContainerInline? inline, string? styleClass)
    {
        if (inline is null)
        {
            return null;
        }

        var text = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap };
        if (styleClass is not null)
        {
            text.Classes.Add(styleClass);
        }

        text.Inlines ??= [];
        AddInlines(text.Inlines, inline, depth: 0);
        return text;
    }

    private static Border CodeBlock(CodeBlock code)
    {
        // Fenced or indented: shown verbatim, never highlighted or executed.
        var lines = code.Lines.ToString().TrimEnd('\n', '\r');
        return new Border
        {
            Classes = { "markdownCode" },
            Child = new SelectableTextBlock { Text = lines, FontFamily = new FontFamily(MONOSPACE), TextWrapping = TextWrapping.Wrap },
        };
    }

    private Border Quote(QuoteBlock quote, int depth)
    {
        var content = NewStack();
        AddBlocks(content, quote, depth + 1);
        return new Border { Classes = { "markdownQuote" }, Child = content };
    }

    private Grid List(ListBlock list, int depth)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Classes = { "markdownList" } };
        var number = list.IsOrdered && int.TryParse(list.OrderedStart, out var start) ? start : 1;
        var row = 0;
        foreach (var item in list.OfType<ListItemBlock>())
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var marker = new TextBlock
            {
                Text = list.IsOrdered ? $"{number++}{list.OrderedDelimiter}" : "•",
                Classes = { "markdownBullet" },
            };
            Grid.SetRow(marker, row);
            grid.Children.Add(marker);

            var content = NewStack();
            content.Spacing = 4;
            AddBlocks(content, item, depth + 1);
            Grid.SetRow(content, row);
            Grid.SetColumn(content, 1);
            grid.Children.Add(content);
            row++;
        }

        return grid;
    }

    private Border Table(Table table, int depth)
    {
        var grid = new Grid { Classes = { "markdownTable" } };
        var columns = table.OfType<TableRow>().Select(r => r.Count).DefaultIfEmpty(0).Max();
        for (var c = 0; c < columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        }

        var rowIndex = 0;
        foreach (var row in table.OfType<TableRow>())
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var columnIndex = 0;
            foreach (var cell in row.OfType<TableCell>())
            {
                var content = NewStack();
                content.Spacing = 2;
                AddBlocks(content, cell, depth + 1);
                var border = new Border { Classes = { row.IsHeader ? "markdownHeaderCell" : "markdownCell" }, Child = content };
                Grid.SetRow(border, rowIndex);
                Grid.SetColumn(border, columnIndex++);
                grid.Children.Add(border);
            }

            rowIndex++;
        }

        // Wide tables scroll sideways instead of squeezing the page.
        return new Border { Child = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = grid } };
    }

    private StackPanel Container(ContainerBlock container, int depth)
    {
        var content = NewStack();
        AddBlocks(content, container, depth + 1);
        return content;
    }

    private void AddInlines(InlineCollection target, ContainerInline container, int depth)
    {
        foreach (var inline in container)
        {
            if (Render(inline, depth) is { } rendered)
            {
                target.Add(rendered);
            }
        }
    }

    private Avalonia.Controls.Documents.Inline? Render(MdInline inline, int depth)
    {
        if (depth > MAX_DEPTH)
        {
            return new Run(PlainText(inline));
        }

        switch (inline)
        {
            case LiteralInline literal:
                return new Run(literal.Content.ToString());
            case CodeInline code:
                return new Run(code.Content) { FontFamily = new FontFamily(MONOSPACE), Classes = { "markdownInlineCode" } };
            case LineBreakInline lineBreak:
                return lineBreak.IsHard ? new LineBreak() : new Run(" ");
            case HtmlEntityInline entity:
                return new Run(entity.Transcoded.ToString());
            case HtmlInline html:
                // Only reached if HTML parsing is ever re-enabled: still literal text, never markup.
                return new Run(html.Tag);
            case TaskList task:
                return new Run(task.Checked ? "☑" : "☐");
            case AutolinkInline autolink:
                return Link(autolink.IsEmail ? "mailto:" + autolink.Url : autolink.Url, autolink.Url);
            case LinkInline { IsImage: true } image:
                // Never fetched: a remote image is how a sender learns the content was opened.
                return new Run("🖼 " + PlainText(image)) { Classes = { "markdownImage" } };
            case LinkInline link:
                return Link(link.Url, PlainText(link));
            case EmphasisInline emphasis:
                return Emphasis(emphasis, depth);
            case ContainerInline other:
                var span = new Span();
                AddInlines(span.Inlines, other, depth + 1);
                return span;
            default:
                return new Run(inline.ToString());
        }
    }

    private Span Emphasis(EmphasisInline emphasis, int depth)
    {
        Span span = (emphasis.DelimiterChar, emphasis.DelimiterCount) switch
        {
            ('~', 2) => new Span { TextDecorations = TextDecorations.Strikethrough },
            (_, >= 2) => new Bold(),
            _ => new Italic(),
        };
        AddInlines(span.Inlines, emphasis, depth + 1);
        return span;
    }

    private Avalonia.Controls.Documents.Inline Link(string? url, string label)
    {
        var text = string.IsNullOrWhiteSpace(label) ? url ?? "" : label;
        if (SafeLink(url) is not { } uri)
        {
            return new Run(text);
        }

        var button = new Button
        {
            Content = text,
            Classes = { "markdownLink" },
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        ToolTip.SetTip(button, uri.AbsoluteUri);
        button.Click += (_, _) => _onLink(uri);
        return new InlineUIContainer(button);
    }

    private static string PlainText(MarkdownObject node)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var descendant in node.Descendants())
        {
            switch (descendant)
            {
                case LiteralInline literal:
                    builder.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    builder.Append(code.Content);
                    break;
                case LineBreakInline:
                    builder.Append(' ');
                    break;
                case LeafBlock { Inline: null } leaf when leaf.Lines.Count > 0:
                    builder.Append(leaf.Lines.ToString()).Append(' ');
                    break;
            }
        }

        return builder.ToString().Trim();
    }
}
