// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Presentation;

/// <summary>Zelle im Raster der Kompaktspieltage: Meldung einer Mannschaft an einem Sonntag und ihre Rolle im Plan.</summary>
/// <param name="Meldung">Art der Meldung: <c>heim</c>, <c>auswaerts</c>, <c>beides</c>, <c>nicht</c> oder <c>unbekannt</c>.</param>
/// <param name="Zeichen">Inhalt: ★ Ausrichter, ● Gast, ✗ kann nicht, ? keine Meldung, sonst leer.</param>
/// <param name="Verstoss">Die Rolle im Plan widerspricht der Meldung.</param>
/// <param name="Gedimmt">Gehört nicht zur gewählten Mannschaft.</param>
/// <param name="Hinweis">Tooltip.</param>
public sealed record Kompaktzelle(string Meldung, string Zeichen, bool Verstoss, bool Gedimmt, string Hinweis)
{
    /// <summary>Holt einen Wert, der angibt, ob nur Heim gemeldet ist.</summary>
    public bool IstHeim => Meldung == "heim";

    /// <summary>Holt einen Wert, der angibt, ob nur auswärts gemeldet ist.</summary>
    public bool IstAuswaerts => Meldung == "auswaerts";

    /// <summary>Holt einen Wert, der angibt, ob Heim und auswärts gemeldet sind.</summary>
    public bool IstBeides => Meldung == "beides";

    /// <summary>Holt einen Wert, der angibt, ob die Mannschaft nicht kann.</summary>
    public bool IstNicht => Meldung == "nicht";

    /// <summary>Holt einen Wert, der angibt, ob keine Meldung vorliegt.</summary>
    public bool IstUnbekannt => Meldung == "unbekannt";

    /// <summary>Holt einen Wert, der angibt, ob die Mannschaft an diesem Tag spielt (★ oder ●).</summary>
    public bool Spielt => Zeichen is "★" or "●";
}
