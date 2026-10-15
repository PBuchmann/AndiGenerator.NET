// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Media;

namespace AndiGenerator.UI;

/// <summary>Farben aus der Gestaltung (Stil.axaml) für Steuerelemente, die selbst zeichnen.</summary>
internal static class Pinsel
{
    /// <summary>Die Farbe mit diesem Namen aus den Ressourcen der Anwendung (grau, wenn es sie nicht gibt).</summary>
    /// <param name="name">Name der Ressource, z. B. <c>Akzent</c>.</param>
    /// <returns>Der Pinsel.</returns>
    public static IBrush Farbe(string name) =>
        global::Avalonia.Application.Current?.TryFindResource(name, out object? wert) == true && wert is IBrush b ? b : Brushes.Gray;

    /// <summary>Dieselbe Farbe mit der Deckkraft <paramref name="deckkraft"/> (0 bis 1).</summary>
    /// <param name="farbe">Die Farbe.</param>
    /// <param name="deckkraft">Deckkraft.</param>
    /// <returns>Der Pinsel.</returns>
    public static IBrush Mit(IBrush farbe, double deckkraft) =>
        farbe is ISolidColorBrush s ? new SolidColorBrush(s.Color, Math.Clamp(deckkraft, 0, 1)) : farbe;
}
