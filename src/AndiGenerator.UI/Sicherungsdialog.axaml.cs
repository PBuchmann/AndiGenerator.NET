// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Presentation;
using Avalonia.Controls;

namespace AndiGenerator.UI;

/// <summary>
/// Dialog „Plan sichern“: Bevor ein Plan durch einen Neustart oder das Schließen verloren ginge und es schon einen
/// gesicherten Plan gibt, zeigt er beide im Vergleich und fragt, welcher bleiben soll.
/// </summary>
public partial class Sicherungsdialog : Window
{
    /// <summary>Initialisiert den Dialog (für den XAML-Designer).</summary>
    public Sicherungsdialog()
    {
        InitializeComponent();
    }

    /// <summary>Initialisiert den Dialog.</summary>
    /// <param name="vergleich">Der Vergleich der beiden Pläne.</param>
    public Sicherungsdialog(Planvergleich vergleich)
        : this()
    {
        DataContext = vergleich;
        Ueberschreiben.Click += (_, _) => Close(Sicherungswahl.Ueberschreiben);
        Behalten.Click += (_, _) => Close(Sicherungswahl.Behalten);
        Abbrechen.Click += (_, _) => Close(Sicherungswahl.Abbrechen);
    }
}
