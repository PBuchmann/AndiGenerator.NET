// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Persistence.Aufstellungen;

namespace AndiGenerator.Presentation.Tests;

/// <summary>Einrichtung „Daten aus click-TT“: Setzliste aus der Mannschaftsmeldung.</summary>
public sealed class ZusatzdatenTests : IDisposable
{
    private const string Kopf = "MannschaftName;SpielerMannschaftNr;SpielerRang;TTR;SpielerStatus";
    private readonly Arbeitsplatz a = new("datendialoge/D01_Allgemein.xml");
    private readonly string csv = Path.Combine(Path.GetTempPath(), "andigen-meldung-" + Guid.NewGuid().ToString("N") + ".csv");

    public void Dispose()
    {
        a.Dispose();
        File.Delete(csv);
    }

    [Fact]
    public async Task Staerkste_Mannschaft_zuerst_ohne_Aufstellung_am_Ende_und_als_Setzliste_uebernommen()
    {
        IReadOnlyList<string> namen = a.Bearbeitung.Setzliste().Reihenfolge;
        string Zeilen(string name, params int[] ttr) =>
            string.Concat(ttr.Select((t, i) => $"{name};{MannschaftsaufstellungenCsv.Nummer(name)};{i + 1};{t};\n"));
        await File.WriteAllTextAsync(csv, Kopf + "\n" + Zeilen(namen[0], 1500, 1500, 1500, 1500) + Zeilen(namen[1], 1900, 1800, 1700, 1600, 1000) + Zeilen(namen[2], 1700, 1700, 1700, 1700));
        var seite = new ZusatzdatenSeiteViewModel(a.Bearbeitung, a.Oberflaeche);
        a.Oberflaeche.DateiZumOeffnen = csv;

        await seite.AufstellungenLadenCommand.ExecuteAsync(null);

        Assert.False(seite.HatFehler);
        Assert.Equal([namen[1], namen[2], namen[0]], seite.Staerken.Take(3).Select(z => z.Mannschaft));
        Assert.Equal("Ø 1750", seite.Staerken[0].SchnittText);
        Assert.Equal("keine Aufstellung", seite.Staerken[3].SchnittText);
        Assert.Contains(namen[3], seite.AufstellungsHinweis, StringComparison.Ordinal);

        seite.Speichern();
        Setzlistendaten setzliste = a.Bearbeitung.Setzliste();
        Assert.True(setzliste.Aktiv);
        Assert.Equal(seite.Staerken.Select(z => z.Mannschaft), setzliste.Reihenfolge);
    }

    [Fact]
    public void Ohne_Haken_bleibt_die_Setzliste_unveraendert()
    {
        Setzlistendaten vorher = a.Bearbeitung.Setzliste();
        File.WriteAllText(csv, Kopf + "\n" + vorher.Reihenfolge[^1] + ";1;1;2000;\n");
        var seite = new ZusatzdatenSeiteViewModel(a.Bearbeitung, a.Oberflaeche);
        seite.AufstellungenLaden(csv);

        seite.AlsSetzliste = false;
        seite.Speichern();

        Assert.Equal(vorher.Aktiv, a.Bearbeitung.Setzliste().Aktiv);
        Assert.Equal(vorher.Reihenfolge, a.Bearbeitung.Setzliste().Reihenfolge);
    }

    [Fact]
    public void Falsche_Datei_zeigt_einen_Fehler()
    {
        File.WriteAllText(csv, "Datum;Heim;Gast\n01.01.2027;A;B\n");
        var seite = new ZusatzdatenSeiteViewModel(a.Bearbeitung, a.Oberflaeche);

        seite.AufstellungenLaden(csv);

        Assert.True(seite.HatFehler);
        Assert.False(seite.HatAufstellungen);
        Assert.True(seite.IstStandard);
    }
}
