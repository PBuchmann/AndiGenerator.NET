// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>Ein 4er-Spieltag (oder eine Einzelbegegnung) der vollständigen Suche, noch ohne Sonntag.</summary>
/// <param name="form">Vierer oder Einzel.</param>
/// <param name="mannschaften">Mannschaften, Ausrichter zuerst.</param>
/// <param name="paare">Abgedeckte Paarungen als Bitmaske.</param>
/// <param name="teamMaske">Beteiligte Mannschaften als Bitmaske.</param>
/// <param name="km">Kilometer der Gäste, hin und zurück.</param>
/// <param name="tage">Sonntage ohne Verstoß gegen die Meldungen als Bitmaske.</param>
internal sealed class Viererblock(Turnierform form, int[] mannschaften, ulong paare, int teamMaske, double km, ulong tage)
{
    public Turnierform Form { get; } = form;

    public int[] Mannschaften { get; } = mannschaften;

    public ulong Paare { get; } = paare;

    public int TeamMaske { get; } = teamMaske;

    public double Km { get; } = km;

    public ulong Tage { get; } = tage;

    public bool IstEinzel => Form == Turnierform.Einzel;

    public int Ausrichter => Mannschaften[0];
}
