// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Schritt einer Führung „Schritt für Schritt“.</summary>
/// <param name="Titel">Kurzer Titel, z. B. „Generierung starten“.</param>
/// <param name="Text">Was zu tun ist und worauf zu achten ist.</param>
/// <param name="Ziel">Das Element der Oberfläche, auf das der Schritt zeigt (siehe <see cref="Fuehrungsziel"/>), oder <c>null</c>.</param>
/// <param name="Abschnitt">Der Abschnitt der Anleitung, der den Schritt ausführlich beschreibt, z. B. „5.7“.</param>
public sealed record Fuehrungsschritt(string Titel, string Text, string? Ziel, string Abschnitt)
{
    /// <summary>
    /// Holt eine Bedingung, die den Schritt als erledigt erkennt (dann geht die Führung von selbst weiter), oder <c>null</c>
    /// für Schritte, die man mit „Weiter“ abschließt.
    /// </summary>
    public Func<HauptfensterViewModel, bool>? Erledigt { get; init; }

    /// <summary>Holt, was vor dem Zeigen geschehen muss (z. B. die Ansicht öffnen, in der das Ziel steht), oder <c>null</c>.</summary>
    public Action<HauptfensterViewModel>? Vorbereiten { get; init; }
}
