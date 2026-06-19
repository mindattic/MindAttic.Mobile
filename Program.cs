using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;

// Config via env vars so Console can set them per-launch.
var workDir  = Environment.GetEnvironmentVariable("TERMINAL_WORKDIR")
               ?? @"D:\Projects\MindAttic\StreetSamurai";
var command  = Environment.GetEnvironmentVariable("TERMINAL_COMMAND") ?? "ss";
var token    = Environment.GetEnvironmentVariable("TERMINAL_TOKEN");   // required if set
var title    = Environment.GetEnvironmentVariable("TERMINAL_TITLE")   ?? "StreetSamurai";
var port     = Environment.GetEnvironmentVariable("TERMINAL_PORT")    ?? "7680";

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
var app = builder.Build();

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });

app.MapGet("/", () => Results.Content(BuildHtml(title), "text/html"));

app.MapGet("/ws", async (HttpContext ctx) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }

    if (token is not null && ctx.Request.Query["token"] != token)
    {
        ctx.Response.StatusCode = 403;
        return;
    }

    var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    await RunSession(ws, workDir, title);
});

Console.WriteLine($"MindAttic.Terminal → http://0.0.0.0:{port}  (workDir={workDir})");
app.Run();

// ── session ────────────────────────────────────────────────────────────────

static async Task RunSession(WebSocket ws, string workDir, string title)
{
    await WsSend(ws,
        $"\x1b[35m{title} Terminal\x1b[0m  \x1b[2m{workDir}\x1b[0m\r\n" +
        $"\x1b[2mtype any command, e.g.\x1b[0m  \x1b[36mss --list-strands\x1b[0m\r\n$ ");

    var buf  = new StringBuilder();
    var recv = new byte[4096];

    // Track the currently-running process so Ctrl+C can kill it.
    Process? running = null;

    while (ws.State == WebSocketState.Open)
    {
        WebSocketReceiveResult result;
        try { result = await ws.ReceiveAsync(recv, CancellationToken.None); }
        catch { break; }

        if (result.MessageType == WebSocketMessageType.Close)
        {
            running?.Kill(entireProcessTree: true);
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
            break;
        }

        var text = Encoding.UTF8.GetString(recv, 0, result.Count);
        foreach (var ch in text)
        {
            switch (ch)
            {
                case '\r':
                case '\n':
                    var line = buf.ToString().Trim();
                    buf.Clear();
                    await WsSend(ws, "\r\n");
                    if (line.Length > 0)
                    {
                        running = await RunCommand(ws, workDir, line);
                        running = null;
                    }
                    await WsSend(ws, "$ ");
                    break;

                case '\b':
                case '\x7f': // backspace / DEL
                    if (buf.Length > 0) { buf.Remove(buf.Length - 1, 1); await WsSend(ws, "\b \b"); }
                    break;

                case '\x03': // Ctrl+C
                    running?.Kill(entireProcessTree: true);
                    buf.Clear();
                    await WsSend(ws, "\r\n\x1b[33m^C\x1b[0m\r\n$ ");
                    break;

                default:
                    if (ch >= ' ') { buf.Append(ch); await WsSend(ws, ch.ToString()); }
                    break;
            }
        }
    }
}

static async Task<Process?> RunCommand(WebSocket ws, string workDir, string line)
{
    var psi = new ProcessStartInfo
    {
        FileName               = "cmd.exe",
        Arguments              = $"/c {line}",
        WorkingDirectory       = workDir,
        RedirectStandardOutput = true,
        RedirectStandardError  = true,
        UseShellExecute        = false,
        CreateNoWindow         = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding  = Encoding.UTF8,
    };

    Process proc;
    try { proc = Process.Start(psi)!; }
    catch (Exception ex) { await WsSend(ws, $"\x1b[31m[launch error: {ex.Message}]\x1b[0m\r\n"); return null; }

    using var cts = new CancellationTokenSource();
    var t1 = PipeStream(proc.StandardOutput, ws, cts.Token);
    var t2 = PipeStream(proc.StandardError,  ws, cts.Token);

    await proc.WaitForExitAsync();
    cts.Cancel();
    await Task.WhenAll(t1, t2);

    if (proc.ExitCode != 0)
        await WsSend(ws, $"\r\n\x1b[31m[exit {proc.ExitCode}]\x1b[0m");

    return proc;
}

static async Task PipeStream(StreamReader reader, WebSocket ws, CancellationToken ct)
{
    var buf = new char[4096];
    while (!ct.IsCancellationRequested)
    {
        int n;
        try { n = await reader.ReadAsync(buf.AsMemory(), ct); }
        catch { break; }
        if (n == 0) break;
        // normalise line-endings for the browser terminal
        var text = new string(buf, 0, n).Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        await WsSend(ws, text);
    }
}

static async Task WsSend(WebSocket ws, string text)
{
    if (ws.State != WebSocketState.Open) return;
    var bytes = Encoding.UTF8.GetBytes(text);
    try { await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None); }
    catch { }
}

// ── embedded HTML ───────────────────────────────────────────────────────────

static string BuildHtml(string title) => $$"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no">
<title>{{title}}</title>
<link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/xterm@5.3.0/css/xterm.css">
<style>
* { margin: 0; padding: 0; box-sizing: border-box; }
html, body { width: 100%; height: 100%; background: #280A14; overflow: hidden; }
#term { width: 100%; height: 100%; }
</style>
</head>
<body>
<div id="term"></div>
<script src="https://cdn.jsdelivr.net/npm/xterm@5.3.0/lib/xterm.js"></script>
<script src="https://cdn.jsdelivr.net/npm/xterm-addon-fit@0.8.0/lib/xterm-addon-fit.js"></script>
<script src="https://cdn.jsdelivr.net/npm/xterm-addon-web-links@0.9.0/lib/xterm-addon-web-links.js"></script>
<script>
const term = new Terminal({
  cursorBlink: true,
  fontSize: 15,
  fontFamily: '"SF Mono", Menlo, Consolas, "Courier New", monospace',
  scrollback: 5000,
  allowProposedApi: true,
  theme: {
    background:          '#280A14',
    foreground:          '#F2F2F2',
    cursor:              '#FF4081',
    selectionBackground: 'rgba(255,64,129,0.3)',
    black:         '#0C0C0C', red:          '#C50F1F', green:        '#13A10E', yellow:       '#C19C00',
    blue:          '#0037DA', magenta:      '#881798', cyan:         '#3A96DD', white:        '#CCCCCC',
    brightBlack:   '#767676', brightRed:    '#E74856', brightGreen:  '#16C60C', brightYellow: '#F9F1A5',
    brightBlue:    '#3B78FF', brightMagenta:'#B4009E', brightCyan:   '#61D6D6', brightWhite:  '#F2F2F2',
  },
});

const fit   = new FitAddon.FitAddon();
const links = new WebLinksAddon.WebLinksAddon();
term.loadAddon(fit);
term.loadAddon(links);
term.open(document.getElementById('term'));
fit.fit();

const params = new URLSearchParams(location.search);
const token  = params.get('token') ?? '';
const wsUrl  = `ws://${location.hostname}:${location.port}/ws${token ? '?token=' + encodeURIComponent(token) : ''}`;
const ws = new WebSocket(wsUrl);
ws.onopen    = () => term.focus();
ws.onmessage = e  => term.write(e.data);
ws.onclose   = ()  => term.write('\r\n\x1b[31m[disconnected]\x1b[0m\r\n');
ws.onerror   = ()  => term.write('\r\n\x1b[31m[connection error]\x1b[0m\r\n');

term.onData(data => { if (ws.readyState === 1) ws.send(data); });

window.addEventListener('resize', () => fit.fit());
// iOS Safari: refit when the software keyboard raises/lowers
window.visualViewport?.addEventListener('resize', () => fit.fit());
</script>
</body>
</html>
""";
