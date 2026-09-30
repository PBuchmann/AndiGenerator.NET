// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Persistence.Plandaten;

namespace AndiGenerator.Persistence.Tests;

/// <summary>
/// Künstlich angereicherte Staffeln auf Basis des Referenzfalls R2. Sie enthalten Datenarten, die in den echten
/// Referenzfällen fehlen oder selten sind (Setzliste, Heimrecht, festgelegte Begegnungen, gleichzeitige Heimspiele mit
/// einer Nachbarmannschaft, Koppeltermine aller Stufen, spielfreie Tage), damit alle Kostenarten und Auswertungen
/// durchlaufen werden.
/// </summary>
internal static class Anreicherung
{
    /// <summary>Name, unter dem die angereicherte Staffel in den Differenztests erscheint.</summary>
    public const string Name = "R2 angereichert";

    /// <summary>Name der künstlichen Nachbarmannschaft mit gewünschten gleichzeitigen Heimspielen.</summary>
    public const string Nachbar = "Nachbar Test";

    /// <summary>Lädt den Referenzfall R2 (click-TT-Export und Änderungen).</summary>
    /// <returns>Die Staffel.</returns>
    public static Staffel R2()
    {
        string pfad = Path.Combine(Testdaten.Referenz, "eingabe", "R2_4__Kreisklasse_Gruppe_A (2).xml");
        DatenKnoten stand = ClickTtNachPlanDaten.Laden(pfad);
        stand.Zusammenfuehren(PlanDatenDatei.Laden(Path.ChangeExtension(pfad, ".modifications")));
        return StaffelAbbildung.AusPlanDaten(stand);
    }

    /// <summary>R2 mit allen zusätzlichen Datenarten.</summary>
    /// <returns>Die Staffel.</returns>
    public static Staffel Erzeugen()
    {
        Staffel r2 = R2();
        List<Mannschaft> m = [.. r2.Mannschaften];

        // Heimrecht: Zahl der Heimspiele in der Vorrunde und Vorgaben gegen einzelne Gegner.
        m[0] = m[0] with
        {
            HeimspieleHinrunde = 2,
            Heimrechte = [new Heimrechtvorgabe(m[1].Name, Runde.Hinrunde), new Heimrechtvorgabe(m[2].Name, Runde.Rueckrunde)],
        };
        m[1] = m[1] with { HeimspieleHinrunde = 3, Heimrechte = [new Heimrechtvorgabe(m[0].Name, Runde.Rueckrunde)] };

        // Nachbarmannschaft, deren Heimspiele gleichzeitig stattfinden sollen (Kostenart „Gleiche Heimtermine“).
        List<Nachbarspiel> nachbarspiele = m[2].Heimspieltermine
            .Take(6)
            .Select((t, i) => i % 2 == 0 ? new Nachbarspiel(t.Zeitpunkt, Nachbar, "Gegner Test", string.Empty) : new Nachbarspiel(t.Zeitpunkt, "Gegner Test", Nachbar, string.Empty))
            .ToList();
        m[2] = m[2] with
        {
            Nachbarmannschaften = [.. m[2].Nachbarmannschaften, new Nachbarmannschaft(Nachbar, "Damen", 1, string.Empty, false, true, nachbarspiele)],
        };

        // Koppeltermine und Doppelspieltage in allen Prioritäten, dazu ein abweichendes Spiellokal.
        (Terminkoppelung Art, Koppelprioritaet Prio)[] koppeln =
        [
            (Terminkoppelung.Koppeltermin, Koppelprioritaet.Moeglich), (Terminkoppelung.Koppeltermin, Koppelprioritaet.Weich),
            (Terminkoppelung.Koppeltermin, Koppelprioritaet.Hart), (Terminkoppelung.Doppelspieltag, Koppelprioritaet.Moeglich),
            (Terminkoppelung.Doppelspieltag, Koppelprioritaet.Weich), (Terminkoppelung.Doppelspieltag, Koppelprioritaet.Hart),
        ];
        List<Heimspieltermin> termine = m[3].Heimspieltermine
            .Select((t, i) => i < koppeln.Length
                ? t with { Koppelung = koppeln[i].Art, Prioritaet = koppeln[i].Prio, ZweiteUhrzeit = new TimeOnly(15, 0) }
                : t)
            .ToList();
        termine[koppeln.Length] = termine[koppeln.Length] with { Spiellokal = "2" };
        m[3] = m[3] with { Heimspieltermine = termine };

        var setzliste = new Setzliste(true, m.Select((x, i) => new Setzlisteneintrag(x.Name, i)).ToList());
        Spiel[] vorgegeben =
        [
            new(m[4].Heimspieltermine[0].Zeitpunkt, m[4].Name, m[5].Name, string.Empty),
            new(m[5].Heimspieltermine[^1].Zeitpunkt, m[5].Name, m[6].Name, "2"),
        ];
        DateOnly spielfrei = DateOnly.FromDateTime(m[6].Heimspieltermine[1].Zeitpunkt);
        return r2 with
        {
            Mannschaften = m,
            Setzliste = setzliste,
            VorgegebeneSpiele = vorgegeben,
            SpielfreieTage = [.. r2.SpielfreieTage, spielfrei],
        };
    }
}
