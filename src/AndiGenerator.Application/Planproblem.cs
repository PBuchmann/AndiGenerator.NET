// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Application;

/// <summary>Ein Problem eines Plans in einfachen Worten, z. B. „TSG Linden: 8 Spiele auf Sperrterminen“.</summary>
/// <param name="Stufe">A (Muss, Pflicht), B (Soll, Wünsche der Vereine) oder C (Feinschliff).</param>
/// <param name="Kriterium">Das Kriterium, oder <c>null</c> bei harten Fehlern (Spiele ohne gültigen Termin).</param>
/// <param name="Wer">Wen es betrifft (Mannschaften, fett angezeigt), z. B. „TSG Linden:“; leer, wenn es den ganzen Plan betrifft.</param>
/// <param name="Was">Was nicht passt.</param>
/// <param name="Anzahl">Verstöße insgesamt (für die Reihenfolge).</param>
public sealed record Planproblem(Stufe Stufe, Kostenkriterium? Kriterium, string Wer, string Was, int Anzahl)
{
    /// <inheritdoc/>
    public override string ToString() => string.IsNullOrEmpty(Wer) ? Was : Wer + " " + Was;
}
