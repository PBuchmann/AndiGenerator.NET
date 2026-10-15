// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AndiGenerator.Import;

/// <summary>
/// Misst, wie viel Rechenzeit (CPU) die Fäden der Suche und HiGHS verbrauchen (nicht im Original; für den Leitstand). Die
/// Fäden melden sich an; gemessen wird mit der Rechenzeit des Betriebssystems je Faden. Auf anderen Systemen als Windows und
/// Linux gibt es keine Messung.
/// </summary>
public static class Rechenfaeden
{
    private const int ThreadCpuZeit = 3;

    /// <summary>Fäden der Suche: Betriebssystem-Nummer → zuletzt gemessene Rechenzeit (Ticks).</summary>
    private static readonly ConcurrentDictionary<int, long> Suche = new();

    /// <summary>Fäden, die gerade HiGHS rechnen: Nummer → Rechenzeit des Fadens beim Beginn (Ticks).</summary>
    private static readonly ConcurrentDictionary<int, long> Highs = new();

    /// <summary>Rechenzeit beendeter HiGHS-Läufe (Ticks).</summary>
    private static long highsFertig;

    /// <summary>Holt einen Wert, der angibt, ob auf diesem System gemessen werden kann.</summary>
    public static bool Messbar => OperatingSystem.IsWindows() || OperatingSystem.IsLinux();

    /// <summary>Ein Faden der Suche beginnt (auf dem Faden selbst aufrufen).</summary>
    public static void SucheBeginnt()
    {
        if (Nummer() is int nummer)
        {
            Suche[nummer] = EigeneZeit();
        }
    }

    /// <summary>HiGHS rechnet auf diesem Faden, bis das Ergebnis freigegeben wird.</summary>
    /// <returns>Beendet die Messung des Laufs.</returns>
    public static IDisposable HighsRechnet()
    {
        if (Nummer() is not int nummer)
        {
            return new Lauf(null);
        }

        Highs[nummer] = EigeneZeit();
        return new Lauf(nummer);
    }

    /// <summary>Misst die Rechenzeit seit dem Start des Programms.</summary>
    /// <returns>Suche, HiGHS und das ganze Programm; <c>null</c>, wenn nicht gemessen werden kann.</returns>
    public static (TimeSpan Suche, TimeSpan Highs, TimeSpan Gesamt)? Messen()
    {
        if (!Messbar)
        {
            return null;
        }

        try
        {
            using Process prozess = Process.GetCurrentProcess();
            var faeden = new Dictionary<int, long>();
            foreach (ProcessThread faden in prozess.Threads)
            {
                using (faden)
                {
                    try
                    {
                        faeden[faden.Id] = faden.TotalProcessorTime.Ticks;
                    }
                    catch (Exception fehler) when (fehler is InvalidOperationException or System.ComponentModel.Win32Exception)
                    {
                        // Faden inzwischen beendet.
                    }
                }
            }

            long suche = 0;
            foreach ((int nummer, long zuletzt) in Suche)
            {
                long jetzt = faeden.GetValueOrDefault(nummer, zuletzt);
                Suche[nummer] = jetzt;
                suche += jetzt;
            }

            long highs = Interlocked.Read(ref highsFertig);
            foreach ((int nummer, long beginn) in Highs)
            {
                highs += Math.Max(0, faeden.GetValueOrDefault(nummer, beginn) - beginn);
            }

            return (TimeSpan.FromTicks(suche), TimeSpan.FromTicks(highs), prozess.TotalProcessorTime);
        }
        catch (Exception fehler) when (fehler is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>Vergisst die Fäden der Suche (neue Generierung).</summary>
    public static void SucheVergessen() => Suche.Clear();

    private static int? Nummer()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return (int)GetCurrentThreadId();
            }

            return OperatingSystem.IsLinux() ? LinuxFadennummer() : null;
        }
        catch (Exception fehler) when (fehler is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    /// <summary>Rechenzeit des aufrufenden Fadens (Ticks).</summary>
    private static long EigeneZeit()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return GetThreadTimes(GetCurrentThread(), out _, out _, out long kern, out long nutzer) ? kern + nutzer : 0;
            }

            return OperatingSystem.IsLinux() && LinuxUhr(ThreadCpuZeit, out Zeitpunkt z) == 0 ? (z.Sekunden * TimeSpan.TicksPerSecond) + (z.Nanosekunden / 100) : 0;
        }
        catch (Exception fehler) when (fehler is DllNotFoundException or EntryPointNotFoundException)
        {
            return 0;
        }
    }

    [DllImport("kernel32", EntryPoint = "GetCurrentThreadId")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32", EntryPoint = "GetCurrentThread")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32", EntryPoint = "GetThreadTimes")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetThreadTimes(IntPtr faden, out long erzeugt, out long beendet, out long kern, out long nutzer);

    [DllImport("libc", EntryPoint = "gettid")]
    private static extern int LinuxFadennummer();

    [DllImport("libc", EntryPoint = "clock_gettime")]
    private static extern int LinuxUhr(int uhr, out Zeitpunkt zeit);

    [StructLayout(LayoutKind.Sequential)]
    private struct Zeitpunkt
    {
        public long Sekunden;
        public long Nanosekunden;
    }

    /// <summary>Ein Lauf von HiGHS: Beim Freigeben kommt seine Rechenzeit zu den beendeten Läufen.</summary>
    private sealed class Lauf(int? nummer) : IDisposable
    {
        public void Dispose()
        {
            if (nummer is int n && Highs.TryRemove(n, out long beginn))
            {
                Interlocked.Add(ref highsFertig, Math.Max(0, EigeneZeit() - beginn));
            }
        }
    }
}
