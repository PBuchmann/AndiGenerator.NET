// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;

namespace AndiGenerator.Application;

/// <summary>
/// Liste der zuletzt geöffneten Staffeln für die Startseite (neu, im Original nicht vorhanden). Gespeichert als Textdatei
/// im eigenen Ordner, eine Zeile je Staffel: Zeitpunkt, Name und Pfad, getrennt durch Tabulatoren.
/// </summary>
public static class Zuletztliste
{
    /// <summary>Name der Datei im eigenen Ordner.</summary>
    public const string Dateiname = "zuletzt-geoeffnet.txt";

    /// <summary>Höchstzahl der gemerkten Staffeln.</summary>
    public const int Hoechstzahl = 8;

    /// <summary>Liest die Liste; fehlt die Datei oder ist eine Zeile ungültig, wird sie übergangen.</summary>
    /// <param name="basis">Der eigene Ordner.</param>
    /// <returns>Die Einträge, neueste zuerst.</returns>
    public static IReadOnlyList<ZuletztGeoeffnet> Laden(string basis)
    {
        ArgumentException.ThrowIfNullOrEmpty(basis);
        string pfad = Path.Combine(basis, Dateiname);
        if (!File.Exists(pfad))
        {
            return [];
        }

        return File.ReadAllLines(pfad).Select(Eintrag).OfType<ZuletztGeoeffnet>().Take(Hoechstzahl).ToList();
    }

    /// <summary>Setzt eine Staffel an den Anfang der Liste und speichert sie.</summary>
    /// <param name="basis">Der eigene Ordner.</param>
    /// <param name="eintrag">Die gerade geöffnete Staffel.</param>
    /// <returns>Die neue Liste.</returns>
    /// <exception cref="IOException">Die Datei konnte nicht geschrieben werden.</exception>
    public static IReadOnlyList<ZuletztGeoeffnet> Hinzufuegen(string basis, ZuletztGeoeffnet eintrag)
    {
        ArgumentException.ThrowIfNullOrEmpty(basis);
        ArgumentNullException.ThrowIfNull(eintrag);
        List<ZuletztGeoeffnet> liste =
        [
            eintrag,
            .. Laden(basis).Where(e => !string.Equals(e.Pfad, eintrag.Pfad, StringComparison.OrdinalIgnoreCase)),
        ];
        liste = liste.Take(Hoechstzahl).ToList();
        Speichern(basis, liste);
        return liste;
    }

    /// <summary>Entfernt eine Staffel aus der Liste und speichert sie; die Datei der Staffel selbst bleibt unberührt.</summary>
    /// <param name="basis">Der eigene Ordner.</param>
    /// <param name="pfad">Pfad der Staffel (Groß-/Kleinschreibung egal).</param>
    /// <returns>Die neue Liste.</returns>
    /// <exception cref="IOException">Die Datei konnte nicht geschrieben werden.</exception>
    public static IReadOnlyList<ZuletztGeoeffnet> Entfernen(string basis, string pfad)
    {
        ArgumentException.ThrowIfNullOrEmpty(basis);
        ArgumentException.ThrowIfNullOrEmpty(pfad);
        List<ZuletztGeoeffnet> liste = Laden(basis).Where(e => !string.Equals(e.Pfad, pfad, StringComparison.OrdinalIgnoreCase)).ToList();
        Speichern(basis, liste);
        return liste;
    }

    private static void Speichern(string basis, List<ZuletztGeoeffnet> liste)
    {
        Directory.CreateDirectory(basis);
        File.WriteAllLines(
            Path.Combine(basis, Dateiname),
            liste.Select(e => string.Join('\t', e.Zeitpunkt.ToString("O", CultureInfo.InvariantCulture), Einzeilig(e.Name), e.Pfad)));
    }

    private static ZuletztGeoeffnet? Eintrag(string zeile)
    {
        string[] teile = zeile.Split('\t');
        return teile.Length == 3 && teile[2].Length > 0
            && DateTime.TryParse(teile[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime zeitpunkt)
            ? new ZuletztGeoeffnet(teile[2], teile[1], zeitpunkt)
            : null;
    }

    private static string Einzeilig(string text) => text.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
}
