// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Application;
using AndiGenerator.Presentation;
using Avalonia.Threading;

namespace AndiGenerator.UI;

/// <summary>
/// Fängt unerwartete Fehler ab, damit sie das Programm nicht still beenden: Jeder Fehler kommt ins Fehlerprotokoll im
/// eigenen Datenordner. Bei einem Fehler auf der Oberfläche wird zusätzlich der aktuelle Plan als „Notfallsicherung“
/// gemerkt und einmal je Sitzung eine Meldung gezeigt; das Programm läuft weiter. Bei einem Fehler, der das Programm
/// ohnehin beendet (außerhalb der Oberfläche), wird der Plan noch gesichert und protokolliert.
/// </summary>
internal static class Fehlerbehandlung
{
    private static HauptfensterViewModel? modell;
    private static IOberflaeche? oberflaeche;
    private static int gemeldet;

    /// <summary>Meldet die Fehlerbehandlung für die ganze Anwendung an.</summary>
    /// <param name="hauptfenster">Das Hauptfenster (für die Notfallsicherung des Plans).</param>
    /// <param name="dienste">Dienste der Oberfläche (für die Meldung).</param>
    public static void Anmelden(HauptfensterViewModel hauptfenster, IOberflaeche dienste)
    {
        modell = hauptfenster;
        oberflaeche = dienste;
        Dispatcher.UIThread.UnhandledException += AufDerOberflaeche;
        AppDomain.CurrentDomain.UnhandledException += Unbehandelt;
        TaskScheduler.UnobservedTaskException += Unbeobachtet;
    }

    private static async void AufDerOberflaeche(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        string text = Festhalten(e.Exception, "Oberfläche", sichern: true);
        if (oberflaeche is not null && Interlocked.Exchange(ref gemeldet, 1) == 0)
        {
            await oberflaeche.MeldenAsync("Unerwarteter Fehler", text);
        }
    }

    private static void Unbehandelt(object? sender, UnhandledExceptionEventArgs e) =>
        Festhalten(e.ExceptionObject as Exception ?? new InvalidOperationException(e.ExceptionObject?.ToString()), "Programm", sichern: true);

    private static void Unbeobachtet(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Festhalten(e.Exception, "Hintergrundaufgabe", sichern: false);
        e.SetObserved();
    }

    /// <summary>Protokolliert den Fehler, sichert auf Wunsch den Plan und liefert die Meldung für den Anwender.</summary>
    private static string Festhalten(Exception fehler, string wobei, bool sichern)
    {
        string? protokoll;
        try
        {
            protokoll = Fehlerprotokoll.Schreiben(Plansitzung.EigeneBasis(), fehler, wobei);
        }
        catch (Exception schreibfehler) when (schreibfehler is IOException or UnauthorizedAccessException)
        {
            protokoll = null;
        }

        string? gesichert = sichern ? modell?.NotfallSichern() : null;
        return Fehlerprotokoll.Meldung(fehler, gesichert, protokoll);
    }
}
