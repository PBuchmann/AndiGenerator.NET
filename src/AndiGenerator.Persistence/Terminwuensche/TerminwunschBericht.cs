// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text.RegularExpressions;

namespace AndiGenerator.Persistence.Terminwuensche;

/// <summary>
/// Liest den click-TT-Bericht „Terminwünsche“ (nu.Dokument 019, als Textzeilen einer PDF-Datei): je Mannschaft Verein
/// mit Vereinsnummer, Spiellokale 1 bis 3 mit Adresse, Bemerkung, Terminwünsche der Vor- und Rückrunde und
/// Sperrtermine. Kontaktpersonen werden übergangen. Die Spalten der Tabellen ergeben sich aus ihrer Kopfzeile.
/// </summary>
public static partial class TerminwunschBericht
{
    /// <summary>Kopf jeder Seite (Verband, Staffel, „Terminwünsche“) liegt oberhalb dieser Höhe.</summary>
    private const double Seitenkopf = 90;

    private enum Abschnitt
    {
        Sonstiges,
        Terminwuensche,
        Sperrtermine,
    }

    /// <summary>Liest die Meldungen aus den Zeilen aller Seiten.</summary>
    /// <param name="zeilen">Die Zeilen in Lesereihenfolge.</param>
    /// <returns>Die Meldungen in der Reihenfolge des Berichts.</returns>
    /// <exception cref="FormatException">Im Text steht keine Mannschaft – vermutlich ein anderer Bericht.</exception>
    public static IReadOnlyList<Vereinsmeldung> Lesen(IEnumerable<Textzeile> zeilen)
    {
        ArgumentNullException.ThrowIfNull(zeilen);
        List<Textzeile> inhalt = zeilen
            .Where(z => z.Oben >= Seitenkopf && !z.Text.StartsWith("nu.Dokument", StringComparison.Ordinal))
            .ToList();
        IReadOnlyList<Vereinsmeldung> meldungen = new Leser(inhalt).Alle();
        if (meldungen.Count == 0)
        {
            throw new FormatException("Die Datei ist kein Bericht „Terminwünsche“ aus click-TT (keine Mannschaft gefunden).");
        }

        return meldungen;
    }

    [GeneratedRegex(@"^(.+) \((\d+)\) - (.+)$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Mannschaftskopf();

    [GeneratedRegex(@"^Spiellokal (\d)$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Spiellokalkopf();

    [GeneratedRegex(@"^\d+\. Spieltag", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Spieltagszeile();

    [GeneratedRegex(@"(\d\d\.\d\d\.\d{4}) (\d\d:\d\d)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DatumUhrzeit();

    [GeneratedRegex(@"\d\d\.\d\d\.\d{4}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Tagesdatum();

    /// <summary>Spalten der Tabelle „Terminwünsche“ (linke Kanten aus der Kopfzeile).</summary>
    private sealed record Spalten(double Datum, double Lokal, double Max, double Koppel);

    /// <summary>Liest Zeile für Zeile; je Mannschaft entsteht eine Meldung.</summary>
    private sealed class Leser(List<Textzeile> zeilen)
    {
        private readonly List<Vereinsmeldung> fertig = [];
        private int index;
        private Entwurf? aktuell;
        private Abschnitt abschnitt;
        private Spalten? spalten;

        public IReadOnlyList<Vereinsmeldung> Alle()
        {
            while (index < zeilen.Count)
            {
                Textzeile zeile = zeilen[index];
                index++;
                Verarbeiten(zeile);
            }

            Abschliessen();
            return fertig;
        }

        private static bool IstLokalEnde(string text) =>
            Spiellokalkopf().IsMatch(text) || text == "Zusatzinformationen" || text.StartsWith("Tel.", StringComparison.Ordinal)
            || text.StartsWith("Mobil", StringComparison.Ordinal) || text.StartsWith("E-Mail", StringComparison.Ordinal)
            || text.StartsWith("Fax", StringComparison.Ordinal);

        private static string Anhaengen(string bisher, string neu)
        {
            if (neu.Length == 0)
            {
                return bisher;
            }

            return bisher.EndsWith('-') ? bisher + neu : (bisher + " " + neu).Trim();
        }

        private void Verarbeiten(Textzeile zeile)
        {
            string text = zeile.Text;
            Match kopf = Mannschaftskopf().Match(text);
            if (kopf.Success)
            {
                Abschliessen();
                aktuell = new Entwurf(kopf.Groups[1].Value, int.Parse(kopf.Groups[2].Value, CultureInfo.InvariantCulture), kopf.Groups[3].Value);
                abschnitt = Abschnitt.Sonstiges;
                return;
            }

            if (aktuell is null)
            {
                return;
            }

            if (Spiellokalkopf().Match(text) is { Success: true } lokal)
            {
                SpiellokalLesen(int.Parse(lokal.Groups[1].Value, CultureInfo.InvariantCulture));
            }
            else if (text.StartsWith("Bemerkung:", StringComparison.Ordinal))
            {
                BemerkungLesen(text["Bemerkung:".Length..].Trim());
            }
            else if (text.StartsWith("Terminwünsche ", StringComparison.Ordinal))
            {
                abschnitt = Abschnitt.Terminwuensche;
            }
            else if (text == "Sperrtermine")
            {
                abschnitt = Abschnitt.Sperrtermine;
            }
            else if (text is "Auswärtskoppelspiele" || text.StartsWith("Gesperrte Auswärtsspiele", StringComparison.Ordinal))
            {
                abschnitt = Abschnitt.Sonstiges;
            }
            else
            {
                Inhalt(zeile);
            }
        }

        private void Inhalt(Textzeile zeile)
        {
            if (abschnitt == Abschnitt.Sperrtermine)
            {
                foreach (Match datum in Tagesdatum().Matches(zeile.Text))
                {
                    aktuell!.Sperrtermine.Add(DateOnly.ParseExact(datum.Value, "dd.MM.yyyy", CultureInfo.InvariantCulture));
                }
            }
            else if (abschnitt == Abschnitt.Terminwuensche)
            {
                TerminLesen(zeile);
            }
        }

        private void TerminLesen(Textzeile zeile)
        {
            if (zeile.Text.StartsWith("Spieltag ", StringComparison.Ordinal))
            {
                double Links(string wort) => zeile.Woerter.FirstOrDefault(w => w.Text == wort)?.Links ?? double.MaxValue;
                spalten = new Spalten(Links("Datum"), Links("Spiellokal"), Links("max."), Links("Heimkoppelspiel"));
                return;
            }

            if (spalten is null)
            {
                return;
            }

            if (Spieltagszeile().IsMatch(zeile.Text)
                && DatumUhrzeit().Match(zeile.Spalte(spalten.Datum, spalten.Lokal)) is { Success: true } zeit)
            {
                aktuell!.Termine.Add(new TerminEntwurf(
                    DateTime.ParseExact(zeit.Groups[1].Value + " " + zeit.Groups[2].Value, "dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture),
                    zeile.Spalte(spalten.Lokal, spalten.Max),
                    zeile.Spalte(spalten.Max, spalten.Koppel),
                    zeile.Spalte(spalten.Koppel, double.MaxValue)));
            }
            else if (aktuell!.Termine.Count > 0)
            {
                // Umbruch innerhalb der Zeile eines Termins (lange Hallennamen, „wenn möglich“).
                TerminEntwurf letzter = aktuell.Termine[^1];
                letzter.Halle = Anhaengen(letzter.Halle, zeile.Spalte(spalten.Lokal, spalten.Max));
                letzter.Max = Anhaengen(letzter.Max, zeile.Spalte(spalten.Max, spalten.Koppel));
                letzter.Koppel = Anhaengen(letzter.Koppel, zeile.Spalte(spalten.Koppel, double.MaxValue));
            }
        }

        /// <summary>Adresse bis zur nächsten Überschrift; Telefon und E-Mail des Spiellokals werden übergangen.</summary>
        private void SpiellokalLesen(int nummer)
        {
            var teile = new List<string>();
            while (index < zeilen.Count && !IstLokalEnde(zeilen[index].Text))
            {
                teile.Add(zeilen[index].Text);
                index++;
            }

            string adresse = string.Join(' ', teile).Trim().TrimEnd(',').Trim();
            if (adresse.Length > 0)
            {
                aktuell!.Spiellokale[nummer] = Spiellokaladresse.Aus(adresse);
            }
        }

        /// <summary>Bemerkung bis zur Tabelle der Terminwünsche (kann über mehrere Zeilen gehen).</summary>
        private void BemerkungLesen(string erste)
        {
            var teile = new List<string> { erste };
            while (index < zeilen.Count && !zeilen[index].Text.StartsWith("Terminwünsche ", StringComparison.Ordinal)
                && !Mannschaftskopf().IsMatch(zeilen[index].Text))
            {
                teile.Add(zeilen[index].Text);
                index++;
            }

            aktuell!.Bemerkung = string.Join(' ', teile.Where(t => t.Length > 0)).Trim();
        }

        private void Abschliessen()
        {
            if (aktuell is not null)
            {
                fertig.Add(aktuell.Meldung());
                aktuell = null;
            }

            spalten = null;
        }
    }

    private sealed class TerminEntwurf(DateTime zeitpunkt, string halle, string max, string koppel)
    {
        public DateTime Zeitpunkt { get; } = zeitpunkt;

        public string Halle { get; set; } = halle;

        public string Max { get; set; } = max;

        public string Koppel { get; set; } = koppel;
    }

    private sealed class Entwurf(string verein, int nummer, string klasse)
    {
        public Dictionary<int, Spiellokaladresse> Spiellokale { get; } = [];

        public List<TerminEntwurf> Termine { get; } = [];

        public List<DateOnly> Sperrtermine { get; } = [];

        public string Bemerkung { get; set; } = string.Empty;

        public Vereinsmeldung Meldung() => new(
            verein,
            nummer,
            klasse,
            Spiellokale,
            Bemerkung,
            Termine.Select(t => new GemeldeterHeimtermin(t.Zeitpunkt, t.Halle, t.Max, t.Koppel)).ToList(),
            Sperrtermine);
    }
}
