// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// MCP-Server für AndiGenerator.NET (stdio): Eine KI-Anwendung startet ihn als Unterprozess und steuert damit die Planung wie in
// der Oberfläche. Kein Netzwerk, keine Schlüssel; Protokoll nur auf stderr (stdout gehört dem Protokoll).
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// --basis <Ordner>: eigene Ablage für Optionen und gemerkte Pläne (Tests); sonst wie das Programm unter %LOCALAPPDATA%.
int stelle = Array.IndexOf(args, "--basis");
string? basis = stelle >= 0 && stelle + 1 < args.Length ? args[stelle + 1] : null;
builder.Services.AddSingleton(_ => basis is null ? new Fernsteuerung() : new Fernsteuerung(basis, null));
builder.Services
    .AddMcpServer(o => o.ServerInstructions = Werkzeuge.Anleitung)
    .WithStdioServerTransport()
    .WithTools<Werkzeuge>();
await builder.Build().RunAsync();
