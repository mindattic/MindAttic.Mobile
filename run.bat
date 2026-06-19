@echo off
set TERMINAL_WORKDIR=D:\Projects\MindAttic\StreetSamurai
set TERMINAL_TITLE=StreetSamurai
set TERMINAL_TOKEN=mindattic
set TERMINAL_PORT=8765
dotnet run --project "%~dp0MindAttic.Terminal.csproj"
