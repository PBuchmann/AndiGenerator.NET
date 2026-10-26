// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>Der gemeinsame Stand paralleler Läufe auf demselben Modell (bester Wert, höchste Schranke).</summary>
internal sealed class Laufstand
{
    private readonly Lock sperre = new();
    private double wert = double.PositiveInfinity;
    private double schranke = double.NegativeInfinity;

    /// <summary>Holt die höchste Schranke aller Läufe.</summary>
    public double Schranke
    {
        get
        {
            lock (sperre)
            {
                return schranke;
            }
        }
    }

    /// <summary>Nimmt den Stand eines Laufs auf (ohne Meldung).</summary>
    /// <param name="s">Der Stand.</param>
    public void Merken(Loeserstand s) => Aufnehmen(s);

    /// <summary>Nimmt den Stand eines Laufs auf.</summary>
    /// <param name="s">Der Stand.</param>
    /// <returns>Der neue gemeinsame Stand, wenn er sich verändert hat; sonst <c>null</c>.</returns>
    public Loeserstand? Aufnehmen(Loeserstand s)
    {
        const double Schritt = 1e-6;
        lock (sperre)
        {
            bool besser = s.Wert < wert - Schritt;
            bool hoeher = double.IsFinite(s.Schranke) && s.Schranke > schranke + Schritt;
            if (!besser && !hoeher)
            {
                return null;
            }

            wert = Math.Min(wert, s.Wert);
            schranke = double.IsFinite(s.Schranke) ? Math.Max(schranke, s.Schranke) : schranke;
            return new Loeserstand(wert, schranke);
        }
    }
}
