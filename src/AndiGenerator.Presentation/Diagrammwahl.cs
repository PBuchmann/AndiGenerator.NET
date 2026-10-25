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
        Diagrammteil.Ausrichter => "Ausrichter und Spielorte",
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
        Diagrammteil.Spieltage => "Wann jeder Spieltag gespielt wird: Termine, Spanne und Überschneidungen mit anderen Spieltagen.",
        Diagrammteil.Ausrichter => "Wer wie oft ausrichtet und wo jede Mannschaft spielt; bei Kompaktspieltagen zählt nicht der Wechsel.",
        Diagrammteil.WechselHeimAuswaerts => "Heim- und Auswärtsspiele je Mannschaft als Kacheln; ideal ist ein gleichmäßiges Schachbrett, Serien ab drei sind rot unterstrichen.",
        Diagrammteil.Spielverteilung => "Jedes Spiel am genauen Tag: Doppelspieltage, Spiele kurz nacheinander und lange Pausen je Mannschaft.",
        Diagrammteil.AbstandHeimAuswaerts => "Wie viele Spiele zwischen Hin- und Rückspiel gegen jeden Gegner liegen (bei einer Doppelrunde zwischen den vier Spielen); grün ist das Soll (eine Runde ± 2 Spiele).",
        Diagrammteil.SpieleProWoche => "Wie viele Spiele jede Mannschaft je Woche hat.",
        _ => "Abgleich des Plans mit der Setzliste: wann die gesetzten Mannschaften aufeinandertreffen.",
    };
}
