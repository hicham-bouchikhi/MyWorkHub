using System.Globalization;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace MyWorkHub.UI.Controls;

public partial class MarkdownEditorControl : UserControl
{
    private enum ViewMode { Split, Editor, Preview }

    private bool _webViewReady;
    private string? _previewOriginatedText;
    private ViewMode _viewMode = ViewMode.Split;

    public MarkdownEditorControl()
    {
        InitializeComponent();
        EditorTextBox.Text = DefaultMarkdown;
        EditorTextBox.TextChanged += OnEditorTextChanged;
        // Tunnel so we see Ctrl+E before the TextBox consumes it.
        AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel);
        SetViewMode(ViewMode.Split);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        PreviewWebView.NavigationCompleted += OnNavigationCompleted;
        PreviewWebView.WebMessageReceived += OnWebMessageReceived;
        PreviewWebView.NavigateToString(HtmlTemplate);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        PreviewWebView.NavigationCompleted -= OnNavigationCompleted;
        PreviewWebView.WebMessageReceived -= OnWebMessageReceived;
        base.OnDetachedFromVisualTree(e);
    }

    // ── View mode ─────────────────────────────────────────────────────────

    private void OnToggleModeClick(object? sender, RoutedEventArgs e) => CycleViewMode();

    private void OnTunnelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.E && e.KeyModifiers == KeyModifiers.Control)
        {
            CycleViewMode();
            e.Handled = true;
        }
    }

    private void CycleViewMode() => SetViewMode(_viewMode switch
    {
        ViewMode.Split => ViewMode.Editor,
        ViewMode.Editor => ViewMode.Preview,
        _ => ViewMode.Split
    });

    private void SetViewMode(ViewMode mode)
    {
        _viewMode = mode;
        bool showEditor = mode != ViewMode.Preview;
        bool showPreview = mode != ViewMode.Editor;
        bool split = mode == ViewMode.Split;

        EditorTextBox.IsVisible = showEditor;
        PreviewSplitter.IsVisible = split;
        PreviewWebView.IsVisible = showPreview;
        FormatToolbar.IsVisible = showEditor;

        var cols = ContentGrid.ColumnDefinitions;
        cols[0].Width = showEditor ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        cols[1].Width = split ? GridLength.Auto : new GridLength(0);
        cols[2].Width = showPreview ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        ModeToggleLabel.Text = mode switch
        {
            ViewMode.Split => "◧  Split",
            ViewMode.Editor => "✏  Editor",
            _ => "👁  Preview"
        };

        if (showPreview) _ = UpdatePreviewAsync();
    }

    // ── Format ────────────────────────────────────────────────────────────

    private void OnFormatClick(object? sender, RoutedEventArgs e)
    {
        var formatted = FormatMarkdown(EditorTextBox.Text ?? string.Empty);
        if (formatted != EditorTextBox.Text)
            EditorTextBox.Text = formatted; // triggers preview refresh via TextChanged
    }

    // ── WebView events ────────────────────────────────────────────────────

    private async void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess) return;
        _webViewReady = true;
        await UpdatePreviewAsync();
    }

    private void OnWebMessageReceived(object? sender, WebMessageReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Body)) return;
        try
        {
            using var doc = JsonDocument.Parse(e.Body);
            var root = doc.RootElement;
            if (root.GetProperty("type").GetString() == "md")
                SetEditorTextFromPreview(root.GetProperty("val").GetString() ?? string.Empty);
        }
        catch (JsonException) { }
        catch (InvalidOperationException) { }
        catch (KeyNotFoundException) { }
    }

    /// <summary>
    /// Applies markdown produced by editing the rendered preview. We suppress the
    /// round-trip re-render so the caret/selection in the WebView is not disturbed.
    /// </summary>
    private void SetEditorTextFromPreview(string markdown)
    {
        if (markdown == EditorTextBox.Text) return;
        _previewOriginatedText = markdown;
        EditorTextBox.Text = markdown;
    }

    // ── Editor events ─────────────────────────────────────────────────────

    private async void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        // If this change originated from an edit in the preview, don't re-render —
        // rebuilding innerHTML would reset the caret to the top. Compare content
        // rather than a flag, because TextChanged is raised asynchronously.
        if (_previewOriginatedText is not null &&
            (EditorTextBox.Text ?? string.Empty) == _previewOriginatedText)
        {
            _previewOriginatedText = null;
            return;
        }
        _previewOriginatedText = null;
        await UpdatePreviewAsync();
    }

    private async System.Threading.Tasks.Task UpdatePreviewAsync()
    {
        if (!_webViewReady) return;
        var escaped = JsonSerializer.Serialize(EditorTextBox.Text ?? string.Empty);
        await PreviewWebView.InvokeScript($"update({escaped})");
    }

    // ── Toolbar: headings ─────────────────────────────────────────────────

    private void OnH1Click(object? sender, RoutedEventArgs e) => InsertAtLineStart("# ");
    private void OnH2Click(object? sender, RoutedEventArgs e) => InsertAtLineStart("## ");
    private void OnH3Click(object? sender, RoutedEventArgs e) => InsertAtLineStart("### ");

    // ── Toolbar: inline ───────────────────────────────────────────────────

    private void OnBoldClick(object? sender, RoutedEventArgs e) => InsertAround("**", "**");
    private void OnItalicClick(object? sender, RoutedEventArgs e) => InsertAround("*", "*");
    private void OnStrikeClick(object? sender, RoutedEventArgs e) => InsertAround("~~", "~~");
    private void OnCodeClick(object? sender, RoutedEventArgs e) => InsertAround("`", "`");

    // ── Toolbar: blocks ───────────────────────────────────────────────────

    private void OnQuoteClick(object? sender, RoutedEventArgs e) => InsertAtLineStart("> ");

    private void OnCodeBlockClick(object? sender, RoutedEventArgs e)
    {
        var tb = EditorTextBox;
        string text = tb.Text ?? string.Empty;
        int start = tb.SelectionStart;
        int end = tb.SelectionEnd;
        string selected = end > start ? text[start..end] : string.Empty;
        tb.Text = text.Remove(start, end - start).Insert(start, "```\n" + selected + "\n```");
        tb.SelectionStart = start + 4;
        tb.SelectionEnd = start + 4 + selected.Length;
        tb.Focus();
    }

    private void OnHRClick(object? sender, RoutedEventArgs e)
    {
        var tb = EditorTextBox;
        string text = tb.Text ?? string.Empty;
        int pos = tb.SelectionEnd;
        const string hr = "\n\n---\n\n";
        tb.Text = text.Insert(pos, hr);
        tb.SelectionStart = pos + hr.Length;
        tb.SelectionEnd = pos + hr.Length;
        tb.Focus();
    }

    // ── Toolbar: lists ────────────────────────────────────────────────────

    private void OnULClick(object? sender, RoutedEventArgs e) => InsertAtLineStart("- ");
    private void OnOLClick(object? sender, RoutedEventArgs e) => InsertAtLineStart("1. ");

    // ── Toolbar: links ────────────────────────────────────────────────────

    private void OnLinkClick(object? sender, RoutedEventArgs e)
    {
        var tb = EditorTextBox;
        string text = tb.Text ?? string.Empty;
        int start = tb.SelectionStart;
        int end = tb.SelectionEnd;
        string selected = end > start ? text[start..end] : "link text";
        tb.Text = text.Remove(start, end - start).Insert(start, $"[{selected}](url)");
        int urlStart = start + selected.Length + 3;
        tb.SelectionStart = urlStart;
        tb.SelectionEnd = urlStart + 3;
        tb.Focus();
    }

    private void OnImageClick(object? sender, RoutedEventArgs e)
    {
        var tb = EditorTextBox;
        string text = tb.Text ?? string.Empty;
        int start = tb.SelectionStart;
        int end = tb.SelectionEnd;
        string selected = end > start ? text[start..end] : "alt text";
        tb.Text = text.Remove(start, end - start).Insert(start, $"![{selected}](url)");
        int urlStart = start + selected.Length + 4;
        tb.SelectionStart = urlStart;
        tb.SelectionEnd = urlStart + 3;
        tb.Focus();
    }

    // ── Toolbar: table wizard ─────────────────────────────────────────────

    private void OnInsertTableClick(object? sender, RoutedEventArgs e)
    {
        int dataRows = (int)(TableRowCount.Value ?? 3m);
        int cols = (int)(TableColCount.Value ?? 3m);
        string alignment = TableAlignment.SelectedIndex switch
        {
            1 => "left",
            2 => "center",
            3 => "right",
            _ => "none"
        };
        InsertAtCursor(BuildTable(dataRows, cols, alignment));
        BtnTable.Flyout?.Hide();
    }

    // ── Text helpers ──────────────────────────────────────────────────────

    private void InsertAround(string prefix, string suffix)
    {
        var tb = EditorTextBox;
        string text = tb.Text ?? string.Empty;
        int start = tb.SelectionStart;
        int end = tb.SelectionEnd;
        string selected = end > start ? text[start..end] : "text";
        tb.Text = text.Remove(start, end - start).Insert(start, prefix + selected + suffix);
        tb.SelectionStart = start + prefix.Length;
        tb.SelectionEnd = start + prefix.Length + selected.Length;
        tb.Focus();
    }

    private void InsertAtLineStart(string prefix)
    {
        var tb = EditorTextBox;
        string text = tb.Text ?? string.Empty;
        int pos = tb.SelectionStart;
        int lineStart = text.LastIndexOf('\n', pos > 0 ? pos - 1 : 0) + 1;
        tb.Text = text.Insert(lineStart, prefix);
        tb.SelectionStart = pos + prefix.Length;
        tb.SelectionEnd = pos + prefix.Length;
        tb.Focus();
    }

    private void InsertAtCursor(string content)
    {
        var tb = EditorTextBox;
        string text = tb.Text ?? string.Empty;
        int pos = tb.SelectionEnd;
        tb.Text = text.Insert(pos, content);
        tb.SelectionStart = pos + content.Length;
        tb.SelectionEnd = pos + content.Length;
        tb.Focus();
    }

    private static string BuildTable(int dataRows, int cols, string alignment)
    {
        string sep = alignment switch
        {
            "left"   => " :---------- |",
            "center" => " :----------: |",
            "right"  => " ----------: |",
            _        => " ----------- |"
        };

        var sb = new StringBuilder("\n");

        sb.Append('|');
        for (int c = 1; c <= cols; c++)
            sb.Append(string.Create(CultureInfo.InvariantCulture, $" Column {c}   |"));
        sb.AppendLine();

        sb.Append('|');
        for (int c = 0; c < cols; c++) sb.Append(sep);
        sb.AppendLine();

        for (int r = 0; r < dataRows; r++)
        {
            sb.Append('|');
            for (int c = 0; c < cols; c++) sb.Append("             |");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ── Markdown formatter ────────────────────────────────────────────────

    private static string FormatMarkdown(string md)
    {
        var lines = md.Replace("\r\n", "\n", StringComparison.Ordinal)
                      .Replace('\r', '\n')
                      .Split('\n');

        var outLines = new List<string>();
        int i = 0;
        while (i < lines.Length)
        {
            // A table = a '|' row immediately followed by a separator row
            if (IsTableRow(lines[i]) && i + 1 < lines.Length && IsSeparatorRow(lines[i + 1]))
            {
                var block = new List<string>();
                while (i < lines.Length && IsTableRow(lines[i]))
                {
                    block.Add(lines[i]);
                    i++;
                }
                outLines.AddRange(FormatTable(block));
            }
            else
            {
                outLines.Add(lines[i].TrimEnd());
                i++;
            }
        }

        // Collapse runs of blank lines to a single blank line
        var collapsed = new List<string>();
        int blanks = 0;
        foreach (var line in outLines)
        {
            if (line.Length == 0)
            {
                blanks++;
                if (blanks <= 1) collapsed.Add(line);
            }
            else
            {
                blanks = 0;
                collapsed.Add(line);
            }
        }

        while (collapsed.Count > 0 && collapsed[0].Length == 0) collapsed.RemoveAt(0);
        while (collapsed.Count > 0 && collapsed[^1].Length == 0) collapsed.RemoveAt(collapsed.Count - 1);

        return string.Join("\n", collapsed);
    }

    private static List<string> FormatTable(List<string> block)
    {
        var rows = new List<string[]>(block.Count);
        foreach (var line in block) rows.Add(SplitRow(line));

        int cols = 0;
        foreach (var r in rows) cols = Math.Max(cols, r.Length);

        // Normalise ragged rows to the same column count, and trim every cell
        for (int r = 0; r < rows.Count; r++)
        {
            var cells = new string[cols];
            for (int c = 0; c < cols; c++)
                cells[c] = c < rows[r].Length ? rows[r][c].Trim() : string.Empty;
            rows[r] = cells;
        }

        // Alignment comes from the separator row (index 1)
        var align = new char[cols];
        for (int c = 0; c < cols; c++)
        {
            string s = rows.Count > 1 ? rows[1][c] : string.Empty;
            bool left = s.Length > 0 && s[0] == ':';
            bool right = s.Length > 0 && s[^1] == ':';
            align[c] = left && right ? 'c' : right ? 'r' : left ? 'l' : 'n';
        }

        // Column widths from content rows (separator excluded), min 3 for the dashes
        var width = new int[cols];
        for (int c = 0; c < cols; c++)
        {
            int w = 3;
            for (int r = 0; r < rows.Count; r++)
            {
                if (r == 1) continue;
                w = Math.Max(w, rows[r][c].Length);
            }
            width[c] = w;
        }

        var result = new List<string>(rows.Count);
        for (int r = 0; r < rows.Count; r++)
        {
            var sb = new StringBuilder("|");
            for (int c = 0; c < cols; c++)
            {
                sb.Append(' ');
                sb.Append(r == 1 ? BuildSeparator(width[c], align[c]) : PadCell(rows[r][c], width[c], align[c]));
                sb.Append(" |");
            }
            result.Add(sb.ToString());
        }
        return result;
    }

    private static bool IsTableRow(string line)
    {
        var t = line.TrimStart();
        return t.Length > 0 && t[0] == '|';
    }

    private static bool IsSeparatorRow(string line)
    {
        var cells = SplitRow(line);
        if (cells.Length == 0) return false;
        foreach (var cell in cells)
        {
            var s = cell.Trim();
            if (s.Length == 0) return false;
            bool hasDash = false;
            foreach (char ch in s)
            {
                if (ch == '-') hasDash = true;
                else if (ch != ':') return false;
            }
            if (!hasDash) return false;
        }
        return true;
    }

    private static string[] SplitRow(string line)
    {
        var t = line.Trim();
        if (t.Length > 0 && t[0] == '|') t = t[1..];
        if (t.Length > 0 && t[^1] == '|') t = t[..^1];
        return t.Split('|');
    }

    private static string BuildSeparator(int width, char align) => align switch
    {
        'c' => ":" + new string('-', Math.Max(1, width - 2)) + ":",
        'r' => new string('-', Math.Max(1, width - 1)) + ":",
        'l' => ":" + new string('-', Math.Max(1, width - 1)),
        _   => new string('-', width)
    };

    private static string PadCell(string text, int width, char align)
    {
        if (text.Length >= width) return text;
        int total = width - text.Length;
        return align switch
        {
            'r' => new string(' ', total) + text,
            'c' => new string(' ', total / 2) + text + new string(' ', total - total / 2),
            _   => text + new string(' ', total)
        };
    }

    // ── Default content ───────────────────────────────────────────────────

    private const string DefaultMarkdown = """
        # Markdown Editor

        Editor and preview show **side by side**. Type in either one — edits in the
        preview sync back to the source. Press **Ctrl+E** (or the top-right button)
        to cycle **Split → Editor → Preview**. **🧹 Format** tidies the source and
        aligns tables.

        ## Editable Preview

        Click anywhere in the preview and just type. In tables, use Tab / Enter to
        move between cells:

        | Feature | Status | Notes |
        | :--- | :---: | :--- |
        | Live preview | ✅ | Renders as you type |
        | Split / full modes | ✅ | Ctrl+E to cycle |
        | Format button | ✅ | Aligns table columns |
        | Edit in preview | ✅ | Two-way sync |

        ## Code

        ```csharp
        var msg = "Hello from MarkdownEditorControl!";
        Console.WriteLine(msg);
        ```

        ## Lists

        - Item one
        - Item two
          - Nested

        1. First
        2. Second

        > **Tip:** In the editor, select text first then click a toolbar button to wrap it.
        """;

    // ── Preview HTML ──────────────────────────────────────────────────────

    private const string HtmlTemplate = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <script src="https://cdn.jsdelivr.net/npm/marked@9/marked.min.js"></script>
        <script src="https://cdn.jsdelivr.net/npm/turndown@7/dist/turndown.js"></script>
        <script src="https://cdn.jsdelivr.net/npm/turndown-plugin-gfm@1/dist/turndown-plugin-gfm.js"></script>
        <style>
          :root{--bg:#1e1e2e;--fg:#cdd6f4;--muted:#a6adc8;--code-bg:#313244;--border:#45475a;--link:#89b4fa;--accent:#cba6f7;--sel:#4a9eff}
          *{box-sizing:border-box;margin:0;padding:0}
          html,body{height:100%}
          body{font-family:'Segoe UI',system-ui,-apple-system,sans-serif;background:var(--bg);color:var(--fg);padding:20px 24px;line-height:1.65;font-size:14px}
          h1,h2,h3,h4,h5,h6{color:var(--accent);margin:1.2em 0 .4em;font-weight:600;line-height:1.3}
          h1{font-size:1.8em;border-bottom:1px solid var(--border);padding-bottom:.3em}
          h2{font-size:1.4em;border-bottom:1px solid var(--border);padding-bottom:.2em}
          h3{font-size:1.15em} h4{font-size:1em}
          p{margin:.75em 0}
          a{color:var(--link);text-decoration:none} a:hover{text-decoration:underline}
          code{font-family:'Cascadia Code','Consolas','Courier New',monospace;background:var(--code-bg);border-radius:4px;padding:2px 5px;font-size:.88em}
          pre{background:var(--code-bg);border-radius:8px;padding:14px 16px;overflow-x:auto;margin:.8em 0;border:1px solid var(--border)}
          pre code{background:none;padding:0;font-size:.9em}
          blockquote{border-left:3px solid var(--accent);padding-left:14px;margin:.8em 0;color:var(--muted)}
          /* ── Excel-style tables ── */
          table{border-collapse:collapse;width:100%;margin:.8em 0;font-size:.95em}
          th{background:var(--code-bg);color:var(--accent);font-weight:600;text-align:left;padding:7px 10px;border:1px solid var(--border);cursor:cell;min-width:60px}
          td{padding:6px 10px;border:1px solid var(--border);cursor:cell;min-width:60px}
          tr:nth-child(even) td{background:rgba(255,255,255,.02)}
          th:focus,td:focus{outline:2px solid var(--sel)!important;outline-offset:-1px;background:rgba(74,158,255,.08)!important;position:relative;z-index:1}
          th:empty::after,td:empty::after{content:'\00a0'}
          ul,ol{padding-left:1.8em;margin:.5em 0} li{margin:.25em 0}
          hr{border:none;border-top:1px solid var(--border);margin:1.4em 0}
          img{max-width:100%;border-radius:6px;margin:.5em 0}
          strong{color:var(--fg);font-weight:600} em{font-style:italic;color:var(--muted)} del{color:var(--muted)}
          /* Whole preview is editable */
          #content{min-height:100%;outline:none}
          #content:focus{outline:none}
        </style>
        </head>
        <body>
        <div id="content" contenteditable="true" spellcheck="false"></div>
        <script>
        function notify(msg){
          if(typeof invokeCSharpAction!=='undefined'){invokeCSharpAction(msg)}
          else if(window.chrome&&window.chrome.webview){window.chrome.webview.postMessage(msg)}
        }

        // ── HTML → Markdown (edits made in the preview) ──
        var _td=null;
        if(typeof TurndownService!=='undefined'){
          _td=new TurndownService({headingStyle:'atx',codeBlockStyle:'fenced',bulletListMarker:'-',emDelimiter:'*'});
          if(typeof turndownPluginGfm!=='undefined'){_td.use(turndownPluginGfm.gfm);}
        }

        var _syncTimer=null;
        function scheduleSync(){
          if(!_td)return;
          if(_syncTimer)clearTimeout(_syncTimer);
          _syncTimer=setTimeout(function(){
            var md=_td.turndown(document.getElementById('content').innerHTML);
            notify(JSON.stringify({type:'md',val:md}));
          },350);
        }

        // ── Table cell navigation (Tab / Enter / Esc) ──
        function initTables(){
          document.querySelectorAll('table').forEach(function(tbl){
            var rows=tbl.querySelectorAll('tr');
            rows.forEach(function(row){
              row.querySelectorAll('th,td').forEach(function(cell){
                cell.tabIndex=0;
                cell.addEventListener('keydown',function(ev){
                  var allCells=Array.from(tbl.querySelectorAll('th,td'));
                  var idx=allCells.indexOf(cell);
                  if(ev.key==='Tab'){
                    ev.preventDefault();
                    var nx=allCells[idx+(ev.shiftKey?-1:1)];
                    if(nx){nx.focus();selectAll(nx);}
                  } else if(ev.key==='Enter'&&!ev.shiftKey){
                    ev.preventDefault();
                    var colCount=rows[0].querySelectorAll('th,td').length;
                    var nx=allCells[idx+colCount];
                    if(nx){nx.focus();selectAll(nx);}
                  } else if(ev.key==='Escape'){
                    cell.blur();
                  }
                });
              });
            });
          });
        }

        function selectAll(el){
          var r=document.createRange();r.selectNodeContents(el);
          var s=window.getSelection();s.removeAllRanges();s.addRange(r);
        }

        // Any edit inside the preview schedules a sync back to the editor.
        document.getElementById('content').addEventListener('input',scheduleSync);

        if(typeof marked!=='undefined'){marked.setOptions({breaks:true,gfm:true});}
        function update(md){
          var el=document.getElementById('content');
          if(typeof marked!=='undefined'){el.innerHTML=marked.parse(md||'');}
          else{el.innerText=md||'';}
          initTables();
        }
        </script>
        </body>
        </html>
        """;
}
