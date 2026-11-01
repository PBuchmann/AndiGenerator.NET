// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Einfach;
using AndiGenerator.Engine.Referenz;
using AndiGenerator.Engine.Turniere;
using AndiGenerator.Import;
using AndiGenerator.Persistence.Gemeinsam;

namespace AndiGenerator.Application;

/// <summary>
/// Der normale Modus der Oberfläche (Peter, 29.10.2026): plant mit dem <see cref="Schnellplaner"/> im Hintergrund, bis der
/// Klassenleiter den Plan übernimmt (oder die Planung nach langem Stillstand selbst endet). Der beste Plan bleibt immer
/// erhalten: Er liegt als gemerkter Plan <see cref="BesterPlanName"/> im Staffelordner und wird nur durch einen besseren
/// ersetzt (<see cref="Uebernehmen"/>).
/// </summary>
public sealed class Normalplanung : IDisposable
{
    /// <summary>Name des gemerkten Plans mit dem besten Plan des normalen Modus.</summary>
    public const string BesterPlanName = "Bester Plan (normaler Modus)";

    private readonly Action beenden;

    private Normalplanung(Task<Schnellergebnis> lauf, Action beenden)
    {
        Lauf = lauf;
        this.beenden = beenden;
    }

    /// <summary>Gets die laufende Planung; ihr Ergebnis kommt nach <see cref="Beenden"/>.</summary>
    public Task<Schnellergebnis> Lauf { get; }

    /// <summary>Startet die Planung.</summary>
    /// <param name="sitzung">Die geöffnete Staffel.</param>
    /// <param name="melden">Bekommt jeden Stand (auf einem Hintergrund-Thread), oder <c>null</c>.</param>
    /// <param name="einstellungen">Zeiten und Kerne, oder <c>null</c> (Standard: alle Kerne bis auf einen).</param>
    /// <param name="loeser">Der Löser je Lauf, oder <c>null</c> (HiGHS, wenn verfügbar).</param>
    /// <param name="steuerung">Die Steuerung von außen (Wichtigkeit der Kriterien), oder <c>null</c> (Standard).</param>
    /// <returns>Die laufende Planung.</returns>
    public static Normalplanung Starten(Plansitzung sitzung, Action<Schnellstand>? melden = null, Schnellplanereinstellungen? einstellungen = null, Func<int, ISchrankenloeser>? loeser = null, Schnellsteuerung? steuerung = null)
    {
        ArgumentNullException.ThrowIfNull(sitzung);
        Staffel staffel = sitzung.Staffel with { BestehenderSpielplan = Vorrunde.Festhalten(sitzung.Staffel, sitzung.Optionen, null) };
        loeser ??= Highsbruecke.Verfuegbar ? samen => new Highsbruecke(new Highsvorgabe(samen)) : null;
        var abbruch = new CancellationTokenSource();
        Task<Schnellergebnis> lauf = Task.Run(() => Schnellplaner.Planen(staffel, sitzung.Optionen, loeser, einstellungen ?? Schnellplanereinstellungen.Standard, steuerung, melden, abbruch.Token));
        _ = lauf.ContinueWith(_ => abbruch.Dispose(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        return new Normalplanung(lauf, () =>
        {
            if (!lauf.IsCompleted)
            {
                abbruch.Cancel();
            }
        });
    }

    /// <summary>
    /// Übernimmt einen Plan als besten Plan der Staffel – nur, wenn er besser ist als der bisher beste (erst Pflicht, dann
    /// Wünsche, dann Güte); sonst bleibt der bisherige.
    /// </summary>
    /// <param name="sitzung">Die Staffel.</param>
    /// <param name="neu">Der neue Plan.</param>
    /// <param name="massstab">Wonach verglichen wird (Wichtigkeit der Kriterien), oder <c>null</c> (Standard).</param>
    /// <returns>Der beste Plan, seine Güte und ob es der neue ist.</returns>
    public static (IReadOnlyList<Spiel> Plan, Planguete Guete, bool Neu) Uebernehmen(Plansitzung sitzung, IReadOnlyList<Spiel> neu, Massstab? massstab = null)
    {
        ArgumentNullException.ThrowIfNull(sitzung);
        ArgumentNullException.ThrowIfNull(neu);
        Planguete neuGuete = Planguete.Bewerten(sitzung.Bewerten(neu));
        if (BesterPlan(sitzung) is { } bisher && !(massstab is null ? neuGuete.IstBesserAls(bisher.Guete) : massstab.IstBesser(neuGuete, bisher.Guete)))
        {
            return (bisher.Plan, bisher.Guete, false);
        }

        sitzung.PlanMerken(BesterPlanName, neu);
        return (neu, neuGuete, true);
    }

    /// <summary>Der beste Plan des normalen Modus dieser Staffel, sofern es ihn gibt.</summary>
    /// <param name="sitzung">Die Staffel.</param>
    /// <returns>Plan, Güte und Zeit der Speicherung, oder <c>null</c>.</returns>
    public static (IReadOnlyList<Spiel> Plan, Planguete Guete, DateTime Zeit)? BesterPlan(Plansitzung sitzung)
    {
        ArgumentNullException.ThrowIfNull(sitzung);
        return sitzung.GemerktePlaene().FirstOrDefault(e => e.Name == BesterPlanName) is GemerkterPlanEintrag eintrag ? Laden(sitzung, eintrag) : null;
    }

    /// <summary>
    /// Der Plan zum Vergleich während der Planung: der beste des normalen Modus, sonst der zuletzt gespeicherte (gemerkt oder
    /// gesichert); <c>null</c>, wenn es keinen gibt oder er sich nicht lesen lässt.
    /// </summary>
    /// <param name="sitzung">Die Staffel.</param>
    /// <returns>Plan, Güte und Zeit der Speicherung, oder <c>null</c>.</returns>
    public static (IReadOnlyList<Spiel> Plan, Planguete Guete, DateTime Zeit)? Vergleichsplan(Plansitzung sitzung)
    {
        ArgumentNullException.ThrowIfNull(sitzung);
        if (BesterPlan(sitzung) is { } bester)
        {
            return bester;
        }

        GemerkterPlanEintrag? juengster = sitzung.GemerktePlaene().Append(sitzung.GesicherterPlan()).OfType<GemerkterPlanEintrag>().MaxBy(e => e.Geaendert);
        return juengster is null ? null : Laden(sitzung, juengster);
    }

    /// <summary>Beendet die Planung; das Ergebnis kommt über <see cref="Lauf"/>.</summary>
    public void Beenden() => beenden();

    /// <inheritdoc/>
    public void Dispose() => beenden();

    private static (IReadOnlyList<Spiel> Plan, Planguete Guete, DateTime Zeit)? Laden(Plansitzung sitzung, GemerkterPlanEintrag eintrag)
    {
        try
        {
            IReadOnlyList<Spiel> plan = sitzung.GemerktenPlanLaden(eintrag);
            return (plan, Planguete.Bewerten(sitzung.Bewerten(plan)), eintrag.Geaendert);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException or Persistence.Plandaten.PlanDatenFormatException)
        {
            return null;
        }
    }
}
