// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Engine.Schranke;

/// <summary>Ergebnis von <see cref="Ligaschranke.Berechnen"/>.</summary>
/// <param name="Spalten">Spalten (Entscheidungen) des Modells.</param>
/// <param name="Zeilen">Zeilen (Bedingungen) des Modells.</param>
/// <param name="Stufen">
/// A1 (Spiele ohne Termin), je Kriterium der Stufen A und B seine Schranke allein und sein Wert im besten Plan, dann die
/// Kriterien der Stufe C (bei bestmöglichem A und B).
/// </param>
/// <param name="Hinweise">Vereinfachungen und Gründe, warum etwas nicht berechnet wurde.</param>
/// <param name="Plan">Der beste Plan des vereinfachten Modells (Spiele ohne Termin mit <c>null</c>), oder leer.</param>
/// <param name="SummeA">
/// Die Summe aller Verstöße der Stufe A: So viele hat jeder Plan ohne Spiele ohne Termin mindestens; <c>null</c>, wenn nicht
/// berechnet. So vergleicht auch der Automodus (innerhalb einer Stufe zählt jeder Verstoß gleich).
/// </param>
/// <param name="SummeB">Die Summe aller Verstöße der Stufe B bei bestmöglichem A; <c>null</c>, wenn nicht berechnet.</param>
/// <param name="Alternativen">Je B-Kriterium ein Plan, in dem es bestmöglich ist (bei bestmöglichem A); leer, wenn keine.</param>
/// <param name="Unbekannt">
/// Kriterien der Stufen A und B, die das Modell nicht kennt: Sie fehlen in den Grenzen (die bleiben gültig, sind aber nicht
/// erreichbar, solange der Plan dort Verstöße hat) und in den Varianten; leer, wenn keine.
/// </param>
public sealed record Schrankenbericht(
    int Spalten,
    int Zeilen,
    IReadOnlyList<Schrankenstufe> Stufen,
    IReadOnlyList<string> Hinweise,
    IReadOnlyList<Spiel> Plan,
    Schrankenstufe? SummeA = null,
    Schrankenstufe? SummeB = null,
    IReadOnlyList<Planalternative>? Alternativen = null,
    IReadOnlyList<Kostenkriterium>? Unbekannt = null)
{
    /// <summary>Holt die bewiesene Schranke der Stufe A (Summe); <c>null</c>, wenn sie nicht bewiesen ist.</summary>
    public int? SchrankeA => SummeA is { Bewiesen: true } a && (Stufen.FirstOrDefault(s => s.Stufe == "A1")?.Bewiesen ?? false) ? a.Schranke : null;

    /// <summary>Holt die bewiesene Schranke der Stufe B (Summe, bei bestmöglichem A); <c>null</c>, wenn sie nicht bewiesen ist.</summary>
    public int? SchrankeB => SchrankeA is not null && SummeB is { Bewiesen: true } b ? b.Schranke : null;

    /// <summary>Ob das Modell alle Kriterien der Stufe kennt (sonst gilt „optimal“ nur für die bekannten).</summary>
    /// <param name="einteilung">Die Einteilung, für die der Bericht gilt.</param>
    /// <param name="stufe">A oder B.</param>
    /// <returns><c>true</c>, wenn keines fehlt.</returns>
    public bool Vollstaendig(Stufeneinteilung einteilung, Stufe stufe)
    {
        ArgumentNullException.ThrowIfNull(einteilung);
        return Unbekannt is null || !Unbekannt.Any(k => einteilung.StufeVon(k) == stufe);
    }
}
