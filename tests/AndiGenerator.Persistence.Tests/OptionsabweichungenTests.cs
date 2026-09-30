// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;

namespace AndiGenerator.Persistence.Tests;

public class OptionsabweichungenTests
{
    [Fact]
    public void Standardoptionen_haben_keine_Abweichungen()
    {
        Assert.Empty(Optionsabweichungen.Meldungen(Berechnungsoptionen.Standard, Staffel.Leer));
    }

    [Fact]
    public void Abweichungen_in_der_Reihenfolge_des_Originals()
    {
        Staffel staffel = Staffel.Leer with
        {
            Mannschaften =
            [
                new Mannschaft("TTC Test", string.Empty, "4711", "1", 1, string.Empty, -1, [], [], [], [], [], []),
            ],
        };
        Berechnungsoptionen optionen = new KostenartGewichtungsziel(MannschaftsKostenart.Sperrtermine).Setzen(Berechnungsoptionen.Standard, Gewichtung.Hoch);
        optionen = new MannschaftsKostenartGewichtungsziel("4711", "TTC Test", MannschaftsKostenart.DreiTageAbstand).Setzen(optionen, Gewichtung.Wenig);
        optionen = optionen with
        {
            Spieltag = Gewichtung.SehrHoch,
            FreitagZaehltZumWochenende = false,
            Rundenplanung = Rundenplanung.Halbrunde,
            Doppelrunde = true,
        };

        IReadOnlyList<string> meldungen = Optionsabweichungen.Meldungen(optionen, staffel);

        string[] erwartet =
            [
                "Gewichtung",
                "  Länge der Spieltage: sehr hoch",
                "  Sperrtermine: hoch",
                "  TTC Test 3-Tage-Abstand: wenig",
                string.Empty,
                "Freitag ist bei der 60km-Regel nicht erlaubt",
                string.Empty,
                "Halbrunde wird generiert",
                string.Empty,
                "Doppelrunde ist aktiv",
                string.Empty,
            ];
        Assert.Equal(erwartet, meldungen);
    }

    [Fact]
    public void Erststart_ohne_Besonderheiten_nennt_Spiellokale_und_Setzliste()
    {
        IReadOnlyList<Erststarthinweis> hinweise = Erststart.Hinweise(Staffel.Leer);

        string[] erwartet = ["Spiellokale", "Setzliste"];
        Assert.Equal(erwartet, hinweise.Select(h => h.Titel));
    }

    [Fact]
    public void Erststart_Punkte_mit_Schaltflaechen_wie_im_Original()
    {
        string[] aktionen = Enum.GetValues<Erststartpunkt>().Select(p => Erststart.Hinweis(p).Aktion).ToArray();
        string[] erwartet =
        [
            "Spiellokale bearbeiten", "Koppeltermine bearbeiten", "Auswärtskoppelwünsche bearbeiten", "Setzliste aktivieren",
            "Pflichtspieltage bearbeiten",
        ];
        Assert.Equal(erwartet, aktionen);
        Assert.True(Erststart.Zutreffend(Staffel.Leer, Erststartpunkt.Setzliste));
        Assert.False(Erststart.Zutreffend(Staffel.Leer, Erststartpunkt.Pflichtspieltage));
    }
}
