// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Engine.Einfach;

/// <summary>Einstellungen des einfachen Modus (<see cref="Schnellplaner"/>).</summary>
/// <param name="Kerne">Rechen-Threads (Standard: alle logischen Prozessoren bis auf einen).</param>
/// <param name="PhaseEinsHoechstens">So lange darf HiGHS höchstens an A und B rechnen; danach zählt der beste gefundene Plan.</param>
/// <param name="ZeitJeProbe">
/// Zeitgrenze je Probe eines Kriteriums mit HiGHS (wie weit es sich bei festem A und B senken lässt); 0 = keine Proben (Standard:
/// über die ganze Saison brachten sie in 15 s nichts).
/// </param>
/// <param name="Vorglaetten">So lange glättet die Grundeinteilung mit allen Kernen, bevor die Kandidaten abzweigen.</param>
/// <param name="ErsteRunde">Rechenzeit je Kandidat in der ersten Runde des Rennens (jede weitere doppelt so lang).</param>
/// <param name="LaengsteRunde">Längste Runde (danach wird nur noch bewertet und weitergerechnet).</param>
/// <param name="Laufzeit">Zeitgrenze nach Fortschritt für die Schritte von HiGHS (A und B).</param>
/// <param name="Startwert">Startwert der Zufallsfolgen, oder <c>null</c>.</param>
/// <param name="Toleranz">
/// Nach der ersten Wahl: so viel dürfen die Kriterien schlechter werden als im gewählten Plan (Anteil, mindestens ein Verstoß);
/// jede weitere Stufe halbiert sie.
/// </param>
/// <param name="Schwelle">
/// In Stufe 1 so weit müssen Vorschläge auseinanderliegen (gewichtete Gruppenunterschiede, Einheit der Güte); jede weitere Stufe
/// halbiert sie.
/// </param>
public sealed record Schnellplanereinstellungen(
    int Kerne,
    TimeSpan PhaseEinsHoechstens,
    TimeSpan ZeitJeProbe,
    TimeSpan Vorglaetten,
    TimeSpan ErsteRunde,
    TimeSpan LaengsteRunde,
    Laufzeitregel Laufzeit,
    int? Startwert = null,
    double Toleranz = 0.3,
    double Schwelle = 0.4)
{
    /// <summary>Gets die Standardeinstellungen.</summary>
    public static Schnellplanereinstellungen Standard { get; } = new(
        Kerne: Math.Max(1, Environment.ProcessorCount - 1),
        PhaseEinsHoechstens: TimeSpan.FromMinutes(3),
        ZeitJeProbe: TimeSpan.Zero,
        Vorglaetten: TimeSpan.FromSeconds(45),
        ErsteRunde: TimeSpan.FromSeconds(30),
        LaengsteRunde: TimeSpan.FromMinutes(4),
        Laufzeit: new Laufzeitregel(TimeSpan.FromSeconds(20), TimeSpan.FromMinutes(3), TimeSpan.FromSeconds(15)));
}
