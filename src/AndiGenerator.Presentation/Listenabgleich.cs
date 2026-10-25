// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;

namespace AndiGenerator.Presentation;

/// <summary>
/// Gleicht eine angezeigte Liste mit einer neu berechneten ab: Gleiche Einträge bleiben (die Oberfläche baut für sie nichts
/// neu), nur geänderte werden ersetzt. So zeichnet eine Änderung im Raster der Kompaktspieltage nur die betroffenen Zeilen neu
/// statt aller Zeilen, Karten und Spalten (vorher gut eine Viertelsekunde je Eingabe).
/// </summary>
public static class Listenabgleich
{
    /// <summary>Gleicht <paramref name="ziel"/> mit <paramref name="neu"/> ab.</summary>
    /// <typeparam name="T">Art der Einträge.</typeparam>
    /// <param name="ziel">Die angezeigte Liste.</param>
    /// <param name="neu">Die neu berechnete Liste.</param>
    /// <param name="gleich">Ob ein alter Eintrag für den neuen stehen bleiben kann, oder <c>null</c> für <see cref="EqualityComparer{T}.Default"/>.</param>
    /// <param name="behalten">Übernimmt in einen stehen bleibenden alten Eintrag, was sich ohne Neuaufbau ändern darf, oder <c>null</c>.</param>
    /// <returns><c>true</c>, wenn sich etwas geändert hat.</returns>
    public static bool Abgleichen<T>(ObservableCollection<T> ziel, IReadOnlyList<T> neu, Func<T, T, bool>? gleich = null, Action<T, T>? behalten = null)
    {
        ArgumentNullException.ThrowIfNull(ziel);
        ArgumentNullException.ThrowIfNull(neu);
        gleich ??= EqualityComparer<T>.Default.Equals;
        bool geaendert = false;
        int gemeinsam = Math.Min(ziel.Count, neu.Count);
        for (int i = 0; i < gemeinsam; i++)
        {
            if (gleich(ziel[i], neu[i]))
            {
                behalten?.Invoke(ziel[i], neu[i]);
            }
            else
            {
                ziel[i] = neu[i];
                geaendert = true;
            }
        }

        while (ziel.Count > neu.Count)
        {
            ziel.RemoveAt(ziel.Count - 1);
            geaendert = true;
        }

        for (int i = ziel.Count; i < neu.Count; i++)
        {
            ziel.Add(neu[i]);
            geaendert = true;
        }

        return geaendert;
    }
}
