using Microsoft.Web.WebView2.Core;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wheelhouse.UI.ViewModels;

namespace Wheelhouse.UI.Views;

public partial class TerminalPaneView : UserControl
{
    private TerminalPaneViewModel? _vm;
    private bool _webViewReady;

    // xterm.js + multi-terminal host HTML
    private const string TerminalHtml = """
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset="utf-8">
        <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/xterm@5.3.0/css/xterm.css">
        <style>
          * { box-sizing: border-box; }
          body { margin: 0; background: #0d1117; overflow: hidden; }
          .term-wrap { display: none; width: 100vw; height: 100vh; }
          .term-wrap.active { display: block; }
        </style>
        </head>
        <body>
        <script src="https://cdn.jsdelivr.net/npm/xterm@5.3.0/lib/xterm.js"></script>
        <script src="https://cdn.jsdelivr.net/npm/xterm-addon-fit@0.8.0/lib/xterm-addon-fit.js"></script>
        <script>
        const terms = {};

        window.chrome.webview.addEventListener('message', e => {
          const m = JSON.parse(e.data);
          if (m.type === 'create')  createTerm(m.id);
          if (m.type === 'write')   terms[m.id]?.term.write(m.data);
          if (m.type === 'switch')  switchTerm(m.id);
          if (m.type === 'destroy') destroyTerm(m.id);
        });

        function createTerm(id) {
          const wrap = document.createElement('div');
          wrap.className = 'term-wrap';
          wrap.id = 'w' + id;
          document.body.appendChild(wrap);

          const term = new Terminal({
            cursorBlink: true,
            theme: { background:'#0d1117', foreground:'#e6edf3', cursor:'#58a6ff',
                     selectionBackground:'#264f78' },
            fontFamily: 'Cascadia Code, Consolas, Courier New, monospace',
            fontSize: 13, lineHeight: 1.2
          });
          const fit = new FitAddon.FitAddon();
          term.loadAddon(fit);
          term.open(wrap);
          fit.fit();

          term.onData(d => post({type:'input', id, data:d}));

          new ResizeObserver(() => {
            fit.fit();
            post({type:'resize', id, cols:term.cols, rows:term.rows});
          }).observe(wrap);

          terms[id] = {term, fit, wrap};
          switchTerm(id);
        }

        function switchTerm(id) {
          Object.values(terms).forEach(t => t.wrap.classList.remove('active'));
          if (terms[id]) terms[id].wrap.classList.add('active');
        }

        function destroyTerm(id) {
          if (!terms[id]) return;
          terms[id].term.dispose();
          terms[id].wrap.remove();
          delete terms[id];
        }

        function post(obj) { window.chrome.webview.postMessage(JSON.stringify(obj)); }
        </script>
        </body>
        </html>
        """;

    public TerminalPaneView()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) return; // already initialized

        _vm = DataContext as TerminalPaneViewModel;
        if (_vm is null) return;

        _vm.TabCreated   += OnTabCreated;
        _vm.TabClosed    += OnTabClosed;
        _vm.TabActivated += OnTabActivated;

        // Init WebView2 with a dedicated user-data folder
        var userDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Wheelhouse", "WebView2");

        var env = await CoreWebView2Environment.CreateAsync(null, userDataDir);
        await WebView.EnsureCoreWebView2Async(env);

        WebView.CoreWebView2.WebMessageReceived += OnWebMessage;
        WebView.CoreWebView2.NavigateToString(TerminalHtml);
        _webViewReady = true;

        // Open the first tab automatically
        if (_vm.Tabs.Count == 0)
            await _vm.AddTabCommand.ExecuteAsync(null);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.TabCreated   -= OnTabCreated;
        _vm.TabClosed    -= OnTabClosed;
        _vm.TabActivated -= OnTabActivated;
    }

    // Called by xterm.js when user types or terminal resizes
    private async void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_vm is null) return;
        try
        {
            var raw = e.TryGetWebMessageAsString();
            if (raw is null) return;
            using var doc = JsonDocument.Parse(raw);
            var type = doc.RootElement.GetProperty("type").GetString();
            var id   = doc.RootElement.GetProperty("id").GetString();
            var tab  = _vm.Tabs.FirstOrDefault(t => t.TerminalId == id);
            if (tab is null) return;

            if (type == "input")
            {
                var data = doc.RootElement.GetProperty("data").GetString() ?? "";
                await tab.Session.WriteInputAsync(data);
            }
            else if (type == "resize")
            {
                var cols = doc.RootElement.GetProperty("cols").GetInt32();
                var rows = doc.RootElement.GetProperty("rows").GetInt32();
                await tab.Session.ResizeAsync(cols, rows);
            }
        }
        catch { /* malformed message — ignore */ }
    }

    // New terminal tab was created by the ViewModel
    private void OnTabCreated(object? sender, TerminalTabViewModel tab)
    {
        if (!_webViewReady) return;
        PostToWebView(new { type = "create", id = tab.TerminalId });
        tab.Session.OutputReceived += (_, data) => SendOutput(tab.TerminalId, data);
    }

    // Tab was removed
    private void OnTabClosed(object? sender, TerminalTabViewModel tab)
    {
        if (!_webViewReady) return;
        PostToWebView(new { type = "destroy", id = tab.TerminalId });
    }

    // Active tab switched
    private void OnTabActivated(object? sender, TerminalTabViewModel tab)
    {
        if (!_webViewReady) return;
        PostToWebView(new { type = "switch", id = tab.TerminalId });
    }

    private void SendOutput(string terminalId, string data)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_webViewReady)
                PostToWebView(new { type = "write", id = terminalId, data });
        });
    }

    private void PostToWebView(object message)
    {
        try { WebView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(message)); }
        catch { /* WebView may not be ready */ }
    }

    // Click on a tab header to activate it
    private void OnTabClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null) return;
        if ((sender as FrameworkElement)?.DataContext is TerminalTabViewModel tab)
            _vm.ActiveTab = tab;
    }
}
