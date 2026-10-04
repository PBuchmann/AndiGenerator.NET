// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only

using AndiGenerator.Engine.Referenz;

namespace AndiGenerator.Engine.Inseln;

/// <summary>
/// Einteilung der Kriterien in die Stufen A, B und C für den Automodus; der Staffelleiter legt sie selbst fest (Vorgabe
/// Peter). Die Reihenfolge innerhalb von C ist die Wichtigkeit: Beim Glätten wird beim ersten C-Kriterium begonnen. Nicht
/// genannte Kriterien gehören zu C und kommen nach den genannten in der Reihenfolge von <see cref="Standard"/>. Kriterien ohne
/// Anzahl (Spieltagslänge und -überlappung) gehören immer zu C.
/// </summary>
/// <param name="A">Kriterien der Stufe A.</param>
/// <param name="B">Kriterien der Stufe B.</param>
/// <param name="C">Kriterien der Stufe C, wichtigstes zuerst.</param>
public sealed record Stufeneinteilung(IReadOnlyList<Kostenkriterium> A, IReadOnlyList<Kostenkriterium> B, IReadOnlyList<Kostenkriterium> C)
{
    /// <summary>Anzahl der Kriterien.</summary>
    public static readonly int AnzahlKriterien = Enum.GetValues<Kostenkriterium>().Length;

    /// <summary>
    /// Gets die Einteilung nach E14: A Hallenbelegung, parallele Spiele, Pflichtspieltage, vereinsinterne Spiele am Anfang;
    /// B Auswärts- und Heimkoppel, Sperr- und Ausweichtermine; C nach Wichtigkeit: Heim/Auswärts ausgeglichen, Wechsel,
    /// Abstände, Spielverteilung, übrige, zuletzt die Spieltage.
    /// </summary>
    public static Stufeneinteilung Standard { get; } = new(
        [Kostenkriterium.Hallenbelegung, Kostenkriterium.ParalleleSpiele, Kostenkriterium.Pflichtspieltage, Kostenkriterium.VereinsinterneSpieleAmAnfang],
        [Kostenkriterium.Auswaertskoppel, Kostenkriterium.Heimkoppel, Kostenkriterium.Sperrtermine, Kostenkriterium.Ausweichtermine],
        [
            Kostenkriterium.UngleichHeimAuswaerts, Kostenkriterium.WechselHeimAuswaerts, Kostenkriterium.DreiTageAbstand,
            Kostenkriterium.ZweiSpieleProWoche, Kostenkriterium.Spielverteilung, Kostenkriterium.AbstandHeimAuswaerts,
            Kostenkriterium.GleicheHeimtermine,
            Kostenkriterium.SechzigKilometerRegel, Kostenkriterium.Setzliste, Kostenkriterium.Spieltaglaenge,
            Kostenkriterium.Spieltagueberlappung, Kostenkriterium.LetzterSpieltagLaenge, Kostenkriterium.LetzterSpieltagUeberlappung,
        ]);

    /// <summary>Liest eine Einteilung wie <c>A:Hallenbelegung,ParalleleSpiele;B:Sperrtermine;C:DreiTageAbstand,Spielverteilung</c>.</summary>
    /// <param name="text">Der Text; nicht genannte Kriterien gehören zu C.</param>
    /// <returns>Die Einteilung; <c>null</c>, wenn der Text ungültig ist.</returns>
    public static Stufeneinteilung? Lesen(string text)
    {
        var listen = new List<Kostenkriterium>[] { [], [], [] };
        foreach (string teil in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] paar = teil.Split(':', 2);
            if (paar.Length != 2 || !Enum.TryParse(paar[0].Trim(), true, out Stufe stufe) || !Enum.IsDefined(stufe))
            {
                return null;
            }

            foreach (string name in paar[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Enum.TryParse(name, true, out Kostenkriterium k) || !Enum.IsDefined(k) || listen.Any(l => l.Contains(k)))
                {
                    return null;
                }

                listen[(int)stufe].Add(k);
                if (!HatAnzahl(k) && stufe != Stufe.C)
                {
                    return null;
                }
            }
        }

        return new Stufeneinteilung(listen[0], listen[1], listen[2]).Vervollstaendigt();
    }

    /// <summary>Liest eine gespeicherte Einteilung; leer oder ungültig ergibt den <see cref="Standard"/>.</summary>
    /// <param name="text">Der Text, z. B. aus den Spielplandaten.</param>
    /// <returns>Die vollständige Einteilung.</returns>
    public static Stufeneinteilung AusText(string? text) =>
        string.IsNullOrWhiteSpace(text) ? Standard : (Lesen(text) ?? Standard);

    /// <summary>Kriterien mit einer Anzahl von Verstößen (alle außer Spieltagslänge und -überlappung).</summary>
    /// <param name="k">Das Kriterium.</param>
    /// <returns><c>true</c>, wenn es eine Anzahl hat.</returns>
    public static bool HatAnzahl(Kostenkriterium k) => k <= Kostenkriterium.VereinsinterneSpieleAmAnfang;

    /// <summary>Text zum Speichern, z. B. <c>A:Hallenbelegung,ParalleleSpiele;B:Sperrtermine;C:…</c>; mit <see cref="Lesen"/> lesbar.</summary>
    /// <returns>Der Text.</returns>
    public string Text() => $"A:{string.Join(',', A)};B:{string.Join(',', B)};C:{string.Join(',', Vervollstaendigt().C)}";

    /// <summary>Stufe eines Kriteriums.</summary>
    /// <param name="k">Das Kriterium.</param>
    /// <returns>A, B oder C.</returns>
    public Stufe StufeVon(Kostenkriterium k)
    {
        if (A.Contains(k))
        {
            return Stufe.A;
        }

        return B.Contains(k) ? Stufe.B : Stufe.C;
    }

    /// <summary>
    /// Stuft ein Kriterium um: Rückt es nach unten (A → B, B → C), kommt es an die erste Stelle der neuen Stufe, rückt es
    /// nach oben (C → B, B → A), ans Ende. Kriterien ohne Anzahl (Spieltage) bleiben in C.
    /// </summary>
    /// <param name="k">Das Kriterium.</param>
    /// <param name="stufe">Die neue Stufe.</param>
    /// <returns>Die neue Einteilung (unverändert, wenn nicht möglich).</returns>
    public Stufeneinteilung Einstufen(Kostenkriterium k, Stufe stufe)
    {
        if ((!HatAnzahl(k) && stufe != Stufe.C) || StufeVon(k) == stufe)
        {
            return this;
        }

        bool nachUnten = stufe > StufeVon(k);
        var listen = new[] { A.Where(x => x != k).ToList(), B.Where(x => x != k).ToList(), Vervollstaendigt().C.Where(x => x != k).ToList() };
        listen[(int)stufe].Insert(nachUnten ? 0 : listen[(int)stufe].Count, k);
        return new Stufeneinteilung(listen[0], listen[1], listen[2]);
    }

    /// <summary>
    /// Setzt ein Kriterium direkt vor oder hinter ein anderes (Ziehen und Ablegen): Es übernimmt dessen Stufe. Kriterien
    /// ohne Anzahl (Spieltage) bleiben in C.
    /// </summary>
    /// <param name="k">Das Kriterium.</param>
    /// <param name="nachbar">Das Kriterium, neben das es kommt.</param>
    /// <param name="davor"><c>true</c> = davor, sonst dahinter.</param>
    /// <returns>Die neue Einteilung (unverändert, wenn nicht möglich).</returns>
    public Stufeneinteilung Platzieren(Kostenkriterium k, Kostenkriterium nachbar, bool davor)
    {
        Stufeneinteilung voll = Vervollstaendigt();
        Stufe ziel = voll.StufeVon(nachbar);
        if (k == nachbar || (!HatAnzahl(k) && ziel != Stufe.C))
        {
            return this;
        }

        var listen = new[] { voll.A.Where(x => x != k).ToList(), voll.B.Where(x => x != k).ToList(), voll.C.Where(x => x != k).ToList() };
        List<Kostenkriterium> liste = listen[(int)ziel];
        int index = liste.IndexOf(nachbar);
        liste.Insert(davor ? index : index + 1, k);
        return new Stufeneinteilung(listen[0], listen[1], listen[2]);
    }

    /// <summary>Position eines Kriteriums innerhalb seiner Stufe (0 = wichtigstes).</summary>
    /// <param name="k">Das Kriterium.</param>
    /// <returns>Die Position.</returns>
    public int Position(Kostenkriterium k)
    {
        Stufeneinteilung voll = Vervollstaendigt();
        return voll.StufeVon(k) switch
        {
            Stufe.A => voll.A.ToList().IndexOf(k),
            Stufe.B => voll.B.ToList().IndexOf(k),
            _ => voll.C.ToList().IndexOf(k),
        };
    }

    /// <summary>Verschiebt ein Kriterium innerhalb seiner Stufe um eine Stelle nach oben (−1) oder unten (+1).</summary>
    /// <param name="k">Das Kriterium.</param>
    /// <param name="richtung">−1 oder +1.</param>
    /// <returns>Die neue Einteilung (unverändert am Rand der Stufe).</returns>
    public Stufeneinteilung Verschieben(Kostenkriterium k, int richtung)
    {
        Stufeneinteilung voll = Vervollstaendigt();
        var listen = new[] { voll.A.ToList(), voll.B.ToList(), voll.C.ToList() };
        List<Kostenkriterium> liste = listen[(int)voll.StufeVon(k)];
        int alt = liste.IndexOf(k);
        int neu = alt + Math.Sign(richtung);
        if (alt < 0 || neu < 0 || neu >= liste.Count)
        {
            return this;
        }

        (liste[alt], liste[neu]) = (liste[neu], liste[alt]);
        return new Stufeneinteilung(listen[0], listen[1], listen[2]);
    }

    /// <summary>Kriterien mit einer Anzahl von Verstößen (alle außer Spieltagslänge und -überlappung).</summary>
    /// <returns>Stufe je Kriterium, Index = Enumwert.</returns>
    public Stufe[] AlsFeld()
    {
        var stufen = new Stufe[AnzahlKriterien];
        Array.Fill(stufen, Stufe.C);
        foreach (Kostenkriterium k in A)
        {
            stufen[(int)k] = Stufe.A;
        }

        foreach (Kostenkriterium k in B)
        {
            stufen[(int)k] = Stufe.B;
        }

        return stufen;
    }

    /// <summary>
    /// Zählt die Verstöße einer Bewertung nach dieser Einteilung (wie der Automodus): Anzahlen je Mannschafts-Kostenart über
    /// alle Mannschaften, vereinsinterne Spiele am Anfang als 1; Spieltagslänge und -überlappung zählen nur bei den C-Kosten.
    /// </summary>
    /// <param name="bewertung">Die Bewertung eines Plans.</param>
    /// <returns>Harte Fehler, Verstöße A, B und C, Kosten der Stufe C (ohne Ausreißer).</returns>
    public Stufenwert Zaehlen(Planbewertung bewertung)
    {
        ArgumentNullException.ThrowIfNull(bewertung);
        Stufe[] stufen = AlsFeld();
        double[] anzahl = new double[AnzahlKriterien];
        double[] kosten = new double[AnzahlKriterien];
        for (int k = 0; k < (int)Kostenkriterium.VereinsinterneSpieleAmAnfang; k++)
        {
            anzahl[k] = bewertung.Mannschaften.Sum(m => Math.Max(0, m.JeKostenart[k].Anzahl));
            kosten[k] = bewertung.Mannschaften.Sum(m => m.JeKostenart[k].Kosten);
        }

        anzahl[(int)Kostenkriterium.VereinsinterneSpieleAmAnfang] = bewertung.VereinsinterneSpieleAmAnfang > 0 ? 1 : 0;
        kosten[(int)Kostenkriterium.VereinsinterneSpieleAmAnfang] = bewertung.VereinsinterneSpieleAmAnfang;
        kosten[(int)Kostenkriterium.Spieltaglaenge] = bewertung.LaengeSpieltage;
        kosten[(int)Kostenkriterium.Spieltagueberlappung] = bewertung.UeberlappungSpieltage;
        kosten[(int)Kostenkriterium.LetzterSpieltagLaenge] = bewertung.LaengeLetzterSpieltag;
        kosten[(int)Kostenkriterium.LetzterSpieltagUeberlappung] = bewertung.UeberlappungLetzterSpieltag;

        double[] jeStufe = new double[3];
        double c = 0;
        for (int k = 0; k < AnzahlKriterien; k++)
        {
            jeStufe[(int)stufen[k]] += anzahl[k];
            c += stufen[k] == Stufe.C ? kosten[k] : 0;
        }

        int harte = (int)Math.Round((bewertung.NichtTerminiert + bewertung.UngueltigeSpiele + bewertung.SpieleAnSpielfreienTagen) / (10.0 * RefPlan.MaxKostenOhneHartenFehler));
        return new Stufenwert(harte, (int)jeStufe[0], (int)jeStufe[1], 0, (int)jeStufe[2], c);
    }

    /// <summary>Alle Kriterien der Stufe C nach Wichtigkeit: die genannten, dann die übrigen in der Reihenfolge des Standards.</summary>
    /// <returns>Die Einteilung mit vollständiger C-Liste.</returns>
    public Stufeneinteilung Vervollstaendigt()
    {
        IEnumerable<Kostenkriterium> rest = Standard.C.Concat(Standard.A).Concat(Standard.B).Concat(Enum.GetValues<Kostenkriterium>());
        List<Kostenkriterium> c = C.Concat(rest).Distinct().Where(k => !A.Contains(k) && !B.Contains(k)).ToList();
        return this with { C = c };
    }
}
