// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;

namespace AndiGenerator.Application;

/// <summary>
/// Hinweise beim ersten Öffnen einer click-TT-Datei (noch ohne <c>.modifications</c>), Texte und Bedingungen wie im
/// Original <c>TDialogFirstStart</c> (<c>UpdateTexts</c>, <c>HasData</c>).
/// </summary>
public static class Erststart
{
    private const string Absatz = "\n\n";

    private const string Eingabefehler =
        "Alle möglichen Optionen können in Click-TT nicht eingegeben werden und die Mannschaften machen hier auch gerne mal Eingabefehler."
        + Absatz + "Bitte überprüfen Sie die Eingaben der Mannschaften.";

    private const string SpiellokaleText =
        "Die Spiellokale werden von Click-TT über die Schnittstelle nicht übertragen."
        + Absatz + "Falls Sie Vereine mit mehreren Spiellokalen in Ihrer Runde haben, so müssen die Spiellokale bei den "
        + "Mannschaften und Nachbarmannschaften angegeben werden, sonst liefert die Berechnung der Hallenbelegung fehlerhafte Ergebnisse."
        + Absatz + "Falls alle Vereine nur ein Spiellokal haben, so kann dieser Punkt übersprungen werden."
        + "\nFalls die Vereine keine Beschränkung bei der Anzahl der Heimspiele angegeben haben, so kann der Punkt ebenfalls übersprungen werden.";

    private const string SetzlistenText =
        "Mit der Setzliste können Sie die erwartete Tabelle am Ende der Saison vorgeben."
        + Absatz + "Das Ziel ist, dass am Ende der Saison die stärksten und schwächsten Mannschaften gegeneinander spielen sollen. "
        + "Damit soll verhindert werden, dass in den wichtigen Spielen um den Auf- und Abstieg Mannschaften beteiligt sind, bei denen es um nichts mehr geht."
        + Absatz + "Mit der Setzliste soll die Spannung bis zum Schluss erhalten bleiben und, falls es so etwas überhaupt geben sollte, Mauscheleien verhindert werden.";

    private const string PflichtspielText =
        "Der Plan enthält Pflichtspieltage."
        + Absatz + "Pflichtspieltage sind vor allem bei Ligen mit ungerader Mannschaftszahl problematisch. In diesem Fall müsste "
        + "eine Mannschaft in einer Woche zwei Spiele machen. Um das zu verhindern sollte der Zeitraums des Pflichtspiels verlängert "
        + "werden oder am Besten die Pflichtspieltage gelöscht werden und stattdessen sollte der letzte Spieltag optimiert werden."
        + Absatz + "Bitte Bearbeiten Sie die Pflichtspieltage.";

    /// <summary>Ermittelt die Hinweise für eine Staffel.</summary>
    /// <param name="staffel">Die Staffel.</param>
    /// <returns>Die zutreffenden Hinweise in der Reihenfolge des Originals.</returns>
    public static IReadOnlyList<Erststarthinweis> Hinweise(Staffel staffel)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        return Enum.GetValues<Erststartpunkt>().Where(p => Zutreffend(staffel, p)).Select(Hinweis).ToList();
    }

    /// <summary>Original <c>HasData</c>: ob ein Punkt für die Staffel angezeigt wird.</summary>
    /// <param name="staffel">Die Staffel.</param>
    /// <param name="punkt">Der Punkt.</param>
    /// <returns><c>true</c>, wenn der Punkt angezeigt wird.</returns>
    public static bool Zutreffend(Staffel staffel, Erststartpunkt punkt)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        return punkt switch
        {
            Erststartpunkt.Koppeltermine => HatKoppeltermine(staffel),
            Erststartpunkt.Auswaertskoppeln => staffel.Mannschaften.Any(m => m.Auswaertskoppeln.Count > 0),
            Erststartpunkt.Pflichtspieltage => staffel.Pflichtspielzeitraeume.Count > 0,
            _ => true,
        };
    }

    /// <summary>Original <c>UpdateTexts</c>: Überschrift, Text und Schaltfläche eines Punkts.</summary>
    /// <param name="punkt">Der Punkt.</param>
    /// <returns>Der Hinweis.</returns>
    public static Erststarthinweis Hinweis(Erststartpunkt punkt) => punkt switch
    {
        Erststartpunkt.Spiellokale => new(
            punkt,
            "Spiellokale",
            SpiellokaleText,
            "Spiellokale bearbeiten"),
        Erststartpunkt.Koppeltermine => new(
            punkt,
            "Koppeltermine",
            "Der Plan enthält Koppelwünsche oder mögliche Doppelspieltage." + Absatz + Eingabefehler,
            "Koppeltermine bearbeiten"),
        Erststartpunkt.Auswaertskoppeln => new(
            punkt,
            "Auswärtskoppelwünsche",
            "Der Plan enthält Auswärtskoppelwünsche." + Absatz + Eingabefehler,
            "Auswärtskoppelwünsche bearbeiten"),
        Erststartpunkt.Setzliste => new(
            punkt,
            "Setzliste",
            SetzlistenText,
            "Setzliste aktivieren"),
        _ => new(
            punkt,
            "Pflichtspieltage",
            PflichtspielText,
            "Pflichtspieltage bearbeiten"),
    };

    /// <summary>Original <c>HasPossibleKoppelTermine</c>: Koppel-/Doppelspieltermin oder zwei Heimspieltermine an aufeinanderfolgenden Tagen.</summary>
    /// <param name="staffel">Die Staffel.</param>
    /// <returns><c>true</c>, wenn es solche Termine gibt.</returns>
    private static bool HatKoppeltermine(Staffel staffel) => staffel.Mannschaften.Any(m =>
        m.Heimspieltermine.Any(t => t.Koppelung != Terminkoppelung.Keine)
        || m.Heimspieltermine.Any(a => m.Heimspieltermine.Any(b => Math.Abs(DateOnly.FromDateTime(a.Zeitpunkt).DayNumber - DateOnly.FromDateTime(b.Zeitpunkt).DayNumber) == 1)));
}
