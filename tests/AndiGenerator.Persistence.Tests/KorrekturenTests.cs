// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Persistence.Optionen;

namespace AndiGenerator.Persistence.Tests;

/// <summary>Korrekturen der Klasse-3-Befunde (MIGRATIONSPLAN E9, Entscheidung 11.10.2026) und die Regel für den Pflichtspieltag.</summary>
public sealed class KorrekturenTests
{
    [Fact]
    public void Standard_korrigiert_alles_Original_nichts()
    {
        Assert.Equal(Korrekturen.Alle, Berechnungsoptionen.Standard.Korrekturen);
        Assert.True(Berechnungsoptionen.Standard.SpielfreiAmPflichtspieltag);
        Assert.Equal(Korrekturen.Keine, Berechnungsoptionen.Original.Korrekturen);
        Assert.False(Berechnungsoptionen.Original.SpielfreiAmPflichtspieltag);
    }

    [Theory]
    [MemberData(nameof(ParitaetTests.Referenzfaelle), MemberType = typeof(ParitaetTests))]
    public void Die_Regel_fuer_den_Pflichtspieltag_entlastet_hoechstens_eine_Mannschaft_je_Zeitraum(string fall)
    {
        Staffel staffel = ParitaetTests.StaffelLaden(fall);
        int vorher = Pflichtverstoesse(Referenzbewertung.Bewerten(staffel, Berechnungsoptionen.Original));
        int nachher = Pflichtverstoesse(Referenzbewertung.Bewerten(staffel, Berechnungsoptionen.Original with { SpielfreiAmPflichtspieltag = true }));
        if (staffel.Mannschaften.Count % 2 == 0)
        {
            Assert.Equal(vorher, nachher);
        }
        else
        {
            Assert.InRange(vorher - nachher, 0, staffel.Pflichtspielzeitraeume.Count(z => z.AnzahlSpiele == 1));
        }
    }

    [Theory]
    [MemberData(nameof(ParitaetTests.Referenzfaelle), MemberType = typeof(ParitaetTests))]
    public void Summiert_wird_die_Ueberlappung_des_letzten_Spieltags_nie_kleiner(string fall)
    {
        Staffel staffel = ParitaetTests.StaffelLaden(fall);
        Planbewertung original = Referenzbewertung.Bewerten(staffel, Berechnungsoptionen.Original);
        Planbewertung summiert = Referenzbewertung.Bewerten(
            staffel,
            Berechnungsoptionen.Original with { Korrekturen = Korrekturen.Keine with { LetzteUeberlappungSummieren = true } });
        Assert.True(summiert.UeberlappungLetzterSpieltag >= original.UeberlappungLetzterSpieltag);
        Assert.Equal(original.UeberlappungSpieltage, summiert.UeberlappungSpieltage);
    }

    [Fact]
    public void Hallen_aus_click_TT_werden_uebernommen_und_entlasten_die_Hallenbelegung()
    {
        Staffel staffel = ParitaetTests.StaffelLaden("R21");
        Assert.Contains(staffel.Mannschaften.SelectMany(m => m.Heimspieltermine), t => t.Halle.Length > 0);
        Assert.Contains(staffel.Mannschaften.SelectMany(m => m.Nachbarmannschaften).SelectMany(n => n.Spiele), s => s.Halle.Length > 0);
        Planbewertung original = Referenzbewertung.Bewerten(staffel, Berechnungsoptionen.Original);
        Planbewertung nachNamen = Referenzbewertung.Bewerten(
            staffel,
            Berechnungsoptionen.Original with { Korrekturen = Korrekturen.Keine with { HallenNachNamen = true } });
        Assert.True(Hallenbelegungen(nachNamen) <= Hallenbelegungen(original));
    }

    [Fact]
    public void Heimspiel_der_Nachbarmannschaft_in_anderer_Halle_belegt_die_Halle_nicht()
    {
        var termin = new DateTime(2026, 9, 12, 18, 0, 0, DateTimeKind.Unspecified);
        Staffel Staffel(string halleNachbar)
        {
            var nachbar = new Nachbarmannschaft("A II", "Erwachsene", 2, string.Empty, false, false, [new Nachbarspiel(termin, "A II", "X", string.Empty) { Halle = halleNachbar }]);
            Mannschaft a = Team("A", "1") with
            {
                Heimspieltermine = [new Heimspieltermin(termin, 1, false, string.Empty, Terminkoppelung.Keine, Koppelprioritaet.Moeglich, null, false) { Halle = "Halle Nord" }],
                Nachbarmannschaften = [nachbar],
            };
            return Domain.Stammdaten.Staffel.Leer with
            {
                Name = "Test",
                Geschlecht = "Erwachsene",
                Beginn = new DateOnly(2026, 8, 17),
                Ende = new DateOnly(2027, 5, 9),
                Rueckrundenbeginn = new DateOnly(2026, 12, 7),
                Mannschaften = [a, Team("B", "2")],
                BestehenderSpielplan = [new Spiel(termin, "A", "B", string.Empty)],
            };
        }

        Berechnungsoptionen original = Berechnungsoptionen.Original with { Rundenplanung = Rundenplanung.Halbrunde };
        Berechnungsoptionen korrigiert = original with { Korrekturen = Korrekturen.Keine with { HallenNachNamen = true } };

        Assert.Equal(1, Hallenbelegungen(Referenzbewertung.Bewerten(Staffel("Halle Süd"), original)));
        Assert.Equal(0, Hallenbelegungen(Referenzbewertung.Bewerten(Staffel("Halle Süd"), korrigiert)));
        Assert.Equal(1, Hallenbelegungen(Referenzbewertung.Bewerten(Staffel("halle nord "), korrigiert)));
        Assert.Equal(1, Hallenbelegungen(Referenzbewertung.Bewerten(Staffel(string.Empty), korrigiert)));
    }

    [Fact]
    public void Die_Einstellung_wird_nur_bei_Abweichung_gespeichert()
    {
        byte[] standard = OptionenDatei.ErzeugenBytes(Berechnungsoptionen.Standard);
        Assert.DoesNotContain("mandatory-games", System.Text.Encoding.Latin1.GetString(standard), StringComparison.Ordinal);

        string pfad = Path.Combine(Path.GetTempPath(), "andigen-korrektur-" + Guid.NewGuid().ToString("N") + ".options");
        try
        {
            OptionenDatei.Speichern(pfad, Berechnungsoptionen.Standard with { SpielfreiAmPflichtspieltag = false });
            Assert.False(OptionenDatei.Laden(pfad).SpielfreiAmPflichtspieltag);
            OptionenDatei.Speichern(pfad, Berechnungsoptionen.Standard);
            Assert.True(OptionenDatei.Laden(pfad).SpielfreiAmPflichtspieltag);
        }
        finally
        {
            File.Delete(pfad);
        }
    }

    private static int Hallenbelegungen(Planbewertung bewertung) =>
        bewertung.Mannschaften.Sum(m => Math.Max(0, m.JeKostenart[(int)MannschaftsKostenart.Hallenbelegung].Anzahl));

    private static Mannschaft Team(string name, string id) =>
        new(name, string.Empty, id, "V" + name, 1, string.Empty, -1, [], [], [], [], [], []);

    private static int Pflichtverstoesse(Planbewertung bewertung) =>
        bewertung.Mannschaften.Sum(m => m.JeKostenart[(int)MannschaftsKostenart.Pflichtspieltage].Anzahl);
}
