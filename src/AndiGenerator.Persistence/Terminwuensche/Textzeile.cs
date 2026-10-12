// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Persistence.Terminwuensche;

/// <summary>Eine Zeile einer PDF-Seite: Wörter mit gleicher Höhe, von links nach rechts.</summary>
/// <param name="Seite">Seite ab 0.</param>
/// <param name="Oben">Abstand der Grundlinie der Zeile vom oberen Seitenrand (in Punkt).</param>
/// <param name="Woerter">Die Wörter von links nach rechts.</param>
public sealed record Textzeile(int Seite, double Oben, IReadOnlyList<Textwort> Woerter)
{
    /// <summary>Holt den Text der Zeile (Wörter mit Leerzeichen).</summary>
    public string Text { get; } = string.Join(' ', Woerter.Select(w => w.Text));

    /// <summary>Text der Wörter, deren linke Kante in einer Spalte liegt.</summary>
    /// <param name="von">Linke Kante der Spalte.</param>
    /// <param name="bis">Linke Kante der nächsten Spalte.</param>
    /// <returns>Die Wörter der Spalte mit Leerzeichen.</returns>
    public string Spalte(double von, double bis) =>
        string.Join(' ', Woerter.Where(w => w.Links >= von - 1 && w.Links < bis - 1).Select(w => w.Text));
}
