@echo off
set TERMINAL_WORKDIR=D:\Projects\MindAttic\StreetSamurai
set TERMINAL_TITLE=StreetSamurai
set TERMINAL_TOKEN=mindattic
set TERMINAL_PORT=8765
rem Pass through the API key from the environment (set it once in System env vars)
rem or hard-code it here: set ANTHROPIC_API_KEY=sk-ant-...
dotnet run --project "%~dp0MindAttic.Mobile.csproj"
