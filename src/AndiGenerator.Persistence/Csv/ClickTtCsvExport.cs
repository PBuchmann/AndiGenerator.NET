// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text;
using AndiGenerator.Persistence.Gemeinsam;

namespace AndiGenerator.Persistence.Csv;

/// <summary>
/// CSV-Export für den click-TT-Import – byte-genaue Nachbildung von <c>TPlan.SaveScheduleToCsv</c> (Version 26.7.1.0):
/// Windows-1252 ohne BOM, CRLF nach jeder Zeile (auch der letzten), Semikolon als Trenner.
/// Eigenheiten des Originals, die bewusst erhalten bleiben (MIGRATIONSPLAN E8/E9):
/// Kopfzeile mit 15, Datenzeilen mit 16 Feldern (Befund #30); die Nummer ist der Index über ALLE Spiele,
/// auch die nicht exportierten (Lücken, Befund #8); nicht terminierte Spiele erhalten das Datum <see cref="CsvExportOptionen.Staffelbeginn"/>.
/// </summary>
public static class ClickTtCsvExport
{
    /// <summary>Kopfzeile wie im Original.</summary>
    public const string Kopfzeile = "Nr.;Vor/Rück;Tag;;Datum;;Uhrzeit;HeimVereinNr;Heim-Mannschaft;GastVereinNr;Gast-Mannschaft;HeimMannschaftNr;GastMannschaftNr;Ergebnisse;Spiellokal";

    /// <summary>Erzeugt den Dateiinhalt als Zeichenkette (Zeilenende CRLF).</summary>
    /// <param name="spiele">Alle Spiele des Plans (auch nicht terminierte).</param>
    /// <param name="mannschaften">Mannschaften der Staffel (für Vereins- und Mannschaftsnummern).</param>
    /// <param name="optionen">Rahmenbedingungen des Exports.</param>
    /// <returns>Der Dateiinhalt als Text (Zeilenende CRLF).</returns>
    public static string Erzeugen(IEnumerable<CsvSpiel> spiele, IEnumerable<CsvMannschaft> mannschaften, CsvExportOptionen optionen)
    {
        ArgumentNullException.ThrowIfNull(spiele);
        ArgumentNullException.ThrowIfNull(mannschaften);
        ArgumentNullException.ThrowIfNull(optionen);

        // Original: Mannschaften.FindByName (erster Treffer).
        var nachName = new Dictionary<string, CsvMannschaft>(StringComparer.Ordinal);
        foreach (CsvMannschaft m in mannschaften)
        {
            nachName.TryAdd(m.Name, m);
        }

        // Original: TSchedule.SortByDate – Datum aufsteigend (nicht terminiert = 0 = zuerst),
        // bei gleichem Datum ordinaler Vergleich von "Heim Gast" (Delphi CompareStr).
        List<CsvSpiel> sortiert = spiele.ToList();
        sortiert.Sort(static (a, b) =>
        {
            int vergleich = (a.Zeitpunkt ?? DateTime.MinValue).CompareTo(b.Zeitpunkt ?? DateTime.MinValue);
            return vergleich != 0 ? vergleich : string.CompareOrdinal(a.Heim + " " + a.Gast, b.Heim + " " + b.Gast);
        });

        var text = new StringBuilder();
        text.Append(Kopfzeile).Append("\r\n");

        for (int i = 0; i < sortiert.Count; i++)
        {
            CsvSpiel spiel = sortiert[i];
            if (!optionen.IstInZuPlanenderRunde(spiel.Zeitpunkt))
            {
                continue;
            }

            if (!nachName.TryGetValue(spiel.Heim, out CsvMannschaft? heim) || !nachName.TryGetValue(spiel.Gast, out CsvMannschaft? gast))
            {
                continue; // Original: Zeile entfällt, wenn eine Mannschaft nicht gefunden wird.
            }

            string vorRueck = !optionen.Halbrunde && spiel.Zeitpunkt is { } z && z > optionen.Rueckrundenbeginn ? "1" : "0";
            DateTime datum = spiel.Zeitpunkt ?? optionen.Staffelbeginn;

            text.Append((i + 1).ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(vorRueck).Append(';')
                .Append(DelphiKompatibel.Wochentag(datum)).Append(";;")
                .Append(datum.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)).Append(";;")
                .Append(datum.ToString("HH:mm", CultureInfo.InvariantCulture)).Append(';')
                .Append(heim.VereinsId).Append(';')
                .Append(heim.Name).Append(';')
                .Append(gast.VereinsId).Append(';')
                .Append(gast.Name).Append(';')
                .Append(heim.MannschaftsId).Append(';')
                .Append(gast.MannschaftsId).Append(';')
                .Append(';')
                .Append(spiel.Spiellokal).Append(';')
                .Append("\r\n");
        }

        return text.ToString();
    }

    /// <summary>Erzeugt den Dateiinhalt als Bytes in Windows-1252 ohne BOM.</summary>
    /// <param name="spiele">Alle Spiele des Plans (auch nicht terminierte).</param>
    /// <param name="mannschaften">Mannschaften der Staffel.</param>
    /// <param name="optionen">Rahmenbedingungen des Exports.</param>
    /// <returns>Der Dateiinhalt in Windows-1252.</returns>
    public static byte[] ErzeugenBytes(IEnumerable<CsvSpiel> spiele, IEnumerable<CsvMannschaft> mannschaften, CsvExportOptionen optionen)
    {
        return DelphiKompatibel.Windows1252.GetBytes(Erzeugen(spiele, mannschaften, optionen));
    }

    /// <summary>Schreibt die CSV-Datei.</summary>
    /// <param name="pfad">Pfad der CSV-Datei.</param>
    /// <param name="spiele">Alle Spiele des Plans (auch nicht terminierte).</param>
    /// <param name="mannschaften">Mannschaften der Staffel.</param>
    /// <param name="optionen">Rahmenbedingungen des Exports.</param>
    public static void Schreiben(string pfad, IEnumerable<CsvSpiel> spiele, IEnumerable<CsvMannschaft> mannschaften, CsvExportOptionen optionen)
    {
        File.WriteAllBytes(pfad, ErzeugenBytes(spiele, mannschaften, optionen));
    }
}
