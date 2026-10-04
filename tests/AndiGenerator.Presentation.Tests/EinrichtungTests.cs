// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;

namespace AndiGenerator.Presentation.Tests;

/// <summary>Einrichtungsseite nach dem Öffnen: Reihenfolge, Übernehmen, Überspringen, Abschließen.</summary>
public sealed class EinrichtungTests
{
    [Fact]
    public async Task Punkte_werden_uebernommen_uebersprungen_und_der_Reihe_nach_gezeigt()
    {
        var entscheidung = new Einrichtungsschritt(1, "Rundenplanung", "Welche Spiele?", ["a"], [new Einrichtungsoption("Rückrunde", "x"), new Einrichtungsoption("Ganz", "y")]);
        var lokale = new Einrichtungsschritt(2, Erststart.Hinweis(Erststartpunkt.Spiellokale));
        var setzliste = new Einrichtungsschritt(3, Erststart.Hinweis(Erststartpunkt.Setzliste));
        var uebernommen = new List<string>();
        bool erfolg = false;
        var erzeugt = new List<string>();
        int abgeschlossen = 0;
        var einrichtung = new EinrichtungViewModel(
            "Staffel einrichten",
            [entscheidung, lokale, setzliste],
            s =>
            {
                uebernommen.Add(s.Titel + ":" + s.Auswahl);
                return Task.FromResult(erfolg);
            },
            s =>
            {
                erzeugt.Add(s.Titel);
                return null;
            },
            () =>
            {
                abgeschlossen++;
                return Task.CompletedTask;
            });

        Assert.Same(entscheidung, einrichtung.Aktiv);
        Assert.True(entscheidung.IstEntscheidung);
        Assert.True(entscheidung.HatDetails);
        Assert.Equal("Auswahl übernehmen", einrichtung.UebernehmenText);
        Assert.Equal("jetzt dran", entscheidung.Status);
        Assert.Equal("1", entscheidung.Marke);
        Assert.Empty(erzeugt);

        entscheidung.Auswahl = -3;
        Assert.Equal(0, entscheidung.Auswahl);
        entscheidung.Auswahl = 1;
        await einrichtung.UebernehmenCommand.ExecuteAsync(null);
        Assert.True(einrichtung.HatMeldung);
        Assert.Equal(Schrittzustand.Offen, entscheidung.Zustand);

        erfolg = true;
        await einrichtung.UebernehmenCommand.ExecuteAsync(null);
        Assert.Equal(["Rundenplanung:1", "Rundenplanung:1"], uebernommen);
        Assert.Equal(Schrittzustand.Erledigt, entscheidung.Zustand);
        Assert.True(entscheidung.IstErledigt);
        Assert.Equal("erledigt", entscheidung.Status);
        Assert.Same(lokale, einrichtung.Aktiv);
        Assert.False(einrichtung.HatMeldung);
        Assert.Equal("Übernehmen und weiter", einrichtung.UebernehmenText);
        Assert.Equal(["Spiellokale"], erzeugt);
        Assert.False(lokale.IstEntscheidung);
        Assert.Equal(Erststartpunkt.Spiellokale, lokale.Punkt);

        setzliste.WaehlenCommand.Execute(null);
        Assert.Same(setzliste, einrichtung.Aktiv);
        Assert.False(lokale.IstAktiv);
        einrichtung.UeberspringenCommand.Execute(null);
        Assert.Equal("übersprungen", setzliste.Status);
        Assert.Equal("–", setzliste.Marke);

        // Nach dem letzten geht es von vorn zum ersten offenen Punkt.
        Assert.Same(lokale, einrichtung.Aktiv);
        einrichtung.UeberspringenCommand.Execute(null);
        Assert.Same(lokale, einrichtung.Aktiv);
        Assert.Equal("3 von 3 Punkten erledigt", einrichtung.FortschrittText);
        Assert.Equal(100, einrichtung.Fortschritt);

        await einrichtung.AbschliessenCommand.ExecuteAsync(null);
        Assert.Equal(1, abgeschlossen);
    }

    [Fact]
    public async Task Abschliessen_uebernimmt_angeklickte_Entscheidungen_und_laesst_unberuehrte_unveraendert()
    {
        var angeklickt = new Einrichtungsschritt(1, "Kriterien", "?", [], [new Einrichtungsoption("Diese", "x"), new Einrichtungsoption("Standard", "y")]);
        var unberuehrt = new Einrichtungsschritt(2, "Runde", "?", [], [new Einrichtungsoption("Rückrunde", "x"), new Einrichtungsoption("Ganz", "y")]);
        var uebernommen = new List<string>();
        int abgeschlossen = 0;
        var einrichtung = new EinrichtungViewModel(
            "Staffel einrichten",
            [angeklickt, unberuehrt],
            s =>
            {
                uebernommen.Add(s.Titel + ":" + s.Auswahl);
                return Task.FromResult(true);
            },
            _ => null,
            () =>
            {
                abgeschlossen++;
                return Task.CompletedTask;
            });

        Assert.False(angeklickt.IstGewaehlt);
        angeklickt.Auswahl = 0;
        Assert.False(angeklickt.IstGewaehlt);
        angeklickt.Auswahl = 1;
        Assert.True(angeklickt.IstGewaehlt);
        await einrichtung.AbschliessenCommand.ExecuteAsync(null);

        Assert.Equal(["Kriterien:1"], uebernommen);
        Assert.Equal(Schrittzustand.Erledigt, angeklickt.Zustand);
        Assert.Equal(Schrittzustand.Offen, unberuehrt.Zustand);
        Assert.Equal(1, abgeschlossen);
    }

    [Fact]
    public void Details_ohne_Leerzeilen_mit_Ueberschrift_und_Unterpunkten()
    {
        IReadOnlyList<Detailzeile> zeilen = Detailzeile.Aus(["Gewichtung", "  Sperrtermine: extrem hoch", "  Wechsel H/A: sehr hoch", string.Empty, "Doppelrunde ist aktiv", string.Empty]);

        Assert.Equal(
            [
                new Detailzeile("Gewichtung", Detailart.Ueberschrift),
                new Detailzeile("Sperrtermine: extrem hoch", Detailart.Unterpunkt),
                new Detailzeile("Wechsel H/A: sehr hoch", Detailart.Unterpunkt),
                new Detailzeile("Doppelrunde ist aktiv", Detailart.Punkt),
            ],
            zeilen);
        Assert.False(zeilen[0].IstPunkt);
        Assert.True(zeilen[1].IstUnterpunkt);
    }
}
