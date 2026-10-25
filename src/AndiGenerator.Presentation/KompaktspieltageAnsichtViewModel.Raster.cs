// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Turniere;
using AndiGenerator.Engine.Turniere;

namespace AndiGenerator.Presentation;

/// <summary>Zeilen des Rasters „Meldungen und Plan“ – für die Ansicht und den Ausdruck.</summary>
public sealed partial class KompaktspieltageAnsichtViewModel
{
    /// <summary>
    /// Eine Zeile des Rasters „Meldungen und Plan“ (auch für den Ausdruck); gedimmt ist, was nicht zur Auswahl gehört
    /// (andere Mannschaften bzw. andere Tage).
    /// </summary>
    /// <param name="a">Die Auswertung.</param>
    /// <param name="tag">Der Tag der Zeile.</param>
    /// <param name="gewaehlt">Ob der Tag gewählt ist.</param>
    /// <param name="team">Die gewählte Mannschaft oder <c>null</c>.</param>
    /// <param name="andererTag">Ob ein anderer Tag gewählt ist (die Zeile wird gedimmt).</param>
    /// <param name="rahmen">Feld mit dem Rahmen beim Bearbeiten oder <c>null</c>.</param>
    /// <param name="gruppentage">Spitzen- und Abstiegsspieltag des Plans (für die Ringe).</param>
    /// <returns>Die Zeile.</returns>
    internal static Kompaktrasterzeile Rasterzeile(
        Kompaktauswertung a,
        DateOnly tag,
        bool gewaehlt,
        string? team,
        bool andererTag,
        (string Mannschaft, DateOnly Tag)? rahmen,
        (Kompaktspieltag? Spitze, Kompaktspieltag? Keller) gruppentage)
    {
        var zellen = new List<Kompaktzelle>();
        foreach (Kompaktmannschaft m in a.Mannschaften)
        {
            Turnierbereitschaft? b = m.Meldung?.An(tag);
            Kompaktspieltag? s = a.Spieltage.FirstOrDefault(x => x.Tag == tag && x.Mannschaften.Contains(m.Name));
            bool ausrichter = s is not null && s.Ausrichter == m.Name;
            bool verstoss = s is not null && b is Turnierbereitschaft bb
                && !bb.HasFlag(ausrichter ? Turnierbereitschaft.Heim : Turnierbereitschaft.Auswaerts);
            string zeichen = (s, b) switch
            {
                (not null, _) => ausrichter ? "★" : "●",
                (null, null) => "?",
                _ => string.Empty,
            };
            string meldung = b switch
            {
                null => "keine Meldung",
                Turnierbereitschaft.None => "kann nicht",
                Turnierbereitschaft.Heim => "nur Heim",
                Turnierbereitschaft.Auswaerts => "nur auswärts",
                _ => "Heim oder auswärts",
            };
            string rolle = string.Empty;
            if (s is not null)
            {
                rolle = ausrichter ? " · richtet aus" : " · Gast bei " + (s.Ausrichter ?? "?");
            }

            List<Kompaktspieltag> amTag = a.Spieltage.Where(x => x.Tag == tag).ToList();
            string termin = m.Termine.TryGetValue(tag, out string? text) ? Environment.NewLine + text : string.Empty;
            zellen.Add(new Kompaktzelle(Meldungsart(b), zeichen, verstoss, andererTag || (team is not null && team != m.Name), string.Create(Deutsch, $"{m.Name}, {tag:dd.MM.yyyy}: {meldung}{rolle}{termin}"))
            {
                Spieltag = s is null ? 0 : amTag.IndexOf(s),
                Einzel = s?.Form == Turnierform.Einzel,
                Mannschaft = m.Name,
                Tag = tag,
                IstCursor = rahmen == (m.Name, tag),
                Spitzenspieltag = s is not null && ReferenceEquals(s, gruppentage.Spitze),
                Abstiegsspieltag = s is not null && ReferenceEquals(s, gruppentage.Keller),
            });
        }

        // Sonntage wie bisher nur mit Datum, Samstage und Wochentage mit Kürzel (z. B. „Sa 12.09.“).
        string datum = tag.DayOfWeek == DayOfWeek.Sunday ? tag.ToString("dd.MM.", Deutsch) : tag.ToString("ddd dd.MM.", Deutsch);

        // Beobachtbar: Ändert eine Eingabe nur einzelne Felder, ersetzt der Abgleich nur diese (siehe ZeileGleich).
        return new Kompaktrasterzeile(tag, datum, gewaehlt, new System.Collections.ObjectModel.ObservableCollection<Kompaktzelle>(zellen));
    }
}
