// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using AndiGenerator.Application;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Einfach;
using AndiGenerator.Engine.Inseln;
using CommunityToolkit.Mvvm.Input;

namespace AndiGenerator.Presentation;

/// <summary>Normaler Modus: Wichtigkeit je Kriterium und Terminplan/Diagramme schon während der Planung.</summary>
public sealed partial class NormalmodusViewModel
{
    /// <summary>Holt den Befehl, der die Wichtigkeit eines Kriteriums setzt (Parameter „Kriterium:Stufe“, siehe <see cref="Normalbalken"/>).</summary>
    public IRelayCommand<string> WichtigkeitCommand { get; }

    /// <summary>Holt einen Wert, der angibt, ob ein Reiter mit Plan gewählt ist, es aber noch keinen Plan gibt.</summary>
    public bool PlanungOhneAnsicht => Reiter != 0 && Terminplan is null;

    /// <summary>Holt die eingestellte Wichtigkeit eines Kriteriums.</summary>
    /// <param name="kriterium">Das Kriterium.</param>
    /// <returns>Die Stufe.</returns>
    public Kriterienwichtigkeit WichtigkeitVon(Kostenkriterium kriterium)
    {
        lock (sperre)
        {
            return wichtigkeiten.GetValueOrDefault(kriterium, Kriterienwichtigkeit.Normal);
        }
    }

    private void WichtigkeitSetzen(string? parameter)
    {
        string[] teile = (parameter ?? string.Empty).Split(':');
        if (teile.Length != 2
            || !int.TryParse(teile[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int k)
            || !int.TryParse(teile[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int w)
            || !Enum.IsDefined((Kostenkriterium)k)
            || !Enum.IsDefined((Kriterienwichtigkeit)w))
        {
            return;
        }

        var kriterium = (Kostenkriterium)k;
        var stufe = (Kriterienwichtigkeit)w;
        lock (sperre)
        {
            if (stufe == Kriterienwichtigkeit.Normal)
            {
                _ = wichtigkeiten.Remove(kriterium);
            }
            else
            {
                wichtigkeiten[kriterium] = stufe;
            }

            steuerung.Wichtigkeit(kriterium, Kriterienwichtigkeiten.Faktor(stufe));
            letzteGuete = null;
        }

        // Die Anzeige sofort nachziehen (auch während der Pause), ohne auf den nächsten Takt zu warten.
        if (Laeuft && hauptfenster.Sitzung is Plansitzung s)
        {
            _ = Task.Run(() => Takt(s));
        }
    }

    /// <summary>Zeigt Terminplan oder Diagramme des besten bisherigen Plans, wenn dafür ein Reiter gewählt ist.</summary>
    private void PlanungsansichtAnfordern()
    {
        if (!Laeuft || Reiter == 0 || hauptfenster.Sitzung is not Plansitzung s)
        {
            return;
        }

        (IReadOnlyList<Spiel> Plan, Planguete Guete)? gesamt;
        int version;
        Massstab massstab;
        lock (sperre)
        {
            gesamt = gesamtBester;
            version = gezaehlteVerbesserungen;
            massstab = steuerung.Massstab;
        }

        if (gesamt is { } g)
        {
            _ = PlanungsansichtAktualisierenAsync(s, g.Plan, version, massstab);
        }
    }

    /// <summary>Speichert den besten Plan (nur wenn besser als der gespeicherte) und lädt die Ansichten neu.</summary>
    private async Task PlanungsansichtAktualisierenAsync(Plansitzung s, IReadOnlyList<Spiel> plan, int version, Massstab massstab)
    {
        if (ansichtBeschaeftigt)
        {
            return;
        }

        ansichtBeschaeftigt = true;
        try
        {
            bool gespeichert = await Task.Run(() =>
            {
                try
                {
                    _ = Normalplanung.Uebernehmen(s, plan, massstab);
                    return true;
                }
                catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
                {
                    return false;
                }
            }).ConfigureAwait(true);
            if (gespeichert && Laeuft && Reiter != 0)
            {
                ansichtVersion = version;
                GemerkterPlanEintragSuchen(s);
            }
        }
        finally
        {
            ansichtBeschaeftigt = false;
        }
    }
}
