// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;

namespace AndiGenerator.Persistence.Tests;

/// <summary>
/// Zugriff auf die Testdaten. Standard sind die anonymisierten Kopien im Repository (<c>testdaten/clicktt</c> und
/// <c>testdaten/referenz</c>, erzeugt mit <c>tools/anonymisieren</c>). Mit den Umgebungsvariablen ANDIGEN_TESTDATEN und
/// ANDIGEN_REFERENZ lassen sich die Tests gegen die privaten Originale laufen lassen.
/// </summary>
internal static class Testdaten
{
    /// <summary>Holt den Ordner mit den click-TT-Exporten, CSV-Plänen, gemerkten Plänen und Optionsdateien.</summary>
    public static string Ordner { get; } = Ermitteln("ANDIGEN_TESTDATEN", "Testdaten");

    /// <summary>Holt den Ordner mit den Referenzfällen (eingabe, werte, perf, datendialoge).</summary>
    public static string Referenz { get; } = Ermitteln("ANDIGEN_REFERENZ", "Referenz");

    public static string Datei(string name) => Path.Combine(Ordner, name);

    private static string Ermitteln(string umgebungsvariable, string schluessel)
    {
        string? pfad = Environment.GetEnvironmentVariable(umgebungsvariable);
        if (string.IsNullOrWhiteSpace(pfad))
        {
            pfad = typeof(Testdaten).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == schluessel)?.Value;
        }

        if (pfad is null || !Directory.Exists(pfad))
        {
            throw new DirectoryNotFoundException($"Testdaten-Ordner nicht gefunden: '{pfad}'. Umgebungsvariable {umgebungsvariable} setzen.");
        }

        return Path.GetFullPath(pfad);
    }
}
