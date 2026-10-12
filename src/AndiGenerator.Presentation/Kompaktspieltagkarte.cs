// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Karte eines Kompaktspieltags.</summary>
/// <param name="Nummer">Laufende Nummer (für die Auswahl).</param>
/// <param name="Datum">z. B. „So 06.09.2026“.</param>
/// <param name="Form">„3er“, „4er“ oder „Einzel“.</param>
/// <param name="Ausrichter">z. B. „★ ESV Engers“.</param>
/// <param name="Km">Kilometer der Gäste als Text.</param>
/// <param name="Spiele">Je Spiel „11:00  Engers – Konz“.</param>
/// <param name="Verlegen">Hinweis zur Halle in click-TT oder leer.</param>
/// <param name="Gaeste">z. B. „Konz, Trier-Olewig, Wirges reisen an“.</param>
/// <param name="Hinweise">Verstöße gegen die Meldungen.</param>
/// <param name="Gewaehlt">Spieltag ist ausgewählt.</param>
public sealed record Kompaktspieltagkarte(
    int Nummer,
    string Datum,
    string Form,
    string Ausrichter,
    string Km,
    IReadOnlyList<string> Spiele,
    string Verlegen,
    string Gaeste,
    IReadOnlyList<string> Hinweise,
    bool Gewaehlt)
{
    /// <summary>Holt einen Wert, der angibt, ob es Verstöße gibt.</summary>
    public bool HatHinweise => Hinweise.Count > 0;

    /// <summary>Holt einen Wert, der angibt, ob in click-TT eine Halle zu verlegen ist.</summary>
    public bool HatVerlegen => Verlegen.Length > 0;

    /// <summary>Holt die Marke oben rechts, z. B. „4er“ oder „4er · Verstoß“.</summary>
    public string Marke => HatHinweise ? Form + " · Verstoß" : Form;
}
