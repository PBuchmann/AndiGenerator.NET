// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Application;

/// <summary>Momentaufnahme einer laufenden Generierung (Anzeige „N Pläne wurden berechnet (X Pläne/s)“ im Original).</summary>
/// <param name="Laeuft">Eine Generierung ist gestartet.</param>
/// <param name="Pausiert">Sie ist angehalten.</param>
/// <param name="Durchlaeufe">Berechnete Pläne seit dem Start.</param>
/// <param name="PlaeneProSekunde">Pläne je Sekunde seit Start bzw. Ende der letzten Pause.</param>
/// <param name="Kosten">Kosten des besten Plans.</param>
/// <param name="Verbesserungen">Anzahl der Verbesserungen des besten Plans.</param>
/// <param name="SeitLetzterVerbesserung">Zeit seit der letzten Verbesserung.</param>
/// <param name="SpezialKostenart">Kostenart der Spezial-Insel, wenn sie läuft.</param>
/// <param name="Spiele">Der beste Plan (alle Spiele; <see cref="Spiel.Zeitpunkt"/> = <c>null</c> für nicht terminierte).</param>
/// <param name="Lenkung">Stand des Automodus; <c>null</c>, wenn er nicht läuft.</param>
public sealed record Optimierungsstand(
    bool Laeuft,
    bool Pausiert,
    long Durchlaeufe,
    double PlaeneProSekunde,
    double Kosten,
    int Verbesserungen,
    TimeSpan SeitLetzterVerbesserung,
    MannschaftsKostenart? SpezialKostenart,
    IReadOnlyList<Spiel> Spiele,
    Lenkungsstand? Lenkung = null)
{
    /// <summary>Stand ohne laufende Generierung.</summary>
    public static Optimierungsstand Leer { get; } = new(false, false, 0, 0, -1, 0, TimeSpan.Zero, null, []);
}
