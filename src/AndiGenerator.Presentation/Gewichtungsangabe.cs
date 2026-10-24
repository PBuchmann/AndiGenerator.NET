// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;

namespace AndiGenerator.Presentation;

/// <summary>Eine Gewichtung mit ihrem Ziel (für die Steuerung von außen, z. B. den MCP-Server).</summary>
/// <param name="Ebene">„Plan“, „Kostenart“, „Mannschaft“ oder „Mannschaft und Kostenart“.</param>
/// <param name="Kostenart">Name der Kostenart (leer bei „Mannschaft“).</param>
/// <param name="Mannschaft">Name der Mannschaft (leer bei „Plan“ und „Kostenart“).</param>
/// <param name="Wert">Die Gewichtung.</param>
/// <param name="Ziel">Das Ziel zum Ändern.</param>
public sealed record Gewichtungsangabe(string Ebene, string Kostenart, string Mannschaft, Gewichtung Wert, Gewichtungsziel Ziel)
{
    /// <summary>Holt den Namen der Stufe, z. B. „sehr hoch“.</summary>
    public string Stufe => Gewichtungsanzeige.Name(Wert);
}
