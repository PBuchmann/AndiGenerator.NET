// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;

namespace AndiGenerator.UI.Seiten;

/// <summary>
/// Einheitliche Bedienung der Listen in „Spielplandaten bearbeiten“: Doppelklick oder Eingabetaste bearbeitet den gewählten
/// Eintrag, die Entf-Taste löscht ihn (mit der Rückfrage der Seite).
/// </summary>
internal static class Listenbedienung
{
    /// <summary>Meldet die Bedienung für eine Liste an.</summary>
    /// <param name="liste">Die Liste.</param>
    /// <param name="befehle">Liefert die Befehle „Bearbeiten“ und „Löschen“ der Seite (aus ihrem Datenkontext).</param>
    public static void Anmelden(ListBox liste, Func<(ICommand? Bearbeiten, ICommand? Loeschen)> befehle)
    {
        ArgumentNullException.ThrowIfNull(liste);
        ArgumentNullException.ThrowIfNull(befehle);
        liste.DoubleTapped += (_, _) => Ausfuehren(befehle().Bearbeiten);
        liste.KeyDown += (_, e) =>
        {
            ICommand? befehl = e.Key switch
            {
                Key.Enter => befehle().Bearbeiten,
                Key.Delete => befehle().Loeschen,
                _ => null,
            };
            if (befehl is not null)
            {
                Ausfuehren(befehl);
                e.Handled = true;
            }
        };
    }

    private static void Ausfuehren(ICommand? befehl)
    {
        if (befehl?.CanExecute(null) == true)
        {
            befehl.Execute(null);
        }
    }
}
