// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text;
using AndiGenerator.Domain.Turniere;

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Prüft einen Plan aus Turniertagen gegen die Meldungen der Mannschaften: Form der Turniere, jede Paarung genau einmal,
/// höchstens ein Turnier je Mannschaft und Tag, Ausrichter kann zuhause (und beim Vierer zwei Spiele parallel), die
/// übrigen können auswärts, jede Mannschaft richtet mindestens einmal ein Turnier aus. Einzelbegegnungen (bei gerader
/// Mannschaftszahl) werden ebenso auf Heim und Auswärts geprüft, zählen aber nicht als ausgerichtetes Turnier; mehr als
/// eine je Mannschaft wird gemeldet.
/// </summary>
public static class Turnierpruefung
{
    /// <summary>Prüft die Turniere.</summary>
    /// <param name="turniere">Die Turniere des Plans.</param>
    /// <param name="mannschaften">Alle Mannschaften der Staffel.</param>
    /// <param name="meldungen">Die Meldungen.</param>
    /// <returns>Das Ergebnis mit dem gewählten Ausrichter je Turnier und allen Befunden.</returns>
    public static Turnierpruefungsergebnis Pruefen(
        IReadOnlyList<Turnier> turniere,
        IReadOnlyList<string> mannschaften,
        IReadOnlyList<Turniermeldung> meldungen)
    {
        ArgumentNullException.ThrowIfNull(turniere);
        ArgumentNullException.ThrowIfNull(mannschaften);
        ArgumentNullException.ThrowIfNull(meldungen);
        var befunde = new List<string>();
        Dictionary<string, Turniermeldung?> meldungJe = mannschaften.ToDictionary(m => m, m => Meldung(m, meldungen), StringComparer.Ordinal);
        befunde.AddRange(mannschaften.Where(m => meldungJe[m] is null).Select(m => $"{m}: keine Meldung gefunden – an allen Tagen als möglich angenommen."));
        befunde.AddRange(Paarungen(turniere, mannschaften));
        befunde.AddRange(Doppelte(turniere));
        befunde.AddRange(turniere.Where(t => t.Form == Turnierform.Einzel).SelectMany(t => t.Mannschaften)
            .GroupBy(m => m, StringComparer.Ordinal).Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} hat {g.Count()} Einzelbegegnungen."));

        string?[] gewaehlt = Ausrichterverteilung.Verteilen(
            turniere,
            mannschaften,
            (t, m) => Kann(t, m, meldungJe),
            (t, a) => t.Mannschaften.Count(m => m != a && !KannAuswaerts(t, m, meldungJe)));
        var ergebnis = new List<Turnierbewertung>();
        var ausgerichtet = mannschaften.ToDictionary(m => m, _ => 0, StringComparer.Ordinal);
        for (int i = 0; i < turniere.Count; i++)
        {
            Turnier turnier = turniere[i];
            string? ausrichter = gewaehlt[i];
            var hinweise = new List<string>();
            if (turnier.Form == Turnierform.Unbekannt)
            {
                hinweise.Add($"Keine bekannte Form ({turnier.Mannschaften.Count} Mannschaften, {turnier.Spiele.Count} Spiele).");
            }

            if (ausrichter is null)
            {
                string gruende = string.Join("; ", turnier.MoeglicheAusrichter.Select(m => $"{m}: {Grund(m, turnier, meldungJe)}"));
                hinweise.Add($"Kein möglicher Ausrichter ({gruende}).");
            }
            else if (turnier.Form != Turnierform.Einzel)
            {
                ausgerichtet[ausrichter] = ausgerichtet.GetValueOrDefault(ausrichter) + 1;
            }

            foreach (string gast in turnier.Mannschaften.Where(m => m != ausrichter && !KannAuswaerts(turnier, m, meldungJe)))
            {
                hinweise.Add($"{gast} kann an diesem Tag nicht auswärts spielen ({Text(meldungJe[gast]!.An(turnier.Tag))}).");
            }

            befunde.AddRange(hinweise.Select(h => $"{Datum(turnier.Tag)}: {h}"));
            ergebnis.Add(new Turnierbewertung(turnier, ausrichter, hinweise));
        }

        befunde.AddRange(mannschaften.Where(m => ausgerichtet[m] == 0).Select(m => $"{m} richtet kein Turnier aus."));
        return new Turnierpruefungsergebnis(ergebnis, befunde, ausgerichtet);
    }

    /// <summary>Sucht die Meldung zu einer Mannschaft: gleicher Name, sonst ohne Satz- und Leerzeichen, sonst Teil des Namens.</summary>
    /// <param name="mannschaft">Name aus der click-TT-Datei.</param>
    /// <param name="meldungen">Die Meldungen.</param>
    /// <returns>Die Meldung oder <c>null</c>.</returns>
    public static Turniermeldung? Meldung(string mannschaft, IReadOnlyList<Turniermeldung> meldungen)
    {
        ArgumentNullException.ThrowIfNull(meldungen);
        string gesucht = Kern(mannschaft);
        return meldungen.FirstOrDefault(m => string.Equals(m.Mannschaft, mannschaft, StringComparison.OrdinalIgnoreCase))
            ?? meldungen.FirstOrDefault(m => Kern(m.Mannschaft) == gesucht)
            ?? meldungen.FirstOrDefault(m => Kern(m.Mannschaft).Length > 3 && (gesucht.Contains(Kern(m.Mannschaft), StringComparison.Ordinal) || Kern(m.Mannschaft).Contains(gesucht, StringComparison.Ordinal)));
    }

    /// <summary>Text einer Bereitschaft.</summary>
    /// <param name="b">Die Bereitschaft.</param>
    /// <returns>Z. B. „nur zuhause“.</returns>
    public static string Text(Turnierbereitschaft b) => b switch
    {
        Turnierbereitschaft.Heim | Turnierbereitschaft.Auswaerts => "zuhause und auswärts",
        Turnierbereitschaft.Heim => "nur zuhause",
        Turnierbereitschaft.Auswaerts => "nur auswärts",
        _ => "kann nicht",
    };

    /// <summary>Ausrichter muss zuhause können, beim Vierer mit zwei Spielen parallel; ohne Meldung gilt alles als möglich.</summary>
    private static bool Kann(Turnier turnier, string mannschaft, Dictionary<string, Turniermeldung?> meldungJe) =>
        meldungJe.GetValueOrDefault(mannschaft) is not { } meldung
        || (meldung.An(turnier.Tag).HasFlag(Turnierbereitschaft.Heim) && (turnier.Form != Turnierform.Vierer || meldung.ZweiTische));

    private static bool KannAuswaerts(Turnier turnier, string mannschaft, Dictionary<string, Turniermeldung?> meldungJe) =>
        meldungJe.GetValueOrDefault(mannschaft) is not { } meldung || meldung.An(turnier.Tag).HasFlag(Turnierbereitschaft.Auswaerts);

    private static string Grund(string mannschaft, Turnier turnier, Dictionary<string, Turniermeldung?> meldungJe)
    {
        Turniermeldung meldung = meldungJe[mannschaft]!;
        Turnierbereitschaft b = meldung.An(turnier.Tag);
        return b.HasFlag(Turnierbereitschaft.Heim) ? "Halle nur für ein Spiel gleichzeitig" : Text(b);
    }

    /// <summary>Jede Paarung genau einmal.</summary>
    private static IEnumerable<string> Paarungen(IReadOnlyList<Turnier> turniere, IReadOnlyList<string> mannschaften)
    {
        var anzahl = new Dictionary<(string Erste, string Zweite), int>();
        foreach ((string Erste, string Zweite) paar in turniere.SelectMany(t => t.Spiele).Select(s => Turniererkennung.Paar(s.Heim, s.Gast)))
        {
            anzahl[paar] = anzahl.GetValueOrDefault(paar) + 1;
        }

        for (int i = 0; i < mannschaften.Count; i++)
        {
            for (int j = i + 1; j < mannschaften.Count; j++)
            {
                (string Erste, string Zweite) paar = Turniererkennung.Paar(mannschaften[i], mannschaften[j]);
                int n = anzahl.GetValueOrDefault(paar);
                if (n != 1)
                {
                    yield return n == 0
                        ? $"Paarung {paar.Erste} – {paar.Zweite} fehlt."
                        : $"Paarung {paar.Erste} – {paar.Zweite} kommt {n}-mal vor.";
                }
            }
        }
    }

    /// <summary>Höchstens ein Turnier je Mannschaft und Tag.</summary>
    private static IEnumerable<string> Doppelte(IReadOnlyList<Turnier> turniere) =>
        turniere.SelectMany(t => t.Mannschaften.Select(m => (t.Tag, Mannschaft: m)))
            .GroupBy(x => x)
            .Where(g => g.Count() > 1)
            .Select(g => $"{Datum(g.Key.Tag)}: {g.Key.Mannschaft} spielt in {g.Count()} Turnieren.");

    private static string Datum(DateOnly tag) => tag.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>Name nur aus Buchstaben und Ziffern, klein, Umlaute vereinfacht.</summary>
    private static string Kern(string name)
    {
        var text = new StringBuilder();
        foreach (char c in name.ToLowerInvariant().Replace("ä", "ae", StringComparison.Ordinal).Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal).Replace("ß", "ss", StringComparison.Ordinal).Where(char.IsLetterOrDigit))
        {
            text.Append(c);
        }

        return text.ToString();
    }
}
