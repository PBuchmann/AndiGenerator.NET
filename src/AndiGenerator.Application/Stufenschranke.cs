// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Domain.Stammdaten;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Schranke;
using AndiGenerator.Engine.Turniere;

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
    public static Schrankenbericht Berechnen(Staffel staffel, Berechnungsoptionen optionen, Stufeneinteilung einteilung, Action<Schrankenfortschritt>? fortschritt = null, Func<bool>? aufhoeren = null, Func<IReadOnlyList<Spiel>?>? startplan = null) =>
        Verfuegbar
            ? Ligaschranke.Berechnen(staffel, optionen, new Highsbruecke(), ZeitJeStufe, einteilung, fortschritt, aufhoeren: aufhoeren, startplan: startplan, laufzeit: Laufzeit)
            : new Schrankenbericht(0, 0, [], ["Der Löser HiGHS ist auf diesem Rechner nicht verfügbar."], []);
}
