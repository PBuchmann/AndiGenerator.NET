// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;

namespace AndiGenerator.Engine.Referenz;

/// <summary>Rundenlogik des Originals für Aufrufer außerhalb der Engine (z. B. CSV-Export: „Vor/Rück“).</summary>
public static class Rundenpruefung
{
    /// <summary>Erzeugt die Prüfung „liegt der Termin in einer zu planenden Runde“ (Original <c>TPlan.IsInRoundToGenerate</c>).</summary>
    /// <param name="staffel">Staffel.</param>
    /// <param name="optionen">Berechnungsoptionen (Rundenplanung, Doppelrunde, Rundenmitten).</param>
    /// <returns>Die Prüfung; ein nicht terminiertes Spiel (<c>null</c>) wird wie im Original mit Datum 0 geprüft.</returns>
    public static Func<DateTime?, bool> Erzeugen(Staffel staffel, Berechnungsoptionen optionen)
    {
        ArgumentNullException.ThrowIfNull(staffel);
        ArgumentNullException.ThrowIfNull(optionen);
        RefPlan plan = RefPlan.Laden(staffel with { BestehenderSpielplan = [] }, optionen);
        return zeitpunkt => plan.IstInZuPlanenderRunde(zeitpunkt is DateTime z ? DelphiDatum.Wert(z) : 0.0);
    }
}
