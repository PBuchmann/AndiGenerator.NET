// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>Seitengröße und Ränder in Punkt.</summary>
/// <param name="Breite">Seitenbreite.</param>
/// <param name="Hoehe">Seitenhöhe.</param>
/// <param name="Rand">Rand an allen Seiten.</param>
public sealed record Seitenformat(double Breite, double Hoehe, double Rand)
{
    /// <summary>Höhe der Kopfzeile (Abschnittstitel mit Linie).</summary>
    public const double Kopfhoehe = 34;

    /// <summary>Höhe der Fußzeile (Staffel, Plan, Datum, Seitenzahl).</summary>
    public const double Fusshoehe = 18;

    /// <summary>Holt A4 im Hochformat.</summary>
    public static Seitenformat A4Hoch { get; } = new(595.28, 841.89, 36);

    /// <summary>Holt A4 im Querformat.</summary>
    public static Seitenformat A4Quer { get; } = new(841.89, 595.28, 36);

    /// <summary>Holt die nutzbare Breite.</summary>
    public double Nutzbreite => Breite - (2 * Rand);

    /// <summary>Holt die Oberkante des Inhalts.</summary>
    public double InhaltOben => Rand + Kopfhoehe;

    /// <summary>Holt die Unterkante des Inhalts.</summary>
    public double InhaltUnten => Hoehe - Rand - Fusshoehe;
}
