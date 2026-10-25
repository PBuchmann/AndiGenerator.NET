// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Globalization;
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

    /// <summary>Seit wann HiGHS am Schritt <see cref="highsFortschritt"/> rechnet (Zeitstempel).</summary>
    private long highsSeit;

    /// <summary>Bricht die laufende Rechnung ab, wenn eine neue beginnt (andere Staffel, Optionen oder Einteilung).</summary>
    private CancellationTokenSource? schrankeAbbruch;

    /// <summary>
    /// Die Kriterien, die das Modell von HiGHS für die Staffel und Optionen nicht kennt (für <see cref="ModellUnbekannt"/>);
    /// <c>null</c>, solange nicht bestimmt oder ohne Modell.
    /// </summary>
    private (Staffel? Staffel, Berechnungsoptionen? Optionen, IReadOnlyList<Kostenkriterium>? Unbekannt) modellUnbekannt;

    /// <summary>Holt, woran HiGHS gerade rechnet, z. B. <c>A3 Parallele Spiele</c>; leer, wenn nicht gerechnet wird.</summary>
    internal string SchrankeFortschritt => schrankeFortschritt;

    /// <summary>Holt die Festlegung des Staffelleiters (Name und Obergrenzen je Kriterium); <c>null</c> = keine.</summary>
    internal (string Name, IReadOnlyDictionary<Kostenkriterium, int> Grenzen)? Festlegung => festlegung;

    /// <summary>
    /// Holt die Kriterien, die HiGHS für die geöffnete Staffel nicht kennt: Sie bleiben in C (keine Grenze, keine Varianten – in A
    /// oder B ergäben sie keinen Sinn); leer, wenn HiGHS alle kennt; <c>null</c>, wenn HiGHS fehlt oder es noch nicht bestimmt ist.
    /// </summary>
    internal IReadOnlyList<Kostenkriterium>? ModellUnbekannt =>
        sitzung is Plansitzung s && ReferenceEquals(modellUnbekannt.Staffel, s.Staffel) && ReferenceEquals(modellUnbekannt.Optionen, s.Optionen) ? modellUnbekannt.Unbekannt : null;

    /// <summary>Name einer Alternative, z. B. <c>Vorrang Heimkoppel (bestmöglich)</c>, <c>Plan B optimal</c> oder <c>Plan B1–B7 optimal</c>.</summary>
    /// <param name="alternative">Die Alternative.</param>
    /// <returns>Der Name.</returns>
    internal static string Alternativname(Planalternative alternative) => alternative switch
    {
        { Kriterium: null } => alternative.Name,
        { Bewiesen: true } => $"Vorrang {alternative.Name} (bestmöglich)",
        _ => $"Vorrang {alternative.Name}",
    };

    /// <summary>Warum ein Kriterium in C bleibt (HiGHS kennt es für diese Staffel nicht); leer, wenn es nach A oder B darf.</summary>
    /// <param name="k">Das Kriterium.</param>
    /// <returns>Der Grund oder leer.</returns>
    internal string NurCGrund(Kostenkriterium k) =>
        ModellUnbekannt?.Contains(k) == true
            ? "HiGHS kennt dieses Kriterium für diese Staffel nicht: In A oder B gäbe es dafür weder Grenze noch Varianten – es bleibt in C."
            : string.Empty;

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
    /// Holt die B-Varianten, die gegenüber dem Plan etwas besser machen (siehe <see cref="Variantenauswahl"/>) – nur sie stehen
    /// zur Wahl.
    /// </summary>
    /// <param name="angezeigt">Der Plan, mit dem verglichen wird; <c>null</c> = der laufende.</param>
    /// <returns>Die Varianten mit einer Besonderheit.</returns>
    internal IReadOnlyList<(Planalternative Alternative, Planbewertung Bewertung)> BesondereVarianten(Planstand? angezeigt) =>
        Variantenauswahl.Besondere(AlternativenHolen(), (angezeigt ?? laufenderStand)?.Bewertung, Einteilung);

    /// <summary>
    /// Zeigt die Alternativen in B im bildschirmfüllenden Dialog, zuerst ausgewählt <paramref name="start"/>; übernimmt der
    /// Staffelleiter eine, sind A und B damit festgelegt.
    /// </summary>
    /// <param name="start">Die Alternative, die zuerst ausgewählt ist.</param>
    /// <param name="angezeigt">Der Plan, mit dem verglichen wird (der in der Qualitätsansicht angezeigte).</param>
    /// <returns>Erledigt, wenn der Dialog geschlossen ist.</returns>
    internal async Task AlternativenZeigenAsync(Planalternative? start, Planstand? angezeigt)
    {
        // Die Liste gilt für den Dialog, auch wenn währenddessen der Bericht nach der Stufe C kommt. Zur Wahl stehen nur Varianten,
        // die gegenüber dem Plan etwas besser machen – die übrigen machten ihn nur schlechter.
        if ((angezeigt ?? laufenderStand) is not Planstand aktuell)
        {
            return;
        }

        int alle = AlternativenHolen().Count;
        IReadOnlyList<(Planalternative Alternative, Planbewertung Bewertung)> liste = BesondereVarianten(aktuell);
        if (liste.Count == 0)
        {
            return;
        }

        string ausgeblendet = alle > liste.Count
            ? string.Create(CultureInfo.GetCultureInfo("de-DE"), $"{alle - liste.Count} weitere Varianten sind ausgeblendet: Der aktuelle Plan ist in ihrem Kriterium schon so gut oder besser.")
            : string.Empty;
        string hinweis = string.Join(" ", new[] { UnbekanntHinweis(), ausgeblendet }.Where(t => t.Length > 0));

        var dialog = new AlternativendialogViewModel(
            aktuell,
            liste,
            start,
            Einteilung,
            (plan, k) => Kriteriumsdetails(false, k, plan),
            Laeuft && dienst.Automodus,
            hinweis);
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
    /// Hinweis, welche A- und B-Kriterien HiGHS nicht kennt (sie fehlen in Grenzen und Varianten), z. B. <c>HiGHS kennt B8
    /// Abstand H/A nicht: …</c>; leer, wenn es alle kennt.
    /// </summary>
    private string UnbekanntHinweis()
    {
        if (schranke is not { Unbekannt: { Count: > 0 } unbekannt })
        {
            return string.Empty;
        }

        Stufeneinteilung einteilung = Einteilung.Vervollstaendigt();
        IEnumerable<string> namen = new[] { ("A", einteilung.A, 2), ("B", einteilung.B, 1) }
            .SelectMany(s => s.Item2.Select((k, i) => (Stufe: $"{s.Item1}{i + s.Item3}", Kriterium: k)))
            .Where(e => unbekannt.Contains(e.Kriterium))
            .Select(e => $"{e.Stufe} {Planqualitaet.Name(e.Kriterium)}");
        return $"HiGHS kennt {string.Join(", ", namen)} für diese Staffel nicht: In den Varianten und in der Grenze der Stufe fehlt es – „optimal“ gilt nur für die übrigen Kriterien der Stufe.";
    }

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

            // Ein besserer Plan mitten in einem Schritt: nur anbieten.
            if (f.NurPlan)
            {
                if (f.Plan is { Count: > 0 } neu && Laeuft && dienst.Automodus)
                {
                    dienst.PlanAnbieten(neu, $"HiGHS ({f.Text})", inselnNachziehen: false);
                }

                return;
            }

            // Stufe A fertig: Die Teilneuplanung wartet nicht länger (sie senkt B meist schneller als die weiteren Schritte).
            if (f.AFertig)
            {
                dienst.HighsVorrang = false;
            }

            // Stufe A bewiesen: Der Automodus hört sofort auf, A zu drängen (nicht erst nach Stufe B).
            if (f.SchrankeA is int a && Laeuft && dienst.Automodus)
            {
                dienst.HighsEinspielen([], a, null);
            }

            if (f.Plan is { Count: > 0 } plan && Laeuft && dienst.Automodus)
            {
                dienst.PlanAnbieten(plan, $"Zwischenplan aus HiGHS (vor {f.Text})");
            }

            if (highsFortschritt?.Text != f.Text)
            {
                highsSeit = Stopwatch.GetTimestamp();
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

    /// <summary>Die rechte Hälfte der Kachel „Berechnete Pläne“: woran HiGHS gerade rechnet; <c>null</c> außerhalb des Automodus.</summary>
    /// <param name="stand">Der Stand der Generierung.</param>
    /// <returns>Die Anzeige.</returns>
    private Highsanzeige? HighsanzeigeBilden(Optimierungsstand stand)
    {
        if (stand.Lenkung is null)
        {
            return null;
        }

        TimeSpan seit = highsFortschritt is null ? TimeSpan.Zero : Stopwatch.GetElapsedTime(highsSeit);
        Highsanzeige anzeige = Highsanzeige.Bilden(
            Stufenschranke.Verfuegbar,
            stand.Pausiert,
            highsFortschritt,
            seit,
            Stufenschranke.Laufzeit,
            schranke is not null && highsFortschritt is null,
            stand.Teilneuplanung);
        return anzeige with { Varianten = festlegung is null ? BesondereVarianten(null).Count : 0 };
    }

    /// <summary>Kachel „HiGHS“: die B-Varianten im Dialog, zuerst ausgewählt der Plan mit der niedrigsten Summe in B.</summary>
    private Task BVariantenZeigenAsync()
    {
        IReadOnlyList<(Planalternative Alternative, Planbewertung Bewertung)> liste = BesondereVarianten(null);
        if (liste.Count == 0)
        {
            return Task.CompletedTask;
        }

        Planalternative start = liste.FirstOrDefault(e => e.Alternative.Kriterium is null).Alternative ?? liste[0].Alternative;
        return AlternativenZeigenAsync(start, null);
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
            // Erst, was das Modell nicht kennt (schnell): Das zeigt die Qualitätsansicht beim Einstufen schon, bevor die
            // Schranke da ist.
            if (!ReferenceEquals(modellUnbekannt.Staffel, schluessel.Staffel) || !ReferenceEquals(modellUnbekannt.Optionen, schluessel.Optionen))
            {
                IReadOnlyList<Kostenkriterium>? unbekannt = await Task.Run(() => Stufenschranke.Unbekannt(schluessel.Staffel, schluessel.Optionen), CancellationToken.None).ConfigureAwait(false);
                oberflaeche.AufOberflaeche(() =>
                {
                    if (!Aktuell(schluessel))
                    {
                        return;
                    }

                    modellUnbekannt = (schluessel.Staffel, schluessel.Optionen, unbekannt);
                    foreach (QualitaetAnsichtViewModel qualitaet in ansichten.OfType<QualitaetAnsichtViewModel>())
                    {
                        qualitaet.VerfahrenGeaendert();
                    }
                });
            }

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
