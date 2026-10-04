// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Inseln;

namespace AndiGenerator.Application;

/// <summary>Ein Kriterium der Planqualität nach der Rangfolge aus MIGRATIONSPLAN E14.</summary>
/// <param name="Stufe">Stufe, z. B. <c>A1</c>, <c>B1</c> oder <c>C</c>.</param>
/// <param name="Name">Beschriftung.</param>
/// <param name="Anzahl">Zahl der Verstöße; <c>null</c>, wenn die Kostenart keine Anzahl kennt (dann zählen nur die Kosten).</param>
/// <param name="Gesamtzahl">Zahl der Wünsche, gegen die geprüft wurde (z. B. gemeldete Sperrtermine), sonst <c>null</c>.</param>
/// <param name="Kosten">Kosten dieses Kriteriums.</param>
/// <param name="Kriterium">Das einstufbare Kriterium; <c>null</c> bei den harten Fehlern (immer A1).</param>
public sealed record Qualitaetskriterium(string Stufe, string Name, int? Anzahl, int? Gesamtzahl, double Kosten, Kostenkriterium? Kriterium = null)
{
    /// <summary>Holt einen Wert, der angibt, ob das Kriterium ohne Verstoß erfüllt ist.</summary>
    public bool Erfuellt => Anzahl is int anzahl ? anzahl == 0 : Kosten <= 0;

    /// <summary>Holt einen Wert, der angibt, ob es zur Pflichtstufe A gehört.</summary>
    public bool IstPflicht => Stufe.StartsWith('A');
}
