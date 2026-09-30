// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Kern;

/// <summary>Austausch der gemerkten (besten) Lösung mit anderen Plänen derselben Stammdaten (Inselmodell, E6).</summary>
internal sealed partial class KernPlan
{
    /// <summary>Schreibt die gemerkte Lösung (Original <c>FDateRef</c> usw.) in <paramref name="ziel"/>.</summary>
    public void LoesungSchreiben(Loesung ziel)
    {
        Array.Copy(gemerktDatum, ziel.Datum, anzahlErlaubt);
        Array.Copy(gemerktOptionen, ziel.Optionen, anzahlErlaubt);
        Array.Copy(gemerktMaxHeim, ziel.MaxHeim, anzahlErlaubt);
        Array.Copy(gemerktWunsch, ziel.Wunsch, anzahlErlaubt);
        Array.Copy(gemerktNichtNotwendig, ziel.NichtNotwendig, anzahlErlaubt);
    }

    /// <summary>Übernimmt <paramref name="quelle"/> als gemerkte Lösung (Original <c>DoReInitPlan</c> eines Workers).</summary>
    public void LoesungUebernehmen(Loesung quelle)
    {
        Array.Copy(quelle.Datum, gemerktDatum, anzahlErlaubt);
        Array.Copy(quelle.Optionen, gemerktOptionen, anzahlErlaubt);
        Array.Copy(quelle.MaxHeim, gemerktMaxHeim, anzahlErlaubt);
        Array.Copy(quelle.Wunsch, gemerktWunsch, anzahlErlaubt);
        Array.Copy(quelle.NichtNotwendig, gemerktNichtNotwendig, anzahlErlaubt);
    }
}
