using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Media;
using MyWorkHub.UI.Controls.Markdown;

namespace MyWorkHub.UI.Tests.Controls;

public sealed class MarkdownRendererTests
{
    private readonly List<Uri> _opened = [];

    private StackPanel Render(string markdown) => MarkdownRenderer.Render(markdown, _opened.Add);

    // --- Tree helpers ----------------------------------------------------------------------

    private static IEnumerable<Control> Descendants(Control control)
    {
        yield return control;
        IEnumerable<Control> children = control switch
        {
            Panel panel => panel.Children,
            Border { Child: { } child } => [child],
            ScrollViewer { Content: Control content } => [content],
            _ => [],
        };
        foreach (var child in children.SelectMany(Descendants))
        {
            yield return child;
        }

        if (control is SelectableTextBlock { Inlines: { } inlines })
        {
            foreach (var hosted in inlines.SelectMany(InlinesOf).OfType<InlineUIContainer>())
            {
                foreach (var nested in Descendants(hosted.Child))
                {
                    yield return nested;
                }
            }
        }
    }

    private static IEnumerable<Inline> InlinesOf(Inline inline)
    {
        yield return inline;
        if (inline is Span span)
        {
            foreach (var child in span.Inlines.SelectMany(InlinesOf))
            {
                yield return child;
            }
        }
    }

    private static string TextOf(SelectableTextBlock block)
    {
        if (block.Inlines is not { Count: > 0 } inlines)
        {
            return block.Text ?? "";
        }

        var builder = new StringBuilder();
        foreach (var inline in inlines.SelectMany(InlinesOf))
        {
            switch (inline)
            {
                case Run run:
                    builder.Append(run.Text);
                    break;
                case LineBreak:
                    builder.Append('\n');
                    break;
                case InlineUIContainer { Child: Button { Content: string label } }:
                    builder.Append(label);
                    break;
            }
        }

        return builder.ToString();
    }

    private static List<SelectableTextBlock> Texts(Control root) => [.. Descendants(root).OfType<SelectableTextBlock>()];

    private static List<Button> Links(Control root) => [.. Descendants(root).OfType<Button>()];

    // --- Blocks ----------------------------------------------------------------------------

    [Fact]
    public void Should_render_headings_with_a_class_per_level()
    {
        var texts = Texts(Render("# Title\n\n### Sub"));

        Assert.Equal(["Title", "Sub"], texts.Select(TextOf));
        Assert.Contains("markdownH1", texts[0].Classes);
        Assert.Contains("markdownH3", texts[1].Classes);
    }

    [Fact]
    public void Should_render_bold_italic_strikethrough_and_inline_code()
    {
        var paragraph = Assert.Single(Texts(Render("**b** *i* ~~s~~ `c`")));

        var inlines = paragraph.Inlines!.SelectMany(InlinesOf).ToList();
        Assert.Contains(inlines, i => i is Bold);
        Assert.Contains(inlines, i => i is Italic);
        Assert.Contains(inlines, i => i is Span { TextDecorations: { } decorations } && decorations == TextDecorations.Strikethrough);
        Assert.Contains(inlines, i => i is Run { Text: "c" } run && run.Classes.Contains("markdownInlineCode"));
    }

    [Fact]
    public void Should_render_lists_with_bullets_numbers_and_task_boxes()
    {
        var root = Render("1. one\n2. two\n\n- [x] done\n- [ ] todo");

        var markers = Descendants(root).OfType<TextBlock>().Where(t => t.Classes.Contains("markdownBullet")).Select(t => t.Text);
        Assert.Equal(["1.", "2.", "•", "•"], markers);
        Assert.Contains("☑ done", Texts(root).Select(TextOf));
        Assert.Contains("☐ todo", Texts(root).Select(TextOf));
    }

    [Fact]
    public void Should_render_fenced_code_verbatim_in_a_code_block()
    {
        var root = Render("```csharp\nvar x = \"<b>\";\n```");

        var code = Assert.Single(Descendants(root).OfType<Border>(), b => b.Classes.Contains("markdownCode"));
        Assert.Equal("var x = \"<b>\";", ((SelectableTextBlock)code.Child!).Text);
    }

    [Fact]
    public void Should_render_pipe_tables_as_a_grid_with_header_cells()
    {
        var root = Render("| A | B |\n|---|---|\n| 1 | 2 |");

        var grid = Assert.Single(Descendants(root).OfType<Grid>(), g => g.Classes.Contains("markdownTable"));
        Assert.Equal(2, grid.ColumnDefinitions.Count);
        Assert.Equal(2, grid.RowDefinitions.Count);
        Assert.Equal(2, grid.Children.OfType<Border>().Count(b => b.Classes.Contains("markdownHeaderCell")));
        Assert.Equal(["A", "B", "1", "2"], Texts(grid).Select(TextOf));
    }

    [Fact]
    public void Should_render_quotes_and_rules()
    {
        var root = Render("> quoted\n\n---");

        Assert.Single(Descendants(root).OfType<Border>(), b => b.Classes.Contains("markdownQuote"));
        Assert.Single(Descendants(root).OfType<Separator>());
    }

    // --- Isolation -------------------------------------------------------------------------

    [Fact]
    public void Should_show_raw_html_as_literal_text_and_never_interpret_it()
    {
        var root = Render("<script>alert(1)</script>\n\nHi <b onclick=x>there</b>");

        var text = string.Join("\n", Texts(root).Select(TextOf));
        Assert.Contains("<script>alert(1)</script>", text, StringComparison.Ordinal);
        Assert.Contains("<b onclick=x>there</b>", text, StringComparison.Ordinal);
        Assert.DoesNotContain(Texts(root).SelectMany(t => t.Inlines!).SelectMany(InlinesOf), i => i is Bold);
    }

    [Fact]
    public void Should_never_load_images_and_show_their_alt_text_instead()
    {
        var root = Render("![tracking pixel](https://evil.example/p.png)");

        Assert.DoesNotContain(Descendants(root), c => c is Image);
        Assert.Empty(Links(root));
        Assert.Equal("🖼 tracking pixel", TextOf(Assert.Single(Texts(root))));
    }

    [Fact]
    public void Should_raise_web_links_without_navigating_by_itself()
    {
        var root = Render("[the PR](https://dev.azure.com/cegid/Retail/_git/x/pullrequest/1) and <https://example.com>");

        var links = Links(root);
        Assert.Equal(["the PR", "https://example.com"], links.Select(l => l.Content));
        links[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal([new Uri("https://dev.azure.com/cegid/Retail/_git/x/pullrequest/1")], _opened);
    }

    [Theory]
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("[x](file:///etc/passwd)")]
    [InlineData("[x](vscode://open)")]
    [InlineData("[x](relative/path)")]
    public void Should_keep_non_web_links_inert(string markdown)
    {
        var root = Render(markdown);

        Assert.Empty(Links(root));
        Assert.Equal("x", TextOf(Assert.Single(Texts(root))));
    }

    [Fact]
    public void Should_flatten_blocks_nested_deeper_than_the_limit()
    {
        var root = Render(new string('>', 200) + " deep");

        var quoteDepth = Descendants(root).OfType<Border>().Count(b => b.Classes.Contains("markdownQuote"));
        Assert.True(quoteDepth <= MarkdownRenderer.MAX_DEPTH + 1, $"{quoteDepth} nested quotes");
        Assert.Contains(Texts(root), t => TextOf(t).Contains("deep", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_fall_back_to_plain_text_when_the_parser_refuses_the_nesting()
    {
        var hostile = new string('[', 20_000) + "x";

        var text = Assert.Single(Texts(Render(hostile)));

        Assert.Equal(hostile, TextOf(text));
    }

    [Fact]
    public void Should_truncate_oversized_input_with_a_notice()
    {
        var root = Render(new string('a', MarkdownRenderer.MAX_LENGTH + 10));

        Assert.Equal(MarkdownRenderer.TRUNCATED_NOTICE, Assert.IsType<TextBlock>(root.Children[^1]).Text);
        Assert.Equal(MarkdownRenderer.MAX_LENGTH, TextOf(Assert.Single(Texts(root))).Length);
    }

    [Fact]
    public void Should_render_nothing_for_empty_input()
    {
        Assert.Empty(Render("").Children);
    }
}
