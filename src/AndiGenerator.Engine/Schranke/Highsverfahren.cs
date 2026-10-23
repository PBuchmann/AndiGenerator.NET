// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Schranke;

/// <summary>Die Verfahren im Vergleich mit HiGHS (siehe <see cref="Ligaschranke.Vergleichen"/>).</summary>
public enum Highsverfahren
{
    /// <summary>Wie die Untergrenze heute: erst A, dann B, je mindestens 20 s und weiter, solange es vorangeht (höchstens 3 min).</summary>
    Kette,

    /// <summary>
    /// Wie <see cref="Kette"/>, aber Stufe A mindestens 60 s und erst nach 30 s ohne Fortschritt zu Ende (die Kette brach A nach
    /// 29 s mit A 66 ab, sieben Sekunden später hätte HiGHS A 10 gehabt).
    /// </summary>
    KetteGeduldig,

    /// <summary>Wie <see cref="Kette"/>, aber ohne Stillstandsregel: B rechnet bis zum Ende.</summary>
    KetteLang,

    /// <summary>A gleich nach A1 auf das Ziel begrenzt (ohne Beweis von A), dann ein einziger Lauf für B bis zum Ende.</summary>
    Direkt,

    /// <summary>Wie <see cref="Direkt"/>, aber mehrere lange Läufe gleichzeitig (verschiedene Startwerte und Heuristiken).</summary>
    Portfolio,

    /// <summary>Kurzer Lauf wie <see cref="Direkt"/> für einen Startplan, dann Teilneuplanung auf allen Läufen gleichzeitig.</summary>
    Teilneuplanung,

    /// <summary>
    /// Ein langer Lauf wie <see cref="Direkt"/> (meldet jede bessere Lösung sofort), gleichzeitig Stufe A und danach
    /// Teilneuplanung auf den übrigen Läufen, alle am selben besten Plan.
    /// </summary>
    Kombination,

    /// <summary>
    /// Mehrere lange Läufe wie <see cref="Direkt"/> (Linien), gleichzeitig Stufe A; die übrigen Läufe planen je einer Linie
    /// Ausschnitte neu – so werden auch Pläne aus anderen Gegenden verfeinert, nicht nur der beste.
    /// </summary>
    Mehrfach,
}
