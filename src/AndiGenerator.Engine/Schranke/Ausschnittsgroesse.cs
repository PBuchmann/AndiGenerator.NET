// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Schranke;

/// <summary>Wie erfolgreich die Teilneuplanung mit Ausschnitten einer Größe ist (für den Leitstand).</summary>
/// <param name="Mannschaften">Mannschaften je Ausschnitt.</param>
/// <param name="Ausschnitte">So viele Ausschnitte dieser Größe wurden neu geplant.</param>
/// <param name="Besser">Davon besser (angenommen).</param>
/// <param name="Sekunden">Rechenzeit von HiGHS für diese Ausschnitte zusammen.</param>
public sealed record Ausschnittsgroesse(int Mannschaften, int Ausschnitte, int Besser, double Sekunden);
