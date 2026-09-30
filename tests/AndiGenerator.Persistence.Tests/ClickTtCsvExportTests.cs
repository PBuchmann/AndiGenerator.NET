// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Persistence.ClickTt;
using AndiGenerator.Persistence.Csv;
using AndiGenerator.Persistence.Gemeinsam;

namespace AndiGenerator.Persistence.Tests;

/// <summary>
/// Byte-Parität des CSV-Exports (MIGRATIONSPLAN E8): Aus den Spielen einer Original-CSV wird die Datei neu erzeugt
/// und mit der Delphi-Ausgabe verglichen. Die Spiele werden vorher umsortiert, damit die Sortierung mitgeprüft wird.
/// </summary>
public class ClickTtCsvExportTests
{
    public static TheoryData<string, string> VollstaendigTerminiertePlaene() => new()
    {
        { "1. Kreisklasse Gruppe A.csv", "1__Kreisklasse_Gruppe_A (1).xml" },
        { "1. Kreisklasse Gruppe B.csv", "1__Kreisklasse_Gruppe_B (1).xml" },
        { "1. Kreisklasse.csv", "1__Kreisklasse (4).xml" },
        { "2. Kreisklasse Gruppe A.csv", "2__Kreisklasse_Gruppe_A (1).xml" },
        { "2. Kreisklasse Gruppe B.csv", "2__Kreisklasse_Gruppe_B (1).xml" },
        { "2. Kreisklasse.csv", "2__Kreisklasse (8).xml" },
        { "3. Kreisklasse.csv", "3__Kreisklasse (11).xml" },
        { "4. Kreisklasse.csv", "4__Kreisklasse (5).xml" },
        { "4. Kreisklasse A.csv", "4__Kreisklasse (5).xml" },
        { "4. Kreisklasse Gruppe A.csv", "4__Kreisklasse_Gruppe_A (2).xml" },
        { "4. Kreisklasse Gruppe B.csv", "4__Kreisklasse_Gruppe_B (2).xml" },
        { "Bezirksliga Rheinhessen Nord.csv", "Bezirksliga_Rheinhessen_Nord (1).xml" },
        { "Bezirksliga Rheinhessen Süd.csv", "Bezirksliga_Rheinhessen_Süd (3).xml" },
        { "Bezirksoberliga Rheinhessen Nord.csv", "Bezirksoberliga_Rheinhessen_Nord (1).xml" },
        { "Bezirksoberliga Rheinhessen Süd.csv", "Bezirksoberliga_Rheinhessen_Süd (2).xml" },
        { "Damen Bezirksliga Rheinhessen.csv", "Damen_Bezirksliga_Rheinhessen (1).xml" },
        { "Damen Bezirksoberliga Rheinhessen.csv", "Damen_Bezirksoberliga_Rheinhessen (1).xml" },
    };

    [Theory]
    [MemberData(nameof(VollstaendigTerminiertePlaene))]
    public void Csv_ist_byte_gleich_zum_Original(string csvDatei, string xmlDatei)
    {
        byte[] original = File.ReadAllBytes(Testdaten.Datei(csvDatei));
        ClickTtStaffel staffel = ClickTtLeser.Lesen(Testdaten.Datei(xmlDatei));
        List<CsvSpiel> spiele = SpieleAusCsv(original);
        spiele.Reverse();

        var optionen = new CsvExportOptionen(
            Staffelbeginn: staffel.Von.ToDateTime(TimeOnly.MinValue),
            Rueckrundenbeginn: staffel.Rueckrundenbeginn!.Value.ToDateTime(TimeOnly.MinValue),
            Halbrunde: false,
            IstInZuPlanenderRunde: _ => true);
        IEnumerable<CsvMannschaft> mannschaften = staffel.Mannschaften.Select(m => new CsvMannschaft(m.Name, m.VereinsId, m.Id));

        byte[] neu = ClickTtCsvExport.ErzeugenBytes(spiele, mannschaften, optionen);

        if (!original.AsSpan().SequenceEqual(neu))
        {
            string[] a = DelphiKompatibel.Windows1252.GetString(original).Split("\r\n");
            string[] b = DelphiKompatibel.Windows1252.GetString(neu).Split("\r\n");
            int zeile = Enumerable.Range(0, Math.Min(a.Length, b.Length)).FirstOrDefault(i => a[i] != b[i], Math.Min(a.Length, b.Length));
            Assert.Fail($"Abweichung in Zeile {zeile + 1} (Original {a.Length} / neu {b.Length} Zeilen):\n  Original: {Zeile(a, zeile)}\n  Neu:      {Zeile(b, zeile)}");
        }
    }

    [Fact]
    public void Nummerierung_zaehlt_nicht_exportierte_Spiele_mit()
    {
        // Befund #8: Index über alle Spiele, auch nicht exportierte.
        var mannschaften = new[] { new CsvMannschaft("A", "1", "10"), new CsvMannschaft("B", "2", "20") };
        var spiele = new[]
        {
            new CsvSpiel(null, "B", "A", string.Empty),
            new CsvSpiel(new DateTime(2026, 9, 4, 20, 0, 0, DateTimeKind.Unspecified), "A", "B", "2"),
        };
        var optionen = new CsvExportOptionen(new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 12, 7, 0, 0, 0, DateTimeKind.Unspecified), Halbrunde: true, IstInZuPlanenderRunde: d => d is not null);

        string csv = ClickTtCsvExport.Erzeugen(spiele, mannschaften, optionen);

        Assert.Equal(ClickTtCsvExport.Kopfzeile + "\r\n2;0;Freitag;;04.09.2026;;20:00;1;A;2;B;10;20;;2;\r\n", csv);
    }

    private static string Zeile(string[] zeilen, int i) => i < zeilen.Length ? zeilen[i] : "(keine)";

    private static List<CsvSpiel> SpieleAusCsv(byte[] inhalt)
    {
        return DelphiKompatibel.Windows1252.GetString(inhalt)
            .Split("\r\n")
            .Skip(1)
            .Where(z => z.Length > 0)
            .Select(z => z.Split(';'))
            .Select(f => new CsvSpiel(
                DateTime.ParseExact(f[4] + " " + f[6], "dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture),
                f[8],
                f[10],
                f[14]))
            .ToList();
    }
}
