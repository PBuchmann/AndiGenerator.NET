// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;

namespace AndiGenerator.Import;

/// <summary>
/// Löst ein ganzzahliges lineares Programm (<see cref="Ganzzahlproblem"/>) mit HiGHS (nicht im Original; https://highs.dev,
/// MIT-Lizenz, Paket Highs.Native) über dessen C-Schnittstelle. Der Löser fragt während der Suche regelmäßig nach, ob er
/// aufhören soll; dabei kann die Planung auch anhalten (Pause).
/// </summary>
public static class HighsLoeser
{
    private const string Bibliothek = "highs";

    /// <summary>Zeilenweise Matrix (kHighsMatrixFormatRowwise).</summary>
    private const int Zeilenweise = 2;

    /// <summary>Minimieren (kHighsObjSenseMinimize).</summary>
    private const int Minimieren = 1;

    /// <summary>Ganzzahlige Spalte (kHighsVarTypeInteger).</summary>
    private const int Ganzzahlig = 1;

    /// <summary>Status „beweisbar beste Lösung“ (kHighsModelStatusOptimal).</summary>
    private const int Optimal = 7;

    /// <summary>Status „keine Lösung“ (kHighsModelStatusInfeasible).</summary>
    private const int Unzulaessig = 8;

    /// <summary>Lösung vorhanden (kHighsSolutionStatusFeasible).</summary>
    private const int ZulaessigeLoesung = 2;

    /// <summary>Mehr Aufwand für Heuristiken, damit früh gute Lösungen da sind (Standard 0,05).</summary>
    private const double Heuristik = 0.2;

    /// <summary>Rückruf während der ganzzahligen Suche mit der Frage nach Abbruch (kCallbackMipInterrupt).</summary>
    private const int MipUnterbrechung = 6;

    /// <summary>Ab so vielen Spalten ohne Vorverarbeitung (die üblichen Modelle haben 2.000 bis 4.000).</summary>
    private const int GrossesModell = 10_000;

    private static int verfuegbar;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void Rueckruf(int art, IntPtr meldung, IntPtr datenAus, IntPtr datenEin, IntPtr nutzerdaten);

    /// <summary>Holt einen Wert, der angibt, ob HiGHS geladen werden kann.</summary>
    public static bool Verfuegbar
    {
        get
        {
            if (verfuegbar == 0)
            {
                bool geht;
                try
                {
                    IntPtr h = Erzeugen();
                    geht = h != IntPtr.Zero;
                    if (geht)
                    {
                        Zerstoeren(h);
                    }
                }
                catch (DllNotFoundException)
                {
                    geht = false;
                }
                catch (EntryPointNotFoundException)
                {
                    geht = false;
                }
                catch (BadImageFormatException)
                {
                    geht = false;
                }

                verfuegbar = geht ? 1 : -1;
            }

            return verfuegbar > 0;
        }
    }

    /// <summary>Löst das Problem.</summary>
    /// <param name="problem">Das Problem.</param>
    /// <param name="start">Eine bekannte Lösung als Startpunkt (Werte der Spalten), oder <c>null</c>.</param>
    /// <param name="startwert">Startwert der Zufallszahlen von HiGHS.</param>
    /// <param name="aufhoeren">
    /// Wird während der Suche (im Thread des Lösers) gefragt; <c>true</c> beendet sie mit der bis dahin besten Lösung.
    /// Darf blockieren (Pause).
    /// </param>
    /// <param name="zeitgrenze">Harte Zeitgrenze für HiGHS (auch für Phasen ohne Rückruf), oder <c>null</c>.</param>
    /// <returns>
    /// Die Werte der Spalten (0 oder 1) der besten gefundenen Lösung, oder <c>null</c>, wenn keine gefunden wurde; und ob sie
    /// beweisbar die beste ist.
    /// </returns>
    public static (double[]? Werte, bool Optimal) Loesen(Ganzzahlproblem problem, double[]? start, int startwert, Func<bool> aufhoeren, TimeSpan? zeitgrenze = null)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(aufhoeren);
        IntPtr h = Erzeugen();
        if (h == IntPtr.Zero)
        {
            return (null, false);
        }

        Rueckruf rueckruf = (_, _, _, datenEin, _) =>
        {
            // Das erste Feld der Eingabedaten ist „user_interrupt“.
            if (datenEin != IntPtr.Zero && aufhoeren())
            {
                Marshal.WriteInt32(datenEin, 1);
            }
        };

        try
        {
            SetzeSchalter(h, "output_flag", 0);
            SetzeZahl(h, "mip_heuristic_effort", Heuristik);

            // Große Modelle (z. B. mit Verstößen, wenn Mannschaften keine Heimtermine haben) ohne Vorverarbeitung: Deren
            // „Probing“ dauerte bei 25.000 Spalten rund 20 Minuten – ohne Rückruf, also ohne Fortschritt und ohne Abbruch.
            // Ohne sie findet HiGHS in Sekunden erste Lösungen (Peter, 14.10.2026).
            if (problem.Kosten.Length > GrossesModell)
            {
                SetzeText(h, "presolve", "off");
            }

            // Harte Zeitgrenze als Rückfallebene für Phasen ohne Rückruf.
            if (zeitgrenze is TimeSpan grenze && grenze > TimeSpan.Zero)
            {
                SetzeZahl(h, "time_limit", grenze.TotalSeconds);
            }

            SetzeGanzzahl(h, "random_seed", Math.Abs(startwert % 2_000_000_000));
            double unendlich = Unendlich(h);
            int n = problem.Kosten.Length;
            int m = problem.ZeileUnten.Length;
            double[] zeileOben = problem.ZeileOben.Select(x => double.IsPositiveInfinity(x) ? unendlich : x).ToArray();
            int status = UebergebeMip(
                h,
                n,
                m,
                problem.Spalten.Length,
                Zeilenweise,
                Minimieren,
                0,
                problem.Kosten,
                new double[n],
                problem.SpalteOben,
                problem.ZeileUnten,
                zeileOben,
                problem.ZeilenAnfang,
                problem.Spalten,
                problem.Werte,
                Enumerable.Repeat(Ganzzahlig, n).ToArray());
            if (status < 0)
            {
                return (null, false);
            }

            if (start is not null && start.Length == n)
            {
                SetzeLoesung(h, start, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            }

            SetzeRueckruf(h, Marshal.GetFunctionPointerForDelegate(rueckruf), IntPtr.Zero);
            StarteRueckruf(h, MipUnterbrechung);
            using IDisposable messung = Rechenfaeden.HighsRechnet();
            if (Ausfuehren(h) < 0)
            {
                return (null, false);
            }

            // Abgebrochen, bevor es eine Lösung gab: keine Werte (sonst lauter Nullen).
            _ = LeseGanzzahlInfo(h, "primal_solution_status", out int loesungsstatus);
            if (loesungsstatus != ZulaessigeLoesung)
            {
                return (null, false);
            }

            var werte = new double[n];
            LeseLoesung(h, werte, new double[n], new double[m], new double[m]);
            return (werte, Modellstatus(h) == Optimal);
        }
        finally
        {
            Zerstoeren(h);
            GC.KeepAlive(rueckruf);
        }
    }

    /// <summary>
    /// Löst das Problem bis zur Zeitgrenze und liefert neben der besten Lösung die beweisbare untere Schranke (für die untere
    /// Schranke der Ligaplanung, MIGRATIONSPLAN O7).
    /// </summary>
    /// <param name="problem">Das Problem.</param>
    /// <param name="zeitgrenze">Zeitgrenze.</param>
    /// <param name="start">Eine bekannte Lösung als Startpunkt, oder <c>null</c>.</param>
    /// <param name="aufhoeren">
    /// Wird während der Suche gefragt; <c>true</c> beendet sie mit der bis dahin besten Lösung (Abbruch, Pause), oder <c>null</c>.
    /// </param>
    /// <param name="fortschritt">
    /// Zeitgrenze nach Fortschritt (<paramref name="zeitgrenze"/> ist dann die Obergrenze), oder <c>null</c>.
    /// </param>
    /// <returns>
    /// Die Werte der besten Lösung (oder <c>null</c>), ihre Kosten, die untere Schranke, ob die Lösung beweisbar die beste ist
    /// und ob das Problem keine Lösung hat.
    /// </returns>
    public static (double[]? Werte, double Wert, double Schranke, bool Optimal, bool Unloesbar) LoesenMitSchranke(Ganzzahlproblem problem, TimeSpan zeitgrenze, double[]? start = null, Func<bool>? aufhoeren = null, Fortschrittsgrenze? fortschritt = null)
    {
        ArgumentNullException.ThrowIfNull(problem);
        IntPtr h = Erzeugen();
        if (h == IntPtr.Zero)
        {
            return (null, double.PositiveInfinity, double.NegativeInfinity, false, false);
        }

        var stand = new Fortschrittsstand(fortschritt);
        Rueckruf rueckruf = (_, _, datenAus, datenEin, _) =>
        {
            if (datenEin != IntPtr.Zero && (aufhoeren?.Invoke() == true || stand.Steht(datenAus)))
            {
                Marshal.WriteInt32(datenEin, 1);
            }
        };

        try
        {
            SetzeSchalter(h, "output_flag", 0);
            SetzeZahl(h, "time_limit", zeitgrenze.TotalSeconds);

            // Hier bleibt die Vorverarbeitung an (anders als bei den Kompaktspieltagen): Ohne sie fand die Teilneuplanung in der
            // Zeitgrenze nichts mehr (Kreisklasse (7): 0 von 10 Ausschnitten), und abbrechen lässt sich jetzt über den Rückruf.
            double unendlich = Unendlich(h);
            int n = problem.Kosten.Length;
            int m = problem.ZeileUnten.Length;
            double[] zeileOben = problem.ZeileOben.Select(x => double.IsPositiveInfinity(x) ? unendlich : x).ToArray();
            double[] zeileUnten = problem.ZeileUnten.Select(x => double.IsNegativeInfinity(x) ? -unendlich : x).ToArray();
            int status = UebergebeMip(
                h,
                n,
                m,
                problem.Spalten.Length,
                Zeilenweise,
                Minimieren,
                0,
                problem.Kosten,
                new double[n],
                problem.SpalteOben,
                zeileUnten,
                zeileOben,
                problem.ZeilenAnfang,
                problem.Spalten,
                problem.Werte,
                Enumerable.Repeat(Ganzzahlig, n).ToArray());
            if (status < 0)
            {
                return (null, double.PositiveInfinity, double.NegativeInfinity, false, false);
            }

            if (start is not null && start.Length == n)
            {
                SetzeLoesung(h, start, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            }

            if (aufhoeren is not null || fortschritt is not null)
            {
                SetzeRueckruf(h, Marshal.GetFunctionPointerForDelegate(rueckruf), IntPtr.Zero);
                StarteRueckruf(h, MipUnterbrechung);
            }

            using IDisposable messung = Rechenfaeden.HighsRechnet();
            if (Ausfuehren(h) < 0)
            {
                return (null, double.PositiveInfinity, double.NegativeInfinity, false, false);
            }

            int modellstatus = Modellstatus(h);
            if (modellstatus == Unzulaessig)
            {
                return (null, double.PositiveInfinity, double.PositiveInfinity, false, true);
            }

            _ = LeseGanzzahlInfo(h, "primal_solution_status", out int loesungsstatus);
            _ = LeseZahlInfo(h, "mip_dual_bound", out double schranke);
            if (loesungsstatus != ZulaessigeLoesung)
            {
                return (null, double.PositiveInfinity, schranke, false, false);
            }

            var werte = new double[n];
            LeseLoesung(h, werte, new double[n], new double[m], new double[m]);
            _ = LeseZahlInfo(h, "objective_function_value", out double wert);
            bool optimal = modellstatus == Optimal;
            return (werte, wert, optimal ? wert : schranke, optimal, false);
        }
        finally
        {
            Zerstoeren(h);
            GC.KeepAlive(rueckruf);
        }
    }

    [DllImport(Bibliothek, EntryPoint = "Highs_create", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr Erzeugen();

    [DllImport(Bibliothek, EntryPoint = "Highs_destroy", CallingConvention = CallingConvention.Cdecl)]
    private static extern void Zerstoeren(IntPtr highs);

    [DllImport(Bibliothek, EntryPoint = "Highs_passMip", CallingConvention = CallingConvention.Cdecl)]
    private static extern int UebergebeMip(
        IntPtr highs,
        int spalten,
        int zeilen,
        int eintraege,
        int format,
        int richtung,
        double versatz,
        double[] kosten,
        double[] spalteUnten,
        double[] spalteOben,
        double[] zeileUnten,
        double[] zeileOben,
        int[] anfang,
        int[] index,
        double[] wert,
        int[] ganzzahlig);

    [DllImport(Bibliothek, EntryPoint = "Highs_run", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Ausfuehren(IntPtr highs);

    [DllImport(Bibliothek, EntryPoint = "Highs_getModelStatus", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Modellstatus(IntPtr highs);

    [DllImport(Bibliothek, EntryPoint = "Highs_getSolution", CallingConvention = CallingConvention.Cdecl)]
    private static extern int LeseLoesung(IntPtr highs, [Out] double[] spalten, [Out] double[] spaltenDual, [Out] double[] zeilen, [Out] double[] zeilenDual);

    [DllImport(Bibliothek, EntryPoint = "Highs_setBoolOptionValue", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SetzeSchalter(IntPtr highs, [MarshalAs(UnmanagedType.LPUTF8Str)] string option, int wert);

    [DllImport(Bibliothek, EntryPoint = "Highs_setDoubleOptionValue", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SetzeZahl(IntPtr highs, [MarshalAs(UnmanagedType.LPUTF8Str)] string option, double wert);

    [DllImport(Bibliothek, EntryPoint = "Highs_setStringOptionValue", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SetzeText(IntPtr highs, [MarshalAs(UnmanagedType.LPUTF8Str)] string option, [MarshalAs(UnmanagedType.LPUTF8Str)] string wert);

    [DllImport(Bibliothek, EntryPoint = "Highs_setIntOptionValue", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SetzeGanzzahl(IntPtr highs, [MarshalAs(UnmanagedType.LPUTF8Str)] string option, int wert);

    [DllImport(Bibliothek, EntryPoint = "Highs_setSolution", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SetzeLoesung(IntPtr highs, double[] spalten, IntPtr zeilen, IntPtr spaltenDual, IntPtr zeilenDual);

    [DllImport(Bibliothek, EntryPoint = "Highs_getDoubleInfoValue", CallingConvention = CallingConvention.Cdecl)]
    private static extern int LeseZahlInfo(IntPtr highs, [MarshalAs(UnmanagedType.LPUTF8Str)] string info, out double wert);

    [DllImport(Bibliothek, EntryPoint = "Highs_getIntInfoValue", CallingConvention = CallingConvention.Cdecl)]
    private static extern int LeseGanzzahlInfo(IntPtr highs, [MarshalAs(UnmanagedType.LPUTF8Str)] string info, out int wert);

    [DllImport(Bibliothek, EntryPoint = "Highs_getInfinity", CallingConvention = CallingConvention.Cdecl)]
    private static extern double Unendlich(IntPtr highs);

    [DllImport(Bibliothek, EntryPoint = "Highs_setCallback", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SetzeRueckruf(IntPtr highs, IntPtr rueckruf, IntPtr nutzerdaten);

    [DllImport(Bibliothek, EntryPoint = "Highs_startCallback", CallingConvention = CallingConvention.Cdecl)]
    private static extern int StarteRueckruf(IntPtr highs, int art);

    /// <summary>
    /// Verfolgt im Rückruf, ob HiGHS noch vorankommt: bester gefundener Wert (primal) und Untergrenze (dual) mit der Laufzeit
    /// ihrer letzten Verbesserung.
    /// </summary>
    private sealed class Fortschrittsstand(Fortschrittsgrenze? grenze)
    {
        private double bester = double.PositiveInfinity;
        private double untergrenze = double.NegativeInfinity;
        private double zuletzt;

        /// <summary>Gibt an, ob HiGHS aufhören soll: nach der Mindestzeit und so lange ohne Fortschritt.</summary>
        public bool Steht(IntPtr datenAus)
        {
            if (grenze is null || Ausgabewert(datenAus, "running_time") is not double zeit)
            {
                return false;
            }

            const double Schritt = 1e-6;
            if (Ausgabewert(datenAus, "mip_primal_bound") is double primal && double.IsFinite(primal) && primal < bester - Schritt)
            {
                bester = primal;
                zuletzt = zeit;
            }

            if (Ausgabewert(datenAus, "mip_dual_bound") is double dual && double.IsFinite(dual) && dual > untergrenze + Schritt)
            {
                untergrenze = dual;
                zuletzt = zeit;
            }

            // Ohne gefundenen Plan sucht HiGHS weiter (bis zur Obergrenze): Der erste Plan ist selbst der Fortschritt.
            return double.IsFinite(bester) && zeit >= grenze.Mindestens && zeit - zuletzt >= grenze.Stillstand;
        }

        /// <summary>Ein Wert (double) aus den Daten des Rückrufs; <c>null</c>, wenn es ihn nicht gibt.</summary>
        private static double? Ausgabewert(IntPtr datenAus, string name)
        {
            IntPtr zeiger = datenAus == IntPtr.Zero ? IntPtr.Zero : LeseRueckrufwert(datenAus, name);
            return zeiger == IntPtr.Zero ? null : BitConverter.Int64BitsToDouble(Marshal.ReadInt64(zeiger));
        }

        [DllImport(Bibliothek, EntryPoint = "Highs_getCallbackDataOutItem", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr LeseRueckrufwert(IntPtr datenAus, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    }
}
