// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Presentation;

/// <summary>Ein Problem des Plans als Satz, z. B. „<b>TSG Linden:</b> 8 Spiele auf Sperrterminen“.</summary>
/// <param name="Marke">„MUSS“, „SOLL“ oder „GÜTE“.</param>
/// <param name="Stufe">Die Stufe (Farbe der Marke).</param>
/// <param name="Wer">Die Mannschaften (fett), leer für den ganzen Plan.</param>
/// <param name="Was">Was nicht passt.</param>
public sealed record Normalproblem(string Marke, Stufe Stufe, string Wer, string Was)
{
    /// <summary>Gets die Mannschaften mit folgendem Leerzeichen (leer für den ganzen Plan), für die Anzeige vor <see cref="Was"/>.</summary>
    public string WerVorneweg => Wer.Length > 0 ? Wer + " " : string.Empty;
}
