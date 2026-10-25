// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Schranke;
using AndiGenerator.Engine.Turniere;
using AndiGenerator.Import;

namespace AndiGenerator.Application;

/// <summary>
/// Untere Schranke für den Automodus (MIGRATIONSPLAN O7, nicht im Original): wie viele Verstöße jeder Plan mindestens hat – je
/// Stufe A und B zusammengezählt, dazu je Kriterium allein. Rechnet mit HiGHS einige Sekunden.
/// </summary>
public static class Stufenschranke
{
    /// <summary>Zeitgrenze je Stufe (siehe <see cref="Ligaschranke.StandardZeitJeStufe"/>).</summary>
    public static TimeSpan ZeitJeStufe => Ligaschranke.StandardZeitJeStufe;

    /// <summary>
    /// Zeitgrenze nach Fortschritt für die Schritte von A und B: mindestens <see cref="ZeitJeStufe"/>, danach weiter, solange HiGHS
    /// in den letzten 15 s vorangekommen ist (besserer Plan oder höhere Untergrenze), höchstens 3 Minuten.
    /// </summary>
    public static Laufzeitregel Laufzeit { get; } = new(Ligaschranke.StandardZeitJeStufe, TimeSpan.FromMinutes(3), TimeSpan.FromSeconds(15));

    /// <summary>
    /// So viele HiGHS-Linien rechnen im Automodus neben Untergrenze und Teilneuplanung (lange Läufe für A und B ab dem Start, je
    /// zwei Teilneuplanungen verfeinern sie): zwei ab zwölf logischen Prozessoren, eine ab acht, darunter keine – dort blieben
    /// sonst kaum Kerne für die Inseln.
    /// </summary>
    public static int Linien => Environment.ProcessorCount switch
    {
        >= 12 => 2,
        >= 8 => 1,
        _ => 0,
    };

    /// <summary>Zeit je B-Variante zum Nachbessern (die Varianten rechnen gleichzeitig).</summary>
    public static TimeSpan VariantenNachbessern { get; } = TimeSpan.FromSeconds(30);

    /// <summary>Holt einen Wert, der angibt, ob die Schranke auf diesem Rechner berechnet werden kann (HiGHS vorhanden).</summary>
    public static bool Verfuegbar => Highsbruecke.Verfuegbar;

    /// <summary>
    /// Die Kriterien mit einer Anzahl, die das Modell von HiGHS für diese Staffel nicht kennt (sie fehlen in Grenzen und Varianten,
    /// der Automodus schiebt sie nicht nach A oder B); <c>null</c>, wenn HiGHS fehlt oder es kein Modell gibt.
    /// </summary>
    /// <param name="staffel">Die Staffel.</param>
    /// <param name="optionen">Die Optionen.</param>
    /// <returns>Die unbekannten Kriterien, oder <c>null</c>.</returns>
    public static IReadOnlyList<Kostenkriterium>? Unbekannt(Staffel staffel, Berechnungsoptionen optionen) =>
        Verfuegbar ? Ligaschranke.Unbekannt(staffel, optionen) : null;

    /// <summary>
    /// Der Löser eines Laufs einer HiGHS-Linie: eigener Startwert und Aufwand der Heuristiken (0,05 / 0,2 / 0,5 im Wechsel), ohne
    /// Neustart der Suche (darin reagierte HiGHS in langen Läufen minutenlang nicht auf Zeitgrenze und Abbruch).
    /// </summary>
    /// <param name="lauf">Die Nummer des Laufs.</param>
    /// <returns>Der Löser.</returns>
    public static ISchrankenloeser Linienloeser(int lauf) =>
        new Highsbruecke(new Highsvorgabe(lauf + 1, (lauf % 3) switch { 0 => 0.05, 1 => 0.2, _ => 0.5 }, OhneNeustart: true));

    /// <summary>Berechnet die Schranke.</summary>
    /// <param name="staffel">Die Staffel.</param>
    /// <param name="optionen">Die Optionen.</param>
    /// <param name="einteilung">Die Einteilung der Kriterien.</param>
    /// <param name="fortschritt">Meldet, woran HiGHS gerade rechnet, oder <c>null</c>.</param>
    /// <param name="aufhoeren">Bricht die Rechnung ab (auch mitten in einem Lauf von HiGHS), oder <c>null</c>.</param>
    /// <param name="startplan">Liefert den besten Plan der Generierung als Startlösung, oder <c>null</c>.</param>
    /// <returns>
    /// Der Bericht: die Schranke je Kriterium der Stufe A (<c>null</c> = Spiele ohne Termin) in der Rangfolge; ohne Stufen,
    /// wenn sie sich nicht berechnen lässt – der Grund steht dann in den Hinweisen.
    /// </returns>
    public static Schrankenbericht Berechnen(Staffel staffel, Berechnungsoptionen optionen, Stufeneinteilung einteilung, Action<Schrankenfortschritt>? fortschritt = null, Func<bool>? aufhoeren = null, Func<IReadOnlyList<Spiel>?>? startplan = null)
    {
        if (!Verfuegbar)
        {
            return new Schrankenbericht(0, 0, [], ["Der Löser HiGHS ist auf diesem Rechner nicht verfügbar."], []);
        }

        Schrankenbericht bericht = Ligaschranke.Berechnen(staffel, optionen, new Highsbruecke(), ZeitJeStufe, einteilung, fortschritt, aufhoeren: aufhoeren, startplan: startplan, laufzeit: Laufzeit);
        if (bericht.Alternativen is not { Count: > 0 })
        {
            return bericht;
        }

        fortschritt?.Invoke(new Schrankenfortschritt("B-Varianten nachbessern", 3, 4, bericht.Plan));
        return Ligaschranke.VariantenNachbessern(staffel, optionen, new Highsbruecke(), einteilung, bericht, VariantenNachbessern, aufhoeren);
    }
}
