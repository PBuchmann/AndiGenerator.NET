// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Engine.Kern;

/// <summary>
/// Kompakte Lösung: Termin und Terminangaben je erlaubtem Spiel (statt einer ganzen Plan-Kopie wie im Original).
/// Wird zwischen Suchplätzen, Inseln und Optimierer ausgetauscht; nach dem Versand nicht mehr verändert.
/// </summary>
internal sealed class Loesung
{
    public Loesung(int anzahl)
    {
        Datum = new double[anzahl];
        Optionen = new Wunschterminoptionen[anzahl];
        MaxHeim = new int[anzahl];
        Wunsch = new int[anzahl];
        Array.Fill(Wunsch, -1);
        NichtNotwendig = new bool[anzahl];
    }

    public double[] Datum { get; }

    public Wunschterminoptionen[] Optionen { get; }

    public int[] MaxHeim { get; }

    public int[] Wunsch { get; }

    public bool[] NichtNotwendig { get; }

    /// <summary>Unabhängige Kopie.</summary>
    public Loesung Kopie()
    {
        var kopie = new Loesung(Datum.Length);
        KopierenNach(kopie);
        return kopie;
    }

    /// <summary>Überschreibt <paramref name="ziel"/> mit dieser Lösung.</summary>
    public void KopierenNach(Loesung ziel)
    {
        Array.Copy(Datum, ziel.Datum, Datum.Length);
        Array.Copy(Optionen, ziel.Optionen, Datum.Length);
        Array.Copy(MaxHeim, ziel.MaxHeim, Datum.Length);
        Array.Copy(Wunsch, ziel.Wunsch, Datum.Length);
        Array.Copy(NichtNotwendig, ziel.NichtNotwendig, Datum.Length);
    }
}
