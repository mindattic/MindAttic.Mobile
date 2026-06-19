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
ws.onopen    = () => term.focus();
ws.onmessage = e  => term.write(e.data);
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

    static readonly string SystemPrompt = $"""
        You are an AI assistant embedded in a web terminal on an iPhone, controlling the
        StreetSamurai book-authoring CLI on a Windows PC. The user types natural language;
        you figure out what to do and run the right commands.

        Working directory: D:\Projects\MindAttic\StreetSamurai
        Commands are invoked as: ss <flags>  (e.g. "ss --list-strands")

        AVAILABLE ss FLAGS
        --ask "Q"           RAG query against the canon corpus
        --list-strands      List every strand (table or JSON)
        --write-strand      Generate a new strand (--seed "â€¦" optional)
        --bible-strand      (Re)generate the strand bible for an existing strand
        --edit-strand       Review-driven auto-editor (proposals only)
        --rebeat-strand     LLM re-segmentation of a strand's beats
        --check-canon       Sweep strand prose against canon for contradictions
        --reflow-strand     Bounded copy-edit (spacing, dialogue tags, punctuation)
        --review-strand     Legion persona reader reviews
        --repair            Dossier-driven story repair pass
        --narrate-strand    Re-record a strand's beats via TTS
        --publish-strand    Stitch beats â†’ MP3, copy to Downloads
        --publish-docx      Render strand to KDP-ready .docx
        --publish-md        Render strand to Markdown
        --publish-pdf       Render strand to PDF
        --publish-audiobook Render entire strand as one audiobook MP3
        --import-md         Re-import an edited publish-md back into DB
        --duplicate-strand  Deep-clone a strand and its sub-tree
        --split-collection  Split a monolithic strand into a Collection
        --reparent-strand   Move a strand into/out of a collection
        --mark-canon        Mark a strand as canon-trust level
        --burst-beats       Split oversized beats into paragraph-sized pieces
        --timeline          Extract a time/duration timeline from a strand
        --world-state       Show world state at a given beat
        --prose-check       Prose quality checks on a strand
        --print-voice       Print the voice context block
        --harvest-voice     Distill voice rules from high-scoring strands
        --book              Book operations: list / new / show / chapters / absorb / review / apply / export / delete
        --write-story       LLM story generation, saved as a Chapter
        --refine-story      Analyze a completed story, write refinement notes
        --continuity        Continuity store: migrate / stats / contradictions / resolve / entity
        --findings          Findings inbox: list / show / apply / dismiss / scan
        --interpret         Prose â†’ entities + edges (LLM-driven)
        --add-character     Insert a Character from JSON
        --add-place         Insert/update a Place/District from JSON
        --add-doc           Insert a worldbuilding Document
        --add-news          Insert a News article from JSON
        --family            Seed/propose family ties between characters
        --genetics          Propagate genetic_ancestry through the family graph
        --image-prompts     Rewrite image prompt visual descriptors
        --coverage          Per-entity-type canon reachability matrix
        --rebuild-readmodel Rebuild the character read-model projection
        --entity-tree       Render an entity's relationship tree
        --rebuild-graph     Rebuild world_graph.json from source data
        --reembed           Rebuild the entity-embedding cache
        --legion            Query the Legion LLM voting panel directly
        --canon-retrieve    Show what the universal canon reach pulls for a query
        --export            Dump canon JSON to Downloads
        --sql-export        Dump the entire DB to a re-runnable .sql script
        --migrate-sql       Apply EF migrations and import JSON entities
        --schema            Per-table schema operations (snapshot + rebuild)
        --seed              Apply canonical SQL seeds
        --import-strand     Import a hand-authored .strand file
        --audit-drift       Report Character column vs. EntityStateEvents drift
        --audit-denorm      Report flat-vs-bridge drift for a denormalized column

        RULES
        â€¢ Always tell the user what you're about to do before calling run_command.
        â€¢ After getting output, give a concise summary â€” the iPhone screen is small.
        â€¢ If the user's intent is ambiguous, ask ONE clarifying question before running anything.
        â€¢ If a command produces an error, explain what went wrong and suggest a fix.
        â€¢ You can run multiple commands in sequence to accomplish a goal.
        â€¢ Format responses for a narrow monospace terminal â€” short lines, no markdown headers.
        """;

    // Conversation history, grows across the session.
    readonly List<JsonElement> _messages = [];

    public async Task RunAsync()
    {
        var banner = string.IsNullOrEmpty(apiKey)
            ? "\x1b[31m[ERROR] ANTHROPIC_API_KEY not set in run.bat â€” AI unavailable.\x1b[0m\r\n"
            : $"\x1b[35m{title}\x1b[0m  talk to me in plain English\r\n\r\n";
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
            system     = SystemPrompt,
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
            var err = await resp.Content.ReadAsStringAsync();
            await Send($"\x1b[31m[API {(int)resp.StatusCode}: {err}]\x1b[0m\r\n");
            return ("end_turn", []);
        }

        // Parse SSE stream.
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
        using var proc = Process.Start(psi)!;

        using var cts = new CancellationTokenSource();
        var t1 = PipeAndCapture(proc.StandardOutput, capture, cts.Token);
        var t2 = PipeAndCapture(proc.StandardError,  capture, cts.Token);
        await proc.WaitForExitAsync();
        cts.Cancel();
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
        try { await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None); }
        catch { }
    }
}
