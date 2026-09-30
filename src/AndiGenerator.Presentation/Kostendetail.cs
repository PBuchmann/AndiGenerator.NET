// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation;

/// <summary>Details zur Auswahl in der Kostenansicht; ersetzt den Gewichtungsdialog des Originals.</summary>
/// <param name="Art">Art der Auswahl in Großbuchstaben (ZELLE, KOSTENART …).</param>
/// <param name="Titel">Überschrift, z. B. die Kostenart.</param>
/// <param name="Unterzeile">Zusatz, z. B. die Mannschaft.</param>
/// <param name="Verstoesse">Verstöße, z. B. „2 von 178“.</param>
/// <param name="Kosten">Kosten.</param>
/// <param name="Anteil">Anteil an den Gesamtkosten.</param>
/// <param name="GewichtungFuer">Wofür die Gewichtung gilt.</param>
/// <param name="Ziel">Die Gewichtung, die die Tasten ändern; <c>null</c> = keine (z. B. Gesamtkosten).</param>
/// <param name="Stufen">Die Tasten der Gewichtung.</param>
/// <param name="Meldungen">Meldungen bzw. Aufteilung der Kosten.</param>
/// <param name="MeldungenTitel">Überschrift der Meldungen.</param>
/// <param name="IstZelle">Die Auswahl ist eine Zelle (Sprung zu Kostenart und Mannschaft möglich).</param>
public sealed record Kostendetail(
    string Art,
    string Titel,
    string Unterzeile,
    string Verstoesse,
    string Kosten,
    string Anteil,
    string GewichtungFuer,
    Gewichtungsziel? Ziel,
    IReadOnlyList<Gewichtungsstufe> Stufen,
    IReadOnlyList<string> Meldungen,
    string MeldungenTitel,
    bool IstZelle)
{
    /// <summary>Holt einen Wert, der angibt, ob es eine Gewichtung gibt.</summary>
    public bool HatGewichtung => Ziel is not null;

    /// <summary>Holt einen Wert, der angibt, ob es Meldungen gibt.</summary>
    public bool HatMeldungen => Meldungen.Count > 0;
}
