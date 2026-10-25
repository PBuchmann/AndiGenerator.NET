// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Mcp;

/// <summary>Die Ansicht Kompaktspieltage.</summary>
/// <param name="Kopf">Kopfzeile.</param>
/// <param name="Rechnet">Die Berechnung läuft.</param>
/// <param name="Fortschritt">Fortschritt der Berechnung.</param>
/// <param name="Plaene">Die Pläne zur Wahl (gewählt mit „▶“).</param>
/// <param name="Erlaeuterung">Erläuterung des gewählten Plans.</param>
/// <param name="Spieltage">Die Spieltage des gewählten Plans.</param>
/// <param name="Hinweise">Hinweise (Verstöße, fehlende Lagen, Bemerkungen der Vereine).</param>
public sealed record Kompaktstand(
    string Kopf,
    bool Rechnet,
    string Fortschritt,
    IReadOnlyList<string> Plaene,
    IReadOnlyList<string> Erlaeuterung,
    IReadOnlyList<string> Spieltage,
    IReadOnlyList<string> Hinweise);
