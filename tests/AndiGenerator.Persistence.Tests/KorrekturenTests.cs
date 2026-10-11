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

    private static int Pflichtverstoesse(Planbewertung bewertung) =>
        bewertung.Mannschaften.Sum(m => m.JeKostenart[(int)MannschaftsKostenart.Pflichtspieltage].Anzahl);
}
