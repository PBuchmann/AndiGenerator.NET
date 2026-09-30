// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Persistence.ClickTt;

namespace AndiGenerator.Persistence.Tests;

public class ClickTtLeserTests
{
    public static TheoryData<string> AlleClickTtDateien()
    {
        var daten = new TheoryData<string>();
        foreach (string datei in Directory.GetFiles(Testdaten.Ordner, "*.xml").Order(StringComparer.Ordinal))
        {
            if (ClickTtLeser.IstClickTtDatei(datei))
            {
                daten.Add(Path.GetFileName(datei));
            }
        }

        return daten;
    }

    [Theory]
    [MemberData(nameof(AlleClickTtDateien))]
    public void Breitentest_alle_Exporte_lassen_sich_lesen(string datei)
    {
        ClickTtStaffel staffel = ClickTtLeser.Lesen(Testdaten.Datei(datei));

        Assert.False(string.IsNullOrEmpty(staffel.Name));
        Assert.True(staffel.Von < staffel.Bis);
        Assert.Equal(staffel.Mannschaften.Count, staffel.Mannschaften.Select(m => m.Id).Distinct().Count());
        foreach (ClickTtMannschaft m in staffel.Mannschaften)
        {
            Assert.Equal(m.Name.Trim(), m.Name);
            Assert.NotEqual(string.Empty, m.Name);
        }
    }

    [Fact]
    public void Verbandsoberliga_wird_vollstaendig_gelesen()
    {
        ClickTtStaffel s = ClickTtLeser.Lesen(Testdaten.Datei("Verbandsoberliga (1).xml"));

        Assert.Equal("Verbandsoberliga", s.Name);
        Assert.Equal(new DateOnly(2026, 8, 17), s.Von);
        Assert.Equal(new DateOnly(2027, 5, 9), s.Bis);
        Assert.Equal(new DateOnly(2026, 12, 7), s.Rueckrundenbeginn);
        Assert.Equal(12, s.Mannschaften.Count);
        Assert.Equal(132, s.BestehenderSpielplan.Count);
        Assert.Equal(7, s.Pflichtspieltage.Count);
        Assert.Equal(261, s.Mannschaften.Sum(m => m.Heimspieltermine.Count));
        Assert.Equal(61, s.Mannschaften.Sum(m => m.Heimspieltermine.Count(t => t.Koppelwunsch is not null)));
        Assert.Equal(14, s.Mannschaften.Sum(m => m.Auswaertskoppeln.Count));
        Assert.Equal(55, s.Mannschaften.Sum(m => m.KeinWochenspielGegen.Count));
    }

    [Fact]
    public void Fuehrendes_Leerzeichen_im_Mannschaftsnamen_wird_entfernt()
    {
        // Befund #32: im Export " Aowi Trbk Bosu Agps IV", in CSV und .modifications ohne Leerzeichen.
        ClickTtStaffel s = ClickTtLeser.Lesen(Testdaten.Datei("Bezirksliga_Rheinhessen_Süd (3).xml"));

        Assert.Contains(s.Mannschaften, m => m.Name == "Aowi Trbk Bosu Agps IV");
    }

    [Fact]
    public void Doppeltes_Leerzeichen_im_Namen_bleibt_erhalten()
    {
        ClickTtStaffel staffel = ClickTtLeser.Lesen(Testdaten.Datei("4__Kreisklasse_Gruppe_A (1).xml"));
        ClickTtStaffel mitNachbar = ClickTtLeser.Lesen(Testdaten.Datei("Kreisliga (9).xml"));

        Assert.Contains(staffel.Mannschaften, m => m.Name == "Ttki Cnmq  III");
        Assert.Contains(mitNachbar.Mannschaften.SelectMany(m => m.Nachbarmannschaften), n => n.Name == "Ttki Cnmq  III");
    }

    [Fact]
    public void Keine_click_tt_Datei_wird_abgelehnt()
    {
        Assert.False(ClickTtLeser.IstClickTtDatei(Testdaten.Datei("Verbandsoberliga.xml")));
        Assert.Throws<ClickTtFormatException>(() => ClickTtLeser.Lesen(Testdaten.Datei("Verbandsoberliga.xml")));
    }
}
