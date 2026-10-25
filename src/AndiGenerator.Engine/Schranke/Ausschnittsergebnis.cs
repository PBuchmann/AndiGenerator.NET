// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Engine.Schranke;

/// <summary>Wie ein Ausschnitt der Teilneuplanung ausgegangen ist.</summary>
public enum Ausschnittsergebnis
{
    /// <summary>HiGHS plant ihn gerade neu.</summary>
    Laeuft,

    /// <summary>Besser nach der echten Bewertung: angenommen.</summary>
    Besser,

    /// <summary>HiGHS hat im Modell nichts Besseres gefunden (oder keine Lösung).</summary>
    NichtsBesseres,

    /// <summary>Im Modell besser, nach der echten Bewertung aber nicht: abgelehnt.</summary>
    Abgelehnt,

    /// <summary>Kein Ausschnitt: Ein besserer Plan von außen (der Inseln) ist neuer Ausgangsplan geworden.</summary>
    Ausgangsplan,
}
