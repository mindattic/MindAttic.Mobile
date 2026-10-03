# MindAttic.Mobile

Drive the Prose book-writing CLI from your phone: a small web terminal that turns plain-English requests into commands on your Windows PC, using Claude with a run-command tool.

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4) ![ASP.NET Core WebSockets](https://img.shields.io/badge/ASP.NET%20Core-WebSockets-512BD4) ![xterm.js 5.3](https://img.shields.io/badge/xterm.js-5.3-1f1f1f) ![Claude API](https://img.shields.io/badge/Claude-Messages%20API-d97757) ![Status prototype](https://img.shields.io/badge/Status-prototype-d9762b)

```text
 phone browser (iPhone)                     Windows PC running MindAttic.Mobile
 ┌──────────────────────┐   ws://pc:8765/ws  ┌──────────────────────────────────────────┐
 │ xterm.js terminal    │ <────────────────> │ TerminalSession                           │
 │ > list my strands    │   keystrokes in,   │   line editor (Backspace, Ctrl+C, Enter)  │
 │ → ss.cmd --list-...  │   output out       │   Claude Messages API, streaming          │
 │ ...command output... │                    │     tool: run_command ──> cmd.exe /c ...  │
 └──────────────────────┘                    │           in TERMINAL_WORKDIR (Prose)     │
                                             └──────────────────────────────────────────┘
```

Open the page on your phone, type what you want in plain English, and watch the agent say what it is about to do, run the commands on your PC, and summarise the output for a small screen. The project began as a web-terminal bridge reached over Tailscale.

## Why

- Work on a Prose book from the couch or the train, with the PC doing the real work.
- Skip memorising CLI flags: the agent reads the current list of Prose flags from Prose's own source at the start of every session.
- See exactly what runs: every command is echoed in yellow before its output streams in.
- Get answers sized for a phone: the system prompt asks for short lines and concise summaries.
- Run nothing new on the phone: it is a plain web page with no app to install.

## Features

- **Web terminal** served at `/`: xterm.js 5.3 with the fit and web-links add-ons, a 5000-line scrollback, resizing with the browser and the on-screen keyboard.
- **WebSocket bridge** at `/ws` with 30-second keep-alives and binary frames, so UTF-8 output renders correctly.
- **Optional access token:** when `TERMINAL_TOKEN` is set, the WebSocket only accepts connections whose `token` query parameter matches it (open the page as `http://<pc>:8765/?token=<token>`).
- **Agent loop:** each line you enter goes to the Claude Messages API (streaming, model `claude-sonnet-4-6`). Claude can call one tool, `run_command`, which runs `cmd.exe /c <command>` in the working directory with a 120-second timeout; output streams back to the terminal and to Claude, and the loop continues until Claude stops asking for tools. Conversation history lasts for the session.
- **Prose-aware prompt:** the system prompt points the agent at `ss.cmd` in the working directory and lists every `--flag` found in Prose's `Program.cs`.
- **API key lookup** from the `ANTHROPIC_API_KEY` environment variable, or from MindAttic Vault's stored `claude` key.
- **A random greeting** on connect, drawn from 10,240 combinations.

## Quick start

Prerequisites: Windows, the .NET 10 SDK, a Claude API key, the Prose repository, and the MindAttic.Vault repository checked out next to this one (the project references `..\MindAttic.Vault\MindAttic.Vault\MindAttic.Vault.csproj`).

1. Make your API key available: set `ANTHROPIC_API_KEY` in your environment, or store a `claude` key in MindAttic Vault.
2. Edit `run.bat` to set `TERMINAL_WORKDIR` to your Prose folder and choose your own `TERMINAL_TOKEN`.

Then start it:

```powershell
.\run.bat
```

The console prints the address and working directory, for example `http://0.0.0.0:8765`. On your phone, open `http://<your-pc>:8765/?token=<your-token>` over your private network.

## Configuration

All settings are environment variables, set in `run.bat`:

| Variable | Default | Meaning |
|---|---|---|
| `TERMINAL_WORKDIR` | `D:\Projects\MindAttic\Prose` | Folder where commands run and where `ss.cmd` lives |
| `TERMINAL_TOKEN` | none | If set, required as the `token` query parameter on the WebSocket |
| `TERMINAL_TITLE` | `Prose` | Page title and banner |
| `TERMINAL_PORT` | `8765` | Port to listen on, on all interfaces |
| `ANTHROPIC_API_KEY` | from Vault | Claude API key; falls back to MindAttic Vault |

## Project layout

| Path | What it is |
|---|---|
| `Program.cs` | Everything: host setup, the embedded terminal page, `TerminalSession` with the agent loop, Claude streaming and command execution |
| `MindAttic.Mobile.csproj` | .NET 10 web project referencing MindAttic.Vault |
| `run.bat` | Sets the environment variables and runs the project |

## Limitations

- This runs any command Claude decides on, with your user's rights, on your PC. Only expose it on a private network you control, such as a Tailscale tailnet, and always set a token.
- The token travels in the URL and the connection is plain `ws://`, not TLS; protection comes from the private network, not from the app.
- It is built around Prose: the system prompt, default working directory and title all assume the Prose CLI.
- The model name and the 8192-token response limit are constants in `Program.cs`.
- Conversation history grows for the whole session; reload the page to start fresh.

## Documentation

- [AGENTS.md](AGENTS.md): entry point for AI agents working in this repo; it points at the shared MindAttic agent standard.

## License

This repository has no LICENSE file; all rights are reserved.

Part of [MindAttic](https://mindattic.com) — see more projects at [github.com/mindattic](https://github.com/mindattic). Related: [MindAttic.Vault](https://github.com/mindattic/MindAttic.Vault), where the API key can be stored.
