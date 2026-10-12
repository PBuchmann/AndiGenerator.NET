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
/// eine je Mannschaft wird gemeldet. Für Turniere mit Befund werden Ausweichtage vorgeschlagen. Es zählen nur die
/// Meldungen; Sperrtermine und spielfreie Tage aus click-TT gelten für Turnierrunden nicht.
/// </summary>
public static class Turnierpruefung
{
    /// <summary>Prüft die Turniere gegen die Meldungen.</summary>
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
        var kontext = new Kontext(
            turniere,
            mannschaften.ToDictionary(m => m, m => Meldung(m, meldungen), StringComparer.Ordinal),
            meldungen.SelectMany(m => m.Tage.Keys).Distinct().Order().ToList());
        var befunde = new List<string>();
        befunde.AddRange(mannschaften.Where(m => kontext.MeldungJe[m] is null).Select(m => $"{m}: keine Meldung gefunden – an allen Tagen als möglich angenommen."));
        befunde.AddRange(Paarungen(turniere, mannschaften));
        befunde.AddRange(Doppelte(turniere));
        befunde.AddRange(turniere.Where(t => t.Form == Turnierform.Einzel).SelectMany(t => t.Mannschaften)
            .GroupBy(m => m, StringComparer.Ordinal).Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} hat {g.Count()} Einzelbegegnungen."));

        string?[] gewaehlt = Ausrichterverteilung.Verteilen(
            turniere,
            mannschaften,
            (t, m) => Kann(t, m, kontext.MeldungJe),
            (t, a) => t.Mannschaften.Count(m => m != a && !KannAuswaerts(t, m, kontext.MeldungJe)));
        var ergebnis = new List<Turnierbewertung>();
        var ausgerichtet = mannschaften.ToDictionary(m => m, _ => 0, StringComparer.Ordinal);
        for (int i = 0; i < turniere.Count; i++)
        {
            Turnierbewertung bewertung = Bewerten(turniere[i], gewaehlt[i], kontext);
            if (bewertung.Ausrichter is { } ausrichter && turniere[i].Form != Turnierform.Einzel)
            {
                ausgerichtet[ausrichter] = ausgerichtet.GetValueOrDefault(ausrichter) + 1;
            }

            befunde.AddRange(bewertung.Hinweise.Select(h => $"{Datum(turniere[i].Tag)}: {h}"));
            ergebnis.Add(bewertung);
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

    private static Turnierbewertung Bewerten(Turnier turnier, string? ausrichter, Kontext kontext)
    {
        var hinweise = new List<string>();
        if (turnier.Form == Turnierform.Unbekannt)
        {
            hinweise.Add($"Keine bekannte Form ({turnier.Mannschaften.Count} Mannschaften, {turnier.Spiele.Count} Spiele).");
        }

        if (ausrichter is null)
        {
            string gruende = string.Join("; ", turnier.MoeglicheAusrichter.Select(m => $"{m}: {Grund(m, turnier, kontext.MeldungJe)}"));
            hinweise.Add($"Kein möglicher Ausrichter ({gruende}).");
        }

        hinweise.AddRange(turnier.Mannschaften.Where(m => m != ausrichter && !KannAuswaerts(turnier, m, kontext.MeldungJe))
            .Select(m => $"{m} kann an diesem Tag nicht auswärts spielen ({Text(kontext.MeldungJe[m]!.An(turnier.Tag))})."));
        return new Turnierbewertung(turnier, ausrichter, hinweise, hinweise.Count > 0 ? Ausweichtage(turnier, kontext) : []);
    }

    /// <summary>
    /// Tage aus den Meldungen, an denen das Turnier ohne Befund stattfinden könnte: keine der Mannschaften spielt dort
    /// ein anderes Turnier, ein möglicher Ausrichter kann zuhause und alle anderen können auswärts.
    /// </summary>
    private static List<DateOnly> Ausweichtage(Turnier turnier, Kontext kontext)
    {
        var belegt = kontext.Turniere.Where(t => !ReferenceEquals(t, turnier))
            .SelectMany(t => t.Mannschaften.Select(m => (t.Tag, m)))
            .ToHashSet();
        return kontext.Tage
            .Where(tag => tag != turnier.Tag && turnier.Mannschaften.All(m => !belegt.Contains((tag, m))))
            .Where(tag => Ausrichtbar(turnier with { Tag = tag }, kontext.MeldungJe))
            .ToList();
    }

    private static bool Ausrichtbar(Turnier turnier, Dictionary<string, Turniermeldung?> meldungJe) =>
        turnier.MoeglicheAusrichter.Any(a => Kann(turnier, a, meldungJe) && turnier.Mannschaften.All(m => m == a || KannAuswaerts(turnier, m, meldungJe)));

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

    /// <summary>Was alle Turniere einer Prüfung gemeinsam brauchen.</summary>
    /// <param name="Turniere">Alle Turniere des Plans.</param>
    /// <param name="MeldungJe">Meldung je Mannschaft der Staffel oder <c>null</c>.</param>
    /// <param name="Tage">Alle Tage der Meldungen, aufsteigend.</param>
    private sealed record Kontext(
        IReadOnlyList<Turnier> Turniere,
        Dictionary<string, Turniermeldung?> MeldungJe,
        IReadOnlyList<DateOnly> Tage);
}
