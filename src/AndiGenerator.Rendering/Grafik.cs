// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>
/// Zeichnung (Diagramm, Terminwunschraster) als Baustein. Sie wird als Ganzes auf eine Seite gesetzt und dazu so weit
/// verkleinert, dass sie in die Nutzbreite und auf eine Seite passt.
/// </summary>
/// <param name="Breite">Breite in Zeicheneinheiten.</param>
/// <param name="Hoehe">Höhe in Zeicheneinheiten.</param>
/// <param name="Zeichnung">Zeichnet die Grafik ab (0, 0) auf eine Fläche.</param>
/// <param name="AbstandDavor">Freiraum über der Grafik in Punkt.</param>
public sealed record Grafik(double Breite, double Hoehe, Action<IZeichenflaeche> Zeichnung, double AbstandDavor = 0) : Baustein(AbstandDavor);
