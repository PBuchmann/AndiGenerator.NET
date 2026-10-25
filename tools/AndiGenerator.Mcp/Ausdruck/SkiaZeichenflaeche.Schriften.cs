// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.UI;

/// <summary>Schriften des PDF-Ausdrucks im MCP-Server: in das Programm eingebettet (ohne Avalonia).</summary>
internal sealed partial class SkiaZeichenflaeche
{
    private static partial Stream SchriftOeffnen(string datei) =>
        typeof(SkiaZeichenflaeche).Assembly.GetManifestResourceStream("Schrift." + datei) ?? throw new FileNotFoundException("Schrift fehlt: " + datei, datei);
}
