// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Eine Zeile im Vergleich des aktuellen mit dem gesicherten Plan.</summary>
/// <param name="Bezeichnung">Was verglichen wird, z. B. „Stufe A“ oder „A2 · Hallenbelegung“.</param>
/// <param name="Aktuell">Wert des aktuellen Plans.</param>
/// <param name="Gesichert">Wert des gesicherten Plans.</param>
/// <param name="AktuellBesser">Der aktuelle Plan ist hier besser.</param>
/// <param name="GesichertBesser">Der gesicherte Plan ist hier besser.</param>
public sealed record Vergleichszeile(string Bezeichnung, string Aktuell, string Gesichert, bool AktuellBesser, bool GesichertBesser);
