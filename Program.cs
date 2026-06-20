using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MindAttic.Vault.Credentials;

// â”€â”€ Config â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
var workDir = Environment.GetEnvironmentVariable("TERMINAL_WORKDIR")
    ?? @"D:\Projects\MindAttic\StreetSamurai";
var wsToken = Environment.GetEnvironmentVariable("TERMINAL_TOKEN");
var title   = Environment.GetEnvironmentVariable("TERMINAL_TITLE") ?? "StreetSamurai";
var port    = Environment.GetEnvironmentVariable("TERMINAL_PORT")  ?? "8765";
var apiKey  = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? LlmCredentialStore.Default.GetKey("claude")
    ?? string.Empty;

// â”€â”€ Web app â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
var app = builder.Build();

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
app.MapGet("/", () => Results.Content(BuildHtml(title), "text/html"));
app.MapGet("/ws", async (HttpContext ctx) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    if (wsToken is not null && ctx.Request.Query["token"] != wsToken) { ctx.Response.StatusCode = 403; return; }
    var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    await new TerminalSession(ws, workDir, title, apiKey).RunAsync();
});

Console.WriteLine($"MindAttic.Terminal â†’ http://0.0.0.0:{port}  workDir={workDir}");
if (string.IsNullOrEmpty(apiKey))
    Console.WriteLine("WARNING: ANTHROPIC_API_KEY not set â€” AI responses will fail.");
app.Run();

// â”€â”€ Embedded HTML â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
    background:           '#280A14',
    foreground:           '#F2F2F2',
    cursor:               '#FF4081',
    selectionBackground:  'rgba(255,64,129,0.3)',
    black: '#0C0C0C', red: '#C50F1F', green: '#13A10E', yellow: '#C19C00',
    blue: '#0037DA', magenta: '#881798', cyan: '#3A96DD', white: '#CCCCCC',
    brightBlack: '#767676', brightRed: '#E74856', brightGreen: '#16C60C',
    brightYellow: '#F9F1A5', brightBlue: '#3B78FF', brightMagenta: '#B4009E',
    brightCyan: '#61D6D6', brightWhite: '#F2F2F2',
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
ws.binaryType = 'arraybuffer';
ws.onopen    = () => term.focus();
ws.onmessage = e  => term.write(new Uint8Array(e.data));
ws.onclose   = ()  => term.write('\r\n\x1b[31m[disconnected]\x1b[0m\r\n');
ws.onerror   = ()  => term.write('\r\n\x1b[31m[connection error]\x1b[0m\r\n');
term.onData(data => { if (ws.readyState === 1) ws.send(data); });

window.addEventListener('resize', () => fit.fit());
window.visualViewport?.addEventListener('resize', () => fit.fit());
</script>
</body>
</html>
""";

// â”€â”€ Session â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

sealed class TerminalSession(WebSocket ws, string workDir, string title, string apiKey)
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    const string Model = "claude-sonnet-4-6";

    static string RandomGreeting()
    {
        // 16 * 32 * 20 = 10,240 distinct combinations
        string[] opens = [
            "Blade's edge. Terminal live",
            "Night protocol active",
            "Ink and iron",
            "Chrome and shadow",
            "Steel city, open channel",
            "Neon grid online",
            "Dark streets, bright blade",
            "Cold city, hot terminal",
            "Circuit complete",
            "Ghost in the wire",
            "Samurai code loaded",
            "Signal locked",
            "Hardline established",
            "Midnight uplink",
            "Black market channel open",
            "Street feed live",
        ];
        string[] middles = [
            ", warrior",         ", ronin",            ", ghost",            ", exile",
            ", street poet",     ", operator",         ", cipher",           ", blade runner",
            ", night walker",    ", street samurai",   ", chrome fist",      ", neon shadow",
            ", urban ghost",     ", iron hand",        ", ink slinger",      ", road sage",
            ", knife edge",      ", dark matter",      ", shadow broker",    ", wire dancer",
            ", data phantom",    ", chrome monk",      ", signal wraith",    ", city wolf",
            ", black market sage", ", arc welder",     ", silicon ghost",    ", night blade",
            ", void walker",     ", code crow",        ", steel nomad",      ", storm crow",
        ];
        string[] closes = [
            ". What do you need?",
            ". Speak.",
            ". Begin.",
            ". The city waits.",
            ". Write something.",
            ". The night is yours.",
            ". Tell me.",
            ". Say the word.",
            ". The feed is open.",
            ". Clock is running.",
            ". I'm listening.",
            ". Make your move.",
            ". Channel's yours.",
            ". What's the play?",
            ". Run the line.",
            ". Go.",
            ". Type your move.",
            ". Command received — what next?",
            ". Street's quiet. Not for long.",
            ". The wire never sleeps.",
        ];
        return opens[Random.Shared.Next(opens.Length)]
            + middles[Random.Shared.Next(middles.Length)]
            + closes[Random.Shared.Next(closes.Length)];
    }

    // Built once per session; reads StreetSamurai source to stay in sync with new commands.
    string BuildSystemPrompt()
    {
        var flags = ReadSsFlags();
        var flagBlock = flags.Count > 0
            ? string.Join("\r\n        ", flags)
            : "(could not read StreetSamurai source — run ss.cmd --help for the full list)";
        var ssExe = Path.Combine(workDir, "ss.cmd");
        return $"""
            You are an AI assistant embedded in a web terminal on an iPhone, controlling the
            StreetSamurai book-authoring CLI on a Windows PC. The user types natural language;
            you figure out what to do and run the right commands.

            Working directory: {workDir}
            IMPORTANT: The CLI is a .cmd batch file. ALWAYS invoke it as the full path:
                {ssExe} <flags>
            Never use just "ss" — it is not on PATH and will fail every time.
            Example: {ssExe} --list-strands

            AVAILABLE ss FLAGS (auto-read from source; refreshed each session)
            {flagBlock}

            RULES
            • Always tell the user what you are about to do before calling run_command.
            • After getting output, give a concise summary — the iPhone screen is small.
            • If the user's intent is ambiguous, ask ONE clarifying question before running anything.
            • If a command produces an error, explain what went wrong and suggest a fix.
            • You can run multiple commands in sequence to accomplish a goal.
            • Format responses for a narrow monospace terminal — short lines, no markdown headers.
            """;
    }

    // Scan StreetSamurai Program.cs for every args.Contains("--flag") call.
    List<string> ReadSsFlags()
    {
        var candidates = new[]
        {
            Path.Combine(workDir, "v3", "StreetSamurai.Blazor", "Program.cs"),
            Path.Combine(workDir, "Program.cs"),
        };
        var rx = new System.Text.RegularExpressions.Regex(@"args\.Contains\(""(--[a-z][a-z0-9-]*)""\)");
        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            var seen  = new HashSet<string>();
            var flags = new List<string>();
            foreach (var line in File.ReadLines(path))
            {
                var m = rx.Match(line);
                if (m.Success && seen.Add(m.Groups[1].Value))
                    flags.Add(m.Groups[1].Value);
            }
            if (flags.Count > 0) return flags;
        }
        return [];
    }

    // Conversation history, grows across the session.
    readonly List<JsonElement> _messages = [];

    public async Task RunAsync()
    {
        var banner = string.IsNullOrEmpty(apiKey)
            ? "\x1b[31m[ERROR] ANTHROPIC_API_KEY not set in run.bat â€” AI unavailable.\x1b[0m\r\n"
            : $"\x1b[35m{title}\x1b[0m  {RandomGreeting()}\r\n\r\n";
        await Send(banner + "> ");

        var buf  = new StringBuilder();
        var recv = new byte[8192];

        while (ws.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            try { result = await ws.ReceiveAsync(recv, CancellationToken.None); }
            catch { break; }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
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
                        await Send("\r\n");
                        if (line.Length > 0 && !string.IsNullOrEmpty(apiKey))
                            await HandleMessage(line);
                        await Send("\r\n> ");
                        break;
                    case '\b':
                    case '\x7f':
                        if (buf.Length > 0) { buf.Remove(buf.Length - 1, 1); await Send("\b \b"); }
                        break;
                    case '\x03':
                        buf.Clear();
                        await Send("\r\n^C\r\n> ");
                        break;
                    default:
                        if (ch >= ' ') { buf.Append(ch); await Send(ch.ToString()); }
                        break;
                }
            }
        }
    }

    // â”€â”€ Agentic loop â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    async Task HandleMessage(string userText)
    {
        _messages.Add(JsonSerializer.SerializeToElement(new { role = "user", content = userText }));
        await AgentLoop();
    }

    async Task AgentLoop()
    {
        while (true)
        {
            var (stopReason, assistantBlocks) = await StreamMessage();
            _messages.Add(JsonSerializer.SerializeToElement(new { role = "assistant", content = assistantBlocks }));

            if (stopReason != "tool_use") break;

            // Execute every tool call, collect results.
            var results = new List<object>();
            foreach (var block in assistantBlocks)
            {
                if (!block.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "tool_use") continue;

                var id      = block.GetProperty("id").GetString()!;
                var command = block.GetProperty("input").GetProperty("command").GetString()!;

                await Send($"\x1b[33mâ†’ {command}\x1b[0m\r\n");
                var output = await RunCommand(command);
                results.Add(new { type = "tool_result", tool_use_id = id, content = output });
            }

            _messages.Add(JsonSerializer.SerializeToElement(new { role = "user", content = results }));
        }
    }

    // â”€â”€ Anthropic streaming â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    static readonly object[] Tools =
    [
        new
        {
            name = "run_command",
            description = "Run a shell command in the StreetSamurai directory and return its output.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    command = new
                    {
                        type = "string",
                        description = "Full command to run, e.g. \"ss --list-strands\" or \"dir\""
                    }
                },
                required = new[] { "command" }
            }
        }
    ];

    async Task<(string stopReason, List<JsonElement> content)> StreamMessage()
    {
        var body = JsonSerializer.Serialize(new
        {
            model      = Model,
            max_tokens = 8192,
            system     = BuildSystemPrompt(),
            messages   = _messages,
            tools      = Tools,
            stream     = true
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("x-api-key", apiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");

        HttpResponseMessage resp;
        try
        {
            resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        }
        catch (Exception ex)
        {
            await Send($"\x1b[31m[API error: {ex.Message}]\x1b[0m\r\n");
            return ("end_turn", []);
        }

        if (!resp.IsSuccessStatusCode)
        {
            using (resp)
            {
                var err = await resp.Content.ReadAsStringAsync();
                await Send($"\x1b[31m[API {(int)resp.StatusCode}: {err}]\x1b[0m\r\n");
            }
            return ("end_turn", []);
        }

        // Parse SSE stream.
        using var _ = resp;
        await using var stream = await resp.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        var blocks      = new List<JsonElement>();
        var textBufs    = new Dictionary<int, StringBuilder>();
        var inputBufs   = new Dictionary<int, StringBuilder>();
        var blockMeta   = new Dictionary<int, (string type, string id, string name)>();
        var stopReason  = "end_turn";

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (!line.StartsWith("data: ")) continue;
            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            JsonElement evt;
            try { evt = JsonSerializer.Deserialize<JsonElement>(data); }
            catch { continue; }

            switch (evt.GetProperty("type").GetString())
            {
                case "content_block_start":
                {
                    var idx   = evt.GetProperty("index").GetInt32();
                    var block = evt.GetProperty("content_block");
                    var btype = block.GetProperty("type").GetString()!;
                    if (btype == "text")
                    {
                        textBufs[idx] = new StringBuilder();
                        blockMeta[idx] = ("text", "", "");
                    }
                    else if (btype == "tool_use")
                    {
                        var id   = block.GetProperty("id").GetString()!;
                        var name = block.GetProperty("name").GetString()!;
                        inputBufs[idx] = new StringBuilder();
                        blockMeta[idx] = ("tool_use", id, name);
                    }
                    break;
                }

                case "content_block_delta":
                {
                    var idx   = evt.GetProperty("index").GetInt32();
                    var delta = evt.GetProperty("delta");
                    var dtype = delta.GetProperty("type").GetString();

                    if (dtype == "text_delta" && textBufs.TryGetValue(idx, out var tb))
                    {
                        var chunk = delta.GetProperty("text").GetString() ?? "";
                        tb.Append(chunk);
                        await Send(chunk.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n"));
                    }
                    else if (dtype == "input_json_delta" && inputBufs.TryGetValue(idx, out var ib))
                    {
                        ib.Append(delta.GetProperty("partial_json").GetString() ?? "");
                    }
                    break;
                }

                case "content_block_stop":
                {
                    var idx = evt.GetProperty("index").GetInt32();
                    if (!blockMeta.TryGetValue(idx, out var meta)) break;

                    if (meta.type == "text")
                    {
                        blocks.Add(JsonSerializer.SerializeToElement(new
                        {
                            type = "text",
                            text = textBufs.GetValueOrDefault(idx)?.ToString() ?? ""
                        }));
                    }
                    else if (meta.type == "tool_use")
                    {
                        var raw = inputBufs.GetValueOrDefault(idx)?.ToString() ?? "{}";
                        JsonElement inputEl;
                        try { inputEl = JsonSerializer.Deserialize<JsonElement>(raw); }
                        catch { inputEl = JsonSerializer.Deserialize<JsonElement>("{}"); }

                        blocks.Add(JsonSerializer.SerializeToElement(new
                        {
                            type  = "tool_use",
                            id    = meta.id,
                            name  = meta.name,
                            input = inputEl
                        }));
                    }
                    break;
                }

                case "message_delta":
                    if (evt.TryGetProperty("delta", out var d) && d.TryGetProperty("stop_reason", out var sr))
                        stopReason = sr.GetString() ?? "end_turn";
                    break;
            }
        }

        return (stopReason, blocks);
    }

    // â”€â”€ Command execution â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    async Task<string> RunCommand(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = "cmd.exe",
            Arguments              = $"/c {command}",
            WorkingDirectory       = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding  = Encoding.UTF8,
        };

        var capture = new StringBuilder();
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start cmd.exe for: {command}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var t1 = PipeAndCapture(proc.StandardOutput, capture, cts.Token);
        var t2 = PipeAndCapture(proc.StandardError,  capture, cts.Token);
        try
        {
            await proc.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
        }
        finally
        {
            cts.Cancel();
        }
        await Task.WhenAll(t1, t2);

        if (proc.ExitCode != 0)
        {
            var msg = $"\r\n[exit {proc.ExitCode}]";
            await Send($"\x1b[31m{msg}\x1b[0m");
            capture.Append(msg);
        }

        return capture.ToString();
    }

    async Task PipeAndCapture(StreamReader reader, StringBuilder capture, CancellationToken ct)
    {
        var buf = new char[4096];
        while (!ct.IsCancellationRequested)
        {
            int n;
            try { n = await reader.ReadAsync(buf.AsMemory(), ct); }
            catch { break; }
            if (n == 0) break;
            var text = new string(buf, 0, n).Replace("\r\n", "\n").Replace("\r", "\n");
            capture.Append(text);
            await Send(text.Replace("\n", "\r\n"));
        }
    }

    async Task Send(string text)
    {
        if (ws.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(text);
        // Binary frames: xterm.js receives Uint8Array and decodes UTF-8 correctly.
        try { await ws.SendAsync(bytes, WebSocketMessageType.Binary, true, CancellationToken.None); }
        catch { }
    }
}
