// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;

namespace AndiGenerator.Engine.Turniere;

/// <summary>
/// Verfolgt einen Schritt der gezielten Konstruktion (nicht im Original; Peter, 26.10.2026): bester Wert und untere Schranke
/// aller Läufe von HiGHS, der beste Plan bisher, wann zuletzt etwas vorankam, und daraus eine Zeile, mit der man den Stand
/// bewerten kann – was gefunden ist und was nach dem Beweis mindestens nötig ist, statt einer Prozentzahl.
/// </summary>
/// <param name="name">Name des Schritts, z. B. „Kürzester Plan“.</param>
/// <param name="nurKilometer">Das Ziel sind allein die Kilometer (dann gibt die Schranke die kürzestmöglichen Kilometer an).</param>
/// <param name="ohneLuecke">
/// Keine Schranke nennen (weitere Pläne mit anderen Spieltagen: Ihre Schranke ist schwach und sagt nichts über den besten Plan).
/// </param>
/// <param name="kmJePunkt">Kilometer je Punkt des Modells.</param>
/// <param name="stillstand">So lange ohne Fortschritt gilt der Schritt als stehend.</param>
internal sealed class Schrittverlauf(string name, bool nurKilometer, bool ohneLuecke, double kmJePunkt, TimeSpan stillstand)
{
    /// <summary>
    /// Als Fortschritt zählt ein besserer Plan oder eine Lücke, die seit dem letzten Fortschritt um mindestens 2 % kleiner
    /// geworden ist (die Schranke von HiGHS steigt oft in vielen kleinen Schritten).
    /// </summary>
    private const double Deutlich = 0.98;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private readonly Lock sperre = new();
    private double wert = double.PositiveInfinity;
    private double schranke = double.NegativeInfinity;
    private double planWert = double.PositiveInfinity;
    private double planKm = double.NaN;
    private TimeSpan jetzt;
    private TimeSpan zuletzt;
    private double lueckeZuletzt = double.PositiveInfinity;

    /// <summary>Holt einen Wert, der angibt, ob der Schritt seit <c>stillstand</c> nicht mehr vorankam.</summary>
    public bool Steht
    {
        get
        {
            lock (sperre)
            {
                return double.IsFinite(wert) && jetzt - zuletzt >= stillstand;
            }
        }
    }

    /// <summary>Setzt die Rechenzeit des Schritts (für „zuletzt vorangekommen“).</summary>
    /// <param name="verstrichen">Rechenzeit seit Beginn des Schritts.</param>
    public void Zeit(TimeSpan verstrichen)
    {
        lock (sperre)
        {
            jetzt = verstrichen;
        }
    }

    /// <summary>Nimmt den gemeinsamen Stand der Läufe auf.</summary>
    /// <param name="stand">Bester Wert und höchste Schranke.</param>
    public void Aufnehmen(Loeserstand stand)
    {
        ArgumentNullException.ThrowIfNull(stand);
        lock (sperre)
        {
            bool besser = stand.Wert < wert - 1e-6;
            wert = Math.Min(wert, stand.Wert);
            schranke = double.IsFinite(stand.Schranke) ? Math.Max(schranke, stand.Schranke) : schranke;
            double luecke = Luecke();
            if (besser || luecke <= Deutlich * lueckeZuletzt)
            {
                zuletzt = jetzt;
                lueckeZuletzt = luecke;
            }
        }
    }

    /// <summary>Nimmt einen Plan auf, den der Löser während der Suche gefunden hat.</summary>
    /// <param name="plan">Der Plan.</param>
    /// <param name="modellwert">Sein Wert im Modell.</param>
    public void Plan(Kompaktloesung plan, double modellwert)
    {
        ArgumentNullException.ThrowIfNull(plan);
        lock (sperre)
        {
            if (modellwert <= planWert + 1e-9)
            {
                planWert = modellwert;
                planKm = plan.Km;
            }
        }

        Aufnehmen(new Loeserstand(modellwert, double.NegativeInfinity));
    }

    /// <summary>
    /// Die Zeile zum Stand, z. B. „Kürzester Plan: bisher 2.452 km · möglich ab ≈ 2.391 km · zuletzt vorangekommen vor 12 s“.
    /// </summary>
    /// <param name="rest">Restzeit, wenn der Schritt über seine Zeit hinaus rechnet, sonst <c>null</c>.</param>
    /// <returns>Die Zeile.</returns>
    public string Zeile(TimeSpan? rest)
    {
        lock (sperre)
        {
            if (!double.IsFinite(wert))
            {
                return name + ": noch kein Plan gefunden";
            }

            string zeile = name + ": " + Stand();
            double seit = Math.Max(0, (jetzt - zuletzt).TotalSeconds);
            zeile += string.Create(Deutsch, $" · zuletzt vorangekommen vor {seit:N0} s");
            if (rest is TimeSpan r)
            {
                zeile += string.Create(Deutsch, $" · rechnet weiter, solange es vorangeht (höchstens noch {(int)Math.Max(0, r.TotalMinutes)}:{Math.Max(0, r.Seconds):00} min)");
            }

            return zeile;
        }
    }

    /// <summary>Die Bewertung des Ergebnisses für den Hinweis am Ende.</summary>
    /// <param name="plan">Der Plan des Schritts.</param>
    /// <param name="optimal">Er ist bewiesen der beste.</param>
    /// <param name="luecke">Lücke zur Schranke in Punkten.</param>
    /// <returns>Z. B. „bestmöglich (bewiesen)“ oder „nicht bewiesen bestmöglich – mindestens ≈ 2.288 km nötig“.</returns>
    public string Ergebnis(Kompaktloesung plan, bool optimal, double luecke)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (optimal)
        {
            return "bestmöglich (bewiesen)";
        }

        return nurKilometer && !ohneLuecke && double.IsFinite(luecke)
            ? string.Create(Deutsch, $"nicht bewiesen bestmöglich – mindestens ≈ {Minimum(plan.Km, luecke):N0} km sind nötig")
            : "nicht bewiesen bestmöglich (Zeit)";
    }

    private double Minimum(double km, double luecke) => Math.Max(0, km - (luecke * kmJePunkt));

    private double Luecke() => double.IsFinite(schranke) ? Math.Max(0, wert - schranke) : double.PositiveInfinity;

    /// <summary>Was gefunden ist und was die Schranke sagt (unter der Sperre).</summary>
    private string Stand()
    {
        double km = double.IsNaN(planKm) ? wert * kmJePunkt : planKm;
        string gefunden = string.Create(Deutsch, $"bisher {km:N0} km");
        double luecke = double.IsFinite(schranke) ? Math.Max(0, Math.Min(wert, planWert) - schranke) : double.PositiveInfinity;
        if (ohneLuecke)
        {
            return gefunden;
        }

        if (luecke <= 1e-6 * Math.Max(1, Math.Abs(wert)))
        {
            return gefunden + " · bestmöglich (bewiesen)";
        }

        if (!nurKilometer)
        {
            return gefunden + " · noch nicht bewiesen, dass es kein besseres gibt";
        }

        return double.IsFinite(luecke)
            ? gefunden + string.Create(Deutsch, $" · mindestens ≈ {Minimum(km, luecke):N0} km sind nötig")
            : gefunden + " · Untergrenze noch nicht bekannt";
    }
}
