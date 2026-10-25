// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Schritt von HiGHS in der Kachel (ein Punkt der Schrittreihe).</summary>
/// <param name="Name">Der Name, z. B. <c>Stufe B</c>.</param>
/// <param name="Fertig">Gibt an, ob der Schritt fertig ist.</param>
/// <param name="Aktiv">Gibt an, ob HiGHS gerade an ihm rechnet.</param>
public sealed record Highsschritt(string Name, bool Fertig, bool Aktiv)
{
    /// <summary>Holt einen Wert, der angibt, ob der Schritt noch kommt.</summary>
    public bool Offen => !Fertig && !Aktiv;

    /// <summary>Holt den Hinweis zum Punkt, z. B. <c>Stufe B: läuft</c>.</summary>
    public string Hinweis => (Fertig, Aktiv) switch
    {
        (true, _) => Name + ": fertig",
        (_, true) => Name + ": läuft",
        _ => Name + ": kommt noch",
    };
}
