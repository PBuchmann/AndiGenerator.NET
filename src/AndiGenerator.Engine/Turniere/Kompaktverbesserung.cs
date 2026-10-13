// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Verbessert einen gefundenen Plan, ohne die Kilometer zu ändern: Spieltage auf andere Sonntage verlegen (für den
/// Stand oder einen gemeinsamen Spieltag) und das Heimrecht von Einzelbegegnungen tauschen (echte Heimspiele
/// gleichmäßig). Verstöße gegen die Meldungen werden nicht mehr.
/// </summary>
internal static class Kompaktverbesserung
{
    private const int Runden = 20;

    /// <summary>Ziel „gleichmäßiger Stand“: kleiner ist besser.</summary>
    public static int Stand(Kompaktloesung a, Kompaktloesung b) =>
        (a.Verstoesse, a.Abstand, a.SummeAbstand, -a.Gemeinsam).CompareTo((b.Verstoesse, b.Abstand, b.SummeAbstand, -b.Gemeinsam));

    /// <summary>Ziel „gemeinsamer Spieltag“: kleiner ist besser.</summary>
    public static int Gemeinsam(Kompaktloesung a, Kompaktloesung b) =>
        (a.Verstoesse, -a.Gemeinsam, a.Abstand, a.SummeAbstand).CompareTo((b.Verstoesse, -b.Gemeinsam, b.Abstand, b.SummeAbstand));

    /// <summary>Verlegt Spieltag um Spieltag auf den Sonntag, der das Ziel am meisten verbessert, bis nichts mehr hilft.</summary>
    public static Kompaktloesung TageVerteilen(Kompaktmodell modell, Kompaktloesung loesung, Comparison<Kompaktloesung> ziel)
    {
        Kompaktloesung beste = loesung;
        for (int runde = 0; runde < Runden; runde++)
        {
            Kompaktloesung? besser = BesterZug(modell, beste, ziel);
            if (besser is null)
            {
                break;
            }

            beste = besser;
        }

        return beste;
    }

    /// <summary>
    /// Tauscht das Heimrecht von Einzelbegegnungen, solange es besser wird: zuerst sollen möglichst alle Mannschaften
    /// wenigstens ein Heimspiel haben (wer keinen Spieltag ausrichtet, bekommt so sein Einzel zuhause), dann die echten
    /// Heimspiele gleichmäßig verteilt, dann die Fahrten kurz sein (Einzelfahrten doppelt gezählt).
    /// </summary>
    public static Kompaktloesung EinzelAusgleichen(Kompaktmodell modell, Kompaktloesung loesung)
    {
        Kompaktloesung beste = loesung;
        bool verbessert = true;
        for (int runde = 0; verbessert && runde < Runden; runde++)
        {
            (beste, verbessert) = EinzelRunde(modell, beste);
        }

        return beste;
    }

    private static (Kompaktloesung Loesung, bool Verbessert) EinzelRunde(Kompaktmodell modell, Kompaktloesung loesung)
    {
        Kompaktloesung beste = loesung;
        bool verbessert = false;
        for (int i = 0; i < beste.Turniere.Length; i++)
        {
            Kompaktkandidat k = beste.Turniere[i];
            if (!k.IstEinzel)
            {
                continue;
            }

            Kompaktkandidat getauscht = modell.Kandidat(Turnierform.Einzel, [k.Mannschaften[1], k.Mannschaften[0]], k.Tag);
            if (getauscht.Verstoesse > k.Verstoesse)
            {
                continue;
            }

            var turniere = (Kompaktkandidat[])beste.Turniere.Clone();
            turniere[i] = getauscht;
            var probe = new Kompaktloesung(modell, turniere);
            if ((probe.OhneHeimspiel, probe.Heimspiele, probe.Bewertungskm).CompareTo((beste.OhneHeimspiel, beste.Heimspiele, beste.Bewertungskm)) < 0)
            {
                beste = probe;
                verbessert = true;
            }
        }

        return (beste, verbessert);
    }

    private static Kompaktloesung? BesterZug(Kompaktmodell modell, Kompaktloesung loesung, Comparison<Kompaktloesung> ziel)
    {
        Kompaktloesung? beste = null;
        Kompaktkandidat[] turniere = loesung.Turniere;
        for (int i = 0; i < turniere.Length; i++)
        {
            for (int tag = 0; tag < modell.T; tag++)
            {
                Kompaktloesung? probe = Verlegen(modell, turniere, i, tag);
                if (probe is not null && ziel(probe, beste ?? loesung) < 0)
                {
                    beste = probe;
                }
            }
        }

        return beste;
    }

    private static Kompaktloesung? Verlegen(Kompaktmodell modell, Kompaktkandidat[] turniere, int index, int tag)
    {
        Kompaktkandidat k = turniere[index];
        if (tag == k.Tag || !Frei(turniere, index, tag))
        {
            return null;
        }

        int verstoesse = modell.Verstoesse(k.Form, k.Mannschaften, tag);
        if (verstoesse > k.Verstoesse)
        {
            return null;
        }

        var neu = (Kompaktkandidat[])turniere.Clone();
        neu[index] = k.AmTag(tag, verstoesse);
        return new Kompaktloesung(modell, neu);
    }

    /// <summary>Ob keine Mannschaft des Spieltags am Tag schon spielt.</summary>
    private static bool Frei(Kompaktkandidat[] turniere, int index, int tag)
    {
        int[] mannschaften = turniere[index].Mannschaften;
        for (int j = 0; j < turniere.Length; j++)
        {
            if (j != index && turniere[j].Tag == tag && turniere[j].Mannschaften.Intersect(mannschaften).Any())
            {
                return false;
            }
        }

        return true;
    }
}
