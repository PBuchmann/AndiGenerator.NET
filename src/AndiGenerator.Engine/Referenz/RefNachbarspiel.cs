// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Referenz;

/// <summary>Original <c>TSisterGame</c>.</summary>
internal sealed class RefNachbarspiel
{
    public double Datum { get; init; }

    public string Geschlecht { get; init; } = string.Empty;

    public int Nummer { get; init; }

    public string Mannschaftsname { get; init; } = string.Empty;

    public bool IstHeimspiel { get; init; }

    public string Heim { get; init; } = string.Empty;

    public string Gast { get; init; } = string.Empty;

    public string Lokal { get; init; } = string.Empty;

    public bool KeineParallelenSpiele { get; init; }

    public bool ParalleleHeimspiele { get; init; }
}
