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

    /// <summary>Mehr Aufwand für Heuristiken, damit früh gute Lösungen da sind (Standard 0,05).</summary>
    private const double Heuristik = 0.2;

    /// <summary>Rückruf während der ganzzahligen Suche mit der Frage nach Abbruch (kCallbackMipInterrupt).</summary>
    private const int MipUnterbrechung = 6;

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
    /// <returns>
    /// Die Werte der Spalten (0 oder 1) der besten gefundenen Lösung, oder <c>null</c>, wenn keine gefunden wurde; und ob sie
    /// beweisbar die beste ist.
    /// </returns>
    public static (double[]? Werte, bool Optimal) Loesen(Ganzzahlproblem problem, double[]? start, int startwert, Func<bool> aufhoeren)
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
            if (Ausfuehren(h) < 0)
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

    [DllImport(Bibliothek, EntryPoint = "Highs_setIntOptionValue", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SetzeGanzzahl(IntPtr highs, [MarshalAs(UnmanagedType.LPUTF8Str)] string option, int wert);

    [DllImport(Bibliothek, EntryPoint = "Highs_setSolution", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SetzeLoesung(IntPtr highs, double[] spalten, IntPtr zeilen, IntPtr spaltenDual, IntPtr zeilenDual);

    [DllImport(Bibliothek, EntryPoint = "Highs_getInfinity", CallingConvention = CallingConvention.Cdecl)]
    private static extern double Unendlich(IntPtr highs);

    [DllImport(Bibliothek, EntryPoint = "Highs_setCallback", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SetzeRueckruf(IntPtr highs, IntPtr rueckruf, IntPtr nutzerdaten);

    [DllImport(Bibliothek, EntryPoint = "Highs_startCallback", CallingConvention = CallingConvention.Cdecl)]
    private static extern int StarteRueckruf(IntPtr highs, int art);
}
