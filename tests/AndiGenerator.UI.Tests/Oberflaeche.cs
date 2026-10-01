// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Logging;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Xunit.Abstractions;

namespace AndiGenerator.UI.Tests;

/// <summary>
/// Eine Avalonia-Sitzung ohne Bildschirm für alle Oberflächentests. Avalonia erlaubt nur einen UI-Thread je Prozess,
/// deshalb teilen sich die Tests die Sitzung und laufen nacheinander (Sammlung „Oberfläche“).
/// </summary>
public sealed class Oberflaeche : IDisposable
{
    private readonly HeadlessUnitTestSession sitzung = HeadlessUnitTestSession.StartNew(typeof(TestAnwendung));

    /// <summary>Initialisiert die Sitzung und das Protokoll.</summary>
    public Oberflaeche()
    {
        Logger.Sink = Protokoll;
    }

    /// <summary>Holt das Protokoll der Avalonia-Meldungen.</summary>
    internal Protokoll Protokoll { get; } = new();

    /// <summary>Arbeitet die anstehenden Aufträge des UI-Threads ab und zeichnet das Fenster einmal vollständig.</summary>
    /// <param name="fenster">Das Fenster.</param>
    public static void Zeichnen(Window fenster)
    {
        Dispatcher.UIThread.RunJobs();
        using WriteableBitmap? bild = fenster.CaptureRenderedFrame();
        Assert.NotNull(bild);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Führt einen Testablauf auf dem UI-Thread aus.</summary>
    /// <param name="ablauf">Der Ablauf.</param>
    /// <returns>Der laufende Ablauf.</returns>
    public Task Ausfuehren(Func<Task> ablauf) =>
        sitzung.Dispatch(
            async () =>
            {
                await ablauf();
                return true;
            },
            CancellationToken.None);

    /// <summary>
    /// Gibt die Warnungen von Avalonia (z. B. Bindungsfehler) aus und lässt den Test bei Fehlern scheitern. Danach ist
    /// das Protokoll leer.
    /// </summary>
    /// <param name="ausgabe">Ausgabe des Tests.</param>
    public void KeineFehler(ITestOutputHelper ausgabe)
    {
        var eintraege = Protokoll.Eintraege.ToList();
        Protokoll.Leeren();
        foreach ((LogEventLevel stufe, string bereich, string text) in eintraege)
        {
            ausgabe.WriteLine($"{stufe} [{bereich}] {text}");
        }

        Assert.DoesNotContain(eintraege, e => e.Stufe >= LogEventLevel.Error);
    }

    /// <inheritdoc/>
    public void Dispose() => sitzung.Dispose();
}
