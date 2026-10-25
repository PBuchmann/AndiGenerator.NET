// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>
/// Die Beispielstaffeln, die dem Programm beiliegen (Ordner „Beispiele“ neben dem Programm): die Testpläne des Originals
/// (Saison 2015/16) und zwei Klassen mit Kompaktspieltagen (tools/kompaktklasse/mytischtennis.py). Geöffnet wird eine Kopie im eigenen Datenordner – der Programmordner kann schreibgeschützt sein
/// (macOS, Linux) und wird bei Updates ersetzt; die Änderungen an einem Beispiel bleiben so erhalten.
/// </summary>
public static class Beispielsammlung
{
    private static readonly Dictionary<string, (int Rang, string Text)> Beschreibungen = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Liga mit idealen Meldungen"] = (1, "10 Mannschaften mit idealen Terminwünschen – der einfachste Einstieg."),
        ["Jugend 19 Hessenliga (HR)"] = (2, "Kompaktspieltage: die 10 Mannschaften der Jugend 19 Hessenliga 2026/27 (Namen von myTischtennis), Termine und Hallen erfunden."),
        ["Jugend 19 NRW-Liga 1 (HR)"] = (3, "Kompaktspieltage: die 8 Mannschaften der Jugend 19 NRW-Liga 1 2026/27 (Namen von myTischtennis, zwei aus einem Verein), Termine und Hallen erfunden."),
        ["Liga mit Koppelterminen"] = (4, "10 Mannschaften, Heimkoppeln (zwei Heimspiele an einem Tag) und ein Plan aus click-TT."),
        ["Liga mit 2 Mannschaften aus einem Verein"] = (5, "10 Mannschaften, zwei davon aus demselben Verein, mit Plan aus click-TT."),
        ["Liga mit 11 Mannschaften und 40km Regel"] = (6, "11 Mannschaften, ungerade Zahl (spielfrei), Auswärtsspiele nur am Wochenende, mit Plan aus click-TT."),
        ["Liga für Doppelrunde"] = (7, "6 Mannschaften, jeder spielt viermal gegen jeden (Doppelrunde)."),
    };

    /// <summary>Beispiele, die als Doppelrunde gedacht sind; beim ersten Öffnen wird die Doppelrunde eingeschaltet.</summary>
    private static readonly HashSet<string> Doppelrunden = new(StringComparer.OrdinalIgnoreCase) { "Liga für Doppelrunde" };

    /// <summary>Holt den Ordner der beiliegenden Beispiele.</summary>
    public static string Programmordner { get; } = Path.Combine(AppContext.BaseDirectory, "Beispiele");

    /// <summary>Liest die Beispiele eines Ordners, bekannte in sinnvoller Reihenfolge zuerst.</summary>
    /// <param name="ordner">Der Ordner.</param>
    /// <returns>Die Beispiele; leer, wenn es den Ordner nicht gibt.</returns>
    public static IReadOnlyList<Beispielzeile> Lesen(string ordner)
    {
        if (!Directory.Exists(ordner))
        {
            return [];
        }

        return Directory.EnumerateFiles(ordner, "*.xml")
            .Select(p => (Pfad: p, Name: Path.GetFileNameWithoutExtension(p)))
            .Select(e => (e.Pfad, e.Name, Angabe: Beschreibungen.GetValueOrDefault(e.Name, (Rang: int.MaxValue, Text: string.Empty))))
            .OrderBy(e => e.Angabe.Rang)
            .ThenBy(e => e.Name, StringComparer.CurrentCulture)
            .Select(e => new Beispielzeile(e.Name, e.Angabe.Text, e.Pfad))
            .ToList();
    }

    /// <summary>Gibt an, ob ein Beispiel als Doppelrunde gedacht ist (jeder spielt viermal gegen jeden).</summary>
    /// <param name="pfad">Pfad des Beispiels (beiliegend oder Kopie).</param>
    /// <returns><c>true</c> für eine Doppelrunde.</returns>
    public static bool IstDoppelrunde(string pfad) => Doppelrunden.Contains(Path.GetFileNameWithoutExtension(pfad));

    /// <summary>
    /// Legt die Kopie eines Beispiels im Datenordner an (Unterordner „Beispiele“), falls es sie noch nicht gibt; eine
    /// vorhandene Kopie bleibt mit allen Änderungen erhalten.
    /// </summary>
    /// <param name="quelle">Die beiliegende Datei.</param>
    /// <param name="basis">Der Datenordner.</param>
    /// <returns>Der Pfad der Kopie.</returns>
    public static string Kopieren(string quelle, string basis)
    {
        string ordner = Path.Combine(basis, "Beispiele");
        string ziel = Path.Combine(ordner, Path.GetFileName(quelle));
        if (!File.Exists(ziel))
        {
            Directory.CreateDirectory(ordner);
            Kopie(quelle, ziel);

            // Vorbereitete Spielplandaten (z. B. Hallenadressen) liegen als .modifications daneben.
            string aenderungen = Path.ChangeExtension(quelle, ".modifications");
            if (File.Exists(aenderungen))
            {
                Kopie(aenderungen, Path.ChangeExtension(ziel, ".modifications"));
            }
        }

        return ziel;
    }

    private static void Kopie(string quelle, string ziel)
    {
        File.Copy(quelle, ziel, overwrite: true);

        // Aus einem schreibgeschützten Programmordner kommt die Datei schreibgeschützt an.
        File.SetAttributes(ziel, File.GetAttributes(ziel) & ~FileAttributes.ReadOnly);
    }
}
