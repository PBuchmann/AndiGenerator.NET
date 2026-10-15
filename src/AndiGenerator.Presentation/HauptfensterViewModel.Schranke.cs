// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Schranke;

namespace AndiGenerator.Presentation;

/// <summary>
/// Hauptfenster: untere Schranke aus HiGHS (MIGRATIONSPLAN O7) für die Spalte „Bestmöglich“ der Qualitätsansicht und den
/// Automodus – HiGHS rechnet nebenher; Schranke und Plan gehen in die laufende Generierung, sobald sie da sind.
/// </summary>
public sealed partial class HauptfensterViewModel
{
    /// <summary>Ohne geöffnete Staffel: keine Schranke, kein Grund.</summary>
    private static readonly Schrankenbericht KeineSchranke = new(0, 0, [], [], []);

    /// <summary>Wofür die Schranke berechnet wurde bzw. wird (Staffel und Optionen als Objekte, Einteilung als Text).</summary>
    private (Staffel? Staffel, Berechnungsoptionen? Optionen, string Einteilung) schrankeFuer;

    /// <summary>Die Schranke; <c>null</c>, solange sie berechnet wird.</summary>
    private Schrankenbericht? schranke = KeineSchranke;

    /// <summary>Die Alternativen in B der Schranke mit ihrer Bewertung (im Hintergrund bewertet).</summary>
    private IReadOnlyList<(Planalternative Alternative, Planbewertung Bewertung)> alternativen = [];

    /// <summary>Festlegung des Staffelleiters (gewählte Alternative): Name und je Kriterium die Obergrenze; <c>null</c> = keine.</summary>
    private (string Name, IReadOnlyDictionary<Kostenkriterium, int> Grenzen)? festlegung;

    /// <summary>Woran HiGHS gerade rechnet (leer, wenn nicht gerechnet wird).</summary>
    private string schrankeFortschritt = string.Empty;

    /// <summary>Letzter Fortschritt von HiGHS für den Leitstand (auch, wenn er erst während der Rechnung geöffnet wird).</summary>
    private Schrankenfortschritt? highsFortschritt;

    /// <summary>Bricht die laufende Rechnung ab, wenn eine neue beginnt (andere Staffel, Optionen oder Einteilung).</summary>
    private CancellationTokenSource? schrankeAbbruch;

    /// <summary>Holt, woran HiGHS gerade rechnet, z. B. <c>A3 Parallele Spiele</c>; leer, wenn nicht gerechnet wird.</summary>
    internal string SchrankeFortschritt => schrankeFortschritt;

    /// <summary>Holt die Festlegung des Staffelleiters (Name und Obergrenzen je Kriterium); <c>null</c> = keine.</summary>
    internal (string Name, IReadOnlyDictionary<Kostenkriterium, int> Grenzen)? Festlegung => festlegung;

    /// <summary>Name einer Alternative, z. B. <c>Vorrang Heimkoppel (bestmöglich)</c> oder <c>Plan B optimal</c>.</summary>
    /// <param name="alternative">Die Alternative.</param>
    /// <returns>Der Name.</returns>
    internal static string Alternativname(Planalternative alternative) => alternative switch
    {
        { Kriterium: null } => alternative.Name,
        { Bewiesen: true } => $"Vorrang {alternative.Name} (bestmöglich)",
        _ => $"Vorrang {alternative.Name}",
    };

    /// <summary>
    /// Holt die untere Schranke für die geöffnete Staffel und die gespeicherte Einteilung. Ist sie noch nicht berechnet,
    /// beginnt die Rechnung im Hintergrund; danach werden die Qualitätsansichten erneuert und – im Automodus – Schranke und
    /// Plan in die laufende Generierung übernommen.
    /// </summary>
    /// <returns>
    /// Der Bericht mit den Stufen in der Rangfolge (ohne Stufen: der Grund in den Hinweisen); <c>null</c>, solange er
    /// berechnet wird.
    /// </returns>
    internal Schrankenbericht? SchrankeHolen()
    {
        if (sitzung is not Plansitzung s)
        {
            return KeineSchranke;
        }

        Stufeneinteilung einteilung = Einteilung.Vervollstaendigt();
        (Staffel, Berechnungsoptionen, string) schluessel = (s.Staffel, s.Optionen, einteilung.Text());
        if (!ReferenceEquals(schrankeFuer.Staffel, s.Staffel) || !ReferenceEquals(schrankeFuer.Optionen, s.Optionen) || schrankeFuer.Einteilung != schluessel.Item3)
        {
            schrankeFuer = schluessel;
            schranke = null;
            schrankeAbbruch?.Cancel();
            schrankeAbbruch?.Dispose();
            schrankeAbbruch = new CancellationTokenSource();
            _ = SchrankeBerechnenAsync(schluessel, einteilung, schrankeAbbruch.Token);
        }

        return schranke;
    }

    /// <summary>Holt die Alternativen in B der aktuellen Schranke mit ihrer Bewertung (leer, solange keine).</summary>
    /// <returns>Die Alternativen.</returns>
    internal IReadOnlyList<(Planalternative Alternative, Planbewertung Bewertung)> AlternativenHolen() => schranke is null ? [] : alternativen;

    /// <summary>
    /// Zeigt die Alternativen in B im bildschirmfüllenden Dialog, zuerst ausgewählt <paramref name="start"/>; übernimmt der
    /// Staffelleiter eine, sind A und B damit festgelegt.
    /// </summary>
    /// <param name="start">Die Alternative, die zuerst ausgewählt ist.</param>
    /// <param name="angezeigt">Der Plan, mit dem verglichen wird (der in der Qualitätsansicht angezeigte).</param>
    /// <returns>Erledigt, wenn der Dialog geschlossen ist.</returns>
    internal async Task AlternativenZeigenAsync(Planalternative? start, Planstand? angezeigt)
    {
        // Die Liste gilt für den Dialog, auch wenn währenddessen der Bericht nach der Stufe C kommt.
        IReadOnlyList<(Planalternative Alternative, Planbewertung Bewertung)> liste = alternativen;
        if (liste.Count == 0 || (angezeigt ?? laufenderStand) is not Planstand aktuell)
        {
            return;
        }

        var dialog = new AlternativendialogViewModel(
            aktuell,
            liste,
            start,
            Einteilung,
            (plan, k) => Kriteriumsdetails(false, k, plan),
            Laeuft && dienst.Automodus);
        if (await oberflaeche.AlternativenZeigenAsync(dialog) && dialog.Gewaehlt is { } gewaehlt
            && liste.FirstOrDefault(a => ReferenceEquals(a.Alternative, gewaehlt)) is { Alternative: not null } paar)
        {
            AlternativeFestlegen(paar.Alternative, paar.Bewertung);
        }
    }

    /// <summary>
    /// Übernimmt eine Alternative (Qualitätsansicht): A und B sind damit entschieden – kein A- oder B-Kriterium darf mehr
    /// Verstöße haben als in ihr, der Plan wird übernommen, und der Automodus glättet nur noch C. Die Einteilung bleibt.
    /// </summary>
    /// <param name="alternative">Die Alternative.</param>
    /// <param name="bewertung">Ihre Bewertung.</param>
    internal void AlternativeFestlegen(Planalternative alternative, Planbewertung bewertung)
    {
        ArgumentNullException.ThrowIfNull(alternative);
        ArgumentNullException.ThrowIfNull(bewertung);
        Stufeneinteilung einteilung = Einteilung.Vervollstaendigt();
        Dictionary<Kostenkriterium, int> grenzen = Planqualitaet.Kriterien(bewertung, einteilung)
            .Where(k => k.Stufe[0] is 'A' or 'B' && k.Kriterium is not null && k.Anzahl is not null)
            .ToDictionary(k => k.Kriterium!.Value, k => k.Anzahl!.Value);
        string name = Alternativname(alternative);
        festlegung = (name, grenzen);
        dienst.Festlegen(grenzen, name, alternative.Plan);
        foreach (QualitaetAnsichtViewModel qualitaet in ansichten.OfType<QualitaetAnsichtViewModel>())
        {
            qualitaet.VerfahrenGeaendert();
        }
    }

    /// <summary>Hebt die Festlegung auf: Der Automodus drängt wieder A und B nach der Einteilung.</summary>
    internal void FestlegungAufheben()
    {
        festlegung = null;
        dienst.Festlegen(null, string.Empty, null);
        foreach (QualitaetAnsichtViewModel qualitaet in ansichten.OfType<QualitaetAnsichtViewModel>())
        {
            qualitaet.VerfahrenGeaendert();
        }
    }

    /// <summary>Bewertet die Varianten in B eines Berichts (im Rechen-Thread).</summary>
    private static List<(Planalternative Alternative, Planbewertung Bewertung)> Bewerten(Schrankenbericht bericht, (Staffel Staffel, Berechnungsoptionen Optionen, string Einteilung) schluessel) =>
        (bericht.Alternativen ?? [])
            .Select(x => (x, Referenzbewertung.Bewerten(schluessel.Staffel with { BestehenderSpielplan = x.Plan }, schluessel.Optionen)))
            .ToList();

    /// <summary>
    /// Automodus: Schranke und Plan aus HiGHS in die laufende Generierung übernehmen. Ist die Schranke noch nicht berechnet,
    /// beginnt die Rechnung; übernommen wird dann, sobald sie fertig ist.
    /// </summary>
    private void HighsEinspielen()
    {
        if (!Laeuft || !dienst.Automodus || SchrankeHolen() is not { Stufen.Count: > 0 } bericht)
        {
            return;
        }

        // Nur bewiesene Schranken (Summe je Stufe): Sie beenden das Drängen der Stufe. Die Alternativen in B werden ebenfalls
        // angeboten – ist eine nach Stufen besser (oft weniger B-Verstöße als der Plan der Summe), wird sie bester Plan.
        dienst.HighsEinspielen(bericht.Plan, bericht.SchrankeA, bericht.SchrankeB);
        foreach (Planalternative alternative in bericht.Alternativen ?? [])
        {
            dienst.PlanAnbieten(alternative.Plan, $"Alternative aus HiGHS ({Alternativname(alternative)})");
        }
    }

    /// <summary>
    /// Fortschritt von HiGHS (aus dem Rechen-Thread): in die Qualitätsansichten und in den Leitstand. Im Automodus geht der
    /// Plan des letzten Schritts gleich an die Generierung – ist er besser, wird er sofort bester Plan.
    /// </summary>
    private void FortschrittMelden(Schrankenfortschritt f, (Staffel Staffel, Berechnungsoptionen Optionen, string Einteilung) schluessel)
    {
        // Sind A und B fertig, kommen Schranken und Varianten in B schon vor der Stufe C (hier im Rechen-Thread bewertet).
        IReadOnlyList<(Planalternative Alternative, Planbewertung Bewertung)>? zwischen = f.Zwischenbericht is { } z ? Bewerten(z, schluessel) : null;
        oberflaeche.AufOberflaeche(() =>
        {
            // Ein veralteter Lauf (inzwischen andere Staffel, Optionen oder Einteilung) meldet nichts mehr.
            if (!Aktuell(schluessel))
            {
                return;
            }

            if (f.Zwischenbericht is { } bericht && zwischen is not null)
            {
                dienst.HighsVorrang = false;
                schranke = bericht;
                alternativen = zwischen;
                HighsEinspielen();
                foreach (QualitaetAnsichtViewModel qualitaet in ansichten.OfType<QualitaetAnsichtViewModel>())
                {
                    qualitaet.VerfahrenGeaendert();
                }
            }

            if (f.Plan is { Count: > 0 } plan && Laeuft && dienst.Automodus)
            {
                dienst.PlanAnbieten(plan, $"Zwischenplan aus HiGHS (vor {f.Text})");
            }

            schrankeFortschritt = f.Text;
            highsFortschritt = f;
            leitstandAnsicht?.HighsZeigen(f);
            foreach (QualitaetAnsichtViewModel qualitaet in ansichten.OfType<QualitaetAnsichtViewModel>())
            {
                qualitaet.FortschrittZeigen();
            }
        });
    }

    /// <summary>Gibt an, ob ein Lauf für die aktuelle Staffel, Optionen und Einteilung rechnet.</summary>
    private bool Aktuell((Staffel Staffel, Berechnungsoptionen Optionen, string Einteilung) schluessel) =>
        ReferenceEquals(schrankeFuer.Staffel, schluessel.Staffel) && ReferenceEquals(schrankeFuer.Optionen, schluessel.Optionen) && schrankeFuer.Einteilung == schluessel.Einteilung;

    private async Task SchrankeBerechnenAsync((Staffel Staffel, Berechnungsoptionen Optionen, string Einteilung) schluessel, Stufeneinteilung einteilung, CancellationToken abbruch)
    {
        Schrankenbericht ergebnis;
        IReadOnlyList<(Planalternative, Planbewertung)> bewertet = [];
        try
        {
            // Vorrang für HiGHS, bis A und B fertig sind (die Teilneuplanung wartet); Startlösung ist der beste Plan der Generierung.
            dienst.HighsVorrang = true;
            ergebnis = await Task.Run(() => Stufenschranke.Berechnen(schluessel.Staffel, schluessel.Optionen, einteilung, f => FortschrittMelden(f, schluessel), () => abbruch.IsCancellationRequested, dienst.BesterPlan), CancellationToken.None).ConfigureAwait(false);

            // Die Varianten gleich hier bewerten (nicht im Bedienfaden).
            bewertet = Bewerten(ergebnis, schluessel);
        }
        catch (Exception fehler) when (fehler is InvalidOperationException or ArgumentException or DllNotFoundException or EntryPointNotFoundException)
        {
            ergebnis = new Schrankenbericht(0, 0, [], ["Die Berechnung ist fehlgeschlagen: " + fehler.Message], []);
        }

        oberflaeche.AufOberflaeche(() =>
        {
            // Ein veralteter Lauf ändert nichts mehr (auch nicht die Anzeige des neuen).
            if (!Aktuell(schluessel))
            {
                return;
            }

            dienst.HighsVorrang = false;
            highsFortschritt = null;
            leitstandAnsicht?.HighsZeigen(null);
            schranke = ergebnis;
            alternativen = bewertet;
            schrankeFortschritt = string.Empty;
            HighsEinspielen();
            foreach (QualitaetAnsichtViewModel qualitaet in ansichten.OfType<QualitaetAnsichtViewModel>())
            {
                qualitaet.VerfahrenGeaendert();
            }
        });
    }
}
