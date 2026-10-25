// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

namespace AndiGenerator.Rendering;

/// <summary>
/// Kürzt Texte (vor allem Mannschaftsnamen) auf eine Breite mit „…“ am Ende, damit sehr lange Namen (z. B. mit
/// Sponsorennamen) weder Spalten sprengen noch über Diagramme laufen. Die volle Fassung steht in Hinweisen und Tabellen
/// mit Zeilenumbruch.
/// </summary>
public static class Textkuerzung
{
    /// <summary>Holt die größte Breite einer Namensspalte in Punkt (Ausdruck, Karte, Diagramme).</summary>
    public const double Namensbreite = 230;

    /// <summary>Kürzt einen Text auf eine Breite.</summary>
    /// <param name="flaeche">Zeichenfläche zum Messen oder <c>null</c> (dann geschätzt).</param>
    /// <param name="text">Der Text.</param>
    /// <param name="breite">Die größte Breite.</param>
    /// <param name="groesse">Schriftgröße in Punkt.</param>
    /// <param name="fett">Fettschrift.</param>
    /// <returns>Der Text, wenn er passt, sonst der längste Anfang mit „…“, der passt (mindestens ein Zeichen).</returns>
    public static string Kuerzen(IZeichenflaeche? flaeche, string text, double breite, double groesse, bool fett)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Kleine Toleranz: Spalten, die genau so breit sind wie ihr Text, bleiben trotz Rundung ungekürzt.
        double Breite(string t) => (flaeche?.TextBreite(t, groesse, fett) ?? Schaetzen(t, groesse)) - 0.01;
        if (Breite(text) <= breite)
        {
            return text;
        }

        // Längster Anfang, der mit „…“ passt (Halbierung); Leerzeichen am Schnitt entfallen.
        int unten = 1;
        int oben = text.Length - 1;
        while (unten < oben)
        {
            int mitte = (unten + oben + 1) / 2;
            if (Breite(Gekuerzt(text, mitte)) <= breite)
            {
                unten = mitte;
            }
            else
            {
                oben = mitte - 1;
            }
        }

        return Gekuerzt(text, Math.Max(1, unten));
    }

    /// <summary>Schätzt die Breite eines Textes ohne Zeichenfläche.</summary>
    /// <param name="text">Der Text.</param>
    /// <param name="groesse">Schriftgröße in Punkt.</param>
    /// <returns>Die geschätzte Breite.</returns>
    public static double Schaetzen(string text, double groesse)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length * groesse * 0.58;
    }

    private static string Gekuerzt(string text, int zeichen) => text[..zeichen].TrimEnd() + "…";
}
