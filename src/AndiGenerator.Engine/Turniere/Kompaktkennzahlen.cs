// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>Kennzahlen eines Plans aus Kompaktspieltagen.</summary>
/// <param name="Km">Kilometer aller Gäste, hin und zurück (Luftlinie).</param>
/// <param name="Abstand">Größter Unterschied der Zahl gespielter Spiele zwischen zwei Mannschaften nach einem Spieltag.</param>
/// <param name="Gemeinsam">Größte Zahl von Mannschaften, die am selben Tag spielen.</param>
/// <param name="Verstoesse">Verstöße gegen die Meldungen.</param>
/// <param name="OhneHeimspieltag">Mannschaften, die keinen Spieltag ausrichten.</param>
/// <param name="Heimspiele">Größter Unterschied der echten Heimspiele (Spiele in der eigenen Halle).</param>
/// <param name="Vierer">Zahl der 4er-Spieltage.</param>
/// <param name="Dreier">Zahl der 3er-Spieltage.</param>
/// <param name="Einzel">Zahl der Einzelbegegnungen.</param>
public sealed record Kompaktkennzahlen(double Km, int Abstand, int Gemeinsam, int Verstoesse, int OhneHeimspieltag, int Heimspiele, int Vierer, int Dreier, int Einzel);
