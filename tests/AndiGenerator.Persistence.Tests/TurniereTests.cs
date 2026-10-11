// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Domain.Turniere;
using AndiGenerator.Engine.Turniere;
using AndiGenerator.Persistence.Turniere;

namespace AndiGenerator.Persistence.Tests;

/// <summary>Turnierrunden (Jugend): Meldungen aus der Umfrage lesen, Turniertage erkennen und gegen die Meldungen prüfen.</summary>
public class TurniereTests
{
    // Kopf über zwei Zeilen und Umlaute, wie sie entstehen, wenn UTF-8 als Latin-1 gelesen wurde.
    private const string Umfrage = """
        Zeitstempel,E-Mail-Adresse,Mannschaft,"15.11.2026
        (Volkstrauertag)",22.11.2026,Halle,Zwei Spiele parallel in der Halle mÃ¶glich?,Bemerkungen
        01.09.2026 10:00:00,a@example.org,Verein A,"mÃ¶glicher Heimspieltag, mÃ¶glicher AuswÃ¤rtsspieltag",kÃ¶nnen nicht,Halle Nord,Ja,"erst ab 12 Uhr, bitte"
        01.09.2026 11:00:00,b@example.org,Verein B,mÃ¶glicher AuswÃ¤rtsspieltag,mÃ¶glicher Heimspieltag,Halle SÃ¼d,Nein,
        02.09.2026 09:00:00,a@example.org,Verein A,mÃ¶glicher Heimspieltag,mÃ¶glicher AuswÃ¤rtsspieltag,Halle Nord,Ja,
        """;

    private static readonly DateOnly Tag1 = new(2026, 11, 15);
    private static readonly DateOnly Tag2 = new(2026, 11, 22);

    [Fact]
    public void Umfrage_wird_gelesen_und_letzte_Meldung_gilt()
    {
        IReadOnlyList<Turniermeldung> meldungen = TurniermeldungenCsv.Lesen(Umfrage);

        Assert.Equal(new[] { "Verein A", "Verein B" }, meldungen.Select(m => m.Mannschaft));
        Turniermeldung a = meldungen[0];
        Assert.Equal(Turnierbereitschaft.Heim, a.An(Tag1));
        Assert.Equal(Turnierbereitschaft.Auswaerts, a.An(Tag2));
        Assert.True(a.ZweiTische);
        Assert.Equal(string.Empty, a.Bemerkung);
        Turniermeldung b = meldungen[1];
        Assert.Equal("Halle Süd", b.Halle);
        Assert.False(b.ZweiTische);
        Assert.Equal(Turnierbereitschaft.Auswaerts, b.An(Tag1));
        Assert.Equal(Turnierbereitschaft.None, b.An(new DateOnly(2026, 12, 6)));
    }

    [Theory]
    [InlineData("möglicher Heimspieltag, möglicher Auswärtsspieltag", Turnierbereitschaft.Heim | Turnierbereitschaft.Auswaerts)]
    [InlineData("möglicher Auswärtsspieltag", Turnierbereitschaft.Auswaerts)]
    [InlineData("können nicht", Turnierbereitschaft.None)]
    [InlineData("", Turnierbereitschaft.None)]
    public void Antwort_wird_erkannt(string zelle, Turnierbereitschaft erwartet) =>
        Assert.Equal(erwartet, TurniermeldungenCsv.Bereitschaft(zelle));

    [Fact]
    public void Umfrage_ohne_Termine_wird_abgelehnt() =>
        Assert.Throws<FormatException>(() => TurniermeldungenCsv.Lesen("Mannschaft,Halle\nVerein A,Halle Nord\n"));

    [Fact]
    public void Dreier_und_Vierer_werden_erkannt()
    {
        List<Spiel> spiele = [.. Dreier(Tag1, "A", "B", "C"), .. Vierer(Tag2, "A", "D", "B", "C")];

        IReadOnlyList<Turnier> turniere = Turniererkennung.Erkennen(spiele);

        Assert.Equal(2, turniere.Count);
        Assert.Equal(Turnierform.Dreier, turniere[0].Form);
        Assert.Equal(["A"], turniere[0].MoeglicheAusrichter);
        Assert.Equal(Turnierform.Vierer, turniere[1].Form);
        Assert.Equal(["A", "D"], turniere[1].MoeglicheAusrichter);
    }

    [Fact]
    public void Getrennte_Gruppen_am_selben_Tag_sind_getrennte_Turniere()
    {
        List<Spiel> spiele = [.. Dreier(Tag1, "A", "B", "C"), .. Dreier(Tag1, "D", "E", "F")];

        IReadOnlyList<Turnier> turniere = Turniererkennung.Erkennen(spiele);

        Assert.Equal(2, turniere.Count);
        Assert.All(turniere, t => Assert.Equal(Turnierform.Dreier, t.Form));
    }

    [Fact]
    public void Passender_Plan_hat_keine_Befunde()
    {
        // Bei 3 Mannschaften gibt es nur einen Dreier; dass B und C nie ausrichten, ist dann der einzige Befund.
        Turnierpruefungsergebnis ergebnis = Pruefen([.. Dreier(Tag1, "A", "B", "C")], Meldung("A", Turnierbereitschaft.Heim), Meldung("B", Turnierbereitschaft.Auswaerts), Meldung("C", Turnierbereitschaft.Auswaerts));

        Assert.Equal("A", ergebnis.Turniere[0].Ausrichter);
        Assert.Empty(ergebnis.Turniere[0].Hinweise);
        Assert.Equal(["B richtet kein Turnier aus.", "C richtet kein Turnier aus."], ergebnis.Befunde);
    }

    [Fact]
    public void Ausrichter_ohne_Heimspieltag_und_Gast_ohne_Auswaertsspieltag_werden_gemeldet()
    {
        Turnierpruefungsergebnis ergebnis = Pruefen([.. Dreier(Tag1, "A", "B", "C")], Meldung("A", Turnierbereitschaft.Auswaerts), Meldung("B", Turnierbereitschaft.Heim), Meldung("C", Turnierbereitschaft.None));

        Turnierbewertung turnier = ergebnis.Turniere[0];
        Assert.Null(turnier.Ausrichter);
        Assert.Contains(turnier.Hinweise, h => h.StartsWith("Kein möglicher Ausrichter (A: nur auswärts)", StringComparison.Ordinal));
        Assert.Contains("B kann an diesem Tag nicht auswärts spielen (nur zuhause).", turnier.Hinweise);
        Assert.Contains("C kann an diesem Tag nicht auswärts spielen (kann nicht).", turnier.Hinweise);
    }

    [Fact]
    public void Vierer_braucht_zwei_Tische_beim_Ausrichter()
    {
        Turniermeldung a = Meldung("A", Turnierbereitschaft.Heim | Turnierbereitschaft.Auswaerts) with { ZweiTische = false };
        Turniermeldung d = Meldung("D", Turnierbereitschaft.Heim | Turnierbereitschaft.Auswaerts);

        Turnierpruefungsergebnis ergebnis = Pruefen([.. Vierer(Tag1, "A", "D", "B", "C")], a, d, Meldung("B", Turnierbereitschaft.Auswaerts), Meldung("C", Turnierbereitschaft.Auswaerts));

        Assert.Equal("D", ergebnis.Turniere[0].Ausrichter);
        Assert.Empty(ergebnis.Turniere[0].Hinweise);
    }

    [Fact]
    public void Ausrichter_wird_so_gewaehlt_dass_alle_Gaeste_auswaerts_koennen()
    {
        // D stünde nach dem Heimrecht vorn, A kann aber nur zuhause – also muss A ausrichten.
        Turnierpruefungsergebnis ergebnis = Pruefen(
            [.. Vierer(Tag1, "D", "A", "B", "C")],
            Meldung("A", Turnierbereitschaft.Heim),
            Meldung("B", Turnierbereitschaft.Auswaerts),
            Meldung("C", Turnierbereitschaft.Auswaerts),
            Meldung("D", Turnierbereitschaft.Heim | Turnierbereitschaft.Auswaerts));

        Assert.Equal("A", ergebnis.Turniere[0].Ausrichter);
        Assert.Empty(ergebnis.Turniere[0].Hinweise);
    }

    [Fact]
    public void Ausrichter_werden_ueber_alle_Turniere_verteilt()
    {
        // Gierig richtete A beide Turniere aus; verteilt richten zwei verschiedene Mannschaften je eines aus.
        Turniermeldung Alles(string name) => Meldung(name, Turnierbereitschaft.Heim | Turnierbereitschaft.Auswaerts);

        Turnierpruefungsergebnis ergebnis = Pruefen(
            [.. Vierer(Tag1, "A", "B", "C", "D"), .. Vierer(Tag2, "A", "D", "B", "C")],
            Alles("A"),
            Alles("B"),
            Alles("C"),
            Alles("D"));

        Assert.Equal(2, ergebnis.Ausgerichtet.Values.Count(n => n == 1));
        Assert.Equal(2, ergebnis.Ausgerichtet.Values.Count(n => n == 0));
    }

    [Fact]
    public void Fehlende_und_doppelte_Paarungen_werden_gemeldet()
    {
        List<Spiel> spiele = [.. Dreier(Tag1, "A", "B", "C"), Partie(Tag2, 10, "A", "B")];

        Turnierpruefungsergebnis ergebnis = Pruefen(spiele, Meldung("A", Turnierbereitschaft.Heim), Meldung("B", Turnierbereitschaft.Auswaerts), Meldung("C", Turnierbereitschaft.Auswaerts), Meldung("D", Turnierbereitschaft.Auswaerts));

        Assert.Contains("Paarung A – B kommt 2-mal vor.", ergebnis.Befunde);
        Assert.Contains("Paarung A – D fehlt.", ergebnis.Befunde);
        Assert.Equal(Turnierform.Einzel, ergebnis.Turniere[1].Turnier.Form);
    }

    [Fact]
    public void Unvollstaendiges_Turnier_hat_keine_bekannte_Form()
    {
        Turnierpruefungsergebnis ergebnis = Pruefen([Partie(Tag1, 10, "A", "B"), Partie(Tag1, 13, "B", "C")], Meldung("A", Turnierbereitschaft.Heim), Meldung("B", Turnierbereitschaft.Heim), Meldung("C", Turnierbereitschaft.Auswaerts));

        Assert.Equal(Turnierform.Unbekannt, ergebnis.Turniere[0].Turnier.Form);
        Assert.Contains("15.11.2026: Keine bekannte Form (3 Mannschaften, 2 Spiele).", ergebnis.Befunde);
    }

    [Fact]
    public void Einzelbegegnung_wird_geprueft_und_zaehlt_nicht_als_Ausrichtung()
    {
        List<Spiel> spiele = [Partie(Tag1, 10, "A", "B"), Partie(Tag2, 10, "C", "A")];

        Turnierpruefungsergebnis ergebnis = Pruefen(spiele, Meldung("A", Turnierbereitschaft.Heim), Meldung("B", Turnierbereitschaft.Auswaerts), Meldung("C", Turnierbereitschaft.Auswaerts));

        Assert.Equal("A", ergebnis.Turniere[0].Ausrichter);
        Assert.Empty(ergebnis.Turniere[0].Hinweise);
        Assert.Null(ergebnis.Turniere[1].Ausrichter);
        Assert.Contains("A kann an diesem Tag nicht auswärts spielen (nur zuhause).", ergebnis.Turniere[1].Hinweise);
        Assert.Contains("A hat 2 Einzelbegegnungen.", ergebnis.Befunde);
        Assert.Equal(0, ergebnis.Ausgerichtet["A"]);
    }

    [Fact]
    public void Meldung_wird_auch_bei_abweichender_Schreibweise_gefunden()
    {
        Turniermeldung meldung = Meldung("TTC Muendersbach", Turnierbereitschaft.Heim);

        Assert.Same(meldung, Turnierpruefung.Meldung("TTC Mündersbach", [meldung]));
        Assert.Same(meldung, Turnierpruefung.Meldung("TTC Muendersbach II", [meldung]));
        Assert.Null(Turnierpruefung.Meldung("SV Anders", [meldung]));
    }

    private static Turnierpruefungsergebnis Pruefen(List<Spiel> spiele, params Turniermeldung[] meldungen) =>
        Turnierpruefung.Pruefen(Turniererkennung.Erkennen(spiele), meldungen.Select(m => m.Mannschaft).ToList(), meldungen);

    private static Turniermeldung Meldung(string name, Turnierbereitschaft an) =>
        new(name, new Dictionary<DateOnly, Turnierbereitschaft> { [Tag1] = an, [Tag2] = an }, "Halle " + name, true, string.Empty);

    /// <summary>3er: Ausrichter gegen beide Gäste, die Gäste gegeneinander (10, 13, 16 Uhr).</summary>
    private static Spiel[] Dreier(DateOnly tag, string ausrichter, string gast1, string gast2) =>
        [Partie(tag, 10, ausrichter, gast1), Partie(tag, 13, gast1, gast2), Partie(tag, 16, ausrichter, gast2)];

    /// <summary>4er: Ausrichter und zweite Mannschaft je gegen die beiden übrigen (11 und 14 Uhr).</summary>
    private static Spiel[] Vierer(DateOnly tag, string ausrichter, string zweite, string gast1, string gast2) =>
        [Partie(tag, 11, ausrichter, gast1), Partie(tag, 11, zweite, gast2), Partie(tag, 14, ausrichter, gast2), Partie(tag, 14, zweite, gast1)];

    private static Spiel Partie(DateOnly tag, int stunde, string heim, string gast) =>
        new(tag.ToDateTime(new TimeOnly(stunde, 0)), heim, gast, string.Empty);
}
