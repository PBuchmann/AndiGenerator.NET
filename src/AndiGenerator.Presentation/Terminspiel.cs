// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Ein Spiel der Terminplanansicht.</summary>
/// <param name="Nummer">Laufende Nummer im Plan (Auswahl).</param>
/// <param name="Tag">Tag kurz, z. B. „Sa 19.09.“, oder „ohne Termin“.</param>
/// <param name="Datum">Datum lang, z. B. „Samstag, 19.09.2026“.</param>
/// <param name="Zeit">Uhrzeit oder leer.</param>
/// <param name="Heim">Heimmannschaft.</param>
/// <param name="Gast">Gastmannschaft.</param>
/// <param name="Runde">Runde, z. B. „Vorrunde“.</param>
/// <param name="Chips">Die Hinweise.</param>
/// <param name="Nachbarn">Spiele der Nachbar- und Vereinsmannschaften am selben Tag (beide Mannschaften).</param>
public sealed record Terminspiel(
    int Nummer, string Tag, string Datum, string Zeit, string Heim, string Gast, string Runde, IReadOnlyList<Hinweischip> Chips, IReadOnlyList<Nachbarspiel> Nachbarn)
{
    /// <summary>Holt einen Wert, der angibt, ob es Hinweise gibt.</summary>
    public bool HatChips => Chips.Count > 0;

    /// <summary>Holt einen Wert, der angibt, ob es Spiele von Nachbar- oder Vereinsmannschaften am selben Tag gibt.</summary>
    public bool HatNachbarn => Nachbarn.Count > 0;

    /// <summary>Holt einen Wert, der angibt, ob ein Hinweis ein Verstoß ist.</summary>
    public bool HatProblem => Chips.Any(c => c.Art == Hinweisart.Problem);
}
