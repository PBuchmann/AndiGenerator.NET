// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Inhalt des Tabs „Terminwünsche“ im Original (<c>PaintTerminMeldung</c>).</summary>
/// <param name="Erster">Erster Wunschtermin aller Mannschaften (Beginn der Zeitachse).</param>
/// <param name="Letzter">Letzter Wunschtermin aller Mannschaften.</param>
/// <param name="HatKoppeltermine">Es gibt Koppeltermine oder Doppelspieltage (Legende).</param>
/// <param name="Hinweise">Allgemeine Hinweise, z. B. „Keine Auswärtskoppelwünsche gemeldet“.</param>
/// <param name="Mannschaften">Die Mannschaften in der Reihenfolge des Plans.</param>
public sealed record Terminwunschuebersicht(
    DateOnly? Erster,
    DateOnly? Letzter,
    bool HatKoppeltermine,
    IReadOnlyList<string> Hinweise,
    IReadOnlyList<MannschaftsTerminwuensche> Mannschaften);
