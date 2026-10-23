// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Domain.Optionen;
using AndiGenerator.Engine.Inseln;
using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Engine.Kern;

/// <summary>
/// Zielfunktion der Suche beim Glätten von C im Automodus (statt der Kosten des Originals): Sie zählt wie der Automodus –
/// zuerst die harten Fehler, dann die Verstöße in A, dann in B, dann in C (jeder Verstoß einer Stufe zählt gleich); die Kosten
/// des Originals entscheiden nur bei Gleichstand. So hat die Suche dasselbe Ziel wie der Vergleich der Pläne und wie
/// die Teilneuplanung – mit den Kosten des Originals wanderte sie zu Plänen, die mit den Gewichten billiger sind, aber mehr
/// Verstöße in C haben. Gezählt wird wie im Automodus, auch die Pausen der Spielverteilung und die Spieltage.
/// <para>
/// Mit einer <see cref="Grenze"/> (harte Fehler, A, B des besten Plans) zählt jeder Plan darüber zusätzlich
/// <see cref="Strafe"/>: Die Suche mit später Annahme (<see cref="Spaetannahme"/>) darf in C auch schlechtere Pläne annehmen,
/// A und B aber nie schlechter machen.
/// </para>
/// </summary>
internal sealed class Zaehlziel
{
    /// <summary>So viel zählt ein Plan über der <see cref="Grenze"/> zusätzlich.</summary>
    public const double Strafe = 1e15;

    /// <summary>So viel zählt ein harter Fehler.</summary>
    private const double Hart = 1e12;

    /// <summary>So viel zählt ein Verstoß in A (mehr als jede übliche Zahl von Verstößen in B und C zusammen).</summary>
    private const double JeA = 1e9;

    /// <summary>So viel zählt ein Verstoß in B (mehr als jede übliche Zahl von Verstößen in C).</summary>
    private const double JeB = 1e6;

    /// <summary>Anteil der Kosten des Originals (nur zum Entscheiden bei Gleichstand; bleibt unter einem Verstoß).</summary>
    private const double Gleichstand = 1e-9;

    private readonly IReadOnlyList<Stufe> stufen;

    private (int Harte, int A, int B)? grenze;

    /// <summary>Initialisiert das Ziel.</summary>
    /// <param name="stufen">Stufe je Kriterium (Index = <see cref="Kostenkriterium"/>).</param>
    /// <param name="spaetannahme">Länge des Gedächtnisses der späten Annahme (0 = nur Verbesserungen annehmen).</param>
    public Zaehlziel(IReadOnlyList<Stufe> stufen, int spaetannahme = 0)
    {
        this.stufen = stufen;
        Spaetannahme = spaetannahme;
    }

    /// <summary>
    /// Holt die Länge des Gedächtnisses der späten Annahme (Late Acceptance): Ein Suchplatz nimmt einen Plan an, der nicht
    /// schlechter ist als sein Plan vor so vielen Schritten. 0 = nur Verbesserungen (wie im Original).
    /// </summary>
    public int Spaetannahme { get; }

    /// <summary>
    /// Holt oder setzt die Grenze (harte Fehler, Verstöße in A und in B), über der ein Plan <see cref="Strafe"/> zählt;
    /// <c>null</c> = keine. Gesetzt vom Koordinator (der beste Plan), gelesen von allen Suchplätzen.
    /// </summary>
    public (int Harte, int A, int B)? Grenze
    {
        get
        {
            lock (stufen)
            {
                return grenze;
            }
        }

        set
        {
            lock (stufen)
            {
                grenze = value;
            }
        }
    }

    /// <summary>Der Wert eines Plans nach seiner Kostenrechnung (die Anzahlen je Mannschaft und Art sind dann aktuell).</summary>
    /// <param name="plan">Der Plan.</param>
    /// <param name="kosten">Seine Kosten nach dem Original.</param>
    /// <param name="harte">Seine harten Fehler (Spiele ohne Termin, an spielfreien Tagen, nicht erlaubt).</param>
    /// <returns>Der Wert (kleiner ist besser).</returns>
    public double Wert(KernPlan plan, double kosten, int harte)
    {
        Berechnungsoptionen optionen = plan.D.Optionen;
        int a = 0;
        int b = 0;
        int c = 0;
        for (int art = 0; art < KernDefinition.AnzahlArten; art++)
        {
            // Die Spielverteilung zählt unten mit ihren Pausen (die Kostenrechnung hat für sie keine Anzahl).
            if (optionen.Fuer((MannschaftsKostenart)art) == Gewichtung.NichtBeruecksichtigen || art >= stufen.Count || art == (int)MannschaftsKostenart.Spielverteilung)
            {
                continue;
            }

            int summe = 0;
            for (int t = 0; t < plan.D.N; t++)
            {
                summe += Math.Max(0, plan.KostenAnzahl[(t * KernDefinition.AnzahlArten) + art]);
            }

            switch (stufen[art])
            {
                case Stufe.A:
                    a += summe;
                    break;
                case Stufe.B:
                    b += summe;
                    break;
                default:
                    c += summe;
                    break;
            }
        }

        if (optionen.VereinsinterneSpieleAmAnfang != Gewichtung.NichtBeruecksichtigen && stufen[(int)Kostenkriterium.VereinsinterneSpieleAmAnfang] != Stufe.C)
        {
            a += plan.TeamsNichtAmAnfang();
        }

        // Wie der Automodus (KernPlan.StufenBewerten): Pausen der Spielverteilung und die Spieltage zählen mit.
        (int pausen, int laenge, int ueberlappung, bool letzteLaenge, bool letzteUeberlappung) = plan.ZusatzAnzahlen();
        Zuordnen(Kostenkriterium.Spielverteilung, optionen.Fuer(MannschaftsKostenart.Spielverteilung) != Gewichtung.NichtBeruecksichtigen ? pausen : 0, ref a, ref b, ref c);
        Zuordnen(Kostenkriterium.Spieltaglaenge, optionen.Spieltag != Gewichtung.NichtBeruecksichtigen ? laenge : 0, ref a, ref b, ref c);
        Zuordnen(Kostenkriterium.Spieltagueberlappung, optionen.SpieltagUeberlappung != Gewichtung.NichtBeruecksichtigen ? ueberlappung : 0, ref a, ref b, ref c);
        Zuordnen(Kostenkriterium.LetzterSpieltagLaenge, optionen.LetzterSpieltag != Gewichtung.NichtBeruecksichtigen && letzteLaenge ? 1 : 0, ref a, ref b, ref c);
        Zuordnen(Kostenkriterium.LetzterSpieltagUeberlappung, optionen.LetzterSpieltagUeberlappung != Gewichtung.NichtBeruecksichtigen && letzteUeberlappung ? 1 : 0, ref a, ref b, ref c);

        double wert = (Hart * harte) + (JeA * a) + (JeB * b) + c + Math.Min(0.49, kosten * Gleichstand);
        return Grenze is { } g && (harte, a, b).CompareTo(g) > 0 ? Strafe + wert : wert;
    }

    /// <summary>Zählt Verstöße eines Kriteriums zu seiner Stufe.</summary>
    private void Zuordnen(Kostenkriterium k, int anzahl, ref int a, ref int b, ref int c)
    {
        if ((int)k >= stufen.Count)
        {
            return;
        }

        switch (stufen[(int)k])
        {
            case Stufe.A:
                a += anzahl;
                break;
            case Stufe.B:
                b += anzahl;
                break;
            default:
                c += anzahl;
                break;
        }
    }
}
