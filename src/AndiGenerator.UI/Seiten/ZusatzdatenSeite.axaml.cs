// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.ComponentModel;
using AndiGenerator.Presentation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AndiGenerator.UI.Seiten;

/// <summary>
/// Seite „Daten aus click-TT“. Nach dem Laden einer Datei rollt sie zur Erfolgsmeldung unter dem Knopf, damit das Ergebnis
/// nicht unbemerkt weiter unten erscheint.
/// </summary>
public partial class ZusatzdatenSeite : UserControl
{
    /// <summary>So weit unter der Meldung soll nach dem Laden noch zu sehen sein (Anfang des Ergebnisses).</summary>
    private const double Nachlauf = 320;

    private ZusatzdatenSeiteViewModel? seite;

    /// <summary>Initialisiert die Seite.</summary>
    public ZusatzdatenSeite()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Verbinden();
    }

    private void Verbinden()
    {
        if (seite is not null)
        {
            seite.PropertyChanged -= Geaendert;
        }

        seite = DataContext as ZusatzdatenSeiteViewModel;
        if (seite is not null)
        {
            seite.PropertyChanged += Geaendert;
        }
    }

    private void Geaendert(object? sender, PropertyChangedEventArgs e)
    {
        Control? ziel = e.PropertyName switch
        {
            nameof(ZusatzdatenSeiteViewModel.AufstellungsErgebnis) when seite?.HatAufstellungen == true => AufstellungsBanner,
            nameof(ZusatzdatenSeiteViewModel.TerminwunschErgebnis) when seite?.HatTerminwuensche == true => TerminwunschBanner,
            _ => null,
        };
        if (ziel is not null)
        {
            // Erst nach dem Layout ist die Meldung sichtbar und hat ihre Größe.
            Dispatcher.UIThread.Post(() => ziel.BringIntoView(new Rect(0, 0, ziel.Bounds.Width, ziel.Bounds.Height + Nachlauf)), DispatcherPriority.Background);
        }
    }
}
