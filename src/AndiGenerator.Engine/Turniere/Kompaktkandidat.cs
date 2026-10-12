// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>Ein möglicher Spieltag der Suche: Form, Mannschaften (Ausrichter zuerst) und Tag als Nummern, abgedeckte Paarungen.</summary>
/// <param name="form">Form.</param>
/// <param name="mannschaften">Mannschaften in der Reihenfolge von <see cref="Planturnier.Mannschaften"/>.</param>
/// <param name="tag">Nummer des Tages.</param>
/// <param name="paare">Nummern der abgedeckten Paarungen.</param>
/// <param name="km">Kilometer der Gäste, hin und zurück.</param>
/// <param name="verstoesse">Verstöße gegen die Meldungen an diesem Tag.</param>
internal sealed class Kompaktkandidat(Turnierform form, int[] mannschaften, int tag, int[] paare, double km, int verstoesse)
{
    public Turnierform Form { get; } = form;

    public int[] Mannschaften { get; } = mannschaften;

    public int Tag { get; } = tag;

    public int[] Paare { get; } = paare;

    public double Km { get; } = km;

    public int Verstoesse { get; } = verstoesse;

    public bool IstEinzel => Form == Turnierform.Einzel;

    public int Ausrichter => Mannschaften[0];

    /// <summary>Derselbe Spieltag an einem anderen Tag.</summary>
    public Kompaktkandidat AmTag(int neuerTag, int neueVerstoesse) => new(Form, Mannschaften, neuerTag, Paare, Km, neueVerstoesse);
}
