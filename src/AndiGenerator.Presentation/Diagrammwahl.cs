// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Rendering;

namespace AndiGenerator.Presentation;

/// <summary>Eine Karte der Diagrammauswahl.</summary>
/// <param name="Teil">Das Diagramm.</param>
/// <param name="Titel">Titel der Karte.</param>
/// <param name="Beschreibung">Was das Diagramm zeigt, in einem Satz.</param>
/// <param name="Aktiv">Die Karte ist gewählt.</param>
public sealed record Diagrammwahl(Diagrammteil Teil, string Titel, string Beschreibung, bool Aktiv)
{
    /// <summary>Titel eines Diagramms.</summary>
    /// <param name="teil">Das Diagramm.</param>
    /// <returns>Der Titel.</returns>
    public static string TitelVon(Diagrammteil teil) => teil switch
    {
        Diagrammteil.Spieltage => "Spieltage",
        Diagrammteil.WechselHeimAuswaerts => "Wechsel Heim/Auswärts",
        Diagrammteil.Spielverteilung => "Spielverteilung",
        Diagrammteil.AbstandHeimAuswaerts => "Abstand Hin-/Rückspiel",
        Diagrammteil.SpieleProWoche => "Spiele pro Woche",
        _ => "Setzliste",
    };

    /// <summary>Beschreibung eines Diagramms.</summary>
    /// <param name="teil">Das Diagramm.</param>
    /// <returns>Ein Satz.</returns>
    public static string BeschreibungVon(Diagrammteil teil) => teil switch
    {
        Diagrammteil.Spieltage => "Wann jede Mannschaft spielt; Überlappungen der Spieltage sind grau hinterlegt.",
        Diagrammteil.WechselHeimAuswaerts => "Folge der Heim- und Auswärtsspiele je Mannschaft; lange Serien fallen sofort auf.",
        Diagrammteil.Spielverteilung => "Die Termine jeder Mannschaft über die Saison und ihre Abstände zueinander.",
        Diagrammteil.AbstandHeimAuswaerts => "Wie weit Heim- und Auswärtsspiel gegen denselben Gegner auseinanderliegen.",
        Diagrammteil.SpieleProWoche => "Wie viele Spiele jede Mannschaft je Woche hat.",
        _ => "Abgleich des Plans mit der Setzliste: wann die gesetzten Mannschaften aufeinandertreffen.",
    };
}
