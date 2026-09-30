// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;

namespace AndiGenerator.Presentation.Tests;

/// <summary>Zugriff auf die Referenzfälle (Standard: anonymisierte Kopie im Repository, sonst ANDIGEN_REFERENZ).</summary>
internal static class Testdaten
{
    /// <summary>Holt den Ordner mit den Referenzfällen.</summary>
    public static string Referenz { get; } = Ermitteln();

    private static string Ermitteln()
    {
        string? pfad = Environment.GetEnvironmentVariable("ANDIGEN_REFERENZ");
        if (string.IsNullOrWhiteSpace(pfad))
        {
            pfad = typeof(Testdaten).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "Referenz")?.Value;
        }

        if (pfad is null || !Directory.Exists(pfad))
        {
            throw new DirectoryNotFoundException($"Referenz-Ordner nicht gefunden: '{pfad}'. Umgebungsvariable ANDIGEN_REFERENZ setzen.");
        }

        return Path.GetFullPath(pfad);
    }
}
