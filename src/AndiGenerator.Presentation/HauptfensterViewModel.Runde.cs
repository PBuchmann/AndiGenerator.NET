// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Presentation;

/// <summary>Rundenplanung: Punkt der Einrichtung (Original: Rückfrage beim Öffnen) und Vorgabe durch den Klassennamen.</summary>
public sealed partial class HauptfensterViewModel
{
    private const string Rundentitel = "Rundenplanung";

    /// <summary>
    /// Holt die vom Klassennamen vorgegebene Rundenplanung (nicht im Original): „(HR)“ → nur Hinrunde, „(FR)“ → nur
    /// Rückrunde; sonst <c>null</c>.
    /// </summary>
    internal Rundenplanung? VorgegebeneRundenplanung => sitzung?.VorgegebeneRundenplanung;

    /// <summary>Erstellt den Punkt „Rundenplanung“: Halbrunde bzw. nur Rückrunde (Vorschlag) oder die ganze Runde.</summary>
    private static Einrichtungsschritt RundenschrittErstellen(Plansitzung neu, int nummer, Rundenvorschlag vorschlag)
    {
        var ganzeRunde = new Einrichtungsoption("Die ganze Runde", "Vor- und Rückrunde planen");
        return vorschlag == Rundenvorschlag.Halbrunde
            ? new Einrichtungsschritt(
                nummer,
                Rundentitel,
                "Die Runde ist kürzer als 6 Monate. Welche Spiele sollen generiert werden?",
                [],
                [new Einrichtungsoption("Nur eine Halbrunde", "Vorschlag für kurze Runden"), ganzeRunde])
            : new Einrichtungsschritt(
                nummer,
                Rundentitel,
                RueckrundenFrage(neu.Staffel.Rueckrundenbeginn),
                [],
                [new Einrichtungsoption("Nur die Rückrunde", "Vorschlag – die Vorrunde bleibt, wie sie ist"), ganzeRunde]);
    }
}
