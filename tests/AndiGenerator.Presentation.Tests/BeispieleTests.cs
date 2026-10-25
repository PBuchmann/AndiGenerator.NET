// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Persistence.Optionen;

namespace AndiGenerator.Presentation.Tests;

/// <summary>
/// Beispiele auf der Startseite: Die beiliegenden Staffeln (Testpläne des Originals, Kompaktklassen Hessenliga und NRW-Liga) lassen sich
/// öffnen; geöffnet wird eine Kopie im Datenordner, die mit ihren Änderungen erhalten bleibt.
/// </summary>
public sealed class BeispieleTests : IDisposable
{
    private readonly string ordner = Path.Combine(Path.GetTempPath(), "andigen-beispiele-" + Guid.NewGuid().ToString("N"));
    private readonly TestOberflaeche o = new();
    private readonly HauptfensterViewModel hf;

    public BeispieleTests()
    {
        hf = new HauptfensterViewModel(o, () => throw new InvalidOperationException("Keine Fenster im Test"), Path.Combine(ordner, "basis"), null);
    }

    public void Dispose()
    {
        hf.Dispose();
        try
        {
            Directory.Delete(ordner, recursive: true);
        }
        catch (IOException)
        {
            // Aufräumen ist nicht Teil des Tests.
        }
    }

    [Fact]
    public void Die_Beispiele_liegen_bei_bekannte_zuerst_mit_Beschreibung()
    {
        Assert.True(hf.HatBeispiele);
        Assert.Equal(
            ["Liga mit idealen Meldungen", "Jugend 19 Hessenliga (HR)", "Jugend 19 NRW-Liga 1 (HR)", "Liga mit Koppelterminen", "Liga mit 2 Mannschaften aus einem Verein", "Liga mit 11 Mannschaften und 40km Regel", "Liga für Doppelrunde"],
            hf.Beispiele.Select(b => b.Name));
        Assert.All(hf.Beispiele, b => Assert.NotEmpty(b.Beschreibung));
        Assert.All(hf.Beispiele, b => Assert.True(File.Exists(b.Pfad)));
        Assert.Empty(Beispielsammlung.Lesen(Path.Combine(ordner, "gibt es nicht")));
    }

    [Fact]
    public async Task Jedes_Beispiel_oeffnet_sich_als_Kopie_im_Datenordner()
    {
        foreach (Beispielzeile beispiel in hf.Beispiele)
        {
            await hf.BeispielOeffnenCommand.ExecuteAsync(beispiel.Pfad);
            Assert.True(hf.IstGeoeffnet, beispiel.Name);
            Assert.True(File.Exists(Path.Combine(ordner, "basis", "Beispiele", Path.GetFileName(beispiel.Pfad))), beispiel.Name);
            if (hf.Einrichtung is { } einrichtung)
            {
                await einrichtung.AbschliessenCommand.ExecuteAsync(null);
            }

            Assert.True(hf.ZeigtArbeitsbereich, beispiel.Name);
            Assert.Equal(beispiel.Name.StartsWith("Jugend 19", StringComparison.Ordinal), hf.KompaktAktiv);
            await hf.SchliessenCommand.ExecuteAsync(null);
        }

        Assert.DoesNotContain(o.Meldungen, m => m.Contains("Fehler", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Die_Doppelrunde_wird_nur_beim_ersten_Oeffnen_eingeschaltet()
    {
        // Die Doppelrunde ist eine Einstellung der Staffel, keine Angabe der click-TT-Datei.
        Beispielzeile doppel = hf.Beispiele.Single(b => b.Name == "Liga für Doppelrunde");
        await hf.BeispielOeffnenCommand.ExecuteAsync(doppel.Pfad);
        await hf.SchliessenCommand.ExecuteAsync(null);
        string datei = Assert.Single(Directory.GetFiles(Path.Combine(ordner, "basis"), OptionenDatei.Dateiname, SearchOption.AllDirectories), d => d.Contains("Doppelrunde", StringComparison.Ordinal));
        Assert.True(OptionenDatei.Laden(datei).Doppelrunde);

        // Wer sie in den Einstellungen ausschaltet, behält das auch beim nächsten Öffnen.
        OptionenDatei.Speichern(datei, OptionenDatei.Laden(datei) with { Doppelrunde = false });
        await hf.BeispielOeffnenCommand.ExecuteAsync(doppel.Pfad);
        await hf.SchliessenCommand.ExecuteAsync(null);
        Assert.False(OptionenDatei.Laden(datei).Doppelrunde);
    }

    [Fact]
    public void Die_Kompaktklasse_bringt_die_Hallenadressen_mit_und_die_Kopie_bleibt_erhalten()
    {
        Beispielzeile hessenliga = hf.Beispiele.Single(b => b.Name == "Jugend 19 Hessenliga (HR)");
        string basis = Path.Combine(ordner, "basis");
        string kopie = Beispielsammlung.Kopieren(hessenliga.Pfad, basis);

        Plansitzung sitzung = Plansitzung.Oeffnen(kopie, basis);
        Assert.Equal(10, sitzung.Staffel.Mannschaften.Count);
        Assert.Equal(10, sitzung.DatenBearbeiten().Hallenkoordinaten().Count);
        Assert.Contains("TV Eschersheim 1895", sitzung.DatenBearbeiten().Hallenadressen().Keys);

        // Eine vorhandene Kopie wird nicht überschrieben (eigene Änderungen bleiben).
        string aenderungen = Plansitzung.ModifikationenPfad(kopie);
        File.AppendAllText(aenderungen, "<!-- eigene Änderung -->");
        Assert.Equal(kopie, Beispielsammlung.Kopieren(hessenliga.Pfad, basis));
        Assert.EndsWith("<!-- eigene Änderung -->", File.ReadAllText(aenderungen), StringComparison.Ordinal);
    }
}
