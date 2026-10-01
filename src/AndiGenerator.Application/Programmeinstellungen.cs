// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Application;

/// <summary>
/// Einstellungen des Programms, unabhängig von einer Staffel (neu, im Original nicht vorhanden). Gespeichert als kleine
/// Textdatei im eigenen Ordner, eine Zeile <c>Schlüssel=Wert</c> je Einstellung; Unbekanntes wird übergangen.
/// </summary>
/// <param name="UpdatesSuchen">Beim Start auf GitHub nach einer neuen Version suchen.</param>
public sealed record Programmeinstellungen(bool UpdatesSuchen)
{
    /// <summary>Name der Datei im eigenen Ordner.</summary>
    public const string Dateiname = "programm-einstellungen.txt";

    private const string SchluesselUpdates = "updates-suchen";

    /// <summary>Holt die Standardeinstellungen (Update-Suche an).</summary>
    public static Programmeinstellungen Standard { get; } = new(UpdatesSuchen: true);

    /// <summary>Liest die Einstellungen; fehlt die Datei oder ein Wert, gilt der Standard.</summary>
    /// <param name="basis">Der eigene Ordner.</param>
    /// <returns>Die Einstellungen.</returns>
    /// <exception cref="IOException">Die Datei konnte nicht gelesen werden.</exception>
    public static Programmeinstellungen Laden(string basis)
    {
        ArgumentException.ThrowIfNullOrEmpty(basis);
        string pfad = Path.Combine(basis, Dateiname);
        if (!File.Exists(pfad))
        {
            return Standard;
        }

        Programmeinstellungen ergebnis = Standard;
        foreach (string zeile in File.ReadAllLines(pfad))
        {
            string[] teile = zeile.Split('=', 2, StringSplitOptions.TrimEntries);
            if (teile.Length == 2 && teile[0] == SchluesselUpdates && teile[1] is "ja" or "nein")
            {
                ergebnis = ergebnis with { UpdatesSuchen = teile[1] == "ja" };
            }
        }

        return ergebnis;
    }

    /// <summary>Speichert die Einstellungen.</summary>
    /// <param name="basis">Der eigene Ordner.</param>
    /// <exception cref="IOException">Die Datei konnte nicht geschrieben werden.</exception>
    public void Speichern(string basis)
    {
        ArgumentException.ThrowIfNullOrEmpty(basis);
        Directory.CreateDirectory(basis);
        File.WriteAllLines(Path.Combine(basis, Dateiname), [SchluesselUpdates + "=" + (UpdatesSuchen ? "ja" : "nein")]);
    }
}
