// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Persistence.Tests;

public class GewichtungTests
{
    [Theory]
    [InlineData(PlanKostenart.UeberlappungSpieltage)]
    [InlineData(PlanKostenart.LaengeSpieltage)]
    [InlineData(PlanKostenart.UeberlappungLetzterSpieltag)]
    [InlineData(PlanKostenart.LaengeLetzterSpieltag)]
    [InlineData(PlanKostenart.VereinsinterneSpieleAmAnfang)]
    public void Plan_Kostenart_wird_einzeln_gesetzt(PlanKostenart art)
    {
        var ziel = new PlanGewichtungsziel(art);
        Berechnungsoptionen neu = ziel.Setzen(Berechnungsoptionen.Standard, Gewichtung.SehrHoch);

        Assert.Equal(Gewichtung.SehrHoch, ziel.Lesen(neu));
        int geaendert = Enum.GetValues<PlanKostenart>().Count(a => new PlanGewichtungsziel(a).Lesen(neu) != Gewichtung.Normal);
        Assert.Equal(1, geaendert);
    }

    [Fact]
    public void Kostenart_fuer_alle_Mannschaften()
    {
        var ziel = new KostenartGewichtungsziel(MannschaftsKostenart.Sperrtermine);
        Berechnungsoptionen neu = ziel.Setzen(Berechnungsoptionen.Standard, Gewichtung.Wenig);

        Assert.Equal(Gewichtung.Wenig, neu.Fuer(MannschaftsKostenart.Sperrtermine));
        Assert.Equal(Berechnungsoptionen.AnzahlKostenarten - 1, neu.JeKostenart.Count(g => g == Gewichtung.Normal));
        Assert.All(Berechnungsoptionen.Standard.JeKostenart, g => Assert.Equal(Gewichtung.Normal, g));
        Assert.Equal("Sperrtermine", ziel.Beschriftung);
    }

    [Fact]
    public void Mannschaft_wird_bei_Bedarf_angelegt_und_danach_geaendert()
    {
        var gesamt = new MannschaftsGewichtungsziel("4711", "TTC Test");
        var detail = new MannschaftsKostenartGewichtungsziel("4711", "TTC Test", MannschaftsKostenart.DreiTageAbstand);
        Assert.Equal(Gewichtung.Normal, gesamt.Lesen(Berechnungsoptionen.Standard));
        Assert.Equal(Gewichtung.Normal, detail.Lesen(Berechnungsoptionen.Standard));

        Berechnungsoptionen neu = detail.Setzen(Berechnungsoptionen.Standard, Gewichtung.ExtremHoch);
        neu = gesamt.Setzen(neu, Gewichtung.NichtBeruecksichtigen);

        MannschaftsGewichtung eintrag = Assert.Single(neu.Mannschaften);
        Assert.Equal("4711", eintrag.MannschaftsId);
        Assert.Equal(Gewichtung.NichtBeruecksichtigen, eintrag.Gesamt);
        Assert.Equal(Gewichtung.ExtremHoch, eintrag.JeKostenart[(int)MannschaftsKostenart.DreiTageAbstand]);
        Assert.Equal(Berechnungsoptionen.AnzahlKostenarten - 1, eintrag.JeKostenart.Count(g => g == Gewichtung.Normal));
        Assert.Equal(Gewichtung.ExtremHoch, detail.Lesen(neu));
    }

    [Theory]
    [InlineData(new[] { Gewichtung.Normal, Gewichtung.Normal, Gewichtung.Normal }, 0, "")]
    [InlineData(new[] { Gewichtung.Hoch, Gewichtung.SehrWenig, Gewichtung.Normal }, -1, "-1")]
    [InlineData(new[] { Gewichtung.ExtremHoch, Gewichtung.SehrHoch }, 5, "+5")]
    [InlineData(new[] { Gewichtung.ExtremHoch, Gewichtung.NichtBeruecksichtigen, Gewichtung.Hoch }, Gewichtungsanzeige.Ignoriert, "X")]
    public void Anzeige_summiert_wie_das_Original(Gewichtung[] stufen, int wert, string markierung)
    {
        Assert.Equal(wert, Gewichtungsanzeige.Wert(stufen));
        Assert.Equal(markierung, Gewichtungsanzeige.Markierung(Gewichtungsanzeige.Wert(stufen)));
    }

    [Fact]
    public void Qualitaet_zaehlt_Verstoesse_je_Kriterium()
    {
        Kostenzelle[] Zellen(int sperrAnzahl, int sperrGesamt)
        {
            var zellen = Enumerable.Repeat(new Kostenzelle(-1, -1, 0), Berechnungsoptionen.AnzahlKostenarten).ToArray();
            zellen[(int)MannschaftsKostenart.Sperrtermine] = new Kostenzelle(sperrAnzahl, sperrGesamt, sperrAnzahl * 100.0);
            zellen[(int)MannschaftsKostenart.Hallenbelegung] = new Kostenzelle(0, -1, 0);
            return zellen;
        }

        var bewertung = new Planbewertung(
            2e10 + 500,
            0,
            2e10,
            0,
            0,
            0,
            0,
            0,
            0,
            [MannschaftsKostenart.Sperrtermine],
            [
                new Mannschaftsbewertung("A", Zellen(2, 10), 200, []),
                new Mannschaftsbewertung("B", Zellen(1, 5), 100, []),
            ]);

        IReadOnlyList<Qualitaetskriterium> kriterien = Planqualitaet.Kriterien(bewertung);

        Qualitaetskriterium ohneTermin = kriterien.Single(k => k.Name == "Spiele ohne Termin");
        Assert.Equal(0, ohneTermin.Anzahl);
        Qualitaetskriterium ungueltig = kriterien.Single(k => k.Name == "Spiele mit ungültigem Termin");
        Assert.Equal(2, ungueltig.Anzahl);
        Assert.False(ungueltig.Erfuellt);
        Assert.True(ungueltig.IstPflicht);

        Qualitaetskriterium sperr = kriterien.Single(k => k.Stufe == "B1");
        Assert.Equal(3, sperr.Anzahl);
        Assert.Equal(15, sperr.Gesamtzahl);
        Assert.Equal(300.0, sperr.Kosten);

        Qualitaetskriterium halle = kriterien.Single(k => k.Stufe == "A2");
        Assert.True(halle.Erfuellt);
        Assert.Null(kriterien.Single(k => k.Stufe == "B2").Anzahl);
        Assert.DoesNotContain(kriterien, k => k.Stufe == "C" && k.Name == "Sperrtermine");
    }
}
