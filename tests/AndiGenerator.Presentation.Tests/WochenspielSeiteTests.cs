// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Persistence.Terminwuensche;

namespace AndiGenerator.Presentation.Tests;

/// <summary>
/// Einrichtung „Daten aus click-TT“, Teil Terminwünsche: Abgleich des Berichts, Vereinskarten, Prüfung der 60-km-Sperren
/// mit einstellbarer Grenze, fehlende Sperren setzen und mit „Standard“ zurücknehmen.
/// </summary>
public sealed class WochenspielSeiteTests : IDisposable
{
    private static readonly string[] Roemisch = [string.Empty, string.Empty, " II", " III", " IV", " V", " VI", " VII", " VIII", " IX", " X"];
    private static readonly Spiellokaladresse Mainz = new("Halle Mainz", "Weg 1", "55116", "Mainz");
    private static readonly Spiellokaladresse Berlin = new("Halle Berlin", "Weg 2", "10115", "Berlin");

    private readonly Arbeitsplatz a = new("../clicktt/2__Kreisklasse_Gruppe_A (1).xml");

    public void Dispose() => a.Dispose();

    [Fact]
    public void Ohne_Bericht_gibt_es_nichts_zu_pruefen()
    {
        var seite = new ZusatzdatenSeiteViewModel(a.Bearbeitung, a.Oberflaeche);

        Assert.False(seite.HatTerminwuensche);
        Assert.Equal(string.Empty, seite.TerminwunschErgebnis);
        Assert.Equal(string.Empty, seite.TerminwunschDatei);
        Assert.Empty(seite.NichtImBericht());
        Assert.False(seite.HatWochenspielpruefung);
        Assert.False(seite.HatFehlendeSperren);
        Assert.False(seite.SperrenSetzenCommand.CanExecute(null));
        Assert.Equal(4, seite.WochenspielKennzahlen.Count);
        Assert.True(seite.IstStandard);
        Assert.NotEmpty(ZusatzdatenSeiteViewModel.TerminwunschErklaerung);
        Assert.NotEmpty(ZusatzdatenSeiteViewModel.AufstellungsErklaerung);
        Assert.NotEmpty(ZusatzdatenSeiteViewModel.WochenspielErklaerung);
    }

    [Fact]
    public async Task Bericht_abgleichen_Sperren_setzen_und_zuruecknehmen()
    {
        IReadOnlyList<Mannschaft> mannschaften = a.Sitzung.Staffel.Mannschaften;
        string fern = mannschaften.First(WocheUndWochenende).Name;
        string fehlt = mannschaften[^1].Name == fern ? mannschaften[^2].Name : mannschaften[^1].Name;
        List<Vereinsmeldung> meldungen = mannschaften
            .Where(m => m.Name != fehlt)
            .Select(m => Meldung(m, m.Name == fern ? Berlin : Mainz, m.Name == fern ? "Bitte nicht vor 19 Uhr." : string.Empty))
            .ToList();
        var seite = new ZusatzdatenSeiteViewModel(a.Bearbeitung, a.Oberflaeche);

        seite.TerminwuenscheAnzeigen(a.Bearbeitung.TerminwuenscheAbgleichen(meldungen), "Terminmeldungen.pdf");

        Assert.True(seite.HatTerminwuensche);
        Assert.False(seite.IstStandard);
        Assert.Equal("Terminmeldungen.pdf", seite.TerminwunschDatei);
        Assert.StartsWith("„Terminmeldungen.pdf“ geladen: " + (mannschaften.Count - 1).ToString(CultureInfo.InvariantCulture) + " von ", seite.TerminwunschErgebnis, StringComparison.Ordinal);
        Assert.Equal([fehlt], seite.NichtImBericht());
        Assert.Equal(4, seite.Kennzahlen.Count);
        Assert.Equal(mannschaften.Count, seite.Vereine.Count);

        // Auffällige Vereine (Bemerkung, Unterschiede, nicht gefunden) stehen oben.
        Vereinskarte nichtGefunden = seite.Vereine.Single(v => v.Mannschaft == fehlt);
        Assert.False(nichtGefunden.Gefunden);
        Assert.NotEqual(string.Empty, nichtGefunden.Marke);
        Vereinskarte mitBemerkung = seite.Vereine.Single(v => v.Mannschaft == fern);
        Assert.True(mitBemerkung.HatBemerkung);
        Assert.True(seite.Vereine.IndexOf(mitBemerkung) < seite.Vereine.Count - 1 || seite.Vereine.All(v => !v.Gefunden || v.HatBemerkung || v.HatAbweichungen));

        // Berlin liegt weit weg: Gäste unter der Woche sollten dort gesperrt sein.
        Assert.True(seite.HatWochenspielpruefung);
        Assert.True(seite.HatWochentagsgastgeber);
        Assert.True(seite.HatFehlendeSperren);
        Assert.StartsWith("Gastgeber mit Heimterminen unter der Woche und am Wochenende: ", seite.WochenspielStatus, StringComparison.Ordinal);
        Assert.Contains("Ohne Hallenadresse: " + fehlt, seite.WochenspielText, StringComparison.Ordinal);
        Assert.All(seite.FehlendeSperren, z => Assert.Contains(fern, new[] { z.Heim, z.Gast }));

        // Eine Grenze über der Entfernung Mainz–Berlin: keine fehlenden Sperren mehr.
        seite.Grenze = 2000;
        Assert.False(seite.HatFehlendeSperren);
        seite.Grenze = 60;
        int fehlend = seite.FehlendeSperren.Count;
        Assert.True(fehlend > 0);

        // Rückfrage abgelehnt: nichts gesetzt.
        a.Oberflaeche.Antwort = false;
        await seite.SperrenSetzenCommand.ExecuteAsync(null);
        Assert.False(seite.HatSperrenProtokoll);

        a.Oberflaeche.Antwort = true;
        await seite.SperrenSetzenCommand.ExecuteAsync(null);
        Assert.True(seite.HatSperrenProtokoll);
        Assert.Contains(" Sperren gesetzt (Grenze 60 km)", seite.SperrenProtokollKopf, StringComparison.Ordinal);
        Assert.Equal(fehlend, seite.SperrenProtokoll.Count);
        Assert.All(seite.SperrenProtokoll, z => Assert.Contains(" nur am Wochenende (", z, StringComparison.Ordinal));
        Assert.False(seite.HatFehlendeSperren);
        string[] paar = seite.SperrenProtokoll[0].Split(" spielt bei ");
        string gast = paar[0];
        string heim = paar[1].Split(" nur am Wochenende")[0];
        Assert.Contains(heim, a.Bearbeitung.NurWochenendeGegen(gast));

        // Ohne fehlende Sperren tut der Befehl nichts.
        await seite.SperrenSetzenCommand.ExecuteAsync(null);
        Assert.Equal(fehlend, seite.SperrenProtokoll.Count);

        // Übernehmen schreibt die Adressen; „Standard“ nimmt Bericht und gesetzte Sperren zurück.
        seite.Speichern();
        Assert.NotEqual(string.Empty, a.Bearbeitung.TerminwunschVermerk);
        seite.Standard();
        Assert.True(seite.IstStandard);
        Assert.False(seite.HatTerminwuensche);
        Assert.Empty(seite.Vereine);
        Assert.False(seite.HatSperrenProtokoll);
        Assert.DoesNotContain(heim, a.Bearbeitung.NurWochenendeGegen(gast));
    }

    [Fact]
    public async Task Bericht_laden_meldet_Fehler_und_Abbrechen()
    {
        var seite = new ZusatzdatenSeiteViewModel(a.Bearbeitung, a.Oberflaeche);
        a.Oberflaeche.DateiZumOeffnen = null;
        await seite.TerminwuenscheLadenCommand.ExecuteAsync(null);
        Assert.False(seite.HatFehler);

        string pdf = Path.Combine(Path.GetTempPath(), "andigen-kein-bericht-" + Guid.NewGuid().ToString("N") + ".pdf");
        await File.WriteAllTextAsync(pdf, "kein PDF");
        try
        {
            a.Oberflaeche.DateiZumOeffnen = pdf;
            await seite.TerminwuenscheLadenCommand.ExecuteAsync(null);
            Assert.True(seite.HatFehler);
            Assert.False(seite.HatTerminwuensche);
        }
        finally
        {
            File.Delete(pdf);
        }

        seite.TerminwuenscheLaden(Path.Combine(Path.GetTempPath(), "andigen-fehlt-" + Guid.NewGuid().ToString("N") + ".pdf"));
        Assert.True(seite.HatFehler);
    }

    /// <summary>Gastgeber mit Heimterminen unter der Woche (Mo–Do) und am Wochenende (Fr–So).</summary>
    private static bool WocheUndWochenende(Mannschaft m) =>
        m.Heimspieltermine.Any(t => t.Zeitpunkt.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Thursday)
        && m.Heimspieltermine.Any(t => t.Zeitpunkt.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday);

    private static Vereinsmeldung Meldung(Mannschaft m, Spiellokaladresse halle, string bemerkung) => new(
        "Verein",
        int.Parse(m.VereinsId, CultureInfo.InvariantCulture),
        "Erwachsene" + Roemisch[m.Nummer],
        new Dictionary<int, Spiellokaladresse> { [1] = halle, [2] = halle, [3] = halle },
        bemerkung,
        [],
        []);
}
