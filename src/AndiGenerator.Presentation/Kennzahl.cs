// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Beschriftete Kennzahl, z. B. eine Plan-Kostenart.</summary>
/// <param name="Name">Beschriftung.</param>
/// <param name="Wert">Wert mit Gewichtungsmarke und Klickziel.</param>
public sealed record Kennzahl(string Name, Kostenfeld Wert);
