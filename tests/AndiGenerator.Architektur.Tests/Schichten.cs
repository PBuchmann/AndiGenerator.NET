// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;

namespace AndiGenerator.Architektur.Tests;

/// <summary>Schichten der Lösung (MIGRATIONSPLAN E5) und wo ihre Dateien liegen.</summary>
internal static class Schichten
{
    /// <summary>Direkt erlaubte Projektverweise je Schicht.</summary>
    public static IReadOnlyDictionary<string, string[]> Erlaubt { get; } = new Dictionary<string, string[]>
    {
        ["AndiGenerator.Domain"] = [],
        ["AndiGenerator.Engine"] = ["AndiGenerator.Domain"],
        ["AndiGenerator.Persistence"] = ["AndiGenerator.Domain"],
        ["AndiGenerator.Rendering"] = ["AndiGenerator.Domain", "AndiGenerator.Engine"],
        ["AndiGenerator.Application"] = ["AndiGenerator.Domain", "AndiGenerator.Engine", "AndiGenerator.Persistence", "AndiGenerator.Rendering"],
        ["AndiGenerator.Presentation"] = ["AndiGenerator.Domain", "AndiGenerator.Application", "AndiGenerator.Rendering"],
        ["AndiGenerator.UI"] = ["AndiGenerator.Presentation"],
        ["AndiGenerator.Desktop"] = ["AndiGenerator.UI"],
    };

    /// <summary>Holt die Schichten des Rechenkerns: reine Logik ohne Dateien, Netz und Drittbibliotheken.</summary>
    public static string[] Rechenkern { get; } = ["AndiGenerator.Domain", "AndiGenerator.Engine", "AndiGenerator.Rendering"];

    /// <summary>Holt die Schichten ohne Drittbibliotheken (nur .NET selbst).</summary>
    public static string[] NurDotNet { get; } =
        ["AndiGenerator.Domain", "AndiGenerator.Engine", "AndiGenerator.Persistence", "AndiGenerator.Rendering", "AndiGenerator.Application"];

    /// <summary>Holt den Ordner der Lösung.</summary>
    public static string Loesungsordner { get; } = Path.GetFullPath(
        typeof(Schichten).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().First(a => a.Key == "Loesungsordner").Value!);

    /// <summary>Alle Schichten, die eine Schicht verwenden darf: die erlaubten und alles, was diese verwenden dürfen.</summary>
    /// <param name="schicht">Die Schicht.</param>
    /// <returns>Die Schichten, ohne die Schicht selbst.</returns>
    public static HashSet<string> Huelle(string schicht)
    {
        var ergebnis = new HashSet<string>(StringComparer.Ordinal);
        var offen = new Stack<string>(Erlaubt[schicht]);
        while (offen.Count > 0)
        {
            string naechste = offen.Pop();
            if (ergebnis.Add(naechste))
            {
                foreach (string weitere in Erlaubt[naechste])
                {
                    offen.Push(weitere);
                }
            }
        }

        return ergebnis;
    }

    /// <summary>Name der gebauten Datei einer Schicht (das Programm heißt anders als sein Projekt).</summary>
    /// <param name="schicht">Die Schicht.</param>
    /// <returns>Der Dateiname der DLL.</returns>
    public static string Dateiname(string schicht) => schicht == "AndiGenerator.Desktop" ? "AndiGenerator.NET.dll" : schicht + ".dll";

    /// <summary>Alle Schichten als Testdaten.</summary>
    /// <returns>Die Testdaten.</returns>
    public static TheoryData<string> Alle() => Daten(Erlaubt.Keys);

    /// <summary>Schichten als Testdaten.</summary>
    /// <param name="schichten">Die Schichten.</param>
    /// <returns>Die Testdaten.</returns>
    public static TheoryData<string> Daten(IEnumerable<string> schichten)
    {
        var daten = new TheoryData<string>();
        foreach (string schicht in schichten)
        {
            daten.Add(schicht);
        }

        return daten;
    }
}
